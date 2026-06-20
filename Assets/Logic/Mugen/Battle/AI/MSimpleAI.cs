// 简单确定性 AI：控制一名角色对抗玩家（vs AI 模式）。纯逻辑、定点、无 System.Random
// （按帧节奏决策→可回放/对账确定）。行为：走向对手→进入攻击距离后按固定节奏出拳脚（A/B/C 轮替），
// 偶尔起跳。够"会打"用于 demo；不追求高级博弈。
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Mugen.Battle.AI
{
    /// <summary>简单确定性 AI 输入生成器（vs AI）。每帧据双方位置 + 帧节奏产出 <see cref="MInput"/>。</summary>
    public sealed class MSimpleAI
    {
        // 调参（MUGEN 单位 / 帧）。
        public int ApproachRange = 45;   // 进入此距离内开始攻击
        public int AttackPeriod = 24;    // 攻击节奏周期
        public int AttackHold = 4;       // 周期内按住攻击键的帧数
        public int JumpPeriod = 150;     // 偶尔起跳的周期
        public int Aggression = 1;       // 1=标准；0=偏防守（只走位少攻击）

        readonly int _seed;

        // Project-specific: deterministic demo AI input generator; Ikemen AI is authored through commands/CNS rather than a fixed C# policy.
        public MSimpleAI(int seed = 0)
        {
            _seed = seed;
        }

        /// <summary>产出本帧 AI 输入。self/opponent 为空或失控时返回 None。</summary>
        // Project-specific: emits synthetic MInput for local vs-AI demos; Ikemen equivalent player input flows through src/input.go CommandList.
        public MInput Decide(MChar self, MChar opponent, int frame)
        {
            if (self == null || opponent == null)
            {
                return MInput.None;
            }
            // 失控（受击/出招中/回合非战斗）时不强行输入（让硬编码/受击机接管）。
            if (!self.Ctrl)
            {
                return MInput.None;
            }

            MInput input = MInput.None;
            int dx = (opponent.Pos.X - self.Pos.X).ToInt();
            int adx = dx < 0 ? -dx : dx;

            if (adx > ApproachRange)
            {
                // 走向对手（绝对屏幕方向）。
                input |= dx > 0 ? MInput.Right : MInput.Left;
            }
            else if (Aggression > 0)
            {
                // 进入攻击距离：按节奏出招（A/B/C 轮替）。
                int phase = Phase(frame, AttackPeriod);
                if (phase < AttackHold)
                {
                    input |= AttackButton(frame);
                    // 攻击时也朝向对手保持压制。
                    input |= dx >= 0 ? MInput.Right : MInput.Left;
                }
            }

            // 偶尔起跳（混淆节奏）。
            if (JumpPeriod > 0 && Phase(frame, JumpPeriod) == 0)
            {
                input |= MInput.Up;
            }

            return input;
        }

        // Project-specific: deterministic AI rhythm helper; no Ikemen runtime counterpart beyond frame-time command evaluation.
        int Phase(int frame, int period)
        {
            if (period <= 0)
            {
                return 0;
            }
            int v = (frame + _seed) % period;
            return v < 0 ? v + period : v;
        }

        // Project-specific: demo-only attack-button rotation; Ikemen maps named commands/buttons through src/input.go CommandList.
        static MInput AttackButton(int frame)
        {
            switch ((frame / 8) % 3)
            {
                case 0: return MInput.A;
                case 1: return MInput.B;
                default: return MInput.C;
            }
        }
    }
}
