using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M7：HitDef 控制器 + Clsn 重叠 + 命中结算(伤害/击退/硬直/gethit 5000/GetHitVar/连击)。</summary>
    [TestFixture]
    public sealed class MHitSystemTests
    {
        static FFloat F(int v) => FFloat.FromInt(v);
        static MClsnBox Box(int x1, int y1, int x2, int y2) => new MClsnBox(F(x1), F(y1), F(x2), F(y2));

        // 攻方在 x=0 面右，攻击框 [10,30] 高度[-40,0]；守方在 x=20 面左，受击框 [-10,10] 高度[-40,0]，重叠。
        static (MChar atk, MChar def) Pair()
        {
            MChar atk = new MChar
            {
                Id = 1, Facing = FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(0), F(0), F(0)),
                Clsn1 = new[] { Box(10, -40, 30, 0) },
            };
            MChar def = new MChar
            {
                Id = 2, Facing = -FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(20), F(0), F(0)),
                Clsn2 = new[] { Box(-10, -40, 10, 0) },
            };
            return (atk, def);
        }

        static MHitDef BasicHitDef()
        {
            return new MHitDef
            {
                HitHigh = true, HitLow = true,
                HitDamage = 80,
                P1PauseTime = 8, P2PauseTime = 8,
                GroundHitTime = 14, GroundSlideTime = 5,
                GroundVelX = F(-4), GroundVelY = F(0),
                AnimType = MReaction.Medium,
            };
        }

        [Test]
        public void Clsn_Overlap_DetectsHit()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = BasicHitDef();
            atk.HitDef.Active = true;
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
        }

        [Test]
        public void NoOverlap_NoHit()
        {
            (MChar atk, MChar def) = Pair();
            def.Pos = new FVector3(F(500), F(0), F(0));   // 拉远，不重叠
            atk.HitDef = BasicHitDef();
            atk.HitDef.Active = true;
            Assert.IsFalse(MHitSystem.TryHit(atk, def));
        }

        [Test]
        public void Hit_AppliesDamageKnockbackHitstopState()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = BasicHitDef();
            atk.HitDef.Active = true;

            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(920), "扣 80 血");
            Assert.That(def.Hitstop, Is.EqualTo(8), "守方 hitstop");
            Assert.That(atk.Hitstop, Is.EqualTo(8), "攻方 hitstop");
            Assert.That(def.PendingStateNo, Is.EqualTo(5000), "守方进受击状态 5000");
            Assert.That(def.MoveType, Is.EqualTo(2), "守方 movetype=H");
            Assert.IsFalse(def.Ctrl, "守方失控");
            Assert.That(def.Vel.X.Raw, Is.EqualTo(F(-4).Raw), "击退速度");
            Assert.That(def.Facing.Raw, Is.EqualTo((-atk.Facing).Raw), "守方转向面对攻方");
            Assert.That(def.Ghv.HitTime, Is.EqualTo(14), "GetHitVar.HitTime");
            Assert.That(def.Ghv.Damage, Is.EqualTo(80));
            Assert.That(atk.MoveHit, Is.EqualTo(1), "攻方 movehit");
            Assert.That(atk.HitCount, Is.EqualTo(1));
        }

        [Test]
        public void SameMove_HitsTargetOnlyOnce()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = BasicHitDef();
            atk.HitDef.Active = true;
            Assert.IsTrue(MHitSystem.TryHit(atk, def), "首次命中");
            Assert.IsFalse(MHitSystem.TryHit(atk, def), "同招不再次命中(target 已登记)");
            Assert.That(def.Life, Is.EqualTo(920), "只扣一次");
        }

        [Test]
        public void HitFlag_RespectsDefenderStateType()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = BasicHitDef();
            atk.HitDef.HitLow = false;   // 不能击中蹲
            atk.HitDef.Active = true;
            def.StateType = 2;           // 守方蹲
            Assert.IsFalse(MHitSystem.TryHit(atk, def), "hitflag 不含 L，蹲不中");
        }

        [Test]
        public void HitDefController_ActivatesAndSetsMoveType()
        {
            MChar c = new MChar { StateType = 1, MoveType = 1 };
            HitDefController ctrl = new HitDefController { Template = BasicHitDef() };
            ctrl.Run(c);
            Assert.IsTrue(c.HitDef.Active);
            Assert.That(c.HitDef.HitDamage, Is.EqualTo(80));
            Assert.That(c.MoveType, Is.EqualTo(4), "激活 HitDef → movetype=A");
        }

        [Test]
        public void Cns_ParsesHitDef_EndToEnd()
        {
            string cns =
                "[Statedef 200]\ntype = S\nmovetype = A\n" +
                "[State 200, hit]\ntype = HitDef\ntrigger1 = 1\n" +
                "attr = S, NA\nhitflag = MAF\ndamage = 80, 10\npausetime = 8, 8\n" +
                "ground.velocity = -4, 0\nground.hittime = 14\nanimtype = Medium\np2stateno = 5001\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);

            MChar atk = new MChar { StateNo = 200, StateType = 1 };
            new MStateMachine().RunFrame(atk, states);
            Assert.IsTrue(atk.HitDef.Active, "CNS HitDef 控制器激活");
            Assert.That(atk.HitDef.HitDamage, Is.EqualTo(80));
            Assert.That(atk.HitDef.P2PauseTime, Is.EqualTo(8));
            Assert.That(atk.HitDef.GroundHitTime, Is.EqualTo(14));
            Assert.That(atk.HitDef.P2StateNo, Is.EqualTo(5001));
            Assert.IsTrue(atk.HitDef.HitAir, "hitflag MAF 含 A");
        }
    }
}
