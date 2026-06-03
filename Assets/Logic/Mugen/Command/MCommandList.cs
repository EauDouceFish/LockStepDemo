// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/input.go (CommandList Step/BufferedCmd 激活与缓冲)。
// 每帧 Update：递减各命令激活计时 → 压入本帧输入 → 重匹配 → 命中则置 active=bufferTime。
// command="name" 经 IsActive 查询。挂回滚：Clone/WriteHash。See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Core;

namespace Lockstep.Mugen.Command
{
    public sealed class MCommandList
    {
        public List<MCommandDef> Commands = new List<MCommandDef>();
        public MCommandBuffer Buffer = new MCommandBuffer(60);

        // 各命令名的剩余激活帧（>0 即 active）。同名多条命令 OR。
        readonly Dictionary<string, int> _active = new Dictionary<string, int>();

        /// <summary>每帧推进：input=本帧输入，facingRight=角色是否面右。</summary>
        public void Update(MInput input, bool facingRight)
        {
            List<string> keys = new List<string>(_active.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int v = _active[keys[i]] - 1;
                _active[keys[i]] = v < 0 ? 0 : v;
            }

            Buffer.Push(input);

            for (int i = 0; i < Commands.Count; i++)
            {
                MCommandDef cmd = Commands[i];
                if (MCommandMatcher.Matches(cmd, Buffer, facingRight))
                {
                    int t = cmd.BufferTime > 0 ? cmd.BufferTime : 1;
                    if (!_active.TryGetValue(cmd.Name, out int cur) || cur < t)
                    {
                        _active[cmd.Name] = t;
                    }
                }
            }
        }

        public bool IsActive(string name)
        {
            return _active.TryGetValue(name, out int v) && v > 0;
        }

        public MCommandList Clone()
        {
            MCommandList c = new MCommandList
            {
                Commands = Commands,   // 命令定义不可变，浅引用即可
                Buffer = Buffer.Clone(),
            };
            foreach (KeyValuePair<string, int> kv in _active)
            {
                c._active[kv.Key] = kv.Value;
            }
            return c;
        }

        public void WriteHash(ref Hash64 hash)
        {
            Buffer.WriteHash(ref hash);
            List<string> keys = new List<string>(_active.Keys);
            keys.Sort(System.StringComparer.Ordinal);
            hash.AddInt32(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                for (int k = 0; k < keys[i].Length; k++)
                {
                    hash.AddInt32(keys[i][k]);
                }
                hash.AddInt32(_active[keys[i]]);
            }
        }
    }
}
