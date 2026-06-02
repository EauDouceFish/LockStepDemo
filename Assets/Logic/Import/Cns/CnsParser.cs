using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Lockstep.Game.Data;
using Lockstep.Game.Expr;

namespace Lockstep.Import.Cns
{
    /// <summary>CNS 解析结果：状态表 + 降级警告数（编译失败的 trigger/param 个数）。</summary>
    public sealed class CnsParseResult
    {
        public readonly Dictionary<int, StateDef> States = new Dictionary<int, StateDef>();
        public int Warnings;
    }

    /// <summary>
    /// MUGEN .cns → StateDef/StateController 结构化解析器（容错）。
    /// 处理 [Statedef N] 字段(type/movetype/physics/anim/ctrl/velset/poweradd/juggle) 与
    /// [State N,label] 控制器(type + triggerall/triggerN 合成 + 其余 key=value 作 params)。
    /// 触发合成：triggerall && (group1 || group2 ...)，同号多行 AND。
    /// 容错：trigger/param 编译失败则降级（trigger 设为恒假、param 跳过）并计 Warnings，绝不崩。
    /// 注：枚举 token(StateType=S)、字符串 command="..."、p2 重定向等运行期语义为后续工作(T2.3b)。
    /// </summary>
    public static class CnsParser
    {
        enum Mode { None, Statedef, State }

        public static CnsParseResult ParseFile(string path)
        {
            return Parse(File.ReadAllText(path));
        }

        public static CnsParseResult Parse(string text)
        {
            ExpressionVM vm = new ExpressionVM();
            IExpr never = vm.Compile("0");
            CnsParseResult result = new CnsParseResult();

            Mode mode = Mode.None;
            StateDef currentState = null;
            List<StateController> controllers = new List<StateController>();

            // 当前控制器累积
            string controllerType = null;
            List<string> triggerAll = new List<string>();
            SortedDictionary<int, List<string>> triggerGroups = new SortedDictionary<int, List<string>>();
            Dictionary<string, string> controllerParams = new Dictionary<string, string>();

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string rawLine in lines)
            {
                string line = StripComment(rawLine).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '[')
                {
                    int rb = line.IndexOf(']');
                    string header = (rb > 0 ? line.Substring(1, rb - 1) : line.Substring(1)).Trim();
                    string headerLower = header.ToLowerInvariant();

                    FinalizeController(vm, never, result, controllers, ref controllerType,
                        triggerAll, triggerGroups, controllerParams);

                    if (headerLower.StartsWith("statedef"))
                    {
                        FinalizeState(currentState, controllers);
                        controllers = new List<StateController>();
                        currentState = new StateDef { Id = ParseTrailingInt(header), Controllers = null };
                        result.States[currentState.Id] = currentState;
                        mode = Mode.Statedef;
                    }
                    else if (headerLower.StartsWith("state "))
                    {
                        mode = (currentState != null) ? Mode.State : Mode.None;
                    }
                    else
                    {
                        FinalizeState(currentState, controllers);
                        currentState = null;
                        controllers = new List<StateController>();
                        mode = Mode.None;
                    }
                    continue;
                }

                int equalsAt = line.IndexOf('=');
                if (equalsAt < 0)
                {
                    continue;
                }
                string key = line.Substring(0, equalsAt).Trim().ToLowerInvariant();
                string value = line.Substring(equalsAt + 1).Trim();

                if (mode == Mode.Statedef && currentState != null)
                {
                    ApplyStatedefField(currentState, key, value);
                }
                else if (mode == Mode.State)
                {
                    if (key == "type")
                    {
                        controllerType = value;
                    }
                    else if (key == "triggerall")
                    {
                        triggerAll.Add(value);
                    }
                    else if (key.StartsWith("trigger") && key.Length > 7)
                    {
                        if (int.TryParse(key.Substring(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                        {
                            if (!triggerGroups.TryGetValue(index, out List<string> groupLines))
                            {
                                groupLines = new List<string>();
                                triggerGroups[index] = groupLines;
                            }
                            groupLines.Add(value);
                        }
                    }
                    else
                    {
                        controllerParams[key] = value;
                    }
                }
            }

            FinalizeController(vm, never, result, controllers, ref controllerType,
                triggerAll, triggerGroups, controllerParams);
            FinalizeState(currentState, controllers);
            return result;
        }

        // ───────── 控制器/状态收尾 ─────────

        static void FinalizeController(ExpressionVM vm, IExpr never, CnsParseResult result,
            List<StateController> controllers, ref string controllerType,
            List<string> triggerAll, SortedDictionary<int, List<string>> triggerGroups,
            Dictionary<string, string> controllerParams)
        {
            if (controllerType != null)
            {
                StateController controller = new StateController
                {
                    Type = MapControllerType(controllerType),
                    Trigger = ComposeTrigger(vm, never, result, triggerAll, triggerGroups),
                    Params = CompileParams(vm, result, controllerParams),
                };
                controllers.Add(controller);
            }
            controllerType = null;
            triggerAll.Clear();
            triggerGroups.Clear();
            controllerParams.Clear();
        }

        static void FinalizeState(StateDef state, List<StateController> controllers)
        {
            if (state != null)
            {
                state.Controllers = controllers.ToArray();
            }
        }

        static IExpr ComposeTrigger(ExpressionVM vm, IExpr never, CnsParseResult result,
            List<string> triggerAll, SortedDictionary<int, List<string>> triggerGroups)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < triggerAll.Count; i++)
            {
                parts.Add("(" + triggerAll[i] + ")");
            }
            if (triggerGroups.Count > 0)
            {
                List<string> groupStrings = new List<string>();
                foreach (KeyValuePair<int, List<string>> group in triggerGroups)
                {
                    List<string> lineParens = new List<string>();
                    for (int i = 0; i < group.Value.Count; i++)
                    {
                        lineParens.Add("(" + group.Value[i] + ")");
                    }
                    groupStrings.Add("(" + string.Join(" && ", lineParens) + ")");
                }
                parts.Add("(" + string.Join(" || ", groupStrings) + ")");
            }

            if (parts.Count == 0)
            {
                return null; // 无 trigger = 恒真
            }

            string composed = string.Join(" && ", parts);
            try
            {
                return vm.Compile(composed);
            }
            catch (ExprException)
            {
                result.Warnings++;
                return never; // 降级为恒假，控制器不触发但状态仍建立
            }
        }

        static Dictionary<string, IExpr> CompileParams(ExpressionVM vm, CnsParseResult result,
            Dictionary<string, string> rawParams)
        {
            if (rawParams.Count == 0)
            {
                return null;
            }
            Dictionary<string, IExpr> compiled = new Dictionary<string, IExpr>();
            foreach (KeyValuePair<string, string> entry in rawParams)
            {
                try
                {
                    compiled[entry.Key] = vm.Compile(entry.Value);
                }
                catch (ExprException)
                {
                    result.Warnings++;   // 跳过无法编译的 param（如字符串字面量）
                }
            }
            return compiled;
        }

        // ───────── Statedef 字段 ─────────

        static void ApplyStatedefField(StateDef state, string key, string value)
        {
            switch (key)
            {
                case "type":
                    state.StateType = ParseStateType(value);
                    break;
                case "movetype":
                    state.MoveType = ParseMoveType(value);
                    break;
                case "physics":
                    state.Physics = ParsePhysics(value);
                    break;
                case "anim":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int anim))
                    {
                        state.Anim = anim;
                    }
                    break;
                case "ctrl":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int ctrl))
                    {
                        state.Ctrl = ctrl != 0;
                    }
                    break;
                case "poweradd":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int powerAdd))
                    {
                        state.PowerAdd = powerAdd;
                    }
                    break;
                case "juggle":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int juggle))
                    {
                        state.Juggle = juggle;
                    }
                    break;
            }
        }

        // ───────── 枚举/工具 ─────────

        static ControllerType MapControllerType(string raw)
        {
            switch (raw.Trim().ToLowerInvariant())
            {
                case "null": return ControllerType.Null;
                case "changestate": return ControllerType.ChangeState;
                case "selfstate": return ControllerType.SelfState;
                case "changeanim": return ControllerType.ChangeAnim;
                case "velset": return ControllerType.VelSet;
                case "veladd": return ControllerType.VelAdd;
                case "posset": return ControllerType.PosSet;
                case "posadd": return ControllerType.PosAdd;
                case "ctrlset": return ControllerType.CtrlSet;
                case "statetypeset": return ControllerType.StateTypeSet;
                case "hitdef": return ControllerType.HitDef;
                case "turn": return ControllerType.Turn;
                case "width": return ControllerType.Width;
                case "playsnd": return ControllerType.PlaySnd;
                case "varset": return ControllerType.VarSet;
                case "varadd": return ControllerType.VarAdd;
                case "assertspecial": return ControllerType.AssertSpecial;
                case "pause": return ControllerType.Pause;
                default: return ControllerType.NotImplemented;
            }
        }

        static StateType ParseStateType(string value)
        {
            switch (value.Trim().ToUpperInvariant())
            {
                case "S": return StateType.Stand;
                case "C": return StateType.Crouch;
                case "A": return StateType.Air;
                case "L": return StateType.LieDown;
                default: return StateType.Unchanged;
            }
        }

        static MoveType ParseMoveType(string value)
        {
            switch (value.Trim().ToUpperInvariant())
            {
                case "I": return MoveType.Idle;
                case "A": return MoveType.Attack;
                case "H": return MoveType.BeingHit;
                default: return MoveType.Unchanged;
            }
        }

        static Physics ParsePhysics(string value)
        {
            switch (value.Trim().ToUpperInvariant())
            {
                case "S": return Physics.Stand;
                case "C": return Physics.Crouch;
                case "A": return Physics.Air;
                case "N": return Physics.None;
                default: return Physics.Unchanged;
            }
        }

        static string StripComment(string line)
        {
            int semi = line.IndexOf(';');
            return semi >= 0 ? line.Substring(0, semi) : line;
        }

        static int ParseTrailingInt(string header)
        {
            string[] tokens = header.Split(' ');
            string last = tokens[tokens.Length - 1];
            return int.TryParse(last, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }
    }
}
