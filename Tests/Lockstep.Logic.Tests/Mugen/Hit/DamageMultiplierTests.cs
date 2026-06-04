using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Tests.Mugen
{
    /// <summary>
    /// R-HITDEF chunk-2：攻防倍率伤害公式 + AttackMulSet/DefenceMulSet 控制器。
    /// computeDamage 移植 char.go:8440：damage ×= (AttackMul×attackBase/100) / finalDefense，
    /// 至少 1、不超剩血、kill 保底、四舍五入。finalDefense 移植 char.go:12081-12085。
    /// </summary>
    [TestFixture]
    public sealed class DamageMultiplierTests
    {
        static FFloat F(int v) => FFloat.FromInt(v);
        static FFloat Frac(int num, int den) => FFloat.FromInt(num) / FFloat.FromInt(den);
        static MClsnBox Box(int x1, int y1, int x2, int y2) => new MClsnBox(F(x1), F(y1), F(x2), F(y2));

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

        static MHitDef Hit(int damage)
        {
            return new MHitDef
            {
                HitHigh = true, HitLow = true, HitAir = true,
                HitDamage = damage, GuardDamage = 0,
                P1PauseTime = 8, P2PauseTime = 8, GroundHitTime = 14,
                GroundVelX = F(-4), AnimType = MReaction.Medium,
                Active = true,
            };
        }

        static BytecodeExp Compile(string expr) => new MugenExprCompiler().Compile(expr);

        // ───────── 默认倍率 = 不改变伤害（回归保护）─────────

        [Test]
        public void DefaultMultipliers_DamageUnchanged()
        {
            (MChar atk, MChar def) = Pair();
            atk.HitDef = Hit(80);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(920), "默认 atkmul=defmul=1，伤害不变");
        }

        // ───────── 攻击倍率 ─────────

        [Test]
        public void AttackMul_ScalesDamage()
        {
            (MChar atk, MChar def) = Pair();
            atk.AttackMul = F(2);
            atk.HitDef = Hit(80);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(1000 - 160), "AttackMul=2 → 双倍伤害");
        }

        [Test]
        public void AttackBase_FromConstants_ScalesDamage()
        {
            (MChar atk, MChar def) = Pair();
            atk.Constants = new MConstants { Attack = 150 };   // [Data] attack=150 → atkmul=1.5
            atk.HitDef = Hit(80);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(1000 - 120), "attackBase=150 → 1.5× 伤害");
        }

        // ───────── 防御倍率 ─────────

        [Test]
        public void DefenceBase_FromConstants_ReducesDamage()
        {
            (MChar atk, MChar def) = Pair();
            def.Constants = new MConstants { Defence = 200 };   // finalDefense=2 → 半伤
            atk.HitDef = Hit(80);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(1000 - 40), "defenceBase=200 → 半伤");
        }

        [Test]
        public void MinimumOneDamage_DespiteHugeDefense()
        {
            (MChar atk, MChar def) = Pair();
            def.Constants = new MConstants { Defence = 1000 };   // finalDefense=10 → 1/10=0.1 → 保底 1
            atk.HitDef = Hit(1);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(999), "极高防仍至少 1 点伤害");
        }

        [Test]
        public void Damage_RoundsHalfUp()
        {
            (MChar atk, MChar def) = Pair();
            atk.AttackMul = Frac(3, 2);   // 1.5
            atk.HitDef = Hit(5);          // 5×1.5 = 7.5 → 四舍五入 8
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(1000 - 8), "7.5 → 8（四舍五入）");
        }

        // ───────── AttackMulSet 控制器 ─────────

        [Test]
        public void AttackMulSetController_Value_ScalesDamage()
        {
            (MChar atk, MChar def) = Pair();
            new AttackMulSetController { Value = Compile("2") }.Run(atk);
            Assert.That(atk.AttackMul.Raw, Is.EqualTo(F(2).Raw));
            atk.HitDef = Hit(80);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(1000 - 160));
        }

        [Test]
        public void AttackMulSetController_DamageParam_OverridesValue()
        {
            MChar c = new MChar();
            new AttackMulSetController { Value = Compile("2"), Damage = Compile("3") }.Run(c);
            Assert.That(c.AttackMul.Raw, Is.EqualTo(F(3).Raw), "damage 分量优先");
        }

        // ───────── DefenceMulSet 控制器 ─────────

        [Test]
        public void DefenceMulSetController_MugenDefault_ValueIsDamageMultiplier()
        {
            // MUGEN 默认 mulType=0 → customDefense=1/value；value=2 → 受到 2× 伤害
            (MChar atk, MChar def) = Pair();
            new DefenceMulSetController { Value = Compile("2") }.Run(def);
            Assert.IsTrue(def.DefenseMulDelay, "MUGEN 默认 onHit=true");
            atk.HitDef = Hit(80);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(1000 - 160), "DefenceMulSet value=2 → 2× 伤害");
        }

        [Test]
        public void DefenceMulSetController_MulType1_DirectDefense()
        {
            (MChar atk, MChar def) = Pair();
            new DefenceMulSetController { Value = Compile("2"), MulType = Compile("1") }.Run(def);
            atk.HitDef = Hit(80);
            Assert.IsTrue(MHitSystem.TryHit(atk, def));
            Assert.That(def.Life, Is.EqualTo(1000 - 40), "mulType=1：customDefense=2 → 半伤");
        }

        // ───────── finalDefense onHit 延迟语义（单元级）─────────

        [Test]
        public void FinalDefense_OnHitDelay_OnlyAppliesInHitState()
        {
            MChar c = new MChar { CustomDefense = F(2), DefenseMulDelay = true, MoveType = 0 };
            Assert.That(c.ComputeFinalDefense().Raw, Is.EqualTo(FFloat.One.Raw), "非受击态：延迟，customDef 不计");

            c.MoveType = 2;   // H 受击
            Assert.That(c.ComputeFinalDefense().Raw, Is.EqualTo(F(2).Raw), "受击态：customDef 生效");
        }

        [Test]
        public void FinalDefense_NoDelay_AlwaysApplies()
        {
            MChar c = new MChar { CustomDefense = F(2), DefenseMulDelay = false, MoveType = 0 };
            Assert.That(c.ComputeFinalDefense().Raw, Is.EqualTo(F(2).Raw), "onHit=false：任何态都生效");
        }

        // ───────── CNS 解析 ─────────

        [Test]
        public void Cns_ParsesAttackMulAndDefenceMul()
        {
            string cns =
                "[Statedef 300]\ntype = S\n" +
                "[State 300, atk]\ntype = AttackMulSet\ntrigger1 = 1\nvalue = 2\n" +
                "[State 300, def]\ntype = DefenceMulSet\ntrigger1 = 1\nvalue = 3\nmultype = 1\n";
            System.Collections.Generic.Dictionary<int, Lockstep.Mugen.State.MStateDef> states =
                Lockstep.Mugen.Parse.MugenCnsParser.Parse(cns);
            MChar c = new MChar { StateNo = 300, StateType = 1 };
            new Lockstep.Mugen.State.MStateMachine().RunFrame(c, states);
            Assert.That(c.AttackMul.Raw, Is.EqualTo(F(2).Raw), "AttackMulSet value=2");
            Assert.That(c.CustomDefense.Raw, Is.EqualTo(F(3).Raw), "DefenceMulSet mulType=1 value=3");
        }
    }
}
