using System;

namespace Lockstep.Import.Sff
{
    /// <summary>Reads a MUGEN ACT palette into the same RGB byte layout used by the SFF readers.</summary>
    public static class ActPaletteReader
    {
        public const int ColorCount = 256;
        public const int ByteCount = ColorCount * 3;

        public static byte[] Read(byte[] data)
        {
            if (data == null) { throw new ArgumentNullException(nameof(data)); }
            byte[] palette = new byte[ByteCount];
            int colors = System.Math.Min(data.Length / 3, ColorCount);
            for (int source = 0; source < colors; source++)
            {
                int destination = ColorCount - 1 - source;
                Buffer.BlockCopy(data, source * 3, palette, destination * 3, 3);
            }
            return palette;
        }
    }
}
