using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using Lockstep.Math;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M5 第一批：基础 StateController（vel/pos/anim/ctrl/var/statetype）行为 + Ikemen 朝向语义。</summary>
    [TestFixture]
    public sealed class BasicControllersTests
    {
        static readonly MugenExprCompiler C = new MugenExprCompiler();
        static BytecodeExp E(string expr) => C.Compile(expr);
        static FFloat F(int num, int den) => FFloat.FromInt(num) / FFloat.FromInt(den);

        [Test]
        public void VelSet_SetsRawVelocity_NoFacing()
        {
            // VelSet 存原始值，不乘 facing（facing 在物理积分时应用）
            MChar c = new MChar { Facing = -FFloat.One };
            new VelSetController { X = E("2.5"), Y = E("0 - 3"), Z = E("4") }.Run(c);
            Assert.That(c.Vel.X.Raw, Is.EqualTo(F(5, 2).Raw), "vel.x=2.5 原值(不乘 facing)");
            Assert.That(c.Vel.Y.Raw, Is.EqualTo(FFloat.FromInt(-3).Raw));
            Assert.That(c.Vel.Z.Raw, Is.EqualTo(FFloat.FromInt(4).Raw));
        }

        [Test]
        public void VelAdd_Accumulates()
        {
            MChar c = new MChar { Vel = new FVector3(FFloat.FromInt(1), FFloat.FromInt(2), FFloat.FromInt(7)) };
            new VelAddController { X = E("3"), Y = E("0 - 5"), Z = E("2") }.Run(c);
            Assert.That(c.Vel.X.Raw, Is.EqualTo(FFloat.FromInt(4).Raw));
            Assert.That(c.Vel.Y.Raw, Is.EqualTo(FFloat.FromInt(-3).Raw));
            Assert.That(c.Vel.Z.Raw, Is.EqualTo(FFloat.FromInt(9).Raw));
        }

        [Test]
        public void PosSet_IsAbsolute_NoFacing()
        {
            // PosSet 绝对坐标（对齐 Ikemen setPosX，不乘 facing）
            MChar c = new MChar { Facing = -FFloat.One, Pos = new FVector3(FFloat.FromInt(99), FFloat.Zero, FFloat.Zero) };
            new PosSetController { X = E("10"), Y = E("20"), Z = E("30") }.Run(c);
            Assert.That(c.Pos.X.Raw, Is.EqualTo(FFloat.FromInt(10).Raw), "facing=-1 也设绝对 10");
            Assert.That(c.Pos.Y.Raw, Is.EqualTo(FFloat.FromInt(20).Raw));
            Assert.That(c.Pos.Z.Raw, Is.EqualTo(FFloat.FromInt(30).Raw));
        }

        [Test]
        public void PosAdd_XIsFacingRelative_YAbsolute()
        {
            // PosAdd：pos.x += x*facing（对齐 Ikemen addX），pos.y += y（绝对）
            MChar right = new MChar { Facing = FFloat.One, Pos = new FVector3(FFloat.FromInt(100), FFloat.FromInt(50), FFloat.Zero) };
            new PosAddController { X = E("5"), Y = E("3"), Z = E("4") }.Run(right);
            Assert.That(right.Pos.X.Raw, Is.EqualTo(FFloat.FromInt(105).Raw), "朝右 +5");
            Assert.That(right.Pos.Y.Raw, Is.EqualTo(FFloat.FromInt(53).Raw));
            Assert.That(right.Pos.Z.Raw, Is.EqualTo(FFloat.FromInt(4).Raw));

            MChar left = new MChar { Facing = -FFloat.One, Pos = new FVector3(FFloat.FromInt(100), FFloat.Zero, FFloat.FromInt(10)) };
            new PosAddController { X = E("5"), Z = E("2") }.Run(left);
            Assert.That(left.Pos.Z.Raw, Is.EqualTo(FFloat.FromInt(12).Raw));
            Assert.That(left.Pos.X.Raw, Is.EqualTo(FFloat.FromInt(95).Raw), "朝左 +5*(-1) → 后退到 95");
        }

        [Test]
        public void ChangeAnim_SetsAnimAndPrev()
        {
            MChar c = new MChar { AnimNo = 0 };
            new ChangeAnimController { Value = E("200") }.Run(c);
            Assert.That(c.AnimNo, Is.EqualTo(200));
            Assert.That(c.PrevAnimNo, Is.EqualTo(0), "记录前一动画号");
        }

        // 切到不存在的动画号：保留当前动画（对齐 Ikemen changeAnimEx a==nil → return）。
        // 这是 KFM"跳起浮空"bug 的根因——借用的 common1 切到 KFM 没有的 anim 44，曾把动画运行态冻死。
        static System.Collections.Generic.Dictionary<int, Lockstep.Mugen.Anim.MAnimData> AnimTableWith(params int[] nos)
        {
            System.Collections.Generic.Dictionary<int, Lockstep.Mugen.Anim.MAnimData> table =
                new System.Collections.Generic.Dictionary<int, Lockstep.Mugen.Anim.MAnimData>();
            for (int k = 0; k < nos.Length; k++)
            {
                table[nos[k]] = new Lockstep.Mugen.Anim.MAnimData { No = nos[k], Frames = new Lockstep.Mugen.Anim.MAnimFrame[1] };
            }
            return table;
        }

        static MAnimData TimedAnim(int no, params int[] times)
        {
            MAnimFrame[] frames = new MAnimFrame[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                frames[i] = new MAnimFrame { SpriteGroup = no, SpriteImage = i, Time = times[i] };
            }
            MAnimData anim = new MAnimData { No = no, Frames = frames };
            anim.ComputePacing();
            return anim;
        }

        static Dictionary<int, MAnimData> TimedAnimTable(params MAnimData[] anims)
        {
            Dictionary<int, MAnimData> table = new Dictionary<int, MAnimData>();
            for (int i = 0; i < anims.Length; i++)
            {
                table[anims[i].No] = anims[i];
            }
            return table;
        }

        static MBattleEngine LoadKfmEngine(out MChar p1)
        {
            string directory = TestAssets.KfmDir();
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("KFM test character is missing.");
            }

            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            MBattleEngine engine = new MBattleEngine { EnableDemoAutoTurnFallback = false };
            p1 = MCharLoader.SpawnChar(data, 1, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(data, 2, startStateNo: 0, startAnimNo: 0);
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        [Test]
        public void ChangeAnim_ToMissingAnim_IsNoOp_WhenTablePresent()
        {
            MChar c = new MChar { AnimNo = 41, AnimTable = AnimTableWith(41, 43) };
            new ChangeAnimController { Value = E("44") }.Run(c);   // 44 不在表中
            Assert.That(c.AnimNo, Is.EqualTo(41), "切到不存在的动画应 no-op，保留 41（不冻结）");
        }

        [Test]
        public void ChangeAnim_ToExistingAnim_Switches_WhenTablePresent()
        {
            MChar c = new MChar { AnimNo = 41, AnimTable = AnimTableWith(41, 43, 44) };
            new ChangeAnimController { Value = E("44") }.Run(c);
            Assert.That(c.AnimNo, Is.EqualTo(44), "存在则正常切换");
            Assert.That(c.PrevAnimNo, Is.EqualTo(41));
        }

        [Test]
        public void StateDefCtrl_ExplicitZeroAndOneOverrideCurrentControl()
        {
            MChar c = new MChar { Ctrl = true };

            new MStateDef { Ctrl = E("0") }.RunInit(c);
            Assert.That(c.Ctrl, Is.False, "statedef ctrl=0 must explicitly clear control.");

            new MStateDef { Ctrl = E("1") }.RunInit(c);
            Assert.That(c.Ctrl, Is.True, "statedef ctrl=1 must explicitly grant control.");
        }

        [Test]
        public void ChangeState_DefaultCtrlAndAnimMinusOne_DoNotOverride()
        {
            string cns =
                "[Statedef 0]\n" +
                "[State 0, change]\n" +
                "type = ChangeState\n" +
                "trigger1 = 1\n" +
                "value = 20\n" +
                "[Statedef 20]\n" +
                "type = S\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            MChar c = new MChar
            {
                StateNo = 0,
                Ctrl = false,
                AnimNo = 123,
                AnimRunningNo = 123,
            };

            new MStateMachine().RunFrame(c, states);

            Assert.That(c.StateNo, Is.EqualTo(20));
            Assert.That(c.Ctrl, Is.False, "missing ChangeState ctrl keeps the previous ctrl value.");
            Assert.That(c.AnimNo, Is.EqualTo(123), "missing ChangeState anim defaults to -1 and keeps current animation.");
        }

        [Test]
        public void ChangeState_CtrlAndAnimExpressions_EvaluateAtRunTime()
        {
            string cns =
                "[Statedef 0]\n" +
                "[State 0, change]\n" +
                "type = ChangeState\n" +
                "trigger1 = 1\n" +
                "value = 20\n" +
                "ctrl = var(0)\n" +
                "anim = 200 + var(1)\n" +
                "[Statedef 20]\n" +
                "type = S\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            MChar c = new MChar
            {
                StateNo = 0,
                Ctrl = true,
                AnimNo = 0,
                AnimTable = TimedAnimTable(TimedAnim(0, 1), TimedAnim(201, 3, 4)),
            };
            c.IntVars[0] = 0;
            c.IntVars[1] = 1;

            new MStateMachine().RunFrame(c, states);

            Assert.That(c.StateNo, Is.EqualTo(20));
            Assert.That(c.Ctrl, Is.False, "ctrl expression var(0)=0 should clear control.");
            Assert.That(c.AnimNo, Is.EqualTo(201));
            Assert.That(c.AnimTime, Is.EqualTo(-7), "ChangeState anim expression should refresh derived AnimTime immediately.");
        }

        [Test]
        public void CtrlSet_SetsControl()
        {
            MChar c = new MChar { Ctrl = false };
            new CtrlSetController { Value = E("1") }.Run(c);
            Assert.IsTrue(c.Ctrl);
            new CtrlSetController { Value = E("0") }.Run(c);
            Assert.IsFalse(c.Ctrl);
        }

        [Test]
        public void ChangeAnim_Elem_JumpsToRequestedOneBasedElement()
        {
            MChar c = new MChar { AnimNo = 0 };
            c.AnimTable = new System.Collections.Generic.Dictionary<int, Lockstep.Mugen.Anim.MAnimData>
            {
                [0] = new Lockstep.Mugen.Anim.MAnimData
                {
                    No = 0,
                    Frames = new[] { new Lockstep.Mugen.Anim.MAnimFrame { Time = 1 } },
                },
                [210] = new Lockstep.Mugen.Anim.MAnimData
                {
                    No = 210,
                    Frames = new[]
                    {
                        new Lockstep.Mugen.Anim.MAnimFrame { Time = 1 },
                        new Lockstep.Mugen.Anim.MAnimFrame { Time = 1 },
                        new Lockstep.Mugen.Anim.MAnimFrame { Time = 1 },
                        new Lockstep.Mugen.Anim.MAnimFrame { Time = 1 },
                    },
                },
            };

            new ChangeAnimController { Value = E("210"), Elem = E("4") }.Run(c);

            Assert.That(c.AnimNo, Is.EqualTo(210));
            Assert.That(c.AnimElem, Is.EqualTo(3), "ChangeAnim elem is 1-based in CNS and 0-based at runtime.");
            Assert.That(c.AnimElemNo, Is.EqualTo(4));
        }

        [Test]
        public void VarSet_IntAndFloat()
        {
            MChar c = new MChar();
            new VarSetController { Index = 3, IsFloat = false, Value = E("42") }.Run(c);
            Assert.That(c.IntVars[3], Is.EqualTo(42));
            new VarSetController { Index = 1, IsFloat = true, Value = E("1.5") }.Run(c);
            Assert.That(c.FloatVars[1].Raw, Is.EqualTo(F(3, 2).Raw));
        }

        [Test]
        public void VarAdd_AccumulatesFromZero()
        {
            MChar c = new MChar();
            new VarAddController { Index = 5, Value = E("10") }.Run(c);
            new VarAddController { Index = 5, Value = E("7") }.Run(c);
            Assert.That(c.IntVars[5], Is.EqualTo(17), "未设变量按 0 起算累加");
        }

        [Test]
        public void VarSet_RoundTripsThroughTrigger()
        {
            // VarSet 写入后，var(n) trigger 应读到同值（控制器↔表达式 VM 闭环）
            MChar c = new MChar();
            new VarSetController { Index = 2, Value = E("88") }.Run(c);
            Assert.That(C.Compile("var(2)").Run(c).ToI(), Is.EqualTo(88));
        }

        [Test]
        public void StateTypeSet_ChangesTypes()
        {
            MChar c = new MChar { StateType = 1, MoveType = 1, Ctrl = true };
            // 改为 air(4) + attack(4) + ctrl=0
            new StateTypeSetController { StateType = 4, MoveType = 4, CtrlExpr = E("0") }.Run(c);
            Assert.That(c.StateType, Is.EqualTo(4));
            Assert.That(c.MoveType, Is.EqualTo(4));
            Assert.IsFalse(c.Ctrl);
            // statetype = A 经编译应为真
            Assert.IsTrue(C.Compile("statetype = A").Run(c).ToB());
        }

        [TestCase(400, MInput.X, true, TestName = "RealKfm_CrouchingLightPunch400_RecoversCtrlAndAnimTime")]
        [TestCase(410, MInput.Y, false, TestName = "RealKfm_CrouchingStrongPunch410_RecoversCtrlAndAnimTime")]
        public void RealKfm_CrouchingPunchRecovery_RestoresCtrlAndAnimTime(int stateNo, MInput button, bool expectsCtrlBeforeRecovery)
        {
            MBattleEngine engine = LoadKfmEngine(out MChar p1);

            p1.QueueTransition(11, p1.PlayerNo);
            engine.Tick(new[] { MInput.None, MInput.None });
            Assert.That(p1.StateNo, Is.EqualTo(11), "precondition: KFM should be crouching.");
            Assert.That(p1.Ctrl, Is.True, "precondition: crouch idle should be controllable.");

            engine.Tick(new[] { MInput.Down | button, MInput.None });
            Assert.That(p1.StateNo, Is.EqualTo(stateNo));
            Assert.That(p1.Ctrl, Is.False, "crouching attack statedef starts with ctrl=0.");
            Assert.That(p1.AnimNo, Is.EqualTo(stateNo));
            Assert.That(p1.AnimTime, Is.LessThan(0));

            int framesInMove = 1;
            int firstCtrlTime = -1;
            bool animTimeAdvanced = false;
            int previousAnimTime = p1.AnimTime;
            bool returnedToCrouch = false;
            for (int i = 0; i < 80; i++)
            {
                engine.Tick(new[] { MInput.Down | button, MInput.None });
                if (p1.StateNo == stateNo)
                {
                    framesInMove++;
                    animTimeAdvanced |= p1.AnimTime != previousAnimTime;
                    previousAnimTime = p1.AnimTime;
                    if (firstCtrlTime < 0 && p1.Ctrl)
                    {
                        firstCtrlTime = p1.Time;
                    }
                    continue;
                }

                Assert.That(p1.StateNo, Is.EqualTo(11), "KFM crouching punch should recover to crouch state 11.");
                returnedToCrouch = true;
                break;
            }

            Assert.That(returnedToCrouch, Is.True, "KFM crouching punch should finish within the test window.");
            Assert.That(framesInMove, Is.GreaterThan(3), "move should keep authored recovery frames.");
            Assert.That(animTimeAdvanced, Is.True, "attack animation should progress before AnimTime=0 recovery.");
            if (expectsCtrlBeforeRecovery)
            {
                Assert.That(firstCtrlTime, Is.GreaterThanOrEqualTo(6), "state 400 authored CtrlSet is at Time=6.");
            }
            else
            {
                Assert.That(firstCtrlTime, Is.EqualTo(-1), "state 410 relies on ChangeState ctrl=1 at recovery.");
            }
            Assert.That(p1.Ctrl, Is.True, "recovered crouch should be controllable.");
            Assert.That(p1.AnimNo, Is.EqualTo(11), "state 11 statedef anim should restore crouch animation.");
            Assert.That(p1.AnimRunningNo, Is.EqualTo(11));
            Assert.That(p1.AnimElemNo, Is.EqualTo(1));
            Assert.That(p1.AnimTime, Is.EqualTo(0), "KFM action 11 is a one-frame crouch anim, so refreshed AnimTime is 0.");
        }

        [Test]
        public void Null_DoesNothing()
        {
            MChar c = new MChar { Life = 100, StateNo = 5 };
            bool changed = new NullController().Run(c);
            Assert.IsFalse(changed);
            Assert.That(c.Life, Is.EqualTo(100));
            Assert.That(c.StateNo, Is.EqualTo(5));
        }
    }
}
