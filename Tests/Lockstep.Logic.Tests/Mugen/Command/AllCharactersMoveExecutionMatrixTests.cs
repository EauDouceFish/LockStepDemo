using System;
using System.Collections.Generic;
using System.IO;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using NUnit.Framework;

namespace Lockstep.Tests.Mugen
{
    [TestFixture]
    public sealed class AllCharactersMoveExecutionMatrixTests
    {
        static IEnumerable<string> CharacterDirectories()
        {
            string root = TestAssets.MugenSourceDir();
            if (!Directory.Exists(root)) { yield break; }
            foreach (string directory in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(directory);
                if (name.StartsWith("_")) { continue; }
                if (MugenCharacterPackageTestLoader.PickMainDef(directory) != null) { yield return directory; }
            }
        }

        [TestCaseSource(nameof(CharacterDirectories))]
        public void LiteralCommandTransitions_AreExecutionProbedFromFreshSessions(string directory)
        {
            MCharData data = MugenCharacterPackageTestLoader.Load(directory);
            string cmdPath = MugenCharacterPackageTestLoader.Resolve(directory, data.Definition.Files.Cmd);
            Assert.That(cmdPath, Is.Not.Null, Path.GetFileName(directory));

            Dictionary<string, List<MCommandDef>> commandsByName = CommandsByName(data.Commands);
            List<CommandTransitionEntry> transitions = CommandTransitionMatrix.Parse(File.ReadAllText(cmdPath));

            int probed = 0;
            int reached = 0;
            int skipped = 0;
            List<string> misses = new List<string>();
            for (int i = 0; i < transitions.Count; i++)
            {
                CommandTransitionEntry entry = transitions[i];
                if (!IsProbeable(data, commandsByName, entry))
                {
                    skipped++;
                    continue;
                }

                probed++;
                if (CanReach(data, commandsByName, entry))
                {
                    reached++;
                }
                else if (misses.Count < 40)
                {
                    misses.Add(entry.Describe());
                }
            }

            TestContext.Progress.WriteLine(Path.GetFileName(directory) + ": probed=" + probed +
                " reached=" + reached + " skipped=" + skipped + " missed=" + (probed - reached));
            for (int i = 0; i < misses.Count; i++)
            {
                TestContext.Progress.WriteLine(Path.GetFileName(directory) + " move-probe-miss: " + misses[i]);
            }

            Assert.That(probed, Is.GreaterThan(0), Path.GetFileName(directory) + " has no probeable transitions");
        }

        static bool IsProbeable(MCharData data, Dictionary<string, List<MCommandDef>> commandsByName,
            CommandTransitionEntry entry)
        {
            if (!entry.TargetStateNo.HasValue || entry.TargetStateNo.Value < 0)
            {
                return false;
            }
            if (!data.States.ContainsKey(entry.TargetStateNo.Value) &&
                !data.CommonStates.ContainsKey(entry.TargetStateNo.Value))
            {
                return false;
            }
            for (int i = 0; i < entry.CommandNames.Count; i++)
            {
                if (entry.CommandNames[i] == "ai" || !commandsByName.ContainsKey(entry.CommandNames[i]))
                {
                    return false;
                }
            }
            return true;
        }

        static Dictionary<string, List<MCommandDef>> CommandsByName(List<MCommandDef> commands)
        {
            Dictionary<string, List<MCommandDef>> result =
                new Dictionary<string, List<MCommandDef>>(StringComparer.Ordinal);
            for (int i = 0; i < commands.Count; i++)
            {
                MCommandDef command = commands[i];
                if (command == null || string.IsNullOrEmpty(command.Name) || command.Steps.Count == 0)
                {
                    continue;
                }
                if (!result.TryGetValue(command.Name, out List<MCommandDef> list))
                {
                    list = new List<MCommandDef>();
                    result[command.Name] = list;
                }
                list.Add(command);
            }
            return result;
        }

        static bool CanReach(MCharData data, Dictionary<string, List<MCommandDef>> commandsByName,
            CommandTransitionEntry entry)
        {
            List<MCommandDef> commandDefinitions = SelectFirstDefinitions(commandsByName, entry.CommandNames);
            if (commandDefinitions.Count == 0)
            {
                return false;
            }

            MBattleEngine engine = CreateEngine(data);
            MChar actor = engine.Chars[0];
            actor.Power = actor.PowerMax;
            List<MInput> sequence = BuildCombinedSequence(commandDefinitions);

            for (int warmup = 0; warmup < 5; warmup++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
            }
            for (int frame = 0; frame < sequence.Count; frame++)
            {
                engine.Tick(new[] { sequence[frame], MInput.None });
                if (actor.StateNo == entry.TargetStateNo.Value)
                {
                    return true;
                }
            }
            for (int tail = 0; tail < 45; tail++)
            {
                engine.Tick(new[] { MInput.None, MInput.None });
                if (actor.StateNo == entry.TargetStateNo.Value)
                {
                    return true;
                }
            }
            return false;
        }

        static List<MCommandDef> SelectFirstDefinitions(Dictionary<string, List<MCommandDef>> commandsByName,
            List<string> commandNames)
        {
            List<MCommandDef> result = new List<MCommandDef>();
            for (int i = 0; i < commandNames.Count; i++)
            {
                if (commandsByName.TryGetValue(commandNames[i], out List<MCommandDef> definitions) &&
                    definitions.Count > 0)
                {
                    result.Add(definitions[0]);
                }
            }
            return result;
        }

        static List<MInput> BuildCombinedSequence(List<MCommandDef> commands)
        {
            List<List<MInput>> prefixes = new List<List<MInput>>();
            int max = 0;
            for (int i = 0; i < commands.Count; i++)
            {
                List<MInput> prefix = ActivationPrefix(commands[i]);
                prefixes.Add(prefix);
                if (prefix.Count > max)
                {
                    max = prefix.Count;
                }
            }

            List<MInput> combined = new List<MInput>();
            for (int frame = 0; frame < max; frame++)
            {
                MInput input = MInput.None;
                for (int p = 0; p < prefixes.Count; p++)
                {
                    int offset = max - prefixes[p].Count;
                    int local = frame - offset;
                    if (local >= 0 && local < prefixes[p].Count)
                    {
                        input |= prefixes[p][local];
                    }
                }
                combined.Add(input);
            }
            return combined;
        }

        static List<MInput> ActivationPrefix(MCommandDef command)
        {
            List<MInput> sequence = CommandInputMatrix.BuildSequence(command, facingRight: true);
            MCommandList list = new MCommandList();
            list.Commands.Add(command);
            for (int i = 0; i < sequence.Count; i++)
            {
                list.Update(sequence[i], facingRight: true);
                if (list.IsActive(command.Name))
                {
                    return sequence.GetRange(0, i + 1);
                }
            }
            return sequence;
        }

        static MBattleEngine CreateEngine(MCharData data)
        {
            MBattleEngine engine = new MBattleEngine();
            MChar left = MCharLoader.SpawnChar(data, 1);
            MChar right = MCharLoader.SpawnChar(data, 2);
            left.Pos = new FVector3(FFloat.FromInt(-30), FFloat.Zero, FFloat.Zero);
            right.Pos = new FVector3(FFloat.FromInt(30), FFloat.Zero, FFloat.Zero);
            right.Facing = -FFloat.One;
            engine.Add(left, data);
            engine.Add(right, data);
            engine.LinkPair();
            engine.StartRound();
            return engine;
        }
    }
}
