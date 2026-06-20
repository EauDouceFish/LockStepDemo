using System.Collections.Generic;

namespace Lockstep.Mugen.Command
{
    /// <summary>
    /// Builds deterministic input scripts from parsed MUGEN command definitions.
    /// Used by headless move probes and later by the Unity exhibit runner.
    /// </summary>
    public static class MCommandInputSynthesizer
    {
        // Project-specific: synthesizes deterministic C# input scripts from Ikemen-style command definitions for tests/previews.
        public static List<MInput> BuildSequence(MCommandDef command, bool facingRight)
        {
            List<MInput> frames = new List<MInput>();
            frames.Add(MInput.None);
            frames.Add(MInput.None);
            for (int i = 0; i < command.Steps.Count; i++)
            {
                AppendStep(frames, command.Steps[i], facingRight);
            }
            frames.Add(MInput.None);
            return frames;
        }

        // Project-specific: merges several synthesized command prefixes so the C# preview harness can activate multi-command transitions.
        public static List<MInput> BuildCombinedSequence(IReadOnlyList<MCommandDef> commands, bool facingRight)
        {
            List<List<MInput>> prefixes = new List<List<MInput>>();
            int max = 0;
            for (int i = 0; i < commands.Count; i++)
            {
                List<MInput> prefix = ActivationPrefix(commands[i], facingRight);
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
                for (int i = 0; i < prefixes.Count; i++)
                {
                    int offset = max - prefixes[i].Count;
                    int local = frame - offset;
                    if (local >= 0 && local < prefixes[i].Count)
                    {
                        input |= prefixes[i][local];
                    }
                }
                combined.Add(input);
            }
            return combined;
        }

        // Project-specific: probes MCommandList until an Ikemen-style command becomes active, then returns the minimal test prefix.
        public static List<MInput> ActivationPrefix(MCommandDef command, bool facingRight)
        {
            List<MInput> sequence = BuildSequence(command, facingRight);
            MCommandList list = new MCommandList();
            list.Commands.Add(command);
            for (int i = 0; i < sequence.Count; i++)
            {
                list.Update(sequence[i], facingRight);
                if (list.IsActive(command.Name))
                {
                    return sequence.GetRange(0, i + 1);
                }
            }
            return sequence;
        }

        // Project-specific: helper emits one scripted input step from src/input.go cmdElem key semantics.
        static void AppendStep(List<MInput> frames, MCommandStep step, bool facingRight)
        {
            List<MCommandKey> keys = SelectedKeys(step);
            MInput preReleaseHold = MInput.None;
            int holdFrames = 0;
            for (int i = 0; i < keys.Count; i++)
            {
                if (!keys[i].Release)
                {
                    continue;
                }

                preReleaseHold |= KeyBits(keys[i], facingRight);
                int required = keys[i].ChargeTime > 0 ? keys[i].ChargeTime : 1;
                if (required > holdFrames)
                {
                    holdFrames = required;
                }
            }

            for (int i = 0; i < holdFrames; i++)
            {
                frames.Add(preReleaseHold);
            }

            MInput current = MInput.None;
            for (int i = 0; i < keys.Count; i++)
            {
                if (keys[i].Release)
                {
                    continue;
                }
                current |= KeyBits(keys[i], facingRight);
            }
            frames.Add(current);
        }

        // Project-specific: helper chooses a deterministic branch for OR command steps; Ikemen receives real player input instead.
        static List<MCommandKey> SelectedKeys(MCommandStep step)
        {
            List<MCommandKey> keys = new List<MCommandKey>();
            if (!step.OrLogic)
            {
                for (int i = 0; i < step.Keys.Count; i++)
                {
                    keys.Add(step.Keys[i]);
                }
                return keys;
            }

            if (step.Keys.Count > 0)
            {
                keys.Add(step.Keys[0]);
            }
            return keys;
        }

        // Ikemen reference: src/input.go CommandKey direction/button bits, with B/F resolved against player facing.
        static MInput KeyBits(MCommandKey key, bool facingRight)
        {
            if (key.IsNeutral)
            {
                return MInput.None;
            }

            MInput result = key.Bits;
            if (key.IsBack)
            {
                result |= facingRight ? MInput.Left : MInput.Right;
            }
            if (key.IsFwd)
            {
                result |= facingRight ? MInput.Right : MInput.Left;
            }
            return result;
        }
    }
}
