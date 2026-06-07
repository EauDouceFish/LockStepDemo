using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Command;
using Lockstep.Mugen.Parse;

namespace Lockstep.View
{
    /// <summary>
    /// Minimal character museum validation panel. It is intentionally a thin Unity adapter:
    /// file discovery + read-only diagnostics only; move execution remains in Lockstep.Logic.
    /// </summary>
    public sealed class MugenMuseumDashboard : MonoBehaviour
    {
        public int PerPage = 12;
        public string CommonCharacterFolder = "Terrarian";

        readonly List<string> _characterFolders = new List<string>();
        readonly Dictionary<string, MugenMuseumReport> _reports = new Dictionary<string, MugenMuseumReport>();
        int _page;
        int _selected;

        void Start()
        {
            Application.runInBackground = true;
            ScanCharacters();
            if (_characterFolders.Count > 0)
            {
                BuildReport(_characterFolders[0]);
            }
        }

        void ScanCharacters()
        {
            _characterFolders.Clear();
            string root = MugenRoot();
            if (!Directory.Exists(root))
            {
                Debug.LogError("[MUGEN Museum] Source root not found: " + root);
                return;
            }

            string[] directories = Directory.GetDirectories(root);
            System.Array.Sort(directories);
            for (int i = 0; i < directories.Length; i++)
            {
                string name = Path.GetFileName(directories[i]);
                if (name.StartsWith("_"))
                {
                    continue;
                }
                if (PickMainDef(directories[i]) != null)
                {
                    _characterFolders.Add(directories[i]);
                }
            }
        }

        void OnGUI()
        {
            GUI.Box(new Rect(10f, 10f, 260f, Screen.height - 20f), "MUGEN Character Museum");
            if (_characterFolders.Count == 0)
            {
                GUI.Label(new Rect(24f, 42f, 230f, 24f), "No character DEF packages found.");
                return;
            }

            DrawList();
            DrawReport();
        }

        void DrawList()
        {
            int pageCount = (_characterFolders.Count + PerPage - 1) / PerPage;
            GUI.Label(new Rect(24f, 42f, 220f, 24f),
                "Page " + (_page + 1) + "/" + pageCount + "  Characters " + _characterFolders.Count);

            if (GUI.Button(new Rect(24f, 70f, 82f, 28f), "Prev"))
            {
                _page = (_page + pageCount - 1) % pageCount;
            }
            if (GUI.Button(new Rect(114f, 70f, 82f, 28f), "Next"))
            {
                _page = (_page + 1) % pageCount;
            }

            int start = _page * PerPage;
            for (int i = 0; i < PerPage; i++)
            {
                int index = start + i;
                if (index >= _characterFolders.Count)
                {
                    break;
                }
                string label = Path.GetFileName(_characterFolders[index]);
                if (GUI.Button(new Rect(24f, 108f + i * 30f, 220f, 26f), label))
                {
                    _selected = index;
                    BuildReport(_characterFolders[index]);
                }
            }
        }

        void DrawReport()
        {
            string folder = _characterFolders[_selected];
            MugenMuseumReport report = BuildReport(folder);
            float x = 290f;
            GUI.Box(new Rect(x, 10f, Screen.width - x - 10f, Screen.height - 20f), report.Name);

            float y = 42f;
            DrawLine(x, ref y, "DEF", report.DefPath);
            DrawLine(x, ref y, "localcoord", report.LocalCoord);
            DrawLine(x, ref y, "states", report.StateCount.ToString());
            DrawLine(x, ref y, "common states", report.CommonStateCount.ToString());
            DrawLine(x, ref y, "animations", report.AnimationCount.ToString());
            DrawLine(x, ref y, "commands", report.CommandCount.ToString());
            DrawLine(x, ref y, "command activation", report.ActivatedCommands + "/" + report.CommandCount);
            DrawLine(x, ref y, "unknown controllers", report.UnknownControllers.ToString());
            DrawLine(x, ref y, "parsed-only controllers", report.ParsedOnlyControllers.ToString());
            DrawLine(x, ref y, "native hash", report.NativeHash);
            DrawLine(x, ref y, "status", report.Status);
        }

        static void DrawLine(float x, ref float y, string label, string value)
        {
            GUI.Label(new Rect(x + 18f, y, 150f, 24f), label);
            GUI.Label(new Rect(x + 170f, y, Screen.width - x - 190f, 24f), value ?? "");
            y += 28f;
        }

        MugenMuseumReport BuildReport(string folder)
        {
            if (_reports.TryGetValue(folder, out MugenMuseumReport cached))
            {
                return cached;
            }

            MugenMuseumReport report = new MugenMuseumReport { Name = Path.GetFileName(folder) };
            try
            {
                string defPath = PickMainDef(folder);
                report.DefPath = defPath ?? "";
                MCharacterDefinition definition = MDefParser.Parse(File.ReadAllText(defPath));
                report.LocalCoord = definition.LocalCoordWidth + "x" + definition.LocalCoordHeight;
                MCharData data = LoadCharacter(folder, definition);
                report.StateCount = data.States.Count;
                report.CommonStateCount = data.CommonStates.Count;
                report.AnimationCount = data.Anims.Count;
                report.CommandCount = data.Commands.Count;
                report.UnknownControllers = Sum(data.Compatibility.UnknownControllers);
                report.ParsedOnlyControllers = Sum(data.Compatibility.ParsedOnlyControllers);
                report.ActivatedCommands = CountActivatableCommands(data.Commands);
                report.NativeHash = ComputeInitialHash(data);
                report.Status = report.UnknownControllers == 0 ? "Ready for move probes" : "Import diagnostics present";
            }
            catch (System.Exception ex)
            {
                report.Status = "ImportFailure: " + ex.GetType().Name + " " + ex.Message;
            }
            _reports[folder] = report;
            return report;
        }

        MCharData LoadCharacter(string folder, MCharacterDefinition definition)
        {
            List<string> states = new List<string>();
            for (int i = 0; i < definition.Files.St.Count; i++)
            {
                string text = ReadOptional(folder, definition.Files.St[i]);
                if (text != null)
                {
                    states.Add(text);
                }
            }

            string common = ReadOptional(folder, definition.Files.StCommon);
            if (common == null)
            {
                string commonPath = Path.Combine(MugenRoot(), CommonCharacterFolder, "common1.cns");
                common = File.Exists(commonPath) ? File.ReadAllText(commonPath) : null;
            }

            string name = string.IsNullOrEmpty(definition.DisplayName) ? definition.Name : definition.DisplayName;
            return MCharLoader.Load(
                states,
                ReadOptional(folder, definition.Files.Cns),
                common,
                ReadOptional(folder, definition.Files.Anim),
                ReadOptional(folder, definition.Files.Cmd),
                name,
                definition);
        }

        string ReadOptional(string folder, string relativePath)
        {
            string path = Resolve(folder, relativePath);
            return path != null ? File.ReadAllText(path) : null;
        }

        static int CountActivatableCommands(List<MCommandDef> commands)
        {
            int count = 0;
            for (int i = 0; i < commands.Count; i++)
            {
                MCommandDef command = commands[i];
                if (command == null || string.IsNullOrEmpty(command.Name) || command.Steps.Count == 0)
                {
                    continue;
                }
                if (CanActivate(command))
                {
                    count++;
                }
            }
            return count;
        }

        static bool CanActivate(MCommandDef command)
        {
            List<MInput> sequence = MCommandInputSynthesizer.BuildSequence(command, true);
            MCommandList list = new MCommandList();
            list.Commands.Add(command);
            for (int i = 0; i < sequence.Count; i++)
            {
                list.Update(sequence[i], true);
                if (list.IsActive(command.Name))
                {
                    return true;
                }
            }
            return false;
        }

        static string ComputeInitialHash(MCharData data)
        {
            MBattleEngine engine = new MBattleEngine();
            engine.Add(MCharLoader.SpawnChar(data, 1), data);
            engine.Add(MCharLoader.SpawnChar(data, 2), data);
            engine.LinkPair();
            engine.StartRound();
            return engine.ComputeHash().ToString("x16");
        }

        static int Sum(Dictionary<string, int> values)
        {
            int total = 0;
            foreach (KeyValuePair<string, int> pair in values)
            {
                total += pair.Value;
            }
            return total;
        }

        static string PickMainDef(string folder)
        {
            string[] files = Directory.GetFiles(folder, "*.def");
            for (int i = 0; i < files.Length; i++)
            {
                MDefFiles parsed = MDefParser.ParseFiles(File.ReadAllText(files[i]));
                if (!string.IsNullOrEmpty(parsed.Cmd) && !string.IsNullOrEmpty(parsed.Cns))
                {
                    return files[i];
                }
            }
            return null;
        }

        static string Resolve(string folder, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return null;
            }
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            string direct = Path.Combine(folder, normalized);
            if (File.Exists(direct))
            {
                return direct;
            }

            string current = folder;
            string[] parts = normalized.Split(Path.DirectorySeparatorChar);
            for (int i = 0; i < parts.Length; i++)
            {
                string match = null;
                foreach (string candidate in Directory.GetFileSystemEntries(current))
                {
                    if (string.Equals(Path.GetFileName(candidate), parts[i], System.StringComparison.OrdinalIgnoreCase))
                    {
                        match = candidate;
                        break;
                    }
                }
                if (match == null)
                {
                    return null;
                }
                current = match;
            }
            return File.Exists(current) ? current : null;
        }

        static string MugenRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "MugenSource"));
        }

        sealed class MugenMuseumReport
        {
            public string Name = "";
            public string DefPath = "";
            public string LocalCoord = "";
            public int StateCount;
            public int CommonStateCount;
            public int AnimationCount;
            public int CommandCount;
            public int ActivatedCommands;
            public int UnknownControllers;
            public int ParsedOnlyControllers;
            public string NativeHash = "";
            public string Status = "";
        }
    }
}
