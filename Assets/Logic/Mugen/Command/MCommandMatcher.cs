// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/input.go (Command.Step 匹配语义)。
// 匹配模型：末步由当前帧边沿触发，其余步在 Time 帧窗内按序贪心匹配(允许夹无关帧)；
// '>' 步要求与上一步严格相邻；'~' 释放边沿；蓄力=连续按住 N 帧；'$' 4way 子集；'/' 按住不要求边沿。
// 方向 B/F 按朝向解析(facing>0 面右：F=Right,B=Left)。See Docs/移植方案_Ikemen.md.
namespace Lockstep.Mugen.Command
{
    public static class MCommandMatcher
    {
        /// <summary>命令是否在当前帧(缓冲最新帧)达成。facingRight=角色是否面向右(+X)。</summary>
        public static bool Matches(MCommandDef cmd, MCommandBuffer buf, bool facingRight)
        {
            int n = cmd.Steps.Count;
            if (n == 0 || buf.Count == 0)
            {
                return false;
            }

            int window = buf.Capacity < buf.Count ? buf.Capacity : buf.Count;
            if (cmd.Time > 0 && cmd.Time < window)
            {
                window = cmd.Time;
            }

            // age：0=最新帧。末步须在 age 0 达成(边沿触发)。向更早(age 增大)依次匹配更早的步。
            int prevAge = -1;
            for (int i = n - 1; i >= 0; i--)
            {
                MCommandStep step = cmd.Steps[i];
                bool isLast = i == n - 1;
                int found = -1;

                // 搜索范围：末步固定 age 0；其余从 prevAge+1 起到窗口末
                int startAge = isLast ? 0 : prevAge + 1;
                int endAge = isLast ? 0 : window - 1;
                for (int age = startAge; age <= endAge; age++)
                {
                    if (StepSatisfied(step, buf, age, facingRight))
                    {
                        // '>' 严格相邻：本步(更早)须正好在上一步(更晚)的下一帧，即 age == prevAge+1
                        if (step.Greater && !isLast && age != prevAge + 1)
                        {
                            continue;
                        }
                        found = age;
                        break;
                    }
                }
                if (found < 0)
                {
                    return false;
                }
                prevAge = found;
            }
            return true;
        }

        static bool StepSatisfied(MCommandStep step, MCommandBuffer buf, int age, bool facingRight)
        {
            if (step.Keys.Count == 0)
            {
                return false;
            }
            if (step.OrLogic)
            {
                // 任一键满足即可
                for (int k = 0; k < step.Keys.Count; k++)
                {
                    if (KeySatisfied(step.Keys[k], buf, age, facingRight))
                    {
                        return true;
                    }
                }
                return false;
            }
            // 全部键满足（'+' AND）
            for (int k = 0; k < step.Keys.Count; k++)
            {
                if (!KeySatisfied(step.Keys[k], buf, age, facingRight))
                {
                    return false;
                }
            }
            return true;
        }

        static bool KeySatisfied(MCommandKey key, MCommandBuffer buf, int age, bool facingRight)
        {
            if (key.IsButton)
            {
                bool downNow = (buf.Ago(age) & key.Bits) != 0;
                if (key.Release)
                {
                    return !downNow && (buf.Ago(age + 1) & key.Bits) != 0;
                }
                if (key.Hold)
                {
                    return downNow;
                }
                // 按下边沿
                return downNow && (buf.Ago(age + 1) & key.Bits) == 0;
            }

            // 方向键
            MInput want = ResolveDir(key, facingRight);
            if (key.ChargeTime > 0)
            {
                // 连续按住 charge 帧(含 age 起向更早)
                for (int t = 0; t < key.ChargeTime; t++)
                {
                    if (!DirHeld(buf.Ago(age + t), want, key.Dollar))
                    {
                        return false;
                    }
                }
                return true;
            }
            bool dirNow = DirHeld(buf.Ago(age), want, key.Dollar);
            if (key.Release)
            {
                return !dirNow && DirHeld(buf.Ago(age + 1), want, key.Dollar);
            }
            if (key.Hold)
            {
                return dirNow;
            }
            // 方向按下边沿：本帧成立、上一帧不成立
            return dirNow && !DirHeld(buf.Ago(age + 1), want, key.Dollar);
        }

        // 把命令键的方向解析为具体 U/D/L/R 位（B/F 按朝向）。
        static MInput ResolveDir(MCommandKey key, bool facingRight)
        {
            MInput dir = key.Bits & MInput.DirMask;   // U/D 分量
            if (key.IsFwd)
            {
                dir |= facingRight ? MInput.Right : MInput.Left;
            }
            if (key.IsBack)
            {
                dir |= facingRight ? MInput.Left : MInput.Right;
            }
            return dir;
        }

        // dollar(4way)：子集匹配(want 的位都按下即可，忽略多余)；否则方向轴精确相等。
        static bool DirHeld(MInput frame, MInput want, bool dollar)
        {
            MInput fd = frame & MInput.DirMask;
            if (dollar)
            {
                return (fd & want) == want && want != MInput.None;
            }
            return fd == want && want != MInput.None;
        }
    }
}
