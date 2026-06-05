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

        readonly MStateMachine _stateMachine = new MStateMachine();

        // 确定性随机源（= Ikemen 单一全局 sys.randseed）：全场角色共享，random trigger 推进它。
        // 固定默认种子保证回放/双端逐位一致；联机时由对局配置同步初始种子。模拟状态 → 纳入 ComputeHash。
        public readonly MRandom Random = new MRandom(DefaultRandomSeed);
        const int DefaultRandomSeed = 1;

        // 全局暂停态（= Ikemen sys.pausetime/supertime + buffer/playerno）：全场角色共享，控制器经 SetPause 写。
        public readonly MPauseState PauseState = new MPauseState();

        // R-ENT 实体世界（共享 spawn 通道 + id 分配 + helper 列表）。_helperData 与 World.Helpers 平行（引擎侧配置）。
        public readonly MEntityWorld World = new MEntityWorld();
        public List<MChar> Helpers => World.Helpers;
        readonly List<MCharData> _helperData = new List<MCharData>();

        public void Add(MChar c, MCharData data)
        {
            c.Rng = Random;       // 接入共享随机源（对齐 Ikemen 全局种子）
            c.Pause = PauseState; // 接入共享暂停态（对齐 Ikemen 全局 sys.pause）
            c.World = World;      // 接入实体世界（helper spawn 通道）
            Chars.Add(c);
            Data.Add(data);
        }

        // 找某实体对应的 MCharData（玩家在 Data、helper 在 _helperData）。helper 用 owner 的角色数据。
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
                helper.Rng = Random;
                helper.Pause = PauseState;
                helper.World = World;
                helper.KeyCtrl = req.KeyCtrl;
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
                int facingSign = req.Owner.Facing.Raw >= 0 ? 1 : -1;
                MCharData ownerData = DataOf(req.Owner);
                Hit.MClsnBox[] clsn1 = ProjAnimClsn1(ownerData, req.AnimNo);
                World.Projectiles.Add(new MProjectile
                {
                    Id = World.AllocId(), OwnerId = req.Owner.Id, ProjId = req.ProjId,
                    Facing = FFloat.FromInt(facingSign),
                    Vel = new FVector3(req.VelX, req.VelY, FFloat.Zero),
                    Accel = new FVector3(req.AccelX, req.AccelY, FFloat.Zero),
                    Pos = new FVector3(req.Owner.Pos.X + req.PosX * FFloat.FromInt(facingSign),
                        req.Owner.Pos.Y + req.PosY, FFloat.Zero),
                    RemoveTime = req.RemoveTime, AnimNo = req.AnimNo,
                    HitDef = req.HitDef, Owner = req.Owner, Clsn1 = clsn1,
                });
            }
            World.ProjSpawnQueue.Clear();
        }

        // 弹幕 projanim 首帧的攻击框 Clsn1（弹幕攻击框来源）。无表/无帧 → null。
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
                        if (MHitSystem.TryProjectileHit(proj, target))
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
        public void StartRound()
        {
            for (int i = 0; i < Chars.Count; i++)
            {
                Chars[i].KeyCtrl = true;
                Chars[i].Ctrl = true;
            }
        }

        /// <summary>推进一帧。inputs[i] 对应 Chars[i] 本帧输入。
        /// 顺序对齐 Ikemen 每帧：输入缓冲 → actionPrepare(硬编码基础动作) → actionRun(状态机) → update(物理/动画) → 命中。
        /// Pause/SuperPause（移植 system.go super/pause 全局冻结）：暂停期间被冻结角色跳过本帧全部处理，
        /// 仅施暂停方在其 movetime 窗口内可动；SuperPause 优先于 Pause。命中在暂停期间不结算。</summary>
        public void Tick(IReadOnlyList<MInput> inputs)
        {
            // 0) 全局暂停推进（移植 system.go:2562）：递减 super/pause 时长 + 应用 buffer（上一帧控制器设的暂停此刻生效）。
            //    再对每角色算 PauseBool/Acttmp（移植 char.go:11421 + 11524 movetime 递减）。无暂停时 PauseBool 全 false → 与原逐位一致。
            PauseState.Step();
            for (int i = 0; i < Chars.Count; i++) { Chars[i].ComputePauseBool(); }
            for (int i = 0; i < Helpers.Count; i++) { Helpers[i].ComputePauseBool(); }
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
            for (int i = 0; i < Chars.Count; i++)
            {
                if (!Chars[i].PauseBool) { MActionSystem.Prepare(Chars[i]); }
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                if (!Helpers[i].PauseBool) { MActionSystem.Prepare(Helpers[i]); }
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
            for (int i = 0; i < Chars.Count; i++) { StepPhysics(Chars[i]); }
            for (int i = 0; i < Helpers.Count; i++) { StepPhysics(Helpers[i]); }

            // 5) 动画推进（玩家 + helper）。
            for (int i = 0; i < Chars.Count; i++)
            {
                if (!Chars[i].PauseBool) { MAnimSystem.Action(Chars[i], Data[i].Anims); }
            }
            for (int i = 0; i < Helpers.Count; i++)
            {
                if (!Helpers[i].PauseBool) { MAnimSystem.Action(Helpers[i], _helperData[i].Anims); }
            }

            // 5b) 排空 spawn 队列（本帧状态机里 Helper/Projectile 控制器请求的实体，下帧起跑）+ 推进现有弹幕。
            DrainSpawns();
            if (!anyPause) { StepProjectiles(); }

            // 6) 命中：全 MChar 实体（玩家 + helper）跨队两两尝试。暂停期间不结算。
            //    队伍 = Root.Id（helper 继承 owner 的 Root）；只在不同队之间判定，避免打自己人。
            if (!anyPause)
            {
                RunHits();
            }

            // 7) 移除 DestroySelf 的 helper。
            RemoveDestroyed();
        }

        static void StepProjectileContactTime(MChar character)
        {
            if (character.ProjectileContactTime >= 0)
            {
                character.ProjectileContactTime++;
            }
        }

        // 队伍归属（移植 Ikemen teamside 简化）：Root 玩家的 id（0/1）。helper.Root=owner 玩家 → 与 owner 同队。
        static int TeamOf(MChar c)
        {
            return c.Root != null ? c.Root.Id : c.Id;
        }

        // 本角色本帧用哪张状态表：StateOwner 非 null(投技自定义状态)→其角色数据；否则自身数据(fallback)。
        MCharData StateDataFor(MChar c, MCharData own)
        {
            if (c.StateOwner != null && !ReferenceEquals(c.StateOwner, c))
            {
                MCharData ownerData = DataOf(c.StateOwner);
                if (ownerData != null) { return ownerData; }
            }
            return own;
        }

        // 全实体跨队命中：把玩家与 helper 合到一个列表，攻防两两尝试（TryHit 内部做 active/重叠/同招一次判定）。
        readonly List<MChar> _hitEntities = new List<MChar>();
        void RunHits()
        {
            _hitEntities.Clear();
            _hitEntities.AddRange(Chars);
            _hitEntities.AddRange(Helpers);
            for (int a = 0; a < _hitEntities.Count; a++)
            {
                MChar attacker = _hitEntities[a];
                for (int d = 0; d < _hitEntities.Count; d++)
                {
                    if (a == d) { continue; }
                    MChar defender = _hitEntities[d];
                    if (TeamOf(attacker) != TeamOf(defender))
                    {
                        MHitSystem.TryHit(attacker, defender);
                    }
                }
            }
        }

        // 单实体物理一相：PauseBool 冻结跳过；PosFreeze 跳积分；否则积分 + 落地检测。
        void StepPhysics(MChar c)
        {
            if (c.PauseBool) { return; }
            if (!c.PosFreeze)
            {
                MPhysics.Step(c);
                MActionSystem.LandCheck(c);
            }
            c.ApplyBind();
            c.PosFreeze = false;
        }

        /// <summary>全角色哈希（确定性对账/黄金哈希用）。</summary>
        public ulong ComputeHash()
        {
            Hash64 hash = new Hash64();
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
            World.WriteHash(ref hash);    // 实体世界（id 计数）
            return hash.Value;
        }
    }
}
