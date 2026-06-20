using System.Collections.Generic;
using System.IO;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Battle
{
    [TestFixture]
    public sealed class DownloadedCharacterRunLogTests
    {
        const int MaxFrames = 180;

        static IEnumerable<TestCaseData> DownloadedCharacters()
        {
            yield return Case("CodeFuMan", "CodeFuMan", "MIT-style license in repository");
            yield return Case("DGBlossom", "dgblossom-mugen", "LICENSE.md in repository");
            yield return Case("DGButtercup", "dgbuttercup-mugen", "LICENSE.md in repository");
            yield return Case("NarutoHokage", "NarutoHokage", "public repository; no explicit license found");
            yield return Case("Invisible", "invisible-character", "public repository; no explicit license found");
        }

        [TestCaseSource(nameof(DownloadedCharacters))]
        public void DownloadedCharacter_RunsShortTimeOverRound_WithReplayVerifiedRunLog(
            string name,
            string directory,
            string licenseNote)
        {
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("Downloaded MUGEN character is missing: " + directory);
            }
            if (!Directory.Exists(TestAssets.KfmDir()))
            {
                Assert.Ignore("KFM opponent is missing.");
            }

            MCharData downloaded = MugenCharacterPackageTestLoader.Load(directory);
            MCharData kfm = MugenCharacterPackageTestLoader.Load(TestAssets.KfmDir());
            RunResult recorded = RunRound(downloaded, kfm, name);

            RunResult replay = CreateRound(downloaded, kfm);
            MBattleRunLogVerification verification = MBattleRunLogVerifier.Verify(
                inputs => replay.Round.Tick(inputs),
                () => replay.Engine.ComputeHash(),
                recorded.Log);

            Assert.Multiple(() =>
            {
                Assert.That(recorded.Log.Frames.Count, Is.GreaterThan(0), name + ": no frames captured");
                Assert.That(recorded.Round.BoutComplete || recorded.Round.MatchOver, Is.True,
                    name + ": scripted round did not finish");
                Assert.That(recorded.Log.Players[0].Uid, Does.StartWith("local-test-"));
                Assert.That(recorded.Log.Players[1].Agent, Is.EqualTo("ai-script"));
                Assert.That(recorded.Log.InputChecksumHex, Is.Not.EqualTo("0000000000000000"));
                Assert.That(recorded.Log.HashChecksumHex, Is.Not.EqualTo("0000000000000000"));
                Assert.That(verification.Success, Is.True, verification.Message);
            });

            TestContext.Progress.WriteLine(name + ": frames=" + recorded.Log.Frames.Count +
                " finalHash=" + recorded.Log.FinalHashHex +
                " inputChecksum=" + recorded.Log.InputChecksumHex +
                " license=" + licenseNote);
        }

        static TestCaseData Case(string name, string folder, string licenseNote)
        {
            string directory = Path.Combine(TestAssets.MugenSourceDir(), "_downloads", folder);
            return new TestCaseData(name, directory, licenseNote).SetName(
                "DownloadedCharacter_" + name + "_ShortTimeOverRunLogReplay");
        }

        static RunResult RunRound(MCharData downloaded, MCharData opponent, string name)
        {
            RunResult result = CreateRound(downloaded, opponent);
            MBattleRunLogRecorder recorder = new MBattleRunLogRecorder(
                MBattleRunMode.LocalTest, "downloaded-" + name, playerCount: 2);
            recorder.SetPlayer(0, "local-test-" + name, "script", downloaded.Name);
            recorder.SetPlayer(1, "ai-kfm", "ai-script", opponent.Name);

            for (int frame = 0; frame < MaxFrames && !result.Round.BoutComplete && !result.Round.MatchOver; frame++)
            {
                MInput[] inputs = InputsFor(frame);
                result.Round.Tick(inputs);
                recorder.CaptureFrame(frame, result.Engine, inputs);
            }
            result.Log = recorder.Complete("scripted-round-end");
            return result;
        }

        static RunResult CreateRound(MCharData downloaded, MCharData opponent)
        {
            MBattleEngine engine = new MBattleEngine();
            engine.Stage.SetSymmetric(160);
            MChar p0 = MCharLoader.SpawnChar(downloaded, 0);
            MChar p1 = MCharLoader.SpawnChar(opponent, 1);
            p0.Pos = new FVector3(FFloat.FromInt(-35), FFloat.Zero, FFloat.Zero);
            p1.Pos = new FVector3(FFloat.FromInt(35), FFloat.Zero, FFloat.Zero);
            p1.Facing = -FFloat.One;
            engine.Add(p0, downloaded);
            engine.Add(p1, opponent);
            engine.LinkPair();
            engine.StartRound();

            MRoundSystem round = new MRoundSystem(engine)
            {
                RoundTime = 90,
                OverHitTime = 1,
                OverWaitTime = 2,
                WinPoseTime = 2,
                OverReadyWaitTime = 0,
                SingleRound = true,
            };
            round.ForceFight();
            return new RunResult { Engine = engine, Round = round };
        }

        static MInput[] InputsFor(int frame)
        {
            int phase = frame % 72;
            MInput p0 = MInput.None;
            if (phase < 18) { p0 = MInput.Right; }
            else if (phase < 24) { p0 = MInput.X; }
            else if (phase < 30) { p0 = MInput.A; }
            else if (phase < 38) { p0 = MInput.Down | MInput.Right; }
            else if (phase < 44) { p0 = MInput.Y; }
            else if (phase < 52) { p0 = MInput.Left; }
            else if (phase < 58) { p0 = MInput.Up; }

            MInput p1 = MInput.None;
            if (phase >= 12 && phase < 30) { p1 = MInput.Left; }
            else if (phase >= 30 && phase < 36) { p1 = MInput.X; }
            else if (phase >= 54 && phase < 60) { p1 = MInput.B; }
            return new[] { p0, p1 };
        }

        sealed class RunResult
        {
            public MBattleEngine Engine;
            public MRoundSystem Round;
            public MBattleRunLog Log;
        }
    }
}
