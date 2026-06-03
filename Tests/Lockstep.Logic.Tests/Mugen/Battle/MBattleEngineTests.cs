using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.Battle;
using Lockstep.Tests;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>M9.1：.def 解析 + 角色装配 + 引擎每帧 pipeline（命令→状态机→物理→动画→命中）。</summary>
    [TestFixture]
    public sealed class MBattleEngineTests
    {
        // ───────── .def [Files] 解析 ─────────
        [Test]
        public void DefParser_ReadsFilesSection()
        {
            string def = "[Info]\nname = \"Foo\"\n[Files]\ncmd = foo.cmd\ncns = foo.cns\nst = foo.cns\nstcommon = common1.cns\nsprite = foo.sff\nanim = foo.air\n";
            MDefFiles files = MDefParser.ParseFiles(def);
            Assert.That(files.Cmd, Is.EqualTo("foo.cmd"));
            Assert.That(files.Cns, Is.EqualTo("foo.cns"));
            Assert.That(files.St, Does.Contain("foo.cns"));
            Assert.That(files.StCommon, Is.EqualTo("common1.cns"));
            Assert.That(files.Anim, Is.EqualTo("foo.air"));
        }

        [Test]
        public void DefParser_CollectsMultipleStateFiles()
        {
            string def = "[Files]\nst = a.cns\nst1 = b.cns\nst2 = c.cns\n";
            MDefFiles files = MDefParser.ParseFiles(def);
            Assert.That(files.St, Is.EqualTo(new[] { "a.cns", "b.cns", "c.cns" }));
        }

        // ───────── 合成角色：装配 + 推进 ─────────
        const string SynthCns =
            "[Statedef 0]\ntype = S\nanim = 0\n\n" +
            "[State 0, walk]\ntype = VelSet\ntrigger1 = 1\nx = 2\n";

        const string SynthAir =
            "[Begin Action 0]\nClsn2: 1\n Clsn2[0] = -10,-80, 10, 0\n0,0, 0,0, 4\n0,1, 0,0, 4\n";

        [Test]
        public void Loader_BuildsStatesAndAnims()
        {
            MCharData data = MCharLoader.Load(new[] { SynthCns }, SynthCns, null, SynthAir, null, "Synth");
            Assert.That(data.States.ContainsKey(0), Is.True);
            Assert.That(data.Anims.ContainsKey(0), Is.True);
            Assert.That(data.Anims[0].Frames.Length, Is.EqualTo(2));
        }

        [Test]
        public void Engine_TicksSingleCharAndAdvancesAnim()
        {
            MCharData data = MCharLoader.Load(new[] { SynthCns }, SynthCns, null, SynthAir, null, "Synth");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0), data);

            List<MInput> inputs = new List<MInput> { MInput.None };
            int startElemSum = engine.Chars[0].AnimCurTime;
            for (int f = 0; f < 10; f++)
            {
                engine.Tick(inputs);
            }
            Assert.That(engine.Chars[0].AnimCurTime, Is.GreaterThan(startElemSum), "动画随帧推进");
            // VelSet x=2 + 最小积分 → 朝右移动（facing=+1）
            Assert.That(engine.Chars[0].Pos.X.Raw, Is.GreaterThan(0), "VelSet 经物理积分使角色右移");
        }

        [Test]
        public void Engine_IsDeterministic_SameInputsSameHash()
        {
            MCharData data = MCharLoader.Load(new[] { SynthCns }, SynthCns, null, SynthAir, null, "Synth");
            ulong RunOnce()
            {
                MBattleEngine e = new MBattleEngine();
                e.Add(MCharLoader.SpawnChar(data, 0), data);
                List<MInput> inputs = new List<MInput> { MInput.None };
                for (int f = 0; f < 20; f++)
                {
                    e.Tick(inputs);
                }
                return e.ComputeHash();
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()), "相同输入逐帧确定性一致");
        }

        // ───────── 真实 KFM：加载 + 连跑不崩 ─────────
        [Test]
        public void RealKfm_LoadsAndTicks()
        {
            string dir = TestAssets.KfmDir();
            string cns = Path.Combine(dir, "kfm.cns");
            string cmd = Path.Combine(dir, "kfm.cmd");
            string air = Path.Combine(dir, "kfm.air");
            if (!File.Exists(cns) || !File.Exists(air))
            {
                Assert.Ignore("KFM 素材缺失（../MugenSource/kfm），跳过真实加载测试。");
            }

            MCharData data = MCharLoader.Load(
                new[] { File.ReadAllText(cns) }, File.ReadAllText(cns), null,
                File.ReadAllText(air), File.Exists(cmd) ? File.ReadAllText(cmd) : null, "kfm");

            Assert.That(data.States.Count, Is.GreaterThan(0), "kfm.cns 应解析出状态");
            Assert.That(data.Anims.ContainsKey(0), Is.True, "kfm.air 应含动画 0(站立)");
            Assert.That(data.Commands.Count, Is.GreaterThan(0), "kfm.cmd 应解析出命令");

            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            List<MInput> inputs = new List<MInput> { MInput.None };
            Assert.DoesNotThrow(() =>
            {
                for (int f = 0; f < 60; f++)
                {
                    engine.Tick(inputs);
                }
            }, "真实 KFM 连跑 60 帧不崩");
            Assert.That(engine.Chars[0].AnimCurTime, Is.GreaterThan(0), "KFM 动画 0 随帧推进");
            Assert.That(engine.Chars[0].Alive, Is.True, "未无故自杀");
        }
    }
}
