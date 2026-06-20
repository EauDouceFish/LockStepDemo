using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Lockstep.Game.Data;
using Lockstep.Import.Air;
using Lockstep.Mugen.Battle;
using UnityEngine;

namespace Lockstep.View
{
    public sealed class MugenBattlePreloadedBundle
    {
        public string Folder;
        public MCharData Data;
        public MugenSpriteLoader.Source Source;
        public string SoundPath;
        public readonly Dictionary<int, AnimData> GameAnims = new Dictionary<int, AnimData>();
        public readonly HashSet<int> Built = new HashSet<int>();
    }

    public static class MugenBattlePreloadCache
    {
        static readonly Dictionary<string, MugenBattlePreloadedBundle> Bundles =
            new Dictionary<string, MugenBattlePreloadedBundle>(StringComparer.OrdinalIgnoreCase);

        static readonly int[] WarmAnimIds =
        {
            0, 5, 10, 11, 12, 20, 21, 40, 41, 42, 47, 50, 51, 52,
            100, 105, 110, 120, 130, 140, 200, 201, 5000, 5010, 5030,
        };

        public static bool TryGet(string folder, string mugenRoot, string commonFolder, float pixelsPerUnit,
            out MugenBattlePreloadedBundle bundle)
        {
            return Bundles.TryGetValue(Key(folder, mugenRoot, commonFolder, pixelsPerUnit), out bundle);
        }

        public static bool TryGetOrLoad(string folder, string mugenRoot, string commonFolder, float pixelsPerUnit,
            out MugenBattlePreloadedBundle bundle)
        {
            string key = Key(folder, mugenRoot, commonFolder, pixelsPerUnit);
            if (Bundles.TryGetValue(key, out bundle))
            {
                return true;
            }

            bundle = Load(folder, mugenRoot, commonFolder, pixelsPerUnit);
            if (bundle == null)
            {
                return false;
            }

            Bundles[key] = bundle;
            return true;
        }

        static MugenBattlePreloadedBundle Load(string folder, string mugenRoot, string commonFolder, float pixelsPerUnit)
        {
            try
            {
                string charDir = Path.Combine(mugenRoot, folder);
                if (!MugenCharacterPackageLoader.TryLoad(charDir, out MugenCharacterPackage package) ||
                    !MugenCharacterPackageLoader.IsBattleLoadable(package))
                {
                    return null;
                }

                string commonPath = Path.Combine(mugenRoot, commonFolder, "common1.cns");
                MugenBattlePreloadedBundle bundle = new MugenBattlePreloadedBundle
                {
                    Folder = folder,
                    Data = package.LoadData(commonPath),
                    Source = MugenSpriteLoader.Open(package.SpritePath, pixelsPerUnit),
                    SoundPath = package.SoundPath,
                };

                List<AnimData> anims = AirParser.ParseFile(package.AnimPath);
                for (int i = 0; i < anims.Count; i++)
                {
                    bundle.GameAnims[anims[i].Id] = anims[i];
                }

                WarmCommonSprites(bundle);
                return bundle;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MUGEN] battle preload failed for " + folder + ": " + ex.Message);
                return null;
            }
        }

        static void WarmCommonSprites(MugenBattlePreloadedBundle bundle)
        {
            for (int i = 0; i < WarmAnimIds.Length; i++)
            {
                int animId = WarmAnimIds[i];
                if (bundle.Built.Contains(animId))
                {
                    continue;
                }
                if (bundle.GameAnims.TryGetValue(animId, out AnimData anim))
                {
                    MugenSpriteLoader.BuildForAnim(bundle.Source, anim);
                }
                bundle.Built.Add(animId);
            }
        }

        static string Key(string folder, string mugenRoot, string commonFolder, float pixelsPerUnit)
        {
            return (mugenRoot ?? string.Empty) + "|" +
                   (commonFolder ?? string.Empty) + "|" +
                   pixelsPerUnit.ToString(CultureInfo.InvariantCulture) + "|" +
                   (folder ?? string.Empty);
        }
    }
}
