using System.Collections.Generic;
using System.IO;
using UnityEngine;
using GameData = Lockstep.Game.Data;
using Lockstep.Import.Air;
using Lockstep.Math;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
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
        public float PixelsPerUnit = 50f;
        public float TicksPerSecond = 60f;
        public float StartSeparation = 58f;
        public bool GrantDebugPower = true;

        readonly List<string> _characterFolders = new List<string>();
        readonly List<MCommandTransitionEntry> _moveEntries = new List<MCommandTransitionEntry>();
        readonly List<MCommandMoveInfo> _moveCatalog = new List<MCommandMoveInfo>();
        readonly Dictionary<string, MugenMuseumReport> _reports = new Dictionary<string, MugenMuseumReport>();
        readonly Dictionary<int, GameData.AnimData> _gameAnims = new Dictionary<int, GameData.AnimData>();
        readonly HashSet<int> _builtAnims = new HashSet<int>();
        readonly SpriteRenderer[] _renderers = new SpriteRenderer[2];
        readonly List<MInput> _inputs = new List<MInput> { MInput.None, MInput.None };
        int _page;
        int _selected;
        int _movePage;
        int _moveHelpPage;
        int _lastManualGuiFrame = -1;
        bool _showMoveHelp;
        MBattleEngine _engine;
        MCharData _data;
        MugenSpriteLoader.Source _source;
        string _loadedFolder = "";
        MInput _lastInput;
        float _accumulator;
        int _frame;

        void Start()
        {
            Application.runInBackground = true;
            EnsurePresentationObjects();
            ScanCharacters();
            if (_characterFolders.Count > 0)
            {
                BuildReport(_characterFolders[0]);
                LoadSession(_characterFolders[0]);
            }
        }

        void Update()
        {
            if (_engine == null)
            {
                return;
            }

            _accumulator += Time.deltaTime * TicksPerSecond;
            int guard = 0;
            while (_accumulator >= 1f && guard < 8)
            {
                _accumulator -= 1f;
                guard++;
                _lastInput = SampleInput();
                _inputs[0] = _lastInput;
                _inputs[1] = MInput.None;
                _engine.Tick(_inputs);
                _frame++;
            }
            RenderAll();
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
            if (_showMoveHelp)
            {
                DrawMoveHelpOverlay();
            }
        }

        void DrawList()
        {
            int visibleRows = Mathf.Min(PerPage, Mathf.Max(3, (Screen.height - 228) / 30));
            int pageCount = (_characterFolders.Count + visibleRows - 1) / visibleRows;
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

            DrawInputPanel(24f, 106f);

            int start = _page * visibleRows;
            float listY = 210f;
            for (int i = 0; i < visibleRows; i++)
            {
                int index = start + i;
                if (index >= _characterFolders.Count)
                {
                    break;
                }
                string label = Path.GetFileName(_characterFolders[index]);
                if (GUI.Button(new Rect(24f, listY + i * 30f, 220f, 26f), label))
                {
                    _selected = index;
                    BuildReport(_characterFolders[index]);
                    LoadSession(_characterFolders[index]);
                }
            }
        }

        void DrawInputPanel(float x, float y)
        {
            GUI.Box(new Rect(x - 4f, y - 4f, 228f, 94f), "Input");
            string active = "";
            if (_engine != null && _engine.Chars.Count > 0)
            {
                active = ActiveCommandText(_engine.Chars[0]);
            }
            GUI.Label(new Rect(x + 8f, y + 20f, 200f, 20f), "Current: " + MInputDisplayFormatter.Format(_lastInput));
            GUI.Label(new Rect(x + 8f, y + 42f, 200f, 20f), "Active: " + (string.IsNullOrEmpty(active) ? "None" : active));
            GUI.Label(new Rect(x + 8f, y + 64f, 200f, 20f), "Move: Arrows  Hit: A/S/D Z/X/C");
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
            DrawLine(x, ref y, "move entries", _moveEntries.Count.ToString());
            DrawLine(x, ref y, "command activation", report.ActivatedCommands + "/" + report.CommandCount);
            DrawLine(x, ref y, "unknown controllers", report.UnknownControllers.ToString());
            DrawLine(x, ref y, "parsed-only controllers", report.ParsedOnlyControllers.ToString());
            DrawLine(x, ref y, "native hash", report.NativeHash);
            DrawLine(x, ref y, "status", report.Status);
            if (_engine != null)
            {
                y += 8f;
                MChar p1 = _engine.Chars[0];
                MChar p2 = _engine.Chars[1];
                DrawLine(x, ref y, "live frame", _frame.ToString());
                DrawLine(x, ref y, "P1", "state " + p1.StateNo + " anim " + p1.AnimNo +
                    " life " + p1.Life + " ctrl " + p1.Ctrl);
                DrawLine(x, ref y, "P2", "state " + p2.StateNo + " anim " + p2.AnimNo +
                    " life " + p2.Life + " movetype " + p2.MoveType);
                DrawLine(x, ref y, "input bits", _lastInput.ToString());
                DrawLine(x, ref y, "active cmd", ActiveCommandText(p1));
                DrawLine(x, ref y, "controls", "Arrows move, A/S/D punches, Z/X/C kicks");
                if (GUI.Button(new Rect(x + 18f, y + 8f, 120f, 30f), "Reset Fight"))
                {
                    LoadSession(folder);
                }
                if (GUI.Button(new Rect(x + 148f, y + 8f, 120f, 30f), "Light Punch"))
                {
                    StepManualInput(MInput.X, 8);
                }
                if (GUI.Button(new Rect(x + 278f, y + 8f, 120f, 30f), "Light Kick"))
                {
                    StepManualInput(MInput.A, 8);
                }
                if (GUI.Button(new Rect(x + 348f, y + 8f, 118f, 30f), _showMoveHelp ? "Hide Help" : "Move Help"))
                {
                    _showMoveHelp = !_showMoveHelp;
                    _moveHelpPage = 0;
                }
                y += 48f;
                DrawMoveEntryControls(x, ref y);
            }
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

        void LoadSession(string folder)
        {
            try
            {
                string defPath = PickMainDef(folder);
                MCharacterDefinition definition = MDefParser.Parse(File.ReadAllText(defPath));
                string sffPath = Resolve(folder, definition.Files.Sprite);
                string airPath = Resolve(folder, definition.Files.Anim);
                if (sffPath == null || airPath == null)
                {
                    Debug.LogWarning("[MUGEN Museum] Missing sprite or anim for " + folder);
                    return;
                }

                _data = LoadCharacter(folder, definition);
                _engine = new MBattleEngine();
                MChar p1 = MCharLoader.SpawnChar(_data, 1, 0, 0);
                MChar p2 = MCharLoader.SpawnChar(_data, 2, 0, 0);
                int half = Mathf.RoundToInt(StartSeparation * 0.5f);
                p1.Pos = new FVector3(FFloat.FromInt(-half), FFloat.Zero, FFloat.Zero);
                p1.Facing = FFloat.One;
                p2.Pos = new FVector3(FFloat.FromInt(half), FFloat.Zero, FFloat.Zero);
                p2.Facing = -FFloat.One;
                if (GrantDebugPower)
                {
                    p1.Power = p1.PowerMax;
                    p2.Power = p2.PowerMax;
                }
                _engine.Add(p1, _data);
                _engine.Add(p2, _data);
                _engine.LinkPair();
                _engine.StartRound();

                _source = MugenSpriteLoader.Open(sffPath, PixelsPerUnit);
                _gameAnims.Clear();
                _builtAnims.Clear();
                List<GameData.AnimData> anims = AirParser.ParseFile(airPath);
                for (int i = 0; i < anims.Count; i++)
                {
                    _gameAnims[anims[i].Id] = anims[i];
                }
                _frame = 0;
                _accumulator = 0f;
                _lastInput = MInput.None;
                _loadedFolder = folder;
                _movePage = 0;
                _moveHelpPage = 0;
                LoadMoveEntries(folder, definition);
                RenderAll();
                Debug.Log("[MUGEN Museum] Loaded playable session: " + Path.GetFileName(folder));
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[MUGEN Museum] Failed to load playable session: " + ex);
            }
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

        void EnsurePresentationObjects()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                GameObject character = new GameObject(i == 0 ? "Museum P1" : "Museum P2");
                character.transform.SetParent(transform, false);
                _renderers[i] = character.AddComponent<SpriteRenderer>();
                _renderers[i].sortingOrder = i;
            }
        }

        void RenderAll()
        {
            if (_engine == null || _source == null || _data == null)
            {
                return;
            }
            for (int i = 0; i < 2 && i < _engine.Chars.Count; i++)
            {
                RenderChar(_engine.Chars[i], _renderers[i]);
            }
        }

        void RenderChar(MChar character, SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }
            renderer.flipX = character.Facing.Raw < 0;
            renderer.transform.localPosition = new Vector3(
                character.Pos.X.ToFloat() / PixelsPerUnit,
                -character.Pos.Y.ToFloat() / PixelsPerUnit,
                0f);

            if (!_data.Anims.TryGetValue(character.AnimNo, out MAnimData anim) || anim.Frames.Length == 0)
            {
                return;
            }
            int elem = Mathf.Clamp(character.AnimElem, 0, anim.Frames.Length - 1);
            MAnimFrame frame = anim.Frames[elem];
            EnsureBuilt(character.AnimNo);
            long key = MugenSpriteAnimator.Key(frame.SpriteGroup, frame.SpriteImage);
            if (_source.Cache.TryGetValue(key, out Sprite sprite))
            {
                renderer.sprite = sprite;
            }
        }

        void EnsureBuilt(int animNo)
        {
            if (_builtAnims.Contains(animNo))
            {
                return;
            }
            if (_gameAnims.TryGetValue(animNo, out GameData.AnimData anim))
            {
                MugenSpriteLoader.BuildForAnim(_source, anim);
            }
            _builtAnims.Add(animNo);
        }

        static MInput SampleInput()
        {
            MInput input = MInput.None;
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow)) { input |= MInput.Up; }
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow)) { input |= MInput.Down; }
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)) { input |= MInput.Left; }
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) { input |= MInput.Right; }
            if (UnityEngine.Input.GetKey(KeyCode.A)) { input |= MInput.X; }
            if (UnityEngine.Input.GetKey(KeyCode.S)) { input |= MInput.Y; }
            if (UnityEngine.Input.GetKey(KeyCode.D)) { input |= MInput.Z; }
            if (UnityEngine.Input.GetKey(KeyCode.Z)) { input |= MInput.A; }
            if (UnityEngine.Input.GetKey(KeyCode.X)) { input |= MInput.B; }
            if (UnityEngine.Input.GetKey(KeyCode.C)) { input |= MInput.C; }
            if (UnityEngine.Input.GetKey(KeyCode.Space)) { input |= MInput.S; }
            if (UnityEngine.Input.GetKey(KeyCode.Return)) { input |= MInput.S; }
            return input;
        }

        void StepManualInput(MInput input, int totalFrames)
        {
            if (_engine == null)
            {
                return;
            }
            if (ConsumeManualGuiThisFrame())
            {
                return;
            }
            _lastInput = input;
            _engine.Tick(new[] { input, MInput.None });
            _frame++;
            for (int i = 1; i < totalFrames; i++)
            {
                _engine.Tick(new[] { MInput.None, MInput.None });
                _frame++;
            }
            RenderAll();
        }

        bool ConsumeManualGuiThisFrame()
        {
            if (_lastManualGuiFrame == Time.frameCount)
            {
                return true;
            }
            _lastManualGuiFrame = Time.frameCount;
            return false;
        }

        void DrawMoveEntryControls(float x, ref float y)
        {
            if (_moveEntries.Count == 0 || _data == null)
            {
                DrawLine(x, ref y, "moves", "No Statedef -1 command transitions");
                return;
            }

            int perPage = 6;
            int pageCount = (_moveEntries.Count + perPage - 1) / perPage;
            GUI.Label(new Rect(x + 18f, y, 180f, 24f), "Moves " + (_movePage + 1) + "/" + pageCount);
            if (GUI.Button(new Rect(x + 200f, y, 66f, 24f), "Prev"))
            {
                _movePage = (_movePage + pageCount - 1) % pageCount;
            }
            if (GUI.Button(new Rect(x + 272f, y, 66f, 24f), "Next"))
            {
                _movePage = (_movePage + 1) % pageCount;
            }
            y += 30f;

            int start = _movePage * perPage;
            for (int i = 0; i < perPage; i++)
            {
                int index = start + i;
                if (index >= _moveEntries.Count)
                {
                    break;
                }

                MCommandTransitionEntry entry = _moveEntries[index];
                string target = entry.TargetStateNo.HasValue ? entry.TargetStateNo.Value.ToString() : entry.TargetValue;
                string label = string.Join("+", entry.CommandNames.ToArray()) + " -> " + target;
                if (GUI.Button(new Rect(x + 18f, y, 360f, 26f), label))
                {
                    StepMoveEntry(entry);
                }
                y += 30f;
            }
        }

        void StepMoveEntry(MCommandTransitionEntry entry)
        {
            if (_engine == null || _data == null || entry == null)
            {
                return;
            }
            if (ConsumeManualGuiThisFrame())
            {
                return;
            }

            List<MCommandDef> commands = FirstDefinitions(entry.CommandNames);
            if (commands.Count != entry.CommandNames.Count)
            {
                Debug.LogWarning("[MUGEN Museum] Missing command definitions for " + entry.Describe());
                return;
            }

            bool facingRight = _engine.Chars[0].Facing.Raw >= 0;
            List<MInput> sequence = MCommandInputSynthesizer.BuildCombinedSequence(commands, facingRight);
            for (int i = 0; i < sequence.Count; i++)
            {
                _lastInput = sequence[i];
                _engine.Tick(new[] { sequence[i], MInput.None });
                _frame++;
            }
            for (int i = 0; i < 12; i++)
            {
                _lastInput = MInput.None;
                _engine.Tick(new[] { MInput.None, MInput.None });
                _frame++;
            }
            RenderAll();
        }

        List<MCommandDef> FirstDefinitions(List<string> names)
        {
            List<MCommandDef> result = new List<MCommandDef>();
            for (int nameIndex = 0; nameIndex < names.Count; nameIndex++)
            {
                string name = names[nameIndex];
                for (int i = 0; i < _data.Commands.Count; i++)
                {
                    if (_data.Commands[i].Name == name)
                    {
                        result.Add(_data.Commands[i]);
                        break;
                    }
                }
            }
            return result;
        }

        void LoadMoveEntries(string folder, MCharacterDefinition definition)
        {
            _moveEntries.Clear();
            _moveCatalog.Clear();
            string cmd = ReadOptional(folder, definition.Files.Cmd);
            if (cmd == null)
            {
                return;
            }

            List<MCommandTransitionEntry> parsed = MCommandTransitionCatalog.Parse(cmd);
            for (int i = 0; i < parsed.Count; i++)
            {
                if (IsExecutableMoveEntry(parsed[i]))
                {
                    _moveEntries.Add(parsed[i]);
                }
            }
            _moveCatalog.AddRange(MCommandMoveCatalog.Build(_data.Commands, _moveEntries));
        }

        void DrawMoveHelpPanel(float x, ref float y)
        {
            if (_moveCatalog.Count == 0)
            {
                DrawLine(x, ref y, "move help", "No executable move help.");
                return;
            }

            int perPage = Mathf.Max(4, Mathf.Min(12, (Screen.height - Mathf.RoundToInt(y) - 34) / 24));
            int pageCount = (_moveCatalog.Count + perPage - 1) / perPage;
            GUI.Box(new Rect(x + 8f, y, Screen.width - x - 28f, perPage * 24f + 58f), "All Move Commands");
            GUI.Label(new Rect(x + 22f, y + 26f, 240f, 22f),
                "Move Help " + (_moveHelpPage + 1) + "/" + pageCount + "  Total " + _moveCatalog.Count);
            if (GUI.Button(new Rect(x + 272f, y + 24f, 64f, 24f), "Prev"))
            {
                _moveHelpPage = (_moveHelpPage + pageCount - 1) % pageCount;
            }
            if (GUI.Button(new Rect(x + 342f, y + 24f, 64f, 24f), "Next"))
            {
                _moveHelpPage = (_moveHelpPage + 1) % pageCount;
            }

            float rowY = y + 52f;
            int start = _moveHelpPage * perPage;
            for (int i = 0; i < perPage; i++)
            {
                int index = start + i;
                if (index >= _moveCatalog.Count)
                {
                    break;
                }

                MCommandMoveInfo move = _moveCatalog[index];
                string target = move.TargetStateNo.HasValue ? move.TargetStateNo.Value.ToString() : move.TargetValue;
                GUI.Label(new Rect(x + 22f, rowY, Screen.width - x - 48f, 22f),
                    move.CommandText + "    " + move.MotionText + "    -> state " + target);
                rowY += 24f;
            }
            y += perPage * 24f + 66f;
        }

        void DrawMoveHelpOverlay()
        {
            float x = 286f;
            float y = 16f;
            float w = Screen.width - x - 16f;
            float h = Screen.height - 32f;
            if (w < 260f || h < 180f)
            {
                return;
            }

            Rect box = new Rect(x, y, w, h);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.05f, 0.07f, 0.10f, 0.96f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Box(box, "All Move Commands");
            if (_moveCatalog.Count == 0)
            {
                GUI.Label(new Rect(x + 18f, y + 34f, w - 36f, 24f), "No executable move help.");
                return;
            }

            int perPage = Mathf.Max(5, Mathf.FloorToInt((h - 92f) / 24f));
            int pageCount = (_moveCatalog.Count + perPage - 1) / perPage;
            GUI.Label(new Rect(x + 18f, y + 34f, 260f, 24f),
                "Move Help " + (_moveHelpPage + 1) + "/" + pageCount + "  Total " + _moveCatalog.Count);
            if (GUI.Button(new Rect(x + w - 150f, y + 32f, 64f, 24f), "Prev"))
            {
                _moveHelpPage = (_moveHelpPage + pageCount - 1) % pageCount;
            }
            if (GUI.Button(new Rect(x + w - 78f, y + 32f, 64f, 24f), "Next"))
            {
                _moveHelpPage = (_moveHelpPage + 1) % pageCount;
            }

            float rowY = y + 66f;
            int start = _moveHelpPage * perPage;
            for (int i = 0; i < perPage; i++)
            {
                int index = start + i;
                if (index >= _moveCatalog.Count)
                {
                    break;
                }
                MCommandMoveInfo move = _moveCatalog[index];
                string target = move.TargetStateNo.HasValue ? move.TargetStateNo.Value.ToString() : move.TargetValue;
                GUI.Label(new Rect(x + 18f, rowY, w - 36f, 22f),
                    move.CommandText + "    " + move.MotionText + "    -> state " + target);
                rowY += 24f;
            }
        }

        bool IsExecutableMoveEntry(MCommandTransitionEntry entry)
        {
            if (entry.CommandNames.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < entry.CommandNames.Count; i++)
            {
                if (FirstDefinition(entry.CommandNames[i]) == null)
                {
                    return false;
                }
            }
            return true;
        }

        MCommandDef FirstDefinition(string name)
        {
            if (_data == null)
            {
                return null;
            }
            for (int i = 0; i < _data.Commands.Count; i++)
            {
                if (_data.Commands[i].Name == name)
                {
                    return _data.Commands[i];
                }
            }
            return null;
        }

        static string ActiveCommandText(MChar character)
        {
            if (character.CommandList == null)
            {
                return "";
            }
            List<string> names = character.CommandList.ActiveNames();
            if (names.Count == 0)
            {
                return "";
            }
            return string.Join(", ", names.ToArray());
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
