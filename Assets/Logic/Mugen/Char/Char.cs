// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go  (Char + StateState 核心字段)
// Adapted to fixed-point. M3 骨架：仅核心 trigger 字段 + Clone/WriteHash(接回滚) + IExprContext(常用 trigger)。
// 完整 ~200 字段(CharSystemVar/hitdef/ghv/targets...)逐步补。See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Battle;

namespace Lockstep.Mugen.Char
{
    /// <summary>
    /// MUGEN 角色运行态（移植 Ikemen Char，定点化）。M3 骨架版。
    /// 实现 <see cref="IExprContext"/>：表达式 VM 的 trigger/redirect opcode 从本 Char 读值。
    /// StateType/MoveType/Physics 暂用 int32 原始 MUGEN 码（枚举映射归 M2 编译器/System）。
    /// </summary>
    public sealed class MChar : IExprContext, IExprVariableContext, IExprTwoArgumentRedirectContext
    {
        public string Name;
        public string StageInfoName = "";

        // StateState（状态机运行态）
        public int Time;
        public int StateNo;
        public int PrevStateNo;
        public int StateType;       // 原始 MUGEN 码（S/C/A/...）
        public int PrevStateType;   // 上一状态的 statetype（prevstatetype trigger）
        public int MoveType;        // I/A/H
        public int Physics;
        public bool Ctrl;

        // 动画
        public int AnimNo;
        public int PrevAnimNo;

        // 生命/能量
        public int Life;
        public int LifeMax = 1000;
        public int Power;
        public int PowerMax = 3000;
        public int Juggle;

        // 攻防倍率（运行态，移植 char.go attackMul[0]/customDefense/superDefenseMul/fallDefenseMul/finalDefense）。
        // 伤害公式 computeDamage：damage *= (AttackMul×AttackBase/100) / FinalDefense。
        // AttackBase/DefenceBase 取自 Constants.Attack/Defence（[Data] attack/defence，默认 100）。
        public FFloat AttackMul = FFloat.One;        // attackMul[0]：仅伤害分量（redlife/dizzy/guard 分量后续）
        public FFloat CustomDefense = FFloat.One;    // DefenceMulSet 设置（mulType 决定取 val 或 1/val）
        public FFloat SuperDefenseMul = FFloat.One;  // SuperPause p2defmul 缓冲应用后的累积（后续）
        public FFloat FallDefenseMul = FFloat.One;   // 浮空防御加成（后续）
        public bool DefenseMulDelay;                 // DefenceMulSet onHit：true 时 customDefense 仅受击态(movetype H)生效

        // 打击感
        public int Hitstop;         // = Ikemen hitPauseTime（hitpausetime trigger）
        public int PendingLifeDamage; // R-DMG-PIPELINE: ghv.damage accumulated during hit resolution, applied after all hits.

        // CharSystemVar 常用计数/标志（攻击命中统计，供 trigger 与控制器读）
        public int HitCount;        // 本招累计命中数（命中时 += HitDef.numhits，char.go:12189）
        public int UniqHitCount;    // 去重命中数（每命中一个目标 +1）
        public int GuardCount;      // 本招被防累计数（被防时 += HitDef.numhits，char.go:12191）
        public int ReceivedHits;    // 本角色当前连段内被命中次数（= Ikemen receivedHits，combocount 源；脱离受击清零）
        public int MoveContact;     // 本招是否接触(命中或被防)：>0
        public int MoveHit;         // 本招是否命中：>0
        public int MoveGuarded;     // 本招是否被防：>0
        public int MoveReversed;    // 本招是否被打断/反击：>0
        public int PalNo = 1;       // 调色板号（palno trigger）

        // 动画运行态（由 MAnimSystem M8 维护）。派生 trigger 量：
        public int AnimTime;        // 当前动画剩余时间 = curtime-totaltime（MUGEN 惯例 ≤0）
        public int AnimElemNo;      // 当前动画元素序号（1-based）
        // 原始运行态（移植 Ikemen Animation.curelem/curelemtime/curtime；快照/回滚以这三者为准）：
        public int AnimElem;        // 当前元素索引（0-based）
        public int AnimElemTime;    // 当前元素已播 tick 数（curelemtime）
        public int AnimCurTime;     // 动画累计已播 tick 数（curtime）
        public bool AnimLoopEnd;    // 本 tick 是否到达动画终点（curtime>=totaltime）
        public int AnimRunningNo = -1;   // MAnimSystem 跟踪的"当前正在播放的动画号"，与 AnimNo 不同则触发重置

        // AssertSpecial 标志位（每帧清空，须每帧重断言才保持）。见 MAssertFlag。
        public int AssertFlags;

        // 受击变量（命中系统 M7 填值；此处随 Char 快照/哈希）
        public MGetHitVar Ghv = new MGetHitVar();

        // 浮空已持续帧数（= Ikemen fallTime）：fallflag 期间每帧 ++，进受击/落地清零。canRecover 判定用。
        public int FallTime;

        // 确定性随机源（= Ikemen 全局 sys.randseed）：`random` trigger 查此。引擎装配时所有角色共享同一实例
        // （对齐 Ikemen 单一全局种子）。Clone 浅拷引用（共享可变态，引擎级快照统一重链 + 入哈希）。可为 null（退化返 0）。
        public MRandom Rng;

        // 命令系统（M6）：输入缓冲 + 命令激活状态。command="name" trigger 查此。可为 null（无输入源时）。
        public Command.MCommandList CommandList;

        // 输入边沿缓冲（移植 Ikemen InputBuffer）：引擎硬编码基础动作读 Fb/Bb/Ub/Db 边沿。每帧 Update。
        public Command.MInputBuffer Input = new Command.MInputBuffer();

        // 是否玩家按键控制（= Ikemen keyctrl[0]）：引擎硬编码基础动作仅对受控角色生效。
        public bool KeyCtrl;
        // 空中跳跃已用次数（= Ikemen airJumpCount）：stateType != A 时清零，airjump 上限判定用。
        public int AirJumpCount;

        // 角色常量（[Data]/[Size]/[Velocity]/[Movement]）：const(...) 的取值来源。
        // 加载后不可变 → Clone 浅拷引用、WriteHash 不混入（与状态表同属配置，不进回滚快照）。可为 null。
        public MConstants Constants;

        // 角色动画表（动画号→动画）：不可变配置引用，由 SpawnChar 接入（与 Constants 同属配置，
        // Clone 浅拷引用、WriteHash 不混入、不进回滚快照）。用于 ChangeAnim 存在性守卫与 animexist trigger。可为 null。
        public System.Collections.Generic.IReadOnlyDictionary<int, Anim.MAnimData> AnimTable;

        // Ikemen-style resource ownership. Dictionary references are immutable fallbacks used before registration.
        public MPlayerResourceRegistry Resources;
        public MCharData OwnData;
        public int PlayerNo = -1;
        public int StatePlayerNo = -1;
        public int AnimPlayerNo = -1;
        public int SpritePlayerNo = -1;

        // 命中系统（M7）：当前 HitDef + 本帧 Clsn 框（Clsn 为动画派生，由 Anim 系统 M8 填，Clone 浅拷不哈希）。
        public Hit.MHitDef HitDef = new Hit.MHitDef();
        public Hit.MClsnBox[] Clsn1;   // 攻击框
        public Hit.MClsnBox[] Clsn2;   // 受击框
        public bool Guarding;          // 守方是否正在防御（守招判定入口；真实防御检测由命令/守招状态后续接）
        // HitBy/NotHitBy 属性免疫过滤槽（time>0 生效；IsNot=NotHitBy）。每帧递减。
        public int HitByAttr;
        public int HitByTime;
        public bool HitByIsNot;

        // ───────── Tier B 控制器运行态（影响模拟，入 Clone/Hash）。表现态（Trans/Angle/PalFX 等）不在此，待表现层。─────────
        // Pause/SuperPause（移植 char.go:8920/8941 + 11421）：全局时长在共享 Pause；本角色只持「暂停期还能动多久」与门控标志。
        public MPauseState Pause;       // 共享全局暂停态引用（引擎装配；同 Rng 浅拷+引擎重链）
        public int PauseMovetime;       // c.pauseMovetime：Pause 期间本角色可动帧
        public int SuperMovetime;       // c.superMovetime：SuperPause 期间本角色可动帧
        public bool PauseBool;          // c.pauseBool：本帧是否被暂停冻结
        public int Acttmp;              // c.acttmp：1 unpaused / 0 default / -2 pause（物理/动画门控）
        public int UnhittableTime;      // c.unhittableTime：SuperPause unhittable 期间不可被击
        // PosFreeze：本帧冻结位置（物理跳过积分）。每帧由控制器重新断言，物理后清零。
        public bool PosFreeze;
        // Width（移植 Width 控制器）：角色推挤宽度（player）与边界宽度（edge），前/后。每帧重新断言（默认取 size box）。
        public FFloat WidthPlayerFront;
        public FFloat WidthPlayerBack;
        public FFloat WidthEdgeFront;
        public FFloat WidthEdgeBack;
        // PlayerPush：是否参与角色间推挤（默认 true）+ 优先级 + 影响队伍。每帧重新断言。
        public bool PlayerPushEnabled = true;
        public int PushPriority;
        public int PushAffectTeam;
        // ScreenBound：是否受屏幕/舞台边界约束 + 相机跟随。每帧重新断言。
        public bool ScreenBoundEnabled;
        public bool ScreenBoundMoveCameraX;
        public bool ScreenBoundMoveCameraY;
        public bool ScreenBoundStageBound;
        // MoveContactTime（= Ikemen mctime）+ CounterHit：MoveHitReset 清零；movecontact/movehit 触发器的底层计数。
        public int MoveContactTime;
        public bool CounterHit;
        // HitOverride 槽（8 个，对齐 Ikemen c.ho[8]）：受击改写。命中系统据此改受击方目标态。
        public MHitOverride[] HitOverrides = new MHitOverride[8];
        public MReversalDefRuntime ReversalDef = new MReversalDefRuntime();

        // 回合状态（roundstate trigger）：1 入场/2 战斗/3 已分胜负缓冲/4 胜利姿态（对齐 MUGEN roundstate）。
        // 默认 2(Fight)：无回合系统驱动时多数 `roundstate=2` 门控即通过；MRoundSystem 驱动时每帧写入实际相位。
        public int RoundState = 2;

        // ───────── 表现态：精灵绘制参数（移植 Ikemen char.go 绘制字段；现已接 Unity 渲染消费层）─────────
        // 旋转角 anglerot[0..2]=value/x/y（char.go:1638）。anglerot 跨帧保留，仅当 AngleDraw 标志置位才用于绘制。
        public FFloat AngleRot;
        public FFloat AngleRotX;
        public FFloat AngleRotY;
        // AngleDraw 缩放 angleDrawScale[2]（char.go:3143，默认 1,1；每帧重置）。
        public FFloat AngleDrawScaleX = FFloat.One;
        public FFloat AngleDrawScaleY = FFloat.One;
        // CSF_angledraw（char.go:34）：本帧是否按 anglerot 旋转绘制。每帧重置为 false，AngleDraw 控制器置位。
        public bool AngleDraw;
        // Trans 混合模式 + alpha[src,dst]（char.go trans/alpha；每帧重置为 Default/255,0）。
        public MTransType Trans = MTransType.Default;
        public int AlphaSrc = 255;
        public int AlphaDst;
        // SprPriority/LayerNo（char.go sprPriority/layerNo；绘制排序，跨帧保留——不在每帧重置块）。
        public int SprPriority;
        public int LayerNo;
        // Offset 绘制偏移 offset[2]（char.go；每帧重置 0,0；Offset 控制器写入时已乘 localscl）。
        public FFloat OffsetX;
        public FFloat OffsetY;
        // VictoryQuote winquote：选定胜利台词索引，默认 -1（跨帧保留）。
        public int WinQuote = -1;

        // Ikemen char.go:9051 angleSet/XangleSet/YangleSet：供 Angle* 控制器设置旋转分量。
        public void AngleSetValue(FFloat a) { AngleRot = a; }
        public void XAngleSetValue(FFloat xa) { AngleRotX = xa; }
        public void YAngleSetValue(FFloat ya) { AngleRotY = ya; }

        // 每帧绘制态重置（移植 char.go:11542，在状态控制器运行前调用）。
        // 仅重置 AngleDraw 标志/angleDrawScale/trans/alpha/offset；anglerot、sprPriority、winquote 跨帧保留。
        public void ResetFrameDrawState()
        {
            AngleDraw = false;
            AngleDrawScaleX = FFloat.One;
            AngleDrawScaleY = FFloat.One;
            Trans = MTransType.Default;
            AlphaSrc = 255;
            AlphaDst = 0;
            OffsetX = FFloat.Zero;
            OffsetY = FFloat.Zero;
        }

        // ───────── R-ENT 实体系统（helper/projectile/explod）─────────
        public MEntityWorld World;   // 共享实体世界引用（spawn 通道 + id 分配；同 Pause/Rng 浅拷+引擎重链）
        public bool IsHelper;        // 本实体是否为 helper（ishelper trigger）
        public int HelperType;       // helper id（= Helper 控制器 id 参数；ishelper(id)/numhelper(id) 匹配）
        public bool Destroyed;       // DestroySelf 标记；引擎帧末移除（仅 helper 等实体，玩家不受影响）
        // Projectile contact trigger state: pcid/pctype/pctime in Ikemen char.go.
        public int ProjectileContactId;
        public int ProjectileContactType; // 0=hit, 1=guarded, 2=cancel
        public int ProjectileContactTime = -1;
        public FFloat AttackDistX = FFloat.FromInt(160);

        /// <summary>请求创建一个 helper（移植 Helper 控制器：入队，引擎 DrainSpawns 时造实体）。</summary>
        public void RequestHelper(int stateNo, int helperType, FFloat posX, FFloat posY, int facing, bool keyCtrl)
        {
            if (World == null) { return; }
            World.RequestHelper(new MHelperRequest
            {
                Owner = this, StateNo = stateNo, HelperType = helperType,
                PosX = posX, PosY = posY, Facing = facing, KeyCtrl = keyCtrl,
            });
        }

        /// <summary>请求发射一个弹幕（移植 Projectile 控制器：入队，引擎 DrainSpawns 时造实体）。</summary>
        public void RequestProjectile(int projId, FFloat velX, FFloat velY, FFloat accelX, FFloat accelY,
            FFloat posX, FFloat posY, int removeTime, int animNo, Hit.MHitDef hitDef)
        {
            if (World == null) { return; }
            World.RequestProjectile(new MProjectileRequest
            {
                Owner = this, ProjId = projId, VelX = velX, VelY = velY,
                AccelX = accelX, AccelY = accelY, PosX = posX, PosY = posY,
                RemoveTime = removeTime, AnimNo = animNo, HitDef = hitDef,
            });
        }

        public void RecordProjectileContact(int projId, bool guarded)
        {
            ProjectileContactId = projId;
            ProjectileContactType = guarded ? 1 : 0;
            ProjectileContactTime = 0;
        }

        public BytecodeValue ProjectileContactTimeValue(int projId, int wantedType)
        {
            if (ProjectileContactTime < 0)
            {
                return BytecodeValue.Int(-1);
            }
            if (projId > 0 && projId != ProjectileContactId)
            {
                return BytecodeValue.Int(-1);
            }
            bool matched = wantedType == 3
                ? ProjectileContactType != 2
                : ProjectileContactType == wantedType;
            return BytecodeValue.Int(matched ? ProjectileContactTime : -1);
        }

        // 状态机：待应用的切换（>=0 表示本帧要 ChangeState 到此号）
        public MStateTransition PendingTransition = MStateTransition.None;
        public int PendingStateNo
        {
            get => PendingTransition.Active ? PendingTransition.StateNo : -1;
            set
            {
                PendingTransition.Active = value >= 0;
                PendingTransition.StateNo = value;
                if (PendingTransition.OwnerPlayerNo < 0) { PendingTransition.OwnerPlayerNo = StatePlayerNo; }
            }
        }
        public bool PendingIsSelf
        {
            get => PendingTransition.Active && PendingTransition.OwnerPlayerNo == PlayerNo;
            set
            {
                if (value) { PendingTransition.OwnerPlayerNo = PlayerNo; }
                else if (PendingTransition.OwnerPlayerNo < 0) { PendingTransition.OwnerPlayerNo = StatePlayerNo; }
            }
        }
        // persistent 计数：当前状态内各控制器(按 index)已触发帧数，进入新状态时清空（仅作用当前状态）
        public Dictionary<int, int> PersistCounters = new Dictionary<int, int>();

        // BindTo*/TargetBind runtime. BindTarget is a structural reference; offset is stored in target-facing space.
        public MChar BindTarget;
        public int BindTime;
        public FVector3 BindPos;
        public int BindFacing;

        // 物理
        public FVector3 Pos;
        public FVector3 OldPos;
        public FVector3 Vel;
        public FFloat Facing = FFloat.One;   // +1 右 / -1 左

        // CNS 变量：var(n)/fvar(n)
        public Dictionary<int, int> IntVars = new Dictionary<int, int>();
        public Dictionary<int, FFloat> FloatVars = new Dictionary<int, FFloat>();
        public Dictionary<int, int> SysIntVars = new Dictionary<int, int>();
        public Dictionary<int, FFloat> SysFloatVars = new Dictionary<int, FFloat>();

        // ───────── redirect 链接（结构性引用，非拥有；Clone 浅拷、Hash 不递归被引者）─────────
        public int Id;                       // 本角色实例 id（playerid / target 匹配用）
        public MChar P2;                     // 对手（1v1 中即对方）
        public MChar Root;                   // 根角色（非 helper 时通常 = 自身）
        public MChar StateOwner;             // 自定义状态归属（投技 p2getp1state）：非 null 时本角色跑该角色的状态表；SelfState 复位
        public MChar Parent;                 // 父角色（helper 的创建者；root 为 null）
        public MChar Partner;                // 同队 partner；v1 无组队时为空，partner redirect 用
        public List<MChar> Targets = new List<MChar>();   // 本角色 HitDef 命中的目标
        public List<MTargetRef> TargetRefs = new List<MTargetRef>();

        public bool Alive => Life > 0;

        /// <summary>
        /// 最终防御系数（移植 char.go:12081-12085）：(DefenceBase × customDef × superDef × fallDef) / 100。
        /// customDef 在 DefenseMulDelay 且非受击态(movetype≠H)时按 1 计（onHit 延迟生效）。
        /// </summary>
        public FFloat ComputeFinalDefense()
        {
            FFloat customDef = (!DefenseMulDelay || MoveType == 2) ? CustomDefense : FFloat.One;
            int defenceBase = Constants != null ? Constants.Defence : 100;
            return FFloat.FromInt(defenceBase) * customDef * SuperDefenseMul * FallDefenseMul / FFloat.FromInt(100);
        }

        /// <summary>伤害攻击系数（移植 char.go atkmul[0]×attackBase/100）：AttackMul × AttackBase / 100。</summary>
        public FFloat AttackDamageMul()
        {
            int attackBase = Constants != null ? Constants.Attack : 100;
            return AttackMul * FFloat.FromInt(attackBase) / FFloat.FromInt(100);
        }

        /// <summary>是否有控制权（移植 Ikemen ctrl()）。standby/dizzy/guardbreak 等状态机后置，暂仅 Ctrl 位。</summary>
        public bool Control()
        {
            return Ctrl;
        }

        // ───────── Pause/SuperPause（移植 char.go:8920 setPauseTime / 8941 setSuperPauseTime / 11421 pauseBool）─────────
        // 注：省略 Ikemen 的 c.playerNo != c.ss.sb.playerNo 条件（自定义态归属，本范围恒等 → 该项恒 false），用 Id 当 playerNo。

        /// <summary>Pause 控制器调用：写共享 buffer（带 playerno 优先级）+ 本角色 movetime 钳制。</summary>
        public void SetPause(int pausetime, int movetime)
        {
            if (Pause == null) { return; }
            if (~pausetime < Pause.PauseTimeBuffer || Pause.PausePlayerNo == Id)
            {
                Pause.PauseTimeBuffer = ~pausetime;
                Pause.PausePlayerNo = Id;
            }
            PauseMovetime = movetime > 0 ? movetime : 0;
            if (PauseMovetime > pausetime) { PauseMovetime = 0; }
            else if (Pause.PauseTime > 0 && PauseMovetime > 0) { PauseMovetime--; }
        }

        /// <summary>SuperPause 控制器调用（unhittable 延 1 帧因暂停下一帧才生效）。</summary>
        public void SetSuperPause(int pausetime, int movetime, bool unhittable)
        {
            if (Pause == null) { return; }
            if (~pausetime < Pause.SuperTimeBuffer || Pause.SuperPlayerNo == Id)
            {
                Pause.SuperTimeBuffer = ~pausetime;
                Pause.SuperPlayerNo = Id;
            }
            SuperMovetime = movetime > 0 ? movetime : 0;
            if (SuperMovetime > pausetime) { SuperMovetime = 0; }
            else if (Pause.SuperTime > 0 && SuperMovetime > 0) { SuperMovetime--; }
            if (unhittable) { UnhittableTime = pausetime + (pausetime > 0 ? 1 : 0); }
        }

        /// <summary>本帧暂停门控（移植 char.go:11421 actionPrepare 开头）：在跑各相之前由引擎调用。
        /// Acttmp = -2(暂停) / 1(活动)；hitpause 的 acttmp=-1 由既有 hitstop 逻辑处理，不在此覆盖。</summary>
        public void ComputePauseBool()
        {
            PauseBool = false;
            if (CommandList != null && Pause != null)
            {
                if (Pause.SuperTime > 0) { PauseBool = SuperMovetime == 0; }
                else if (Pause.PauseTime > 0 && PauseMovetime == 0) { PauseBool = true; }
            }
            Acttmp = PauseBool ? -2 : 1;
            if (UnhittableTime > 0) { UnhittableTime--; }
            // per-frame movetime 递减（char.go:11524-11528，在 pauseBool 计算之后）：施暂停方逐帧耗尽 movetime 后即被冻结。
            if (Pause != null)
            {
                if (Pause.SuperTime > 0)
                {
                    if (SuperMovetime > 0) { SuperMovetime--; }
                }
                else if (Pause.PauseTime > 0 && PauseMovetime > 0)
                {
                    PauseMovetime--;
                }
            }
        }

        /// <summary>当前动画 owner 的动画表中是否存在该动画号（animexist trigger）。</summary>
        public bool AnimExists(int animNo)
        {
            int playerNo = AnimPlayerNo >= 0 ? AnimPlayerNo : PlayerNo;
            return AnimExistsFor(playerNo, animNo);
        }

        /// <summary>自身资源 owner 的动画表中是否存在该动画号（selfanimexist trigger）。</summary>
        public bool SelfAnimExists(int animNo)
        {
            return AnimExistsFor(PlayerNo, animNo);
        }

        public bool AnimExistsFor(int playerNo, int animNo)
        {
            IReadOnlyDictionary<int, Anim.MAnimData> table = AnimationsFor(playerNo);
            return table != null && table.ContainsKey(animNo);
        }

        public MCharData DataFor(int playerNo)
        {
            MCharData data = Resources?.Get(playerNo);
            if (data != null) { return data; }
            if (playerNo < 0 || playerNo == PlayerNo) { return OwnData; }
            if (StateOwner != null && playerNo == StateOwner.PlayerNo) { return StateOwner.OwnData; }
            return null;
        }

        public IReadOnlyDictionary<int, Anim.MAnimData> AnimationsFor(int playerNo)
        {
            MCharData data = DataFor(playerNo);
            if (data != null) { return data.Anims; }
            return playerNo < 0 || playerNo == PlayerNo ? AnimTable : null;
        }

        public IReadOnlyDictionary<int, Anim.MAnimData> CurrentAnimTable()
        {
            return AnimationsFor(AnimPlayerNo >= 0 ? AnimPlayerNo : PlayerNo);
        }

        public int LocalCoordWidthFor(int playerNo)
        {
            if (Resources != null) { return Resources.LocalCoordWidth(playerNo); }
            MCharData data = DataFor(playerNo);
            int width = data?.Definition?.LocalCoordWidth ?? 320;
            return width > 0 ? width : 320;
        }

        public bool PlayAnimation(int animNo, int animPlayerNo, int spritePlayerNo, int elem = 0, int elemTime = 0)
        {
            if (animPlayerNo < 0) { animPlayerNo = PlayerNo; }
            if (spritePlayerNo < 0) { spritePlayerNo = PlayerNo; }
            IReadOnlyDictionary<int, Anim.MAnimData> table = AnimationsFor(animPlayerNo);
            if (table != null && !table.ContainsKey(animNo)) { return false; }
            AnimPlayerNo = animPlayerNo;
            SpritePlayerNo = spritePlayerNo;
            Anim.MAnimSystem.PlayAt(this, animNo, table, elem, elemTime);
            return true;
        }

        public void QueueTransition(int stateNo, int ownerPlayerNo, int animNo = -1, int ctrl = -1)
        {
            PendingTransition = new MStateTransition
            {
                Active = stateNo >= 0,
                StateNo = stateNo,
                OwnerPlayerNo = ownerPlayerNo >= 0 ? ownerPlayerNo : StatePlayerNo,
                AnimNo = animNo,
                Ctrl = ctrl,
            };
        }

        /// <summary>animelemtime(n)：自元素 n（1-based）起已播 tick。当前元素精确；其余按累积起始时间推算。</summary>
        int ComputeAnimElemTime(int elemNo1Based)
        {
            if (elemNo1Based == AnimElemNo)
            {
                return AnimElemTime;   // 当前元素：精确（与 animelem= 首帧语义一致）
            }
            IReadOnlyDictionary<int, Anim.MAnimData> table = CurrentAnimTable();
            if (table == null || !table.TryGetValue(AnimRunningNo, out Anim.MAnimData anim)
                || anim.Frames == null)
            {
                return 0;
            }
            int index = elemNo1Based - 1;
            if (index < 0 || index >= anim.Frames.Length)
            {
                return 0;
            }
            int startTime = 0;
            for (int e = 0; e < index; e++)
            {
                int t = anim.Frames[e].Time;
                if (t < 0) { break; }   // 永久帧前缀：无法累加，止于此
                startTime += t;
            }
            return AnimCurTime - startTime;
        }

        // ───────── 距离（p2dist/p2bodydist；移植 char.go:8743 distX / 8787 bodyDistX 简化形）─────────

        /// <summary>到对手的朝向相对水平距离（前为正）= facing*(opp.x - self.x)。
        /// 对齐 char.go:8859 rdDistX；定点精确，省略 float 版的 |·|&lt;0.0001 噪声夹取。</summary>
        FFloat DistX(MChar opp)
        {
            return Facing * (opp.Pos.X - Pos.X);
        }

        /// <summary>到对手的边到边水平距离 = p2dist X 减双方前缘半宽（MUGEN 形，char.go:8787 注释「不随 Width 变化」）。</summary>
        FFloat BodyDistX(MChar opp)
        {
            FFloat selfFront = Constants != null ? Constants.SizeGroundFront : FFloat.Zero;
            FFloat oppFront = opp.Constants != null ? opp.Constants.SizeGroundFront : FFloat.Zero;
            return DistX(opp) - selfFront - oppFront;
        }

        /// <summary>到对手的水平距离绝对值（inguarddist 判定用，不分前后）。</summary>
        FFloat AbsDistX(MChar opp)
        {
            FFloat d = opp.Pos.X - Pos.X;
            return d.Raw < 0 ? -d : d;
        }

        // ───────── 受击触发器（移植 Ikemen char.go hitOver/hitShakeOver/canRecover；HitFall=ghv.fallflag）─────────

        /// <summary>HitShakeOver：受击抖动结束（char.go:5342，ghv.hitshaketime &lt;= 0）。</summary>
        public bool HitShakeOver()
        {
            return Ghv.HitShakeTime <= 0;
        }

        /// <summary>HitOver：受击硬直结束（char.go:5338，ghv.hittime &lt; 0）。</summary>
        public bool HitOver()
        {
            return Ghv.HitTime < 0;
        }

        /// <summary>CanRecover：浮空可起身（char.go:5165，fall.recover 且浮空时长达 recovertime）。</summary>
        public bool CanRecover()
        {
            return Ghv.FallRecover && FallTime >= Ghv.FallRecoverTime;
        }

        /// <summary>
        /// 受击实际动画类型（移植 Ikemen char.go:7680 gethitAnimtype）：
        /// fall 用 fall.animtype；空中用 air.animtype；地面用 ground.animtype，
        /// 但若 ground.animtype 为 Back 及以上且 yvel=0 则降级为 Hard（MUGEN 行为）。
        /// </summary>
        public int GetHitAnimType()
        {
            if (Ghv.Fall)
            {
                return Ghv.FallAnimType;
            }
            if (StateType == 4)   // ST_A 空中
            {
                return Ghv.AirAnimType;
            }
            if (Ghv.GroundAnimType >= (int)Hit.MReaction.Back && Ghv.YVel == FFloat.Zero)
            {
                return (int)Hit.MReaction.Hard;
            }
            return Ghv.GroundAnimType;
        }

        /// <summary>
        /// 是否允许把当前动画切到该号（对齐 Ikemen changeAnimEx：目标动画不存在则不切、保留当前动画，避免冻结）。
        /// 无表（裸构造的单测）时放行，以保持既有行为。
        /// </summary>
        public bool CanChangeAnimTo(int animNo)
        {
            IReadOnlyDictionary<int, Anim.MAnimData> table = AnimationsFor(PlayerNo);
            return table == null || table.ContainsKey(animNo);
        }

        public void BindTo(MChar target, int time, FVector3 offset, int bindFacing)
        {
            BindTarget = target;
            BindTime = time;
            BindPos = offset;
            BindFacing = bindFacing;
        }

        public void ApplyBind()
        {
            if (BindTarget == null || BindTime <= 0)
            {
                return;
            }
            FFloat targetFacing = BindTarget.Facing.Raw >= 0 ? FFloat.One : -FFloat.One;
            Pos = new FVector3(
                BindTarget.Pos.X + BindPos.X * targetFacing,
                BindTarget.Pos.Y + BindPos.Y,
                BindTarget.Pos.Z + BindPos.Z);
            if (BindFacing != 0)
            {
                Facing = targetFacing * FFloat.FromInt(BindFacing);
            }
            BindTime--;
            if (BindTime <= 0)
            {
                BindTarget = null;
            }
        }

        // ───────── 回滚支持 ─────────

        public void ClearTargets()
        {
            Targets.Clear();
            TargetRefs.Clear();
        }

        public void AddTarget(MChar target, int hitDefId)
        {
            if (target == null)
            {
                return;
            }
            Targets.Add(target);
            TargetRefs.Add(new MTargetRef { Target = target, HitDefId = hitDefId });
        }

        public bool HasTarget(MChar target)
        {
            for (int index = 0; index < TargetRefs.Count; index++)
            {
                if (ReferenceEquals(TargetRefs[index].Target, target))
                {
                    return true;
                }
            }
            return Targets.Contains(target);
        }

        public List<MChar> SelectTargetsByHitId(int hitDefId, int matchIndex)
        {
            List<MChar> selected = new List<MChar>();
            if (TargetRefs.Count > 0)
            {
                for (int index = 0; index < TargetRefs.Count; index++)
                {
                    MTargetRef targetRef = TargetRefs[index];
                    if (targetRef.Target != null && (hitDefId < 0 || targetRef.HitDefId == hitDefId))
                    {
                        selected.Add(targetRef.Target);
                    }
                }
            }
            else
            {
                for (int index = 0; index < Targets.Count; index++)
                {
                    MChar target = Targets[index];
                    if (target != null && (hitDefId < 0 || target.Id == hitDefId))
                    {
                        selected.Add(target);
                    }
                }
            }

            if (matchIndex >= 0)
            {
                List<MChar> single = new List<MChar>();
                if (matchIndex < selected.Count)
                {
                    single.Add(selected[matchIndex]);
                }
                return single;
            }
            return selected;
        }

        public MChar Clone()
        {
            MChar c = new MChar
            {
                Name = Name, StageInfoName = StageInfoName, Id = Id,
                Time = Time, StateNo = StateNo, PrevStateNo = PrevStateNo,
                StateType = StateType, PrevStateType = PrevStateType, MoveType = MoveType, Physics = Physics, Ctrl = Ctrl,
                AnimNo = AnimNo, PrevAnimNo = PrevAnimNo,
                Life = Life, LifeMax = LifeMax, Power = Power, PowerMax = PowerMax, Juggle = Juggle,
                AttackMul = AttackMul, CustomDefense = CustomDefense, SuperDefenseMul = SuperDefenseMul,
                FallDefenseMul = FallDefenseMul, DefenseMulDelay = DefenseMulDelay,
                Hitstop = Hitstop, PendingLifeDamage = PendingLifeDamage, PendingTransition = PendingTransition,
                PersistCounters = new Dictionary<int, int>(PersistCounters),
                BindTarget = BindTarget, BindTime = BindTime, BindPos = BindPos, BindFacing = BindFacing,
                HitCount = HitCount, UniqHitCount = UniqHitCount, GuardCount = GuardCount, ReceivedHits = ReceivedHits,
                MoveContact = MoveContact, MoveHit = MoveHit, MoveGuarded = MoveGuarded, MoveReversed = MoveReversed,
                PalNo = PalNo, AnimTime = AnimTime, AnimElemNo = AnimElemNo, AssertFlags = AssertFlags,
                AnimElem = AnimElem, AnimElemTime = AnimElemTime, AnimCurTime = AnimCurTime,
                AnimLoopEnd = AnimLoopEnd, AnimRunningNo = AnimRunningNo,
                Ghv = Ghv.Clone(), FallTime = FallTime,
                CommandList = CommandList != null ? CommandList.Clone() : null,
                Input = Input != null ? Input.Clone() : null,
                KeyCtrl = KeyCtrl, AirJumpCount = AirJumpCount,
                Constants = Constants,   // 不可变配置，浅拷引用
                AnimTable = AnimTable,   // 不可变配置，浅拷引用（同 Constants，不进哈希）
                Resources = Resources, OwnData = OwnData,
                PlayerNo = PlayerNo, StatePlayerNo = StatePlayerNo,
                AnimPlayerNo = AnimPlayerNo, SpritePlayerNo = SpritePlayerNo,
                Rng = Rng,   // 共享可变随机源：浅拷引用，引擎级快照统一重链（同 redirect 链接）；哈希在引擎层混入一次

                HitDef = HitDef.Clone(),
                Clsn1 = Clsn1, Clsn2 = Clsn2,   // 帧派生数据，浅引用（由 Anim 系统每帧重填）
                Guarding = Guarding, HitByAttr = HitByAttr, HitByTime = HitByTime, HitByIsNot = HitByIsNot,
                Pause = Pause,   // 共享全局暂停态：浅拷引用，引擎级快照统一重链（同 Rng）
                PauseMovetime = PauseMovetime, SuperMovetime = SuperMovetime,
                PauseBool = PauseBool, Acttmp = Acttmp, UnhittableTime = UnhittableTime,
                PosFreeze = PosFreeze,
                WidthPlayerFront = WidthPlayerFront, WidthPlayerBack = WidthPlayerBack,
                WidthEdgeFront = WidthEdgeFront, WidthEdgeBack = WidthEdgeBack,
                PlayerPushEnabled = PlayerPushEnabled, PushPriority = PushPriority, PushAffectTeam = PushAffectTeam,
                ScreenBoundEnabled = ScreenBoundEnabled, ScreenBoundMoveCameraX = ScreenBoundMoveCameraX,
                ScreenBoundMoveCameraY = ScreenBoundMoveCameraY, ScreenBoundStageBound = ScreenBoundStageBound,
                MoveContactTime = MoveContactTime, CounterHit = CounterHit, RoundState = RoundState,
                World = World, IsHelper = IsHelper, HelperType = HelperType, Destroyed = Destroyed,
                ProjectileContactId = ProjectileContactId, ProjectileContactType = ProjectileContactType,
                ProjectileContactTime = ProjectileContactTime, AttackDistX = AttackDistX,
                AngleRot = AngleRot, AngleRotX = AngleRotX, AngleRotY = AngleRotY,
                AngleDrawScaleX = AngleDrawScaleX, AngleDrawScaleY = AngleDrawScaleY, AngleDraw = AngleDraw,
                Trans = Trans, AlphaSrc = AlphaSrc, AlphaDst = AlphaDst,
                SprPriority = SprPriority, LayerNo = LayerNo,
                OffsetX = OffsetX, OffsetY = OffsetY, WinQuote = WinQuote,
                HitOverrides = (MHitOverride[])HitOverrides.Clone(),   // 值类型数组，浅拷贝即深拷
                ReversalDef = ReversalDef != null ? ReversalDef.Clone() : null,
                Pos = Pos, OldPos = OldPos, Vel = Vel, Facing = Facing,
                IntVars = new Dictionary<int, int>(IntVars),
                FloatVars = new Dictionary<int, FFloat>(FloatVars),
                SysIntVars = new Dictionary<int, int>(SysIntVars),
                SysFloatVars = new Dictionary<int, FFloat>(SysFloatVars),
                // redirect 链接是结构性引用：浅拷引用本身（指向旧图），由 World 在快照后统一重链到克隆图，
                // 避免在此深拷造成无限递归。Targets 列表新建容器但元素仍为旧引用，同样待重链。
                P2 = P2, Root = Root, Parent = Parent, Partner = Partner, StateOwner = StateOwner,
                Targets = new List<MChar>(Targets),
                TargetRefs = new List<MTargetRef>(TargetRefs),
            };
            return c;
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(Time); hash.AddInt32(StateNo); hash.AddInt32(PrevStateNo);
            hash.AddString(StageInfoName ?? "");
            hash.AddInt32(StateType); hash.AddInt32(PrevStateType); hash.AddInt32(MoveType); hash.AddInt32(Physics);
            hash.AddBool(Ctrl);
            hash.AddInt32(AnimNo); hash.AddInt32(PrevAnimNo);
            hash.AddInt32(PlayerNo); hash.AddInt32(StatePlayerNo);
            hash.AddInt32(AnimPlayerNo); hash.AddInt32(SpritePlayerNo);
            hash.AddInt32(Life); hash.AddInt32(LifeMax); hash.AddInt32(Power); hash.AddInt32(PowerMax); hash.AddInt32(Juggle);
            hash.AddFixed(AttackMul); hash.AddFixed(CustomDefense); hash.AddFixed(SuperDefenseMul);
            hash.AddFixed(FallDefenseMul); hash.AddBool(DefenseMulDelay);
            hash.AddInt32(Hitstop); hash.AddInt32(PendingLifeDamage);
            hash.AddBool(PendingTransition.Active); hash.AddInt32(PendingTransition.StateNo);
            hash.AddInt32(PendingTransition.OwnerPlayerNo); hash.AddInt32(PendingTransition.AnimNo);
            hash.AddInt32(PendingTransition.Ctrl);
            HashVars(ref hash, PersistCounters);
            hash.AddInt32(BindTarget != null ? BindTarget.Id : -1); hash.AddInt32(BindTime); hash.AddFixed(BindPos); hash.AddInt32(BindFacing);
            hash.AddInt32(HitCount); hash.AddInt32(UniqHitCount); hash.AddInt32(GuardCount); hash.AddInt32(ReceivedHits);
            hash.AddInt32(MoveContact); hash.AddInt32(MoveHit); hash.AddInt32(MoveGuarded); hash.AddInt32(MoveReversed);
            hash.AddInt32(PalNo); hash.AddInt32(AnimTime); hash.AddInt32(AnimElemNo); hash.AddInt32(AssertFlags);
            hash.AddInt32(AnimElem); hash.AddInt32(AnimElemTime); hash.AddInt32(AnimCurTime);
            hash.AddBool(AnimLoopEnd); hash.AddInt32(AnimRunningNo);
            Ghv.WriteHash(ref hash); hash.AddInt32(FallTime);
            if (CommandList != null) { CommandList.WriteHash(ref hash); }
            if (Input != null) { Input.WriteHash(ref hash); }
            hash.AddBool(KeyCtrl); hash.AddInt32(AirJumpCount);
            HitDef.WriteHash(ref hash);
            hash.AddBool(Guarding); hash.AddInt32(HitByAttr); hash.AddInt32(HitByTime); hash.AddBool(HitByIsNot);
            hash.AddInt32(PauseMovetime); hash.AddInt32(SuperMovetime);
            hash.AddBool(PauseBool); hash.AddInt32(Acttmp); hash.AddInt32(UnhittableTime);
            hash.AddBool(PosFreeze);
            hash.AddFixed(WidthPlayerFront); hash.AddFixed(WidthPlayerBack);
            hash.AddFixed(WidthEdgeFront); hash.AddFixed(WidthEdgeBack);
            hash.AddBool(PlayerPushEnabled); hash.AddInt32(PushPriority); hash.AddInt32(PushAffectTeam);
            hash.AddBool(ScreenBoundEnabled); hash.AddBool(ScreenBoundMoveCameraX);
            hash.AddBool(ScreenBoundMoveCameraY); hash.AddBool(ScreenBoundStageBound);
            hash.AddInt32(MoveContactTime); hash.AddBool(CounterHit); hash.AddInt32(RoundState);
            hash.AddBool(IsHelper); hash.AddInt32(HelperType); hash.AddBool(Destroyed);
            hash.AddInt32(ProjectileContactId); hash.AddInt32(ProjectileContactType); hash.AddInt32(ProjectileContactTime);
            hash.AddFixed(AttackDistX);
            hash.AddFixed(AngleRot); hash.AddFixed(AngleRotX); hash.AddFixed(AngleRotY);
            hash.AddFixed(AngleDrawScaleX); hash.AddFixed(AngleDrawScaleY); hash.AddBool(AngleDraw);
            hash.AddInt32((int)Trans); hash.AddInt32(AlphaSrc); hash.AddInt32(AlphaDst);
            hash.AddInt32(SprPriority); hash.AddInt32(LayerNo);
            hash.AddFixed(OffsetX); hash.AddFixed(OffsetY); hash.AddInt32(WinQuote);
            for (int ho = 0; ho < HitOverrides.Length; ho++) { HitOverrides[ho].WriteHash(ref hash); }
            if (ReversalDef != null) { ReversalDef.WriteHash(ref hash); }
            hash.AddFixed(Pos); hash.AddFixed(OldPos); hash.AddFixed(Vel); hash.AddFixed(Facing);
            hash.AddInt32(Id);
            HashVars(ref hash, IntVars);
            HashFloatVars(ref hash, FloatVars);
            HashVars(ref hash, SysIntVars);
            HashFloatVars(ref hash, SysFloatVars);
            // redirect 链接不递归哈希（被引 Char 各自 WriteHash）；混入 target id + 自定义状态归属 id（影响跑哪张状态表）
            hash.AddInt32(P2 != null ? P2.Id : -1);
            hash.AddInt32(Root != null ? Root.Id : -1);
            hash.AddInt32(Parent != null ? Parent.Id : -1);
            hash.AddInt32(StateOwner != null ? StateOwner.Id : -1);
            hash.AddInt32(Partner != null ? Partner.Id : -1);
            hash.AddInt32(Targets.Count);
            for (int t = 0; t < Targets.Count; t++)
            {
                hash.AddInt32(Targets[t] != null ? Targets[t].Id : -1);
            }
            hash.AddInt32(TargetRefs.Count);
            for (int t = 0; t < TargetRefs.Count; t++)
            {
                hash.AddInt32(TargetRefs[t].Target != null ? TargetRefs[t].Target.Id : -1);
                hash.AddInt32(TargetRefs[t].HitDefId);
            }
        }

        // 字典哈希按 key 升序，保证两端顺序无关的确定性
        static void HashVars(ref Hash64 hash, Dictionary<int, int> vars)
        {
            hash.AddInt32(vars.Count);
            List<int> keys = new List<int>(vars.Keys);
            keys.Sort();
            for (int k = 0; k < keys.Count; k++)
            {
                hash.AddInt32(keys[k]);
                hash.AddInt32(vars[keys[k]]);
            }
        }

        static void HashFloatVars(ref Hash64 hash, Dictionary<int, FFloat> vars)
        {
            hash.AddInt32(vars.Count);
            List<int> keys = new List<int>(vars.Keys);
            keys.Sort();
            for (int k = 0; k < keys.Count; k++)
            {
                hash.AddInt32(keys[k]);
                hash.AddFixed(vars[keys[k]]);
            }
        }

        // ───────── IExprContext：trigger/redirect opcode → 本 Char 取值 ─────────

        public BytecodeValue ReadTrigger(OpCode op, byte[] code, ref int i, List<BytecodeValue> stack)
        {
            switch (op)
            {
                case OpCode.OC_time: return BytecodeValue.Int(Time);
                case OpCode.OC_stateno: return BytecodeValue.Int(StateNo);
                case OpCode.OC_prevstateno: return BytecodeValue.Int(PrevStateNo);
                // statetype/movetype：消费 1 字节类型掩码，返回是否相等（对齐 Ikemen OC_statetype/OC_movetype）。
                // 编码：编译器为 `statetype = S` 发 OC_statetype + 掩码(S=1/C=2/A=4/L=8)；多字母用 OR 串联。
                case OpCode.OC_statetype:
                {
                    int mask = code[i]; i++;
                    return BytecodeValue.Bool(StateType == mask);
                }
                case OpCode.OC_movetype:
                {
                    int mtype = code[i]; i++;
                    return BytecodeValue.Bool(MoveType == mtype);   // 我方 MoveType 存小码 I=1/H=2/A=4
                }
                case OpCode.OC_ctrl: return BytecodeValue.Bool(Ctrl);
                case OpCode.OC_anim: return BytecodeValue.Int(AnimNo);
                case OpCode.OC_pos_x: return BytecodeValue.Float(Pos.X);
                case OpCode.OC_pos_y: return BytecodeValue.Float(Pos.Y);
                case OpCode.OC_vel_x: return BytecodeValue.Float(Vel.X);
                case OpCode.OC_vel_y: return BytecodeValue.Float(Vel.Y);
                case OpCode.OC_vel_z: return BytecodeValue.Float(Vel.Z);
                case OpCode.OC_facing: return BytecodeValue.Int(Facing.Raw >= 0 ? 1 : -1);

                // p2dist X/Y、p2bodydist X/Y：到 P2 的距离（1v1，无敌人 → undefined，VM 跳过整块）。
                // 对齐 Ikemen char.go:8743 distX/rdDistX：X 朝向相对（前为正）、|·|<0.0001 归零；Y 不翻向。
                // bodydist = 边到边（MUGEN：p2dist 减去双方前宽，char.go:8787 简化形）。
                case OpCode.OC_p2dist_x:
                    return P2 != null ? BytecodeValue.Float(DistX(P2)) : BytecodeValue.Undefined();
                case OpCode.OC_p2dist_y:
                    return P2 != null ? BytecodeValue.Float(P2.Pos.Y - Pos.Y) : BytecodeValue.Undefined();
                case OpCode.OC_p2bodydist_x:
                    return P2 != null ? BytecodeValue.Float(BodyDistX(P2)) : BytecodeValue.Undefined();
                case OpCode.OC_p2bodydist_y:
                    return P2 != null ? BytecodeValue.Float(P2.Pos.Y - Pos.Y) : BytecodeValue.Undefined();
                case OpCode.OC_life: return BytecodeValue.Int(Life);
                case OpCode.OC_lifemax: return BytecodeValue.Int(LifeMax);
                case OpCode.OC_power: return BytecodeValue.Int(Power);
                case OpCode.OC_powermax: return BytecodeValue.Int(PowerMax);
                case OpCode.OC_alive: return BytecodeValue.Bool(Alive);

                // random：返回 [0,999]（移植 bytecode.go:2308 OC_random → Rand(0,999)）。
                // 推进共享种子；无随机源时退化返 0（不崩）。
                case OpCode.OC_random:
                    return BytecodeValue.Int(Rng != null ? Rng.Rand(0, 999) : 0);

                case OpCode.OC_command:
                {
                    // 编码：OC_command + [1字节名长] + ASCII 名字
                    int len = code[i]; i++;
                    string cmdName = System.Text.Encoding.ASCII.GetString(code, i, len);
                    i += len;
                    return BytecodeValue.Bool(CommandList != null && CommandList.IsActive(cmdName));
                }

                case OpCode.OC_name:
                {
                    // name = "x"：与角色 Name 精确比较（编码同 OC_command：[1字节名长]+ASCII）。
                    int len = code[i]; i++;
                    string wanted = System.Text.Encoding.ASCII.GetString(code, i, len);
                    i += len;
                    return BytecodeValue.Bool(Name == wanted);
                }

                case OpCode.OC_stagevar_info_name:
                {
                    int len = code[i]; i++;
                    string wanted = System.Text.Encoding.ASCII.GetString(code, i, len);
                    i += len;
                    return BytecodeValue.Bool((StageInfoName ?? "") == wanted);
                }

                // CharSystemVar 常用 trigger
                case OpCode.OC_id: return BytecodeValue.Int(Id);
                case OpCode.OC_palno: return BytecodeValue.Int(PalNo);
                case OpCode.OC_hitpausetime: return BytecodeValue.Int(Hitstop);
                case OpCode.OC_hitcount: return BytecodeValue.Int(HitCount);
                case OpCode.OC_uniqhitcount: return BytecodeValue.Int(UniqHitCount);
                case OpCode.OC_movecontact: return BytecodeValue.Int(MoveContact);
                case OpCode.OC_movehit: return BytecodeValue.Int(MoveHit);
                case OpCode.OC_moveguarded: return BytecodeValue.Int(MoveGuarded);
                case OpCode.OC_movereversed: return BytecodeValue.Int(MoveReversed);
                case OpCode.OC_animtime: return BytecodeValue.Int(AnimTime);
                case OpCode.OC_animelemno: return BytecodeValue.Int(AnimElemNo);
                case OpCode.OC_animelemtime:
                {
                    // animelemtime(n)：自元素 n（1-based）起已播 tick。当前元素精确返 AnimElemTime；
                    // 其他元素按累积起始时间推算（移植 anim.go AnimElemTime）。
                    int n = Pop(stack).ToI();
                    return BytecodeValue.Int(ComputeAnimElemTime(n));
                }
                case OpCode.OC_animelem:
                {
                    // animelem = n：到达元素 n 的首帧（当前元素号 == n 且本元素已播 0 tick）。
                    int n = Pop(stack).ToI();
                    return BytecodeValue.Bool(AnimElemNo == n && AnimElemTime == 0);
                }
                case OpCode.OC_numtarget: return BytecodeValue.Int(Targets.Count);
                case OpCode.OC_jugglepoints:
                {
                    int targetId = Pop(stack).ToI();
                    return BytecodeValue.Int(JugglePoints(targetId));
                }
                case OpCode.OC_ishelper:
                {
                    // 弹 id：-1=无参(只判是否 helper)；否则 IsHelper 且 HelperType==id。
                    int id = Pop(stack).ToI();
                    return BytecodeValue.Bool(IsHelper && (id < 0 || HelperType == id));
                }
                case OpCode.OC_numhelper:
                {
                    int id = Pop(stack).ToI();
                    return BytecodeValue.Int(World != null ? World.CountHelpers(id, Root != null ? Root.Id : Id) : 0);
                }
                case OpCode.OC_numproj:
                {
                    int id = Pop(stack).ToI();
                    return BytecodeValue.Int(World != null ? World.CountProjectiles(id, Id) : 0);
                }
                case OpCode.OC_numexplod:
                {
                    int id = Pop(stack).ToI();
                    return BytecodeValue.Int(World != null ? World.CountExplods(id, Id) : 0);
                }
                case OpCode.OC_projcontacttime:
                {
                    int id = Pop(stack).ToI();
                    return ProjectileContactTimeValue(id, 3);
                }
                case OpCode.OC_projhittime:
                {
                    int id = Pop(stack).ToI();
                    return ProjectileContactTimeValue(id, 0);
                }
                case OpCode.OC_projguardedtime:
                {
                    int id = Pop(stack).ToI();
                    return ProjectileContactTimeValue(id, 1);
                }
                case OpCode.OC_projcanceltime:
                {
                    int id = Pop(stack).ToI();
                    return ProjectileContactTimeValue(id, 2);
                }
                case OpCode.OC_roundstate: return BytecodeValue.Int(RoundState);
                case OpCode.OC_inguarddist:
                    // 对手在攻击态(MoveType=A=4)且水平体距在对手 AttackDist 范围内 → 可进入守招判定。
                    return BytecodeValue.Bool(P2 != null && P2.MoveType == 4 && AbsDistX(P2) <= P2.AttackDistX);

                // 受击触发器（common1 5000-5160 状态机用）
                case OpCode.OC_hitshakeover: return BytecodeValue.Bool(HitShakeOver());
                case OpCode.OC_hitover: return BytecodeValue.Bool(HitOver());
                case OpCode.OC_hitfall: return BytecodeValue.Bool(Ghv.Fall);
                case OpCode.OC_canrecover: return BytecodeValue.Bool(CanRecover());

                case OpCode.OC_animexist:
                {
                    // animexist(n)/selfanimexist(n)：弹参数 n，查本角色动画表是否存在编号 n。
                    // animexist 查当前 AnimPlayerNo 表；undefined 参数透传 undefined。
                    BytecodeValue anim = Pop(stack);
                    if (anim.IsUndefined())
                    {
                        return BytecodeValue.Undefined();
                    }
                    return BytecodeValue.Bool(AnimExists(anim.ToI()));
                }
                case OpCode.OC_selfanimexist:
                {
                    // selfanimexist 查自身 PlayerNo 表。ChangeAnim2 后它必须与 animexist 可分歧。
                    BytecodeValue anim = Pop(stack);
                    if (anim.IsUndefined())
                    {
                        return BytecodeValue.Undefined();
                    }
                    return BytecodeValue.Bool(SelfAnimExists(anim.ToI()));
                }

                case OpCode.OC_const_:
                {
                    // const(field)：OC_const_ + 字段id 字节，从不可变常量集读取
                    MConstId constId = (MConstId)code[i]; i++;
                    return Constants != null ? Constants.Read(constId) : BytecodeValue.Int(0);
                }
                case OpCode.OC_ex_:
                {
                    // gethitvar(field)：OC_ex_ + 字段id 字节，从 Ghv 读取
                    int fieldId = code[i]; i++;
                    return ReadGetHitVar(fieldId);
                }
                case OpCode.OC_ex2_:
                {
                    // prevstatetype = X：OC_ex2_ + 掩码，比较上一状态 statetype
                    int mask = code[i]; i++;
                    return BytecodeValue.Bool(PrevStateType == mask);
                }

                default:
                    return BytecodeValue.Undefined();   // 尚未接入的 trigger（增量补全）
            }
        }

        int JugglePoints(int targetId)
        {
            int max = Constants != null ? Constants.Airjuggle : 15;
            for (int i = 0; i < Targets.Count; i++)
            {
                MChar target = Targets[i];
                if (target != null && target.Id == targetId)
                {
                    return target.Ghv.GetJuggle(Id, max);
                }
            }
            return max;
        }

        // gethitvar 字段 id → Ghv 值（id 见 MugenExprCompiler.GetHitVarFieldId）。
        BytecodeValue ReadGetHitVar(int fieldId)
        {
            switch (fieldId)
            {
                case 0: return BytecodeValue.Float(Ghv.XVel);
                case 1: return BytecodeValue.Float(Ghv.YVel);
                case 2: return BytecodeValue.Float(Ghv.ZVel);
                case 3: return BytecodeValue.Int(Ghv.HitTime);
                case 4: return BytecodeValue.Int(Ghv.SlideTime);
                case 5: return BytecodeValue.Int(Ghv.CtrlTime);
                case 6: return BytecodeValue.Int(Ghv.HitShakeTime);
                case 7: return BytecodeValue.Int(Ghv.Damage);
                case 8: return BytecodeValue.Int(Ghv.HitCount);
                case 9: return BytecodeValue.Int(Ghv.FallCount);
                case 10: return BytecodeValue.Int(Ghv.AnimType);
                case 11: return BytecodeValue.Int(Ghv.AttrType);
                case 12: return BytecodeValue.Bool(Ghv.Fall);
                case 13: return BytecodeValue.Bool(Ghv.Guarded);
                case 14: return BytecodeValue.Int(Ghv.GroundType);
                case 15: return BytecodeValue.Int(Ghv.AirType);
                case 16: return BytecodeValue.Float(Ghv.YAccel);
                case 17: return BytecodeValue.Float(Ghv.FallYVel);
                case 18: return BytecodeValue.Float(Ghv.FallXVel);
                case 19: return BytecodeValue.Int(Ghv.FallRecoverTime);
                case 20: return BytecodeValue.Bool(Ghv.FallRecover);
                default: return BytecodeValue.Int(0);
            }
        }

        // redirect opcode → 返回被重定向到的 Char（不存在则 null，VM 压 Undefined 并跳过整块）。
        public IExprContext Redirect(OpCode op, List<BytecodeValue> stack)
        {
            switch (op)
            {
                case OpCode.OC_root: return Root;
                case OpCode.OC_parent: return Parent;
                case OpCode.OC_p2: return P2;
                case OpCode.OC_stateowner: return StateOwner;
                case OpCode.OC_enemy:
                case OpCode.OC_enemynear:
                {
                    // enemy(n) / enemynear(n)：弹索引。1v1 中唯一敌人 = P2（索引 0）；
                    // 其余索引无对应 → null（VM 压 Undefined 并跳过整块）。多敌队战是后续工作。
                    int index = Pop(stack).ToI();
                    return index == 0 ? P2 : null;
                }
                case OpCode.OC_target:
                {
                    // 弹目标 id（<0 表示任意 → 取第一个）。对齐我方编译器约定（Ikemen 原弹 2 个参数）。
                    int wantId = Pop(stack).ToI();
                    List<MChar> targets = SelectTargetsByHitId(wantId, -1);
                    return targets.Count > 0 ? targets[0] : null;
                }
                case OpCode.OC_helper:
                {
                    int matchIndex = Pop(stack).ToI();
                    int helperType = Pop(stack).ToI();
                    return FindHelperRedirect(helperType, matchIndex);
                }
                case OpCode.OC_partner:
                {
                    int index = Pop(stack).ToI();
                    return index == 0 ? Partner : null;
                }
                default:
                    return null;   // 其余 redirect（helper/enemy/partner/...）后续补
            }
        }

        public BytecodeValue ReadVariable(OpCode op, int index)
        {
            switch (op)
            {
                case OpCode.OC_var:
                    return BytecodeValue.Int(IntVars.TryGetValue(index, out int intValue) ? intValue : 0);
                case OpCode.OC_sysvar:
                    return BytecodeValue.Int(SysIntVars.TryGetValue(index, out int sysIntValue) ? sysIntValue : 0);
                case OpCode.OC_fvar:
                    return BytecodeValue.Float(FloatVars.TryGetValue(index, out FFloat floatValue) ? floatValue : FFloat.Zero);
                case OpCode.OC_sysfvar:
                    return BytecodeValue.Float(SysFloatVars.TryGetValue(index, out FFloat sysFloatValue) ? sysFloatValue : FFloat.Zero);
                default:
                    return BytecodeValue.Undefined();
            }
        }

        public IExprContext Redirect(OpCode op, BytecodeValue firstArgument, BytecodeValue secondArgument)
        {
            switch (op)
            {
                case OpCode.OC_target:
                    return FindTargetRedirect(firstArgument.ToI(), secondArgument.ToI());
                case OpCode.OC_helper:
                    return FindHelperRedirect(firstArgument.ToI(), secondArgument.ToI());
                default:
                    return null;
            }
        }

        IExprContext FindTargetRedirect(int targetId, int matchIndex)
        {
            if (matchIndex < 0)
            {
                return null;
            }
            List<MChar> targets = SelectTargetsByHitId(targetId, matchIndex);
            return targets.Count > 0 ? targets[0] : null;
        }

        IExprContext FindHelperRedirect(int helperType, int matchIndex)
        {
            if (matchIndex < 0 || World == null)
            {
                return null;
            }
            MChar root = Root ?? this;
            int seen = 0;
            for (int index = 0; index < World.Helpers.Count; index++)
            {
                MChar helper = World.Helpers[index];
                if (helper == null || helper.Destroyed)
                {
                    continue;
                }
                MChar helperRoot = helper.Root ?? helper;
                if (!ReferenceEquals(helperRoot, root))
                {
                    continue;
                }
                if (helperType > 0 && helper.HelperType != helperType)
                {
                    continue;
                }
                if (seen == matchIndex)
                {
                    return helper;
                }
                seen++;
            }
            return null;
        }

        static BytecodeValue Pop(List<BytecodeValue> stack)
        {
            if (stack.Count == 0)
            {
                return BytecodeValue.Undefined();
            }
            BytecodeValue v = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            return v;
        }
    }
}
