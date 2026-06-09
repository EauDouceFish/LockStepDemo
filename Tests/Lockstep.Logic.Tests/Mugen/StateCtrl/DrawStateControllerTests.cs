// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (trans/sprPriority/offset/angleDraw/angleSet/angleAdd/angleMul Run) +
//         src/char.go:11542 (per-frame draw-state reset) + src/image.go:18 (TransType).
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.StateCtrl;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.StateCtrl
{
    [TestFixture]
    public sealed class DrawStateControllerTests
    {
        static readonly MugenExprCompiler Compiler = new MugenExprCompiler();

        static BytecodeExp E(string text)
        {
            return Compiler.Compile(text);
        }

        static MChar NewChar()
        {
            return new MChar { Facing = FFloat.One };
        }

        [Test]
        public void AngleSet_SetsAllThreeComponents_UnsetDefaultsToZero()
        {
            MChar c = NewChar();
            c.AngleRotX = FFloat.FromInt(7);   // 旧值应被 AngleSet 无参分量清零

            new AngleSetController { Value = E("90") }.Run(c);

            Assert.That(c.AngleRot, Is.EqualTo(FFloat.FromInt(90)));
            Assert.That(c.AngleRotX, Is.EqualTo(FFloat.Zero));
            Assert.That(c.AngleRotY, Is.EqualTo(FFloat.Zero));
        }

        [Test]
        public void AngleAdd_OnlyAddsProvidedComponent()
        {
            MChar c = NewChar();
            c.AngleRot = FFloat.FromInt(30);
            c.AngleRotX = FFloat.FromInt(5);

            new AngleAddController { Value = E("15") }.Run(c);

            Assert.That(c.AngleRot, Is.EqualTo(FFloat.FromInt(45)));
            Assert.That(c.AngleRotX, Is.EqualTo(FFloat.FromInt(5)));   // x 未提供 → 不变
        }

        [Test]
        public void AngleMul_UnsetComponentMultipliesByZero()
        {
            MChar c = NewChar();
            c.AngleRot = FFloat.FromInt(40);
            c.AngleRotX = FFloat.FromInt(10);

            new AngleMulController { Value = E("2") }.Run(c);

            Assert.That(c.AngleRot, Is.EqualTo(FFloat.FromInt(80)));
            Assert.That(c.AngleRotX, Is.EqualTo(FFloat.Zero));   // Mugen 行为：无参分量 ×0
        }

        [Test]
        public void AngleDraw_SetsFlagAndMultipliesScale()
        {
            MChar c = NewChar();

            new AngleDrawController { Value = E("45"), Scale = new[] { E("2"), E("3") } }.Run(c);

            Assert.That(c.AngleDraw, Is.True);
            Assert.That(c.AngleRot, Is.EqualTo(FFloat.FromInt(45)));
            Assert.That(c.AngleDrawScaleX, Is.EqualTo(FFloat.FromInt(2)));   // 1 * 2
            Assert.That(c.AngleDrawScaleY, Is.EqualTo(FFloat.FromInt(3)));   // 1 * 3
        }

        [Test]
        public void Trans_AddType_DefaultAlphaIs255And255()
        {
            MChar c = NewChar();

            new TransController { TransType = MTransType.Add, DefaultSrc = 255, DefaultDst = 255 }.Run(c);

            Assert.That(c.Trans, Is.EqualTo(MTransType.Add));
            Assert.That(c.AlphaSrc, Is.EqualTo(255));
            Assert.That(c.AlphaDst, Is.EqualTo(255));
        }

        [Test]
        public void Trans_AlphaParameterOverridesDefaultAndClamps()
        {
            MChar c = NewChar();

            new TransController
            {
                TransType = MTransType.Add,
                DefaultSrc = 255,
                DefaultDst = 255,
                Alpha = new[] { E("200"), E("999") },   // 999 应夹取到 255
            }.Run(c);

            Assert.That(c.AlphaSrc, Is.EqualTo(200));
            Assert.That(c.AlphaDst, Is.EqualTo(255));
        }

        [Test]
        public void SprPriority_WritesPriorityAndLayer_PersistsAcrossReset()
        {
            MChar c = NewChar();

            new SprPriorityController { Value = E("5"), LayerNo = E("1") }.Run(c);
            Assert.That(c.SprPriority, Is.EqualTo(5));
            Assert.That(c.LayerNo, Is.EqualTo(1));

            // SprPriority 不在每帧重置块 → 保留
            c.ResetFrameDrawState();
            Assert.That(c.SprPriority, Is.EqualTo(5));
            Assert.That(c.LayerNo, Is.EqualTo(1));
        }

        [Test]
        public void Offset_WritesProvidedAxesOnly()
        {
            MChar c = NewChar();
            c.OffsetY = FFloat.FromInt(9);

            new OffsetController { XOffset = E("12") }.Run(c);

            Assert.That(c.OffsetX, Is.EqualTo(FFloat.FromInt(12)));
            Assert.That(c.OffsetY, Is.EqualTo(FFloat.FromInt(9)));   // y 未提供 → 不变
        }

        [Test]
        public void ResetFrameDrawState_ResetsPerFrameFields_KeepsAngleRot()
        {
            MChar c = NewChar();
            c.AngleRot = FFloat.FromInt(33);
            c.AngleDraw = true;
            c.AngleDrawScaleX = FFloat.FromInt(4);
            c.Trans = MTransType.Sub;
            c.AlphaSrc = 100;
            c.AlphaDst = 50;
            c.OffsetX = FFloat.FromInt(7);

            c.ResetFrameDrawState();

            Assert.That(c.AngleDraw, Is.False);
            Assert.That(c.AngleDrawScaleX, Is.EqualTo(FFloat.One));
            Assert.That(c.Trans, Is.EqualTo(MTransType.Default));
            Assert.That(c.AlphaSrc, Is.EqualTo(255));
            Assert.That(c.AlphaDst, Is.EqualTo(0));
            Assert.That(c.OffsetX, Is.EqualTo(FFloat.Zero));
            Assert.That(c.AngleRot, Is.EqualTo(FFloat.FromInt(33)));   // anglerot 跨帧保留
        }

        [Test]
        public void DrawState_SurvivesCloneAndAffectsHash()
        {
            MChar c = NewChar();
            new AngleSetController { Value = E("60") }.Run(c);
            new SprPriorityController { Value = E("3") }.Run(c);
            new TransController { TransType = MTransType.Add, DefaultSrc = 128, DefaultDst = 64 }.Run(c);
            c.OffsetX = FFloat.FromInt(8);

            MChar clone = c.Clone();
            Assert.That(clone.AngleRot, Is.EqualTo(FFloat.FromInt(60)));
            Assert.That(clone.SprPriority, Is.EqualTo(3));
            Assert.That(clone.Trans, Is.EqualTo(MTransType.Add));
            Assert.That(clone.AlphaSrc, Is.EqualTo(128));
            Assert.That(clone.OffsetX, Is.EqualTo(FFloat.FromInt(8)));

            Hash64 h1 = Hash64.Create();
            c.WriteHash(ref h1);
            Hash64 h2 = Hash64.Create();
            clone.WriteHash(ref h2);
            Assert.That(h1.Value, Is.EqualTo(h2.Value));

            // 改一个绘制字段 → 哈希变化
            clone.SprPriority = 9;
            Hash64 h3 = Hash64.Create();
            clone.WriteHash(ref h3);
            Assert.That(h3.Value, Is.Not.EqualTo(h1.Value));
        }
    }
}
