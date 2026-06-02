namespace Lockstep.Import.Sff
{
    /// <summary>
    /// 8 位索引 PCX 解码器（SFF v1 精灵就是 PCX）。RLE 解码 + 取每行前 Width 字节 + 末尾 256 色调色板。
    /// 纯字节运算，无 Unity。
    /// </summary>
    public static class PcxDecoder
    {
        public static PcxImage Decode(byte[] data)
        {
            if (data == null || data.Length < 128)
            {
                throw new SffException("PCX 数据过短");
            }

            int encoding = data[2];
            int xmin = U16(data, 4);
            int ymin = U16(data, 6);
            int xmax = U16(data, 8);
            int ymax = U16(data, 10);
            int nplanes = data[65];
            int bytesPerLine = U16(data, 66);

            int width = xmax - xmin + 1;
            int height = ymax - ymin + 1;
            if (width <= 0 || height <= 0)
            {
                throw new SffException("PCX 尺寸非法: " + width + "x" + height);
            }
            if (nplanes <= 0)
            {
                nplanes = 1;
            }
            if (bytesPerLine <= 0)
            {
                bytesPerLine = width;
            }

            int total = bytesPerLine * nplanes * height;
            byte[] raw = new byte[total];
            int pos = 128;
            int o = 0;
            while (o < total && pos < data.Length)
            {
                byte b = data[pos++];
                if (encoding == 1 && (b & 0xC0) == 0xC0)
                {
                    int count = b & 0x3F;
                    byte value = pos < data.Length ? data[pos++] : (byte)0;
                    for (int k = 0; k < count && o < total; k++)
                    {
                        raw[o++] = value;
                    }
                }
                else
                {
                    raw[o++] = b;
                }
            }

            byte[] indices = new byte[width * height];
            for (int y = 0; y < height; y++)
            {
                int rowStart = y * bytesPerLine;
                for (int x = 0; x < width; x++)
                {
                    int src = rowStart + x;
                    indices[y * width + x] = src < raw.Length ? raw[src] : (byte)0;
                }
            }

            byte[] palette = new byte[768];
            if (data.Length >= 769 && data[data.Length - 769] == 0x0C)
            {
                System.Array.Copy(data, data.Length - 768, palette, 0, 768);
            }

            return new PcxImage
            {
                Width = width,
                Height = height,
                Indices = indices,
                Palette = palette,
            };
        }

        static int U16(byte[] data, int offset)
        {
            return data[offset] | (data[offset + 1] << 8);
        }
    }
}
