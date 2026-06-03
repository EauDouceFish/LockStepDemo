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

        // 打击感
        public int Hitstop;         // = Ikemen hitPauseTime（hitpausetime trigger）

        // CharSystemVar 常用计数/标志（攻击命中统计，供 trigger 与控制器读）
        public int HitCount;        // 本招累计命中数
        public int UniqHitCount;    // 去重命中数
        public int MoveContact;     // 本招是否接触(命中或被防)：>0
        public int MoveHit;         // 本招是否命中：>0
        public int MoveGuarded;     // 本招是否被防：>0
        public int MoveReversed;    // 本招是否被打断/反击：>0
        public int PalNo = 1;       // 调色板号（palno trigger）

        // 动画运行态（由 Anim 系统 M8 维护；此处提供字段供 trigger 读）
        public int AnimTime;        // 当前动画剩余时间（MUGEN 惯例 ≤0）
        public int AnimElemNo;      // 当前动画元素序号（1-based）

        // AssertSpecial 标志位（每帧清空，须每帧重断言才保持）。见 MAssertFlag。
        public int AssertFlags;

        // 受击变量（命中系统 M7 填值；此处随 Char 快照/哈希）
        public MGetHitVar Ghv = new MGetHitVar();

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

        // ───────── 回滚支持 ─────────

        public MChar Clone()
        {
            MChar c = new MChar
            {
                Name = Name, Id = Id,
                Time = Time, StateNo = StateNo, PrevStateNo = PrevStateNo,
                StateType = StateType, MoveType = MoveType, Physics = Physics, Ctrl = Ctrl,
                AnimNo = AnimNo, PrevAnimNo = PrevAnimNo,
                Life = Life, LifeMax = LifeMax, Power = Power, PowerMax = PowerMax, Juggle = Juggle,
                Hitstop = Hitstop, PendingStateNo = PendingStateNo, PendingIsSelf = PendingIsSelf,
                PersistCounters = new Dictionary<int, int>(PersistCounters),
                HitCount = HitCount, UniqHitCount = UniqHitCount,
                MoveContact = MoveContact, MoveHit = MoveHit, MoveGuarded = MoveGuarded, MoveReversed = MoveReversed,
                PalNo = PalNo, AnimTime = AnimTime, AnimElemNo = AnimElemNo, AssertFlags = AssertFlags,
                Ghv = Ghv.Clone(),
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
            hash.AddInt32(StateType); hash.AddInt32(MoveType); hash.AddInt32(Physics);
            hash.AddBool(Ctrl);
            hash.AddInt32(AnimNo); hash.AddInt32(PrevAnimNo);
            hash.AddInt32(Life); hash.AddInt32(LifeMax); hash.AddInt32(Power); hash.AddInt32(PowerMax); hash.AddInt32(Juggle);
            hash.AddInt32(Hitstop); hash.AddInt32(PendingStateNo); hash.AddBool(PendingIsSelf);
            HashVars(ref hash, PersistCounters);
            hash.AddInt32(HitCount); hash.AddInt32(UniqHitCount);
            hash.AddInt32(MoveContact); hash.AddInt32(MoveHit); hash.AddInt32(MoveGuarded); hash.AddInt32(MoveReversed);
            hash.AddInt32(PalNo); hash.AddInt32(AnimTime); hash.AddInt32(AnimElemNo); hash.AddInt32(AssertFlags);
            Ghv.WriteHash(ref hash);
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
                case OpCode.OC_life: return BytecodeValue.Int(Life);
                case OpCode.OC_lifemax: return BytecodeValue.Int(LifeMax);
                case OpCode.OC_power: return BytecodeValue.Int(Power);
                case OpCode.OC_powermax: return BytecodeValue.Int(PowerMax);
                case OpCode.OC_alive: return BytecodeValue.Bool(Alive);

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
                case OpCode.OC_numtarget: return BytecodeValue.Int(Targets.Count);

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

                default:
                    return BytecodeValue.Undefined();   // 尚未接入的 trigger（增量补全）
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
