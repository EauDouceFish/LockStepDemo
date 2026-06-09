// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (envColor/remapPal/victoryQuote/text/modifyText/removeText/clipboard/forceFeedback Run).
using Lockstep.Core;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.StateCtrl;
using NUnit.Framework;

namespace Lockstep.Logic.Tests.Mugen.StateCtrl
{
    [TestFixture]
    public sealed class PresentationControllerTests
    {
        static readonly MugenExprCompiler Compiler = new MugenExprCompiler();

        static BytecodeExp E(string text)
        {
            return Compiler.Compile(text);
        }

        static MChar NewChar()
        {
            return new MChar { Id = 1, Facing = FFloat.One, World = new MEntityWorld() };
        }

        [Test]
        public void VictoryQuote_WritesWinQuote()
        {
            MChar c = NewChar();
            new VictoryQuoteController { Value = E("3") }.Run(c);
            Assert.That(c.WinQuote, Is.EqualTo(3));
        }

        [Test]
        public void EnvColor_EmitsEventWithRgbTimeUnder()
        {
            MChar c = NewChar();
            new EnvColorController
            {
                Value = new[] { E("10"), E("20"), E("30") },
                Time = E("8"),
                Under = E("1"),
            }.Run(c);

            Assert.That(c.World.Events.Visuals.Count, Is.EqualTo(1));
            MVisualEvent ev = c.World.Events.Visuals[0];
            Assert.That(ev.Type, Is.EqualTo(MVisualEventType.EnvColor));
            Assert.That(ev.Value0, Is.EqualTo(10));
            Assert.That(ev.Value1, Is.EqualTo(20));
            Assert.That(ev.Value2, Is.EqualTo(30));
            Assert.That(ev.Time, Is.EqualTo(8));
            Assert.That(ev.Under, Is.True);
        }

        [Test]
        public void EnvColor_DefaultsToWhiteWhenNoValue()
        {
            MChar c = NewChar();
            new EnvColorController().Run(c);
            MVisualEvent ev = c.World.Events.Visuals[0];
            Assert.That(ev.Value0, Is.EqualTo(255));
            Assert.That(ev.Value1, Is.EqualTo(255));
            Assert.That(ev.Value2, Is.EqualTo(255));
            Assert.That(ev.Time, Is.EqualTo(1));
        }

        [Test]
        public void RemapPal_AddsEntry_AndUpdatesInPlaceBySource()
        {
            MChar c = NewChar();
            new RemapPalController { Source = new[] { E("1"), E("0") }, Dest = new[] { E("2"), E("0") } }.Run(c);
            Assert.That(c.RemapPalTable.Count, Is.EqualTo(1));
            Assert.That(c.RemapPalTable[0].DstGroup, Is.EqualTo(2));

            // 同 source 再次映射 → 就地覆盖，不新增
            new RemapPalController { Source = new[] { E("1"), E("0") }, Dest = new[] { E("5"), E("0") } }.Run(c);
            Assert.That(c.RemapPalTable.Count, Is.EqualTo(1));
            Assert.That(c.RemapPalTable[0].DstGroup, Is.EqualTo(5));

            // 不同 source → 追加
            new RemapPalController { Source = new[] { E("3"), E("1") }, Dest = new[] { E("4"), E("0") } }.Run(c);
            Assert.That(c.RemapPalTable.Count, Is.EqualTo(2));
        }

        [Test]
        public void RemapPal_SurvivesCloneAndAffectsHash()
        {
            MChar c = NewChar();
            new RemapPalController { Source = new[] { E("1"), E("0") }, Dest = new[] { E("7"), E("2") } }.Run(c);

            MChar clone = c.Clone();
            Assert.That(clone.RemapPalTable.Count, Is.EqualTo(1));
            Assert.That(clone.RemapPalTable[0].DstGroup, Is.EqualTo(7));
            Assert.That(clone.RemapPalTable[0].DstIndex, Is.EqualTo(2));

            Hash64 h1 = Hash64.Create();
            c.WriteHash(ref h1);
            clone.ApplyRemapPal(9, 9, 9, 9);
            Hash64 h2 = Hash64.Create();
            clone.WriteHash(ref h2);
            Assert.That(h2.Value, Is.Not.EqualTo(h1.Value));
        }

        [Test]
        public void Text_EmitsTextEventWithIdAndRemovetime()
        {
            MChar c = NewChar();
            new TextController { Id = E("4"), Removetime = E("60"), Position = new[] { E("10"), E("20") } }.Run(c);
            MVisualEvent ev = c.World.Events.Visuals[0];
            Assert.That(ev.Type, Is.EqualTo(MVisualEventType.Text));
            Assert.That(ev.Id, Is.EqualTo(4));
            Assert.That(ev.Time, Is.EqualTo(60));
        }

        [Test]
        public void ModifyText_AndRemoveText_EmitDistinctEvents()
        {
            MChar c = NewChar();
            new ModifyTextController { Index = E("0"), Id = E("4") }.Run(c);
            new RemoveTextController { Id = E("4"), Index = E("1") }.Run(c);
            Assert.That(c.World.Events.Visuals[0].Type, Is.EqualTo(MVisualEventType.ModifyText));
            Assert.That(c.World.Events.Visuals[1].Type, Is.EqualTo(MVisualEventType.RemoveText));
            Assert.That(c.World.Events.Visuals[1].Id, Is.EqualTo(4));
            Assert.That(c.World.Events.Visuals[1].Index, Is.EqualTo(1));
        }

        [Test]
        public void Clipboard_DisplayAppendClear_EmitDistinctEvents()
        {
            MChar c = NewChar();
            new DisplayToClipboardController().Run(c);
            new AppendToClipboardController().Run(c);
            new ClearClipboardController().Run(c);
            Assert.That(c.World.Events.Visuals[0].Type, Is.EqualTo(MVisualEventType.DisplayToClipboard));
            Assert.That(c.World.Events.Visuals[1].Type, Is.EqualTo(MVisualEventType.AppendToClipboard));
            Assert.That(c.World.Events.Visuals[2].Type, Is.EqualTo(MVisualEventType.ClearClipboard));
        }

        [Test]
        public void ForceFeedback_EmitsEvent()
        {
            MChar c = NewChar();
            new ForceFeedbackController { Time = E("30"), Intensity = E("128") }.Run(c);
            MVisualEvent ev = c.World.Events.Visuals[0];
            Assert.That(ev.Type, Is.EqualTo(MVisualEventType.ForceFeedback));
            Assert.That(ev.Time, Is.EqualTo(30));
            Assert.That(ev.Value0, Is.EqualTo(128));
        }
    }
}
