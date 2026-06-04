using System.Collections.Generic;
using System.IO;
using Lockstep.Math;
using Lockstep.Mugen.Char;
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

        [Test]
        public void RealKfm_WithCommonStates_StandsAndPhysicsEngages()
        {
            string kfmDir = TestAssets.KfmDir();
            string common = TestAssets.Common1Cns();   // KFM 目录无 common1，借 Terrarian 的标准公共状态
            string cns = Path.Combine(kfmDir, "kfm.cns");
            string air = Path.Combine(kfmDir, "kfm.air");
            if (!File.Exists(cns) || !File.Exists(air) || !File.Exists(common))
            {
                Assert.Ignore("KFM/common1 素材缺失，跳过。");
            }

            MCharData data = MCharLoader.Load(
                new[] { File.ReadAllText(cns) }, File.ReadAllText(cns),
                File.ReadAllText(common), File.ReadAllText(air), null, "kfm");

            Assert.That(data.CommonStates.ContainsKey(0), Is.True, "公共状态含站立 Statedef 0");
            Assert.That(data.CommonStates.ContainsKey(20), Is.True, "公共状态含行走 Statedef 20");

            MChar kfm = MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0);
            Assert.That(kfm.Physics, Is.EqualTo(1), "入场应用公共 Statedef 0 头部 physics=S");
            Assert.That(kfm.StateType, Is.EqualTo(1), "type=S");
            Assert.That(kfm.Constants.StandFriction.Raw, Is.GreaterThan(0), "kfm.cns stand.friction 已载入");

            MBattleEngine engine = new MBattleEngine();
            engine.Add(kfm, data);
            List<MInput> inputs = new List<MInput> { MInput.None };
            Assert.DoesNotThrow(() =>
            {
                for (int f = 0; f < 60; f++)
                {
                    engine.Tick(inputs);
                }
            }, "KFM + 公共状态连跑 60 帧不崩");
            Assert.That(engine.Chars[0].Alive, Is.True);
        }

        // ───────── 模块 E：引擎硬编码基础动作端到端（真实 KFM 走/跳）─────────

        static MBattleEngine LoadKfmEngine()
        {
            string kfmDir = TestAssets.KfmDir();
            string common = TestAssets.Common1Cns();
            string cns = Path.Combine(kfmDir, "kfm.cns");
            string cmd = Path.Combine(kfmDir, "kfm.cmd");
            string air = Path.Combine(kfmDir, "kfm.air");
            if (!File.Exists(cns) || !File.Exists(air) || !File.Exists(common) || !File.Exists(cmd))
            {
                Assert.Ignore("KFM/common1 素材缺失，跳过。");
            }
            MCharData data = MCharLoader.Load(
                new[] { File.ReadAllText(cns) }, File.ReadAllText(cns),
                File.ReadAllText(common), File.ReadAllText(air), File.ReadAllText(cmd), "kfm");
            MChar kfm = MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0);
            MBattleEngine engine = new MBattleEngine();
            engine.Add(kfm, data);
            engine.LinkPair();
            engine.StartRound();   // 授予 ctrl/keyctrl，引擎硬编码键才生效
            return engine;
        }

        [Test]
        public void RealKfm_HoldForward_EntersWalkAndMovesForward()
        {
            MBattleEngine engine = LoadKfmEngine();
            MChar kfm = engine.Chars[0];
            bool sawWalk = false;
            List<MInput> inputs = new List<MInput> { MInput.Right };   // 面右持前进
            for (int f = 0; f < 30; f++)
            {
                engine.Tick(inputs);
                if (kfm.StateNo == 20) { sawWalk = true; }
            }
            Assert.That(sawWalk, Is.True, "持前进 → 引擎硬编码键进走路状态 20");
            Assert.That(kfm.Pos.X.Raw, Is.GreaterThan(0), "走路使 KFM 前移（pos.x>0）");
        }

        [Test]
        public void RealKfm_HoldUp_EntersJumpAndRises()
        {
            MBattleEngine engine = LoadKfmEngine();
            MChar kfm = engine.Chars[0];
            bool sawAirborne = false;   // 跳跃起始(40)经同帧重入直接转空中(50, type=A)，40 仅 1 帧不易观测，验空中态即可
            FFloat minY = FFloat.Zero;   // 记录最高点（y 最负）
            List<MInput> inputs = new List<MInput> { MInput.Up };
            for (int f = 0; f < 30; f++)
            {
                engine.Tick(inputs);
                if (kfm.StateType == 4) { sawAirborne = true; }   // ST_A
                if (kfm.Pos.Y < minY) { minY = kfm.Pos.Y; }
            }
            Assert.That(sawAirborne, Is.True, "持上 → 引擎硬编码键起跳进入空中态(type=A)");
            Assert.That(minY.Raw, Is.LessThan(0), "跳跃使 KFM 升空（pos.y<0，MUGEN 上为负）");
        }

        [Test]
        public void RealKfm_JumpArc_RisesThenLandsBackToGround()
        {
            MBattleEngine engine = LoadKfmEngine();
            MChar kfm = engine.Chars[0];
            // 起跳：按一下上（边沿）即可，之后松开，让重力把它带回地面。
            List<MInput> up = new List<MInput> { MInput.Up };
            List<MInput> none = new List<MInput> { MInput.None };
            for (int f = 0; f < 3; f++) { engine.Tick(up); }
            FFloat apex = FFloat.Zero;
            bool landed = false;
            for (int f = 0; f < 120; f++)   // 给足时间走完整段抛物线
            {
                engine.Tick(none);
                if (kfm.Pos.Y < apex) { apex = kfm.Pos.Y; }
                if (apex.Raw < 0 && kfm.StateType == 1 && kfm.Pos.Y.Raw <= 0)
                {
                    landed = true;   // 已升空过且回到地面站立类
                }
            }
            Assert.That(apex.Raw, Is.LessThan(0), "确实升空过");
            Assert.That(landed, Is.True, "落地：升空后回到地面站立态（pos.y 不再无限下落）");
            Assert.That(kfm.Pos.Y.Raw, Is.LessThanOrEqualTo(8), "落地后 y 收敛在地面附近（非无限穿地）");
        }

        [Test]
        public void RealKfm_WalkThenRelease_BrakesBackToStand()
        {
            MBattleEngine engine = LoadKfmEngine();
            MChar kfm = engine.Chars[0];
            List<MInput> fwd = new List<MInput> { MInput.Right };
            for (int f = 0; f < 8; f++) { engine.Tick(fwd); }
            Assert.That(kfm.StateNo, Is.EqualTo(20), "持前进时在走路态");
            List<MInput> none = new List<MInput> { MInput.None };
            for (int f = 0; f < 8; f++) { engine.Tick(none); }
            Assert.That(kfm.StateNo, Is.EqualTo(0), "松开 → 刹车回站立(0)");
        }

        [Test]
        public void RealKfm_ActionPipeline_IsDeterministic()
        {
            ulong RunOnce()
            {
                MBattleEngine engine = LoadKfmEngine();
                List<MInput> seq = new List<MInput> { MInput.Right };
                for (int f = 0; f < 40; f++)
                {
                    if (f == 15) { seq[0] = MInput.Up; }
                    if (f == 25) { seq[0] = MInput.None; }
                    engine.Tick(seq);
                }
                return engine.ComputeHash();
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()), "含硬编码动作的管线逐帧确定性一致");
        }
    }
}
