// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/input.go CommandKey/cmdElem/Command/CommandBuffer structure and semantics.
// Adapted to fixed-point lockstep; directions keep raw U/D/L/R and resolve B/F during matching.
using System.Collections.Generic;
using Lockstep.Core;

namespace Lockstep.Mugen.Command
{
    /// <summary>One frame of raw input bits. Directions keep physical L/R/U/D until command matching resolves B/F.</summary>
    [System.Flags]
    public enum MInput
    {
        None = 0,
        Up = 1, Down = 2, Left = 4, Right = 8,
        A = 16, B = 32, C = 64, X = 128, Y = 256, Z = 512, S = 1024,
        DirMask = Up | Down | Left | Right,
    }

    /// <summary>CMD command key, mapping C# MCommandKey to Ikemen CommandKey.</summary>
    public struct MCommandKey
    {
        public MInput Bits;       // Button bits, or U/D direction bits; horizontal B/F uses IsBack/IsFwd.
        public bool IsBack;       // Back direction, resolved to L/R by facing.
        public bool IsFwd;        // Forward direction, resolved to L/R by facing.
        public bool Release;      // '~' release.
        public bool Hold;         // '/' held state without edge requirement.
        public bool Dollar;       // '$' 4-way/subset match.
        public bool IsButton;     // True for button keys.
        public bool IsNeutral;    // N, no direction.
        public int ChargeTime;    // Required continuous held frames when > 0.
    }

    /// <summary>One command step, mapping C# MCommandStep to Ikemen cmdElem groups.</summary>
    public sealed class MCommandStep
    {
        public List<MCommandKey> Keys = new List<MCommandKey>();
        public bool Greater;      // '>' requires direct continuity from the previous step.
        public bool OrLogic;      // '|' means any key in the step can satisfy it; otherwise all '+' keys are required.
    }

    /// <summary>One parsed command definition, mapping to Ikemen Command.</summary>
    public sealed class MCommandDef
    {
        public string Name;
        public string Motion;
        public List<MCommandStep> Steps = new List<MCommandStep>();
        public int Time = 15;        // Total input window, matching MUGEN command.time default.
        public int BufferTime = 1;   // Active frames after completion, matching command.buffer.time default.
        public int StepTime;         // Per-step wait window; <= 0 uses Time.
    }

    /// <summary>Raw input history buffer; C# compatibility helper that feeds Ikemen-style command matching.</summary>
    public sealed class MCommandBuffer
    {
        readonly MInput[] _buf;
        int _count;

        // Project-specific: C# history capacity used by compatibility command matching.
        public MCommandBuffer(int capacity = 60)
        {
            _buf = new MInput[capacity];
        }

        public int Capacity => _buf.Length;
        public int Count => _count;

        // Project-specific: stores the newest frame input in a rolling history for compatibility matching.
        public void Push(MInput input)
        {
            _buf[_count % _buf.Length] = input;
            _count++;
        }

        /// <summary>Returns input from n frames ago, where 0 is the newest frame.</summary>
        // Project-specific: indexed access to recent frame inputs during compatibility command matching.
        public MInput Ago(int n)
        {
            if (n < 0 || n >= _buf.Length || n >= _count)
            {
                return MInput.None;
            }
            return _buf[(((_count - 1 - n) % _buf.Length) + _buf.Length) % _buf.Length];
        }

        // Project-specific: clears C# raw input history used by compatibility command matching.
        public void Clear()
        {
            _count = 0;
            for (int i = 0; i < _buf.Length; i++)
            {
                _buf[i] = MInput.None;
            }
        }

        // Project-specific: rollback clone of C# input history; Ikemen does not expose a clone API.
        public MCommandBuffer Clone()
        {
            MCommandBuffer c = new MCommandBuffer(_buf.Length) { _count = _count };
            System.Array.Copy(_buf, c._buf, _buf.Length);
            return c;
        }

        // Project-specific: rollback determinism hash over Ikemen-style command input history.
        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(_count);
            for (int i = 0; i < _buf.Length; i++)
            {
                hash.AddInt32((int)_buf[i]);
            }
        }
    }
}
