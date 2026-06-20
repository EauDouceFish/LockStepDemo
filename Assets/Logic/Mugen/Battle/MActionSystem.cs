// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go actionPrepare/actionRun hardcoded basic action paths.
// Adapted to fixed-point and this demo's MChar/MInputBuffer model.
using Lockstep.Math;
using Lockstep.Mugen.Char;

namespace Lockstep.Mugen.Battle
{
    /// <summary>
    /// Engine hardcoded basic actions. This runs before the state machine for controlled
    /// characters and queues common state transitions such as jump, crouch, walk and guard.
    /// </summary>
    public static class MActionSystem
    {
        const int ST_S = 1;
        const int ST_C = 2;
        const int ST_A = 4;
        const int MT_A = 4;
        const int PHYS_A = 4;

        const int StWalk = 20;
        const int StStand = 0;
        const int StStandToCrouch = 10;
        const int StCrouchToStand = 12;
        const int StStandGuard = 120;
        const int StJumpStart = 40;
        const int StAirJumpStart = 45;
        const int StLand = 52;
        const int StRunFwd = 100;
        const int StRunJumpLand = 105;

        // Ikemen reference: src/char.go:11435-11481 Char.actionPrepare hardcoded jump/crouch/walk/brake decisions.
        public static void Prepare(MChar c)
        {
            if (c == null || c.Input == null)
            {
                return;
            }

            bool canUseHardcodedKeys = c.KeyCtrl && !Asf(c, MAssertFlag.NoHardcodedKeys);
            bool canStartBasicAction = canUseHardcodedKeys && c.MoveType != MT_A;
            bool wantsStandGuard = canStartBasicAction && c.Control() &&
                c.StateType == ST_S && c.Input.Bb > 0 && InGuardDist(c);
            c.Guarding = wantsStandGuard || IsGuardState(c.StateNo);

            if (canUseHardcodedKeys)
            {
                if (canStartBasicAction && c.Control())
                {
                    if (wantsStandGuard)
                    {
                        if (c.StateNo != StStandGuard)
                        {
                            c.QueueTransition(StStandGuard, c.PlayerNo);
                        }
                    }
                    else if (!Asf(c, MAssertFlag.NoJump) && c.StateType == ST_S && c.Input.Ub > 0)
                    {
                        if (c.StateNo != StJumpStart)
                        {
                            c.QueueTransition(StJumpStart, c.PlayerNo);
                        }
                    }
                    else if (!Asf(c, MAssertFlag.NoAirJump) && c.StateType == ST_A && c.Input.Ub == 1 &&
                        c.Pos.Y <= -AirjumpHeight(c) && c.AirJumpCount < AirjumpNum(c))
                    {
                        if (c.StateNo != StAirJumpStart || c.Time > 0)
                        {
                            c.AirJumpCount++;
                            c.QueueTransition(StAirJumpStart, c.PlayerNo);
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
                            c.QueueTransition(StStandToCrouch, c.PlayerNo);
                        }
                    }
                    else if (!Asf(c, MAssertFlag.NoStand) && c.StateType == ST_C && c.Input.Db <= 0)
                    {
                        if (c.StateNo != StCrouchToStand)
                        {
                            c.QueueTransition(StCrouchToStand, c.PlayerNo);
                        }
                    }
                    else if (!Asf(c, MAssertFlag.NoWalk) && c.StateType == ST_S &&
                        (c.Input.Fb > 0) != (c.Input.Bb > 0))
                    {
                        if (c.StateNo != StWalk)
                        {
                            c.QueueTransition(StWalk, c.PlayerNo);
                        }
                    }
                }

                // Braking is special in Ikemen: walk can brake even outside the ctrl branch.
                if (c.MoveType != MT_A && !Asf(c, MAssertFlag.NoBrake) && c.StateNo == StWalk &&
                    (c.Input.Bb > 0) == (c.Input.Fb > 0))
                {
                    c.QueueTransition(StStand, c.PlayerNo);
                }
            }

            if (c.StateType != ST_A)
            {
                c.AirJumpCount = 0;
            }
        }

        // Project-specific demo shim: keep controllable grounded fighters facing their opponent.
        // Full MUGEN/Ikemen turning is authored through common state 5 and Turn controllers. The
        // engine calls this after input buffering and state logic, so current-frame B/F command
        // parsing and state triggers use the pre-turn facing; render and the next frame see the new facing.
        public static void AutoTurn(MChar c)
        {
            if (c == null || c.P2 == null || !c.KeyCtrl || !c.Control())
            {
                return;
            }
            if ((c.AssertFlags & (int)MAssertFlag.NoAutoTurn) != 0 || c.StateOwner != null)
            {
                return;
            }
            if (c.MoveType == 2 || c.StateType == ST_A)
            {
                return;
            }
            if (c.Pos.X == c.P2.Pos.X)
            {
                return;
            }

            c.Facing = c.P2.Pos.X > c.Pos.X ? FFloat.One : FFloat.MinusOne;
        }

        // Ikemen reference: src/char.go:11717-11723 Char.actionRun aerial-physics land transition to state 52.
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

        // Ikemen reference: src/char.go actionPrepare AssertSpecial gates such as NoHardcodedKeys/NoBrake.
        static bool Asf(MChar c, MAssertFlag flag)
        {
            return (c.AssertFlags & (int)flag) != 0;
        }

        // Ikemen reference: src/char.go:11456-11464 actionPrepare airjump height check.
        static FFloat AirjumpHeight(MChar c)
        {
            return c.Constants != null ? c.Constants.AirjumpHeight : FFloat.FromInt(35);
        }

        // Ikemen reference: src/char.go:11456-11464 actionPrepare airjump count check.
        static int AirjumpNum(MChar c)
        {
            return c.Constants != null ? c.Constants.AirjumpNum : 0;
        }

        // Ikemen reference: src/char.go inguarddist guard-entry gate.
        static bool InGuardDist(MChar c)
        {
            if (c.P2 == null || c.P2.MoveType != 4)
            {
                return false;
            }
            FFloat dx = c.Pos.X - c.P2.Pos.X;
            if (dx.Raw < 0)
            {
                dx = -dx;
            }
            return dx <= c.P2.AttackDistX;
        }

        // Ikemen reference: common guard states stay guard-capable while the authored state runs.
        static bool IsGuardState(int stateNo)
        {
            return stateNo == 120 || stateNo == 130 || stateNo == 131 || stateNo == 132 ||
                stateNo == 140 || stateNo == 150 || stateNo == 151 || stateNo == 152 ||
                stateNo == 153 || stateNo == 154 || stateNo == 155;
        }
    }
}
