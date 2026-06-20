// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go  (BytecodeExp.neg/not/add/sub/mul/div/mod/pow/cmp/range/trig/...)
// Adapted to fixed-point + immutable BytecodeValue (helpers return new value). See Docs/移植方案_Ikemen.md.
using Lockstep.Math;

namespace Lockstep.Mugen.Expr
{
    /// <summary>
    /// 表达式值运算（对应 Ikemen BytecodeExp 的算术/逻辑/比较辅助方法）。
    /// 类型规则严格照搬：<c>Min(a.type, b.type) == Float</c> 走浮点，否则整数。
    /// 除零/模零/非法对数 → Undefined（同 Ikemen，不抛异常）。
    /// </summary>
    public static class BytecodeOps
    {
        // Ikemen reference: src/bytecode.go BytecodeValue type ordering for mixed numeric opcode evaluation.
        static bool IsFloatOp(BytecodeValue a, BytecodeValue b)
        {
            int lo = (int)a.Type < (int)b.Type ? (int)a.Type : (int)b.Type;
            return lo == (int)ValueType.Float;
        }

        // ── 一元 ──
        // Ikemen reference: src/bytecode.go BytecodeExp.neg opcode evaluation.
        public static BytecodeValue Neg(BytecodeValue v)
        {
            return v.Type == ValueType.Float ? BytecodeValue.Float(-v.ToF()) : BytecodeValue.Int(-v.ToI());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.not bitwise opcode evaluation.
        public static BytecodeValue Not(BytecodeValue v)
        {
            return BytecodeValue.Int(~v.ToI());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.blnot boolean opcode evaluation.
        public static BytecodeValue BlNot(BytecodeValue v)
        {
            return BytecodeValue.Bool(!v.ToB());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.abs function opcode evaluation.
        public static BytecodeValue Abs(BytecodeValue v)
        {
            if (v.IsUndefined())
            {
                return v;
            }
            return v.Type == ValueType.Float ? BytecodeValue.Float(FMath.Abs(v.ToF())) : BytecodeValue.Int(System.Math.Abs(v.ToI()));
        }

        // ── 算术 ──
        // Ikemen reference: src/bytecode.go BytecodeExp.add opcode evaluation.
        public static BytecodeValue Add(BytecodeValue a, BytecodeValue b)
        {
            return IsFloatOp(a, b) ? BytecodeValue.Float(a.ToF() + b.ToF()) : BytecodeValue.Int(a.ToI() + b.ToI());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.sub opcode evaluation.
        public static BytecodeValue Sub(BytecodeValue a, BytecodeValue b)
        {
            return IsFloatOp(a, b) ? BytecodeValue.Float(a.ToF() - b.ToF()) : BytecodeValue.Int(a.ToI() - b.ToI());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.mul opcode evaluation.
        public static BytecodeValue Mul(BytecodeValue a, BytecodeValue b)
        {
            return IsFloatOp(a, b) ? BytecodeValue.Float(a.ToF() * b.ToF()) : BytecodeValue.Int(a.ToI() * b.ToI());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.div opcode evaluation and undefined divide-by-zero behavior.
        public static BytecodeValue Div(BytecodeValue a, BytecodeValue b)
        {
            if (b.ToF().Raw == 0)
            {
                return BytecodeValue.Undefined();              // 除零 → undefined
            }
            return IsFloatOp(a, b) ? BytecodeValue.Float(a.ToF() / b.ToF()) : BytecodeValue.Int(a.ToI() / b.ToI());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.mod opcode evaluation and undefined modulo-by-zero behavior.
        public static BytecodeValue Mod(BytecodeValue a, BytecodeValue b)
        {
            if (b.ToI() == 0)
            {
                return BytecodeValue.Undefined();
            }
            return BytecodeValue.Int(a.ToI() % b.ToI());
        }

        // 见 Ikemen pow：任一为 float 或指数<0 → 浮点幂；否则整数快速幂（不复刻 oldVersion 的 bug 分支）。
        // Ikemen reference: src/bytecode.go BytecodeExp.pow opcode evaluation.
        public static BytecodeValue Pow(BytecodeValue a, BytecodeValue b)
        {
            if (IsFloatOp(a, b) || b.ToF().Raw < 0)
            {
                return BytecodeValue.Float(FMath.Pow(a.ToF(), b.ToF()));
            }
            int baseI = a.ToI();
            int expI = b.ToI();
            int result = 1;
            int square = baseI;
            for (int bit = 0; (uint)expI >> bit != 0; bit++)
            {
                if ((expI & (1 << bit)) != 0)
                {
                    result *= square;
                }
                square *= square;
            }
            return BytecodeValue.Int(result);
        }

        // ── 比较（结果 Bool）──
        // Ikemen reference: src/bytecode.go BytecodeExp.eq comparison opcode evaluation.
        public static BytecodeValue Eq(BytecodeValue a, BytecodeValue b)
        {
            return BytecodeValue.Bool(IsFloatOp(a, b) ? a.ToF().Raw == b.ToF().Raw : a.ToI() == b.ToI());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.ne comparison opcode evaluation.
        public static BytecodeValue Ne(BytecodeValue a, BytecodeValue b)
        {
            return BytecodeValue.Bool(IsFloatOp(a, b) ? a.ToF().Raw != b.ToF().Raw : a.ToI() != b.ToI());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.gt comparison opcode evaluation.
        public static BytecodeValue Gt(BytecodeValue a, BytecodeValue b)
        {
            return BytecodeValue.Bool(IsFloatOp(a, b) ? a.ToF() > b.ToF() : a.ToI() > b.ToI());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.ge comparison opcode evaluation.
        public static BytecodeValue Ge(BytecodeValue a, BytecodeValue b)
        {
            return BytecodeValue.Bool(IsFloatOp(a, b) ? a.ToF() >= b.ToF() : a.ToI() >= b.ToI());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.lt comparison opcode evaluation.
        public static BytecodeValue Lt(BytecodeValue a, BytecodeValue b)
        {
            return BytecodeValue.Bool(IsFloatOp(a, b) ? a.ToF() < b.ToF() : a.ToI() < b.ToI());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.le comparison opcode evaluation.
        public static BytecodeValue Le(BytecodeValue a, BytecodeValue b)
        {
            return BytecodeValue.Bool(IsFloatOp(a, b) ? a.ToF() <= b.ToF() : a.ToI() <= b.ToI());
        }

        // ── 位运算 / 逻辑 ──
        // Ikemen reference: src/bytecode.go BytecodeExp.and bitwise opcode evaluation.
        public static BytecodeValue And(BytecodeValue a, BytecodeValue b) => BytecodeValue.Int(a.ToI() & b.ToI());
        // Ikemen reference: src/bytecode.go BytecodeExp.or bitwise opcode evaluation.
        public static BytecodeValue Or(BytecodeValue a, BytecodeValue b) => BytecodeValue.Int(a.ToI() | b.ToI());
        // Ikemen reference: src/bytecode.go BytecodeExp.xor bitwise opcode evaluation.
        public static BytecodeValue Xor(BytecodeValue a, BytecodeValue b) => BytecodeValue.Int(a.ToI() ^ b.ToI());
        // Ikemen reference: src/bytecode.go BytecodeExp.bland boolean opcode evaluation.
        public static BytecodeValue BlAnd(BytecodeValue a, BytecodeValue b) => BytecodeValue.Bool(a.ToB() && b.ToB());
        // Ikemen reference: src/bytecode.go BytecodeExp.blor boolean opcode evaluation.
        public static BytecodeValue BlOr(BytecodeValue a, BytecodeValue b) => BytecodeValue.Bool(a.ToB() || b.ToB());
        // Ikemen reference: src/bytecode.go BytecodeExp.blxor boolean opcode evaluation.
        public static BytecodeValue BlXor(BytecodeValue a, BytecodeValue b) => BytecodeValue.Bool(a.ToB() != b.ToB());

        // ── 区间检查 [] [) (] () ──（Ikemen 此处尊重 undefined）
        // Ikemen reference: src/bytecode.go BytecodeExp.range opcode evaluation for []/[)/(]/() comparisons.
        public static BytecodeValue Range(BytecodeValue v, BytecodeValue min, BytecodeValue max, bool incMin, bool incMax)
        {
            if (v.IsUndefined() || min.IsUndefined() || max.IsUndefined())
            {
                return BytecodeValue.Undefined();
            }
            bool minPass, maxPass;
            bool anyFloat = (int)v.Type == (int)ValueType.Float || (int)min.Type == (int)ValueType.Float || (int)max.Type == (int)ValueType.Float;
            // 与 Ikemen 一致：只要三者最小类型标签为 Float 即按浮点；这里用"任一为 Float"近似（v1/min/max 同源）
            if (anyFloat)
            {
                FFloat vf = v.ToF(), lo = min.ToF(), hi = max.ToF();
                minPass = incMin ? vf >= lo : vf > lo;
                maxPass = incMax ? vf <= hi : vf < hi;
            }
            else
            {
                int vi = v.ToI(), lo = min.ToI(), hi = max.ToI();
                minPass = incMin ? vi >= lo : vi > lo;
                maxPass = incMax ? vi <= hi : vi < hi;
            }
            return BytecodeValue.Bool(minPass && maxPass);
        }

        // ── 超越函数（结果 Float；连续量发散来源，见卡点登记）──
        // Ikemen reference: src/bytecode.go BytecodeExp.exp function opcode evaluation.
        public static BytecodeValue Exp(BytecodeValue v) => BytecodeValue.Float(FMath.Exp(v.ToF()));
        // Ikemen reference: src/bytecode.go BytecodeExp.cos function opcode evaluation.
        public static BytecodeValue Cos(BytecodeValue v) => v.IsUndefined() ? v : BytecodeValue.Float(FMath.Cos(v.ToF()));
        // Ikemen reference: src/bytecode.go BytecodeExp.sin function opcode evaluation.
        public static BytecodeValue Sin(BytecodeValue v) => v.IsUndefined() ? v : BytecodeValue.Float(FMath.Sin(v.ToF()));
        // Ikemen reference: src/bytecode.go BytecodeExp.tan function opcode evaluation.
        public static BytecodeValue Tan(BytecodeValue v) => v.IsUndefined() ? v : BytecodeValue.Float(FMath.Tan(v.ToF()));
        // Ikemen reference: src/bytecode.go BytecodeExp.acos function opcode evaluation.
        public static BytecodeValue Acos(BytecodeValue v) => v.IsUndefined() ? v : BytecodeValue.Float(FMath.Acos(v.ToF()));
        // Ikemen reference: src/bytecode.go BytecodeExp.asin function opcode evaluation.
        public static BytecodeValue Asin(BytecodeValue v) => v.IsUndefined() ? v : BytecodeValue.Float(FMath.Asin(v.ToF()));
        // Ikemen reference: src/bytecode.go BytecodeExp.atan function opcode evaluation.
        public static BytecodeValue Atan(BytecodeValue v) => v.IsUndefined() ? v : BytecodeValue.Float(FMath.Atan(v.ToF()));

        // Ikemen reference: src/bytecode.go BytecodeExp.ln function opcode evaluation.
        public static BytecodeValue Ln(BytecodeValue v)
        {
            return v.ToF().Raw <= 0 ? BytecodeValue.Undefined() : BytecodeValue.Float(FMath.Ln(v.ToF()));
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.log function opcode evaluation.
        public static BytecodeValue Log(BytecodeValue baseV, BytecodeValue x)
        {
            if (baseV.ToF().Raw <= 0 || x.ToF().Raw <= 0)
            {
                return BytecodeValue.Undefined();
            }
            return BytecodeValue.Float(FMath.Ln(x.ToF()) / FMath.Ln(baseV.ToF()));
        }

        // ── 取整（只作用于 Float，结果 Int；非 Float 原样返回，同 Ikemen）──
        // Ikemen reference: src/bytecode.go BytecodeExp.floor function opcode evaluation.
        public static BytecodeValue Floor(BytecodeValue v)
        {
            if (v.IsUndefined() || v.Type != ValueType.Float)
            {
                return v;
            }
            return BytecodeValue.Int(FMath.Floor(v.ToF()).ToInt());
        }

        // Ikemen reference: src/bytecode.go BytecodeExp.ceil function opcode evaluation.
        public static BytecodeValue Ceil(BytecodeValue v)
        {
            if (v.IsUndefined() || v.Type != ValueType.Float)
            {
                return v;
            }
            return BytecodeValue.Int(FMath.Ceil(v.ToF()).ToInt());
        }
    }
}
