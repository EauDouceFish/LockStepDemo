using System;
using System.Collections.Generic;
using System.IO;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Parse;

namespace Lockstep.Tests.Mugen
{
    internal static class MugenCharacterPackageTestLoader
    {
        public static string PickMainDef(string directory)
        {
            foreach (string path in Directory.GetFiles(directory, "*.def"))
            {
                MDefFiles files = MDefParser.ParseFiles(File.ReadAllText(path));
                if (!string.IsNullOrEmpty(files.Cmd) && !string.IsNullOrEmpty(files.Cns)) { return path; }
            }
            return null;
        }

        public static MCharData Load(string directory)
        {
            string defPath = PickMainDef(directory);
            if (defPath == null) { throw new InvalidOperationException("No character DEF in " + directory); }
            MCharacterDefinition definition = MDefParser.Parse(File.ReadAllText(defPath));
            List<string> states = new List<string>();
            for (int i = 0; i < definition.Files.St.Count; i++)
            {
                string text = ReadOptional(directory, definition.Files.St[i]);
                if (text != null) { states.Add(text); }
            }

            string common = ReadOptional(directory, definition.Files.StCommon);
            if (common == null && File.Exists(TestAssets.Common1Cns()))
            {
                common = File.ReadAllText(TestAssets.Common1Cns());
            }
            string name = string.IsNullOrEmpty(definition.DisplayName) ? definition.Name : definition.DisplayName;
            return MCharLoader.Load(
                states,
                ReadOptional(directory, definition.Files.Cns),
                common,
                ReadOptional(directory, definition.Files.Anim),
                ReadOptional(directory, definition.Files.Cmd),
                name,
                definition);
        }

        public static string Resolve(string directory, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) { return null; }
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string direct = Path.Combine(directory, normalized);
            if (File.Exists(direct)) { return direct; }

            string current = directory;
            string[] parts = normalized.Split(Path.DirectorySeparatorChar);
            for (int i = 0; i < parts.Length; i++)
            {
                string match = null;
                foreach (string candidate in Directory.GetFileSystemEntries(current))
                {
                    if (string.Equals(Path.GetFileName(candidate), parts[i], StringComparison.OrdinalIgnoreCase))
                    {
                        match = candidate;
                        break;
                    }
                }
                if (match == null) { return null; }
                current = match;
            }
            return File.Exists(current) ? current : null;
        }

        static string ReadOptional(string directory, string relativePath)
        {
            string path = Resolve(directory, relativePath);
            return path != null ? File.ReadAllText(path) : null;
        }
    }
}
