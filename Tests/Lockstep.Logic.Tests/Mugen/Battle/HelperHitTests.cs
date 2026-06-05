using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Battle;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// R-ENT 切片 7：全实体跨队命中——P1 造一个带 HitDef 的攻击 helper，命中 P2(敌队)使其掉血；
    /// P1(与 helper 同队)不被自己 helper 打。验证 RunHits 的跨队判定。
    /// </summary>
    [TestFixture]
    public sealed class HelperHitTests
    {
        // P1：state -1 在 P2 位置造一个攻击 helper(state 1000，HitDef 持续激活)。anim 0 带攻击/受击框。
        const string OwnerCns =
            "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State -1, spawn attacker]\ntype = Helper\ntrigger1 = time = 1\nid = 1\nstateno = 1000\npos = 30, 0\n\n" +
            "[Statedef 1000]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State 1000, hitdef]\ntype = HitDef\ntrigger1 = 1\nattr = S, NA\ndamage = 50\n" +
            "hitflag = MAF\nground.type = High\nground.velocity = -4\nanimtype = Light\nground.hittime = 10\n";
        // P2：站立发呆，anim 0 带受击框。
        const string DefCns = "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n";
        const string Air =
            "[Begin Action 0]\nClsn1: 1\n Clsn1[0] = -40,-100, 40, 0\nClsn2: 1\n Clsn2[0] = -40,-100, 40, 0\n0,0, 0,0, 4\n0,1, 0,0, 4\n";

        static MBattleEngine Engine()
        {
            MCharData owner = MCharLoader.Load(new[] { OwnerCns }, OwnerCns, null, Air, null, "Owner");
            MCharData def = MCharLoader.Load(new[] { DefCns }, DefCns, null, Air, null, "Dummy");
            MBattleEngine engine = new MBattleEngine();
            MChar p1 = MCharLoader.SpawnChar(owner, 0, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(def, 1, startStateNo: 0, startAnimNo: 0);
            p1.Pos = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            p2.Pos = new FVector3(FFloat.FromInt(30), FFloat.Zero, FFloat.Zero);
            engine.Add(p1, owner);
            engine.Add(p2, def);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        static List<MInput> NoInput()
        {
            return new List<MInput> { MInput.None, MInput.None };
        }

        [Test]
        public void Helper_DamagesEnemyPlayer()
        {
            MBattleEngine engine = Engine();
            MChar p2 = engine.Chars[1];
            int lifeBefore = p2.Life;
            for (int f = 0; f < 6; f++) { engine.Tick(NoInput()); }
            Assert.That(engine.Helpers.Count, Is.GreaterThanOrEqualTo(1), "攻击 helper 已造出");
            Assert.That(p2.Life, Is.LessThan(lifeBefore), "敌队 P2 被 helper 命中掉血");
        }

        [Test]
        public void Helper_DoesNotDamageOwnTeam()
        {
            MBattleEngine engine = Engine();
            MChar p1 = engine.Chars[0];
            int p1LifeBefore = p1.Life;
            for (int f = 0; f < 8; f++) { engine.Tick(NoInput()); }
            Assert.That(p1.Life, Is.EqualTo(p1LifeBefore), "同队 owner 不被自己 helper 打");
        }

        [Test]
        public void HelperHit_IsDeterministic()
        {
            ulong RunOnce()
            {
                MBattleEngine engine = Engine();
                for (int f = 0; f < 8; f++) { engine.Tick(NoInput()); }
                return engine.ComputeHash();
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()));
        }
    }
}
