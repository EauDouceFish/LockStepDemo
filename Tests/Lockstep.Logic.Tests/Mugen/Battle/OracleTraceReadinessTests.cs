using System;
using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Command;
using Lockstep.Tests.Mugen;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Battle
{
    [TestFixture]
    public sealed class OracleTraceReadinessTests
    {
        [Test]
        public void IkemenOracleReference_IsPresentAndToolchainStatusIsExplicit()
        {
            string reference = Path.Combine(TestAssets.MugenSourceDir(), "_reference", "Ikemen-GO");
            Assert.That(Directory.Exists(reference), Is.True, "Ikemen reference checkout is required.");
            Assert.That(File.Exists(Path.Combine(reference, "go.mod")), Is.True, "Ikemen go.mod is required.");

            bool hasGo = FindExecutableOnPath("go.exe") || FindExecutableOnPath("go");
            TestContext.Progress.WriteLine("ikemen-reference=" + reference);
            TestContext.Progress.WriteLine("go-toolchain=" + (hasGo ? "available" : "missing"));
            if (!hasGo)
            {
                Assert.Pass("Ikemen reference exists; Go toolchain is unavailable, so headless Oracle build is blocked.");
            }
        }

        [Test]
        public void CSharpTraceRunner_ReplaysSameInputToSameFrames()
        {
            MCharData data = MugenCharacterPackageTestLoader.Load(TestAssets.KfmDir());
            MCommandDef command = FirstCommand(data, "x");
            List<MInput> inputs = MCommandInputSynthesizer.BuildSequence(command, facingRight: true);
            for (int i = 0; i < 20; i++)
            {
                inputs.Add(MInput.None);
            }

            List<MBattleTraceFrame> first = CaptureTrace(data, inputs);
            List<MBattleTraceFrame> second = CaptureTrace(data, inputs);
            List<MTraceDifference> differences = MBattleTraceComparer.Compare(first, second, MTraceTolerance.Exact());

            Assert.That(differences, Is.Empty);
            Assert.That(first.Count, Is.EqualTo(inputs.Count));
            Assert.That(first[0].Inputs[0].Bits, Is.EqualTo((int)inputs[0]));
        }

        static List<MBattleTraceFrame> CaptureTrace(MCharData data, IReadOnlyList<MInput> inputs)
        {
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 1), data);
            engine.Add(MCharLoader.SpawnChar(data, 2), data);
            engine.LinkPair();
            engine.StartRound();

            MBattleTraceRecorder recorder = new MBattleTraceRecorder();
            List<MBattleTraceFrame> frames = new List<MBattleTraceFrame>();
            for (int i = 0; i < inputs.Count; i++)
            {
                MInput input = inputs[i];
                engine.Tick(new[] { input, MInput.None });
                frames.Add(recorder.CapturePostTick(engine, i, new[] { input, MInput.None }));
            }
            return frames;
        }

        static MCommandDef FirstCommand(MCharData data, string name)
        {
            for (int i = 0; i < data.Commands.Count; i++)
            {
                if (data.Commands[i].Name == name)
                {
                    return data.Commands[i];
                }
            }
            Assert.Fail("Missing command " + name);
            return null;
        }

        static bool FindExecutableOnPath(string name)
        {
            string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] directories = path.Split(Path.PathSeparator);
            for (int i = 0; i < directories.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(directories[i]))
                {
                    continue;
                }
                string candidate = Path.Combine(directories[i], name);
                if (File.Exists(candidate))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
