using NUnit.Framework;
using Lockstep.Mugen.Parse;

namespace Lockstep.Tests.Mugen.Parse
{
    [TestFixture]
    public sealed class CompatibilityReportTests
    {
        [Test]
        public void UnknownController_IsReportedInsteadOfSilentlyDiscarded()
        {
            MCompatibilityReport report = new MCompatibilityReport();
            MugenCnsParser.Parse(
                "[Statedef 0]\ntype=S\n[State 0, future]\ntype = FutureController\ntrigger1 = 1\n",
                report);

            Assert.That(report.UnknownControllers["futurecontroller"], Is.EqualTo(1));
            Assert.That(report.IsClean, Is.False);
        }

        [Test]
        public void ParsedButUnimplementedController_IsReportedSeparately()
        {
            // ModifyStageVar 仍是真正的 parse-only（无舞台子系统建模）；AngleDraw 等绘制控制器已实现，不再计入此表。
            MCompatibilityReport report = new MCompatibilityReport();
            MugenCnsParser.Parse(
                "[Statedef 0]\ntype=S\n[State 0, stage]\ntype = ModifyStageVar\ntrigger1 = 1\n",
                report);

            Assert.That(report.UnknownControllers, Is.Empty);
            Assert.That(report.ParsedOnlyControllers["modifystagevar"], Is.EqualTo(1));
        }
    }
}
