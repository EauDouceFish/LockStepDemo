using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Tests.Mugen;

namespace Lockstep.Tests.Mugen.Battle
{
    [TestFixture]
    public sealed class AllCharactersFullSmokeTests
    {
        static IEnumerable<string> CharacterDirectories()
        {
            string root = TestAssets.MugenSourceDir();
            if (!Directory.Exists(root)) { yield break; }
            foreach (string directory in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(directory);
                if (name.StartsWith("_")) { continue; }
                if (MugenCharacterPackageTestLoader.PickMainDef(directory) != null) { yield return directory; }
            }
        }

        [TestCaseSource(nameof(CharacterDirectories))]
        public void FullCharacterPackage_RunsNegativeStatesDeterministically(string directory)
        {
            MCharData firstData = MugenCharacterPackageTestLoader.Load(directory);
            MCharData secondData = MugenCharacterPackageTestLoader.Load(directory);
            foreach (KeyValuePair<string, int> unknown in firstData.Compatibility.UnknownControllers)
            {
                TestContext.Progress.WriteLine(Path.GetFileName(directory) +
                    ": unknown controller " + unknown.Key + " x" + unknown.Value);
            }
            foreach (KeyValuePair<string, int> parsedOnly in firstData.Compatibility.ParsedOnlyControllers)
            {
                TestContext.Progress.WriteLine(Path.GetFileName(directory) +
                    ": parsed-only controller " + parsedOnly.Key + " x" + parsedOnly.Value);
            }
            MBattleEngine first = CreateEngine(firstData);
            MBattleEngine second = CreateEngine(secondData);
            for (int frame = 0; frame < 240; frame++)
            {
                MInput[] inputs = InputsFor(frame);
                first.Tick(inputs);
                second.Tick(inputs);
                Assert.That(second.ComputeHash(), Is.EqualTo(first.ComputeHash()),
                    Path.GetFileName(directory) + ": nondeterministic at frame " + frame);
            }

            Assert.That(first.Chars[0].StateNo, Is.Not.EqualTo(int.MinValue));
            Assert.That(firstData.Definition.Files.Sound, Is.Not.Null.And.Not.Empty);
        }

        static MBattleEngine CreateEngine(MCharData data)
        {
            MBattleEngine engine = new MBattleEngine();
            MChar left = MCharLoader.SpawnChar(data, 1);
            MChar right = MCharLoader.SpawnChar(data, 2);
            left.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.FromInt(-35), Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            right.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.FromInt(35), Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            right.Facing = -Lockstep.Math.FFloat.One;
            engine.Add(left, data);
            engine.Add(right, data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        static MInput[] InputsFor(int frame)
        {
            int phase = frame % 80;
            MInput left = MInput.None;
            if (phase < 8) { left = MInput.Down; }
            else if (phase < 12) { left = MInput.Down | MInput.Right; }
            else if (phase < 16) { left = MInput.Right; }
            else if (phase < 20) { left = MInput.Right | MInput.A; }
            else if (phase < 24) { left = MInput.X; }
            else if (phase < 28) { left = MInput.Y; }
            else if (phase < 32) { left = MInput.Z; }
            else if (phase < 36) { left = MInput.B; }
            else if (phase < 40) { left = MInput.C; }
            else if (phase < 48) { left = MInput.Left; }
            else if (phase < 52) { left = MInput.Down | MInput.Left; }
            else if (phase < 56) { left = MInput.Left | MInput.X; }
            else if (phase < 60) { left = MInput.Up; }
            else if (phase < 64) { left = MInput.S; }

            MInput right = Mirror(left);
            return new[] { left, right };
        }

        static MInput Mirror(MInput input)
        {
            bool left = (input & MInput.Left) != 0;
            bool right = (input & MInput.Right) != 0;
            input &= ~(MInput.Left | MInput.Right);
            if (left) { input |= MInput.Right; }
            if (right) { input |= MInput.Left; }
            return input;
        }
    }
}
