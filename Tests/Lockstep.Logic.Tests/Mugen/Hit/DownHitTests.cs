using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;

namespace Lockstep.Tests.Mugen
{
    /// <summary>
    /// R-HITDEF chunk-3 Part D：down.*（击打倒地对手 statetype=L）。
    /// 贴地倒地命中用 down.velocity/down.hittime；down.bounce=0 且有 Y 速度则落地不反弹（fall.yvel 归零）。
    /// down.velocity 默认随 air.velocity，down.hittime 默认 20（char.go:892/724/10859-10871）。
    /// </summary>
    [TestFixture]
    public sealed class DownHitTests
    {
        static FFloat F(int v) => FFloat.FromInt(v);
        static MClsnBox Box(int x1, int y1, int x2, int y2) => new MClsnBox(F(x1), F(y1), F(x2), F(y2));

        static (MChar atk, MChar def) DownedPair()
        {
            MChar atk = new MChar
            {
                Id = 1, Facing = FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(0), F(0), F(0)), Clsn1 = new[] { Box(10, -40, 30, 0) },
            };
            MChar def = new MChar
            {
                Id = 2, Facing = -FFloat.One, StateType = 8, Life = 1000, LifeMax = 1000,   // ST_L 倒地
                Pos = new FVector3(F(20), F(0), F(0)), Clsn2 = new[] { Box(-10, -40, 10, 0) },
            };
            return (atk, def);
        }

        static MHitDef DownHit()
        {
            return new MHitDef
            {
                HitDown = true,   // 必须含 D 才能击中倒地者
                HitDamage = 30, P1PauseTime = 8, P2PauseTime = 8,
                AirVelX = F(-2), AirVelY = F(-3),
                DownVelX = F(-5), DownVelY = F(2), DownHitTime = 18, DownBounce = false,
                FallYVel = F(-7), AnimType = MReaction.Hard, Active = true,
            };
        }

        [Test]
        public void DownedOnGround_UsesDownVelocityAndHitTime()
        {
            (MChar atk, MChar def) = DownedPair();
            atk.HitDef = DownHit();
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Vel.X.Raw, Is.EqualTo(F(-5).Raw), "贴地倒地用 down.velocity X");
            Assert.That(def.Vel.Y.Raw, Is.EqualTo(F(2).Raw), "down.velocity Y");
            Assert.That(def.Ghv.HitTime, Is.EqualTo(18), "down.hittime");
            Assert.That(def.Ghv.CtrlTime, Is.EqualTo(18), "down.hittime 也作 ctrltime");
            Assert.That(def.PendingStateNo, Is.EqualTo(5080), "倒地命中路由 5080");
        }

        [Test]
        public void DownBounceFalse_ZeroesFallYVel()
        {
            (MChar atk, MChar def) = DownedPair();
            atk.HitDef = DownHit();   // DownBounce=false, down vy=2 != 0
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Ghv.FallYVel.Raw, Is.EqualTo(FFloat.Zero.Raw), "down.bounce=0 → 落地不反弹");
        }

        [Test]
        public void DownBounceTrue_KeepsFallYVel()
        {
            (MChar atk, MChar def) = DownedPair();
            MHitDef hd = DownHit();
            hd.DownBounce = true;
            atk.HitDef = hd;
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Ghv.FallYVel.Raw, Is.EqualTo(F(-7).Raw), "down.bounce=1 → 保留 fall.yvel 反弹");
        }

        [Test]
        public void Cns_DownVelocity_DefaultsToAirVelocity()
        {
            MHitDef hd = ParseHitDef("attr = S, NA\ndamage = 30\nair.velocity = -2, -6\n");
            Assert.That(hd.DownVelX.Raw, Is.EqualTo(F(-2).Raw), "down.velocity 未写 → 随 air.velocity X");
            Assert.That(hd.DownVelY.Raw, Is.EqualTo(F(-6).Raw), "随 air.velocity Y");
            Assert.That(hd.DownHitTime, Is.EqualTo(20), "down.hittime 默认 20");
        }

        [Test]
        public void Cns_ParsesDownFields()
        {
            MHitDef hd = ParseHitDef("attr = S, NA\ndamage = 30\ndown.velocity = -8, 3\ndown.hittime = 25\ndown.bounce = 1\n");
            Assert.That(hd.DownVelX.Raw, Is.EqualTo(F(-8).Raw));
            Assert.That(hd.DownVelY.Raw, Is.EqualTo(F(3).Raw));
            Assert.That(hd.DownHitTime, Is.EqualTo(25));
            Assert.IsTrue(hd.DownBounce);
        }

        static MHitDef ParseHitDef(string extra)
        {
            string cns =
                "[Statedef 200]\ntype = S\nmovetype = A\n" +
                "[State 200, hit]\ntype = HitDef\ntrigger1 = 1\n" + extra;
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            MChar atk = new MChar { StateNo = 200, StateType = 1 };
            new MStateMachine().RunFrame(atk, states);
            return atk.HitDef;
        }
    }
}
