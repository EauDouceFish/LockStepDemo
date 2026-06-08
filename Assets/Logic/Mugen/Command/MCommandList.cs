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

        /// <summary>每帧推进所有命令的独立状态机。</summary>
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

            // 对齐 Ikemen ClearName：同名命令的一条完成后，清掉其余候选的部分进度，保留已有 buffer。
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
