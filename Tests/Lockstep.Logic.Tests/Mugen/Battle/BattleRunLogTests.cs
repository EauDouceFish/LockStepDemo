using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen.Battle
{
    [TestFixture]
    public sealed class BattleRunLogTests
    {
        [Test]
        public void Recorder_CapturesLocalAiFrameInputsCommandsAndHashes()
        {
            MBattleEngine engine = CreateEngine();
            MBattleRunLogRecorder recorder = new MBattleRunLogRecorder(
                MBattleRunMode.LocalAi, "kfm-local-ai", playerCount: 2);
            recorder.SetPlayer(0, "local-test-p0", "local", "kfm");
            recorder.SetPlayer(1, "ai-demo-p1", "ai", "kfm");

            MInput[] script =
            {
                MInput.None,
                MInput.X,
                MInput.None,
                MInput.Right,
            };

            for (int frame = 0; frame < script.Length; frame++)
            {
                MInput[] inputs = { script[frame], MInput.None };
                engine.Tick(inputs);
                recorder.CaptureFrame(frame, engine, inputs);
            }
            MBattleRunLog log = recorder.Complete("script-end");

            Assert.Multiple(() =>
            {
                Assert.That(log.Mode, Is.EqualTo(MBattleRunMode.LocalAi));
                Assert.That(log.IsLocalTest, Is.True);
                Assert.That(log.Players[0].Uid, Is.EqualTo("local-test-p0"));
                Assert.That(log.Players[1].Agent, Is.EqualTo("ai"));
                Assert.That(log.Frames.Count, Is.EqualTo(script.Length));
                Assert.That(log.Frames[1].Inputs[0], Is.EqualTo((int)MInput.X));
                Assert.That(log.Frames[1].ActiveCommands[0], Does.Contain("x"));
                Assert.That(log.Frames[1].HashHex, Is.Not.Empty);
                Assert.That(log.InputChecksumHex, Is.Not.EqualTo("0000000000000000"));
                Assert.That(log.HashChecksumHex, Is.Not.EqualTo("0000000000000000"));
                Assert.That(log.Completed, Is.True);
            });
        }

        [Test]
        public void Verifier_ReplaysInputScriptAndMatchesRecordedFrameHashes()
        {
            MBattleRunLog log = CreateRecordedLog();

            MBattleRunLogVerification ok = MBattleRunLogVerifier.Verify(CreateEngine(), log);
            Assert.That(ok.Success, Is.True, ok.Message);

            log.Frames[2].Inputs[0] = (int)MInput.A;
            MBattleRunLogVerification bad = MBattleRunLogVerifier.Verify(CreateEngine(), log);
            Assert.That(bad.Success, Is.False);
            Assert.That(bad.Message, Does.Contain("frame 2"));
        }

        [Test]
        public void Json_IncludesControlStateForLossOfControlDebugging()
        {
            MBattleRunLog log = CreateRecordedLog();
            string json = MBattleRunLogJson.ToJson(log);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("\"stateNo\""));
                Assert.That(json, Does.Contain("\"animNo\""));
                Assert.That(json, Does.Contain("\"animElemNo\""));
                Assert.That(json, Does.Contain("\"time\""));
                Assert.That(json, Does.Contain("\"ctrl\""));
                Assert.That(json, Does.Contain("\"keyCtrl\""));
                Assert.That(json, Does.Contain("\"hitstop\""));
                Assert.That(json, Does.Contain("\"pauseBool\""));
                Assert.That(json, Does.Contain("\"playerPushEnabled\""));
                Assert.That(json, Does.Contain("\"activeCommands\""));
            });
        }

        [Test]
        public void Verifier_RejectsIncompleteFrameGapsAndChecksumMismatches()
        {
            MBattleRunLog incomplete = CreateRecordedLog();
            incomplete.Completed = false;
            Assert.That(MBattleRunLogVerifier.Verify(CreateEngine(), incomplete).Message, Does.Contain("not completed"));

            MBattleRunLog frameGap = CreateRecordedLog();
            frameGap.Frames[2].Frame = 99;
            Assert.That(MBattleRunLogVerifier.Verify(CreateEngine(), frameGap).Message, Does.Contain("frame sequence"));

            MBattleRunLog inputChecksum = CreateRecordedLog();
            inputChecksum.InputChecksumHex = "0000000000000000";
            Assert.That(MBattleRunLogVerifier.Verify(CreateEngine(), inputChecksum).Message, Does.Contain("input checksum"));

            MBattleRunLog hashChecksum = CreateRecordedLog();
            hashChecksum.HashChecksumHex = "0000000000000000";
            Assert.That(MBattleRunLogVerifier.Verify(CreateEngine(), hashChecksum).Message, Does.Contain("hash checksum"));

            MBattleRunLog finalHash = CreateRecordedLog();
            finalHash.FinalHashHex = "0000000000000000";
            Assert.That(MBattleRunLogVerifier.Verify(CreateEngine(), finalHash).Message, Does.Contain("final hash"));
        }

        static void RunScript(MBattleEngine engine, MBattleRunLogRecorder recorder, IReadOnlyList<MInput[]> script)
        {
            for (int frame = 0; frame < script.Count; frame++)
            {
                engine.Tick(script[frame]);
                recorder.CaptureFrame(frame, engine, script[frame]);
            }
        }

        static MBattleRunLog CreateRecordedLog()
        {
            MBattleEngine recordedEngine = CreateEngine();
            MBattleRunLogRecorder recorder = new MBattleRunLogRecorder(
                MBattleRunMode.LocalTest, "kfm-replay", playerCount: 2);
            recorder.SetPlayer(0, "p0", "script", "kfm");
            recorder.SetPlayer(1, "p1", "idle", "kfm");

            MInput[][] script =
            {
                new[] { MInput.None, MInput.None },
                new[] { MInput.X, MInput.None },
                new[] { MInput.None, MInput.None },
                new[] { MInput.Right, MInput.None },
                new[] { MInput.None, MInput.None },
            };
            RunScript(recordedEngine, recorder, script);
            return recorder.Complete("script-end");
        }

        static MBattleEngine CreateEngine()
        {
            string directory = TestAssets.KfmDir();
            if (!Directory.Exists(directory))
            {
                Assert.Ignore("KFM test character is missing.");
            }
            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0), data);
            engine.Add(MCharLoader.SpawnChar(data, 1), data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }
    }
}
