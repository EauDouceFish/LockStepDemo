using System;
using System.Collections.Generic;

namespace Lockstep.Import.Snd
{
    public sealed class SndException : Exception
    {
        public SndException(string message) : base(message) { }
    }

    public sealed class SndEntry
    {
        public int Group;
        public int Number;
        public int WaveOffset;
        public int WaveLength;
    }

    /// <summary>Parsed SND directory. Wave data remains in the source byte array and is copied only on demand.</summary>
    public sealed class SndFile
    {
        public ushort Version;
        public ushort Version2;
        public readonly List<SndEntry> Entries = new List<SndEntry>();
        public readonly Dictionary<long, SndEntry> Table = new Dictionary<long, SndEntry>();

        public bool TryGet(int group, int number, out SndEntry entry)
        {
            return Table.TryGetValue(Key(group, number), out entry);
        }

        internal static long Key(int group, int number)
        {
            return ((long)group << 32) ^ (uint)number;
        }
    }

    /// <summary>Elecbyte SND v1 directory reader, following Ikemen GO's LoadSndFiltered layout.</summary>
    public static class SndReader
    {
        static readonly byte[] Signature =
        {
            (byte)'E', (byte)'l', (byte)'e', (byte)'c', (byte)'b', (byte)'y',
            (byte)'t', (byte)'e', (byte)'S', (byte)'n', (byte)'d', 0,
        };

        public static SndFile Read(byte[] data)
        {
            if (data == null) { throw new ArgumentNullException(nameof(data)); }
            if (data.Length < 24 || !HasSignature(data)) { throw new SndException("Invalid Elecbyte SND header."); }

            SndFile result = new SndFile
            {
                Version = U16(data, 12),
                Version2 = U16(data, 14),
            };
            uint count = U32(data, 16);
            uint offset = U32(data, 20);
            HashSet<uint> visited = new HashSet<uint>();
            for (uint index = 0; index < count; index++)
            {
                if (!visited.Add(offset)) { throw new SndException("SND subheader chain contains a cycle."); }
                if (offset > data.Length - 16) { throw new SndException("SND subheader is outside the file."); }

                uint next = U32(data, (int)offset);
                uint length = U32(data, (int)offset + 4);
                int group = I32(data, (int)offset + 8);
                int number = I32(data, (int)offset + 12);
                long waveOffset = (long)offset + 16;
                if (waveOffset + length > data.Length) { throw new SndException("SND wave payload is outside the file."); }

                SndEntry entry = new SndEntry
                {
                    Group = group,
                    Number = number,
                    WaveOffset = (int)waveOffset,
                    WaveLength = (int)length,
                };
                long key = SndFile.Key(group, number);
                if (!result.Table.ContainsKey(key))
                {
                    result.Table.Add(key, entry);
                    result.Entries.Add(entry);
                }
                offset = next;
            }
            return result;
        }

        public static byte[] CopyWave(byte[] source, SndEntry entry)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (entry == null) { throw new ArgumentNullException(nameof(entry)); }
            byte[] wave = new byte[entry.WaveLength];
            Buffer.BlockCopy(source, entry.WaveOffset, wave, 0, wave.Length);
            return wave;
        }

        static bool HasSignature(byte[] data)
        {
            for (int i = 0; i < Signature.Length; i++)
            {
                if (data[i] != Signature[i]) { return false; }
            }
            return true;
        }

        static ushort U16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | data[offset + 1] << 8);
        }

        static uint U32(byte[] data, int offset)
        {
            return (uint)(data[offset] | data[offset + 1] << 8 | data[offset + 2] << 16 | data[offset + 3] << 24);
        }

        static int I32(byte[] data, int offset)
        {
            return unchecked((int)U32(data, offset));
        }
    }
}
