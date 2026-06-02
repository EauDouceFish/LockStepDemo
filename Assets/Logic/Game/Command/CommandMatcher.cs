using Lockstep.Game.Data;
using Lockstep.Input;

namespace Lockstep.Game.Command
{
    /// <summary>
    /// 搓招匹配（纯逻辑，无状态、无 Unity、无浮点）。两件事：
    /// 1) 把一帧的 (moveX,moveY,朝向) 转成"朝向相对方向" InputSymbol（0-8）。
    /// 2) 在最近一段输入历史里，判断某条 CommandData 的 Motion 序列是否本帧达成。
    ///
    /// 匹配语义（v1 基线，对齐 MUGEN 直觉，留容差细化空间）：
    /// - 最后一个符号必须由"当前帧"（历史最新帧）满足 → 指令在完成那帧边沿触发。
    /// - 其余符号按顺序在"当前帧之前、TimeWindow 帧之内"的历史里贪心匹配（允许之间夹无关帧）。
    /// - 方向符号要求精确相等；按钮符号要求该帧按下对应键。
    /// 约定：moveX +1=世界右/-1=左；moveY +1=上/-1=下；facingPositive=true 表示面向右。
    /// </summary>
    public static class CommandMatcher
    {
        public static byte Direction(int moveX, int moveY, bool facingPositive)
        {
            int horizontal = 0;                       // -1 后 / 0 / +1 前
            if (moveX > 0)
            {
                horizontal = facingPositive ? 1 : -1;
            }
            else if (moveX < 0)
            {
                horizontal = facingPositive ? -1 : 1;
            }

            int vertical = moveY > 0 ? 1 : (moveY < 0 ? -1 : 0);

            if (vertical > 0)
            {
                if (horizontal < 0) { return (byte)InputSymbol.UpBack; }
                if (horizontal > 0) { return (byte)InputSymbol.UpFwd; }
                return (byte)InputSymbol.Up;
            }
            if (vertical < 0)
            {
                if (horizontal < 0) { return (byte)InputSymbol.DownBack; }
                if (horizontal > 0) { return (byte)InputSymbol.DownFwd; }
                return (byte)InputSymbol.Down;
            }
            if (horizontal < 0) { return (byte)InputSymbol.Back; }
            if (horizontal > 0) { return (byte)InputSymbol.Fwd; }
            return (byte)InputSymbol.Neutral;
        }

        /// <summary>按钮符号 → InputButton bitmask（v1：a=轻拳 b=重拳 c=踢；x/y/z 暂无映射）。</summary>
        public static byte ButtonMask(InputSymbol symbol)
        {
            switch (symbol)
            {
                case InputSymbol.BtnA: return (byte)InputButton.LightPunch;
                case InputSymbol.BtnB: return (byte)InputButton.HeavyPunch;
                case InputSymbol.BtnC: return (byte)InputButton.Kick;
                default: return 0;
            }
        }

        static bool IsButton(InputSymbol symbol)
        {
            return symbol >= InputSymbol.BtnA;
        }

        static bool Satisfied(InputSymbol symbol, byte frameDir, byte frameBtn)
        {
            if (IsButton(symbol))
            {
                byte mask = ButtonMask(symbol);
                return mask != 0 && (frameBtn & mask) != 0;
            }
            return frameDir == (byte)symbol;
        }

        /// <summary>
        /// recentDir/recentBtn 为按时间从旧到新的最近 count 帧（count = min(TimeWindow, 历史容量)）；
        /// 最新帧在下标 count-1。返回该 Motion 是否本帧达成。
        /// </summary>
        public static bool Matches(CommandData command, byte[] recentDir, byte[] recentBtn, int count)
        {
            InputSymbol[] motion = command.Motion;
            if (motion == null || motion.Length == 0 || count <= 0)
            {
                return false;
            }

            int newest = count - 1;
            // 末符号必须由当前帧满足（边沿触发）
            if (!Satisfied(motion[motion.Length - 1], recentDir[newest], recentBtn[newest]))
            {
                return false;
            }
            if (motion.Length == 1)
            {
                return true;
            }

            // 其余符号在 [0, newest) 内按顺序贪心匹配
            int matchIndex = 0;
            int lastNeeded = motion.Length - 1;
            for (int frame = 0; frame < newest && matchIndex < lastNeeded; frame++)
            {
                if (Satisfied(motion[matchIndex], recentDir[frame], recentBtn[frame]))
                {
                    matchIndex++;
                }
            }
            return matchIndex == lastNeeded;
        }
    }
}
