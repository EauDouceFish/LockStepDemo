using Lockstep.Core;
using Lockstep.Game.Components;
using Lockstep.Input;

namespace Lockstep.Game.Systems
{
    /// <summary>
    /// 每帧把当前 FrameInput.Buttons 推入对应实体的 InputBufferC。
    /// 必须在 StateMachineSystem 之前跑（状态机读 buffer 做 cancel 判定）。
    /// </summary>
    public sealed class InputBufferSystem : ISystem
    {
        public void Tick(World world)
        {
            if (world.CurrentInputs == null)
            {
                return;
            }
            for (int i = 0; i < world.Entities.Count; i++)
            {
                Entity entity = world.Entities[i];
                PlayerTagC tag = entity.Get<PlayerTagC>();
                InputBufferC buffer = entity.Get<InputBufferC>();
                if (tag == null || buffer == null)
                {
                    continue;
                }
                if (tag.PlayerIndex >= world.CurrentInputs.Length)
                {
                    continue;
                }
                FrameInput input = world.CurrentInputs[tag.PlayerIndex];
                buffer.Buffer[buffer.Cursor] = input.Buttons;
                buffer.Cursor = (byte)((buffer.Cursor + 1) % InputBufferC.Capacity);
            }
        }
    }
}
