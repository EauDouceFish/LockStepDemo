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
        public int Hitstop;         // = Ikemen hitPauseTime

        // 状态机：待应用的切换（>=0 表示本帧要 ChangeState 到此号）
        public int PendingStateNo = -1;

        // 物理
        public FVector3 Pos;
        public FVector3 OldPos;
        public FVector3 Vel;
        public FFloat Facing = FFloat.One;   // +1 右 / -1 左

        // CNS 变量：var(n)/fvar(n)
        public Dictionary<int, int> IntVars = new Dictionary<int, int>();
        public Dictionary<int, FFloat> FloatVars = new Dictionary<int, FFloat>();

        public bool Alive => Life > 0;

        // ───────── 回滚支持 ─────────

        public MChar Clone()
        {
            MChar c = new MChar
            {
                Name = Name,
                Time = Time, StateNo = StateNo, PrevStateNo = PrevStateNo,
                StateType = StateType, MoveType = MoveType, Physics = Physics, Ctrl = Ctrl,
                AnimNo = AnimNo, PrevAnimNo = PrevAnimNo,
                Life = Life, LifeMax = LifeMax, Power = Power, PowerMax = PowerMax, Juggle = Juggle,
                Hitstop = Hitstop, PendingStateNo = PendingStateNo,
                Pos = Pos, OldPos = OldPos, Vel = Vel, Facing = Facing,
                IntVars = new Dictionary<int, int>(IntVars),
                FloatVars = new Dictionary<int, FFloat>(FloatVars),
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
            hash.AddInt32(Hitstop); hash.AddInt32(PendingStateNo);
            hash.AddFixed(Pos); hash.AddFixed(OldPos); hash.AddFixed(Vel); hash.AddFixed(Facing);
            HashVars(ref hash, IntVars);
            HashFloatVars(ref hash, FloatVars);
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
                case OpCode.OC_statetype: return BytecodeValue.Int(StateType);
                case OpCode.OC_movetype: return BytecodeValue.Int(MoveType);
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
