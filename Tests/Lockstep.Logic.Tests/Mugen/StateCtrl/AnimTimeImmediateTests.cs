// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/anim.go (changeAnimEx reinitializes current animation immediately) and src/bytecode.go ChangeAnim/ChangeAnim2.
using System.Collections.Generic;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.StateCtrl
{
    [TestFixture]
    public sealed class AnimTimeImmediateTests
    {
        static readonly MugenExprCompiler Compiler = new MugenExprCompiler();

        static BytecodeExp Expression(string text)
        {
            return Compiler.Compile(text);
        }

        static MAnimData Anim(int no, params int[] times)
        {
            MAnimFrame[] frames = new MAnimFrame[times.Length];
            for (int i = 0; i < times.Length; i++)
            {
                frames[i] = new MAnimFrame { SpriteGroup = no, SpriteImage = i, Time = times[i] };
            }
            MAnimData anim = new MAnimData { No = no, Frames = frames, LoopStart = 0 };
            anim.ComputePacing();
            return anim;
        }

        static Dictionary<int, MAnimData> Table(params MAnimData[] anims)
        {
            Dictionary<int, MAnimData> table = new Dictionary<int, MAnimData>();
            for (int i = 0; i < anims.Length; i++)
            {
                table[anims[i].No] = anims[i];
            }
            return table;
        }

        [Test]
        public void ChangeAnim_RefreshesAnimTimeBeforeAnimationPhase()
        {
            Dictionary<int, MAnimData> table = Table(Anim(0, 3), Anim(200, 4, 6));
            MChar character = new MChar { AnimNo = 0, AnimTable = table, AnimTime = 0, AnimElemNo = 9 };

            // oracle: bytecode.go ChangeAnim calls changeAnimEx; anim.go reinitializes derived animation state immediately.
            new ChangeAnimController { Value = Expression("200") }.Run(character);

            Assert.That(character.AnimNo, Is.EqualTo(200));
            Assert.That(character.AnimRunningNo, Is.EqualTo(200));
            Assert.That(character.AnimElemNo, Is.EqualTo(1));
            Assert.That(character.AnimTime, Is.EqualTo(-10));
        }

        [Test]
        public void StateDefAnim_RefreshesAnimTimeDuringStateInit()
        {
            Dictionary<int, MAnimData> table = Table(Anim(0, 3), Anim(210, 5));
            MChar character = new MChar { AnimNo = 0, AnimTable = table, AnimTime = 0 };
            MStateDef state = new MStateDef { Anim = Expression("210") };

            state.RunInit(character);

            Assert.That(character.AnimNo, Is.EqualTo(210));
            Assert.That(character.AnimRunningNo, Is.EqualTo(210));
            Assert.That(character.AnimTime, Is.EqualTo(-5));
        }

        [Test]
        public void ChangeAnim2_RefreshesStateOwnerAnimAndElemImmediately()
        {
            Dictionary<int, MAnimData> ownerTable = Table(Anim(900, 3, 7, 2));
            MChar owner = new MChar { AnimTable = ownerTable };
            MChar target = new MChar { AnimNo = 0, StateOwner = owner };

            new ChangeAnim2Controller
            {
                Value = Expression("900"),
                Elem = Expression("2"),
                ElemTime = Expression("1"),
            }.Run(target);

            Assert.That(target.AnimTable, Is.SameAs(ownerTable));
            Assert.That(target.AnimRunningNo, Is.EqualTo(900));
            Assert.That(target.AnimElemNo, Is.EqualTo(2));
            Assert.That(target.AnimElemTime, Is.EqualTo(1));
            Assert.That(target.AnimCurTime, Is.EqualTo(4));
            Assert.That(target.AnimTime, Is.EqualTo(-8));
        }
    }
}
