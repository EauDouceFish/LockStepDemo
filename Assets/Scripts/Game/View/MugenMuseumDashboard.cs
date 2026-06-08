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
        readonly List<SpriteRenderer> _helperRenderers = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _projectileRenderers = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _explodRenderers = new List<SpriteRenderer>();
        readonly List<MInput> _inputs = new List<MInput> { MInput.None, MInput.None };
        readonly Queue<MInput> _manualInputs = new Queue<MInput>();
        int _page;
        int _selected;
        int _movePage;
        int _moveHelpPage;
        int _lastManualGuiFrame = -1;
        bool _showMoveHelp;
        MBattleEngine _engine;
        MCharData _data;
        MugenSpriteLoader.Source _source;
        MMovePreviewSession _movePreview;
        string _loadedFolder = "";
        string _previewStatus = "None";
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
                _lastInput = NextMuseumInput();
                _inputs[0] = _lastInput;
                _inputs[1] = MInput.None;
                _engine.Tick(_inputs);
                if (_movePreview != null)
                {
                    _movePreview.AfterTick(_engine);
                    _previewStatus = _movePreview.StatusText;
                    if (_movePreview.TimedOutInMoveState)
                    {
                        ApplyPresentationRecovery(_movePreview);
                    }
                    if (_movePreview.Done)
                    {
                        _movePreview = null;
                    }
                }
                _frame++;
            }
            RenderAll();
        }

        void ApplyPresentationRecovery(MMovePreviewSession preview)
        {
            if (preview == null || _engine == null || _engine.Chars.Count == 0)
            {
                return;
            }
            MChar actor = _engine.Chars[0];
            actor.QueueTransition(0, actor.PlayerNo);
            _previewStatus = preview.StatusText + " | presentation recovery queued";
        }

        MInput NextMuseumInput()
        {
            if (_movePreview != null && !_movePreview.Done)
            {
                return _movePreview.NextInput();
            }
            if (_manualInputs.Count > 0)
            {
                return _manualInputs.Dequeue();
            }
            return SampleInput();
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
            GUI.Box(new Rect(10f, 10f, 260f, Screen.height - 20f), "MUGEN 角色展馆");
            if (_characterFolders.Count == 0)
            {
                GUI.Label(new Rect(24f, 42f, 230f, 24f), "没有找到角色 DEF 包。");
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
                "第 " + (_page + 1) + "/" + pageCount + " 页  角色 " + _characterFolders.Count);

            if (GUI.Button(new Rect(24f, 70f, 82f, 28f), "上一页"))
            {
                _page = (_page + pageCount - 1) % pageCount;
            }
            if (GUI.Button(new Rect(114f, 70f, 82f, 28f), "下一页"))
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
            GUI.Box(new Rect(x - 4f, y - 4f, 228f, 94f), "当前输入");
            string active = "";
            if (_engine != null && _engine.Chars.Count > 0)
            {
                active = ActiveCommandText(_engine.Chars[0]);
            }
            GUI.Label(new Rect(x + 8f, y + 20f, 200f, 20f), "输入：" + MInputDisplayFormatter.Format(_lastInput));
            GUI.Label(new Rect(x + 8f, y + 42f, 200f, 20f), "命令：" + (string.IsNullOrEmpty(active) ? "无" : active));
            GUI.Label(new Rect(x + 8f, y + 64f, 200f, 20f), "方向键移动，A/S/D 拳，Z/X/C 脚");
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
            DrawLine(x, ref y, "状态数", report.StateCount.ToString());
            DrawLine(x, ref y, "公共状态", report.CommonStateCount.ToString());
            DrawLine(x, ref y, "动画数", report.AnimationCount.ToString());
            DrawLine(x, ref y, "命令数", report.CommandCount.ToString());
            DrawLine(x, ref y, "招式入口", _moveEntries.Count.ToString());
            DrawLine(x, ref y, "命令激活", report.ActivatedCommands + "/" + report.CommandCount);
            DrawLine(x, ref y, "未知控制器", report.UnknownControllers.ToString());
            DrawLine(x, ref y, "仅解析控制器", report.ParsedOnlyControllers.ToString());
            DrawLine(x, ref y, "native hash", report.NativeHash);
            DrawLine(x, ref y, "status", report.Status);
            if (_engine != null)
            {
                y += 8f;
                MChar p1 = _engine.Chars[0];
                MChar p2 = _engine.Chars[1];
                DrawLine(x, ref y, "帧", _frame.ToString());
                DrawLiveDebugLine(x, ref y, "P1", p1);
                DrawLiveDebugLine(x, ref y, "P2", p2);
                DrawLine(x, ref y, "输入", MInputDisplayFormatter.Format(_lastInput));
                DrawLine(x, ref y, "当前命令", ActiveCommandText(p1));
                DrawLine(x, ref y, "预览招式", _movePreview != null ? _movePreview.StatusText : _previewStatus);
                DrawLine(x, ref y, "实体", "helper " + _engine.Helpers.Count + " / projectile " +
                    _engine.World.Projectiles.Count + " / explod " + _engine.World.Explods.Count);
                DrawLine(x, ref y, "表现事件", "sound " + _engine.World.Events.Sounds.Count +
                    " / visual " + _engine.World.Events.Visuals.Count + " " + LastVisualEventText());
                DrawLine(x, ref y, "按键说明", MCommandMoveHelpFormatter.KeyboardLegend());
                if (GUI.Button(new Rect(x + 18f, y + 8f, 90f, 30f), "重置"))
                {
                    LoadSession(folder);
                }
                if (GUI.Button(new Rect(x + 116f, y + 8f, 90f, 30f), "轻拳 x"))
                {
                    StepManualInput(MInput.X, 8, "x");
                }
                if (GUI.Button(new Rect(x + 214f, y + 8f, 90f, 30f), "轻脚 a"))
                {
                    StepManualInput(MInput.A, 8, "a");
                }
                if (GUI.Button(new Rect(x + 312f, y + 8f, 110f, 30f), _showMoveHelp ? "关闭说明" : "招式说明"))
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

        static void DrawLiveDebugLine(float x, ref float y, string label, MChar c)
        {
            string firstLine = string.Format(
                "StateNo {0}   AnimNo {1}   Elem {2}   Physics {3}   Type {4}   Ctrl {5}",
                c.StateNo, c.AnimNo, c.AnimElem, c.Physics, c.StateType, c.Ctrl);
            string secondLine = string.Format(
                "Pos ({0:0.0},{1:0.0})   Vel ({2:0.00},{3:0.00})   Facing {4}   Life {5}",
                c.Pos.X.ToFloat(), c.Pos.Y.ToFloat(), c.Vel.X.ToFloat(), c.Vel.Y.ToFloat(),
                c.Facing.Raw < 0 ? -1 : 1, c.Life);

            GUI.Label(new Rect(x + 18f, y, 150f, 24f), label);
            GUI.Label(new Rect(x + 170f, y, Screen.width - x - 190f, 24f), firstLine);
            y += 22f;
            GUI.Label(new Rect(x + 170f, y, Screen.width - x - 190f, 24f), secondLine);
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
                _manualInputs.Clear();
                _movePreview = null;
                _previewStatus = "None";
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
            RenderHelpers();
            RenderProjectiles();
            RenderExplods();
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

        void RenderHelpers()
        {
            EnsureRendererCount(_helperRenderers, _engine.Helpers.Count, "Museum Helper ", 10);
            for (int i = 0; i < _helperRenderers.Count; i++)
            {
                bool active = i < _engine.Helpers.Count;
                _helperRenderers[i].gameObject.SetActive(active);
                if (active)
                {
                    RenderChar(_engine.Helpers[i], _helperRenderers[i]);
                }
            }
        }

        void RenderProjectiles()
        {
            EnsureRendererCount(_projectileRenderers, _engine.World.Projectiles.Count, "Museum Projectile ", 30);
            for (int i = 0; i < _projectileRenderers.Count; i++)
            {
                bool active = i < _engine.World.Projectiles.Count;
                _projectileRenderers[i].gameObject.SetActive(active);
                if (active)
                {
                    MProjectile projectile = _engine.World.Projectiles[i];
                    RenderAnim(projectile.AnimNo, projectile.Pos, projectile.Facing, _projectileRenderers[i]);
                }
            }
        }

        void RenderExplods()
        {
            EnsureRendererCount(_explodRenderers, _engine.World.Explods.Count, "Museum Explod ", 20);
            for (int i = 0; i < _explodRenderers.Count; i++)
            {
                bool active = i < _engine.World.Explods.Count;
                _explodRenderers[i].gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }
                MExplod explod = _engine.World.Explods[i];
                RenderAnim(explod.AnimNo, explod.Pos, FFloat.FromInt(explod.Facing), _explodRenderers[i]);
                _explodRenderers[i].transform.localScale = new Vector3(
                    Mathf.Max(0.01f, explod.ScaleX.ToFloat()) * (explod.Facing < 0 ? -1f : 1f),
                    Mathf.Max(0.01f, explod.ScaleY.ToFloat()) * (explod.VFacing < 0 ? -1f : 1f),
                    1f);
                _explodRenderers[i].sortingOrder = 20 + explod.SprPriority;
            }
        }

        void RenderAnim(int animNo, FVector3 position, FFloat facing, SpriteRenderer renderer)
        {
            renderer.flipX = facing.Raw < 0;
            renderer.transform.localPosition = new Vector3(
                position.X.ToFloat() / PixelsPerUnit,
                -position.Y.ToFloat() / PixelsPerUnit,
                0f);

            if (!_data.Anims.TryGetValue(animNo, out MAnimData anim) || anim.Frames.Length == 0)
            {
                renderer.sprite = null;
                return;
            }

            MAnimFrame frame = anim.Frames[0];
            EnsureBuilt(animNo);
            long key = MugenSpriteAnimator.Key(frame.SpriteGroup, frame.SpriteImage);
            renderer.sprite = _source.Cache.TryGetValue(key, out Sprite sprite) ? sprite : null;
        }

        void EnsureRendererCount(List<SpriteRenderer> renderers, int count, string prefix, int sortingBase)
        {
            while (renderers.Count < count)
            {
                GameObject obj = new GameObject(prefix + renderers.Count);
                obj.transform.SetParent(transform, false);
                SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = sortingBase + renderers.Count;
                renderers.Add(renderer);
            }
        }

        string LastVisualEventText()
        {
            if (_engine == null || _engine.World.Events.Visuals.Count == 0)
            {
                return "";
            }
            MVisualEvent visual = _engine.World.Events.Visuals[_engine.World.Events.Visuals.Count - 1];
            return visual.Type + " owner " + visual.OwnerId + " t " + visual.Time;
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

        void StepManualInput(MInput input, int totalFrames, string commandName = null)
        {
            if (_engine == null)
            {
                return;
            }
            if (ConsumeManualGuiThisFrame())
            {
                return;
            }
            if (StartPreviewForCommand(commandName))
            {
                return;
            }
            _movePreview = null;
            _previewStatus = "Manual button input";
            _manualInputs.Clear();
            _engine.Chars[0].CommandList?.ResetRuntime();
            _lastInput = input;
            _manualInputs.Enqueue(input);
            for (int i = 1; i < totalFrames; i++)
            {
                _manualInputs.Enqueue(MInput.None);
            }
        }

        bool StartPreviewForCommand(string commandName)
        {
            if (string.IsNullOrEmpty(commandName) || _engine == null || _data == null)
            {
                return false;
            }

            MCommandDef command = FindCommandDefinition(commandName);
            int targetStateNo = FindLiteralTargetState(commandName);
            if (command == null || targetStateNo < 0)
            {
                return false;
            }

            _manualInputs.Clear();
            bool facingRight = _engine.Chars[0].Facing.Raw >= 0;
            _engine.Chars[0].CommandList?.ResetRuntime();
            _movePreview = new MMovePreviewSession(new[] { command }, facingRight, targetStateNo,
                "Preview " + commandName + " -> " + targetStateNo);
            _previewStatus = _movePreview.StatusText;
            return true;
        }

        MCommandDef FindCommandDefinition(string commandName)
        {
            for (int i = 0; i < _data.Commands.Count; i++)
            {
                if (_data.Commands[i].Name == commandName)
                {
                    return _data.Commands[i];
                }
            }
            return null;
        }

        int FindLiteralTargetState(string commandName)
        {
            for (int i = 0; i < _moveEntries.Count; i++)
            {
                MCommandTransitionEntry entry = _moveEntries[i];
                if (!entry.TargetStateNo.HasValue)
                {
                    continue;
                }
                for (int j = 0; j < entry.CommandNames.Count; j++)
                {
                    if (entry.CommandNames[j] == commandName)
                    {
                        return entry.TargetStateNo.Value;
                    }
                }
            }
            return -1;
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
                DrawLine(x, ref y, "招式", "没有 Statedef -1 命令入口");
                return;
            }

            int perPage = 6;
            int pageCount = (_moveEntries.Count + perPage - 1) / perPage;
            GUI.Label(new Rect(x + 18f, y, 180f, 24f), "招式 " + (_movePage + 1) + "/" + pageCount);
            if (GUI.Button(new Rect(x + 200f, y, 66f, 24f), "上页"))
            {
                _movePage = (_movePage + pageCount - 1) % pageCount;
            }
            if (GUI.Button(new Rect(x + 272f, y, 66f, 24f), "下页"))
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
                string label = MoveButtonLabel(entry);
                float buttonWidth = Mathf.Max(360f, Screen.width - x - 58f);
                if (GUI.Button(new Rect(x + 18f, y, buttonWidth, 26f), label))
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
            int targetStateNo = entry.TargetStateNo.HasValue ? entry.TargetStateNo.Value : -1;
            _manualInputs.Clear();
            _engine.Chars[0].CommandList?.ResetRuntime();
            _movePreview = new MMovePreviewSession(commands, facingRight, targetStateNo, MoveButtonLabel(entry));
            _previewStatus = _movePreview.StatusText;
        }

        string MoveButtonLabel(MCommandTransitionEntry entry)
        {
            MCommandMoveInfo move = FindMoveInfo(entry);
            if (move != null)
            {
                string target = move.TargetStateNo.HasValue ? move.TargetStateNo.Value.ToString() : move.TargetValue;
                return "预览 " + move.CommandText + " -> " + target;
            }
            string fallbackTarget = entry.TargetStateNo.HasValue ? entry.TargetStateNo.Value.ToString() : entry.TargetValue;
            return "预览 " + string.Join("+", entry.CommandNames.ToArray()) + " -> " + fallbackTarget;
        }

        MCommandMoveInfo FindMoveInfo(MCommandTransitionEntry entry)
        {
            for (int i = 0; i < _moveCatalog.Count; i++)
            {
                MCommandMoveInfo move = _moveCatalog[i];
                if (move.TargetStateNo != entry.TargetStateNo || move.TargetValue != entry.TargetValue)
                {
                    continue;
                }
                if (move.CommandNames.Count != entry.CommandNames.Count)
                {
                    continue;
                }
                bool same = true;
                for (int c = 0; c < entry.CommandNames.Count; c++)
                {
                    if (move.CommandNames[c] != entry.CommandNames[c])
                    {
                        same = false;
                        break;
                    }
                }
                if (same)
                {
                    return move;
                }
            }
            return null;
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
                    MCommandMoveHelpFormatter.FormatMove(move));
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
            GUI.color = new Color(0.05f, 0.07f, 0.10f, 0.92f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.Box(box, "全部招式说明");
            if (GUI.Button(new Rect(x + w - 78f, y + 8f, 64f, 24f), "关闭"))
            {
                _showMoveHelp = false;
                return;
            }
            if (_moveCatalog.Count == 0)
            {
                GUI.Label(new Rect(x + 18f, y + 34f, w - 36f, 24f), "没有可执行招式说明。");
                return;
            }

            int perPage = Mathf.Max(5, Mathf.FloorToInt((h - 122f) / 24f));
            int pageCount = (_moveCatalog.Count + perPage - 1) / perPage;
            GUI.Label(new Rect(x + 18f, y + 34f, w - 36f, 24f),
                "第 " + (_moveHelpPage + 1) + "/" + pageCount + " 页，共 " + _moveCatalog.Count + " 条");
            GUI.Label(new Rect(x + 18f, y + 58f, w - 36f, 24f), MCommandMoveHelpFormatter.KeyboardLegend());
            if (GUI.Button(new Rect(x + w - 150f, y + 32f, 64f, 24f), "上页"))
            {
                _moveHelpPage = (_moveHelpPage + pageCount - 1) % pageCount;
            }
            if (GUI.Button(new Rect(x + w - 78f, y + 32f, 64f, 24f), "下页"))
            {
                _moveHelpPage = (_moveHelpPage + 1) % pageCount;
            }

            float rowY = y + 90f;
            int start = _moveHelpPage * perPage;
            for (int i = 0; i < perPage; i++)
            {
                int index = start + i;
                if (index >= _moveCatalog.Count)
                {
                    break;
                }
                MCommandMoveInfo move = _moveCatalog[index];
                GUI.Label(new Rect(x + 18f, rowY, w - 36f, 22f),
                    MCommandMoveHelpFormatter.FormatMove(move));
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
