// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/input.go CommandList.Step/Command.Step.
using System;
using System.Collections.Generic;
using Lockstep.Core;

namespace Lockstep.Mugen.Command
{
    public sealed class MCommandList
    {
        public List<MCommandDef> Commands = new List<MCommandDef>();
        public MCommandBuffer Buffer = new MCommandBuffer(60);

        MInputBuffer _input = new MInputBuffer();
        List<MCommandRuntime> _runtimes = new List<MCommandRuntime>();
        bool[] _completedThisFrame = new bool[0];
        readonly HashSet<string> _completedNames = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Advances all command runtimes for one frame.</summary>
        // Ikemen reference: src/input.go CommandList.Step updates CommandBuffer and advances each Command.Step.
        public void Update(MInput input, bool facingRight)
        {
            EnsureRuntimes();
            Buffer.Push(input);
            _input.Update(input, facingRight);

            Array.Clear(_completedThisFrame, 0, _completedThisFrame.Length);
            _completedNames.Clear();
            for (int i = 0; i < _runtimes.Count; i++)
            {
                if (!_runtimes[i].Step(_input))
                {
                    continue;
                }
                _completedThisFrame[i] = true;
                _completedNames.Add(_runtimes[i].Definition.Name ?? string.Empty);
            }

            // Aligns Ikemen Command.Clear behavior: once one same-name command completes, clear partial progress in the others.
            if (_completedNames.Count == 0)
            {
                return;
            }
            for (int i = 0; i < _runtimes.Count; i++)
            {
                if (!_completedThisFrame[i] && _completedNames.Contains(_runtimes[i].Definition.Name ?? string.Empty))
                {
                    _runtimes[i].ClearProgress();
                }
            }
        }

        // Ikemen reference: src/input.go CommandList.GetState/CommandList.BufReset active command buffer state.
        public bool IsActive(string name)
        {
            EnsureRuntimes();
            for (int i = 0; i < _runtimes.Count; i++)
            {
                if (_runtimes[i].Active && string.Equals(_runtimes[i].Definition.Name, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // Project-specific: exposes active C# command names for tests and move probes; Ikemen checks command triggers directly.
        public List<string> ActiveNames()
        {
            EnsureRuntimes();
            List<string> names = new List<string>();
            foreach (string completedName in _completedNames)
            {
                if (!names.Contains(completedName))
                {
                    names.Add(completedName);
                }
            }
            for (int i = 0; i < _runtimes.Count; i++)
            {
                if (!_runtimes[i].Active)
                {
                    continue;
                }
                string name = _runtimes[i].Definition.Name ?? string.Empty;
                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }
            return names;
        }

        // Ikemen reference: src/input.go Command.Clear/CommandList.BufReset style command runtime buffer clearing.
        public void ResetRuntime()
        {
            Buffer = new MCommandBuffer(60);
            _input = new MInputBuffer();
            EnsureRuntimes();
            Array.Clear(_completedThisFrame, 0, _completedThisFrame.Length);
            _completedNames.Clear();
            for (int i = 0; i < _runtimes.Count; i++)
            {
                _runtimes[i].Reset();
            }
        }

        // Project-specific: rollback clone copies C# command/input runtime state; Ikemen does not expose a clone API.
        public MCommandList Clone()
        {
            EnsureRuntimes();
            MCommandList clone = new MCommandList
            {
                Commands = Commands,
                Buffer = Buffer.Clone(),
                _input = _input.Clone(),
                _runtimes = new List<MCommandRuntime>(_runtimes.Count),
                _completedThisFrame = new bool[_runtimes.Count],
            };
            for (int i = 0; i < _runtimes.Count; i++)
            {
                clone._runtimes.Add(_runtimes[i].Clone());
            }
            return clone;
        }

        // Project-specific: rollback determinism hash over Ikemen-style command and input buffer state.
        public void WriteHash(ref Hash64 hash)
        {
            EnsureRuntimes();
            Buffer.WriteHash(ref hash);
            _input.WriteHash(ref hash);
            hash.AddInt32(_runtimes.Count);
            for (int i = 0; i < _runtimes.Count; i++)
            {
                _runtimes[i].WriteHash(ref hash);
            }
        }

        // Project-specific: rebuilds per-command C# runtime mirrors when the command definition list changes.
        void EnsureRuntimes()
        {
            bool rebuild = _runtimes.Count != Commands.Count;
            if (!rebuild)
            {
                for (int i = 0; i < Commands.Count; i++)
                {
                    if (!ReferenceEquals(_runtimes[i].Definition, Commands[i]))
                    {
                        rebuild = true;
                        break;
                    }
                }
            }
            if (!rebuild)
            {
                return;
            }

            _runtimes = new List<MCommandRuntime>(Commands.Count);
            _completedThisFrame = new bool[Commands.Count];
            for (int i = 0; i < Commands.Count; i++)
            {
                _runtimes.Add(new MCommandRuntime(Commands[i]));
            }
        }
    }
}
