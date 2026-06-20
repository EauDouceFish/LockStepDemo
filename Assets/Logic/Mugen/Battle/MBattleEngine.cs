// 战斗引擎装配器：把已移植的子系统按每帧顺序串成 live pipeline（M9 整合的核心）。
// 顺序对齐 CLAUDE §8.3：命令 → 状态机 → 物理 → 动画(M8) → 命中。无静态可变态：
// 全部运行态在 MChar 上，ComputeHash 覆盖全角色 → 接快照/回滚(M10)。See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.Battle
{
    /// <summary>
    /// 一场对局的运行期：N 个角色 + 各自配置，每 tick 推进一帧。
    /// 物理目前为最小积分（pos += vel，X 乘朝向）——重力/摩擦/地面夹取属 M9 物理完善项，已标注。
    /// </summary>
    public sealed class MBattleEngine
    {
        public readonly List<MChar> Chars = new List<MChar>();
        public readonly List<MCharData> Data = new List<MCharData>();
        public readonly MPlayerResourceRegistry Resources = new MPlayerResourceRegistry();

        readonly MStateMachine _stateMachine = new MStateMachine();

        // 确定性随机源（= Ikemen 单一全局 sys.randseed）：全场角色共享，random trigger 推进它。
        // 固定默认种子保证回放/双端逐位一致；联机时由对局配置同步初始种子。模拟状态 → 纳入 ComputeHash。
        public readonly MRandom Random = new MRandom(DefaultRandomSeed);
        const int DefaultRandomSeed = 1;

        // 全局暂停态（= Ikemen sys.pausetime/supertime + buffer/playerno）：全场角色共享，控制器经 SetPause 写。
        public readonly MPauseState PauseState = new MPauseState();

        // R-ENT 实体世界（共享 spawn 通道 + id 分配 + helper 列表）。_helperData 与 World.Helpers 平行（引擎侧配置）。
        public readonly MEntityWorld World = new MEntityWorld();

        // R-STAGE-minimal：决斗场左右边界（默认关闭=虚空，决斗场表现层开启）。
        public readonly MStage Stage = new MStage();

        // 回合结算窗口禁伤（= Ikemen roundNoDamage）：命中仍登记（受击/硬直），但不扣血。
        // 由 MRoundSystem 每帧据 intro 相位设置；无回合系统时恒 false（行为同旧）。
        public bool NoDamage;

        // Demo-only facing fallback. Full Ikemen/MUGEN fidelity should rely on common state 5 +
        // Turn controllers; this switch lets strict tests or future competitive modes disable it.
        public bool EnableDemoAutoTurnFallback = true;

        public List<MChar> Helpers => World.Helpers;
        readonly List<MCharData> _helperData = new List<MCharData>();
        public int FrameNo { get; private set; }

        // Project-specific: registers C# runtime players/resources before Ikemen-style per-frame logic can run.
        public void Add(MChar c, MCharData data)
        {
            int playerNo = Resources.Register(data);
            c.Resources = Resources;
            c.OwnData = data;
            c.PlayerNo = playerNo;
            c.StatePlayerNo = playerNo;
            c.AnimPlayerNo = playerNo;
            c.SpritePlayerNo = playerNo;
            c.Rng = Random;       // 接入共享随机源（对齐 Ikemen 全局种子）
            c.Pause = PauseState; // 接入共享暂停态（对齐 Ikemen 全局 sys.pause）
            c.World = World;      // 接入实体世界（helper spawn 通道）
            ApplyPhysicsDefaults(c);
            Chars.Add(c);
            Data.Add(data);
        }

        // 找某实体对应的 MCharData（玩家在 Data、helper 在 _helperData）。helper 用 owner 的角色数据。
        // Project-specific: maps C# runtime entities back to immutable character data; Ikemen stores this on Char.
        MCharData DataOf(MChar c)
        {
            for (int i = 0; i < Chars.Count; i++)
            {
                if (ReferenceEquals(Chars[i], c)) { return Data[i]; }
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                if (ReferenceEquals(Helpers[i], c)) { return _helperData[i]; }
            }
            return null;
        }

        // 排空 spawn 队列：据请求造 helper 实体（移植 Helper 控制器 + char.go newHelper）。新 helper 下一帧起跑。
        // Ikemen reference: src/char.go newHelper plus projectile controller spawn paths; queued here for deterministic C# tick order.
        void DrainSpawns()
        {
            for (int q = 0; q < World.SpawnQueue.Count; q++)
            {
                MHelperRequest req = World.SpawnQueue[q];
                MCharData ownerData = DataOf(req.Owner);
                if (ownerData == null) { continue; }
                MChar helper = MCharLoader.SpawnChar(ownerData, World.AllocId(),
                    startStateNo: req.StateNo, startAnimNo: 0);
                helper.IsHelper = true;
                helper.HelperType = req.HelperType;
                helper.Parent = req.Owner;
                helper.Root = req.Owner.Root ?? req.Owner;
                helper.P2 = req.Owner.P2;
                helper.Resources = Resources;
                helper.OwnData = ownerData;
                helper.PlayerNo = req.Owner.PlayerNo;
                helper.StatePlayerNo = req.Owner.PlayerNo;
                helper.AnimPlayerNo = req.Owner.PlayerNo;
                helper.SpritePlayerNo = req.Owner.PlayerNo;
                helper.Rng = Random;
                helper.Pause = PauseState;
                helper.World = World;
                helper.KeyCtrl = req.KeyCtrl;
                ApplyPhysicsDefaults(helper);
                // postype p1 相对：朝向继承 owner×req.Facing，位置 = owner.Pos + 朝向相对偏移（X 乘 owner 朝向）。
                int facingSign = req.Owner.Facing.Raw >= 0 ? 1 : -1;
                helper.Facing = FFloat.FromInt(facingSign * (req.Facing >= 0 ? 1 : -1));
                helper.Pos = new FVector3(
                    req.Owner.Pos.X + req.PosX * FFloat.FromInt(facingSign),
                    req.Owner.Pos.Y + req.PosY, FFloat.Zero);
                Helpers.Add(helper);
                _helperData.Add(ownerData);
            }
            World.SpawnQueue.Clear();

            for (int q = 0; q < World.ProjSpawnQueue.Count; q++)
            {
                MProjectileRequest req = World.ProjSpawnQueue[q];
                if (req.Owner == null) { continue; }
                int facingSign = req.Owner.Facing.Raw >= 0 ? 1 : -1;
                MCharData ownerData = DataOf(req.Owner);
                Hit.MClsnBox[] clsn1 = ProjAnimClsn1(ownerData, req.AnimNo);
                MChar projectileOwner = req.Owner.Root ?? req.Owner.Parent ?? req.Owner;
                World.Projectiles.Add(new MProjectile
                {
                    Id = World.AllocId(), OwnerId = projectileOwner.Id, ProjId = req.ProjId,
                    Facing = FFloat.FromInt(facingSign),
                    Vel = new FVector3(req.VelX, req.VelY, FFloat.Zero),
                    Accel = new FVector3(req.AccelX, req.AccelY, FFloat.Zero),
                    Pos = new FVector3(req.Owner.Pos.X + req.PosX * FFloat.FromInt(facingSign),
                        req.Owner.Pos.Y + req.PosY, FFloat.Zero),
                    RemoveTime = req.RemoveTime, AnimNo = req.AnimNo,
                    HitDef = req.HitDef, Owner = projectileOwner, Clsn1 = clsn1,
                });
            }
            World.ProjSpawnQueue.Clear();
        }

        // 弹幕 projanim 首帧的攻击框 Clsn1（弹幕攻击框来源）。无表/无帧 → null。
        // Project-specific: C# projectile hit checks need first-frame Clsn1 cached from projanim; Ikemen keeps this in Anim.
        static Hit.MClsnBox[] ProjAnimClsn1(MCharData data, int animNo)
        {
            if (data != null && data.Anims != null && data.Anims.TryGetValue(animNo, out Anim.MAnimData anim)
                && anim.Frames != null && anim.Frames.Length > 0)
            {
                return anim.Frames[0].Clsn1;
            }
            return null;
        }

        // 推进所有弹幕实体（运动 + 命中敌队 + 生命周期移除）。
        // Ikemen reference: src/char.go projectile update/contact handling; C# folds movement, contact, and lifetime into MProjectile.Step.
        void StepProjectiles()
        {
            for (int i = 0; i < World.Projectiles.Count; i++)
            {
                MProjectile proj = World.Projectiles[i];
                if (proj.Removed) { continue; }
                proj.Step();
                // 命中敌队玩家（移植弹幕命中：用弹幕几何+HitDef，效果走 owner）。单段弹幕命中即移除。
                if (!proj.HitDone && proj.HitDef != null && proj.Owner != null)
                {
                    for (int c = 0; c < Chars.Count; c++)
                    {
                        MChar target = Chars[c];
                        if (TeamOf(target) == TeamOf(proj.Owner)) { continue; }
                        if (MHitSystem.TryProjectileHit(proj, target, deferDamage: true))
                        {
                            proj.HitDone = true;
                            proj.Removed = true;
                            break;
                        }
                    }
                }
            }
            for (int i = World.Projectiles.Count - 1; i >= 0; i--)
            {
                if (World.Projectiles[i].Removed) { World.Projectiles.RemoveAt(i); }
            }
        }

        // 移除 DestroySelf 标记的 helper（帧末）。
        // Ikemen reference: src/char.go helper destroy/removal after DestroySelf; C# removes marked helper entities at frame end.
        void RemoveDestroyed()
        {
            for (int i = Helpers.Count - 1; i >= 0; i--)
            {
                if (Helpers[i].Destroyed)
                {
                    Helpers.RemoveAt(i);
                    _helperData.RemoveAt(i);
                }
            }
        }

        /// <summary>1v1：互设 P2/Root，便于 redirect(p2) 与命中。</summary>
        // Project-specific: links 1v1 redirect references (P2/Root) for the C# harness; Ikemen builds these through CharList/system setup.
        public void LinkPair()
        {
            for (int i = 0; i < Chars.Count; i++)
            {
                Chars[i].Root = Chars[i];
                Chars[i].P2 = Chars.Count >= 2 ? Chars[1 - i] : null;
            }
        }

        /// <summary>
        /// 回合开始：给玩家控制角色授予按键控制权 + ctrl（faithful shim，对应 Ikemen RoundState 进入战斗活动期）。
        /// 完整回合状态机（intro/5900/fight/KO）后置；此处只把"该读键且有控制权"的初始态摆正，
        /// 否则直接 spawn 进 state 0 的角色 ctrl=false → 引擎硬编码基础动作（走/跳/蹲）全被挡。
        /// </summary>
        // Project-specific shim: grants keyctrl/ctrl for live tests before the full Ikemen round flow is wired.
        public void StartRound()
        {
            for (int i = 0; i < Chars.Count; i++)
            {
                Chars[i].KeyCtrl = true;
                Chars[i].Ctrl = true;
            }
        }

        // Project-specific rollback snapshot; Ikemen has no C# snapshot API, but captured fields mirror simulation state.
        public MBattleEngineSnapshot Snapshot()
        {
            MBattleEngineSnapshot snapshot = new MBattleEngineSnapshot
            {
                FrameNo = FrameNo,
                RandomSeed = Random.Seed,
                Pause = PauseState.Clone(),
                NextEntityId = World.NextEntityId,
                Stage = Stage.Clone(),
                NoDamage = NoDamage,
                EnableDemoAutoTurnFallback = EnableDemoAutoTurnFallback,
            };
            for (int i = 0; i < Chars.Count; i++)
            {
                snapshot.Chars.Add(Chars[i].Clone());
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                snapshot.Helpers.Add(Helpers[i].Clone());
            }
            for (int i = 0; i < World.Projectiles.Count; i++)
            {
                snapshot.Projectiles.Add(World.Projectiles[i].Clone());
            }
            for (int i = 0; i < World.Explods.Count; i++)
            {
                snapshot.Explods.Add(World.Explods[i].Clone());
            }
            for (int i = 0; i < World.SpawnQueue.Count; i++)
            {
                snapshot.SpawnQueue.Add(CloneRequest(World.SpawnQueue[i]));
            }
            for (int i = 0; i < World.ProjSpawnQueue.Count; i++)
            {
                snapshot.ProjectileSpawnQueue.Add(CloneRequest(World.ProjSpawnQueue[i]));
            }
            return snapshot;
        }

        // Project-specific rollback restore; relinks Ikemen-style redirect/target/helper ownership after cloning.
        public void Restore(MBattleEngineSnapshot snapshot)
        {
            Chars.Clear();
            Helpers.Clear();
            _helperData.Clear();
            World.Projectiles.Clear();
            World.Explods.Clear();
            World.SpawnQueue.Clear();
            World.ProjSpawnQueue.Clear();

            FrameNo = snapshot.FrameNo;
            Random.Seed = snapshot.RandomSeed;
            CopyPause(snapshot.Pause, PauseState);
            World.NextEntityId = snapshot.NextEntityId;
            Stage.CopyFrom(snapshot.Stage);
            NoDamage = snapshot.NoDamage;
            EnableDemoAutoTurnFallback = snapshot.EnableDemoAutoTurnFallback;

            Dictionary<int, MChar> map = new Dictionary<int, MChar>();
            for (int i = 0; i < snapshot.Chars.Count; i++)
            {
                MChar clone = snapshot.Chars[i].Clone();
                AttachSharedState(clone, ResourceForPlayer(clone.PlayerNo));
                Chars.Add(clone);
                map[clone.Id] = clone;
            }
            for (int i = 0; i < snapshot.Helpers.Count; i++)
            {
                MChar clone = snapshot.Helpers[i].Clone();
                MCharData data = ResourceForPlayer(clone.PlayerNo) ?? clone.OwnData;
                AttachSharedState(clone, data);
                Helpers.Add(clone);
                _helperData.Add(data);
                map[clone.Id] = clone;
            }

            for (int i = 0; i < Chars.Count; i++)
            {
                RelinkChar(Chars[i], snapshot.Chars[i], map);
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                RelinkChar(Helpers[i], snapshot.Helpers[i], map);
            }

            for (int i = 0; i < snapshot.Projectiles.Count; i++)
            {
                MProjectile projectile = snapshot.Projectiles[i].Clone();
                if (map.TryGetValue(projectile.OwnerId, out MChar owner))
                {
                    projectile.Owner = owner;
                }
                World.Projectiles.Add(projectile);
            }
            for (int i = 0; i < snapshot.Explods.Count; i++)
            {
                World.Explods.Add(snapshot.Explods[i].Clone());
            }
            for (int i = 0; i < snapshot.SpawnQueue.Count; i++)
            {
                MHelperRequest request = CloneRequest(snapshot.SpawnQueue[i]);
                request.Owner = Remap(request.Owner, map);
                World.SpawnQueue.Add(request);
            }
            for (int i = 0; i < snapshot.ProjectileSpawnQueue.Count; i++)
            {
                MProjectileRequest request = CloneRequest(snapshot.ProjectileSpawnQueue[i]);
                request.Owner = Remap(request.Owner, map);
                World.ProjSpawnQueue.Add(request);
            }
        }

        /// <summary>推进一帧。inputs[i] 对应 Chars[i] 本帧输入。
        /// 顺序对齐 Ikemen 每帧：输入缓冲 → actionPrepare(硬编码基础动作) → actionRun(状态机) → update(物理/动画) → 命中。
        /// Pause/SuperPause（移植 system.go super/pause 全局冻结）：暂停期间被冻结角色跳过本帧全部处理，
        /// 仅施暂停方在其 movetime 窗口内可动；SuperPause 优先于 Pause。命中在暂停期间不结算。</summary>
        // Ikemen reference: src/char.go actionPrepare/actionRun/update + src/system.go pause progression; C# orders the same phases explicitly.
        public void Tick(IReadOnlyList<MInput> inputs)
        {
            World.Events.BeginFrame(FrameNo);
            // 0) 全局暂停推进（移植 system.go:2562）：递减 super/pause 时长 + 应用 buffer（上一帧控制器设的暂停此刻生效）。
            //    再对每角色算 PauseBool/Acttmp（移植 char.go:11421 + 11524 movetime 递减）。无暂停时 PauseBool 全 false → 与原逐位一致。
            PauseState.Step();
            for (int i = 0; i < Chars.Count; i++) { Chars[i].ComputePauseBool(); }
            for (int i = 0; i < Helpers.Count; i++) { Helpers[i].ComputePauseBool(); }
            bool[] charHitpause = SnapshotHitpause(Chars);
            bool[] helperHitpause = SnapshotHitpause(Helpers);
            ApplyHitpauseActtmp(Chars, charHitpause);
            ApplyHitpauseActtmp(Helpers, helperHitpause);
            for (int i = 0; i < Chars.Count; i++) { StepProjectileContactTime(Chars[i]); }
            for (int i = 0; i < Helpers.Count; i++) { StepProjectileContactTime(Helpers[i]); }
            bool anyPause = PauseState.AnyActive;

            // 1) 输入缓冲（仅玩家；helper 不读键）。
            for (int i = 0; i < Chars.Count; i++)
            {
                if (Chars[i].PauseBool) { continue; }
                MChar c = Chars[i];
                MInput input = inputs != null && i < inputs.Count ? inputs[i] : MInput.None;
                bool facingRight = c.Facing.Raw >= 0;
                if (c.CommandList != null) { c.CommandList.Update(input, facingRight); }
                if (c.Input != null) { c.Input.Update(input, facingRight); }
            }

            // 2) actionPrepare（硬编码基础动作；helper keyctrl=false 内部自然不动作，仍统一调用）。
            //    先每帧重置绘制态（移植 char.go:11542，在状态控制器运行前；anglerot/sprPriority/winquote 跨帧保留）。
            for (int i = 0; i < Chars.Count; i++)
            {
                if (!Chars[i].PauseBool)
                {
                    ApplyPhysicsDefaults(Chars[i]);
                    Chars[i].ResetFrameDrawState();
                    if (!charHitpause[i])
                    {
                        MActionSystem.Prepare(Chars[i]);
                    }
                }
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                if (!Helpers[i].PauseBool)
                {
                    ApplyPhysicsDefaults(Helpers[i]);
                    Helpers[i].ResetFrameDrawState();
                    if (!helperHitpause[i])
                    {
                        MActionSystem.Prepare(Helpers[i]);
                    }
                }
            }

            // 3) 状态机（玩家 + helper）。自定义状态(投技)：StateOwner 非 null → 跑该角色的状态表。
            for (int i = 0; i < Chars.Count; i++)
            {
                MChar c = Chars[i];
                if (!c.PauseBool) { MCharData sd = StateDataFor(c, Data[i]); _stateMachine.RunFrame(c, sd.States, sd.CommonStates); }
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                MChar c = Helpers[i];
                if (!c.PauseBool) { MCharData sd = StateDataFor(c, _helperData[i]); _stateMachine.RunFrame(c, sd.States, sd.CommonStates); }
            }

            // 4) 物理 + 落地检测（玩家 + helper）。PosFreeze 跳积分后清零。
            for (int i = 0; i < Chars.Count; i++) { StepPhysics(Chars[i], charHitpause[i]); }
            for (int i = 0; i < Helpers.Count; i++) { StepPhysics(Helpers[i], helperHitpause[i]); }
            ResolvePlayerPush(charHitpause, helperHitpause);
            ClearPosFreeze();

            // Demo fallback turn after physics. State logic computes local velocity against the
            // facing sampled at input time, and MPhysics applies vel.x * facing. Flipping before
            // physics reinterprets the same-frame walk direction and causes crossing jitter.
            if (EnableDemoAutoTurnFallback)
            {
                for (int i = 0; i < Chars.Count; i++)
                {
                    if (!Chars[i].PauseBool && !charHitpause[i]) { MActionSystem.AutoTurn(Chars[i]); }
                }
            }

            // 5) 动画推进（玩家 + helper）。
            for (int i = 0; i < Chars.Count; i++)
            {
                if (!Chars[i].PauseBool && !charHitpause[i]) { MAnimSystem.Action(Chars[i], Chars[i].CurrentAnimTable()); }
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                if (!Helpers[i].PauseBool && !helperHitpause[i]) { MAnimSystem.Action(Helpers[i], Helpers[i].CurrentAnimTable()); }
            }

            // 5b) 排空 spawn 队列（本帧状态机里 Helper/Projectile 控制器请求的实体，下帧起跑）+ 推进现有弹幕。
            DrainSpawns();
            if (!anyPause) { StepProjectiles(); }
            if (!anyPause) { World.StepExplods(ownerId => IsEntityHitpause(ownerId, charHitpause, helperHitpause)); }

            // 6) 命中：全 MChar 实体（玩家 + helper）跨队两两尝试。暂停期间不结算。
            //    队伍 = Root.Id（helper 继承 owner 的 Root）；只在不同队之间判定，避免打自己人。
            RunHits(charHitpause, helperHitpause);
            FlushPendingDamage();

            // 7) 移除 DestroySelf 的 helper。
            RemoveDestroyed();
            FrameNo++;
        }

        // Ikemen reference: src/char.go projectile contact trigger timers pcid/pctype/pctime.
        static void StepProjectileContactTime(MChar character)
        {
            if (character.ProjectileContactTime >= 0)
            {
                character.ProjectileContactTime++;
            }
        }

        // 队伍归属（移植 Ikemen teamside 简化）：Root 玩家的 id（0/1）。helper.Root=owner 玩家 → 与 owner 同队。
        // Project-specific helper: approximates Ikemen team/teamside ownership for player/helper hit filtering.
        static int TeamOf(MChar c)
        {
            return c.Root != null ? c.Root.Id : c.Id;
        }

        // 本角色本帧用哪张状态表：StateOwner 非 null(投技自定义状态)→其角色数据；否则自身数据(fallback)。
        // Ikemen reference: src/char.go custom state ownership; C# resolves StatePlayerNo/StateOwner to the right state table.
        MCharData StateDataFor(MChar c, MCharData own)
        {
            MCharData byOwnerId = Resources.Get(c.StatePlayerNo);
            if (byOwnerId != null) { return byOwnerId; }
            if (c.StateOwner != null && !ReferenceEquals(c.StateOwner, c))
            {
                MCharData ownerData = DataOf(c.StateOwner);
                if (ownerData != null) { return ownerData; }
            }
            return own;
        }

        // 全实体跨队命中：把玩家与 helper 合到一个列表，攻防两两尝试（TryHit 内部做 active/重叠/同招一次判定）。
        readonly List<MChar> _hitEntities = new List<MChar>();
        readonly List<bool> _hitEntityHitpause = new List<bool>();
        // Ikemen reference: src/char.go HitDef contact resolution; C# enumerates all player/helper attacker-defender pairs.
        void RunHits(bool[] charHitpause, bool[] helperHitpause)
        {
            _hitEntities.Clear();
            _hitEntityHitpause.Clear();
            for (int i = 0; i < Chars.Count; i++)
            {
                _hitEntities.Add(Chars[i]);
                _hitEntityHitpause.Add(charHitpause != null && i < charHitpause.Length && charHitpause[i]);
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                _hitEntities.Add(Helpers[i]);
                _hitEntityHitpause.Add(helperHitpause != null && i < helperHitpause.Length && helperHitpause[i]);
            }
            for (int a = 0; a < _hitEntities.Count; a++)
            {
                MChar attacker = _hitEntities[a];
                if (attacker.PauseBool || _hitEntityHitpause[a])
                {
                    continue;
                }
                for (int d = 0; d < _hitEntities.Count; d++)
                {
                    if (a == d) { continue; }
                    MChar defender = _hitEntities[d];
                    if (TeamOf(attacker) != TeamOf(defender))
                    {
                        MHitSystem.TryHit(attacker, defender, deferDamage: true);
                    }
                }
            }
        }

        // Project-specific: defers damage until after pair iteration so C# hit order stays deterministic.
        void FlushPendingDamage()
        {
            // 禁伤窗口（roundNoDamage）：丢弃本帧待结算伤害（命中已登记受击/硬直），不扣血。
            if (NoDamage)
            {
                for (int i = 0; i < Chars.Count; i++) { Chars[i].PendingLifeDamage = 0; }
                for (int i = 0; i < Helpers.Count; i++) { Helpers[i].PendingLifeDamage = 0; }
                return;
            }
            for (int i = 0; i < Chars.Count; i++)
            {
                MHitSystem.ApplyPendingDamage(Chars[i]);
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                MHitSystem.ApplyPendingDamage(Helpers[i]);
            }
        }

        // 单实体物理一相：PauseBool 冻结跳过；PosFreeze 跳积分；否则积分 + 落地检测。
        // Ikemen reference: src/char.go pos/vel integration and actionRun land check; C# delegates to MPhysics then LandCheck.
        void StepPhysics(MChar c, bool hitpause)
        {
            if (c.PauseBool || hitpause) { return; }
            if (!c.PosFreeze)
            {
                MPhysics.Step(c);
                MActionSystem.LandCheck(c);
            }
            c.ApplyBind();

            // R-STAGE-minimal：把角色夹回场内左右边界（撞墙则停下横向速度）。
            if (Stage.BoundsEnabled && c.ScreenBoundStageBound)
            {
                FFloat clampedX = c.Pos.X;
                if (Stage.ClampX(ref clampedX))
                {
                    c.Pos = new FVector3(clampedX, c.Pos.Y, c.Pos.Z);
                    c.Vel = new FVector3(FFloat.Zero, c.Vel.Y, c.Vel.Z);
                }
            }
        }

        readonly List<MChar> _physicsEntities = new List<MChar>();

        void ResolvePlayerPush(bool[] charHitpause, bool[] helperHitpause)
        {
            _physicsEntities.Clear();
            for (int i = 0; i < Chars.Count; i++)
            {
                if (!charHitpause[i])
                {
                    _physicsEntities.Add(Chars[i]);
                }
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                if (!helperHitpause[i])
                {
                    _physicsEntities.Add(Helpers[i]);
                }
            }
            MPhysics.ResolvePlayerPush(_physicsEntities, Stage);
        }

        static bool[] SnapshotHitpause(IReadOnlyList<MChar> chars)
        {
            bool[] result = new bool[chars.Count];
            for (int i = 0; i < chars.Count; i++)
            {
                result[i] = chars[i].Hitstop > 0;
            }
            return result;
        }

        static void ApplyHitpauseActtmp(IReadOnlyList<MChar> chars, bool[] hitpause)
        {
            for (int i = 0; i < chars.Count; i++)
            {
                if (hitpause != null && i < hitpause.Length && hitpause[i])
                {
                    chars[i].Acttmp = chars[i].PauseBool ? -3 : -1;
                }
            }
        }

        bool IsEntityHitpause(int ownerId, bool[] charHitpause, bool[] helperHitpause)
        {
            for (int i = 0; i < Chars.Count; i++)
            {
                if (Chars[i].Id == ownerId)
                {
                    return i < charHitpause.Length && charHitpause[i];
                }
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                if (Helpers[i].Id == ownerId)
                {
                    return i < helperHitpause.Length && helperHitpause[i];
                }
            }
            return false;
        }

        void ClearPosFreeze()
        {
            for (int i = 0; i < Chars.Count; i++) { Chars[i].PosFreeze = false; }
            for (int i = 0; i < Helpers.Count; i++) { Helpers[i].PosFreeze = false; }
        }

        static void ApplyPhysicsDefaults(MChar c)
        {
            c.WidthPlayerFront = FFloat.Zero;
            c.WidthPlayerBack = FFloat.Zero;
            c.WidthEdgeFront = FFloat.Zero;
            c.WidthEdgeBack = FFloat.Zero;
            c.WidthPlayerFrontSet = false;
            c.WidthPlayerBackSet = false;
            c.WidthEdgeFrontSet = false;
            c.WidthEdgeBackSet = false;
            c.PlayerPushEnabled = true;
            c.PushPriority = 0;
            c.PushAffectTeam = 1;
            c.ScreenBoundEnabled = true;
            c.ScreenBoundMoveCameraX = true;
            c.ScreenBoundMoveCameraY = true;
            c.ScreenBoundStageBound = true;
        }

        /// <summary>全角色哈希（确定性对账/黄金哈希用）。</summary>
        // Project-specific rollback determinism hash; no Ikemen counterpart, but includes simulation state derived from Ikemen fields.
        public ulong ComputeHash()
        {
            Hash64 hash = Hash64.Create();
            hash.AddInt32(FrameNo);
            hash.AddInt32(Chars.Count);
            for (int i = 0; i < Chars.Count; i++)
            {
                Chars[i].WriteHash(ref hash);
            }
            hash.AddInt32(Helpers.Count);   // helper 实体：数量 + 各自状态
            for (int i = 0; i < Helpers.Count; i++)
            {
                Helpers[i].WriteHash(ref hash);
            }
            hash.AddInt32(World.Projectiles.Count);   // 弹幕实体
            for (int i = 0; i < World.Projectiles.Count; i++)
            {
                World.Projectiles[i].WriteHash(ref hash);
            }
            hash.AddInt32(Random.Seed);   // 共享随机源种子：模拟状态，全场混入一次（不在 per-char 哈希以免重复计数）
            PauseState.WriteHash(ref hash);   // 共享全局暂停态：同理全场混入一次
            Stage.WriteHash(ref hash);
            hash.AddBool(NoDamage);
            hash.AddBool(EnableDemoAutoTurnFallback);
            World.WriteHash(ref hash);    // 实体世界（id 计数）
            return hash.Value;
        }

        // Project-specific: resolves C# player resource registry for localcoord/state/anim ownership.
        MCharData ResourceForPlayer(int playerNo)
        {
            MCharData data = Resources.Get(playerNo);
            if (data != null) { return data; }
            return playerNo >= 0 && playerNo < Data.Count ? Data[playerNo] : null;
        }

        // Project-specific: reconnects shared engine services after clone/restore; does not change Ikemen simulation semantics.
        void AttachSharedState(MChar character, MCharData data)
        {
            character.Resources = Resources;
            character.OwnData = data;
            character.Rng = Random;
            character.Pause = PauseState;
            character.World = World;
        }

        // Project-specific snapshot helper for Ikemen global pause/superpause state.
        static void CopyPause(MPauseState source, MPauseState target)
        {
            target.PauseTime = source.PauseTime;
            target.SuperTime = source.SuperTime;
            target.PauseTimeBuffer = source.PauseTimeBuffer;
            target.SuperTimeBuffer = source.SuperTimeBuffer;
            target.PausePlayerNo = source.PausePlayerNo;
            target.SuperPlayerNo = source.SuperPlayerNo;
        }

        // Project-specific snapshot helper: rebuilds Ikemen redirect, bind, target, and ownership references after clone.
        static void RelinkChar(MChar target, MChar source, Dictionary<int, MChar> map)
        {
            target.P2 = Remap(source.P2, map);
            target.Root = Remap(source.Root, map);
            target.Parent = Remap(source.Parent, map);
            target.Partner = Remap(source.Partner, map);
            target.StateOwner = Remap(source.StateOwner, map);
            target.BindTarget = Remap(source.BindTarget, map);
            target.Targets.Clear();
            for (int i = 0; i < source.Targets.Count; i++)
            {
                target.Targets.Add(Remap(source.Targets[i], map));
            }
            target.TargetRefs.Clear();
            for (int i = 0; i < source.TargetRefs.Count; i++)
            {
                target.TargetRefs.Add(new MTargetRef
                {
                    Target = Remap(source.TargetRefs[i].Target, map),
                    HitDefId = source.TargetRefs[i].HitDefId,
                });
            }
        }

        // Project-specific snapshot helper: maps old runtime ids to cloned C# entities.
        static MChar Remap(MChar character, Dictionary<int, MChar> map)
        {
            if (character == null) { return null; }
            return map.TryGetValue(character.Id, out MChar remapped) ? remapped : null;
        }

        // Project-specific snapshot helper for queued Helper controller requests.
        static MHelperRequest CloneRequest(MHelperRequest request)
        {
            return request;
        }

        // Project-specific snapshot helper for queued Projectile controller requests; HitDef is cloned to avoid shared mutation.
        static MProjectileRequest CloneRequest(MProjectileRequest request)
        {
            request.HitDef = request.HitDef != null ? request.HitDef.Clone() : null;
            return request;
        }
    }
}
