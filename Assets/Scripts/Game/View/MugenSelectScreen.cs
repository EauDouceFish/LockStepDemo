// 选人页：扫 MugenSource 角色 → 头像格子 → 双方各选 3 名（车轮战）。黄色高亮当前光标格。
// P1：方向键移动 / Z 选 / X 撤销。P2：WASD 移动 / J 选 / K 撤销。
// 模式：按 1=本地双人 2=vs AI 3=联机。R=随机补满 AI(或对方)队伍。Enter=开打（不足则随机补满）。
// 程序化构建 uGUI（任何带相机的场景可挂），确认后写入 MugenMatchSetup 并进入 BattleScene。
using System;
using System.Collections.Generic;
using System.IO;
using Lockstep.Client;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Battle.Net;
using Lockstep.Network;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lockstep.View
{
    public sealed class MugenSelectScreen : MonoBehaviour
    {
        public int TeamSize = MugenMatchProtocol.TeamSize;
        public int Columns = 6;

        static readonly Color BgColor = new Color(0.8313726f, 0.8313726f, 0.8313726f, 1f);
        static readonly Color Pink = new Color(1f, 0.31f, 0.68f, 1f);
        static readonly Color Sky = new Color(0.18f, 0.78f, 1f, 1f);
        static readonly Color Gold = new Color(0.72f, 0.48f, 0.06f, 1f);
        static readonly Color SlotDefaultColor = ColorFromHtml("FF8649");
        static readonly Color PlayerSelectedSlotColor = ColorFromHtml("4A9BFF");
        static readonly Color MatchSuccessPrimaryColor = ColorFromHtml("EE525D");
        static readonly Color MatchSuccessSecondaryColor = ColorFromHtml("1A8532");
        static readonly HashSet<string> HiddenCharacterFolders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Terrarian", "Gustavo" };

        sealed class Entry
        {
            public string Folder;
            public string DisplayName;
            public Sprite Portrait;
            public Image Cell;
            public Outline OutlineP1;
            public Outline OutlineP2;
            public Text PickLabel;
            public Image PortraitImage;
            public GameObject PickHint;
            public TMP_Text PickHintTmp;
            public Text PickHintText;
        }

        sealed class CompatibilitySummary
        {
            public int UnknownControllers;
            public int ParsedOnlyControllers;
            public bool LoadFailed;
        }

        readonly List<Entry> _entries = new List<Entry>();
        readonly List<int> _team0 = new List<int>();   // 选中条目索引
        readonly List<int> _team1 = new List<int>();
        readonly Dictionary<string, CompatibilitySummary> _compatibilityCache =
            new Dictionary<string, CompatibilitySummary>(StringComparer.OrdinalIgnoreCase);
        int _cursor0;
        int _cursor1;
        MugenMatchMode _mode = MugenMatchMode.NetKcp;

        Canvas _canvas;
        Text _status;
        Text _modeButtonLabel;
        Text _startButtonLabel;
        Text _serverLabel;
        Button _cancelButton;
        bool _usingSceneUi;
        Button _matchButton;
        Button _randomButton;
        Button _startButton;
        Button _modeButton;
        Button _matchStateButton;
        TMP_Text _matchButtonTmp;
        TMP_Text _randomButtonTmp;
        TMP_Text _sceneStartButtonTmp;
        TMP_Text _sceneModeButtonTmp;
        TMP_Text _matchStateTmp;
        TMP_Text _characterDescTmp;
        TMP_Text _loadingTmp;
        TMP_Text _serverLabelTmp;
        Text _matchButtonText;
        Text _randomButtonText;
        Text _sceneStartButtonText;
        Text _sceneModeButtonText;
        Text _matchStateText;
        Text _characterDescText;
        Text _loadingText;
        Text _serverLabelText;
        Image _matchButtonImage;
        Image _matchStateImage;
        Image _loadingFill;
        GameObject _matchStateObject;
        GameObject _loadingPromptObject;
        bool _matchHover;
        bool _matchSucceeded;
        float _matchSuccessColorT;
        AsyncOperation _loadOperation;
        bool _loading;
        bool _onlineLoading;
        float _localLoadProgress;
        float _remoteLoadProgress = 1f;
        bool _remoteLoadReady = true;
        readonly List<string> _preloadFolders = new List<string>();
        int _preloadIndex;
        bool _preloadComplete = true;
        float _preloadProgress = 1f;
        float _loadCountdown = -1f;
        float _loadProgressSendTimer;
        string _mugenRoot;
        int _seed;
        KcpClientTransport _netClient;
        bool _matching;
        int _matchRequestSeq;
        int _activeMatchRequestId;
        float _matchLogTimer;
        MugenServerLatencyProbe _latencyProbe;
        int _latencyServerIndex = -1;

        public string ServerHost = "";
        public int ServerPort = MugenMatchProtocol.DefaultServerPort;
        public string Nickname = "MobilePlayer";

        void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = BgColor;
            }
        }

        void Start()
        {
            MugenMatchSetup.ApplySelectedServer();
            ServerHost = MugenMatchSetup.NetHost;
            ServerPort = MugenMatchSetup.NetPort;
            _mugenRoot = MugenAssetPaths.MugenRoot();
            DiscoverCharacters();
            if (!BindSceneUi())
            {
                BuildUi();
            }
            StartLatencyProbe();
            Refresh();
            MugenAutoTest.Trace("scene_select", "entries=" + _entries.Count);
            RunAutoTestMatchmaking();
        }

        void DiscoverCharacters()
        {
            if (!Directory.Exists(_mugenRoot)) { return; }
            string[] dirs = Directory.GetDirectories(_mugenRoot);
            Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < dirs.Length; i++)
            {
                string folder = Path.GetFileName(dirs[i]);
                if (HiddenCharacterFolders.Contains(folder)) { continue; }
                if (folder.StartsWith("_")) { continue; }   // 跳过 _reference 等
                if (!MugenCharacterPackageLoader.TryLoad(dirs[i], out MugenCharacterPackage package) ||
                    !MugenCharacterPackageLoader.IsBattleLoadable(package))
                {
                    continue;
                }
                Sprite portrait = LoadPortrait(package.SpritePath);
                _entries.Add(new Entry
                {
                    Folder = folder,
                    DisplayName = MugenChineseText.CharacterName(folder),
                    Portrait = portrait,
                });
            }
        }

        Sprite LoadPortrait(string sffPath)
        {
            try
            {
                MugenSpriteLoader.Source src = MugenSpriteLoader.Open(sffPath, 100f);
                Sprite s = MugenSpriteLoader.BuildSingle(src, 9000, 1);   // 小头像
                if (s == null) { s = MugenSpriteLoader.BuildSingle(src, 9000, 0); }   // 大头像
                return s;
            }
            catch
            {
                return null;
            }
        }

        bool BindSceneUi()
        {
            EnsureEventSystem();

            Canvas sceneCanvas = FindObjectOfType<Canvas>();
            Transform grid = FindSceneTransform("角色选择面板", requireActive: false);
            Transform template = grid != null ? FindChildRecursive(grid, "CharacterSlot") : null;
            if (sceneCanvas == null || grid == null || template == null)
            {
                return false;
            }

            _canvas = sceneCanvas;
            _usingSceneUi = true;

            _matchButton = FindSceneButton("匹配对手按钮", requireActive: false);
            _randomButton = FindSceneButton("随机挑选对手按钮", requireActive: false);
            _startButton = FindSceneButton("开始游戏按钮", requireActive: false);
            _modeButton = FindSceneButton("选择模式按钮", requireActive: false);
            _matchButtonImage = _matchButton != null ? _matchButton.GetComponent<Image>() : null;

            BindButtonText(_matchButton, "匹配对手Desc", out _matchButtonTmp, out _matchButtonText);
            BindButtonText(_randomButton, "随机挑选Desc", out _randomButtonTmp, out _randomButtonText);
            BindButtonText(_startButton, "开始游戏Desc", out _sceneStartButtonTmp, out _sceneStartButtonText);
            BindButtonText(_modeButton, "模式Desc", out _sceneModeButtonTmp, out _sceneModeButtonText);

            Transform matchState = _matchButton != null ? FindChildRecursive(_matchButton.transform, "匹配中Desc") : null;
            _matchStateObject = matchState != null ? matchState.gameObject : null;
            _matchStateImage = matchState != null ? matchState.GetComponent<Image>() : null;
            BindText(matchState, "匹配中匹配成功Desc", out _matchStateTmp, out _matchStateText);

            Transform loading = FindSceneTransform("提示文本", requireActive: false);
            _loadingPromptObject = loading != null ? loading.gameObject : null;
            Transform fill = loading != null ? FindChildRecursive(loading, "FillAmount") : null;
            _loadingFill = fill != null ? fill.GetComponent<Image>() : null;
            BindText(loading, "Text (TMP)", out _loadingTmp, out _loadingText);

            Transform selectPanel = FindSceneTransform("选人面板", requireActive: false);
            BindText(selectPanel, "角色选择Desc", out _characterDescTmp, out _characterDescText);
            BindText(null, "当前服务器info", out _serverLabelTmp, out _serverLabelText);

            if (_matchButton != null)
            {
                _matchButton.onClick.RemoveAllListeners();
                _matchButton.onClick.AddListener(OnMatchButtonClicked);
            }
            if (_matchStateObject != null)
            {
                _matchStateButton = _matchStateObject.GetComponent<Button>();
                if (_matchStateButton == null)
                {
                    _matchStateButton = _matchStateObject.AddComponent<Button>();
                }
                _matchStateButton.targetGraphic = _matchStateImage;
                _matchStateButton.onClick.RemoveAllListeners();
                _matchStateButton.onClick.AddListener(OnMatchStateButtonClicked);

                MatchHoverRelay relay = _matchStateObject.GetComponent<MatchHoverRelay>();
                if (relay == null)
                {
                    relay = _matchStateObject.AddComponent<MatchHoverRelay>();
                }
                relay.Owner = this;
            }
            if (_randomButton != null)
            {
                _randomButton.onClick.RemoveAllListeners();
                _randomButton.onClick.AddListener(() =>
                {
                    RandomizeTeam(_team1);
                    Refresh();
                });
            }
            if (_startButton != null)
            {
                _startButton.onClick.RemoveAllListeners();
                _startButton.onClick.AddListener(TryStart);
            }
            if (_modeButton != null)
            {
                _modeButton.onClick.RemoveAllListeners();
                _modeButton.onClick.AddListener(CycleMode);
            }

            BuildSceneCharacterSlots(grid, template);
            if (_loadingPromptObject != null)
            {
                _loadingPromptObject.SetActive(false);
            }
            return true;
        }

        void BuildSceneCharacterSlots(Transform grid, Transform template)
        {
            for (int i = grid.childCount - 1; i >= 0; i--)
            {
                Transform child = grid.GetChild(i);
                if (child != template && child.name.StartsWith("CharacterSlot_", StringComparison.Ordinal))
                {
                    Destroy(child.gameObject);
                }
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                Transform slot = i == 0 ? template : Instantiate(template, grid);
                if (i > 0)
                {
                    slot.name = "CharacterSlot_" + entry.Folder;
                }
                slot.gameObject.SetActive(true);

                Image slotImage = slot.GetComponent<Image>();
                if (slotImage != null)
                {
                    slotImage.color = SlotDefaultColor;
                }

                Transform inner = FindChildRecursive(slot, "InnerImage");
                Image portraitImage = inner != null ? inner.GetComponent<Image>() : null;
                if (portraitImage != null)
                {
                    portraitImage.sprite = entry.Portrait;
                    portraitImage.preserveAspect = true;
                    portraitImage.color = entry.Portrait != null ? Color.white : new Color(0.24f, 0.24f, 0.24f, 1f);
                }

                Transform hint = FindChildRecursive(slot, "选择提示");
                TMP_Text hintTmp = hint != null ? hint.GetComponentInChildren<TMP_Text>(true) : null;
                Text hintText = hint != null ? hint.GetComponentInChildren<Text>(true) : null;

                Button button = slot.GetComponent<Button>();
                if (button == null)
                {
                    button = slot.gameObject.AddComponent<Button>();
                }
                button.targetGraphic = slotImage;
                int capturedIndex = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnCellClicked(capturedIndex));

                entry.Cell = slotImage;
                entry.PortraitImage = portraitImage;
                entry.PickHint = hint != null ? hint.gameObject : null;
                entry.PickHintTmp = hintTmp;
                entry.PickHintText = hintText;
                if (entry.PickHint != null)
                {
                    entry.PickHint.SetActive(false);
                }
            }
        }

        void BuildUi()
        {
            EnsureEventSystem();
            GameObject canvasGo = new GameObject("SelectCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            Font font = MugenChineseText.Font();

            AddBackground();

            MakeText(font, "角色选择 / 组队大厅", 28, new Vector2(0.5f, 1f), new Vector2(0f, -24f),
                new Vector2(900f, 40f), TextAnchor.UpperCenter);
            _status = MakeText(font, "", 18, new Vector2(0.5f, 0f), new Vector2(0f, 24f),
                new Vector2(1180f, 96f), TextAnchor.LowerCenter);
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;

            const float cell = 110f;
            const float gap = 12f;
            int cols = Columns;
            for (int i = 0; i < _entries.Count; i++)
            {
                int row = i / cols;
                int col = i % cols;
                Entry e = _entries[i];

                GameObject cellGo = new GameObject("Cell_" + e.Folder);
                cellGo.transform.SetParent(_canvas.transform, false);
                Image img = cellGo.AddComponent<Image>();
                img.sprite = e.Portrait;
                img.color = e.Portrait != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 1f);
                Button button = cellGo.AddComponent<Button>();
                button.targetGraphic = img;
                int capturedIndex = i;
                button.onClick.AddListener(() => OnCellClicked(capturedIndex));
                RectTransform rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cell, cell);
                float gridW = cols * (cell + gap);
                rt.anchoredPosition = new Vector2(
                    -gridW * 0.5f + col * (cell + gap) + cell * 0.5f,
                    140f - row * (cell + gap));

                Outline o1 = cellGo.AddComponent<Outline>();
                o1.effectColor = new Color(1f, 0.9f, 0.1f, 1f);   // P1 黄色高亮
                o1.effectDistance = new Vector2(5f, 5f);
                o1.enabled = false;
                Outline o2 = cellGo.AddComponent<Outline>();
                o2.effectColor = new Color(0.2f, 0.7f, 1f, 1f);   // P2 蓝
                o2.effectDistance = new Vector2(-5f, -5f);
                o2.enabled = false;

                Text label = MakeText(font, "", 16, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(cell, cell),
                    TextAnchor.LowerCenter);
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.transform.SetParent(cellGo.transform, false);
                label.rectTransform.anchoredPosition = Vector2.zero;

                e.Cell = img; e.OutlineP1 = o1; e.OutlineP2 = o2; e.PickLabel = label;
            }

            Button modeButton = MakeButton(font, "模式", new Vector2(0f, 0f), new Vector2(110f, 84f), new Vector2(160f, 64f), CycleMode);
            _modeButtonLabel = modeButton.GetComponentInChildren<Text>();
            MakeButton(font, "补齐对手", new Vector2(0f, 0f), new Vector2(290f, 84f), new Vector2(160f, 64f), () => RandomFill(_team1));
            Button startButton = MakeButton(font, "开始", new Vector2(1f, 0f), new Vector2(-300f, 84f), new Vector2(210f, 64f), TryStart);
            _startButtonLabel = startButton.GetComponentInChildren<Text>();
            _cancelButton = MakeButton(font, "取消", new Vector2(1f, 0f), new Vector2(-100f, 84f), new Vector2(150f, 64f), CancelMatch);

            _serverLabel = MakeText(font, "", 16, new Vector2(1f, 0f), new Vector2(-260f, 42f),
                new Vector2(460f, 26f), TextAnchor.MiddleRight);
        }

        void AddBackground()
        {
            GameObject go = new GameObject("LobbyBackground");
            go.transform.SetParent(_canvas.transform, false);
            Image image = go.AddComponent<Image>();
            image.color = BgColor;
            RectTransform rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.transform.SetAsFirstSibling();
        }

        void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) { return; }
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        Text MakeText(Font font, string s, int size, Vector2 anchor, Vector2 pos, Vector2 sz, TextAnchor align)
        {
            GameObject go = new GameObject("Txt");
            go.transform.SetParent(_canvas.transform, false);
            Text t = go.AddComponent<Text>();
            t.font = font; t.text = s; t.fontSize = size; t.alignment = align; t.color = Gold;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = sz;
            rt.anchoredPosition = pos;
            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.72f);
            shadow.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        Button MakeButton(Font font, string label, Vector2 anchor, Vector2 pos, Vector2 sz, UnityEngine.Events.UnityAction action)
        {
            GameObject go = new GameObject("Btn_" + label);
            go.transform.SetParent(_canvas.transform, false);
            Image img = go.AddComponent<Image>();
            Color baseColor = ButtonColor(label);
            img.color = baseColor;
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = btn.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.3f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.28f);
            colors.selectedColor = Color.Lerp(baseColor, Color.white, 0.18f);
            colors.disabledColor = new Color(0.22f, 0.22f, 0.28f, 0.55f);
            colors.fadeDuration = 0.06f;
            colors.colorMultiplier = 1.1f;
            btn.colors = colors;
            btn.onClick.AddListener(action);
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = sz;
            rt.anchoredPosition = pos;

            Text text = MakeText(font, label, 20, new Vector2(0.5f, 0.5f), Vector2.zero, sz, TextAnchor.MiddleCenter);
            text.transform.SetParent(go.transform, false);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.28f);
            outline.effectDistance = new Vector2(2f, -2f);
            return btn;
        }

        static Color ButtonColor(string label)
        {
            if (label.IndexOf("Cancel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("取消", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return new Color(1f, 0.22f, 0.48f, 1f);
            }
            if (label.IndexOf("Start", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("Match", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("开始", StringComparison.OrdinalIgnoreCase) >= 0 ||
                label.IndexOf("匹配", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Pink;
            }
            return Sky;
        }

        void OnCellClicked(int index)
        {
            int existing = _team0.IndexOf(index);
            if (existing >= 0)
            {
                _team0.RemoveAt(existing);
            }
            else
            {
                Pick(_team0, index);
            }
            _cursor0 = index;
            Refresh();
        }

        void CycleMode()
        {
            if (_matching) { CancelMatch(); }
            _mode = _usingSceneUi
                ? (_mode == MugenMatchMode.NetKcp ? MugenMatchMode.VersusAI : MugenMatchMode.NetKcp)
                : (MugenMatchMode)(((int)_mode + 1) % 3);
            Refresh();
        }

        void SetMode(MugenMatchMode mode)
        {
            if (_mode == mode) { return; }
            if (_matching) { CancelMatch(); }
            _mode = mode;
            Refresh();
        }

        void Update()
        {
            if (_latencyProbe != null)
            {
                _latencyProbe.Update();
            }
            UpdateMatchVisuals(Time.deltaTime);
            if (_loading)
            {
                PumpNetwork();
                UpdateLoading();
                Refresh();
                return;
            }

            if (_entries.Count == 0)
            {
                Refresh();
                return;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1)) { SetMode(MugenMatchMode.LocalVersus); }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2)) { SetMode(MugenMatchMode.VersusAI); }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)) { SetMode(MugenMatchMode.NetKcp); }

            _cursor0 = MoveCursor(_cursor0, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow);
            if (UnityEngine.Input.GetKeyDown(KeyCode.Z)) { Pick(_team0, _cursor0); }
            if (UnityEngine.Input.GetKeyDown(KeyCode.X)) { Undo(_team0); }

            // P2 selection is always available; in AI mode it defines the AI roster.
            _cursor1 = MoveCursor(_cursor1, KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S);
            if (UnityEngine.Input.GetKeyDown(KeyCode.J)) { Pick(_team1, _cursor1); }
            if (UnityEngine.Input.GetKeyDown(KeyCode.K)) { Undo(_team1); }

            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) { RandomizeTeam(_team1); }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)) { TryStart(); }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (_matching)
                {
                    CancelMatch();
                }
                else
                {
                    SceneManager.LoadScene(MugenMatchSetup.MainMenuSceneName);
                    return;
                }
            }

            PumpNetwork();
            LogMatchWaitIfNeeded(Time.deltaTime);
            Refresh();
        }

        int MoveCursor(int cursor, KeyCode left, KeyCode right, KeyCode up, KeyCode down)
        {
            if (UnityEngine.Input.GetKeyDown(left)) { cursor--; }
            if (UnityEngine.Input.GetKeyDown(right)) { cursor++; }
            if (UnityEngine.Input.GetKeyDown(up)) { cursor -= Columns; }
            if (UnityEngine.Input.GetKeyDown(down)) { cursor += Columns; }
            if (cursor < 0) { cursor += _entries.Count; }
            return ((cursor % _entries.Count) + _entries.Count) % _entries.Count;
        }

        void Pick(List<int> team, int index)
        {
            if (team.Count >= TeamSize) { return; }
            team.Add(index);
        }

        void Undo(List<int> team)
        {
            if (team.Count > 0) { team.RemoveAt(team.Count - 1); }
        }

        void RandomFill(List<int> team)
        {
            List<int> candidates = new List<int>();
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!team.Contains(i))
                {
                    candidates.Add(i);
                }
            }
            while (team.Count < TeamSize && candidates.Count > 0)
            {
                int pick = NextRandomEntryIndex() % candidates.Count;
                team.Add(candidates[pick]);
                candidates.RemoveAt(pick);
            }
        }

        void RandomizeTeam(List<int> team)
        {
            team.Clear();
            _seed ^= Environment.TickCount;
            List<int> candidates = new List<int>();
            for (int i = 0; i < _entries.Count; i++)
            {
                candidates.Add(i);
            }
            while (team.Count < TeamSize && candidates.Count > 0)
            {
                int pick = NextRandomEntryIndex() % candidates.Count;
                team.Add(candidates[pick]);
                candidates.RemoveAt(pick);
            }
        }

        void RunAutoTestMatchmaking()
        {
            if (!MugenAutoTest.Enabled || _entries.Count == 0)
            {
                return;
            }

            Nickname = MugenAutoTest.ClientId;
            _team0.Clear();
            if (!TryApplyAutoTestTeam(MugenAutoTest.TeamCsv, _team0))
            {
                int count = Mathf.Min(TeamSize, _entries.Count);
                for (int i = 0; i < count; i++)
                {
                    _team0.Add(i);
                }
            }
            SetMode(MugenMatchMode.NetKcp);
            Refresh();
            MugenAutoTest.Trace("button_match", "auto team=" + string.Join(",", AutoTestTeamNames()));
            StartMatchmaking();
        }

        bool TryApplyAutoTestTeam(string csv, List<int> target)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return false;
            }

            string[] parts = csv.Split(',');
            for (int i = 0; i < parts.Length && target.Count < TeamSize; i++)
            {
                string folder = parts[i].Trim();
                if (string.IsNullOrEmpty(folder))
                {
                    continue;
                }

                int index = FindEntryIndex(folder);
                if (index >= 0)
                {
                    target.Add(index);
                }
                else
                {
                    MugenAutoTest.Trace("autotest_team_missing", folder);
                }
            }
            return target.Count > 0;
        }

        int FindEntryIndex(string folder)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].Folder, folder, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        List<string> AutoTestTeamNames()
        {
            List<string> result = new List<string>();
            for (int i = 0; i < _team0.Count; i++)
            {
                int index = _team0[i];
                if (index >= 0 && index < _entries.Count)
                {
                    result.Add(_entries[index].Folder);
                }
            }
            return result;
        }

        int NextRandomEntryIndex()
        {
            _seed = unchecked(_seed * 1103515245 + 12345);
            return ((_seed >> 16) & 0x7fff) % _entries.Count;
        }

        void TryStart()
        {
            if (_loading)
            {
                return;
            }
            if (_mode == MugenMatchMode.NetKcp)
            {
                if (_team0.Count < TeamSize) { return; }
                StartMatchmaking();
                return;
            }

            if (_mode == MugenMatchMode.VersusAI) { RandomFill(_team1); }
            if (_team0.Count < TeamSize || _team1.Count < TeamSize) { return; }

            MugenMatchSetup.Clear();
            MugenMatchSetup.Mode = _mode;
            MugenMatchSetup.AiSeed = (_seed & 0x7fffffff) + 1;
            for (int i = 0; i < _team0.Count; i++) { MugenMatchSetup.Team0.Add(_entries[_team0[i]].Folder); }
            for (int i = 0; i < _team1.Count; i++) { MugenMatchSetup.Team1.Add(_entries[_team1[i]].Folder); }

            BeginLoading(online: false);
        }

        void StartMatchmaking()
        {
            if (_matching || _loading)
            {
                Debug.Log(string.Format("[匹配客户端] 忽略匹配请求：当前状态 matching={0} loading={1}", _matching, _loading));
                MugenAutoTest.Trace("match_request_ignored", "matching=" + _matching + " loading=" + _loading);
                return;
            }
            if (_team0.Count < TeamSize)
            {
                Debug.Log(string.Format("[匹配客户端] 未发送匹配：队伍未满 当前={0}/{1}", _team0.Count, TeamSize));
                MugenAutoTest.Trace("match_request_blocked", "team=" + _team0.Count + "/" + TeamSize);
                return;
            }

            MugenMatchSetup.Clear();
            for (int i = 0; i < _team0.Count; i++) { MugenMatchSetup.Team0.Add(_entries[_team0[i]].Folder); }
            MugenMatchSetup.ApplySelectedServer();
            ServerHost = MugenMatchSetup.NetHost;
            ServerPort = MugenMatchSetup.NetPort;
            Debug.Log(string.Format("[匹配客户端] 使用匹配服务器 {0}:{1}", ServerHost, ServerPort));

            if (_netClient != null && (!_netClient.IsConnected || _netClient.Faulted))
            {
                Debug.Log(string.Format("[匹配客户端] 丢弃旧连接：connected={0} fault={1}",
                    _netClient.IsConnected, _netClient.FaultReason));
                CloseMatchClient(notifyServer: false);
            }

            if (_netClient == null)
            {
                _netClient = new KcpClientTransport();
                Debug.Log(string.Format("[匹配客户端] 连接匹配服务器 {0}:{1}", MugenMatchSetup.NetHost, MugenMatchSetup.NetPort));
                _netClient.Connect(MugenMatchSetup.NetHost, MugenMatchSetup.NetPort);
            }

            _matching = true;
            _matchSucceeded = false;
            _matchSuccessColorT = 0f;
            _activeMatchRequestId = ++_matchRequestSeq;
            _matchLogTimer = 2f;
            string teamCsv = MugenMatchSetup.ToCsv(MugenMatchSetup.Team0);
            string contentHash = BuildContentHash();
            string clientInstanceId = MugenAutoTest.ClientInstanceId;
            MugenAutoTest.SendTrace(_netClient, "match_request",
                "team=" + teamCsv + " hash=" + contentHash + " instance=" + clientInstanceId);
            Debug.Log(string.Format(
                "[匹配客户端] 发送匹配请求 request={0} 昵称={1} 版本={2} 内容Hash={3} 队伍={4}",
                _activeMatchRequestId, Nickname, MugenMatchProtocol.ClientVersion, contentHash, teamCsv));
            _netClient.Send(-1, new FindMatchMsg
            {
                RequestId = _activeMatchRequestId,
                Nickname = Nickname,
                TeamCsv = teamCsv,
                ContentHash = contentHash,
                ClientVersion = MugenMatchProtocol.ClientVersion,
                ClientInstanceId = clientInstanceId,
            });
            _netClient.Flush();
            Debug.Log(string.Format("[匹配客户端] 匹配请求已写入网络 request={0}", _activeMatchRequestId));
            Refresh();
        }

        void CancelMatch()
        {
            if (_loading)
            {
                return;
            }
            if (_netClient != null)
            {
                Debug.Log(string.Format("[匹配客户端] 取消匹配 request={0}", _activeMatchRequestId));
                if (_netClient.IsConnected && !_netClient.Faulted)
                {
                    _netClient.Send(-1, new CancelMatchMsg { RequestId = _activeMatchRequestId });
                    _netClient.Flush();
                }
            }
            _matching = false;
            _matchSucceeded = false;
            _matchHover = false;
            _activeMatchRequestId = 0;
            CloseMatchClient(notifyServer: false);
            Refresh();
        }

        void PumpNetwork()
        {
            if (_netClient == null) { return; }

            _netClient.Update();
            MugenAutoTest.FlushPendingTrace(_netClient);
            if (_netClient.Faulted || !_netClient.IsConnected)
            {
                Debug.Log(string.Format("[匹配客户端] 匹配连接断开：connected={0} fault={1}",
                    _netClient.IsConnected, _netClient.FaultReason));
                _matching = false;
                _matchSucceeded = false;
                _loading = false;
                _activeMatchRequestId = 0;
                CloseMatchClient(notifyServer: false);
                Refresh();
                return;
            }

            while (_netClient.Poll(out int _, out IMessage message))
            {
                if (message is ServerLogMsg serverLog)
                {
                    Debug.Log("[服务器广播] " + serverLog.Message);
                    continue;
                }
                if (message is MatchFoundMsg found)
                {
                    if (!_matching || found.RequestId != _activeMatchRequestId)
                    {
                        Debug.Log(string.Format(
                            "[匹配客户端] 忽略过期匹配成功：收到request={0} 当前request={1} matching={2}",
                            found.RequestId, _activeMatchRequestId, _matching));
                        continue;
                    }
                    Debug.Log(string.Format(
                        "[匹配客户端] 收到匹配成功：房间={0} 本地玩家={1} 种子={2} 对手={3} 队伍0={4} 队伍1={5}",
                        found.RoomId, found.LocalPlayerId, found.Seed, found.OpponentName, found.Team0Csv, found.Team1Csv));
                    MugenAutoTest.SendTrace(_netClient, "match_found",
                        "local=" + found.LocalPlayerId + " opponent=" + found.OpponentName,
                        found.RoomId);
                    PrepareNetBattle(found);
                    BeginLoading(online: true);
                    return;
                }
                if (message is LoadProgressMsg load && _onlineLoading && load.RoomId == MugenMatchSetup.NetRoomId)
                {
                    _remoteLoadProgress = Mathf.Clamp01(load.ProgressPermille / 1000f);
                    _remoteLoadReady = load.Ready || load.ProgressPermille >= 1000;
                    if (_remoteLoadReady)
                    {
                        Debug.Log(string.Format("[匹配客户端] 对方加载完成：房间={0} 玩家={1}", load.RoomId, load.PlayerId));
                    }
                    continue;
                }
                if (message is RoomClosedMsg closed)
                {
                    if (closed.RequestId != 0 && closed.RequestId != _activeMatchRequestId)
                    {
                        continue;
                    }
                    Debug.Log(string.Format(
                        "[匹配客户端] 房间关闭/匹配结束：request={0} 房间={1} 原因={2}",
                        closed.RequestId, closed.RoomId, MatchReasonText(closed.Reason)));
                    _matching = false;
                    _matchSucceeded = false;
                    _loading = false;
                    _activeMatchRequestId = 0;
                    CloseMatchClient(notifyServer: false);
                    Refresh();
                    return;
                }
            }
        }

        void CloseMatchClient(bool notifyServer)
        {
            if (_netClient == null)
            {
                return;
            }

            if (notifyServer && _netClient.IsConnected && !_netClient.Faulted && _activeMatchRequestId != 0)
            {
                _netClient.Send(-1, new CancelMatchMsg { RequestId = _activeMatchRequestId });
                _netClient.Flush();
            }
            _netClient.Close();
            _netClient = null;
        }

        void LogMatchWaitIfNeeded(float dt)
        {
            if (!_matching || _loading || _matchSucceeded)
            {
                return;
            }

            _matchLogTimer -= dt;
            if (_matchLogTimer > 0f)
            {
                return;
            }

            _matchLogTimer = 2f;
            bool connected = _netClient != null && _netClient.IsConnected;
            Debug.Log(string.Format(
                "[匹配客户端] 匹配中：等待服务器响应 {0}:{1} request={2} 本地连接={3}",
                ServerHost, ServerPort, _activeMatchRequestId, connected));
        }

        static string MatchReasonText(string reason)
        {
            if (string.IsNullOrEmpty(reason)) { return "<空>"; }
            switch (reason)
            {
                case "missing client version": return "缺少客户端版本";
                case "missing content hash": return "缺少内容Hash";
                case "missing team": return "缺少队伍";
                case "invalid team size": return "队伍人数不正确";
                case "invalid character name": return "角色名非法";
                case "match timeout": return "匹配超时";
                case "match superseded": return "匹配已被新请求取代";
                case "opponent disconnected": return "对手断开连接";
                case "connection timeout": return "连接超时";
                case "match cancelled": return "匹配已取消";
                case "room not found": return "房间不存在";
                case "not in room": return "玩家不在房间内";
                case "ready mismatch": return "房间就绪信息不匹配";
                case "player left room": return "玩家离开房间";
                case "invalid hash report": return "Hash上报非法";
                case "conflicting hash report": return "Hash上报冲突";
                case "invalid input": return "输入非法";
                case "conflicting input": return "输入冲突";
                case "input frame jump": return "输入帧跳变过大";
            }
            if (reason.StartsWith("hash mismatch frame "))
            {
                return "Hash不一致 " + reason.Substring("hash mismatch ".Length);
            }
            return reason;
        }

        string BuildContentHash()
        {
            unchecked
            {
                uint hash = 2166136261u;
                AddHash(ref hash, MugenMatchProtocol.ClientVersion);
                AddHash(ref hash, "players=" + MugenMatchProtocol.PlayerCount);
                AddHash(ref hash, "team=" + MugenMatchProtocol.TeamSize);
                string common = Path.Combine(_mugenRoot, MugenMatchSetup.CommonFolder, "common1.cns");
                HashFile(ref hash, common, true);
                HashCharacterCatalog(ref hash);
                return hash.ToString("X8");
            }
        }

        void HashCharacterCatalog(ref uint hash)
        {
            List<string> folders = new List<string>();
            for (int i = 0; i < _entries.Count; i++)
            {
                folders.Add(_entries[i].Folder);
            }
            folders.Sort(StringComparer.OrdinalIgnoreCase);

            AddHash(ref hash, "catalog=" + folders.Count);
            for (int i = 0; i < folders.Count; i++)
            {
                string folder = folders[i];
                AddHash(ref hash, "char=" + folder);
                string directory = Path.Combine(_mugenRoot, folder);
                if (!MugenCharacterPackageLoader.TryLoad(directory, out MugenCharacterPackage package) ||
                    !MugenCharacterPackageLoader.IsBattleLoadable(package))
                {
                    AddHash(ref hash, "missing-character");
                    continue;
                }
                HashPackage(ref hash, package);
            }
        }

        static void HashPackage(ref uint hash, MugenCharacterPackage package)
        {
            HashFileWithLabel(ref hash, "def", package.DefPath, includeContents: true);
            HashFileWithLabel(ref hash, "constants", package.ConstantsPath, includeContents: true);
            HashFileWithLabel(ref hash, "stcommon", package.StCommonPath, includeContents: true);
            HashFileWithLabel(ref hash, "cmd", package.CmdPath, includeContents: true);
            HashFileWithLabel(ref hash, "air", package.AnimPath, includeContents: true);
            HashLargeFileSampleWithLabel(ref hash, "sff", package.SpritePath);

            List<string> states = new List<string>(package.StatePaths);
            states.Sort(StringComparer.OrdinalIgnoreCase);
            AddHash(ref hash, "states=" + states.Count);
            for (int i = 0; i < states.Count; i++)
            {
                HashFileWithLabel(ref hash, "state" + i, states[i], includeContents: true);
            }
        }

        static void HashFileWithLabel(ref uint hash, string label, string path, bool includeContents)
        {
            AddHash(ref hash, label);
            HashFile(ref hash, path, includeContents);
        }

        static void HashLargeFileSampleWithLabel(ref uint hash, string label, string path)
        {
            AddHash(ref hash, label);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                AddHash(ref hash, "missing");
                return;
            }

            FileInfo info = new FileInfo(path);
            AddHash(ref hash, Path.GetFileName(path));
            AddHash(ref hash, info.Length.ToString());

            const int sampleSize = 4096;
            using (FileStream stream = File.OpenRead(path))
            {
                HashStreamSample(ref hash, stream, 0, sampleSize);
                if (stream.Length > sampleSize)
                {
                    HashStreamSample(ref hash, stream, System.Math.Max(0, stream.Length / 2 - sampleSize / 2), sampleSize);
                    HashStreamSample(ref hash, stream, System.Math.Max(0, stream.Length - sampleSize), sampleSize);
                }
            }
        }

        static void HashStreamSample(ref uint hash, FileStream stream, long offset, int maxBytes)
        {
            AddHash(ref hash, "sample@" + offset);
            stream.Seek(offset, SeekOrigin.Begin);
            byte[] buffer = new byte[1024];
            int remaining = maxBytes;
            while (remaining > 0)
            {
                int read = stream.Read(buffer, 0, System.Math.Min(buffer.Length, remaining));
                if (read <= 0) { break; }
                for (int i = 0; i < read; i++)
                {
                    hash ^= buffer[i];
                    hash *= 16777619u;
                }
                remaining -= read;
            }
        }

        static void HashFile(ref uint hash, string path, bool includeContents)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                AddHash(ref hash, "missing");
                return;
            }

            FileInfo info = new FileInfo(path);
            AddHash(ref hash, Path.GetFileName(path));
            AddHash(ref hash, info.Length.ToString());
            if (!includeContents) { return; }

            byte[] buffer = new byte[8192];
            using (FileStream stream = File.OpenRead(path))
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0) { break; }
                    for (int i = 0; i < read; i++)
                    {
                        hash ^= buffer[i];
                        hash *= 16777619u;
                    }
                }
            }
        }

        static void AddHash(ref uint hash, string value)
        {
            if (value == null) { value = ""; }
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            hash ^= 0xff;
            hash *= 16777619u;
        }

        void PrepareNetBattle(MatchFoundMsg found)
        {
            MugenMatchSetup.Clear();
            MugenMatchSetup.Mode = MugenMatchMode.NetKcp;
            MugenMatchSetup.NetRoomId = found.RoomId;
            MugenMatchSetup.NetPlayerId = found.LocalPlayerId;
            MugenMatchSetup.NetIsHost = found.LocalPlayerId == 0;
            MugenMatchSetup.AiSeed = found.Seed;
            MugenMatchSetup.NetOpponentName = found.OpponentName ?? "";
            MugenMatchSetup.FillFromCsv(MugenMatchSetup.Team0, found.Team0Csv);
            MugenMatchSetup.FillFromCsv(MugenMatchSetup.Team1, found.Team1Csv);

            _matching = false;
            _matchSucceeded = true;
            _matchSuccessColorT = 0f;
        }

        void BeginLoading(bool online)
        {
            if (_loading)
            {
                return;
            }
            MugenAutoTest.SendTrace(_netClient, "load_begin", "online=" + online, MugenMatchSetup.NetRoomId);
            MugenMatchSetup.ReturnToSelect = null;
            _onlineLoading = online;
            _loading = true;
            _localLoadProgress = 0f;
            _remoteLoadProgress = online ? 0f : 1f;
            _remoteLoadReady = !online;
            PrepareBattlePreloadList();
            _loadCountdown = -1f;
            _loadProgressSendTimer = 0f;
            if (_loadingPromptObject != null)
            {
                _loadingPromptObject.SetActive(true);
            }
            if (_loadingFill != null)
            {
                _loadingFill.fillAmount = 0f;
            }
            SetText(_loadingTmp, _loadingText, "内容加载中：0%");

            _loadOperation = SceneManager.LoadSceneAsync(MugenMatchSetup.BattleSceneName);
            if (_loadOperation != null)
            {
                _loadOperation.allowSceneActivation = false;
            }
            if (online)
            {
                SendLoadProgress(force: true);
            }
        }

        void UpdateLoading()
        {
            if (!_loading)
            {
                return;
            }

            if (_loadOperation != null)
            {
                _localLoadProgress = Mathf.Clamp01(_loadOperation.progress / 0.9f);
                if (_loadOperation.progress >= 0.9f)
                {
                    _localLoadProgress = 1f;
                }
            }
            else
            {
                _localLoadProgress = 1f;
            }

            if (_onlineLoading)
            {
                SendLoadProgressIfNeeded();
            }

            UpdateBattlePreload();
            float remote = _onlineLoading ? _remoteLoadProgress : 1f;
            bool remoteReady = !_onlineLoading || _remoteLoadReady;
            float localReadyProgress = CurrentLocalBattleLoadProgress();
            float combined = Mathf.Clamp01(Mathf.Min(localReadyProgress, remote));
            if (_loadingFill != null)
            {
                _loadingFill.fillAmount = combined;
            }

            if (localReadyProgress < 1f || !remoteReady)
            {
                int percent = Mathf.RoundToInt(combined * 100f);
                SetText(_loadingTmp, _loadingText, "内容加载中：" + percent + "%");
                return;
            }

            if (_loadCountdown < 0f)
            {
                _loadCountdown = 3f;
                MugenAutoTest.SendTrace(_netClient, "load_ready", "local=1 remote=1", MugenMatchSetup.NetRoomId);
                SendLoadProgress(force: true);
            }

            int seconds = Mathf.CeilToInt(Mathf.Max(0.01f, _loadCountdown));
            SetText(_loadingTmp, _loadingText, "加载完毕！" + seconds + "秒后进入对局");
            _loadCountdown -= Time.deltaTime;
            if (_loadCountdown <= 0f)
            {
                ActivateLoadedScene();
            }
        }

        void SendLoadProgressIfNeeded()
        {
            _loadProgressSendTimer -= Time.deltaTime;
            if (_loadProgressSendTimer > 0f)
            {
                return;
            }
            _loadProgressSendTimer = 0.12f;
            SendLoadProgress(force: false);
        }

        void SendLoadProgress(bool force)
        {
            if (!_onlineLoading || _netClient == null)
            {
                return;
            }
            float localReadyProgress = CurrentLocalBattleLoadProgress();
            int permille = Mathf.Clamp(Mathf.RoundToInt(localReadyProgress * 1000f), 0, 1000);
            bool ready = localReadyProgress >= 1f;
            if (!force && permille <= 0 && !ready)
            {
                return;
            }
            _netClient.Send(-1, new LoadProgressMsg
            {
                RoomId = MugenMatchSetup.NetRoomId,
                PlayerId = MugenMatchSetup.NetPlayerId,
                ProgressPermille = permille,
                Ready = ready,
            });
            _netClient.Flush();
        }

        void PrepareBattlePreloadList()
        {
            _preloadFolders.Clear();
            AddPreloadFolders(MugenMatchSetup.Team0);
            AddPreloadFolders(MugenMatchSetup.Team1);
            _preloadIndex = 0;
            _preloadComplete = _preloadFolders.Count == 0;
            _preloadProgress = _preloadComplete ? 1f : 0f;
        }

        void AddPreloadFolders(List<string> folders)
        {
            for (int i = 0; i < folders.Count; i++)
            {
                string folder = folders[i];
                if (!string.IsNullOrWhiteSpace(folder) && !_preloadFolders.Contains(folder))
                {
                    _preloadFolders.Add(folder);
                }
            }
        }

        void UpdateBattlePreload()
        {
            if (_preloadComplete)
            {
                return;
            }

            if (_preloadIndex < _preloadFolders.Count)
            {
                string folder = _preloadFolders[_preloadIndex];
                MugenBattlePreloadedBundle ignored;
                MugenBattlePreloadCache.TryGetOrLoad(folder, _mugenRoot, MugenMatchSetup.CommonFolder, 50f, out ignored);
                _preloadIndex++;
            }

            _preloadProgress = _preloadFolders.Count == 0
                ? 1f
                : Mathf.Clamp01((float)_preloadIndex / _preloadFolders.Count);
            _preloadComplete = _preloadIndex >= _preloadFolders.Count;
        }

        float CurrentLocalBattleLoadProgress()
        {
            return Mathf.Clamp01(Mathf.Min(_localLoadProgress, _preloadProgress));
        }

        void ActivateLoadedScene()
        {
            MugenAutoTest.SendTrace(_netClient, "activate_battle_scene", "", MugenMatchSetup.NetRoomId);
            if (_onlineLoading && _netClient != null)
            {
                MugenMatchSetup.NetTransport = _netClient;
                _netClient = null;
            }

            if (_loadOperation != null)
            {
                _loadOperation.allowSceneActivation = true;
            }
            else
            {
                SceneManager.LoadScene(MugenMatchSetup.BattleSceneName);
            }
        }

        void RestoreSelect()
        {
            MugenMatchSetup.ReturnToSelect = null;
            if (_canvas != null) { _canvas.gameObject.SetActive(true); }
            enabled = true;
            Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                int p0 = _team0.IndexOf(i);
                int p1 = _team1.IndexOf(i);
                if (e.OutlineP1 != null)
                {
                    e.OutlineP1.enabled = i == _cursor0;
                }
                if (e.OutlineP2 != null)
                {
                    e.OutlineP2.enabled = i == _cursor1;
                }

                if (_usingSceneUi)
                {
                    if (e.Cell != null)
                    {
                        e.Cell.color = p0 >= 0 ? PlayerSelectedSlotColor : SlotDefaultColor;
                    }
                    if (e.PortraitImage != null)
                    {
                        e.PortraitImage.sprite = e.Portrait;
                        e.PortraitImage.color = e.Portrait != null ? Color.white : new Color(0.24f, 0.24f, 0.24f, 1f);
                    }
                    bool picked = p0 >= 0 || p1 >= 0;
                    if (e.PickHint != null)
                    {
                        e.PickHint.SetActive(picked);
                    }
                    string hint = p0 >= 0 && p1 >= 0 ? "1P/2P" : (p0 >= 0 ? "1P" : "2P");
                    SetText(e.PickHintTmp, e.PickHintText, picked ? hint : "");
                }
                else
                {
                    string lbl = "";
                    if (p0 >= 0) { lbl += "我方" + (p0 + 1) + " "; }
                    if (p1 >= 0) { lbl += "对手" + (p1 + 1); }
                    if (e.PickLabel != null)
                    {
                        e.PickLabel.text = string.IsNullOrEmpty(lbl)
                            ? e.DisplayName
                            : e.DisplayName + "\n" + lbl;
                    }
                    if (e.Cell != null)
                    {
                        e.Cell.color = e.Portrait != null
                            ? (p0 >= 0 || p1 >= 0 ? new Color(1f, 1f, 1f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f))
                            : new Color(0.3f, 0.3f, 0.3f, 1f);
                    }
                }
            }
            string modeName = GetModeLabel(_mode);
            string action = _mode == MugenMatchMode.NetKcp
                ? (_matching ? "匹配中" : "寻找对手")
                : "开始";
            if (_modeButtonLabel != null)
            {
                _modeButtonLabel.text = modeName;
            }
            if (_startButtonLabel != null)
            {
                _startButtonLabel.text = _mode == MugenMatchMode.NetKcp
                    ? (_matching ? "匹配中" : "寻找对手")
                    : "开始战斗";
            }
            if (_cancelButton != null)
            {
                _cancelButton.gameObject.SetActive(_mode == MugenMatchMode.NetKcp && _matching);
            }
            if (_serverLabel != null)
            {
                _serverLabel.text = _mode == MugenMatchMode.NetKcp
                    ? MugenMatchSetup.FormatSelectedServerStatus()
                    : string.Format("资源 {0}", ShortPath(_mugenRoot));
            }
            RefreshSceneUi();
            if (_status != null)
            {
                if (_entries.Count == 0)
                {
                    _status.text = string.Format("没有可加载角色。请把 MugenSource 放到：{0}", ShortPath(MugenAssetPaths.MugenRootCandidates()[0]));
                    return;
                }

                _status.text = string.Format(
                    "大厅 {0}    我方 {1}/{2}    对手 {3}/{2}    回车={4}    {5}",
                    modeName, _team0.Count, TeamSize, _team1.Count, action, BuildCompatibilityStatus());
            }
        }

        void RefreshSceneUi()
        {
            if (!_usingSceneUi)
            {
                return;
            }

            bool online = _mode == MugenMatchMode.NetKcp;
            if (_matchButton != null)
            {
                _matchButton.gameObject.SetActive(online);
                _matchButton.interactable = !_loading && !_matchSucceeded && (_matching || _team0.Count >= TeamSize);
            }
            if (_randomButton != null)
            {
                _randomButton.gameObject.SetActive(!online && !_loading);
            }
            if (_startButton != null)
            {
                _startButton.gameObject.SetActive(!online && !_loading);
                _startButton.interactable = _team0.Count >= TeamSize;
            }
            if (_loadingPromptObject != null)
            {
                _loadingPromptObject.SetActive(_loading);
            }
            if (_matchStateObject != null)
            {
                _matchStateObject.SetActive(_matching || _matchSucceeded || _loading);
            }
            if (_matchStateButton != null)
            {
                _matchStateButton.interactable = _matching && !_loading;
            }

            SetText(_matchButtonTmp, _matchButtonText, "匹配对手");
            SetText(_randomButtonTmp, _randomButtonText, "随机填充");
            SetText(_sceneStartButtonTmp, _sceneStartButtonText, "开始游戏！");
            SetText(_sceneModeButtonTmp, _sceneModeButtonText, GetSceneModeLabel(_mode));
            string stateText = _matchSucceeded || _loading ? "匹配成功" : (_matchHover && _matching ? "取消" : "匹配中");
            SetText(_matchStateTmp, _matchStateText, stateText);
            if (_matchStateImage != null && _matching && !_matchSucceeded && !_loading)
            {
                _matchStateImage.color = MatchSuccessPrimaryColor;
            }

            string characterDesc = "角色选择";
            if (_team0.Count > 0)
            {
                int selected = _team0[_team0.Count - 1];
                if (selected >= 0 && selected < _entries.Count)
                {
                    characterDesc += " 当前角色：" + _entries[selected].DisplayName;
                }
            }
            SetText(_characterDescTmp, _characterDescText, characterDesc);
            SetText(_serverLabelTmp, _serverLabelText, MugenMatchSetup.FormatSelectedServerStatus());
        }

        void OnMatchButtonClicked()
        {
            Debug.Log("[匹配客户端] 点击【匹配对手】按钮");
            if (_loading)
            {
                Debug.Log("[匹配客户端] 忽略点击：正在加载场景");
                return;
            }
            if (_matching || _matchSucceeded)
            {
                Debug.Log(string.Format("[匹配客户端] 忽略点击：matching={0} matchSucceeded={1}", _matching, _matchSucceeded));
                return;
            }
            SetMode(MugenMatchMode.NetKcp);
            StartMatchmaking();
        }

        void OnMatchStateButtonClicked()
        {
            if (_matching && !_loading)
            {
                CancelMatch();
            }
        }

        void SetMatchHover(bool hover)
        {
            if (_matchHover == hover)
            {
                return;
            }
            _matchHover = hover;
            Refresh();
        }

        void UpdateMatchVisuals(float dt)
        {
            if (!_usingSceneUi)
            {
                return;
            }

            if (_matchSucceeded || _loading)
            {
                _matchSuccessColorT = Mathf.Clamp01(_matchSuccessColorT + dt * 2.5f);
                if (_matchStateImage != null)
                {
                    _matchStateImage.color = Color.Lerp(MatchSuccessPrimaryColor, MatchSuccessSecondaryColor, _matchSuccessColorT);
                }
                return;
            }

            if (_matching)
            {
                if (_matchStateImage != null)
                {
                    _matchStateImage.color = MatchSuccessPrimaryColor;
                }
                return;
            }

            _matchSuccessColorT = 0f;
            if (_matchStateImage != null)
            {
                _matchStateImage.color = MatchSuccessSecondaryColor;
            }
        }

        void StartLatencyProbe()
        {
            MugenMatchSetup.ApplySelectedServer();
            MugenMatchSetup.ServerEndpoint server = MugenMatchSetup.SelectedServer;
            if (server == null)
            {
                return;
            }

            _latencyServerIndex = MugenMatchSetup.SelectedServerIndex;
            if (_latencyProbe == null)
            {
                _latencyProbe = new MugenServerLatencyProbe(latencyMs =>
                {
                    MugenMatchSetup.SetServerLatencyMs(_latencyServerIndex, latencyMs);
                    RefreshSceneUi();
                });
            }
            _latencyProbe.Start(server.Host, server.Port);
            RefreshSceneUi();
        }

        static void SetButtonBaseColor(Button button, Image image, Color color)
        {
            if (image != null)
            {
                image.color = color;
            }
            if (button == null)
            {
                return;
            }
            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.22f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.24f);
            colors.selectedColor = Color.Lerp(color, Color.white, 0.16f);
            button.colors = colors;
        }

        string BuildCompatibilityStatus()
        {
            CompatibilitySummary p0 = SumCompatibility(_team0);
            CompatibilitySummary p1 = SumCompatibility(_team1);
            int unknown = p0.UnknownControllers + p1.UnknownControllers;
            int parsedOnly = p0.ParsedOnlyControllers + p1.ParsedOnlyControllers;
            int failures = (p0.LoadFailed ? 1 : 0) + (p1.LoadFailed ? 1 : 0);

            if (unknown == 0 && parsedOnly == 0 && failures == 0)
            {
                return "兼容诊断通过";
            }

            string text = string.Format("兼容诊断 未知控制器 {0}，parsed-only {1}", unknown, parsedOnly);
            if (failures > 0)
            {
                text += string.Format("，诊断失败队伍 {0}", failures);
            }
            return text;
        }

        CompatibilitySummary SumCompatibility(List<int> team)
        {
            CompatibilitySummary total = new CompatibilitySummary();
            for (int i = 0; i < team.Count; i++)
            {
                int entryIndex = team[i];
                if (entryIndex < 0 || entryIndex >= _entries.Count)
                {
                    continue;
                }

                CompatibilitySummary summary = GetCompatibilitySummary(_entries[entryIndex].Folder);
                total.UnknownControllers += summary.UnknownControllers;
                total.ParsedOnlyControllers += summary.ParsedOnlyControllers;
                total.LoadFailed |= summary.LoadFailed;
            }
            return total;
        }

        CompatibilitySummary GetCompatibilitySummary(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return new CompatibilitySummary { LoadFailed = true };
            }

            if (_compatibilityCache.TryGetValue(folder, out CompatibilitySummary cached))
            {
                return cached;
            }

            CompatibilitySummary summary = new CompatibilitySummary();
            try
            {
                string directory = Path.Combine(_mugenRoot, folder);
                string commonPath = Path.Combine(_mugenRoot, MugenMatchSetup.CommonFolder, "common1.cns");
                if (!MugenCharacterPackageLoader.TryLoad(directory, out MugenCharacterPackage package) ||
                    !MugenCharacterPackageLoader.IsBattleLoadable(package))
                {
                    summary.LoadFailed = true;
                }
                else
                {
                    MCharData data = package.LoadData(commonPath);
                    summary.UnknownControllers = CountValues(data.Compatibility.UnknownControllers);
                    summary.ParsedOnlyControllers = CountValues(data.Compatibility.ParsedOnlyControllers);
                }
            }
            catch
            {
                summary.LoadFailed = true;
            }

            _compatibilityCache[folder] = summary;
            return summary;
        }

        static int CountValues(Dictionary<string, int> values)
        {
            int total = 0;
            foreach (KeyValuePair<string, int> kv in values)
            {
                total += kv.Value;
            }
            return total;
        }

        static string ShortPath(string path)
        {
            if (string.IsNullOrEmpty(path)) { return ""; }
            return path.Length <= 82 ? path : "..." + path.Substring(path.Length - 79);
        }

        static string CurrentServerText()
        {
            MugenMatchSetup.ServerEndpoint server = MugenMatchSetup.SelectedServer;
            if (server != null)
            {
                return MugenMatchSetup.FormatSelectedServerStatus();
            }
            return "当前服务器：未知的服务器 延迟：测算中";
        }

        static string GetModeLabel(MugenMatchMode mode)
        {
            switch (mode)
            {
                case MugenMatchMode.LocalVersus: return "本地双人";
                case MugenMatchMode.NetKcp: return "在线匹配";
                default: return "人机对战";
            }
        }

        static string GetSceneModeLabel(MugenMatchMode mode)
        {
            switch (mode)
            {
                case MugenMatchMode.NetKcp: return "联机对战";
                case MugenMatchMode.LocalVersus: return "本地双人";
                default: return "单机对战";
            }
        }

        static Color ColorFromHtml(string hex)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString("#" + hex, out color))
            {
                return color;
            }
            return Color.white;
        }

        static Button FindSceneButton(string name, bool requireActive)
        {
            Transform target = FindSceneTransform(name, requireActive);
            return target != null ? target.GetComponent<Button>() : null;
        }

        static Transform FindSceneTransform(string name, bool requireActive)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform target = transforms[i];
                if (target == null || target.name != name)
                {
                    continue;
                }
                if (!target.gameObject.scene.IsValid())
                {
                    continue;
                }
                if (requireActive && !target.gameObject.activeInHierarchy)
                {
                    continue;
                }
                return target;
            }
            return null;
        }

        static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }
            if (root.name == name)
            {
                return root;
            }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        static void BindButtonText(Button button, string childName, out TMP_Text tmp, out Text text)
        {
            Transform root = button != null ? button.transform : null;
            BindText(root, childName, out tmp, out text);
            if (tmp == null && text == null && button != null)
            {
                tmp = button.GetComponentInChildren<TMP_Text>(true);
                text = button.GetComponentInChildren<Text>(true);
            }
        }

        static void BindText(Transform root, string name, out TMP_Text tmp, out Text text)
        {
            Transform target = root != null ? FindChildRecursive(root, name) : FindSceneTransform(name, requireActive: false);
            tmp = target != null ? target.GetComponent<TMP_Text>() : null;
            text = target != null ? target.GetComponent<Text>() : null;
            if (tmp == null && target != null)
            {
                tmp = target.GetComponentInChildren<TMP_Text>(true);
            }
            if (text == null && target != null)
            {
                text = target.GetComponentInChildren<Text>(true);
            }
        }

        static void SetText(TMP_Text tmp, Text text, string value)
        {
            if (tmp != null)
            {
                tmp.text = value;
            }
            if (text != null)
            {
                text.text = value;
            }
        }

        sealed class MatchHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public MugenSelectScreen Owner;

            public void OnPointerEnter(PointerEventData eventData)
            {
                if (Owner != null)
                {
                    Owner.SetMatchHover(true);
                }
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                if (Owner != null)
                {
                    Owner.SetMatchHover(false);
                }
            }
        }

        void OnDestroy()
        {
            if (_latencyProbe != null)
            {
                _latencyProbe.Dispose();
                _latencyProbe = null;
            }
            if (_netClient != null)
            {
                CloseMatchClient(notifyServer: _matching);
            }
        }
    }
}
