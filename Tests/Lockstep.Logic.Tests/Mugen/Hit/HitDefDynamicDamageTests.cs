using System.IO;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.StateCtrl;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class HitDefDynamicDamageTests
    {
        [Test]
        public void HitDefDamageExpression_ResolvesAgainstRuntimeFvar()
        {
            string text = @"
[Statedef 200]
[State 200, hit]
type = HitDef
trigger1 = 1
damage = ceil(cond(fvar(10) * 23 < 1, 1, fvar(10) * 23)), 0
";

            HitDefController controller = FirstHitDef(MugenCnsParser.Parse(text)[200]);
            Assert.That(controller.Template.HitDamageExpr, Is.Not.Null);

            MChar character = new MChar();
            character.FloatVars[10] = FFloat.One;
            controller.Run(character);

            Assert.That(character.HitDef.HitDamage, Is.EqualTo(23));
            Assert.That(character.HitDef.GuardDamage, Is.EqualTo(0));
        }

        [TestCase("Ananzi", 200, 20)]
        [TestCase("Noroko", 200, 20)]
        [TestCase("Peketo", 200, 20)]
        public void ImportedCharacterStandingLightAttack_UsesRuntimeDamageScale(
            string folder,
            int stateNo,
            int minimumExpectedDamage)
        {
            string directory = TestAssets.CharDir(folder);
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("MUGEN character is missing: " + folder);
            }

            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            HitDefController controller = FirstHitDef(data.States[stateNo]);
            MChar character = MCharLoader.SpawnChar(data, 1);
            character.FloatVars[10] = FFloat.One;

            controller.Run(character);

            Assert.That(character.HitDef.HitDamage, Is.GreaterThanOrEqualTo(minimumExpectedDamage),
                folder + " state " + stateNo + " should resolve visible runtime damage.");
        }

        [TestCase("Ananzi")]
        [TestCase("Noroko")]
        [TestCase("Peketo")]
        public void ImportedCharacterNormals_CanDamageOpponentInBattleHarness(string folder)
        {
            string directory = TestAssets.CharDir(folder);
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("MUGEN character is missing: " + folder);
            }

            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            int bestDamage = 0;
            string bestCase = "";
            int[] states = { 200, 210, 230, 240, 400, 410, 430, 440 };
            int[] distances = { 20, 30, 40, 50, 60, 70, 80 };

            for (int s = 0; s < states.Length; s++)
            {
                if (!data.States.ContainsKey(states[s]))
                {
                    continue;
                }
                for (int d = 0; d < distances.Length; d++)
                {
                    int damage = RunDirectAttack(data, states[s], distances[d]);
                    if (damage > bestDamage)
                    {
                        bestDamage = damage;
                        bestCase = "state=" + states[s] + " distance=" + distances[d];
                    }
                }
            }

            TestContext.Progress.WriteLine(folder + " best normal damage: " + bestDamage + " (" + bestCase + ")");
            Assert.That(bestDamage, Is.GreaterThanOrEqualTo(10),
                folder + " should be able to deal visible damage through the battle pipeline.");
        }

        static HitDefController FirstHitDef(Lockstep.Mugen.State.MStateDef state)
        {
            for (int i = 0; i < state.Controllers.Count; i++)
            {
                if (state.Controllers[i] is HitDefController hitDef)
                {
                    return hitDef;
                }
            }
            Assert.Fail("State has no HitDef controller: " + state.No);
            return null;
        }

        static int RunDirectAttack(MCharData data, int stateNo, int distance)
        {
            MBattleEngine engine = new MBattleEngine();
            MChar attacker = MCharLoader.SpawnChar(data, 0);
            MChar defender = MCharLoader.SpawnChar(data, 1);
            int half = distance / 2;
            attacker.Pos = new FVector3(FFloat.FromInt(-half), FFloat.Zero, FFloat.Zero);
            defender.Pos = new FVector3(FFloat.FromInt(distance - half), FFloat.Zero, FFloat.Zero);
            defender.Facing = -FFloat.One;
            attacker.FloatVars[10] = FFloat.One;
            defender.FloatVars[10] = FFloat.One;

            engine.Add(attacker, data);
            engine.Add(defender, data);
            engine.LinkPair();
            engine.StartRound();
            defender.KeyCtrl = false;
            attacker.QueueTransition(stateNo, attacker.PlayerNo);

            int startLife = defender.Life;
            for (int frame = 0; frame < 90; frame++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
            }
            return startLife - defender.Life;
        }
    }
}
