// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/compiler.go redirect parsing + src/bytecode.go OC_helper/OC_partner + src/char.go helperTrigger/partner.
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Math;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.Expr
{
    [TestFixture]
    public sealed class HelperRedirectTests
    {
        static int Eval(string expression, MChar character)
        {
            return new MugenExprCompiler().Compile(expression).Run(character).ToI();
        }

        static MChar OwnerWithHelpers()
        {
            MEntityWorld world = new MEntityWorld();
            MChar owner = new MChar { Id = 1, World = world };
            MChar first = new MChar { Id = 100, IsHelper = true, HelperType = 5, Parent = owner, Root = owner };
            MChar second = new MChar { Id = 101, IsHelper = true, HelperType = 5, Parent = owner, Root = owner };
            MChar other = new MChar { Id = 102, IsHelper = true, HelperType = 8, Parent = owner, Root = owner };
            first.IntVars[3] = 11;
            second.IntVars[3] = 22;
            other.IntVars[3] = 88;
            world.Helpers.Add(first);
            world.Helpers.Add(second);
            world.Helpers.Add(other);
            return owner;
        }

        [Test]
        public void HelperRedirect_DefaultsToAnyIdAndFirstIndex()
        {
            MChar owner = OwnerWithHelpers();

            // oracle: compiler.go helper redirect defaults id=-1,index=0; char.go:4732 id<=0 matches any helper.
            Assert.That(Eval("helper, var(3)", owner), Is.EqualTo(11));
        }

        [Test]
        public void HelperRedirect_FiltersByIdAndMatchIndex()
        {
            MChar owner = OwnerWithHelpers();

            // oracle: char.go:4732 helperTrigger counts only helpers matching id, then applies index among matches.
            Assert.That(Eval("helper(5), var(3)", owner), Is.EqualTo(11));
            Assert.That(Eval("helper(5, 1), var(3)", owner), Is.EqualTo(22));
            Assert.That(new MugenExprCompiler().Compile("helper(7), var(3)").Run(owner).IsUndefined(), Is.True);
        }

        [Test]
        public void TargetRedirect_FiltersByIdAndMatchIndexOnRealChar()
        {
            MChar owner = new MChar();
            MChar first = new MChar { Id = 7, Life = 111 };
            MChar second = new MChar { Id = 7, Life = 222 };
            MChar other = new MChar { Id = 9, Life = 999 };
            owner.Targets.Add(first);
            owner.Targets.Add(second);
            owner.Targets.Add(other);

            Assert.That(Eval("target(7, 0), life", owner), Is.EqualTo(111));
            Assert.That(Eval("target(7, 1), life", owner), Is.EqualTo(222));
            Assert.That(Eval("target(9), life", owner), Is.EqualTo(999));
            Assert.That(new MugenExprCompiler().Compile("target(7, 2), life").Run(owner).IsUndefined(), Is.True);
        }

        [Test]
        public void SysVarNamespaces_AreIndependentOnRealChar()
        {
            MChar owner = new MChar();
            owner.IntVars[1] = 10;
            owner.SysIntVars[1] = 20;
            owner.FloatVars[1] = FFloat.FromInt(30);
            owner.SysFloatVars[1] = FFloat.FromInt(40);

            Assert.That(Eval("var(1) + sysvar(1) + fvar(1) + sysfvar(1)", owner), Is.EqualTo(100));
        }

        [Test]
        public void StateOwnerRedirect_UsesCustomStateOwner()
        {
            MChar owner = new MChar { Life = 777 };
            MChar controlled = new MChar { Life = 100, StateOwner = owner };

            Assert.That(Eval("stateowner, life", controlled), Is.EqualTo(777));
            Assert.That(new MugenExprCompiler().Compile("stateowner, life").Run(new MChar()).IsUndefined(), Is.True);
        }

        [Test]
        public void PartnerRedirect_UsesExplicitPartnerLink()
        {
            MChar partner = new MChar { Life = 456 };
            MChar owner = new MChar { Partner = partner };

            // oracle: compiler.go partner redirect has optional index default 0; runtime redirects to the selected partner.
            Assert.That(Eval("partner, life", owner), Is.EqualTo(456));
            Assert.That(Eval("partner(0), life", owner), Is.EqualTo(456));
        }
    }
}
