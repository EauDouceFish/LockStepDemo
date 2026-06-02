using UnityEngine;
using Lockstep.Input;

namespace Lockstep.Client
{
    /// <summary>
    /// 本地键盘输入采样。默认绑定：
    ///   方向：WASD
    ///   轻拳 J / 重拳 K / 踢 L / 跳跃 空格
    /// 第二玩家在 Bootstrap 里可重置为方向键 + UIO + RShift。
    /// </summary>
    public sealed class LocalInputProvider : IInputProvider
    {
        public KeyCode Up         = KeyCode.W;
        public KeyCode Down       = KeyCode.S;
        public KeyCode Left       = KeyCode.A;
        public KeyCode Right      = KeyCode.D;
        public KeyCode LightPunch = KeyCode.J;
        public KeyCode HeavyPunch = KeyCode.K;
        public KeyCode Kick       = KeyCode.L;
        public KeyCode Jump       = KeyCode.Space;

        public FrameInput Sample()
        {
            sbyte moveX = 0;
            sbyte moveY = 0;
            if (UnityEngine.Input.GetKey(Left))
            {
                moveX = -1;
            }
            else if (UnityEngine.Input.GetKey(Right))
            {
                moveX = 1;
            }
            if (UnityEngine.Input.GetKey(Down))
            {
                moveY = -1;
            }
            else if (UnityEngine.Input.GetKey(Up))
            {
                moveY = 1;
            }

            byte buttons = 0;
            if (UnityEngine.Input.GetKey(LightPunch))
            {
                buttons |= (byte)InputButton.LightPunch;
            }
            if (UnityEngine.Input.GetKey(HeavyPunch))
            {
                buttons |= (byte)InputButton.HeavyPunch;
            }
            if (UnityEngine.Input.GetKey(Kick))
            {
                buttons |= (byte)InputButton.Kick;
            }
            if (UnityEngine.Input.GetKey(Jump))
            {
                buttons |= (byte)InputButton.Jump;
            }

            return new FrameInput
            {
                MoveX = moveX,
                MoveY = moveY,
                Buttons = buttons,
            };
        }
    }
}
