using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Lockstep.Game.Data;
using Lockstep.Import.Air;
using Lockstep.Import.Sff;

namespace Lockstep.EditorTools
{
    /// <summary>
    /// MUGEN 素材导入菜单（编辑器离线工具）。调用已被 dotnet test 验证的解析核心
    /// (AirParser / SffV1Reader / PcxDecoder)，把数据落成 Unity 资产。
    /// 注：本类在编辑器内运行，不参与 dotnet test。
    /// </summary>
    public static class MugenImportMenu
    {
        const string CharRelDir = "../MugenSource/Terrarian";

        static string SourceDir
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", CharRelDir)); }
        }

        [MenuItem("MUGEN/Import Terrarian/解析 AIR (日志)")]
        public static void LogAir()
        {
            string path = Path.Combine(SourceDir, "Terrarian.air");
            if (!File.Exists(path))
            {
                Debug.LogError("[MUGEN] 找不到 " + path);
                return;
            }
            List<AnimData> anims = AirParser.ParseFile(path);
            AnimData stand = anims.Find(a => a.Id == 0);
            Debug.Log(string.Format("[MUGEN] AIR 解析成功：{0} 个 action。Action0 帧数={1}",
                anims.Count, stand != null ? stand.Frames.Length : 0));
        }

        [MenuItem("MUGEN/Import Terrarian/导出 SFF 精灵为 PNG")]
        public static void ExportSffToPng()
        {
            string path = Path.Combine(SourceDir, "Terrarian.sff");
            if (!File.Exists(path))
            {
                Debug.LogError("[MUGEN] 找不到 " + path);
                return;
            }

            SffFile sff = SffV1Reader.ReadDirectory(path);
            string outDir = Path.Combine(Application.dataPath, "Mugen", "Terrarian", "Sprites");
            Directory.CreateDirectory(outDir);

            byte[] sharedPalette = null;
            int written = 0;
            for (int i = 0; i < sff.Nodes.Count; i++)
            {
                SffNode node = sff.Nodes[i];
                if (node.DataLength <= 0)
                {
                    continue;
                }

                PcxImage img;
                try
                {
                    img = PcxDecoder.Decode(SffV1Reader.ReadNodeData(path, node));
                }
                catch (SffException)
                {
                    continue;
                }

                if (sharedPalette == null && HasColor(img.Palette))
                {
                    sharedPalette = img.Palette;
                }
                byte[] palette = HasColor(img.Palette) ? img.Palette : sharedPalette;
                if (palette == null)
                {
                    continue;
                }

                Texture2D tex = ToTexture(img, palette);
                File.WriteAllBytes(Path.Combine(outDir, node.Group + "_" + node.Image + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                written++;
            }

            AssetDatabase.Refresh();
            Debug.Log(string.Format("[MUGEN] SFF 导出完成：{0}/{1} 张 → {2}", written, sff.NumImages, outDir));
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

        static Texture2D ToTexture(PcxImage img, byte[] palette)
        {
            Texture2D tex = new Texture2D(img.Width, img.Height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[img.Width * img.Height];
            for (int y = 0; y < img.Height; y++)
            {
                int srcRow = y * img.Width;
                int dstRow = (img.Height - 1 - y) * img.Width;   // 翻转 Y：Unity 纹理原点在左下
                for (int x = 0; x < img.Width; x++)
                {
                    byte idx = img.Indices[srcRow + x];
                    byte r = palette[idx * 3];
                    byte g = palette[idx * 3 + 1];
                    byte b = palette[idx * 3 + 2];
                    byte a = (byte)(idx == 0 ? 0 : 255);          // 索引 0 = 透明（MUGEN 约定）
                    pixels[dstRow + x] = new Color32(r, g, b, a);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
