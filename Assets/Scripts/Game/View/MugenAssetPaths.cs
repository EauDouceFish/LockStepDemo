using System.IO;
using System.IO.Compression;
using System;
using UnityEngine;

namespace Lockstep.View
{
    /// <summary>表现层小工具：在角色素材目录里按通配符找第一个文件。</summary>
    public static class MugenAssetPaths
    {
        const string BundledResourceName = "MugenSourceBundle";
        const string BundledVersion = "full-roster-2026-06-12-v1";
        const string VersionFile = ".mugen_bundle_version";
        static readonly string[] BundledFolders =
        {
            "Ananzi",
            "Animus",
            "Final",
            "Gustavo",
            "Hashi",
            "Janos",
            "kfm",
            "Maxine",
            "Noroko",
            "Peketo",
            "Shar-Makai",
            "Terrarian",
        };

        public static string MugenRoot()
        {
            EnsureBundledMugenSource(Path.Combine(Application.persistentDataPath, "MugenSource"));

            string[] candidates = MugenRootCandidates();
            for (int i = 0; i < candidates.Length; i++)
            {
                if (Directory.Exists(candidates[i]))
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Length - 1];
        }

        static bool EnsureBundledMugenSource(string root)
        {
            if (HasBundledBaseline(root))
            {
                return true;
            }

            TextAsset bundle = Resources.Load<TextAsset>(BundledResourceName);
            if (bundle == null || bundle.bytes == null || bundle.bytes.Length == 0)
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(root);
                ExtractZip(bundle.bytes, root);
                File.WriteAllText(Path.Combine(root, VersionFile), BundledVersion);
                Debug.Log("[MugenAssetPaths] Extracted bundled MugenSource to " + root);
                return HasBundledBaseline(root);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MugenAssetPaths] Failed to extract bundled MugenSource: " + ex.Message);
                return false;
            }
        }

        static bool HasBundledBaseline(string root)
        {
            if (!Directory.Exists(root))
            {
                return false;
            }

            string versionPath = Path.Combine(root, VersionFile);
            if (!File.Exists(versionPath) || File.ReadAllText(versionPath).Trim() != BundledVersion)
            {
                return false;
            }

            for (int i = 0; i < BundledFolders.Length; i++)
            {
                string folder = Path.Combine(root, BundledFolders[i]);
                if (!Directory.Exists(folder) || FirstFile(folder, "*.def") == null)
                {
                    return false;
                }
            }

            return File.Exists(Path.Combine(root, "Terrarian", "common1.cns"));
        }

        static void ExtractZip(byte[] bytes, string root)
        {
            using (MemoryStream stream = new MemoryStream(bytes))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    ZipArchiveEntry entry = archive.Entries[i];
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    string target = Path.GetFullPath(Path.Combine(root, entry.FullName.Replace('\\', '/')));
                    string normalizedRoot = Path.GetFullPath(root);
                    if (!target.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string dir = Path.GetDirectoryName(target);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    using (Stream input = entry.Open())
                    using (FileStream output = File.Create(target))
                    {
                        input.CopyTo(output);
                    }
                }
            }
        }

        public static string[] MugenRootCandidates()
        {
            string env = Environment.GetEnvironmentVariable("MUGEN_SOURCE");
            if (!string.IsNullOrEmpty(env))
            {
                return new[]
                {
                    Path.GetFullPath(env),
                    Path.Combine(Application.persistentDataPath, "MugenSource"),
                    Path.Combine(Application.streamingAssetsPath, "MugenSource"),
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", "MugenSource")),
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "MugenSource")),
                };
            }

            return new[]
            {
                Path.Combine(Application.persistentDataPath, "MugenSource"),
                Path.Combine(Application.streamingAssetsPath, "MugenSource"),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "MugenSource")),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "MugenSource")),
            };
        }

        public static string FirstFile(string dir, string pattern)
        {
            if (!Directory.Exists(dir))
            {
                return null;
            }
            string[] files = Directory.GetFiles(dir, pattern);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files.Length > 0 ? files[0] : null;
        }
    }
}
