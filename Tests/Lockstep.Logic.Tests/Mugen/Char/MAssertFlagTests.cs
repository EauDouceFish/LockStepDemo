using NUnit.Framework;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen
{
    /// <summary>模块 B：引擎硬编码动作禁制标志（NoJump/NoCrouch/... 移植 char.go ASF_*）解析与断言。</summary>
    [TestFixture]
    public sealed class MAssertFlagTests
    {
        [Test]
        public void NewActionFlags_AreDistinctBits()
        {
            // 各标志互不重叠（位标志正确性）。
            int all = (int)MAssertFlag.NoJump | (int)MAssertFlag.NoCrouch | (int)MAssertFlag.NoStand
                | (int)MAssertFlag.NoAirJump | (int)MAssertFlag.NoBrake | (int)MAssertFlag.NoHardcodedKeys
                | (int)MAssertFlag.PostRoundInput | (int)MAssertFlag.NoWalk;
            Assert.That(System.Numerics.BitOperations.PopCount((uint)all), Is.EqualTo(8), "8 个标志占 8 个独立位");
        }

        [Test]
        public void Parser_RecognizesNoJump()
        {
            string cns =
                "[Statedef 0]\ntype = S\n\n" +
                "[State 0, x]\ntype = AssertSpecial\ntrigger1 = 1\nflag = nojump\n";
            var states = MugenCnsParser.Parse(cns);
            MStateDef def = states[0];
            MChar c = new MChar { StateNo = 0 };
            // 跑该状态的 AssertSpecial 控制器。
            foreach (MStateController ctrl in def.Controllers)
            {
                if (ctrl is AssertSpecialController && ctrl.TriggerPasses(c))
                {
                    ctrl.Run(c);
                }
            }
            Assert.That((c.AssertFlags & (int)MAssertFlag.NoJump) != 0, Is.True, "assertspecial flag=nojump 已置位");
        }

        [Test]
        public void Parser_RecognizesAllNewFlags()
        {
            string[] names = { "nocrouch", "nostand", "noairjump", "nobrake", "nohardcodedkeys", "postroundinput" };
            MAssertFlag[] expected =
            {
                MAssertFlag.NoCrouch, MAssertFlag.NoStand, MAssertFlag.NoAirJump,
                MAssertFlag.NoBrake, MAssertFlag.NoHardcodedKeys, MAssertFlag.PostRoundInput,
            };
            for (int i = 0; i < names.Length; i++)
            {
                string cns = "[Statedef 0]\ntype = S\n\n[State 0, x]\ntype = AssertSpecial\ntrigger1 = 1\nflag = " + names[i] + "\n";
                MStateDef def = MugenCnsParser.Parse(cns)[0];
                MChar c = new MChar();
                foreach (MStateController ctrl in def.Controllers)
                {
                    if (ctrl is AssertSpecialController) { ctrl.Run(c); }
                }
                Assert.That((c.AssertFlags & (int)expected[i]) != 0, Is.True, names[i] + " 解析置位");
            }
        }
    }
}
