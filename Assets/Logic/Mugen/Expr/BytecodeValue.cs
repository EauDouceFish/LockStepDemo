// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go  (ValueType, BytecodeValue + factories)
// Adapted to fixed-point (FFloat) for deterministic lockstep/rollback. See Docs/移植方案_Ikemen.md.
using Lockstep.Math;

namespace Lockstep.Mugen.Expr
{
    /// <summary>表达式值的类型标签（对应 Ikemen ValueType）。</summary>
    public enum ValueType
    {
        None = 0,
        Float,
        Int,
        Bool,
        Undefined,
    }

    /// <summary>
    /// 表达式求值的标记联合值（对应 Ikemen BytecodeValue）。
    /// Ikemen 用单个 float64 + NaN 表 undefined；定点无 NaN，故：
    /// - 偏离点（见移植方案 §2.2）：int/bool 存 long <see cref="_ival"/>，float 存 FFloat <see cref="_fval"/>，
    ///   各类型只用对应字段，转换时按 vtype 取源，避免双字段不一致。
    /// - undefined 由 vtype 显式表示（无 NaN）。
    /// ToI 按 Ikemen 语义 **向零截断**（FFloat.ToInt 是 floor，负小数会差 1，故此处自行截断）。
    /// </summary>
    public readonly struct BytecodeValue
    {
        const int FractionalBits = 32;                       // Fix64 Q31.32

        readonly ValueType _vtype;
        readonly long _ival;
        readonly FFloat _fval;

        BytecodeValue(ValueType vtype, long ival, FFloat fval)
        {
            _vtype = vtype;
            _ival = ival;
            _fval = fval;
        }

        public ValueType Type => _vtype;

        public bool IsNone()
        {
            return _vtype == ValueType.None;
        }

        public bool IsUndefined()
        {
            return _vtype == ValueType.Undefined;
        }

        /// <summary>取定点浮点值。undefined→0；int/bool→提升为 FFloat。</summary>
        public FFloat ToF()
        {
            if (_vtype == ValueType.Undefined)
            {
                return FFloat.Zero;
            }
            if (_vtype == ValueType.Float)
            {
                return _fval;
            }
            return FFloat.FromInt((int)_ival);
        }

        /// <summary>取 int32，向零截断（对齐 Ikemen int32(float)）。undefined→0。</summary>
        public int ToI()
        {
            return (int)ToI64();
        }

        public long ToI64()
        {
            if (_vtype == ValueType.Undefined)
            {
                return 0;
            }
            if (_vtype != ValueType.Float)
            {
                return _ival;
            }
            return TruncToLong(_fval);
        }

        /// <summary>布尔：undefined 或 值为 0 → false。</summary>
        public bool ToB()
        {
            if (_vtype == ValueType.Undefined)
            {
                return false;
            }
            if (_vtype == ValueType.Float)
            {
                return _fval.Raw != 0;
            }
            return _ival != 0;
        }

        // FFloat.ToInt() 是 floor；这里实现向零截断以对齐 Ikemen 的 int32(float)。
        static long TruncToLong(FFloat value)
        {
            long raw = value.Raw;
            long floor = raw >> FractionalBits;                 // 算术右移 = 向下取整
            long fractionMask = (1L << FractionalBits) - 1;
            if (raw < 0 && (raw & fractionMask) != 0)
            {
                floor += 1;                                     // 负数且有小数 → 朝零回拨一位
            }
            return floor;
        }

        // ───────── 工厂（对应 Ikemen Bytecode* 构造）─────────

        public static BytecodeValue None()
        {
            return new BytecodeValue(ValueType.None, 0, FFloat.Zero);
        }

        public static BytecodeValue Undefined()
        {
            return new BytecodeValue(ValueType.Undefined, 0, FFloat.Zero);
        }

        public static BytecodeValue Float(FFloat f)
        {
            return new BytecodeValue(ValueType.Float, 0, f);
        }

        public static BytecodeValue Int(int i)
        {
            return new BytecodeValue(ValueType.Int, i, FFloat.Zero);
        }

        public static BytecodeValue Int64(long i)
        {
            return new BytecodeValue(ValueType.Int, i, FFloat.Zero);
        }

        public static BytecodeValue Bool(bool b)
        {
            return new BytecodeValue(ValueType.Bool, b ? 1 : 0, FFloat.Zero);
        }

        public override string ToString()
        {
            switch (_vtype)
            {
                case ValueType.Float: return "F:" + _fval.Raw;
                case ValueType.Int: return "I:" + _ival;
                case ValueType.Bool: return "B:" + (_ival != 0);
                case ValueType.Undefined: return "undef";
                default: return "none";
            }
        }
    }
}
