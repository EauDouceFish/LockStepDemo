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
        // Ikemen reference: src/char.go player data setup loads CNS/common/CMD/AIR resources into a Char-owned state and animation model.
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
                EnsureStandardCommonFallbackStates(data);
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
        // Ikemen reference: src/char.go Char initialization seeds life/power/constants/state/action tables before round logic.
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
        // Ikemen reference: src/char.go selfState/changeState applies StateDef header fields when entering a state.
        static void ApplyInitialStateHeader(MChar c, MCharData data, int stateNo)
        {
            if (!data.States.TryGetValue(stateNo, out MStateDef def) &&
                !data.CommonStates.TryGetValue(stateNo, out def))
            {
                return;
            }
            def.RunInit(c);   // 头部：type/movetype/physics + anim/ctrl/velset 表达式求值（对齐 ApplyTransition）
        }

        // Ikemen reference: src/compiler.go state loading lets later CNS/ST/CMD definitions replace earlier state numbers.
        static void MergeStates(Dictionary<int, MStateDef> into, Dictionary<int, MStateDef> from)
        {
            foreach (KeyValuePair<int, MStateDef> kv in from)
            {
                into[kv.Key] = kv.Value;   // 后载覆盖先载（对齐 MUGEN st1 覆盖 st）
            }
        }

        static readonly int[] StandardCommonFallbackStateNos = { 5, 100, 105, 106 };

        // Some bundled characters point at the engine standard common1.cns, but the only
        // available CNS fallback in this project is a character-custom common file that
        // renumbers run states. Fill only missing standard states and preserve loaded data.
        static void EnsureStandardCommonFallbackStates(MCharData data)
        {
            bool missing = false;
            for (int i = 0; i < StandardCommonFallbackStateNos.Length; i++)
            {
                if (!data.CommonStates.ContainsKey(StandardCommonFallbackStateNos[i]))
                {
                    missing = true;
                    break;
                }
            }
            if (!missing)
            {
                return;
            }

            Dictionary<int, MStateDef> fallback = MugenCnsParser.Parse(StandardCommonFallbackCns, data.Compatibility);
            foreach (KeyValuePair<int, MStateDef> kv in fallback)
            {
                if (!data.CommonStates.ContainsKey(kv.Key))
                {
                    data.CommonStates[kv.Key] = kv.Value;
                }
            }
        }

        const string StandardCommonFallbackCns =
            "[Statedef 5]\n" +
            "type = S\n" +
            "physics = S\n" +
            "anim = 5\n" +
            "ctrl = 0\n\n" +
            "[State 5, turn]\n" +
            "type = Turn\n" +
            "trigger1 = Time = 0\n\n" +
            "[State 5, done]\n" +
            "type = ChangeState\n" +
            "trigger1 = AnimTime = 0\n" +
            "value = 0\n" +
            "ctrl = 1\n\n" +

            "[Statedef 100]\n" +
            "type = S\n" +
            "movetype = I\n" +
            "physics = S\n" +
            "anim = 100\n" +
            "ctrl = 0\n" +
            "sprpriority = 1\n\n" +
            "[State 100, run velocity]\n" +
            "type = VelSet\n" +
            "trigger1 = 1\n" +
            "x = const(velocity.run.fwd.x)\n" +
            "y = const(velocity.run.fwd.y)\n\n" +
            "[State 100, run jump]\n" +
            "type = ChangeState\n" +
            "trigger1 = command = \"holdup\"\n" +
            "value = 40\n\n" +
            "[State 100, stop]\n" +
            "type = ChangeState\n" +
            "trigger1 = command != \"holdfwd\"\n" +
            "value = 0\n" +
            "ctrl = 1\n\n" +

            "[Statedef 105]\n" +
            "type = A\n" +
            "movetype = I\n" +
            "physics = A\n" +
            "anim = 105\n" +
            "ctrl = 0\n" +
            "velset = const(velocity.run.back.x), const(velocity.run.back.y)\n\n" +
            "[State 105, land]\n" +
            "type = ChangeState\n" +
            "trigger1 = Vel Y > 0\n" +
            "trigger1 = Pos Y >= 0\n" +
            "value = 106\n\n" +

            "[Statedef 106]\n" +
            "type = S\n" +
            "movetype = I\n" +
            "physics = S\n" +
            "anim = 47\n" +
            "ctrl = 0\n\n" +
            "[State 106, stop]\n" +
            "type = VelSet\n" +
            "trigger1 = Time = 0\n" +
            "x = 0\n" +
            "y = 0\n\n" +
            "[State 106, ground]\n" +
            "type = PosSet\n" +
            "trigger1 = Time = 0\n" +
            "y = 0\n\n" +
            "[State 106, done]\n" +
            "type = ChangeState\n" +
            "trigger1 = AnimTime = 0\n" +
            "value = 0\n" +
            "ctrl = 1\n";
    }
}
