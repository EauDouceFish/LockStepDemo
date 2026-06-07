using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Lockstep.Import.Sff
{
    /// <summary>Small, dependency-free PNG decoder for SFFv2 formats 10, 11 and 12.</summary>
    internal static class PngDecoder
    {
        internal sealed class Image
        {
            public int Width;
            public int Height;
            public byte ColorType;
            public byte[] Indices;
            public byte[] Rgba;
        }

        static readonly byte[] Signature = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public static Image Decode(byte[] data)
        {
            if (data == null || data.Length < Signature.Length)
            {
                throw new SffException("PNG data is truncated");
            }
            for (int i = 0; i < Signature.Length; i++)
            {
                if (data[i] != Signature[i])
                {
                    throw new SffException("Invalid PNG signature");
                }
            }

            int width = 0;
            int height = 0;
            int bitDepth = 0;
            int colorType = -1;
            int interlace = 0;
            byte[] palette = null;
            byte[] transparency = null;
            MemoryStream idat = new MemoryStream();
            bool hasHeader = false;
            bool hasEnd = false;

            int offset = Signature.Length;
            while (offset < data.Length)
            {
                EnsureAvailable(data, offset, 12, "PNG chunk header");
                int length = ReadInt32BigEndian(data, offset);
                if (length < 0)
                {
                    throw new SffException("PNG chunk length exceeds supported size");
                }
                offset += 4;
                string type = new string(new[]
                {
                    (char)data[offset], (char)data[offset + 1], (char)data[offset + 2], (char)data[offset + 3]
                });
                offset += 4;
                EnsureAvailable(data, offset, length + 4, "PNG " + type + " chunk");

                if (type == "IHDR")
                {
                    if (hasHeader || length != 13)
                    {
                        throw new SffException("Invalid PNG IHDR chunk");
                    }
                    width = ReadInt32BigEndian(data, offset);
                    height = ReadInt32BigEndian(data, offset + 4);
                    bitDepth = data[offset + 8];
                    colorType = data[offset + 9];
                    if (width <= 0 || height <= 0 || data[offset + 10] != 0 || data[offset + 11] != 0)
                    {
                        throw new SffException("Unsupported PNG header");
                    }
                    interlace = data[offset + 12];
                    hasHeader = true;
                }
                else if (type == "PLTE")
                {
                    if (length == 0 || length % 3 != 0 || length > 768)
                    {
                        throw new SffException("Invalid PNG palette length");
                    }
                    palette = Copy(data, offset, length);
                }
                else if (type == "tRNS")
                {
                    transparency = Copy(data, offset, length);
                }
                else if (type == "IDAT")
                {
                    idat.Write(data, offset, length);
                }
                else if (type == "IEND")
                {
                    hasEnd = true;
                    offset += length + 4;
                    break;
                }

                offset += length + 4; // data + CRC
            }

            if (!hasHeader || !hasEnd || idat.Length == 0)
            {
                throw new SffException("PNG is missing IHDR, IDAT or IEND");
            }
            if (interlace != 0)
            {
                throw new SffException("Interlaced PNG is not supported in SFFv2");
            }

            int channels = GetChannelCount(colorType);
            ValidateBitDepth(colorType, bitDepth);
            int rowBytes = CheckedRowBytes(width, channels, bitDepth);
            int bytesPerPixel = System.Math.Max(1, (channels * bitDepth + 7) / 8);
            int filteredLength = checked(height * checked(rowBytes + 1));
            byte[] filtered = Inflate(idat.ToArray(), filteredLength);
            byte[] pixels = Unfilter(filtered, height, rowBytes, bytesPerPixel);

            Image image = new Image
            {
                Width = width,
                Height = height,
                ColorType = (byte)colorType,
            };
            if (colorType == 3)
            {
                if (palette == null)
                {
                    throw new SffException("Indexed PNG has no PLTE chunk");
                }
                image.Indices = UnpackIndices(pixels, width, height, bitDepth, rowBytes);
            }
            image.Rgba = ConvertToRgba(pixels, width, height, bitDepth, colorType, rowBytes,
                palette, transparency);
            return image;
        }

        static byte[] Inflate(byte[] zlib, int expectedLength)
        {
            if (zlib.Length < 6 || (zlib[0] & 15) != 8 || ((zlib[0] << 8) + zlib[1]) % 31 != 0)
            {
                throw new SffException("Invalid PNG zlib stream");
            }
            if ((zlib[1] & 32) != 0)
            {
                throw new SffException("PNG preset dictionaries are not supported");
            }

            byte[] output = new byte[expectedLength];
            using (MemoryStream compressed = new MemoryStream(zlib, 2, zlib.Length - 6, false))
            using (DeflateStream deflate = new DeflateStream(compressed, CompressionMode.Decompress))
            {
                int read = 0;
                while (read < output.Length)
                {
                    int count = deflate.Read(output, read, output.Length - read);
                    if (count == 0)
                    {
                        throw new SffException("PNG scanline data is truncated");
                    }
                    read += count;
                }
                if (deflate.ReadByte() != -1)
                {
                    throw new SffException("PNG scanline data exceeds image dimensions");
                }
            }
            return output;
        }

        static byte[] Unfilter(byte[] filtered, int height, int rowBytes, int bytesPerPixel)
        {
            byte[] output = new byte[checked(height * rowBytes)];
            int source = 0;
            for (int y = 0; y < height; y++)
            {
                int filter = filtered[source++];
                int row = y * rowBytes;
                int previous = row - rowBytes;
                for (int x = 0; x < rowBytes; x++)
                {
                    int raw = filtered[source++];
                    int left = x >= bytesPerPixel ? output[row + x - bytesPerPixel] : 0;
                    int up = y > 0 ? output[previous + x] : 0;
                    int upperLeft = y > 0 && x >= bytesPerPixel
                        ? output[previous + x - bytesPerPixel]
                        : 0;
                    switch (filter)
                    {
                        case 0: break;
                        case 1: raw += left; break;
                        case 2: raw += up; break;
                        case 3: raw += (left + up) >> 1; break;
                        case 4: raw += Paeth(left, up, upperLeft); break;
                        default: throw new SffException("Unknown PNG filter " + filter);
                    }
                    output[row + x] = (byte)raw;
                }
            }
            return output;
        }

        static byte[] UnpackIndices(byte[] data, int width, int height, int bitDepth, int rowBytes)
        {
            byte[] result = new byte[checked(width * height)];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result[y * width + x] = (byte)ReadPackedSample(data, y * rowBytes, x, bitDepth);
                }
            }
            return result;
        }

        static byte[] ConvertToRgba(byte[] data, int width, int height, int bitDepth, int colorType,
            int rowBytes, byte[] palette, byte[] transparency)
        {
            byte[] rgba = new byte[checked(width * height * 4)];
            for (int y = 0; y < height; y++)
            {
                int row = y * rowBytes;
                for (int x = 0; x < width; x++)
                {
                    int destination = (y * width + x) * 4;
                    if (colorType == 0)
                    {
                        int sample = ReadSample(data, row, x, 1, 0, bitDepth);
                        byte gray = ScaleSample(sample, bitDepth);
                        byte alpha = MatchesTransparentGray(sample, transparency) ? (byte)0 : (byte)255;
                        SetRgba(rgba, destination, gray, gray, gray, alpha);
                    }
                    else if (colorType == 2)
                    {
                        int red = ReadSample(data, row, x, 3, 0, bitDepth);
                        int green = ReadSample(data, row, x, 3, 1, bitDepth);
                        int blue = ReadSample(data, row, x, 3, 2, bitDepth);
                        byte alpha = MatchesTransparentRgb(red, green, blue, transparency) ? (byte)0 : (byte)255;
                        SetRgba(rgba, destination, ScaleSample(red, bitDepth), ScaleSample(green, bitDepth),
                            ScaleSample(blue, bitDepth), alpha);
                    }
                    else if (colorType == 3)
                    {
                        int index = ReadPackedSample(data, row, x, bitDepth);
                        if (index * 3 + 2 >= palette.Length)
                        {
                            throw new SffException("PNG palette index is out of range");
                        }
                        byte alpha = transparency != null && index < transparency.Length
                            ? transparency[index]
                            : (byte)255;
                        SetRgba(rgba, destination, palette[index * 3], palette[index * 3 + 1],
                            palette[index * 3 + 2], alpha);
                    }
                    else if (colorType == 4)
                    {
                        byte gray = ScaleSample(ReadSample(data, row, x, 2, 0, bitDepth), bitDepth);
                        byte alpha = ScaleSample(ReadSample(data, row, x, 2, 1, bitDepth), bitDepth);
                        SetRgba(rgba, destination, gray, gray, gray, alpha);
                    }
                    else
                    {
                        SetRgba(rgba, destination,
                            ScaleSample(ReadSample(data, row, x, 4, 0, bitDepth), bitDepth),
                            ScaleSample(ReadSample(data, row, x, 4, 1, bitDepth), bitDepth),
                            ScaleSample(ReadSample(data, row, x, 4, 2, bitDepth), bitDepth),
                            ScaleSample(ReadSample(data, row, x, 4, 3, bitDepth), bitDepth));
                    }
                }
            }
            return rgba;
        }

        static int ReadSample(byte[] data, int row, int x, int channels, int channel, int bitDepth)
        {
            if (bitDepth < 8)
            {
                return ReadPackedSample(data, row, x, bitDepth);
            }
            int bytes = bitDepth / 8;
            int offset = row + (x * channels + channel) * bytes;
            return bytes == 1 ? data[offset] : (data[offset] << 8) | data[offset + 1];
        }

        static int ReadPackedSample(byte[] data, int row, int x, int bitDepth)
        {
            if (bitDepth == 8)
            {
                return data[row + x];
            }
            int perByte = 8 / bitDepth;
            int shift = (perByte - 1 - x % perByte) * bitDepth;
            return (data[row + x / perByte] >> shift) & ((1 << bitDepth) - 1);
        }

        static byte ScaleSample(int sample, int bitDepth)
        {
            int maximum = (1 << bitDepth) - 1;
            return (byte)((sample * 255 + maximum / 2) / maximum);
        }

        static bool MatchesTransparentGray(int gray, byte[] transparency)
        {
            return transparency != null && transparency.Length >= 2
                && gray == ((transparency[0] << 8) | transparency[1]);
        }

        static bool MatchesTransparentRgb(int red, int green, int blue, byte[] transparency)
        {
            return transparency != null && transparency.Length >= 6
                && red == ((transparency[0] << 8) | transparency[1])
                && green == ((transparency[2] << 8) | transparency[3])
                && blue == ((transparency[4] << 8) | transparency[5]);
        }

        static int GetChannelCount(int colorType)
        {
            switch (colorType)
            {
                case 0: return 1;
                case 2: return 3;
                case 3: return 1;
                case 4: return 2;
                case 6: return 4;
                default: throw new SffException("Unsupported PNG color type " + colorType);
            }
        }

        static void ValidateBitDepth(int colorType, int bitDepth)
        {
            bool valid = colorType == 0
                ? bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8 || bitDepth == 16
                : colorType == 3
                    ? bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8
                    : bitDepth == 8 || bitDepth == 16;
            if (!valid)
            {
                throw new SffException("Unsupported PNG bit depth " + bitDepth + " for color type " + colorType);
            }
        }

        static int CheckedRowBytes(int width, int channels, int bitDepth)
        {
            long bits = (long)width * channels * bitDepth;
            long bytes = (bits + 7) / 8;
            if (bytes > int.MaxValue)
            {
                throw new SffException("PNG row is too large");
            }
            return (int)bytes;
        }

        static int Paeth(int left, int up, int upperLeft)
        {
            int prediction = left + up - upperLeft;
            int leftDistance = System.Math.Abs(prediction - left);
            int upDistance = System.Math.Abs(prediction - up);
            int upperLeftDistance = System.Math.Abs(prediction - upperLeft);
            return leftDistance <= upDistance && leftDistance <= upperLeftDistance
                ? left
                : upDistance <= upperLeftDistance ? up : upperLeft;
        }

        static void SetRgba(byte[] rgba, int offset, byte red, byte green, byte blue, byte alpha)
        {
            rgba[offset] = red;
            rgba[offset + 1] = green;
            rgba[offset + 2] = blue;
            rgba[offset + 3] = alpha;
        }

        static int ReadInt32BigEndian(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        static void EnsureAvailable(byte[] data, int offset, int count, string context)
        {
            if (offset < 0 || count < 0 || offset > data.Length - count)
            {
                throw new SffException(context + " is truncated");
            }
        }

        static byte[] Copy(byte[] data, int offset, int count)
        {
            byte[] result = new byte[count];
            Buffer.BlockCopy(data, offset, result, 0, count);
            return result;
        }
    }
}
