// 角色装配器：把已读入的 CNS/CMD/AIR 文本组装成 MCharData + 初始 MChar。
// 逻辑层不做文件 IO（读文件由表现层/测试负责），仅消费文本，保持纯净可单测。
// 复用既有解析器：MugenCnsParser(状态)/MugenConstParser(常量)/MugenCmdParser(命令)/AirParser+MAnimImport(动画)。
// See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Import.Air;
using GameData = Lockstep.Game.Data;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.Battle
{
    /// <summary>从角色文件文本构建 MCharData / MChar。</summary>
    public static class MCharLoader
    {
        /// <summary>
        /// 组装角色配置。各文本可为 null（缺省跳过）。stateTexts 是 .def 里 st/st1… 各状态文件内容，
        /// 后者覆盖前者；cnsText 额外作为常量来源；commonText 为公共状态；airText→动画；cmdText→命令。
        /// </summary>
        public static MCharData Load(IReadOnlyList<string> stateTexts, string cnsText,
            string commonText, string airText, string cmdText, string name = "",
            MCharacterDefinition definition = null)
        {
            MCharData data = new MCharData
            {
                Name = name,
                Definition = definition ?? new MCharacterDefinition { Name = name, DisplayName = name },
            };

            if (stateTexts != null)
            {
                for (int i = 0; i < stateTexts.Count; i++)
                {
                    MergeStates(data.States, MugenCnsParser.Parse(stateTexts[i], data.Compatibility));
                }
            }
            // cns 文件本身常也含状态（KFM st=cns），并入；同时作为常量来源。
            if (cnsText != null)
            {
                MergeStates(data.States, MugenCnsParser.Parse(cnsText, data.Compatibility));
                data.Constants = MugenConstParser.Parse(cnsText);
            }
            if (commonText != null)
            {
                MergeStates(data.CommonStates, MugenCnsParser.Parse(commonText, data.Compatibility));
            }
            if (airText != null)
            {
                List<GameData.AnimData> anims = AirParser.Parse(airText);
                data.Anims = MAnimImport.FromAirTable(anims);
            }
            if (cmdText != null)
            {
                data.Commands = MugenCmdParser.Parse(cmdText);
                // .cmd 不仅含 [Command] 定义，还含 [Statedef -1] 命令解释器（一堆 ChangeState 按
                // command=... 触发出招）。它必须并入 States 才能每帧跑（对齐 MUGEN：.cmd 的状态块与
                // .cns 状态同属角色状态表）。否则命令能匹配但无人消费 → 角色完全不出招。
                MergeStates(data.States, MugenCnsParser.Parse(cmdText, data.Compatibility));
            }
            return data;
        }

        /// <summary>建一个挂好配置的运行期 MChar（命令表/常量接好，进入指定初始状态/动画）。</summary>
        public static MChar SpawnChar(MCharData data, int id, int startStateNo = 0, int startAnimNo = 0)
        {
            MChar c = new MChar
            {
                Id = id,
                Name = data.Name,
                Life = data.Constants.Life,
                LifeMax = data.Constants.Life,
                Power = 0,
                PowerMax = data.Constants.Power,
                Constants = data.Constants,
                AnimTable = data.Anims,   // 动画存在性守卫 + animexist trigger 用（须在 RunInit 前接好）
                OwnData = data,
                StateNo = startStateNo,
                AnimNo = startAnimNo,
                CommandList = new MCommandList { Commands = data.Commands },
            };
            ApplyInitialStateHeader(c, data, startStateNo);   // 模拟 MUGEN 入场 changeState：应用初始状态头部
            return c;
        }

        // 应用初始状态的 [Statedef] 头部（type/movetype/physics/ctrl/anim），对齐 ApplyTransition。
        // 否则直接置 StateNo 不经转换，physics=S 等头部不生效，摩擦/类型 trigger 会失常。
        static void ApplyInitialStateHeader(MChar c, MCharData data, int stateNo)
        {
            if (!data.States.TryGetValue(stateNo, out MStateDef def) &&
                !data.CommonStates.TryGetValue(stateNo, out def))
            {
                return;
            }
            def.RunInit(c);   // 头部：type/movetype/physics + anim/ctrl/velset 表达式求值（对齐 ApplyTransition）
        }

        static void MergeStates(Dictionary<int, MStateDef> into, Dictionary<int, MStateDef> from)
        {
            foreach (KeyValuePair<int, MStateDef> kv in from)
            {
                into[kv.Key] = kv.Value;   // 后载覆盖先载（对齐 MUGEN st1 覆盖 st）
            }
        }
    }
}
