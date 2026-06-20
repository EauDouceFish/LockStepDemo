using System.Collections.Generic;
using NUnit.Framework;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// roundNoDamage（system.go:1592）：分胜负后过了 over_hittime 双 KO 窗口、进 win pose 之前，命中不扣血。
    /// over_hittime 窗口内（刚 KO 的数帧）仍可扣血 → 允许同帧双杀。
    /// </summary>
    [TestFixture]
    public sealed class MRoundNoDamageTests
    {
        const string Cns = "[Statedef 0]\ntype=S\nphysics=S\nanim=0\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n";

        static MBattleEngine TwoChar()
        {
            MCharData data = MCharLoader.Load(new[] { Cns }, Cns, null, Air, null, "ND");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0, 0, 0), data);
            engine.Add(MCharLoader.SpawnChar(data, 0, 0, 0), data);
            engine.Chars[0].Id = 1; engine.Chars[1].Id = 2;
            engine.Chars[0].Life = engine.Chars[0].LifeMax = 1000;
            engine.Chars[1].Life = engine.Chars[1].LifeMax = 1000;
            engine.LinkPair();
            return engine;
        }

        static readonly List<MInput> NoInput = new List<MInput> { MInput.None, MInput.None };

        [Test]
        public void Engine_NoDamageTrue_DiscardsPendingDamage()
        {
            MBattleEngine engine = TwoChar();
            engine.Chars[0].PendingLifeDamage = 100;
            engine.NoDamage = true;
            engine.Tick(NoInput);
            Assert.That(engine.Chars[0].Life, Is.EqualTo(1000), "禁伤窗口丢弃待结算伤害");
        }

        [Test]
        public void Engine_NoDamageFalse_AppliesPendingDamage()
        {
            MBattleEngine engine = TwoChar();
            engine.Chars[0].PendingLifeDamage = 100;
            engine.NoDamage = false;
            engine.Tick(NoInput);
            Assert.That(engine.Chars[0].Life, Is.EqualTo(900), "非禁伤窗口正常扣血");
        }

        [Test]
        public void RoundSystem_DamageAllowedInHitTimeWindow_DisabledAfter()
        {
            MBattleEngine engine = TwoChar();
            MRoundSystem round = new MRoundSystem(engine)
            {
                IntroTime = 1, OverHitTime = 2, OverWaitTime = 5, WinPoseTime = 3, RoundsToWin = 9,
            };
            while (round.State != MRoundState.Fight) { round.Tick(NoInput); }
            Assert.IsFalse(engine.NoDamage, "战斗中可扣血");

            engine.Chars[1].Life = 0;   // KO → 进入结算
            Dictionary<int, bool> byIntro = new Dictionary<int, bool>();
            for (int i = 0; i < 12; i++)
            {
                round.Tick(NoInput);
                byIntro[round.Intro] = engine.NoDamage;
            }
            // NoDamage 在该帧 StepRoundState 起点据当时 Intro 计算，随后 Intro-- → 记录键 = Intro-1。
            // over_hittime 窗口内（计算时 Intro=-1，记录键 -2）仍可扣血。
            Assert.IsTrue(byIntro.ContainsKey(-2) && byIntro.ContainsKey(-3), "应跨过这些 intro 值");
            Assert.IsFalse(byIntro[-2], "over_hittime 窗口内仍可扣血（允许双杀）");
            Assert.IsTrue(byIntro[-3], "过了 over_hittime → 禁伤直到 win pose");
        }
    }
}
