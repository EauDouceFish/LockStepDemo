// 加载完成的角色配置（不可变）：状态表/公共状态/动画表/命令定义/常量。
// 由 MCharLoader 从 CNS/CMD/AIR 文本构建；运行期被 MChar 引用（不进快照）。See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.Battle
{
    /// <summary>一个角色的全部静态配置（加载后只读）。</summary>
    public sealed class MCharData
    {
        public string Name = "";
        public Dictionary<int, MStateDef> States = new Dictionary<int, MStateDef>();         // 角色自身状态（含招式）
        public Dictionary<int, MStateDef> CommonStates = new Dictionary<int, MStateDef>();   // common1.cns 公共状态
        public Dictionary<int, MAnimData> Anims = new Dictionary<int, MAnimData>();           // 动画号→动画
        public List<MCommandDef> Commands = new List<MCommandDef>();                          // 命令定义（搓招）
        public MConstants Constants = new MConstants();                                       // [Data]/[Size]/[Velocity]/[Movement]
    }
}
