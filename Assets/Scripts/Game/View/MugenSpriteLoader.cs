using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Lockstep.Game.Data;
using Lockstep.Import.Sff;

namespace Lockstep.View
{
    /// <summary>
    /// 表现层：把 SFF 角色解码成 (group,image)->Sprite 字典，供动画器取用。自动识别 SFFv1 / SFFv2。
    /// 解码核心(SffV1Reader/PcxDecoder/SffV2Reader)已被 dotnet test 验证；本类只做 Unity Sprite 组装。
    /// 支持懒加载：Open 一次打开 SFF，BuildForAnim 按需补建某动画的精灵进共享缓存
    /// （Terrarian 有上千张，避免一次性全建造成卡顿/爆内存）。
    /// SFFv2 的 PNG 精灵(format>=10，仅头像)暂跳过，逻辑层不解码。
    /// </summary>
    public static class MugenSpriteLoader
    {
        /// <summary>已打开的 SFF 上下文 + 渐增的精灵缓存。Cache 可直接交给 MugenSpriteAnimator。</summary>
        public sealed class Source
        {
            public string SffPath;
            public float PixelsPerUnit;
            public bool IsV2;
            // v1
            public List<SffNode> Nodes;
            public Dictionary<long, SffNode> NodeByKey;
            public byte[] SharedPalette;
            // v2
            public SffV2File V2;
            public Dictionary<long, SffV2Sprite> V2ByKey;
            public Dictionary<long, Sprite> Cache = new Dictionary<long, Sprite>();
        }

        public static Source Open(string sffPath, float pixelsPerUnit)
        {
            Source source = new Source();
            source.SffPath = sffPath;
            source.PixelsPerUnit = pixelsPerUnit;
            source.IsV2 = DetectIsV2(sffPath);

            if (source.IsV2)
            {
                source.V2 = SffV2Reader.ReadDirectory(sffPath);
                source.V2ByKey = new Dictionary<long, SffV2Sprite>();
                for (int i = 0; i < source.V2.Sprites.Count; i++)
                {
                    SffV2Sprite sprite = source.V2.Sprites[i];
                    long key = MugenSpriteAnimator.Key(sprite.Group, sprite.Number);
                    if (!source.V2ByKey.ContainsKey(key))
                    {
                        source.V2ByKey[key] = sprite;
                    }
                }
            }
            else
            {
                SffFile sff = SffV1Reader.ReadDirectory(sffPath);
                source.Nodes = sff.Nodes;
                source.NodeByKey = new Dictionary<long, SffNode>();
                for (int i = 0; i < sff.Nodes.Count; i++)
                {
                    long key = MugenSpriteAnimator.Key(sff.Nodes[i].Group, sff.Nodes[i].Image);
                    if (!source.NodeByKey.ContainsKey(key))
                    {
                        source.NodeByKey[key] = sff.Nodes[i];
                    }
                }
                source.SharedPalette = FindSharedPaletteV1(sffPath, sff.Nodes);
            }
            return source;
        }

        static bool DetectIsV2(string sffPath)
        {
            using (FileStream stream = new FileStream(sffPath, FileMode.Open, FileAccess.Read))
            {
                byte[] header = new byte[16];
                stream.Read(header, 0, 16);
                return header[15] == 2;   // 版本高字节：1=v1，2=v2
            }
        }

        /// <summary>把某个动画引用到的、尚未构建的精灵补进缓存。</summary>
        public static void BuildForAnim(Source source, AnimData anim)
        {
            for (int i = 0; i < anim.Frames.Length; i++)
            {
                AnimFrame frame = anim.Frames[i];
                long key = MugenSpriteAnimator.Key(frame.SpriteGroup, frame.SpriteImage);
                if (source.Cache.ContainsKey(key))
                {
                    continue;
                }
                Sprite sprite = source.IsV2 ? BuildSpriteV2(source, key) : BuildSpriteV1(source, key);
                if (sprite != null)
                {
                    source.Cache[key] = sprite;
                }
            }
        }

        /// <summary>按 (group,image) 构建并缓存单张精灵（如头像 9000,0/9000,1）。失败返回 null。</summary>
        public static Sprite BuildSingle(Source source, int group, int image)
        {
            long key = MugenSpriteAnimator.Key(group, image);
            if (source.Cache.TryGetValue(key, out Sprite cached))
            {
                return cached;
            }
            Sprite sprite = source.IsV2 ? BuildSpriteV2(source, key) : BuildSpriteV1(source, key);
            if (sprite != null)
            {
                source.Cache[key] = sprite;
            }
            return sprite;
        }

        /// <summary>一次性构建若干动画的全部精灵（单动画场景用）。</summary>
        public static Dictionary<long, Sprite> Load(string sffPath, IEnumerable<AnimData> anims, float pixelsPerUnit)
        {
            Source source = Open(sffPath, pixelsPerUnit);
            foreach (AnimData anim in anims)
            {
                BuildForAnim(source, anim);
            }
            return source.Cache;
        }

        // ---- SFFv2 ----

        static Sprite BuildSpriteV2(Source source, long key)
        {
            if (!source.V2ByKey.TryGetValue(key, out SffV2Sprite sprite))
            {
                return null;
            }
            try
            {
                PcxImage image = SffV2Reader.Decode(source.SffPath, source.V2, sprite);
                return BuildSpriteFromImage(image, sprite.AxisX, sprite.AxisY, source.PixelsPerUnit);
            }
            catch (SffException)
            {
                return null;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        // ---- SFFv1 ----

        static byte[] FindSharedPaletteV1(string sffPath, List<SffNode> nodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].DataLength <= 0)
                {
                    continue;
                }
                try
                {
                    PcxImage image = PcxDecoder.Decode(SffV1Reader.ReadNodeData(sffPath, nodes[i]));
                    if (HasColor(image.Palette))
                    {
                        return image.Palette;
                    }
                }
                catch (SffException)
                {
                }
            }
            return null;
        }

        static Sprite BuildSpriteV1(Source source, long key)
        {
            if (!source.NodeByKey.TryGetValue(key, out SffNode node))
            {
                return null;
            }
            SffNode pixelNode = node;
            if (node.DataLength <= 0 && node.LinkedIndex >= 0 && node.LinkedIndex < source.Nodes.Count)
            {
                pixelNode = source.Nodes[node.LinkedIndex];
            }
            if (pixelNode.DataLength <= 0)
            {
                return null;
            }
            PcxImage image;
            try
            {
                image = PcxDecoder.Decode(SffV1Reader.ReadNodeData(source.SffPath, pixelNode));
            }
            catch (SffException)
            {
                return null;
            }
            if (!HasColor(image.Palette))
            {
                image.Palette = source.SharedPalette;
            }
            if (image.Palette == null)
            {
                return null;
            }
            return BuildSpriteFromImage(image, node.AxisX, node.AxisY, source.PixelsPerUnit);
        }

        // ---- 共用：索引图 + 调色板 + 轴 → Unity Sprite ----

        static Sprite BuildSpriteFromImage(PcxImage image, short axisX, short axisY, float pixelsPerUnit)
        {
            if (image == null || image.Width <= 0 || image.Height <= 0)
            {
                return null;
            }
            long pixelCountLong = (long)image.Width * image.Height;
            if (pixelCountLong > int.MaxValue)
            {
                return null;
            }
            int pixelCount = (int)pixelCountLong;
            if (image.IsTrueColor)
            {
                long requiredRgbaLength = pixelCountLong * 4;
                if (requiredRgbaLength > int.MaxValue || image.Rgba.Length < requiredRgbaLength)
                {
                    return null;
                }
            }
            else if (image.Indices == null || image.Indices.Length < pixelCount ||
                     image.Palette == null || image.Palette.Length < 768)
            {
                return null;
            }

            Texture2D texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[pixelCount];
            if (image.IsTrueColor)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    int srcRow = y * image.Width;
                    int dstRow = (image.Height - 1 - y) * image.Width;   // Flip Y for Unity's bottom-left origin.
                    for (int x = 0; x < image.Width; x++)
                    {
                        int source = (srcRow + x) * 4;
                        pixels[dstRow + x] = new Color32(
                            image.Rgba[source], image.Rgba[source + 1], image.Rgba[source + 2], image.Rgba[source + 3]);
                    }
                }
            }
            else
            {
                for (int y = 0; y < image.Height; y++)
                {
                    int srcRow = y * image.Width;
                    int dstRow = (image.Height - 1 - y) * image.Width;   // 翻转 Y（Unity 原点在左下）
                    for (int x = 0; x < image.Width; x++)
                    {
                        byte index = image.Indices[srcRow + x];
                        byte alpha = (byte)(index == 0 ? 0 : 255);       // 索引 0 透明
                        pixels[dstRow + x] = new Color32(
                            image.Palette[index * 3], image.Palette[index * 3 + 1], image.Palette[index * 3 + 2], alpha);
                    }
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            float pivotX = image.Width > 0 ? (float)axisX / image.Width : 0.5f;
            float pivotY = image.Height > 0 ? 1f - (float)axisY / image.Height : 0.5f;
            return Sprite.Create(texture, new Rect(0, 0, image.Width, image.Height),
                new Vector2(pivotX, pivotY), pixelsPerUnit);
        }

        static bool HasColor(byte[] palette)
        {
            if (palette == null)
            {
                return false;
            }
            for (int i = 0; i < palette.Length; i++)
            {
                if (palette[i] != 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
