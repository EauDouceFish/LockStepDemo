using System.Collections.Generic;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Battle;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// R-ENT 切片 5：custom state（投技 p2getp1state）——P1 命中 P2 时把 P2 拉进 P1 的状态 1100（P2 自身无此状态），
    /// 该状态用 P1 的状态表在 P2 身上跑（StateOwner=P1）；P1 状态里的 SelfState 让 P2 退出归属回自身。
    /// </summary>
    [TestFixture]
    public sealed class CustomStateThrowTests
    {
        // P1：state 0 激活投技 HitDef(p2stateno=1100)；P1 定义 state 1100（投技演出），用 ParentVarSet 不行(P2 非 helper)，
        // 用普通 VarSet 写 P2 自己的 var(3)=77（自定义状态里 c=P2，VarSet 写 P2 的 var），再 SelfState 回 0。
        const string OwnerCns =
            "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State 0, throw]\ntype = HitDef\ntrigger1 = time = 0\nattr = S, NT\ndamage = 30\n" +
            "hitflag = MAF\np2stateno = 1100\nground.type = High\nground.velocity = 0\n\n" +
            "[Statedef 1100]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State 1100, mark]\ntype = VarSet\ntrigger1 = 1\nv = 3\nvalue = 77\n\n" +
            "[State 1100, recover]\ntype = SelfState\ntrigger1 = time >= 3\nvalue = 0\n";
        // P2：站立，无 state 1100。
        const string DefCns = "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n";
        const string Air =
            "[Begin Action 0]\nClsn1: 1\n Clsn1[0] = -40,-100, 40, 0\nClsn2: 1\n Clsn2[0] = -40,-100, 40, 0\n0,0, 0,0, 4\n0,1, 0,0, 4\n";

        static MBattleEngine Engine()
        {
            MCharData owner = MCharLoader.Load(new[] { OwnerCns }, OwnerCns, null, Air, null, "Thrower");
            MCharData def = MCharLoader.Load(new[] { DefCns }, DefCns, null, Air, null, "Victim");
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
        public void Throw_PutsVictimIntoThrowerCustomState()
        {
            MBattleEngine engine = Engine();
            MChar p1 = engine.Chars[0];
            MChar p2 = engine.Chars[1];
            engine.Tick(NoInput());   // P1 state0 HitDef 命中 P2 → P2 PendingState=1100, StateOwner=P1
            engine.Tick(NoInput());   // P2 状态机用 P1 表进 1100
            Assert.That(p2.StateNo, Is.EqualTo(1100), "P2 进了投技状态 1100（仅 P1 定义）");
            Assert.That(ReferenceEquals(p2.StateOwner, p1), Is.True, "P2 状态归属=P1（跑 P1 状态表）");
        }

        [Test]
        public void Throw_ThrowerStateRunsOnVictim()
        {
            MBattleEngine engine = Engine();
            MChar p2 = engine.Chars[1];
            for (int f = 0; f < 4; f++) { engine.Tick(NoInput()); }
            p2.IntVars.TryGetValue(3, out int v);
            Assert.That(v, Is.EqualTo(77), "P1 的 state 1100 在 P2 身上跑，写了 P2 的 var(3)=77");
        }

        [Test]
        public void Throw_SelfStateReleasesOwnership()
        {
            MBattleEngine engine = Engine();
            MChar p2 = engine.Chars[1];
            for (int f = 0; f < 12; f++) { engine.Tick(NoInput()); }
            Assert.That(p2.StateOwner, Is.Null, "SelfState 后 P2 退出自定义状态归属（回自身状态表）");
        }

        [Test]
        public void Throw_IsDeterministic()
        {
            ulong RunOnce()
            {
                MBattleEngine engine = Engine();
                for (int f = 0; f < 10; f++) { engine.Tick(NoInput()); }
                return engine.ComputeHash();
            }
            Assert.That(RunOnce(), Is.EqualTo(RunOnce()));
        }
    }
}
