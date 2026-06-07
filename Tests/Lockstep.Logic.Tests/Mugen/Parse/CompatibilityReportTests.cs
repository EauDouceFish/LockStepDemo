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
            MCompatibilityReport report = new MCompatibilityReport();
            MugenCnsParser.Parse(
                "[Statedef 0]\ntype=S\n[State 0, render]\ntype = AngleDraw\ntrigger1 = 1\nvalue = 45\n",
                report);

            Assert.That(report.UnknownControllers, Is.Empty);
            Assert.That(report.ParsedOnlyControllers["angledraw"], Is.EqualTo(1));
        }
    }
}
