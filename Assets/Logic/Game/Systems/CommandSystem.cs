using Lockstep.Core;
using Lockstep.Game.Command;
using Lockstep.Game.Components;
using Lockstep.Game.Data;
using Lockstep.Input;

namespace Lockstep.Game.Systems
{
    /// <summary>
    /// 每帧把各实体输入(方向按朝向转相对 + 按钮)推入 CommandInputC，再对其角色的每条 CommandData
    /// 做序列匹配，结果写 CommandStateC.Active[i]（StateMachine 经 command="..." trigger 读）。
    /// 须在 StateMachineSystem 之前跑。无静态可变状态；scratch 缓冲每次用前全覆盖。
    /// </summary>
    public sealed class CommandSystem : ISystem
    {
        readonly byte[] _scratchDir = new byte[CommandInputC.Capacity];
        readonly byte[] _scratchBtn = new byte[CommandInputC.Capacity];

        public void Tick(World world)
        {
            if (world.CurrentInputs == null)
            {
                return;
            }
            MugenGameData data = world.GameData as MugenGameData;
            if (data == null)
            {
                return;
            }

            for (int i = 0; i < world.Entities.Count; i++)
            {
                Entity entity = world.Entities[i];
                PlayerTagC tag = entity.Get<PlayerTagC>();
                CommandInputC history = entity.Get<CommandInputC>();
                CommandStateC commandState = entity.Get<CommandStateC>();
                CharacterRefC characterRef = entity.Get<CharacterRefC>();
                if (tag == null || history == null || commandState == null || characterRef == null)
                {
                    continue;
                }
                if (tag.PlayerIndex >= world.CurrentInputs.Length)
                {
                    continue;
                }
                if (!data.Characters.TryGetValue(characterRef.CharacterId, out CharacterDef character))
                {
                    continue;
                }

                TransformC transform = entity.Get<TransformC>();
                bool facingPositive = transform == null || transform.FacingX.Raw >= 0;

                FrameInput input = world.CurrentInputs[tag.PlayerIndex];
                byte direction = CommandMatcher.Direction(input.MoveX, input.MoveY, facingPositive);
                history.Push(direction, input.Buttons);

                MatchCommands(character, history, commandState);
            }
        }

        void MatchCommands(CharacterDef character, CommandInputC history, CommandStateC commandState)
        {
            CommandData[] commands = character.Commands;
            int commandCount = commands == null ? 0 : commands.Length;
            if (commandState.Active.Length != commandCount)
            {
                commandState.Active = new bool[commandCount];
            }
            if (commandCount == 0)
            {
                return;
            }

            for (int commandIndex = 0; commandIndex < commandCount; commandIndex++)
            {
                CommandData command = commands[commandIndex];
                int window = command.TimeWindow > 0 ? command.TimeWindow : CommandInputC.Capacity;
                int count = history.ReadRecent(window, _scratchDir, _scratchBtn);
                commandState.Active[commandIndex] = CommandMatcher.Matches(command, _scratchDir, _scratchBtn, count);
            }
        }
    }
}
