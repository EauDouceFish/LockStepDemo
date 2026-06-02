using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Lockstep.Import.Cmd;
using Lockstep.Game.Data;

namespace Lockstep.Tests
{
    /// <summary>T2.2：CMD 搓招指令解析（合成样例 + 真实 Terrarian.cmd）。</summary>
    [TestFixture]
    public sealed class CmdParserTests
    {
        const string Sample =
            "[Command]\n" +
            "name = \"QCF_a\"\n" +
            "command = D, DF, F, a\n" +
            "time = 30\n" +
            "\n" +
            "[Command]\n" +
            "name = \"hold_back\"\n" +
            "command = /B\n" +
            "\n" +
            "[Statedef -1]\n" +   // 非 Command 段应被忽略
            "[State -1, 1]\n" +
            "type = ChangeState\n";

        [Test]
        public void Sample_ParsesCommandsIgnoresOtherSections()
        {
            List<CommandData> commands = CmdParser.Parse(Sample);
            Assert.That(commands.Count, Is.EqualTo(2));

            CommandData qcf = commands[0];
            Assert.That(qcf.Name, Is.EqualTo("QCF_a"));
            Assert.That(qcf.Motion, Is.EqualTo(new[]
            {
                InputSymbol.Down, InputSymbol.DownFwd, InputSymbol.Fwd, InputSymbol.BtnA,
            }));
            Assert.That(qcf.TimeWindow, Is.EqualTo(30));

            // /B 的修饰符被剥离，得到方向 Back
            Assert.That(commands[1].Motion, Is.EqualTo(new[] { InputSymbol.Back }));
        }

        [Test]
        public void DirectionUpperVsButtonLower_AreDistinguished()
        {
            // "B" = 方向 Back；"b" = 按钮 b
            List<CommandData> commands = CmdParser.Parse(
                "[Command]\nname = \"t\"\ncommand = B, b\n");
            Assert.That(commands[0].Motion, Is.EqualTo(new[] { InputSymbol.Back, InputSymbol.BtnB }));
        }

        [Test]
        public void RealTerrarian_ParsesAndHasEspecial1()
        {
            string path = TestAssets.Cmd();
            if (!File.Exists(path))
            {
                Assert.Ignore("MugenSource/Terrarian.cmd 不在，跳过");
            }

            List<CommandData> commands = CmdParser.ParseFile(path);
            Assert.That(commands.Count, Is.GreaterThan(0));

            CommandData especial = commands.Find(c => c.Name == "ESPECIAL 1");
            Assert.That(especial, Is.Not.Null, "应含 ESPECIAL 1 指令");
            Assert.That(especial.Motion, Is.EqualTo(new[]
            {
                InputSymbol.Down, InputSymbol.DownFwd, InputSymbol.Fwd, InputSymbol.BtnA,
            }));
            Assert.That(especial.TimeWindow, Is.EqualTo(30));
        }
    }
}
