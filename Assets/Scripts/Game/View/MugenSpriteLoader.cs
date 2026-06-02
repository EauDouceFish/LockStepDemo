using System.Collections.Generic;
using UnityEngine;
using Lockstep.Game.Data;
using Lockstep.Import.Sff;

namespace Lockstep.View
{
    /// <summary>
    /// 表现层：把 SFFv1 角色解码成 (group,image)->Sprite 字典，供动画器取用。
    /// 解码核心(SffV1Reader/PcxDecoder)已被 dotnet test 验证；本类只做 Unity Sprite 组装。
    /// 支持懒加载：Open 一次打开 SFF，BuildForAnim 按需补建某动画的精灵进共享缓存
    /// （Terrarian 有上千张，避免一次性全建造成卡顿/爆内存）。
    /// </summary>
    public static class MugenSpriteLoader
    {
        /// <summary>已打开的 SFF 上下文 + 渐增的精灵缓存。Cache 可直接交给 MugenSpriteAnimator。</summary>
        public sealed class Source
        {
            public string SffPath;
            public List<SffNode> Nodes;
            public Dictionary<long, SffNode> NodeByKey;
            public byte[] SharedPalette;
            public float PixelsPerUnit;
            public Dictionary<long, Sprite> Cache = new Dictionary<long, Sprite>();
        }

        public static Source Open(string sffPath, float pixelsPerUnit)
        {
            SffFile sff = SffV1Reader.ReadDirectory(sffPath);
            Dictionary<long, SffNode> nodeByKey = new Dictionary<long, SffNode>();
            for (int i = 0; i < sff.Nodes.Count; i++)
            {
                long key = MugenSpriteAnimator.Key(sff.Nodes[i].Group, sff.Nodes[i].Image);
                if (!nodeByKey.ContainsKey(key))
                {
                    nodeByKey[key] = sff.Nodes[i];
                }
            }
            Source source = new Source();
            source.SffPath = sffPath;
            source.Nodes = sff.Nodes;
            source.NodeByKey = nodeByKey;
            source.SharedPalette = FindSharedPalette(sffPath, sff.Nodes);
            source.PixelsPerUnit = pixelsPerUnit;
            return source;
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
                Sprite sprite = BuildSprite(source.SffPath, source.Nodes, source.NodeByKey, key,
                    source.SharedPalette, source.PixelsPerUnit);
                if (sprite != null)
                {
                    source.Cache[key] = sprite;
                }
            }
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

        static byte[] FindSharedPalette(string sffPath, List<SffNode> nodes)
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

        static Sprite BuildSprite(string sffPath, List<SffNode> nodes, Dictionary<long, SffNode> nodeByKey,
            long key, byte[] sharedPalette, float pixelsPerUnit)
        {
            if (!nodeByKey.TryGetValue(key, out SffNode node))
            {
                return null;
            }
            SffNode pixelNode = node;
            if (node.DataLength <= 0 && node.LinkedIndex >= 0 && node.LinkedIndex < nodes.Count)
            {
                pixelNode = nodes[node.LinkedIndex];
            }
            if (pixelNode.DataLength <= 0)
            {
                return null;
            }

            PcxImage image;
            try
            {
                image = PcxDecoder.Decode(SffV1Reader.ReadNodeData(sffPath, pixelNode));
            }
            catch (SffException)
            {
                return null;
            }

            byte[] palette = HasColor(image.Palette) ? image.Palette : sharedPalette;
            if (palette == null)
            {
                return null;
            }

            Texture2D texture = new Texture2D(image.Width, image.Height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            Color32[] pixels = new Color32[image.Width * image.Height];
            for (int y = 0; y < image.Height; y++)
            {
                int srcRow = y * image.Width;
                int dstRow = (image.Height - 1 - y) * image.Width;   // 翻转 Y（Unity 原点在左下）
                for (int x = 0; x < image.Width; x++)
                {
                    byte index = image.Indices[srcRow + x];
                    byte alpha = (byte)(index == 0 ? 0 : 255);       // 索引 0 透明
                    pixels[dstRow + x] = new Color32(palette[index * 3], palette[index * 3 + 1], palette[index * 3 + 2], alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            float pivotX = image.Width > 0 ? (float)node.AxisX / image.Width : 0.5f;
            float pivotY = image.Height > 0 ? 1f - (float)node.AxisY / image.Height : 0.5f;
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
