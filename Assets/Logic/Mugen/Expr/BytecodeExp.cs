// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go  (BytecodeExp + run loop)
// Adapted to fixed-point. 编码上 OC_float 携带 8 字节 FFloat raw（偏离 Ikemen 的 float32，因定点），
// 由我方编译器(M2)与本执行器约定一致。短路跳转(jz/jnz)M1 未实现（纯表达式两侧求值结果一致），M2 需要时补。
// trigger/redirect 类 opcode 走 IExprContext 钩子（M3 接入），未提供则压 Undefined。
// See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Math;

namespace Lockstep.Mugen.Expr
{
    /// <summary>依赖 Char 的 trigger/redirect 读取钩子（M3 由 Char 上下文实现）。</summary>
    public interface IExprContext
    {
        /// <summary>读取一个 trigger/redirect opcode 的值；不支持则返回 Undefined。stack 供需要弹参的 opcode 使用。</summary>
        BytecodeValue ReadTrigger(OpCode op, byte[] code, ref int i, List<BytecodeValue> stack);
    }

    /// <summary>表达式字节码 + 栈式求值器（对应 Ikemen BytecodeExp.run）。</summary>
    public sealed class BytecodeExp
    {
        readonly byte[] _code;

        public BytecodeExp(byte[] code)
        {
            _code = code;
        }

        public byte[] Code => _code;

        /// <summary>求值，返回栈顶（空栈返回 Undefined）。context 可为 null（trigger 类压 Undefined）。</summary>
        public BytecodeValue Run(IExprContext context)
        {
            List<BytecodeValue> stack = new List<BytecodeValue>(16);
            int i = 0;
            while (i < _code.Length)
            {
                OpCode op = (OpCode)_code[i];
                i++;
                switch (op)
                {
                    case OpCode.OC_int8: stack.Add(BytecodeValue.Int((sbyte)_code[i])); i++; break;
                    case OpCode.OC_int: stack.Add(BytecodeValue.Int(ReadInt32(ref i))); break;
                    case OpCode.OC_int64: stack.Add(BytecodeValue.Int64(ReadInt64(ref i))); break;
                    case OpCode.OC_float: stack.Add(BytecodeValue.Float(FFloat.FromRaw(ReadInt64(ref i)))); break;

                    case OpCode.OC_pop: Pop(stack); break;
                    case OpCode.OC_dup: stack.Add(Peek(stack)); break;
                    case OpCode.OC_swap: Swap(stack); break;

                    // 二元：先弹 b 后弹 a，压 op(a,b)
                    case OpCode.OC_add: Binary(stack, BytecodeOps.Add); break;
                    case OpCode.OC_sub: Binary(stack, BytecodeOps.Sub); break;
                    case OpCode.OC_mul: Binary(stack, BytecodeOps.Mul); break;
                    case OpCode.OC_div: Binary(stack, BytecodeOps.Div); break;
                    case OpCode.OC_mod: Binary(stack, BytecodeOps.Mod); break;
                    case OpCode.OC_pow: Binary(stack, BytecodeOps.Pow); break;
                    case OpCode.OC_eq: Binary(stack, BytecodeOps.Eq); break;
                    case OpCode.OC_ne: Binary(stack, BytecodeOps.Ne); break;
                    case OpCode.OC_gt: Binary(stack, BytecodeOps.Gt); break;
                    case OpCode.OC_ge: Binary(stack, BytecodeOps.Ge); break;
                    case OpCode.OC_lt: Binary(stack, BytecodeOps.Lt); break;
                    case OpCode.OC_le: Binary(stack, BytecodeOps.Le); break;
                    case OpCode.OC_and: Binary(stack, BytecodeOps.And); break;
                    case OpCode.OC_or: Binary(stack, BytecodeOps.Or); break;
                    case OpCode.OC_xor: Binary(stack, BytecodeOps.Xor); break;
                    case OpCode.OC_bland: Binary(stack, BytecodeOps.BlAnd); break;
                    case OpCode.OC_blor: Binary(stack, BytecodeOps.BlOr); break;
                    case OpCode.OC_blxor: Binary(stack, BytecodeOps.BlXor); break;
                    case OpCode.OC_log: Binary(stack, BytecodeOps.Log); break;

                    // 一元
                    case OpCode.OC_neg: Unary(stack, BytecodeOps.Neg); break;
                    case OpCode.OC_not: Unary(stack, BytecodeOps.Not); break;
                    case OpCode.OC_blnot: Unary(stack, BytecodeOps.BlNot); break;
                    case OpCode.OC_abs: Unary(stack, BytecodeOps.Abs); break;
                    case OpCode.OC_exp: Unary(stack, BytecodeOps.Exp); break;
                    case OpCode.OC_ln: Unary(stack, BytecodeOps.Ln); break;
                    case OpCode.OC_cos: Unary(stack, BytecodeOps.Cos); break;
                    case OpCode.OC_sin: Unary(stack, BytecodeOps.Sin); break;
                    case OpCode.OC_tan: Unary(stack, BytecodeOps.Tan); break;
                    case OpCode.OC_acos: Unary(stack, BytecodeOps.Acos); break;
                    case OpCode.OC_asin: Unary(stack, BytecodeOps.Asin); break;
                    case OpCode.OC_atan: Unary(stack, BytecodeOps.Atan); break;
                    case OpCode.OC_floor: Unary(stack, BytecodeOps.Floor); break;
                    case OpCode.OC_ceil: Unary(stack, BytecodeOps.Ceil); break;

                    // 区间：弹 max,min,v
                    case OpCode.OC_range_ii: RangeOp(stack, true, true); break;
                    case OpCode.OC_range_ie: RangeOp(stack, true, false); break;
                    case OpCode.OC_range_ei: RangeOp(stack, false, true); break;
                    case OpCode.OC_range_ee: RangeOp(stack, false, false); break;

                    // 三目：弹 falseVal,trueVal,cond（编译器按 cond,trueVal,falseVal 顺序压栈）
                    case OpCode.OC_ifelse:
                    {
                        BytecodeValue falseVal = Pop(stack);
                        BytecodeValue trueVal = Pop(stack);
                        BytecodeValue cond = Pop(stack);
                        stack.Add(cond.ToB() ? trueVal : falseVal);
                        break;
                    }

                    default:
                        // trigger/redirect/子表 opcode：交给 context（M3）；无则压 Undefined
                        if (context != null)
                        {
                            stack.Add(context.ReadTrigger(op, _code, ref i, stack));
                        }
                        else
                        {
                            stack.Add(BytecodeValue.Undefined());
                        }
                        break;
                }
            }
            return stack.Count > 0 ? stack[stack.Count - 1] : BytecodeValue.Undefined();
        }

        delegate BytecodeValue BinOp(BytecodeValue a, BytecodeValue b);
        delegate BytecodeValue UnOp(BytecodeValue a);

        static void Binary(List<BytecodeValue> stack, BinOp op)
        {
            BytecodeValue b = Pop(stack);
            BytecodeValue a = Pop(stack);
            stack.Add(op(a, b));
        }

        static void Unary(List<BytecodeValue> stack, UnOp op)
        {
            stack.Add(op(Pop(stack)));
        }

        static void RangeOp(List<BytecodeValue> stack, bool incMin, bool incMax)
        {
            BytecodeValue max = Pop(stack);
            BytecodeValue min = Pop(stack);
            BytecodeValue v = Pop(stack);
            stack.Add(BytecodeOps.Range(v, min, max, incMin, incMax));
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

        static BytecodeValue Peek(List<BytecodeValue> stack)
        {
            return stack.Count > 0 ? stack[stack.Count - 1] : BytecodeValue.Undefined();
        }

        static void Swap(List<BytecodeValue> stack)
        {
            if (stack.Count < 2)
            {
                return;
            }
            int top = stack.Count - 1;
            BytecodeValue tmp = stack[top];
            stack[top] = stack[top - 1];
            stack[top - 1] = tmp;
        }

        int ReadInt32(ref int i)
        {
            int v = _code[i] | (_code[i + 1] << 8) | (_code[i + 2] << 16) | (_code[i + 3] << 24);
            i += 4;
            return v;
        }

        long ReadInt64(ref int i)
        {
            long v = 0;
            for (int b = 0; b < 8; b++)
            {
                v |= (long)_code[i + b] << (8 * b);
            }
            i += 8;
            return v;
        }
    }
}
