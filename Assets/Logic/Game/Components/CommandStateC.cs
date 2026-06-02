using System;
using Lockstep.Core;

namespace Lockstep.Game.Components
{
    /// <summary>
    /// 本帧命令匹配结果。CommandSystem 写、StateMachine 读（command="..." trigger）。
    /// Active[i] = 角色第 i 条 CommandData 本帧是否成立。长度 = 该角色指令数。
    /// </summary>
    public sealed class CommandStateC : IComponent
    {
        public bool[] Active = Array.Empty<bool>();

        public IComponent Clone()
        {
            CommandStateC clone = new CommandStateC();
            if (Active.Length > 0)
            {
                clone.Active = new bool[Active.Length];
                Array.Copy(Active, clone.Active, Active.Length);
            }
            return clone;
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(Active.Length);
            for (int i = 0; i < Active.Length; i++)
            {
                hash.AddBool(Active[i]);
            }
        }
    }
}
