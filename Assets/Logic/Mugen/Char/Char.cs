// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go  (Char + StateState 核心字段)
// Adapted to fixed-point. M3 骨架：仅核心 trigger 字段 + Clone/WriteHash(接回滚) + IExprContext(常用 trigger)。
// 完整 ~200 字段(CharSystemVar/hitdef/ghv/targets...)逐步补。See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Expr;

namespace Lockstep.Mugen.Char
{
    /// <summary>
    /// MUGEN 角色运行态（移植 Ikemen Char，定点化）。M3 骨架版。
    /// 实现 <see cref="IExprContext"/>：表达式 VM 的 trigger/redirect opcode 从本 Char 读值。
    /// StateType/MoveType/Physics 暂用 int32 原始 MUGEN 码（枚举映射归 M2 编译器/System）。
    /// </summary>
    public sealed class MChar : IExprContext
    {
        public string Name;

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

        // 命中系统（M7）：当前 HitDef + 本帧 Clsn 框（Clsn 为动画派生，由 Anim 系统 M8 填，Clone 浅拷不哈希）。
        public Hit.MHitDef HitDef = new Hit.MHitDef();
        public Hit.MClsnBox[] Clsn1;   // 攻击框
        public Hit.MClsnBox[] Clsn2;   // 受击框
        public bool Guarding;          // 守方是否正在防御（守招判定入口；真实防御检测由命令/守招状态后续接）
        // HitBy/NotHitBy 属性免疫过滤槽（time>0 生效；IsNot=NotHitBy）。每帧递减。
        public int HitByAttr;
        public int HitByTime;
        public bool HitByIsNot;

        // 状态机：待应用的切换（>=0 表示本帧要 ChangeState 到此号）
        public int PendingStateNo = -1;
        public bool PendingIsSelf;       // 待切换是否为 SelfState（用自身状态表）
        // persistent 计数：当前状态内各控制器(按 index)已触发帧数，进入新状态时清空（仅作用当前状态）
        public Dictionary<int, int> PersistCounters = new Dictionary<int, int>();

        // 物理
        public FVector3 Pos;
        public FVector3 OldPos;
        public FVector3 Vel;
        public FFloat Facing = FFloat.One;   // +1 右 / -1 左

        // CNS 变量：var(n)/fvar(n)
        public Dictionary<int, int> IntVars = new Dictionary<int, int>();
        public Dictionary<int, FFloat> FloatVars = new Dictionary<int, FFloat>();

        // ───────── redirect 链接（结构性引用，非拥有；Clone 浅拷、Hash 不递归被引者）─────────
        public int Id;                       // 本角色实例 id（playerid / target 匹配用）
        public MChar P2;                     // 对手（1v1 中即对方）
        public MChar Root;                   // 根角色（非 helper 时通常 = 自身）
        public MChar Parent;                 // 父角色（helper 的创建者；root 为 null）
        public List<MChar> Targets = new List<MChar>();   // 本角色 HitDef 命中的目标

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

        /// <summary>动画表中是否存在该动画号（无表则 false；animexist/selfanimexist trigger 用）。</summary>
        public bool AnimExists(int animNo)
        {
            return AnimTable != null && AnimTable.ContainsKey(animNo);
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
            return AnimTable == null || AnimTable.ContainsKey(animNo);
        }

        // ───────── 回滚支持 ─────────

        public MChar Clone()
        {
            MChar c = new MChar
            {
                Name = Name, Id = Id,
                Time = Time, StateNo = StateNo, PrevStateNo = PrevStateNo,
                StateType = StateType, PrevStateType = PrevStateType, MoveType = MoveType, Physics = Physics, Ctrl = Ctrl,
                AnimNo = AnimNo, PrevAnimNo = PrevAnimNo,
                Life = Life, LifeMax = LifeMax, Power = Power, PowerMax = PowerMax, Juggle = Juggle,
                AttackMul = AttackMul, CustomDefense = CustomDefense, SuperDefenseMul = SuperDefenseMul,
                FallDefenseMul = FallDefenseMul, DefenseMulDelay = DefenseMulDelay,
                Hitstop = Hitstop, PendingStateNo = PendingStateNo, PendingIsSelf = PendingIsSelf,
                PersistCounters = new Dictionary<int, int>(PersistCounters),
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
                Rng = Rng,   // 共享可变随机源：浅拷引用，引擎级快照统一重链（同 redirect 链接）；哈希在引擎层混入一次

                HitDef = HitDef.Clone(),
                Clsn1 = Clsn1, Clsn2 = Clsn2,   // 帧派生数据，浅引用（由 Anim 系统每帧重填）
                Guarding = Guarding, HitByAttr = HitByAttr, HitByTime = HitByTime, HitByIsNot = HitByIsNot,
                Pos = Pos, OldPos = OldPos, Vel = Vel, Facing = Facing,
                IntVars = new Dictionary<int, int>(IntVars),
                FloatVars = new Dictionary<int, FFloat>(FloatVars),
                // redirect 链接是结构性引用：浅拷引用本身（指向旧图），由 World 在快照后统一重链到克隆图，
                // 避免在此深拷造成无限递归。Targets 列表新建容器但元素仍为旧引用，同样待重链。
                P2 = P2, Root = Root, Parent = Parent,
                Targets = new List<MChar>(Targets),
            };
            return c;
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(Time); hash.AddInt32(StateNo); hash.AddInt32(PrevStateNo);
            hash.AddInt32(StateType); hash.AddInt32(PrevStateType); hash.AddInt32(MoveType); hash.AddInt32(Physics);
            hash.AddBool(Ctrl);
            hash.AddInt32(AnimNo); hash.AddInt32(PrevAnimNo);
            hash.AddInt32(Life); hash.AddInt32(LifeMax); hash.AddInt32(Power); hash.AddInt32(PowerMax); hash.AddInt32(Juggle);
            hash.AddFixed(AttackMul); hash.AddFixed(CustomDefense); hash.AddFixed(SuperDefenseMul);
            hash.AddFixed(FallDefenseMul); hash.AddBool(DefenseMulDelay);
            hash.AddInt32(Hitstop); hash.AddInt32(PendingStateNo); hash.AddBool(PendingIsSelf);
            HashVars(ref hash, PersistCounters);
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
            hash.AddFixed(Pos); hash.AddFixed(OldPos); hash.AddFixed(Vel); hash.AddFixed(Facing);
            hash.AddInt32(Id);
            HashVars(ref hash, IntVars);
            HashFloatVars(ref hash, FloatVars);
            // redirect 链接不递归哈希（被引 Char 各自 WriteHash）；只混入 target id 反映命中关系
            hash.AddInt32(Targets.Count);
            for (int t = 0; t < Targets.Count; t++)
            {
                hash.AddInt32(Targets[t] != null ? Targets[t].Id : -1);
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
                case OpCode.OC_animelem:
                {
                    // animelem = n：到达元素 n 的首帧（当前元素号 == n 且本元素已播 0 tick）。
                    int n = Pop(stack).ToI();
                    return BytecodeValue.Bool(AnimElemNo == n && AnimElemTime == 0);
                }
                case OpCode.OC_numtarget: return BytecodeValue.Int(Targets.Count);

                // 受击触发器（common1 5000-5160 状态机用）
                case OpCode.OC_hitshakeover: return BytecodeValue.Bool(HitShakeOver());
                case OpCode.OC_hitover: return BytecodeValue.Bool(HitOver());
                case OpCode.OC_hitfall: return BytecodeValue.Bool(Ghv.Fall);
                case OpCode.OC_canrecover: return BytecodeValue.Bool(CanRecover());

                case OpCode.OC_animexist:
                case OpCode.OC_selfanimexist:
                {
                    // animexist(n)/selfanimexist(n)：弹参数 n，查本角色动画表是否存在编号 n。
                    // 对齐 Ikemen char.go:5088 animExist / char.go:5102 selfAnimExist——
                    // undefined 参数透传 undefined。v1 单角色无 helper/共享动画表，
                    // animExist(查 animPN 表) 与 selfAnimExist(查 gi 表) 同归本角色 AnimTable；
                    // 二者分歧待 R-ENT 实体系统（helper 借用 root 动画）落地后再细分。
                    BytecodeValue anim = Pop(stack);
                    if (anim.IsUndefined())
                    {
                        return BytecodeValue.Undefined();
                    }
                    return BytecodeValue.Bool(AnimExists(anim.ToI()));
                }

                case OpCode.OC_var:
                {
                    int index = Pop(stack).ToI();
                    return BytecodeValue.Int(IntVars.TryGetValue(index, out int v) ? v : 0);
                }
                case OpCode.OC_fvar:
                {
                    int index = Pop(stack).ToI();
                    return BytecodeValue.Float(FloatVars.TryGetValue(index, out FFloat v) ? v : FFloat.Zero);
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
                    for (int t = 0; t < Targets.Count; t++)
                    {
                        if (Targets[t] != null && (wantId < 0 || Targets[t].Id == wantId))
                        {
                            return Targets[t];
                        }
                    }
                    return null;
                }
                default:
                    return null;   // 其余 redirect（helper/enemy/partner/...）后续补
            }
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
