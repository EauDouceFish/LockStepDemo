using System.Collections.Generic;
using System.IO;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Battle;
using Lockstep.Tests;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// R-ENT 切片 1：helper 实体生命周期（spawn 请求→DrainSpawns 造实体→自跑状态机→DestroySelf 移除）
    /// + ishelper/numhelper 触发器 + Clone/Hash 确定性。helper 用 owner 角色数据，Parent/Root/P2 接好。
    /// </summary>
    [TestFixture]
    public sealed class REntFoundationTests
    {
        static MBattleEngine TwoKfm()
        {
            string kfmDir = TestAssets.KfmDir();
            string common = TestAssets.Common1Cns();
            string cns = Path.Combine(kfmDir, "kfm.cns");
            string air = Path.Combine(kfmDir, "kfm.air");
            string cmd = Path.Combine(kfmDir, "kfm.cmd");
            if (!File.Exists(cns) || !File.Exists(air) || !File.Exists(common))
            {
                Assert.Ignore("KFM/common1 素材缺失，跳过。");
            }
            MCharData data = MCharLoader.Load(
                new[] { File.ReadAllText(cns) }, File.ReadAllText(cns),
                File.ReadAllText(common), File.ReadAllText(air),
                File.Exists(cmd) ? File.ReadAllText(cmd) : null, "kfm");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0), data);
            engine.Add(MCharLoader.SpawnChar(data, 1), data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        static List<MInput> NoInput()
        {
            return new List<MInput> { MInput.None, MInput.None };
        }

        [Test]
        public void RequestHelper_DrainCreatesHelperEntity()
        {
            MBattleEngine engine = TwoKfm();
            MChar owner = engine.Chars[0];
            owner.RequestHelper(stateNo: 0, helperType: 5, FFloat.FromInt(20), FFloat.Zero, facing: 1, keyCtrl: false);
            Assert.That(engine.Helpers.Count, Is.EqualTo(0), "请求入队，尚未造实体");

            engine.Tick(NoInput());   // DrainSpawns 造 helper
            Assert.That(engine.Helpers.Count, Is.EqualTo(1), "DrainSpawns 后 helper 实体存在");
            MChar helper = engine.Helpers[0];
            Assert.That(helper.IsHelper, Is.True);
            Assert.That(helper.HelperType, Is.EqualTo(5));
            Assert.That(ReferenceEquals(helper.Parent, owner), Is.True, "Parent = owner");
            Assert.That(helper.Id, Is.GreaterThanOrEqualTo(1000), "helper id 从 1000 起");
        }

        [Test]
        public void Helper_RunsItsOwnStateMachine()
        {
            MBattleEngine engine = TwoKfm();
            engine.Chars[0].RequestHelper(0, 1, FFloat.Zero, FFloat.Zero, 1, false);
            engine.Tick(NoInput());   // 造 helper（Time=0）
            MChar helper = engine.Helpers[0];
            int t0 = helper.Time;
            engine.Tick(NoInput());
            engine.Tick(NoInput());
            Assert.That(helper.Time, Is.GreaterThan(t0), "helper 自跑状态机（Time 推进）");
            Assert.That(helper.Alive, Is.True, "helper 未无故自杀");
        }

        [Test]
        public void NumHelper_IsHelper_Triggers()
        {
            MBattleEngine engine = TwoKfm();
            MChar owner = engine.Chars[0];
            MugenExprCompiler compiler = new MugenExprCompiler();
            BytecodeExp numhelper = compiler.Compile("numhelper");
            BytecodeExp ishelper = compiler.Compile("ishelper");

            Assert.That(numhelper.Run(owner).ToI(), Is.EqualTo(0), "初始无 helper");
            Assert.That(ishelper.Run(owner).ToI(), Is.EqualTo(0), "玩家不是 helper");

            owner.RequestHelper(0, 1, FFloat.Zero, FFloat.Zero, 1, false);
            engine.Tick(NoInput());
            Assert.That(numhelper.Run(owner).ToI(), Is.EqualTo(1), "造 helper 后 numhelper=1");
            Assert.That(ishelper.Run(engine.Helpers[0]).ToI(), Is.EqualTo(1), "helper 实体 ishelper=1");
        }

        [Test]
        public void DestroySelf_RemovesHelperNextFrame()
        {
            MBattleEngine engine = TwoKfm();
            engine.Chars[0].RequestHelper(0, 1, FFloat.Zero, FFloat.Zero, 1, false);
            engine.Tick(NoInput());
            Assert.That(engine.Helpers.Count, Is.EqualTo(1));
            engine.Helpers[0].Destroyed = true;   // 模拟 DestroySelf
            engine.Tick(NoInput());
            Assert.That(engine.Helpers.Count, Is.EqualTo(0), "标记 Destroyed 的 helper 帧末移除");
        }

        [Test]
        public void HelperLifecycle_IsDeterministic()
        {
            ulong RunOnce()
            {
                MBattleEngine engine = TwoKfm();
                engine.Chars[0].RequestHelper(0, 7, FFloat.FromInt(10), FFloat.Zero, 1, false);
                for (int f = 0; f < 12; f++)
                {
                    if (f == 8 && engine.Helpers.Count > 0) { engine.Helpers[0].Destroyed = true; }
                    engine.Tick(NoInput());
                }
                return engine.ComputeHash();
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()), "helper 生命周期逐帧确定性一致");
        }
    }
}
