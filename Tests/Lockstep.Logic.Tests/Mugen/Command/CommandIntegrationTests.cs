using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Parse;

namespace Lockstep.Tests.Mugen
{
    /// <summary>M6 集成：.cmd 解析 + CommandList 每帧更新 + command="name" trigger 端到端。</summary>
    [TestFixture]
    public sealed class CommandIntegrationTests
    {
        const MInput R = MInput.Right, D = MInput.Down, A = MInput.A;

        [Test]
        public void CmdParser_ReadsCommandSections()
        {
            string cmd =
                "[Command]\nname = \"QCF_a\"\ncommand = D, DF, F, a\ntime = 20\n" +
                "[Command]\nname = \"fwd\"\ncommand = F\n";
            System.Collections.Generic.List<MCommandDef> defs = MugenCmdParser.Parse(cmd);
            Assert.That(defs.Count, Is.EqualTo(2));
            Assert.That(defs[0].Name, Is.EqualTo("QCF_a"));
            Assert.That(defs[0].Steps.Count, Is.EqualTo(4));
            Assert.That(defs[0].Time, Is.EqualTo(20));
            Assert.That(defs[1].Name, Is.EqualTo("fwd"));
        }

        [Test]
        public void CmdParser_AppliesDefaultsRegardlessOfSectionOrder()
        {
            string cmd =
                "[Command]\nname = \"defaulted\"\ncommand = a\n" +
                "[Command]\nname = \"override\"\ncommand = b\ntime = 7\nbuffer.time = 2\n" +
                "[Defaults]\ncommand.time = 30\ncommand.buffer.time = 4\n";

            System.Collections.Generic.List<MCommandDef> defs = MugenCmdParser.Parse(cmd);

            Assert.That(defs[0].Time, Is.EqualTo(30));
            Assert.That(defs[0].BufferTime, Is.EqualTo(4));
            Assert.That(defs[1].Time, Is.EqualTo(7));
            Assert.That(defs[1].BufferTime, Is.EqualTo(2));
        }

        [Test]
        public void CommandList_ActivatesOnMatch()
        {
            MCommandList list = new MCommandList();
            list.Commands.AddRange(MugenCmdParser.Parse("[Command]\nname = \"QCF_a\"\ncommand = D, DF, F, a\n"));

            // 逐帧喂 QCF+a
            list.Update(D, true);
            list.Update(D | R, true);
            list.Update(R, true);
            list.Update(R | A, true);
            Assert.IsTrue(list.IsActive("QCF_a"), "搓出 QCF+a 后命令 active");
            Assert.IsFalse(list.IsActive("nonexist"));
        }

        [Test]
        public void CommandTrigger_CompilesAndEvaluates()
        {
            MChar c = new MChar { CommandList = new MCommandList() };
            c.CommandList.Commands.AddRange(MugenCmdParser.Parse("[Command]\nname = \"fwd\"\ncommand = F\n"));

            BytecodeExp expr = new MugenExprCompiler().Compile("command = \"fwd\"");

            // 未输入 → false
            Assert.IsFalse(expr.Run(c).ToB());
            // 喂一个 F 按下边沿 → active
            c.CommandList.Update(MInput.None, true);
            c.CommandList.Update(R, true);
            Assert.IsTrue(expr.Run(c).ToB(), "command=\"fwd\" 在前进按下后为真");
        }

        [Test]
        public void CommandTrigger_Negated()
        {
            MChar c = new MChar { CommandList = new MCommandList() };
            c.CommandList.Commands.AddRange(MugenCmdParser.Parse("[Command]\nname = \"fwd\"\ncommand = F\n"));
            BytecodeExp expr = new MugenExprCompiler().Compile("command != \"fwd\"");
            Assert.IsTrue(expr.Run(c).ToB(), "无输入时 command!=fwd 为真");
        }

        [Test]
        public void CommandList_DeactivatesAfterBufferTime()
        {
            MCommandList list = new MCommandList();
            list.Commands.AddRange(MugenCmdParser.Parse("[Command]\nname = \"fwd\"\ncommand = F\nbuffer.time = 1\n"));
            list.Update(MInput.None, true);
            list.Update(R, true);
            Assert.IsTrue(list.IsActive("fwd"));
            // 再过一帧无输入，buffer.time=1 应失活
            list.Update(MInput.None, true);
            Assert.IsFalse(list.IsActive("fwd"), "buffer.time=1 后失活");
        }

        [Test]
        public void CommandList_ResetRuntimeClearsActiveCommandAndInputHistory()
        {
            MCommandList list = new MCommandList();
            list.Commands.Add(MCommandParser.Parse("x", "x", bufferTime: 8));

            list.Update(MInput.X, true);
            Assert.That(list.IsActive("x"), Is.True);

            list.ResetRuntime();

            Assert.That(list.IsActive("x"), Is.False, "展馆脚本播放完后必须清掉 active，避免返回 state0 后重复触发同一招。");
            Assert.That(list.ActiveNames(), Is.Empty);
        }

        [Test]
        public void ReleaseCharge_RequiresEnoughChargeAndReleaseEdge()
        {
            MCommandList list = new MCommandList();
            list.Commands.Add(MCommandParser.Parse("release", "~3a"));

            list.Update(A, true);
            list.Update(A, true);
            list.Update(A, true);
            Assert.IsFalse(list.IsActive("release"), "~3a 不能在仍按住时完成");

            list.Update(MInput.None, true);
            Assert.IsTrue(list.IsActive("release"), "蓄满三帧后的释放边沿完成命令");
        }

        [Test]
        public void ReleaseCharge_FailsWhenReleasedTooEarly()
        {
            MCommandList list = new MCommandList();
            list.Commands.Add(MCommandParser.Parse("release", "~3a"));

            list.Update(A, true);
            list.Update(A, true);
            list.Update(MInput.None, true);

            Assert.IsFalse(list.IsActive("release"));
        }

        [Test]
        public void DirectionThenButton_CanCompleteInSameFrame()
        {
            MCommandList list = new MCommandList();
            list.Commands.Add(MCommandParser.Parse("same-frame", "F, a"));

            list.Update(R | A, true);

            Assert.IsTrue(list.IsActive("same-frame"));
        }

        [Test]
        public void Greater_AllowsDelayWithoutUnrelatedInputChanges()
        {
            MCommandList list = new MCommandList();
            list.Commands.Add(MCommandParser.Parse("strict", "a, >b"));

            list.Update(A, true);
            list.Update(A, true);
            list.Update(A | MInput.B, true);

            Assert.IsTrue(list.IsActive("strict"), "'>' 约束输入变化，不是简单的相邻帧检查");
        }

        [Test]
        public void Greater_RejectsUnrelatedInputChange()
        {
            MCommandList list = new MCommandList();
            list.Commands.Add(MCommandParser.Parse("strict", "a, >b"));

            list.Update(A, true);
            list.Update(A | MInput.C, true);
            list.Update(A | MInput.B | MInput.C, true);

            Assert.IsFalse(list.IsActive("strict"));
        }

        [Test]
        public void Clone_DeepCopiesCommandState()
        {
            MChar c = new MChar { CommandList = new MCommandList() };
            c.CommandList.Commands.AddRange(MugenCmdParser.Parse("[Command]\nname = \"fwd\"\ncommand = F\n"));
            c.CommandList.Update(MInput.None, true);
            c.CommandList.Update(R, true);

            MChar clone = c.Clone();
            // 改原 char 命令状态不影响克隆
            c.CommandList.Update(MInput.None, true);
            c.CommandList.Update(MInput.None, true);

            Assert.IsTrue(clone.CommandList.IsActive("fwd"), "克隆保留 active 快照");
        }
    }
}
