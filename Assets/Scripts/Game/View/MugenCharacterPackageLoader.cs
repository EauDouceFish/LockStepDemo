using System;
using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Parse;

namespace Lockstep.View
{
    public sealed class MugenCharacterPackage
    {
        public string FolderName;
        public string DirectoryPath;
        public string DefPath;
        public string SpritePath;
        public string AnimPath;
        public string SoundPath;
        public string CmdPath;
        public string ConstantsPath;
        public string StCommonPath;
        public readonly List<string> StatePaths = new List<string>();
        public MCharacterDefinition Definition;

        public MCharData LoadData(string commonFallbackPath)
        {
            List<string> states = new List<string>();
            for (int i = 0; i < StatePaths.Count; i++)
            {
                states.Add(File.ReadAllText(StatePaths[i]));
            }

            string common = ReadOptional(StCommonPath);
            if (common == null && !string.IsNullOrEmpty(commonFallbackPath) && File.Exists(commonFallbackPath))
            {
                common = File.ReadAllText(commonFallbackPath);
            }

            string name = Definition != null && !string.IsNullOrEmpty(Definition.DisplayName)
                ? Definition.DisplayName
                : (Definition != null && !string.IsNullOrEmpty(Definition.Name) ? Definition.Name : FolderName);

            return MCharLoader.Load(
                states,
                ReadOptional(ConstantsPath),
                common,
                ReadOptional(AnimPath),
                ReadOptional(CmdPath),
                name,
                Definition);
        }

        static string ReadOptional(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllText(path) : null;
        }
    }

    public static class MugenCharacterPackageLoader
    {
        public static bool TryLoad(string directory, out MugenCharacterPackage package)
        {
            package = null;
            try
            {
                string defPath = PickMainDef(directory);
                if (defPath == null) { return false; }

                MCharacterDefinition definition = MDefParser.Parse(File.ReadAllText(defPath));
                package = new MugenCharacterPackage
                {
                    FolderName = Path.GetFileName(directory),
                    DirectoryPath = directory,
                    DefPath = defPath,
                    Definition = definition,
                    SpritePath = Resolve(directory, definition.Files.Sprite) ?? MugenAssetPaths.FirstFile(directory, "*.sff"),
                    AnimPath = Resolve(directory, definition.Files.Anim) ?? MugenAssetPaths.FirstFile(directory, "*.air"),
                    SoundPath = Resolve(directory, definition.Files.Sound) ?? MugenAssetPaths.FirstFile(directory, "*.snd"),
                    CmdPath = Resolve(directory, definition.Files.Cmd) ?? MugenAssetPaths.FirstFile(directory, "*.cmd"),
                    ConstantsPath = Resolve(directory, definition.Files.Cns) ?? MugenAssetPaths.FirstFile(directory, "*.cns"),
                    StCommonPath = Resolve(directory, definition.Files.StCommon),
                };

                for (int i = 0; i < definition.Files.St.Count; i++)
                {
                    string statePath = Resolve(directory, definition.Files.St[i]);
                    if (statePath != null && !package.StatePaths.Contains(statePath))
                    {
                        package.StatePaths.Add(statePath);
                    }
                }

                return true;
            }
            catch
            {
                package = null;
                return false;
            }
        }

        public static bool IsBattleLoadable(MugenCharacterPackage package)
        {
            return package != null &&
                !string.IsNullOrEmpty(package.SpritePath) &&
                !string.IsNullOrEmpty(package.AnimPath) &&
                !string.IsNullOrEmpty(package.ConstantsPath);
        }

        public static string PickMainDef(string directory)
        {
            if (!Directory.Exists(directory)) { return null; }

            string[] defs = Directory.GetFiles(directory, "*.def");
            Array.Sort(defs, StringComparer.OrdinalIgnoreCase);
            if (defs.Length == 0) { return null; }

            string folderName = Path.GetFileName(directory);
            for (int i = 0; i < defs.Length; i++)
            {
                if (Path.GetFileNameWithoutExtension(defs[i]).Equals(folderName, StringComparison.OrdinalIgnoreCase))
                {
                    return defs[i];
                }
            }

            for (int i = 0; i < defs.Length; i++)
            {
                try
                {
                    MDefFiles files = MDefParser.ParseFiles(File.ReadAllText(defs[i]));
                    if (!string.IsNullOrEmpty(files.Cmd) && !string.IsNullOrEmpty(files.Cns))
                    {
                        return defs[i];
                    }
                }
                catch
                {
                }
            }

            return defs[0];
        }

        public static string Resolve(string directory, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) { return null; }

            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            string direct = Path.Combine(directory, normalized);
            if (File.Exists(direct)) { return direct; }

            string current = directory;
            string[] parts = normalized.Split(Path.DirectorySeparatorChar);
            for (int i = 0; i < parts.Length; i++)
            {
                string[] entries = Directory.Exists(current)
                    ? Directory.GetFileSystemEntries(current)
                    : Array.Empty<string>();
                Array.Sort(entries, StringComparer.OrdinalIgnoreCase);

                string match = null;
                for (int e = 0; e < entries.Length; e++)
                {
                    if (Path.GetFileName(entries[e]).Equals(parts[i], StringComparison.OrdinalIgnoreCase))
                    {
                        match = entries[e];
                        break;
                    }
                }

                if (match == null) { return null; }
                current = match;
            }

            return File.Exists(current) ? current : null;
        }
    }
}
