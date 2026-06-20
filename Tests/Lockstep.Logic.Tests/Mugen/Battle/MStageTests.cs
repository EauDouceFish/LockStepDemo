using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>R-STAGE-minimal：决斗场左右边界把角色 X 夹回场内；关闭时不夹（保旧"虚空"行为）。</summary>
    [TestFixture]
    public sealed class MStageTests
    {
        const string Cns = "[Statedef 0]\ntype=S\nphysics=S\nanim=0\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n";
        static readonly List<MInput> NoInput = new List<MInput> { MInput.None };

        static MBattleEngine OneCharEngine()
        {
            MCharData data = MCharLoader.Load(new[] { Cns }, Cns, null, Air, null, "Stage");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0), data);
            engine.Chars[0].Id = 1;
            engine.Chars[0].Life = engine.Chars[0].LifeMax = 1000;
            return engine;
        }

        [Test]
        public void DisabledByDefault_NoClamp()
        {
            MBattleEngine engine = OneCharEngine();
            engine.Chars[0].Pos = new FVector3(FFloat.FromInt(500), FFloat.Zero, FFloat.Zero);
            engine.Tick(NoInput);
            Assert.That(engine.Chars[0].Pos.X.ToInt(), Is.EqualTo(500), "默认不启用边界 → 不夹取");
        }

        [Test]
        public void Enabled_ClampsBeyondRightBound()
        {
            MBattleEngine engine = OneCharEngine();
            engine.Stage.SetSymmetric(200);
            engine.Chars[0].Pos = new FVector3(FFloat.FromInt(500), FFloat.Zero, FFloat.Zero);
            engine.Tick(NoInput);
            Assert.That(engine.Chars[0].Pos.X.ToInt(), Is.EqualTo(200), "超右边界夹回 +200");
        }

        [Test]
        public void Enabled_ClampsBeyondLeftBound()
        {
            MBattleEngine engine = OneCharEngine();
            engine.Stage.SetSymmetric(200);
            engine.Chars[0].Pos = new FVector3(FFloat.FromInt(-777), FFloat.Zero, FFloat.Zero);
            engine.Tick(NoInput);
            Assert.That(engine.Chars[0].Pos.X.ToInt(), Is.EqualTo(-200), "超左边界夹回 -200");
        }

        [Test]
        public void WallStop_ZeroesHorizontalVelocity()
        {
            MBattleEngine engine = OneCharEngine();
            engine.Stage.SetSymmetric(200);
            MChar c = engine.Chars[0];
            c.Pos = new FVector3(FFloat.FromInt(199), FFloat.Zero, FFloat.Zero);
            c.Vel = new FVector3(FFloat.FromInt(50), FFloat.Zero, FFloat.Zero);   // 朝右冲向墙
            engine.Tick(NoInput);
            Assert.That(c.Pos.X.ToInt(), Is.EqualTo(200), "撞墙夹在边界");
            Assert.That(c.Vel.X.ToInt(), Is.EqualTo(0), "撞墙横向速度归零");
        }

        [Test]
        public void WithinBounds_NotClamped()
        {
            MBattleEngine engine = OneCharEngine();
            engine.Stage.SetSymmetric(200);
            engine.Chars[0].Pos = new FVector3(FFloat.FromInt(50), FFloat.Zero, FFloat.Zero);
            engine.Tick(NoInput);
            Assert.That(engine.Chars[0].Pos.X.ToInt(), Is.EqualTo(50), "场内不动");
        }

        [Test]
        public void TwoPlayersAtCorner_DoNotCrossRightBound()
        {
            MCharData data = MCharLoader.Load(new[] { Cns }, Cns, null, Air, null, "Stage");
            MBattleEngine engine = new MBattleEngine();
            MChar p0 = MCharLoader.SpawnChar(data, 0, startStateNo: 0, startAnimNo: 0);
            MChar p1 = MCharLoader.SpawnChar(data, 1, startStateNo: 0, startAnimNo: 0);
            p0.Pos = new FVector3(FFloat.FromInt(190), FFloat.Zero, FFloat.Zero);
            p1.Pos = new FVector3(FFloat.FromInt(198), FFloat.Zero, FFloat.Zero);
            p0.Vel = new FVector3(FFloat.FromInt(30), FFloat.Zero, FFloat.Zero);
            p1.Vel = new FVector3(FFloat.FromInt(30), FFloat.Zero, FFloat.Zero);
            engine.Add(p0, data);
            engine.Add(p1, data);
            engine.LinkPair();
            engine.Stage.SetSymmetric(200);

            engine.Tick(new[] { MInput.None, MInput.None });

            Assert.That(p0.Pos.X, Is.LessThanOrEqualTo(FFloat.FromInt(200)));
            Assert.That(p1.Pos.X, Is.EqualTo(FFloat.FromInt(200)));
            Assert.That(p1.Vel.X, Is.EqualTo(FFloat.Zero));
        }
    }
}
