using System;
using Lockstep.Core;
using Lockstep.Math;

namespace Lockstep.Game.Components
{
    /// <summary>
    /// MUGEN 角色变量：var(0..59) int、fvar(0..39) 定点、sysvar/sysfvar(0..4)。
    /// 大量角色状态依赖这些变量，砍掉会无声崩坏（见架构设计 §12）。
    /// 数组字段必须深拷（铁律 3），否则快照被串。
    /// </summary>
    public sealed class VarsC : IComponent
    {
        public const int IntCount = 60;
        public const int FloatCount = 40;
        public const int SysIntCount = 5;
        public const int SysFloatCount = 5;

        public int[] Var = new int[IntCount];
        public FFloat[] FVar = new FFloat[FloatCount];
        public int[] SysVar = new int[SysIntCount];
        public FFloat[] SysFVar = new FFloat[SysFloatCount];

        public IComponent Clone()
        {
            VarsC clone = new VarsC();
            Array.Copy(Var, clone.Var, IntCount);
            Array.Copy(FVar, clone.FVar, FloatCount);
            Array.Copy(SysVar, clone.SysVar, SysIntCount);
            Array.Copy(SysFVar, clone.SysFVar, SysFloatCount);
            return clone;
        }

        public void WriteHash(ref Hash64 hash)
        {
            for (int i = 0; i < IntCount; i++)
            {
                hash.AddInt32(Var[i]);
            }
            for (int i = 0; i < FloatCount; i++)
            {
                hash.AddFixed(FVar[i]);
            }
            for (int i = 0; i < SysIntCount; i++)
            {
                hash.AddInt32(SysVar[i]);
            }
            for (int i = 0; i < SysFloatCount; i++)
            {
                hash.AddFixed(SysFVar[i]);
            }
        }
    }
}
