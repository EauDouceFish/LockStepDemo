using System.Collections.Generic;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.StateCtrl;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.StateCtrl
{
    /// <summary>
    /// R-ENT 切片 6 部分：ParentVarSet/RootVarSet——helper 把值写到 parent/root 的变量（helper 角色常用回传模式）。
    /// owner 造 helper(state 1000)，helper 在该态用 parentvarset 写 owner 的 var(5)=42。
    /// </summary>
    [TestFixture]
    public sealed class RelayVarSetTests
    {
        const string OwnerCns =
            "[Statedef 0]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State -1, spawn]\ntype = Helper\ntrigger1 = time = 1\nid = 1\nstateno = 1000\n\n" +
            "[Statedef 1000]\ntype = S\nphysics = N\nanim = 0\n\n" +
            "[State 1000, write parent var]\ntype = ParentVarSet\ntrigger1 = time = 1\nv = 5\nvalue = 42\n";
        const string Air = "[Begin Action 0]\n0,0, 0,0, 4\n0,1, 0,0, 4\n";

        static MBattleEngine Engine()
        {
            MCharData data = MCharLoader.Load(new[] { OwnerCns }, OwnerCns, null, Air, null, "Dummy");
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 0), data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }

        static List<MInput> NoInput()
        {
            return new List<MInput> { MInput.None };
        }

        [Test]
        public void ParentVarSet_WritesOwnerVariable()
        {
            MBattleEngine engine = Engine();
            MChar owner = engine.Chars[0];
            for (int f = 0; f < 6; f++) { engine.Tick(NoInput()); }
            Assert.That(engine.Helpers.Count, Is.GreaterThanOrEqualTo(1), "helper 已造出");
            owner.IntVars.TryGetValue(5, out int v);
            Assert.That(v, Is.EqualTo(42), "helper 经 ParentVarSet 写了 owner 的 var(5)");
        }

        [Test]
        public void ParentVarSet_NoParent_NoCrash()
        {
            // 玩家无 parent → ParentVarSet 安全跳过（不崩）。
            RelayVarSetController c = new RelayVarSetController
            {
                Target = MVarTarget.Parent, Index = 0, IsFloat = false,
                Value = new Lockstep.Mugen.Expr.MugenExprCompiler().Compile("1"),
            };
            Assert.DoesNotThrow(() => c.Run(new MChar()));
        }
    }
}
