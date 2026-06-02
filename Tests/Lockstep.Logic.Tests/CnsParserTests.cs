using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Lockstep.Core;
using Lockstep.Game;
using Lockstep.Game.Components;
using Lockstep.Game.Data;
using Lockstep.Game.Systems;
using Lockstep.Import.Cns;

namespace Lockstep.Tests
{
    /// <summary>T2.3：CNS 结构化解析（合成样例端到端 + 真实 common1.cns 容错）。</summary>
    [TestFixture]
    public sealed class CnsParserTests
    {
        const string SampleCns =
            "[Statedef 0]\n" +
            "type = S\n" +
            "physics = S\n" +
            "ctrl = 1\n" +
            "\n" +
            "[State 0, 1]\n" +
            "type = VelSet\n" +
            "trigger1 = Time = 0\n" +
            "x = 0\n" +
            "\n" +
            "[State 0, 2]\n" +
            "type = ChangeState\n" +
            "trigger1 = Time >= 2\n" +
            "value = 20\n" +
            "\n" +
            "[Statedef 20]\n" +
            "type = S\n" +
            "physics = S\n" +
            "\n" +
            "[State 20, 1]\n" +
            "type = VelSet\n" +
            "trigger1 = Time = 0\n" +
            "x = 1\n" +
            "\n" +
            "[State 20, 2]\n" +
            "type = ChangeState\n" +
            "trigger1 = Time >= 3\n" +
            "value = 0\n";

        [Test]
        public void Sample_ParsesStructure()
        {
            CnsParseResult result = CnsParser.Parse(SampleCns);
            Assert.That(result.Warnings, Is.EqualTo(0), "样例全用受支持语法，不应有降级");
            Assert.That(result.States.ContainsKey(0));
            Assert.That(result.States.ContainsKey(20));

            StateDef stand = result.States[0];
            Assert.That(stand.StateType, Is.EqualTo(StateType.Stand));
            Assert.That(stand.Physics, Is.EqualTo(Physics.Stand));
            Assert.That(stand.Ctrl, Is.EqualTo(true));
            Assert.That(stand.Controllers.Length, Is.EqualTo(2));
            Assert.That(stand.Controllers[1].Type, Is.EqualTo(ControllerType.ChangeState));
        }

        [Test]
        public void Sample_DrivesEngineEndToEnd()
        {
            CnsParseResult result = CnsParser.Parse(SampleCns);
            CharacterDef character = new CharacterDef
            {
                Id = 0,
                Name = "FromCns",
                States = result.States,
                Anims = new Dictionary<int, AnimData>(),
                Const = new CharConstants(),
            };

            World world = new World();
            world.Init(1);
            world.GameData = new MugenGameData(new Dictionary<int, CharacterDef> { [0] = character });
            Entity entity = world.CreateEntity();
            entity.Add(new MugenStateC { StateNo = 0, StateType = StateType.Stand, Physics = Physics.Stand, Ctrl = true });
            entity.Add(new TransformC());
            entity.Add(new VelocityC());
            entity.Add(new CharacterRefC { CharacterId = 0 });
            entity.Add(new AnimC());
            entity.Add(new VarsC());
            world.RegisterSystem(new MugenStateMachineSystem());
            world.RegisterSystem(new MugenPhysicsSystem());

            // 解析自 CNS 的状态机应表现出和手搓版相同：3 帧后切 walk 并位移
            world.Tick();
            world.Tick();
            Assert.That(entity.Get<MugenStateC>().StateNo, Is.EqualTo(0));
            world.Tick();
            Assert.That(entity.Get<MugenStateC>().StateNo, Is.EqualTo(20));
            Assert.That(entity.Get<TransformC>().Pos.X.ToInt(), Is.EqualTo(1));
        }

        [Test]
        public void RealCommon1_ParsesTolerantly()
        {
            string path = TestAssets.Common1Cns();
            if (!File.Exists(path))
            {
                Assert.Ignore("MugenSource/common1.cns 不在，跳过");
            }

            CnsParseResult result = CnsParser.ParseFile(path);

            // 容错解析不应崩；应解析出大量状态
            Assert.That(result.States.Count, Is.GreaterThan(10));
            Assert.That(result.States.ContainsKey(0), "应含站立状态 0");

            StateDef stand = result.States[0];
            Assert.That(stand.StateType, Is.EqualTo(StateType.Stand));
            Assert.That(stand.Physics, Is.EqualTo(Physics.Stand));
            Assert.That(stand.Controllers.Length, Is.GreaterThan(0));
            // 真实文件含未支持的 trigger/语法 → 预期有降级警告（诚实边界）
            TestContext.WriteLine("common1.cns 解析：状态 " + result.States.Count + " 个，降级警告 " + result.Warnings);
        }

        [Test]
        public void RealCommon1_StandStateRunsWithoutCrashOrSuicide()
        {
            string path = TestAssets.Common1Cns();
            if (!File.Exists(path))
            {
                Assert.Ignore("MugenSource/common1.cns 不在，跳过");
            }

            CnsParseResult result = CnsParser.ParseFile(path);
            CharacterDef character = new CharacterDef
            {
                Id = 0,
                Name = "kfm",
                States = result.States,
                Anims = new Dictionary<int, AnimData>(),
                Const = new CharConstants(),
            };

            World world = new World();
            world.Init(1);
            world.GameData = new MugenGameData(new Dictionary<int, CharacterDef> { [0] = character });
            Entity entity = world.CreateEntity();
            entity.Add(new MugenStateC { StateNo = 0, StateType = StateType.Stand, Physics = Physics.Stand, Ctrl = true });
            entity.Add(new TransformC());
            entity.Add(new VelocityC());
            entity.Add(new CharacterRefC { CharacterId = 0 });
            entity.Add(new AnimC());
            entity.Add(new VarsC());
            entity.Add(new HealthC { HP = 1000, MaxHP = 1000 });
            world.RegisterSystem(new MugenStateMachineSystem());
            world.RegisterSystem(new MugenPhysicsSystem());
            world.RegisterSystem(new MugenAnimSystem());

            // 运行期容错：未支持 trigger 取 0，alive=1 防自杀。无输入时应稳定站立。
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 30; i++)
                {
                    world.Tick();
                }
            });
            Assert.That(entity.Get<MugenStateC>().StateNo, Is.EqualTo(0),
                "无输入时应稳定停在站立状态 0（不应误判 !alive 自杀到 5050）");
        }
    }
}
