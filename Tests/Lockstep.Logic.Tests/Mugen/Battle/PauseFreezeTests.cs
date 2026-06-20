using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Hit;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// Pause/SuperPause 按 Ikemen 模型（全局 sys.pausetime/supertime + 每角色 movetime + pauseBool/acttmp，
    /// 移植 system.go:2562 + char.go:8920/8941/11421/11524）。控制器经 SetSuperPause 写 buffer，下一帧 Step 生效；
    /// 施暂停方在 movetime 帧内可动、之后冻结；被暂停方全程冻结；命中不结算；计时确定性递减。
    /// </summary>
    [TestFixture]
    public sealed class PauseFreezeTests
    {
        const string Cns =
            "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State 0, idle]\ntype = Null\ntrigger1 = 1\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n0,1, 0,0, 4\n";
        const string AttackCns =
            "[Statedef 0]\n" +
            "type = S\n" +
            "movetype = A\n" +
            "physics = N\n" +
            "anim = 0\n" +
            "ctrl = 0\n\n" +
            "[State 0, hit]\n" +
            "type = HitDef\n" +
            "trigger1 = 1\n" +
            "attr = S, NA\n" +
            "hitflag = MAF\n" +
            "damage = 25, 0\n" +
            "pausetime = 0, 0\n" +
            "ground.hittime = 8\n" +
            "ground.velocity = 0, 0\n";
        const string AttackAir =
            "[Begin Action 0]\n" +
            "Clsn1: 1\n" +
            " Clsn1[0] = 10,-40, 40, 0\n" +
            "0,0, 0,0, 4\n";
        const string DefenderCns =
            "[Statedef 0]\n" +
            "type = S\n" +
            "movetype = I\n" +
            "physics = N\n" +
            "anim = 0\n" +
            "ctrl = 0\n\n" +
            "[State 0, idle]\n" +
            "type = Null\n" +
            "trigger1 = 1\n";
        const string DefenderAir =
            "[Begin Action 0]\n" +
            "Clsn2: 1\n" +
            " Clsn2[0] = -20,-40, 20, 0\n" +
            "0,0, 0,0, 4\n";

        static MBattleEngine TwoChars()
        {
            MCharData data = MCharLoader.Load(new[] { Cns }, Cns, null, Air, null, "Dummy");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0), data);
            engine.Add(MCharLoader.SpawnChar(data, 1), data);
            engine.LinkPair();
            return engine;
        }

        static MBattleEngine AttackVsDefender()
        {
            MCharData attack = MCharLoader.Load(new[] { AttackCns }, AttackCns, null, AttackAir, null, "Attack");
            MCharData defender = MCharLoader.Load(new[] { DefenderCns }, DefenderCns, null, DefenderAir, null, "Defender");
            MBattleEngine engine = new MBattleEngine { EnableDemoAutoTurnFallback = false };
            MChar p1 = MCharLoader.SpawnChar(attack, 0);
            MChar p2 = MCharLoader.SpawnChar(defender, 1);
            engine.Add(p1, attack);
            engine.Add(p2, defender);
            engine.LinkPair();
            p1.Pos = new FVector3(FFloat.Zero, FFloat.Zero, FFloat.Zero);
            p2.Pos = new FVector3(FFloat.FromInt(20), FFloat.Zero, FFloat.Zero);
            p1.Facing = FFloat.One;
            p2.Facing = -FFloat.One;
            return engine;
        }

        static List<MInput> NoInput()
        {
            return new List<MInput> { MInput.None, MInput.None };
        }

        static FFloat F(int value) => FFloat.FromInt(value);

        static MClsnBox Box(int x1, int y1, int x2, int y2)
        {
            return new MClsnBox(F(x1), F(y1), F(x2), F(y2));
        }

        static MHitDef BasicHitDef()
        {
            return new MHitDef
            {
                Active = true,
                HitHigh = true,
                HitLow = true,
                HitAir = true,
                HitDamage = 25,
                P1PauseTime = 0,
                P2PauseTime = 0,
                GroundHitTime = 8,
            };
        }

        [Test]
        public void NoPause_BothCharsAdvanceTime()
        {
            MBattleEngine engine = TwoChars();
            engine.Tick(NoInput());
            engine.Tick(NoInput());
            Assert.That(engine.Chars[0].Time, Is.GreaterThan(0));
            Assert.That(engine.Chars[1].Time, Is.GreaterThan(0));
        }

        [Test]
        public void SuperPause_BufferAppliesNextFrame_FreezesNonPauser_PauserActs()
        {
            MBattleEngine engine = TwoChars();
            engine.Tick(NoInput());   // 暖机
            MChar pauser = engine.Chars[0];
            MChar other = engine.Chars[1];
            pauser.SetSuperPause(pausetime: 6, movetime: 6, unhittable: false);   // 写 buffer，本帧未生效
            int otherTimeBefore = other.Time;
            int pauserTimeBefore = pauser.Time;

            engine.Tick(NoInput());   // Step 应用 buffer → SuperTime=6 生效；pauser movetime>0 可动、other 冻结
            engine.Tick(NoInput());

            Assert.That(other.Time, Is.EqualTo(otherTimeBefore), "被暂停方全程冻结（Time 不变）");
            Assert.That(pauser.Time, Is.GreaterThan(pauserTimeBefore), "施暂停方在 movetime 窗口内继续");
        }

        [Test]
        public void PauseBool_DoesNotRequireCommandList()
        {
            MPauseState pause = new MPauseState { PauseTime = 3 };
            MChar character = new MChar { Pause = pause, CommandList = null };

            character.ComputePauseBool();

            Assert.That(character.PauseBool, Is.True);
            Assert.That(character.Acttmp, Is.EqualTo(-2));
        }

        [Test]
        public void Pause_MovetimeAttackerCanHitFrozenDefender_ButFrozenAttackerCannot()
        {
            MBattleEngine engine = AttackVsDefender();
            MChar attacker = engine.Chars[0];
            MChar defender = engine.Chars[1];
            attacker.SetPause(pausetime: 3, movetime: 3);

            engine.Tick(NoInput());

            Assert.That(attacker.PauseBool, Is.False, "movetime owner should act during pause.");
            Assert.That(defender.PauseBool, Is.True, "non-owner should stay frozen.");
            Assert.That(defender.Life, Is.EqualTo(defender.LifeMax - 25));

            engine = AttackVsDefender();
            attacker = engine.Chars[0];
            defender = engine.Chars[1];
            attacker.SetPause(pausetime: 3, movetime: 0);

            engine.Tick(NoInput());

            Assert.That(attacker.PauseBool, Is.True, "no-movetime owner should be frozen.");
            Assert.That(defender.Life, Is.EqualTo(defender.LifeMax));
        }

        [Test]
        public void SuperPause_UnhittableBlocksHitUntilTimerExpires()
        {
            MChar attacker = new MChar
            {
                Id = 1,
                Facing = FFloat.One,
                StateType = 1,
                Life = 1000,
                LifeMax = 1000,
                Pos = new FVector3(F(0), F(0), F(0)),
                Clsn1 = new[] { Box(10, -40, 30, 0) },
                HitDef = BasicHitDef(),
            };
            MChar defender = new MChar
            {
                Id = 2,
                Facing = -FFloat.One,
                StateType = 1,
                Life = 1000,
                LifeMax = 1000,
                Pos = new FVector3(F(20), F(0), F(0)),
                Clsn2 = new[] { Box(-10, -40, 10, 0) },
                UnhittableTime = 2,
            };

            Assert.That(MHitSystem.TryHit(attacker, defender), Is.False);
            Assert.That(defender.Life, Is.EqualTo(1000));

            defender.UnhittableTime = 0;
            Assert.That(MHitSystem.TryHit(attacker, defender), Is.True);
            Assert.That(defender.Life, Is.EqualTo(975));
        }

        [Test]
        public void SuperPause_ExpiresAfterDuration_NonPauserResumes()
        {
            MBattleEngine engine = TwoChars();
            engine.Tick(NoInput());
            MChar other = engine.Chars[1];
            engine.Chars[0].SetSuperPause(pausetime: 4, movetime: 4, unhittable: false);
            for (int f = 0; f < 10; f++) { engine.Tick(NoInput()); }   // 跑满整个暂停
            int otherFrozen = other.Time;
            engine.Tick(NoInput());
            Assert.That(other.Time, Is.GreaterThan(otherFrozen), "暂停结束后被暂停方恢复推进");
            Assert.That(engine.PauseState.SuperTime, Is.EqualTo(0), "SuperTime 归零");
        }

        [Test]
        public void PosFreeze_SkipsPhysicsThisFrame_ThenClears()
        {
            MBattleEngine engine = TwoChars();
            MChar c = engine.Chars[0];
            c.Vel = new FVector3(FFloat.FromInt(5), FFloat.Zero, FFloat.Zero);
            c.PosFreeze = true;
            FFloat xBefore = c.Pos.X;
            engine.Tick(NoInput());
            Assert.That(c.Pos.X.Raw, Is.EqualTo(xBefore.Raw), "PosFreeze 帧位置不变");
            Assert.That(c.PosFreeze, Is.False, "PosFreeze 用后清零（需每帧重新断言）");
            engine.Tick(NoInput());
            Assert.That(c.Pos.X.Raw, Is.Not.EqualTo(xBefore.Raw), "次帧恢复位置积分");
        }

        [Test]
        public void HitpauseSnapshot_FreezesStatePhysicsAnimationAndHitForWholeFrame()
        {
            MBattleEngine engine = TwoChars();
            MChar attacker = engine.Chars[0];
            MChar defender = engine.Chars[1];
            attacker.HitDef = BasicHitDef();
            attacker.Clsn1 = new[] { Box(10, -40, 30, 0) };
            defender.Clsn2 = new[] { Box(-10, -40, 10, 0) };
            attacker.Pos = new FVector3(F(0), F(0), F(0));
            defender.Pos = new FVector3(F(20), F(0), F(0));
            attacker.Facing = FFloat.One;
            defender.Facing = -FFloat.One;
            attacker.Vel = new FVector3(F(5), F(0), F(0));
            attacker.Hitstop = 1;
            int timeBefore = attacker.Time;
            int animTimeBefore = attacker.AnimCurTime;
            FFloat xBefore = attacker.Pos.X;
            int lifeBefore = defender.Life;

            engine.Tick(NoInput());

            Assert.That(attacker.Hitstop, Is.EqualTo(0), "hitpause timer expires inside the frame.");
            Assert.That(attacker.Acttmp, Is.EqualTo(-1), "the whole snapshot frame is still hitpaused.");
            Assert.That(attacker.Time, Is.EqualTo(timeBefore));
            Assert.That(attacker.AnimCurTime, Is.EqualTo(animTimeBefore));
            Assert.That(attacker.Pos.X.Raw, Is.EqualTo(xBefore.Raw));
            Assert.That(defender.Life, Is.EqualTo(lifeBefore), "attacker must not hit on the same frame hitstop reaches zero.");

            engine.Tick(NoInput());

            Assert.That(attacker.Time, Is.GreaterThan(timeBefore), "state resumes after hitpause frame.");
            Assert.That(attacker.Pos.X.Raw, Is.Not.EqualTo(xBefore.Raw), "physics resumes after hitpause frame.");
        }

        [Test]
        public void SuperPause_IsDeterministic()
        {
            ulong RunOnce()
            {
                MBattleEngine engine = TwoChars();
                engine.Tick(NoInput());
                engine.Chars[0].SetSuperPause(pausetime: 5, movetime: 5, unhittable: false);
                for (int f = 0; f < 10; f++) { engine.Tick(NoInput()); }
                return engine.ComputeHash();
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()));
        }
    }
}
