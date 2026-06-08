using System.IO;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Battle
{
    [TestFixture]
    public sealed class AnimationRecoveryTests
    {
        [Test]
        public void Ananzi_WalkRelease_ReturnsToStateZeroAndIdleAnim()
        {
            string directory = TestAssets.CharDir("Ananzi");
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("Ananzi test character is missing.");
            }

            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            MBattleEngine engine = new MBattleEngine();
            MChar p1 = MCharLoader.SpawnChar(data, 1, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(data, 2, startStateNo: 0, startAnimNo: 0);
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            engine.StartRound();

            for (int i = 0; i < 12; i++)
            {
                engine.Tick(new[] { MInput.Right, MInput.None });
            }
            Assert.That(p1.StateNo, Is.EqualTo(20), "precondition: hardcoded walk should enter state 20.");

            for (int i = 0; i < 12; i++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
            }

            Assert.That(p1.StateNo, Is.EqualTo(0));
            Assert.That(p1.AnimNo, Is.EqualTo(0), "state 0 ChangeAnim must recover idle animation after walk release.");
        }

        [Test]
        public void Noroko_State210_OnHit_UsesChangeAnimElemAndRecovers()
        {
            string directory = TestAssets.CharDir("Noroko");
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("Noroko test character is missing.");
            }

            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            MBattleEngine engine = new MBattleEngine();
            MChar p1 = MCharLoader.SpawnChar(data, 1, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(data, 2, startStateNo: 0, startAnimNo: 0);
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            engine.StartRound();
            p1.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.FromInt(-20), Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            p2.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.FromInt(20), Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            p1.Facing = Lockstep.Math.FFloat.One;
            p2.Facing = -Lockstep.Math.FFloat.One;
            p1.QueueTransition(210, p1.PlayerNo);

            bool reachedLateElem = false;
            for (int i = 0; i < 180; i++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
                reachedLateElem |= p1.StateNo == 210 && p1.AnimNo == 210 && p1.AnimElemNo >= 4;
            }

            Assert.That(reachedLateElem, Is.True, "on contact ChangeAnim elem=4 should skip the repeated startup segment.");
            Assert.That(p1.StateNo, Is.EqualTo(0));
            Assert.That(p1.AnimNo, Is.EqualTo(0));
            Assert.That(p1.Ctrl, Is.True);
        }
    }
}
