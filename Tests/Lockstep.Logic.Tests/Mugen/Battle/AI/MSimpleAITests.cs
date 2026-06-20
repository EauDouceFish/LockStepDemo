using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Battle.AI;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.Logic.Tests.Mugen.Battle.AI
{
    /// <summary>简单确定性 AI：走向对手、进距出招、无控制权时让位、确定性可回放。</summary>
    [TestFixture]
    public sealed class MSimpleAITests
    {
        static MChar At(int x, bool ctrl = true)
        {
            MChar c = new MChar
            {
                Pos = new FVector3(FFloat.FromInt(x), FFloat.Zero, FFloat.Zero),
                Facing = FFloat.One,
                Ctrl = ctrl,
            };
            return c;
        }

        [Test]
        public void WalksRight_WhenOpponentIsToTheRight()
        {
            MSimpleAI ai = new MSimpleAI();
            MInput input = ai.Decide(At(0), At(200), frame: 1);
            Assert.IsTrue((input & MInput.Right) != 0, "对手在右 → 向右走");
            Assert.IsTrue((input & MInput.Left) == 0);
        }

        [Test]
        public void WalksLeft_WhenOpponentIsToTheLeft()
        {
            MSimpleAI ai = new MSimpleAI();
            MInput input = ai.Decide(At(0), At(-200), frame: 1);
            Assert.IsTrue((input & MInput.Left) != 0, "对手在左 → 向左走");
        }

        [Test]
        public void AttacksWhenInRange_OverTime()
        {
            MSimpleAI ai = new MSimpleAI();
            MChar self = At(0);
            MChar opp = At(20);   // 近身
            bool attackedSomewhere = false;
            for (int f = 0; f < 60; f++)
            {
                MInput input = ai.Decide(self, opp, f);
                if ((input & (MInput.A | MInput.B | MInput.C)) != 0)
                {
                    attackedSomewhere = true;
                }
            }
            Assert.IsTrue(attackedSomewhere, "进入攻击距离后应在节奏帧出招");
        }

        [Test]
        public void NoInput_WhenNotInControl()
        {
            MSimpleAI ai = new MSimpleAI();
            MInput input = ai.Decide(At(0, ctrl: false), At(20), frame: 5);
            Assert.That(input, Is.EqualTo(MInput.None), "失控时不输入（让受击机/硬编码接管）");
        }

        [Test]
        public void Deterministic_SameStateSameSeedSameInput()
        {
            MSimpleAI a = new MSimpleAI(seed: 7);
            MSimpleAI b = new MSimpleAI(seed: 7);
            for (int f = 0; f < 30; f++)
            {
                MInput ia = a.Decide(At(0), At(30), f);
                MInput ib = b.Decide(At(0), At(30), f);
                Assert.That(ib, Is.EqualTo(ia), "同种子同状态同帧 → 同输入（可回放）");
            }
        }
    }
}
