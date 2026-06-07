using System.Collections.Generic;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Battle
{
    [TestFixture]
    public sealed class KnockdownRecoveryTests
    {
        [Test]
        public void RealKfm_Liedown5110_AutoGetsUpThrough5120AndReturnsToStand()
        {
            MCharData data = MugenCharacterPackageTestLoader.Load(TestAssets.KfmDir());
            MBattleEngine engine = new MBattleEngine();
            MChar kfm = MCharLoader.SpawnChar(data, 1, startStateNo: 5110, startAnimNo: 5110);
            kfm.StateType = 8;
            kfm.MoveType = 2;
            kfm.Physics = 0;
            kfm.Ctrl = false;
            kfm.KeyCtrl = true;
            engine.Add(kfm, data);
            engine.Add(MCharLoader.SpawnChar(data, 2), data);
            engine.LinkPair();

            bool sawGetUp = false;
            bool recovered = false;
            for (int frame = 0; frame < 180; frame++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
                if (kfm.StateNo == 5120)
                {
                    sawGetUp = true;
                }
                if (sawGetUp && kfm.StateNo == 0 && kfm.StateType == 1 && kfm.Ctrl)
                {
                    recovered = true;
                    break;
                }
            }

            Assert.That(sawGetUp, Is.True, "5110 liedown should auto transition to 5120 getup");
            Assert.That(recovered, Is.True, "5120 should return to controllable stand state 0");
        }

        [Test]
        public void RealKfm_StartRound_DoesNotSpawnInLiedown()
        {
            MCharData data = MugenCharacterPackageTestLoader.Load(TestAssets.KfmDir());
            MBattleEngine engine = new MBattleEngine();
            MChar p1 = MCharLoader.SpawnChar(data, 1, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(data, 2, startStateNo: 0, startAnimNo: 0);
            engine.Add(p1, data);
            engine.Add(p2, data);
            engine.LinkPair();
            engine.StartRound();
            for (int i = 0; i < 5; i++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
            }

            Assert.That(p1.StateNo, Is.Not.EqualTo(5110));
            Assert.That(p1.StateType, Is.Not.EqualTo(8));
            Assert.That(p1.Ctrl, Is.True);
        }
    }
}
