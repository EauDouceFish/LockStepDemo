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
    /// <summary>
    /// R-HITDEF chunk-3 Part C：fall.damage + HitFallDamage 控制器。
    /// 受击当帧 ghv.FallDamage/FallKill 由 HitDef 写入（char.go:10842）；落地状态调用 HitFallDamage 控制器，
    /// 在 movetype H 下按 finalDefense 缩放扣血、fall.kill 决定可否致死、应用后清零（char.go:9109）。
    /// </summary>
    [TestFixture]
    public sealed class HitFallDamageTests
    {
        static FFloat F(int v) => FFloat.FromInt(v);

        [Test]
        public void Controller_AppliesFallDamage_InHitState()
        {
            MChar c = new MChar { Life = 1000, LifeMax = 1000, MoveType = 2 };
            c.Ghv.FallDamage = 50;
            c.Ghv.FallKill = true;
            new HitFallDamageController().Run(c);
            Assert.That(c.Life, Is.EqualTo(950), "受击态落地扣 fall.damage");
            Assert.That(c.Ghv.FallDamage, Is.EqualTo(0), "应用后清零");
        }

        [Test]
        public void Controller_NoOp_WhenNotInHitState()
        {
            MChar c = new MChar { Life = 1000, LifeMax = 1000, MoveType = 0 };
            c.Ghv.FallDamage = 50;
            new HitFallDamageController().Run(c);
            Assert.That(c.Life, Is.EqualTo(1000), "非受击态不结算");
            Assert.That(c.Ghv.FallDamage, Is.EqualTo(50), "保留待落地");
        }

        [Test]
        public void Controller_ScalesByFinalDefense()
        {
            MChar c = new MChar { Life = 1000, LifeMax = 1000, MoveType = 2, Constants = new MConstants { Defence = 200 } };
            c.Ghv.FallDamage = 50;
            new HitFallDamageController().Run(c);
            Assert.That(c.Life, Is.EqualTo(975), "defence=200 → fall.damage 半伤 (50/2=25)");
        }

        [Test]
        public void Controller_FallKillFalse_FloorsAtOne()
        {
            MChar c = new MChar { Life = 30, LifeMax = 1000, MoveType = 2 };
            c.Ghv.FallDamage = 100;
            c.Ghv.FallKill = false;
            new HitFallDamageController().Run(c);
            Assert.That(c.Life, Is.EqualTo(1), "fall.kill=0：落地伤害不致死，保底剩 1");
        }

        [Test]
        public void Hit_WritesFallDamageToGhv()
        {
            MChar atk = new MChar
            {
                Id = 1, Facing = FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(0), F(0), F(0)), Clsn1 = new[] { new MClsnBox(F(10), F(-40), F(30), F(0)) },
            };
            MChar def = new MChar
            {
                Id = 2, Facing = -FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(20), F(0), F(0)), Clsn2 = new[] { new MClsnBox(F(-10), F(-40), F(10), F(0)) },
            };
            atk.HitDef = new MHitDef
            {
                HitHigh = true, HitDamage = 40, P1PauseTime = 8, P2PauseTime = 8, GroundHitTime = 14,
                GroundVelX = F(-4), AnimType = MReaction.Medium, FallDamage = 70, FallKill = false, Active = true,
            };
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Ghv.FallDamage, Is.EqualTo(70), "命中写入 ghv.FallDamage");
            Assert.IsFalse(def.Ghv.FallKill, "命中写入 ghv.FallKill");
        }

        [Test]
        public void Cns_ParsesFallDamage_AndController()
        {
            string cns =
                "[Statedef 200]\ntype = S\nmovetype = A\n" +
                "[State 200, hit]\ntype = HitDef\ntrigger1 = 1\nattr = S, NA\ndamage = 40\nfall.damage = 60\n" +
                "[State 200, fd]\ntype = HitFallDamage\ntrigger1 = 1\n";
            Dictionary<int, MStateDef> states = MugenCnsParser.Parse(cns);
            MChar atk = new MChar { StateNo = 200, StateType = 1 };
            new MStateMachine().RunFrame(atk, states);
            Assert.That(atk.HitDef.FallDamage, Is.EqualTo(60), "解析 fall.damage");
            // HitFallDamage 控制器已实例化（非 NullController）
            Assert.IsTrue(states[200].Controllers.Exists(ctrl => ctrl is HitFallDamageController),
                "type=HitFallDamage 解析为 HitFallDamageController");
        }
    }
}
