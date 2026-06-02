using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Lockstep.Game.Data;
using Lockstep.Import.Air;
using Lockstep.Import.Sff;

namespace Lockstep.View
{
    /// <summary>
    /// 运行期 MUGEN 角色演示加载器：解析 AIR + 运行期解码 SFF → 驱动 MugenSpriteAnimator 循环播一段动画。
    /// 角色无关——把任意 SFF v1 角色放到 ../MugenSource/&lt;folder&gt;/ 即可（Terrarian 现成，KFM 放进去同样可用）。
    /// 调用的解析/解码核心(AirParser/SffV1Reader/PcxDecoder)均已被 dotnet test 验证；本类是表现层薄壳。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MugenCharacterView : MonoBehaviour
    {
        public string CharacterFolder = "Terrarian";
        public int AnimNo = 0;
        public float PixelsPerUnit = 50f;

        void Start()
        {
            string baseDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "MugenSource", CharacterFolder));
            string airPath = FirstFile(baseDir, "*.air");
            string sffPath = FirstFile(baseDir, "*.sff");
            if (airPath == null || sffPath == null)
            {
                Debug.LogError("[MUGEN] 找不到 .air/.sff，目录：" + baseDir);
                return;
            }

            Dictionary<int, AnimData> anims = AirParser.ToDictionary(AirParser.ParseFile(airPath));
            if (!anims.TryGetValue(AnimNo, out AnimData anim))
            {
                Debug.LogError("[MUGEN] 角色没有动画号 " + AnimNo);
                return;
            }

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
            byte[] sharedPalette = FindSharedPalette(sffPath, sff.Nodes);

            Dictionary<long, Sprite> sprites = new Dictionary<long, Sprite>();
            for (int i = 0; i < anim.Frames.Length; i++)
            {
                AnimFrame frame = anim.Frames[i];
                long key = MugenSpriteAnimator.Key(frame.SpriteGroup, frame.SpriteImage);
                if (sprites.ContainsKey(key))
                {
                    continue;
                }
                Sprite sprite = BuildSprite(sffPath, sff.Nodes, nodeByKey, key, sharedPalette, PixelsPerUnit);
                if (sprite != null)
                {
                    sprites[key] = sprite;
                }
            }

            MugenSpriteAnimator animator = GetComponent<MugenSpriteAnimator>();
            if (animator == null)
            {
                animator = gameObject.AddComponent<MugenSpriteAnimator>();
            }
            animator.Play(anim, sprites);
            Debug.Log(string.Format("[MUGEN] {0} 动画 {1}：{2} 帧 / {3} 张精灵 已加载",
                CharacterFolder, AnimNo, anim.Frames.Length, sprites.Count));
        }

        static string FirstFile(string dir, string pattern)
        {
            if (!Directory.Exists(dir))
            {
                return null;
            }
            string[] files = Directory.GetFiles(dir, pattern);
            return files.Length > 0 ? files[0] : null;
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
