// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/input.go (CommandStepKey/CommandStep/Command/InputBuffer 结构与语义)。
// Adapted to fixed-point lockstep. 方向存原始 U/D/L/R(B/F 在匹配时按朝向解析)。See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Core;

namespace Lockstep.Mugen.Command
{
    /// <summary>一帧输入位（方向存原始 L/R/U/D；B/F 在匹配时按朝向转）。</summary>
    [System.Flags]
    public enum MInput
    {
        None = 0,
        Up = 1, Down = 2, Left = 4, Right = 8,
        A = 16, B = 32, C = 64, X = 128, Y = 256, Z = 512, S = 1024,
        DirMask = Up | Down | Left | Right,
    }

    /// <summary>CMD 命令键(对应 Ikemen CommandStepKey)：方向/按钮 + 修饰(release/hold/4way/charge)。</summary>
    public struct MCommandKey
    {
        public MInput Bits;       // 按钮位，或方向的 U/D 分量；左右用 IsBack/IsFwd 表达(按朝向解析)
        public bool IsBack;       // 方向含 Back（按朝向解析为 L 或 R）
        public bool IsFwd;        // 方向含 Fwd
        public bool Release;      // '~' 释放
        public bool Hold;         // '/' 按住(不要求边沿)
        public bool Dollar;       // '$' 4way(子集匹配，忽略另一轴)
        public bool IsButton;     // 是否按钮键
        public bool IsNeutral;    // N（无方向）
        public int ChargeTime;    // 蓄力：键须连续按住 N 帧（>0 时生效）
    }

    /// <summary>命令的一步(对应 Ikemen CommandStep)：多键 AND(+)；greater='>' 禁止中间无关输入变化。</summary>
    public sealed class MCommandStep
    {
        public List<MCommandKey> Keys = new List<MCommandKey>();
        public bool Greater;      // '>' 须紧接上一步
        public bool OrLogic;      // '|' 步内任一键满足即可(否则 '+' 全部满足)
    }

    /// <summary>一条命令(对应 Ikemen Command)。</summary>
    public sealed class MCommandDef
    {
        public string Name;
        public string Motion;
        public List<MCommandStep> Steps = new List<MCommandStep>();
        public int Time = 15;        // 完成全部步骤的总帧窗(MUGEN command.time 默认 15)
        public int BufferTime = 1;   // 完成后保持 active 的缓冲帧(MUGEN command.buffer.time 默认 1)
        public int StepTime;         // 单步最大等待时间；<=0 时使用 Time
    }

    /// <summary>输入环形缓冲(对应 Ikemen InputBuffer)：保存最近若干帧输入，支持按"帧前偏移"取值。</summary>
    public sealed class MCommandBuffer
    {
        readonly MInput[] _buf;
        int _count;   // 累计推入帧数

        public MCommandBuffer(int capacity = 60)
        {
            _buf = new MInput[capacity];
        }

        public int Capacity => _buf.Length;
        public int Count => _count;

        public void Push(MInput input)
        {
            _buf[_count % _buf.Length] = input;
            _count++;
        }

        /// <summary>取 n 帧前的输入(0=最新)。越界返回 None。</summary>
        public MInput Ago(int n)
        {
            if (n < 0 || n >= _buf.Length || n >= _count)
            {
                return MInput.None;
            }
            return _buf[(((_count - 1 - n) % _buf.Length) + _buf.Length) % _buf.Length];
        }

        public void Clear()
        {
            _count = 0;
            for (int i = 0; i < _buf.Length; i++)
            {
                _buf[i] = MInput.None;
            }
        }

        public MCommandBuffer Clone()
        {
            MCommandBuffer c = new MCommandBuffer(_buf.Length) { _count = _count };
            System.Array.Copy(_buf, c._buf, _buf.Length);
            return c;
        }

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
