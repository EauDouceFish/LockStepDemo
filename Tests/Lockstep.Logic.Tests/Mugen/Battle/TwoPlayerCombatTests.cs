using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;
using Lockstep.Tests;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// R-2P：双人对战接通（引擎 headless）。引擎双向 TryHit + R-GHV 受击机联调——
    /// P1 普攻命中 P2 → P2 掉血 + 进受击状态机 → 抖动后滑行恢复。用标准 common1 提供受击数据态。
    /// 这是"两个角色真打"从 ≈0% 跨到可验证的关键回归。
    /// </summary>
    [TestFixture]
    public sealed class TwoPlayerCombatTests
    {
        // 攻方：state 0 持续激活一个 HitDef，anim 0 带 Clsn1 攻击框。
        const string AttackerCns =
            "[Statedef 0]\ntype = S\nphysics = S\nmovetype = A\nanim = 0\n\n" +
            "[State 0, hit]\ntype = HitDef\ntrigger1 = Time = 0\n" +   // 只在进状态首帧激活一次（真实招式行为）
            "attr = S, NA\nhitflag = MAF\ndamage = 100, 0\npausetime = 4, 4\n" +
            "ground.velocity = -4, 0\nground.hittime = 8\nground.slidetime = 4\nanimtype = Medium\n";
        const string AttackerAir =
            "[Begin Action 0]\nClsn1: 1\n Clsn1[0] = 10,-80, 40, 0\n0,0, 0,0, 4\n0,1, 0,0, 4\n";

        // 守方：state 0 站立发呆，anim 0 带 Clsn2 受击框。
        const string DefenderCns = "[Statedef 0]\ntype = S\nphysics = S\nanim = 0\n";
        const string DefenderAir =
            "[Begin Action 0]\nClsn2: 1\n Clsn2[0] = -20,-80, 20, 0\n0,0, 0,0, 4\n0,1, 0,0, 4\n";

        static FFloat F(int v) => FFloat.FromInt(v);

        // 装配 1v1：攻方在 x=0 面右、守方在 x=30 面左，攻击框与受击框重叠；带标准 common1（受击态 5000+）。
        static MBattleEngine BuildPair(string common)
        {
            MCharData atkData = MCharLoader.Load(new[] { AttackerCns }, AttackerCns, common, AttackerAir, null, "Atk");
            MCharData defData = MCharLoader.Load(new[] { DefenderCns }, DefenderCns, common, DefenderAir, null, "Def");

            MChar atk = MCharLoader.SpawnChar(atkData, 0, startStateNo: 0, startAnimNo: 0);
            MChar def = MCharLoader.SpawnChar(defData, 0, startStateNo: 0, startAnimNo: 0);
            atk.Id = 1; def.Id = 2;
            atk.Facing = FFloat.One; atk.Pos = new FVector3(F(0), F(0), F(0));
            def.Facing = -FFloat.One; def.Pos = new FVector3(F(30), F(0), F(0));
            atk.Life = atk.LifeMax = 1000;
            def.Life = def.LifeMax = 1000;

            MBattleEngine engine = new MBattleEngine();
            engine.Add(atk, atkData);
            engine.Add(def, defData);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        static string Common()
        {
            string f = TestAssets.Common1Cns();
            return File.Exists(f) ? File.ReadAllText(f) : null;
        }

        [Test]
        public void P1Hits_P2_TakesDamage_AndEntersGetHit()
        {
            string common = Common();
            if (common == null)
            {
                Assert.Ignore("common1 素材缺失，跳过。");
            }
            MBattleEngine engine = BuildPair(common);
            MChar p2 = engine.Chars[1];
            List<MInput> inputs = new List<MInput> { MInput.None, MInput.None };

            bool enteredGetHit = false;
            for (int f = 0; f < 10; f++)
            {
                engine.Tick(inputs);
                if (p2.StateNo >= 5000 && p2.StateNo <= 5160)
                {
                    enteredGetHit = true;
                }
            }
            Assert.That(p2.Life, Is.EqualTo(900), "P2 掉 100 血（只命中一次：同招同目标一次）");
            Assert.IsTrue(enteredGetHit, "P2 进入受击状态机 5000-5160");
            Assert.That(p2.MoveType, Is.EqualTo(2).Or.EqualTo(1),
                "P2 处于受击(H)或已恢复(I)，不会卡在攻击态");
        }

        [Test]
        public void P2_RunsThroughGetHitCycle_BackToStand()
        {
            string common = Common();
            if (common == null)
            {
                Assert.Ignore("common1 素材缺失，跳过。");
            }
            MBattleEngine engine = BuildPair(common);
            MChar p2 = engine.Chars[1];
            List<MInput> inputs = new List<MInput> { MInput.None, MInput.None };

            List<int> seq = new List<int>();
            for (int f = 0; f < 40; f++)
            {
                engine.Tick(inputs);
                if (seq.Count == 0 || seq[seq.Count - 1] != p2.StateNo)
                {
                    seq.Add(p2.StateNo);
                }
            }
            Assert.That(seq, Does.Contain(5000), "经过立受击 5000");
            Assert.That(p2.StateNo, Is.EqualTo(0), "受击周期走完回到站立 0");
        }

        [Test]
        public void BothDirections_OnlyAttackerWithHitDefConnects()
        {
            // 守方没有 HitDef → 不会反伤攻方（双向 TryHit 只有持 HitDef 且重叠的一方生效）。
            string common = Common();
            if (common == null)
            {
                Assert.Ignore("common1 素材缺失，跳过。");
            }
            MBattleEngine engine = BuildPair(common);
            MChar p1 = engine.Chars[0];
            List<MInput> inputs = new List<MInput> { MInput.None, MInput.None };
            for (int f = 0; f < 10; f++)
            {
                engine.Tick(inputs);
            }
            Assert.That(p1.Life, Is.EqualTo(1000), "攻方未被反伤（守方无 HitDef）");
        }

        [Test]
        public void ThroughRoundSystem_HitDuringFight_DamagesP2()
        {
            string common = Common();
            if (common == null)
            {
                Assert.Ignore("common1 素材缺失，跳过。");
            }
            MBattleEngine engine = BuildPair(common);
            // 用回合系统驱动：入场后进入战斗才允许命中演出（这里命中不依赖 ctrl，但验证整链路）。
            MRoundSystem round = new MRoundSystem(engine) { IntroTime = 2 };
            MChar p2 = engine.Chars[1];
            List<MInput> inputs = new List<MInput> { MInput.None, MInput.None };
            for (int f = 0; f < 12; f++)
            {
                round.Tick(inputs);
            }
            Assert.That(round.State, Is.EqualTo(MRoundState.Fight).Or.EqualTo(MRoundState.PreOver));
            Assert.That(p2.Life, Is.LessThan(1000), "回合系统驱动下 P2 仍被命中掉血");
        }

        [Test]
        public void Deterministic_TwoCharCombat_SameHash()
        {
            string common = Common();
            if (common == null)
            {
                Assert.Ignore("common1 素材缺失，跳过。");
            }
            ulong RunOnce()
            {
                MBattleEngine engine = BuildPair(common);
                List<MInput> inputs = new List<MInput> { MInput.None, MInput.None };
                for (int f = 0; f < 30; f++)
                {
                    engine.Tick(inputs);
                }
                return engine.ComputeHash();
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()), "双角色对战逐帧确定性一致");
        }
    }
}
