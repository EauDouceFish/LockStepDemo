// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go actionPrepare（11435-11481 的引擎硬编码基础动作块）。
// Adapted to fixed-point. 这是 MUGEN/Ikemen "站/走/蹲/跳/刹车" 的状态转移——不在角色 .cns 数据里，
// 由引擎按输入边沿 + ctrl 直接驱动 changeState。守招(120)需 inguarddist+敌方攻击，单角色 demo 未接，标 deferred。
// See Docs/动作系统_Ikemen移植.md.
using Lockstep.Math;
using Lockstep.Mugen.Char;

namespace Lockstep.Mugen.Battle
{
    /// <summary>
    /// 引擎硬编码基础动作（移植 Ikemen actionPrepare）。每帧在状态机之前对受控角色判定：
    /// 持上→跳(40)、空中持上边沿→空跳(45)、立持下→蹲(10)、蹲松下→起立(12)、立持前/后→走(20)、走中无方向→刹车(0)。
    /// 仅设 <see cref="MChar.PendingStateNo"/>（changeState 的缓冲），由 <see cref="State.MStateMachine"/> 同帧应用。
    /// 读 <see cref="MChar.Input"/> 的边沿计数（Fb/Bb/Ub/Db）与 <see cref="MChar.Control"/>()/KeyCtrl。
    /// </summary>
    public static class MActionSystem
    {
        // statetype 码（对齐 MugenCodes：S=1/C=2/A=4/L=8）。
        const int ST_S = 1;
        const int ST_C = 2;
        const int ST_A = 4;
        // physics 码（对齐 MugenCodes.Physics：A=4）。空中物理用于落地检测。
        const int PHYS_A = 4;

        // 保留状态号（对齐 Ikemen / MUGEN 公共状态约定）。
        const int StWalk = 20;
        const int StStand = 0;
        const int StStandToCrouch = 10;
        const int StCrouchToStand = 12;
        const int StJumpStart = 40;
        const int StAirJumpStart = 45;
        const int StLand = 52;
        const int StRunFwd = 100;
        const int StRunJumpLand = 105;

        /// <summary>对受控角色执行引擎内置基础动作判定（移植 actionPrepare 硬编码键块）。</summary>
        public static void Prepare(MChar c)
        {
            if (c == null || c.Input == null)
            {
                return;
            }

            // gate：玩家按键控制 + 未禁用硬编码键（对齐 c.keyctrl[0] && c.cmd != nil && !asf(nohardcodedkeys)）。
            if (c.KeyCtrl && !Asf(c, MAssertFlag.NoHardcodedKeys))
            {
                if (c.Control())
                {
                    // 守招(120)：需 inguarddist + 敌方攻击中，单角色无法满足 → deferred（不入 else-if 链）。
                    if (!Asf(c, MAssertFlag.NoJump) && c.StateType == ST_S && c.Input.Ub > 0)
                    {
                        if (c.StateNo != StJumpStart)
                        {
                            c.QueueTransition(StJumpStart, c.PlayerNo);   // 跳跃起始
                        }
                    }
                    else if (!Asf(c, MAssertFlag.NoAirJump) && c.StateType == ST_A && c.Input.Ub == 1 &&
                        c.Pos.Y <= -AirjumpHeight(c) && c.AirJumpCount < AirjumpNum(c))
                    {
                        if (c.StateNo != StAirJumpStart || c.Time > 0)
                        {
                            c.AirJumpCount++;
                            c.QueueTransition(StAirJumpStart, c.PlayerNo);   // 空中跳跃
                        }
                    }
                    else if (!Asf(c, MAssertFlag.NoCrouch) && c.StateType == ST_S && c.Input.Db > 0)
                    {
                        if (c.StateNo != StStandToCrouch)
                        {
                            if (c.StateNo != StRunFwd)
                            {
                                c.Vel = new FVector3(FFloat.Zero, c.Vel.Y, c.Vel.Z);
                            }
                            c.QueueTransition(StStandToCrouch, c.PlayerNo);   // 立 → 蹲
                        }
                    }
                    else if (!Asf(c, MAssertFlag.NoStand) && c.StateType == ST_C && c.Input.Db <= 0)
                    {
                        if (c.StateNo != StCrouchToStand)
                        {
                            c.QueueTransition(StCrouchToStand, c.PlayerNo);   // 蹲 → 起立
                        }
                    }
                    else if (!Asf(c, MAssertFlag.NoWalk) && c.StateType == ST_S &&
                        (c.Input.Fb > 0) != (c.Input.Bb > 0))
                    {
                        // 走路：前/后恰一方持续按住即走（inguarddist 未接，等价 Ikemen 无敌人靠近时的 XOR 解）。
                        if (c.StateNo != StWalk)
                        {
                            c.QueueTransition(StWalk, c.PlayerNo);
                        }
                    }
                }

                // 刹车：走路中前后同按或同松 → 回站立。不需要 ctrl（对齐 Ikemen 注释 "Braking is special"）。
                if (!Asf(c, MAssertFlag.NoBrake) && c.StateNo == StWalk &&
                    (c.Input.Bb > 0) == (c.Input.Fb > 0))
                {
                    c.QueueTransition(StStand, c.PlayerNo);
                }
            }

            // 落地（非空中）即清零空跳计数（对齐 char.go 11479-11481，在 keyctrl 块之外，无条件执行）。
            if (c.StateType != ST_A)
            {
                c.AirJumpCount = 0;
            }
        }

        /// <summary>
        /// 空中物理落地检测（移植 char.go actionRun 内 posUpdate 之后的 "Land from aerial physics"，11717-11723）。
        /// 在物理积分之后调用：空中物理 + 下落(vel.y>0) + 触地(pos.y>=地面) + 非跑跳落地特例(105) → 强制落地态 52。
        /// 缺此则跳起后无限下落（地面夹取在 MUGEN 由此引擎硬编码转移负责，不在 .cns 数据里）。
        /// </summary>
        public static void LandCheck(MChar c)
        {
            if (c == null)
            {
                return;
            }
            if (c.Physics == PHYS_A && c.Vel.Y.Raw > 0 && c.Pos.Y.Raw >= 0 && c.StateNo != StRunJumpLand)
            {
                c.QueueTransition(StLand, c.PlayerNo);
            }
        }

        static bool Asf(MChar c, MAssertFlag flag)
        {
            return (c.AssertFlags & (int)flag) != 0;
        }

        static FFloat AirjumpHeight(MChar c)
        {
            return c.Constants != null ? c.Constants.AirjumpHeight : FFloat.FromInt(35);
        }

        static int AirjumpNum(MChar c)
        {
            return c.Constants != null ? c.Constants.AirjumpNum : 0;
        }
    }
}
