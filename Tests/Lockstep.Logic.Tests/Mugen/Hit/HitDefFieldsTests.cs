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
    /// R-HITDEF chunk-1：HitDef 字段补全的离散对账——
    /// kill/guard.kill 限血、getpower/givepower 能量（含 lifetopowermul 默认值 + 超必杀分支）、
    /// forcestand 蹲被击改判站立、air.*/fall.* 解析、CopyInto 完整性。
    /// oracle：char.go:8433 computeDamage / :931-961 power 默认 / :12241 forcestand / :907-911。
    /// </summary>
    [TestFixture]
    public sealed class HitDefFieldsTests
    {
        static FFloat F(int v) => FFloat.FromInt(v);
        static MClsnBox Box(int x1, int y1, int x2, int y2) => new MClsnBox(F(x1), F(y1), F(x2), F(y2));

        static (MChar atk, MChar def) Pair()
        {
            MChar atk = new MChar
            {
                Id = 1, Facing = FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Power = 0, PowerMax = 3000,
                Pos = new FVector3(F(0), F(0), F(0)),
                Clsn1 = new[] { Box(10, -40, 30, 0) },
            };
            MChar def = new MChar
            {
                Id = 2, Facing = -FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Power = 0, PowerMax = 3000,
                Pos = new FVector3(F(20), F(0), F(0)),
                Clsn2 = new[] { Box(-10, -40, 10, 0) },
            };
            return (atk, def);
        }

        static MHitDef BasicHitDef()
        {
            return new MHitDef
            {
                HitHigh = true, HitLow = true, HitAir = true,
                HitDamage = 80, GuardDamage = 10,
                P1PauseTime = 8, P2PauseTime = 8,
                GroundHitTime = 14, GroundSlideTime = 5,
                GroundVelX = F(-4), GroundVelY = F(0),
                AnimType = MReaction.Medium,
            };
        }

        // ───────── kill / guard.kill ─────────

        [Test]
        public void Kill_False_FloorsLifeAtOne()
        {
            (MChar atk, MChar def) = Pair();
            def.Life = 50;
            atk.HitDef = BasicHitDef();
            atk.HitDef.HitDamage = 200;   // 足以致死
            atk.HitDef.Kill = false;
            atk.HitDef.Active = true;

            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(1), "kill=0：不可致死，保底剩 1 血");
            Assert.That(def.Ghv.Damage, Is.EqualTo(49), "实际伤害 = 50-1");
        }

        [Test]
        public void Kill_True_AllowsKO()
        {
            (MChar atk, MChar def) = Pair();
            def.Life = 50;
            atk.HitDef = BasicHitDef();
            atk.HitDef.HitDamage = 200;
            atk.HitDef.Kill = true;
            atk.HitDef.Active = true;

            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(0), "kill=1：允许 KO");
        }

        [Test]
        public void GuardKill_False_FloorsLifeAtOne()
        {
            (MChar atk, MChar def) = Pair();
            def.Life = 5;
            def.Guarding = true;
            atk.HitDef = BasicHitDef();
            atk.HitDef.GuardDamage = 100;   // chip 足以致死
            atk.HitDef.GuardKill = false;
            atk.HitDef.Active = true;

            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(1), "guard.kill=0：守招 chip 不致死");
        }

        // ───────── getpower / givepower ─────────

        [Test]
        public void Power_Hit_GainsAttackerAndDefender()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = BasicHitDef();
            atk.HitDef.HitGetPower = 56;
            atk.HitDef.HitGivePower = 48;
            atk.HitDef.Active = true;

            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(atk.Power, Is.EqualTo(56), "命中：攻方 +getpower");
            Assert.That(def.Power, Is.EqualTo(48), "命中：守方 +givepower");
        }

        [Test]
        public void Power_Guard_UsesGuardPowerValues()
        {
            (MChar atk, MChar def) = Pair();
            def.Guarding = true;
            atk.HitDef = BasicHitDef();
            atk.HitDef.GuardGetPower = 28;
            atk.HitDef.GuardGivePower = 24;
            atk.HitDef.Active = true;

            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(atk.Power, Is.EqualTo(28), "被防：攻方 +guardgetpower");
            Assert.That(def.Power, Is.EqualTo(24), "被防：守方 +guardgivepower");
        }

        [Test]
        public void Power_CappedAtPowerMax()
        {
            (MChar atk, MChar def) = Pair();
            atk.Power = 2990;
            atk.HitDef = BasicHitDef();
            atk.HitDef.HitGetPower = 100;
            atk.HitDef.Active = true;

            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(atk.Power, Is.EqualTo(3000), "能量夹到 PowerMax");
        }

        // ───────── forcestand ─────────

        [Test]
        public void ForceStand_CrouchHit_RoutesToStanding5000()
        {
            (MChar atk, MChar def) = Pair();
            def.StateType = 2;   // 守方蹲
            atk.HitDef = BasicHitDef();
            atk.HitDef.ForceStand = true;
            atk.HitDef.Active = true;

            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.PendingStateNo, Is.EqualTo(5000), "forcestand：蹲被击改判站立 5000");
            Assert.That(def.StateType, Is.EqualTo(1), "statetype 翻成 S");
        }

        [Test]
        public void NoForceStand_CrouchHit_RoutesToCrouch5010()
        {
            (MChar atk, MChar def) = Pair();
            def.StateType = 2;
            atk.HitDef = BasicHitDef();
            atk.HitDef.ForceStand = false;
            atk.HitDef.Active = true;

            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.PendingStateNo, Is.EqualTo(5010), "无 forcestand：蹲受击 5010");
            Assert.That(def.StateType, Is.EqualTo(2), "statetype 不变");
        }

        // ───────── CNS 解析：power 默认值 ─────────

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

        [Test]
        public void Cns_PowerDefaults_NonHyper()
        {
            MHitDef hd = ParseHitDef("attr = S, NA\ndamage = 100, 10\n");
            Assert.That(hd.HitGetPower, Is.EqualTo(70), "默认 0.7×damage");
            Assert.That(hd.HitGivePower, Is.EqualTo(60), "默认 0.6×damage");
            Assert.That(hd.GuardGetPower, Is.EqualTo(35), "守 = 命中×0.5");
            Assert.That(hd.GuardGivePower, Is.EqualTo(30), "守 = 命中×0.5");
        }

        [Test]
        public void Cns_PowerDefaults_Hyper_AttackerGainsZero()
        {
            MHitDef hd = ParseHitDef("attr = S, HA\ndamage = 100, 10\n");
            Assert.That(hd.HitGetPower, Is.EqualTo(0), "超必杀：攻方默认 0 能量");
            Assert.That(hd.HitGivePower, Is.EqualTo(60), "超必杀守方仍 0.6×damage");
        }

        [Test]
        public void Cns_ExplicitPower_Overrides()
        {
            MHitDef hd = ParseHitDef("attr = S, NA\ndamage = 100\ngetpower = 50, 20\ngivepower = 40, 10\n");
            Assert.That(hd.HitGetPower, Is.EqualTo(50));
            Assert.That(hd.GuardGetPower, Is.EqualTo(20));
            Assert.That(hd.HitGivePower, Is.EqualTo(40));
            Assert.That(hd.GuardGivePower, Is.EqualTo(10));
        }

        // ───────── CNS 解析：air.* / fall.* / forcestand ─────────

        [Test]
        public void Cns_AirAndFallFields()
        {
            MHitDef hd = ParseHitDef(
                "attr = S, NA\ndamage = 80\nground.type = Low\nair.type = High\n" +
                "animtype = Hard\nair.animtype = Back\nfall.recover = 0\nfall.recovertime = 30\n" +
                "yaccel = .5\nfall.yvelocity = -7\n");
            Assert.That(hd.GroundType, Is.EqualTo(MHitType.Low));
            Assert.That(hd.AirType, Is.EqualTo(MHitType.High));
            Assert.That(hd.AirAnimType, Is.EqualTo(MReaction.Back));
            Assert.That(hd.FallAnimType, Is.EqualTo(MReaction.Back), "air.animtype 非 Up → fall.animtype 默认 Back");
            Assert.IsFalse(hd.FallRecover, "fall.recover=0");
            Assert.That(hd.FallRecoverTime, Is.EqualTo(30));
        }

        [Test]
        public void Cns_AirType_DefaultsToGroundType()
        {
            MHitDef hd = ParseHitDef("attr = S, NA\ndamage = 80\nground.type = Trip\n");
            Assert.That(hd.AirType, Is.EqualTo(MHitType.Trip), "air.type 未写 → 随 ground.type");
        }

        [Test]
        public void Cns_ForceStand_DefaultsFromGroundVelocityY()
        {
            MHitDef withY = ParseHitDef("attr = S, NA\ndamage = 80\nground.velocity = -4, -3\n");
            Assert.IsTrue(withY.ForceStand, "ground.velocity Y!=0 → forcestand 默认开");

            MHitDef noY = ParseHitDef("attr = S, NA\ndamage = 80\nground.velocity = -4, 0\n");
            Assert.IsFalse(noY.ForceStand, "ground.velocity Y==0 → forcestand 默认关");
        }

        // ───────── CopyInto 完整性（修遗漏 bug）─────────

        [Test]
        public void HitDefController_CopyInto_CopiesAllNewFields()
        {
            MHitDef template = BasicHitDef();
            template.AirType = MHitType.Low;
            template.AirAnimType = MReaction.Back;
            template.FallAnimType = MReaction.Up;
            template.YAccel = F(2);
            template.FallYVel = F(-9);
            template.FallRecover = false;
            template.FallRecoverTime = 25;
            template.Kill = false;
            template.GuardKill = false;
            template.FallKill = false;
            template.ForceStand = true;
            template.HitGetPower = 11;
            template.HitGivePower = 22;
            template.GuardGetPower = 33;
            template.GuardGivePower = 44;

            MChar c = new MChar { StateType = 1, MoveType = 1 };
            new HitDefController { Template = template }.Run(c);

            MHitDef dst = c.HitDef;
            Assert.That(dst.AirType, Is.EqualTo(MHitType.Low));
            Assert.That(dst.AirAnimType, Is.EqualTo(MReaction.Back));
            Assert.That(dst.FallAnimType, Is.EqualTo(MReaction.Up));
            Assert.That(dst.YAccel.Raw, Is.EqualTo(F(2).Raw));
            Assert.That(dst.FallYVel.Raw, Is.EqualTo(F(-9).Raw));
            Assert.IsFalse(dst.FallRecover);
            Assert.That(dst.FallRecoverTime, Is.EqualTo(25));
            Assert.IsFalse(dst.Kill);
            Assert.IsFalse(dst.GuardKill);
            Assert.IsFalse(dst.FallKill);
            Assert.IsTrue(dst.ForceStand);
            Assert.That(dst.HitGetPower, Is.EqualTo(11));
            Assert.That(dst.HitGivePower, Is.EqualTo(22));
            Assert.That(dst.GuardGetPower, Is.EqualTo(33));
            Assert.That(dst.GuardGivePower, Is.EqualTo(44));
        }
    }
}
