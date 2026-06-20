using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using GameData = Lockstep.Game.Data;
using Lockstep.Import.Air;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Battle.AI;
using Lockstep.Mugen.Battle.Net;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;
using Lockstep.Network;
using Lockstep.Client;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Lockstep.View
{
    public static class MugenPresentationClock
    {
        public const float IkemenTicksPerSecond = 60f;

        public static float EffectiveTickRate(float configuredTicksPerSecond, bool strictSixtyHz)
        {
            return strictSixtyHz ? IkemenTicksPerSecond : Mathf.Max(1f, configuredTicksPerSecond);
        }

        public static int ConsumeLogicFrames(
            float deltaTime,
            float ticksPerSecond,
            int maxFramesPerRender,
            bool dropCatchUpOnSpike,
            ref float accumulator,
            out int droppedFrames)
        {
            droppedFrames = 0;
            if (float.IsNaN(accumulator) || accumulator < 0f)
            {
                accumulator = 0f;
            }
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
            {
                deltaTime = 0f;
            }

            accumulator += Mathf.Max(0f, deltaTime) * Mathf.Max(1f, ticksPerSecond);
            if (accumulator < 1f)
            {
                return 0;
            }

            int dueFrames = Mathf.FloorToInt(accumulator);
            int framesToRun = Mathf.Min(dueFrames, Mathf.Max(1, maxFramesPerRender));
            accumulator -= framesToRun;

            if (dropCatchUpOnSpike && dueFrames > framesToRun)
            {
                droppedFrames = dueFrames - framesToRun;
                accumulator = Mathf.Max(0f, accumulator - droppedFrames);
            }

            return framesToRun;
        }

        public static bool SelfTest()
        {
            float accumulator = 0f;
            int dropped;
            int frames = ConsumeLogicFrames(0.1f, IkemenTicksPerSecond, 1, true, ref accumulator, out dropped);
            if (frames != 1 || dropped != 5 || accumulator >= 0.001f)
            {
                return false;
            }

            frames = ConsumeLogicFrames(1f / IkemenTicksPerSecond, IkemenTicksPerSecond, 1, true, ref accumulator, out dropped);
            if (frames != 1 || dropped != 0 || accumulator >= 0.001f)
            {
                return false;
            }

            accumulator = 0f;
            frames = ConsumeLogicFrames(0.1f, IkemenTicksPerSecond, 8, false, ref accumulator, out dropped);
            if (frames != 6 || dropped != 0 || accumulator >= 0.001f)
            {
                return false;
            }

            accumulator = 0.95f;
            frames = ConsumeLogicFrames(0.001f, IkemenTicksPerSecond, 1, true, ref accumulator, out dropped);
            return frames == 1 && dropped == 0 && accumulator >= 0f && accumulator < 0.02f;
        }
    }

    /// <summary>
    /// Unity view for the MUGEN turns battle demo: 1v1 on screen, N-character rosters,
    /// local, AI, and NetKcp input sources.
    /// </summary>
    public sealed class MugenTeamBattleView : MonoBehaviour
    {
        public float PixelsPerUnit = 50f;
        public float TicksPerSecond = 60f;
        public bool StrictSixtyHzLogic = true;
        public int MaxLogicFramesPerRender = 1;
        public bool DropLogicCatchUpOnSpike = true;
        public bool LogLogicTimingDiagnostics = false;
        public int StartSeparation = 140;
        public int StageHalfWidth = 320;
        public float CameraOrthoSize = 6.5f;
        public bool LogFrames = false;
        public bool ForceMobileControls = false;
        public bool AlignGroundToSceneUi = true;
        public string GroundUiName = "Ground";
        public float GroundViewportY = 0.3f;
        public int FighterSortingOrderBase = 500;
        public int SceneCanvasSortingOrder = 0;
        public int ReturnMenuSortingOrder = 5000;
        public float SceneCanvasPlaneDistance = 100f;
        public float LatencyProbeInterval = 1f;
        public int[] WeakNetworkDelayStepsMs =
        {
            10, 20, 30, 40, 50, 60, 70, 80, 90, 100,
            110, 120, 130, 140, 150, 160, 170, 180, 190, 200,
            210, 220, 230, 240, 250, 260, 270, 280, 290, 300,
        };
        public float FeelAssistMoveOffset = 0.28f;
        public float FeelAssistVerticalOffset = 0.08f;
        public float FeelAssistResponse = 18f;
        public float FeelAssistCorrection = 8f;
        public float FeelAssistAttackScale = 0.08f;
        public bool OnlinePredictionDefault = false;
        public bool OnlineRollbackPredictionDefault = true;
        public int OnlineMaxPredictFrames = 12;
        public int OnlineMaxUnpredictedInputLead = 36;
        public int OnlineMaxSimulatedFramesPerLogicFrame = 1;
        public int OnlineLatencyBudgetCapMs = 1000;
        public int OnlineLatencyBudgetSafetyFrames = 8;
        public int OnlineRollbackPredictionMinBudgetMs = 80;
        public int WeakNetworkSuppressFeelAssistMs = 80;
        public bool AutoRunLog = true;
        public bool SaveRunLogFiles = true;
        public bool SaveNetDiagnosticLogFiles = true;
        public int NetHealthLogIntervalFrames = 120;
        public int LongPauseWarnFrames = 90;

        sealed class Bundle
        {
            public MCharData Data;
            public MugenSpriteLoader.Source Source;
            public string SoundPath;
            public readonly Dictionary<int, GameData.AnimData> GameAnims = new Dictionary<int, GameData.AnimData>();
            public readonly HashSet<int> Built = new HashSet<int>();
        }

        readonly Dictionary<string, Bundle> _bundles = new Dictionary<string, Bundle>();
        readonly Dictionary<MCharData, Bundle> _byData = new Dictionary<MCharData, Bundle>();
        readonly SpriteRenderer[] _renderers = new SpriteRenderer[2];
        SpriteRenderer _stageGroundRenderer;
        readonly Vector3[] _feelOffsets = new Vector3[2];
        readonly List<MInput> _inputs = new List<MInput> { MInput.None, MInput.None };
        readonly List<int> _pendingHashReports = new List<int>();
        readonly HashSet<int> _reportedHashFrames = new HashSet<int>();
        readonly List<MInput> _weakLocalInputDelayBuffer = new List<MInput>();

        MTeamMatch _match;
        MSimpleAI _ai;
        MugenMatchMode _mode;
        MugenBattleHud _hud;
        MugenAudioManager _audio;

        MugenLockstepSession _session;
        MugenRoutedNetChannel _netChannel;
        KcpClientTransport _kcp;
        KcpClientTransport _latencyProbe;
        bool _netStarted;
        string _netClosedReason = "";
        int _localPlayerId;
        MBattleRunLogRecorder _runLog;
        Button _weakNetworkButton;
        Button _feelButton;
        Text _weakNetworkText;
        Text _feelText;
        TMP_Text _weakNetworkTmp;
        TMP_Text _feelTmp;
        int _weakNetworkStepIndex = -1;
        bool _feelOptimizationEnabled;
        bool _rollbackPredictionEnabled;
        int _lastSessionLatencyBudgetMs;
        MInput _lastFeelInput;
        float _feelAttackPulse;
        float _latencyProbeTimer;
        int _latencyProbeSeq;
        int _latencyProbeMs = -1;
        readonly Dictionary<int, int> _latencyProbeSent = new Dictionary<int, int>();
        float _netPingTimer;
        int _netPingSeq;
        int _netPingMs = -1;
        readonly Dictionary<int, int> _netPingSent = new Dictionary<int, int>();
        int _remoteWeakDelayMs;
        int _remoteLatencyMs = -1;
        float _lastRemoteNetStatusRealtime = -1f;
        int _lastNetStatusFrame = -999999;
        int _lastBoutStartedFrame = -1;
        int _lastBoutStartedNo;
        bool _nextBoutStallHandled;

        float _accumulator;
        int _frame;
        int _logicCatchUpDropTotal;
        int _lastLogicTimingDiagnosticFrame = -999999;
        int _lastScreenWidth;
        int _lastScreenHeight;
        string _mugenRoot;
        bool _returningToMainMenu;
        bool _matchOverUiShown;
        bool _remoteForfeit;
        int _lastNetHealthLogFrame = -999999;
        int _lastNetStartWaitLogFrame = -999999;
        int _pauseWarnStartFrame = -1;
        bool _pauseWarnLogged;
        string _netDiagnosticLogPath = "";
        string _returnMenuMessageOverride;
        GameObject _returnMenuRoot;
        Button _returnMenuButton;
        Text _returnMenuText;
        TMP_Text _returnMenuTmp;
        bool _returnMenuDismissible;
        static bool _sceneHooked;
        static Sprite _solidStageSprite;
        const string ReturnMenuOverlayCanvasName = "ReturnMenuOverlayCanvas";
        const string StageGroundObjectName = "MugenStageGround";
        const int StageGroundSortingOrder = -10;
        const int LogicTimingDiagnosticIntervalFrames = 60;
        const float NetPingInterval = 1f;
        const int NetStatusBroadcastIntervalFrames = 60;
        const int NextBoutEnterFightTimeoutFrames = 600;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallBattleSceneBootstrap()
        {
            if (!_sceneHooked)
            {
                SceneManager.sceneLoaded += (scene, mode) => EnsureBattleSceneRunner(scene);
                _sceneHooked = true;
            }
            EnsureBattleSceneRunner(SceneManager.GetActiveScene());
        }

        static void EnsureBattleSceneRunner(Scene scene)
        {
            if (scene.name != MugenMatchSetup.BattleSceneName)
            {
                return;
            }
            if (UnityEngine.Object.FindObjectOfType<MugenTeamBattleView>() != null)
            {
                return;
            }
            GameObject battle = new GameObject("TeamBattle");
            battle.AddComponent<MugenTeamBattleView>();
        }

        void Start()
        {
            MugenMatchSetup.EnsureDefaults();
            _mode = MugenMatchSetup.Mode;
            _mugenRoot = MugenAssetPaths.MugenRoot();
            _audio = MugenAudioManager.Ensure();
            BindReturnToMainMenuUi();

            List<MCharData> team0 = BuildRoster(MugenMatchSetup.Team0);
            List<MCharData> team1 = BuildRoster(MugenMatchSetup.Team1);
            if (team0.Count == 0 || team1.Count == 0)
            {
                Debug.LogError("[MUGEN] invalid team setup: both teams need at least one loadable character.");
                return;
            }

            _match = new MTeamMatch(team0, team1, ConfigureRound, StartSeparation, StageHalfWidth);
            _match.OnBoutResolved += winner =>
            {
                Debug.Log(string.Format(
                    "[Team] bout {0} resolved winner={1} remain0={2} remain1={3}",
                    _match.BoutNo, winner, _match.Remaining(0), _match.Remaining(1)));
                MugenAutoTest.Trace("bout_resolved",
                    "winner=" + winner + " remain0=" + _match.Remaining(0) + " remain1=" + _match.Remaining(1),
                    MugenMatchSetup.NetRoomId,
                    _frame,
                    0);
            };

            if (_mode == MugenMatchMode.NetKcp)
            {
                _localPlayerId = ResolveLocalPlayerId();
            }
            SetupRunLog();
            _ai = new MSimpleAI(MugenMatchSetup.AiSeed);
            _hud = MugenBattleHud.Create(2);
            _match.OnBoutStarted += () =>
            {
                ResetFeelAssist();
                ClearWeakLocalInputDelay();
                _lastBoutStartedFrame = _frame;
                _lastBoutStartedNo = _match.BoutNo;
                _nextBoutStallHandled = false;
                _lastNetHealthLogFrame = -999999;
                WriteNetDiagnostic("bout_started", "bout=" + _match.BoutNo);
                RefreshHudNames();
                MugenAutoTest.Trace("bout_started", "bout=" + _match.BoutNo, MugenMatchSetup.NetRoomId, _frame, 0);
            };
            RefreshHudNames();
            ConfigureSceneCanvasForWorldCharacters();

            for (int i = 0; i < 2; i++)
            {
                GameObject go = new GameObject("Fighter" + (i + 1));
                go.transform.SetParent(transform, false);
                _renderers[i] = go.AddComponent<SpriteRenderer>();
                _renderers[i].sortingOrder = FighterSortingOrderBase + i;
            }

            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                cam.orthographicSize = CameraOrthoSize;
            }
            AlignBattleGroundToSceneUi();

            if (_mode == MugenMatchMode.NetKcp)
            {
                SetupNet();
            }
            else
            {
                StartLatencyProbe();
            }
            SetupBattleSceneNetControls();
            bool sceneControls = MugenMobileInputUi.EnsureSceneControls();
            if (!sceneControls && (Application.isMobilePlatform || ForceMobileControls))
            {
                MugenMobileInputUi.Ensure();
            }

            Application.runInBackground = true;
            MugenAutoTest.Trace("scene_battle",
                "mode=" + _mode + " local=" + _localPlayerId + " team0=" + team0.Count + " team1=" + team1.Count,
                MugenMatchSetup.NetRoomId,
                _frame,
                0);
            Debug.Log(string.Format("[MUGEN] team battle started mode={0} team0={1} team1={2}",
                _mode, team0.Count, team1.Count));
            RenderAll();
        }

        void ConfigureRound(MRoundSystem round)
        {
            int seconds = MugenAutoTest.Enabled ? MugenAutoTest.RoundSeconds : 60;
            round.RoundTime = seconds * MRoundSystem.TicksPerSecond;
            round.Timer = round.RoundTime;
        }

        void ConfigureSceneCanvasForWorldCharacters()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            RectTransform ground = FindSceneRectTransform(GroundUiName);
            Canvas sourceCanvas = ground != null ? ground.GetComponentInParent<Canvas>(true) : null;
            if (ground == null || sourceCanvas == null)
            {
                return;
            }

            Graphic background = FindSceneBackgroundGraphic(sourceCanvas, ground);
            if (background != null)
            {
                Color backgroundColor = background.color;
                backgroundColor.a = 1f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = backgroundColor;
                HideUiGraphic(background);
            }

            HideUiGraphic(ground.GetComponent<Graphic>());
            RefreshWorldGroundFromSceneUi(cam);
        }

        void RefreshWorldGroundFromSceneUi(Camera cam)
        {
            RectTransform ground = FindSceneRectTransform(GroundUiName);
            if (cam == null || ground == null)
            {
                return;
            }

            SpriteRenderer renderer = EnsureStageRenderer(ref _stageGroundRenderer, StageGroundObjectName, StageGroundSortingOrder);
            Graphic groundGraphic = ground.GetComponent<Graphic>();
            renderer.color = groundGraphic != null
                ? groundGraphic.color
                : new Color(0.66f, 0.39f, 0.25f, 1f);

            if (TryRectToWorldBounds(cam, ground, transform.position.z, out Vector3 center, out Vector2 size))
            {
                renderer.transform.position = center;
                renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
                renderer.enabled = true;
            }
        }

        SpriteRenderer EnsureStageRenderer(ref SpriteRenderer renderer, string objectName, int sortingOrder)
        {
            if (renderer == null)
            {
                GameObject go = new GameObject(objectName);
                renderer = go.AddComponent<SpriteRenderer>();
            }
            else if (renderer.transform.parent != null)
            {
                renderer.transform.SetParent(null, true);
            }
            renderer.sprite = SolidStageSprite();
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        static Sprite SolidStageSprite()
        {
            if (_solidStageSprite == null)
            {
                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                texture.hideFlags = HideFlags.HideAndDontSave;
                _solidStageSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                _solidStageSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            return _solidStageSprite;
        }

        static bool TryRectToWorldBounds(Camera cam, RectTransform rect, float z, out Vector3 center, out Vector2 size)
        {
            center = Vector3.zero;
            size = Vector2.zero;
            if (Screen.width <= 0 || Screen.height <= 0)
            {
                return false;
            }

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float minX = corners[0].x;
            float maxX = corners[0].x;
            float minY = corners[0].y;
            float maxY = corners[0].y;
            for (int i = 1; i < corners.Length; i++)
            {
                minX = Mathf.Min(minX, corners[i].x);
                maxX = Mathf.Max(maxX, corners[i].x);
                minY = Mathf.Min(minY, corners[i].y);
                maxY = Mathf.Max(maxY, corners[i].y);
            }

            Vector2 minViewport = new Vector2(0f, Mathf.Clamp01(minY / Screen.height));
            Vector2 maxViewport = new Vector2(1f, Mathf.Clamp01(maxY / Screen.height));
            if (!TryViewportPointToWorldOnZPlane(cam, minViewport, z, out Vector3 bottomLeft) ||
                !TryViewportPointToWorldOnZPlane(cam, new Vector2(maxViewport.x, minViewport.y), z, out Vector3 bottomRight) ||
                !TryViewportPointToWorldOnZPlane(cam, new Vector2(minViewport.x, maxViewport.y), z, out Vector3 topLeft) ||
                !TryViewportPointToWorldOnZPlane(cam, maxViewport, z, out Vector3 topRight))
            {
                return false;
            }

            float minWorldX = Mathf.Min(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x);
            float maxWorldX = Mathf.Max(bottomLeft.x, bottomRight.x, topLeft.x, topRight.x);
            float minWorldY = Mathf.Min(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y);
            float maxWorldY = Mathf.Max(bottomLeft.y, bottomRight.y, topLeft.y, topRight.y);
            float worldWidth = maxWorldX - minWorldX;
            float worldHeight = maxWorldY - minWorldY;
            center = new Vector3((minWorldX + maxWorldX) * 0.5f, (minWorldY + maxWorldY) * 0.5f, z);
            size = new Vector2(Mathf.Max(0.01f, worldWidth), Mathf.Max(0.01f, worldHeight));
            return true;
        }

        static bool TryViewportPointToWorldOnZPlane(Camera cam, Vector2 viewport, float z, out Vector3 world)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, z));
            if (plane.Raycast(ray, out float enter))
            {
                world = ray.GetPoint(enter);
                return true;
            }
            world = Vector3.zero;
            return false;
        }

        static Graphic FindSceneBackgroundGraphic(Canvas canvas, RectTransform ground)
        {
            if (canvas == null)
            {
                return null;
            }
            Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                RectTransform rect = graphic != null ? graphic.transform as RectTransform : null;
                if (graphic == null || rect == null || rect == ground || !graphic.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (graphic.GetComponentInParent<Button>(true) != null)
                {
                    continue;
                }
                if (IsFullScreenStretch(rect))
                {
                    return graphic;
                }
            }
            return null;
        }

        static bool IsFullScreenStretch(RectTransform rect)
        {
            return rect != null &&
                Vector2.Distance(rect.anchorMin, Vector2.zero) < 0.001f &&
                Vector2.Distance(rect.anchorMax, Vector2.one) < 0.001f;
        }

        static void HideUiGraphic(Graphic graphic)
        {
            if (graphic == null)
            {
                return;
            }
            graphic.raycastTarget = false;
            graphic.enabled = false;
        }

        List<MCharData> BuildRoster(List<string> folders)
        {
            List<MCharData> roster = new List<MCharData>();
            for (int i = 0; i < folders.Count; i++)
            {
                Bundle bundle = LoadBundle(folders[i]);
                if (bundle != null)
                {
                    roster.Add(bundle.Data);
                }
            }
            return roster;
        }

        Bundle LoadBundle(string folder)
        {
            if (_bundles.TryGetValue(folder, out Bundle existing))
            {
                return existing;
            }

            if (MugenBattlePreloadCache.TryGet(folder, _mugenRoot, MugenMatchSetup.CommonFolder, PixelsPerUnit,
                    out MugenBattlePreloadedBundle preloaded))
            {
                Bundle cached = new Bundle
                {
                    Data = preloaded.Data,
                    Source = preloaded.Source,
                    SoundPath = preloaded.SoundPath,
                };
                foreach (KeyValuePair<int, GameData.AnimData> pair in preloaded.GameAnims)
                {
                    cached.GameAnims[pair.Key] = pair.Value;
                }
                foreach (int animId in preloaded.Built)
                {
                    cached.Built.Add(animId);
                }
                if (_audio != null)
                {
                    _audio.Register(cached.Data, cached.SoundPath);
                }
                _bundles[folder] = cached;
                _byData[cached.Data] = cached;
                return cached;
            }

            string charDir = Path.Combine(_mugenRoot, folder);
            if (!MugenCharacterPackageLoader.TryLoad(charDir, out MugenCharacterPackage package) ||
                !MugenCharacterPackageLoader.IsBattleLoadable(package))
            {
                Debug.LogError("[MUGEN] skipped unloadable character folder: " + folder);
                return null;
            }

            string commonPath = Path.Combine(_mugenRoot, MugenMatchSetup.CommonFolder, "common1.cns");
            MCharData data = package.LoadData(commonPath);
            Bundle bundle = new Bundle
            {
                Data = data,
                Source = MugenSpriteLoader.Open(package.SpritePath, PixelsPerUnit),
                SoundPath = package.SoundPath,
            };
            if (_audio != null)
            {
                _audio.Register(data, bundle.SoundPath);
            }

            List<GameData.AnimData> anims = AirParser.ParseFile(package.AnimPath);
            for (int i = 0; i < anims.Count; i++)
            {
                bundle.GameAnims[anims[i].Id] = anims[i];
            }

            _bundles[folder] = bundle;
            _byData[data] = bundle;
            return bundle;
        }

        void SetupNet()
        {
            _localPlayerId = ResolveLocalPlayerId();
            bool reusedTransport = MugenMatchSetup.NetTransport != null;
            _kcp = MugenMatchSetup.NetTransport ?? new KcpClientTransport();
            if (reusedTransport && (_kcp.Faulted || !_kcp.IsConnected))
            {
                Debug.LogWarning(string.Format("[联机诊断] 战斗场收到不可用连接 connected={0} fault={1}，尝试重新连接",
                    _kcp.IsConnected, _kcp.FaultReason));
                _kcp.Close();
                _kcp = new KcpClientTransport();
                _kcp.Connect(MugenMatchSetup.NetHost, MugenMatchSetup.NetPort);
            }
            else if (!reusedTransport)
            {
                _kcp.Connect(MugenMatchSetup.NetHost, MugenMatchSetup.NetPort);
            }

            MugenMatchSetup.NetTransport = null;
            _netChannel = new MugenRoutedNetChannel(_kcp, MugenAutoTest.PopulateInputTrace);
            _session = new MugenLockstepSession(
                _match.Tick,
                _match.ComputeHash,
                _netChannel,
                _localPlayerId,
                MugenMatchProtocol.PlayerCount,
                MugenMatchProtocol.DefaultInputLag,
                () => _match.Snapshot(),
                snapshot => _match.Restore((MTeamMatchSnapshot)snapshot),
                CanPredictLogicFrame);
            _feelOptimizationEnabled = OnlinePredictionDefault;
            _rollbackPredictionEnabled = OnlineRollbackPredictionDefault;
            _session.MaxPredictFrameCount = Mathf.Max(1, OnlineMaxPredictFrames);
            _session.MaxSimulatedFramesPerStep = Mathf.Max(1, OnlineMaxSimulatedFramesPerLogicFrame);
            ApplyNetworkSessionBudget(CurrentNetworkOneWayBudgetMs());
            EnforceWeakNetworkFeelPolicy("setup_net");
            _lastNetStartWaitLogFrame = -999999;

            _session.OnFrameSimulated += (frame, hash) =>
            {
                if (LogFrames)
                {
                    Debug.Log(string.Format("[Net] frame {0} hash {1:X}", frame, hash));
                }
            };
            _session.OnFrameConfirmed += QueueHashReport;
            _session.OnFrameInputs += OnNetFrameInputs;

            _kcp.Send(-1, new RoomReadyMsg
            {
                RoomId = MugenMatchSetup.NetRoomId,
                RoomSeed = MugenMatchSetup.AiSeed,
                LocalPlayerId = _localPlayerId,
                PlayerCount = MugenMatchProtocol.PlayerCount,
            });
            MugenAutoTest.SendTrace(_kcp, "battle_room_ready",
                "local=" + _localPlayerId,
                MugenMatchSetup.NetRoomId);
            _kcp.Flush();
            OpenNetDiagnosticLog();
            WriteNetDiagnostic("battle_room_ready", "local=" + _localPlayerId);
            SendNetPing(force: true);
            SendNetStatusIfNeeded(force: true, allowBeforeStart: true);
        }

        void QueueHashReport(int frame)
        {
            if (_reportedHashFrames.Contains(frame) || _pendingHashReports.Contains(frame))
            {
                return;
            }
            _pendingHashReports.Add(frame);
        }

        void FlushHashReports()
        {
            if (_mode != MugenMatchMode.NetKcp || _kcp == null || _session == null)
            {
                return;
            }

            for (int i = _pendingHashReports.Count - 1; i >= 0; i--)
            {
                int frame = _pendingHashReports[i];
                if (_reportedHashFrames.Contains(frame))
                {
                    _pendingHashReports.RemoveAt(i);
                    continue;
                }
                if (!_session.TryGetAuthoritativeFrameHash(frame, out ulong hash))
                {
                    continue;
                }

                SendHashReport(frame, hash);
                _reportedHashFrames.Add(frame);
                _pendingHashReports.RemoveAt(i);
            }
        }

        void SendHashReport(int frame, ulong hash)
        {
            if (_mode != MugenMatchMode.NetKcp || _kcp == null || _session == null)
            {
                return;
            }

            _kcp.Send(-1, new MugenHashReportMsg
            {
                RoomId = MugenMatchSetup.NetRoomId,
                Frame = frame,
                PlayerId = _localPlayerId,
                Hash = hash,
            });
        }

        int ResolveLocalPlayerId()
        {
            return MugenMatchSetup.NetTransport != null ? MugenMatchSetup.NetPlayerId : (MugenMatchSetup.NetIsHost ? 0 : 1);
        }

        void Update()
        {
            if (_match == null)
            {
                return;
            }
            if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            {
                AlignBattleGroundToSceneUi();
            }

            if (_kcp != null)
            {
                _kcp.Update();
                HandleTransportFault();
                PollNetControl();
            }
            float deltaTime = Time.deltaTime;
            UpdateNetPing(deltaTime);
            UpdateLatencyProbe(deltaTime);
            if (MugenAutoTest.Enabled && MugenAutoTest.ConsumeEscape(_frame))
            {
                HandleEscapeKey();
            }
            if (MugenAutoTest.Enabled && MugenAutoTest.ConsumeWeakNetworkToggle(_frame))
            {
                MugenAutoTest.Trace("weak_network_toggle", "scheduled", MugenMatchSetup.NetRoomId, _frame, 0);
                ToggleWeakNetworkSimulation();
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscapeKey();
                return;
            }
            if (_match.MatchOver || _remoteForfeit)
            {
                ShowMatchOverUi();
                if (_match.MatchOver)
                {
                    MugenAutoTest.MarkMatchOver(_match.MatchWinner, _match.BoutNo);
                }
            }
            MugenAutoTest.TickQuit();
            MugenAutoTest.TickFrameQuit(_frame);
            MugenAutoTest.FlushPendingTrace(_kcp);

            int droppedCatchUpFrames;
            int logicFrames = MugenPresentationClock.ConsumeLogicFrames(
                deltaTime,
                MugenPresentationClock.EffectiveTickRate(TicksPerSecond, StrictSixtyHzLogic),
                MaxLogicFramesPerRender,
                DropLogicCatchUpOnSpike,
                ref _accumulator,
                out droppedCatchUpFrames);
            for (int i = 0; i < logicFrames; i++)
            {
                StepOneLogicFrame();
                _frame++;
            }
            LogLogicTimingDropIfNeeded(droppedCatchUpFrames, deltaTime, logicFrames);
            SendNetStatusIfNeeded(force: false);
            CheckNextBoutStartTimeout();

            RenderAll();
            ApplyOnlineFeelAssist(deltaTime);
                if (MugenAutoTest.CaptureScreenshotIfNeeded(_frame, _returnMenuRoot != null && _returnMenuRoot.activeSelf ? "return-menu" : "battle"))
                {
                    WriteNetDiagnostic("visual_screenshot", CharDiag(0) + " | " + CharDiag(1));
                }
            if (_hud != null && _match.Engine != null)
            {
                _hud.UpdateHud(_match.Round, _match.Engine.Chars[0], _match.Engine.Chars[1],
                    deltaTime, _mode, CurrentLocalLatencyMs(), CurrentRemoteLatencyMs(), _match.BoutNo);
            }
            if (_kcp != null)
            {
                _kcp.Update();
                HandleTransportFault();
                PollNetControl();
            }
        }

        void LogLogicTimingDropIfNeeded(int droppedFrames, float deltaTime, int simulatedFrames)
        {
            if (droppedFrames <= 0)
            {
                return;
            }

            _logicCatchUpDropTotal += droppedFrames;
            if (!LogLogicTimingDiagnostics ||
                _frame - _lastLogicTimingDiagnosticFrame < LogicTimingDiagnosticIntervalFrames)
            {
                return;
            }

            _lastLogicTimingDiagnosticFrame = _frame;
            Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                "[MUGEN] dropped catch-up logic frames delta={0:0.0000}s simulated={1} dropped={2} totalDropped={3} tickRate={4:0.##} maxPerRender={5}",
                deltaTime,
                simulatedFrames,
                droppedFrames,
                _logicCatchUpDropTotal,
                MugenPresentationClock.EffectiveTickRate(TicksPerSecond, StrictSixtyHzLogic),
                Mathf.Max(1, MaxLogicFramesPerRender)));
        }

        void StepOneLogicFrame()
        {
            if (_remoteForfeit || _match.MatchOver)
            {
                return;
            }

            bool acceptsGameplayInput = AcceptsGameplayInput();
            MInput rawP0 = acceptsGameplayInput
                ? (MugenAutoTest.Enabled ? MugenAutoTest.SampleInput(_frame) : SampleLocalPlayerInput())
                : MInput.None;
            MInput p0 = acceptsGameplayInput ? ApplyWeakLocalInputDelay(rawP0) : MInput.None;
            if (!acceptsGameplayInput)
            {
                ClearWeakLocalInputDelay();
            }
            if (_mode == MugenMatchMode.NetKcp)
            {
                if (!_netStarted)
                {
                    LogNetStartWaitIfNeeded();
                    return;
                }
                _session.PredictionEnabled = CanPredictLogicFrame();
                _session.Step(p0);
                LogNetHealthIfNeeded();
                FlushHashReports();
                _kcp.Flush();
                PlayFrameAudio();
                return;
            }

            MInput p1 = !acceptsGameplayInput
                ? MInput.None
                : _mode == MugenMatchMode.VersusAI
                ? _ai.Decide(_match.Engine.Chars[1], _match.Engine.Chars[0], _frame)
                : SampleInput(KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D,
                    KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P);

            _inputs[0] = p0;
            _inputs[1] = p1;
            _match.Tick(_inputs);
            LogLongPauseIfNeeded(_frame, _inputs);
            CaptureRunLogFrame(_frame, _inputs);
            PlayFrameAudio();
        }

        bool AcceptsGameplayInput()
        {
            return _match != null &&
                   _match.Round != null &&
                   _match.Round.State == MRoundState.Fight &&
                   _match.Round.FinishType == MFinishType.NotYet;
        }

        void CheckNextBoutStartTimeout()
        {
            if (_mode != MugenMatchMode.NetKcp ||
                !_netStarted ||
                _remoteForfeit ||
                _match == null ||
                _match.MatchOver ||
                _match.Round == null ||
                _match.BoutNo <= 1 ||
                _lastBoutStartedNo != _match.BoutNo ||
                _lastBoutStartedFrame < 0 ||
                _nextBoutStallHandled)
            {
                return;
            }
            if (_match.Round.State == MRoundState.Fight || _match.Round.FinishType != MFinishType.NotYet)
            {
                return;
            }
            if (_frame - _lastBoutStartedFrame < NextBoutEnterFightTimeoutFrames)
            {
                return;
            }

            _nextBoutStallHandled = true;
            string detail = "bout=" + _match.BoutNo +
                " state=" + _match.Round.State +
                " weakDelayMs=" + CurrentWeakNetworkDelayMs() +
                " remoteWeakDelayMs=" + _remoteWeakDelayMs;
            WriteNetDiagnostic("next_bout_start_timeout", detail);
            MugenAutoTest.SendTrace(_kcp, "next_bout_start_timeout", detail, MugenMatchSetup.NetRoomId, _frame, 0);
            EndMatchByRemoteForfeit();
        }

        void PlayFrameAudio()
        {
            if (_audio != null && _match != null)
            {
                _audio.PlayFrameEvents(_match.Engine);
            }
        }

        void PollNetControl()
        {
            if (_netChannel == null)
            {
                return;
            }

            while (_netChannel != null && _netChannel.TryReceiveControl(out IMessage message))
            {
                if (message is ServerLogMsg serverLog)
                {
                    Debug.Log("[服务器广播] " + serverLog.Message);
                }
                else if (message is StartMatchMsg start && start.RoomId == MugenMatchSetup.NetRoomId)
                {
                    _netStarted = true;
                    _netClosedReason = "";
                    _lastNetStartWaitLogFrame = -999999;
                    WriteNetDiagnostic("start_match", "startFrame=" + start.StartFrame);
                    MugenAutoTest.SendTrace(_kcp, "start_match", "startFrame=" + start.StartFrame, start.RoomId, _frame, 0);
                    SendNetStatusIfNeeded(force: true);
                }
                else if (message is MugenNetStatusMsg status)
                {
                    HandleNetStatus(status);
                }
                else if (message is PongMsg pong)
                {
                    HandleNetPong(pong);
                }
                else if (message is RoomClosedMsg closed)
                {
                    _netStarted = false;
                    _netClosedReason = closed.Reason ?? "room closed";
                    WriteNetDiagnostic("room_closed", _netClosedReason);
                    MugenAutoTest.SendTrace(_kcp, "room_closed", _netClosedReason, closed.RoomId, _frame, 0);
                    MugenAutoTest.MarkMatchClosed(_netClosedReason, closed.RoomId, _frame);
                    if (IsOpponentLeaveReason(_netClosedReason) && _match != null && !_match.MatchOver)
                    {
                        EndMatchByRemoteForfeit();
                        break;
                    }
                }
            }
        }

        void HandleNetStatus(MugenNetStatusMsg status)
        {
            if (status.RoomId != MugenMatchSetup.NetRoomId || status.PlayerId == _localPlayerId)
            {
                return;
            }

            _remoteWeakDelayMs = Mathf.Max(0, status.WeakDelayMs);
            _remoteLatencyMs = NormalizeNetLatencyMs(status.LatencyMs, _remoteWeakDelayMs);
            _lastRemoteNetStatusRealtime = Time.realtimeSinceStartup;
            ApplyNetworkSessionBudget(CurrentNetworkOneWayBudgetMs());
            EnforceWeakNetworkFeelPolicy("remote_net_status");
            WriteNetDiagnostic("remote_net_status",
                "player=" + status.PlayerId +
                " weakDelayMs=" + _remoteWeakDelayMs +
                " latencyMs=" + _remoteLatencyMs);
        }

        void UpdateNetPing(float deltaTime)
        {
            if (_mode != MugenMatchMode.NetKcp || _kcp == null || _kcp.Faulted || MugenMatchSetup.NetRoomId == 0)
            {
                return;
            }

            _netPingTimer -= Mathf.Max(0f, deltaTime);
            if (_netPingTimer > 0f)
            {
                return;
            }

            SendNetPing(force: false);
        }

        void SendNetPing(bool force)
        {
            if (_mode != MugenMatchMode.NetKcp || _kcp == null || _kcp.Faulted || MugenMatchSetup.NetRoomId == 0)
            {
                return;
            }
            if (!force && _netPingTimer > 0f)
            {
                return;
            }

            int now = RealtimeMs();
            int seq = ++_netPingSeq;
            _netPingSent[seq] = now;
            _kcp.Send(-1, new PingMsg
            {
                Sequence = seq,
                ClientTimeMs = now,
            });
            _kcp.Flush();
            _netPingTimer = Mathf.Max(0.2f, NetPingInterval);
            PruneNetPing(now);
        }

        void HandleNetPong(PongMsg pong)
        {
            int now = RealtimeMs();
            if (!_netPingSent.TryGetValue(pong.Sequence, out int sentMs))
            {
                return;
            }

            _netPingSent.Remove(pong.Sequence);
            _netPingMs = Mathf.Max(0, now - sentMs);
            ApplyNetworkSessionBudget(CurrentNetworkOneWayBudgetMs());
            WriteNetDiagnostic("net_ping", "rttMs=" + _netPingMs);
            SendNetStatusIfNeeded(force: true, allowBeforeStart: true);
        }

        void PruneNetPing(int nowMs)
        {
            if (_netPingSent.Count <= 8)
            {
                return;
            }

            List<int> stale = null;
            foreach (KeyValuePair<int, int> pair in _netPingSent)
            {
                if (nowMs - pair.Value > 5000)
                {
                    if (stale == null) { stale = new List<int>(); }
                    stale.Add(pair.Key);
                }
            }
            if (stale == null)
            {
                return;
            }
            for (int i = 0; i < stale.Count; i++)
            {
                _netPingSent.Remove(stale[i]);
            }
        }

        void SendNetStatusIfNeeded(bool force, bool allowBeforeStart = false)
        {
            if (_mode != MugenMatchMode.NetKcp || _kcp == null ||
                MugenMatchSetup.NetRoomId == 0 || (!allowBeforeStart && !_netStarted))
            {
                return;
            }
            if (!force && _frame - _lastNetStatusFrame < NetStatusBroadcastIntervalFrames)
            {
                return;
            }

            _lastNetStatusFrame = _frame;
            int weakDelayMs = CurrentWeakNetworkDelayMs();
            int latencyMs = CurrentLocalLatencyMs();
            _kcp.Send(-1, new MugenNetStatusMsg
            {
                RoomId = MugenMatchSetup.NetRoomId,
                PlayerId = _localPlayerId,
                Frame = _frame,
                WeakDelayMs = weakDelayMs,
                LatencyMs = latencyMs,
            });
            _kcp.Flush();
            WriteNetDiagnostic(force ? "net_status_sent_force" : "net_status_sent",
                "weakDelayMs=" + weakDelayMs + " latencyMs=" + latencyMs);
        }

        void HandleTransportFault()
        {
            if (_kcp == null || !_kcp.Faulted)
            {
                return;
            }

            _netStarted = false;
            _weakNetworkStepIndex = -1;
            _netClosedReason = string.IsNullOrEmpty(_kcp.FaultReason) ? "network fault" : _kcp.FaultReason;
            WriteNetDiagnostic("transport_fault", _netClosedReason);
            ClearWeakLocalInputDelay();
            ResetFeelAssist();
            CloseNet(notifyServer: false);
            RefreshNetControlButtons();
        }

        void StartLatencyProbe()
        {
            if (_latencyProbe != null || string.IsNullOrEmpty(MugenMatchSetup.NetHost) || MugenMatchSetup.NetPort <= 0)
            {
                return;
            }

            try
            {
                _latencyProbe = new KcpClientTransport();
                _latencyProbe.Connect(MugenMatchSetup.NetHost, MugenMatchSetup.NetPort);
                _latencyProbeTimer = 0f;
                _latencyProbeMs = -1;
                _latencyProbeSent.Clear();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[MUGEN] latency probe unavailable: " + ex.Message);
                CloseLatencyProbe();
            }
        }

        void UpdateLatencyProbe(float deltaTime)
        {
            if (_mode == MugenMatchMode.NetKcp || _latencyProbe == null)
            {
                return;
            }
            if (_latencyProbe.Faulted)
            {
                CloseLatencyProbe();
                return;
            }

            _latencyProbe.Update();
            int now = RealtimeMs();
            while (_latencyProbe.Poll(out int _, out IMessage message))
            {
                if (message is PongMsg pong && _latencyProbeSent.TryGetValue(pong.Sequence, out int sentMs))
                {
                    _latencyProbeMs = Mathf.Max(0, now - sentMs);
                    _latencyProbeSent.Remove(pong.Sequence);
                }
            }

            if (_latencyProbe.LatencyMs > 0)
            {
                _latencyProbeMs = _latencyProbeMs >= 0
                    ? Mathf.RoundToInt(Mathf.Lerp(_latencyProbeMs, _latencyProbe.LatencyMs, 0.25f))
                    : _latencyProbe.LatencyMs;
            }

            _latencyProbeTimer -= Mathf.Max(0f, deltaTime);
            if (_latencyProbeTimer > 0f)
            {
                return;
            }

            int seq = ++_latencyProbeSeq;
            _latencyProbeSent[seq] = now;
            _latencyProbe.Send(-1, new PingMsg
            {
                Sequence = seq,
                ClientTimeMs = now,
            });
            _latencyProbeTimer = Mathf.Max(0.2f, LatencyProbeInterval);
            PruneLatencyProbe(now);
        }

        void PruneLatencyProbe(int nowMs)
        {
            if (_latencyProbeSent.Count <= 8)
            {
                return;
            }

            List<int> stale = null;
            foreach (KeyValuePair<int, int> pair in _latencyProbeSent)
            {
                if (nowMs - pair.Value > 5000)
                {
                    if (stale == null) { stale = new List<int>(); }
                    stale.Add(pair.Key);
                }
            }
            if (stale == null)
            {
                return;
            }
            for (int i = 0; i < stale.Count; i++)
            {
                _latencyProbeSent.Remove(stale[i]);
            }
        }

        static int RealtimeMs()
        {
            return Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f);
        }

        void CloseLatencyProbe()
        {
            if (_latencyProbe != null)
            {
                _latencyProbe.Close();
                _latencyProbe = null;
            }
            _latencyProbeSent.Clear();
            _latencyProbeMs = -1;
        }

        void SetupBattleSceneNetControls()
        {
            BindSceneButton("弱网模拟", ToggleWeakNetworkSimulation,
                out _weakNetworkButton, out _weakNetworkText, out _weakNetworkTmp);
            BindSceneButton("手感优化", ToggleFeelOptimization,
                out _feelButton, out _feelText, out _feelTmp);
            RefreshNetControlButtons();
        }

        void ToggleWeakNetworkSimulation()
        {
            if (_mode != MugenMatchMode.NetKcp || _kcp == null)
            {
                _weakNetworkStepIndex = -1;
                RefreshNetControlButtons();
                return;
            }

            int stepCount = WeakNetworkStepCount();
            _weakNetworkStepIndex = stepCount > 0 ? _weakNetworkStepIndex + 1 : -1;
            if (_weakNetworkStepIndex >= stepCount)
            {
                _weakNetworkStepIndex = -1;
            }
            ApplyWeakNetworkSimulation();
            EnforceWeakNetworkFeelPolicy("weak_network_changed");
            if (_weakNetworkStepIndex < 0)
            {
                ClearWeakLocalInputDelay();
                ResetFeelAssist();
            }
            WriteNetDiagnostic("weak_network_changed", WeakNetworkLabel());
            SendNetStatusIfNeeded(force: true);
            RefreshNetControlButtons();
        }

        void ToggleFeelOptimization()
        {
            if (_mode != MugenMatchMode.NetKcp)
            {
                _feelOptimizationEnabled = false;
                ResetFeelAssist();
                RefreshNetControlButtons();
                return;
            }

            _feelOptimizationEnabled = !_feelOptimizationEnabled;
            EnforceWeakNetworkFeelPolicy("feel_toggle");
            if (!_feelOptimizationEnabled)
            {
                ResetFeelAssist();
            }
            RefreshNetControlButtons();
        }

        void ApplyWeakNetworkSimulation()
        {
            if (_kcp == null)
            {
                return;
            }
            int delayMs = CurrentWeakNetworkDelayMs();
            _kcp.SetNetworkSimulation(delayMs > 0, dropPercent: 0, minDelayMs: delayMs, jitterMs: 0, burstDropPercent: 0);
            ApplyNetworkSessionBudget(CurrentNetworkOneWayBudgetMs());
        }

        void EnforceWeakNetworkFeelPolicy(string reason)
        {
            if (!IsWeakNetworkFeelSuppressed())
            {
                return;
            }

            if (_feelOptimizationEnabled)
            {
                _feelOptimizationEnabled = false;
                ResetFeelAssist();
                WriteNetDiagnostic("feel_optimization_suppressed", reason);
            }
        }

        void ApplyNetworkSessionBudget(int oneWayBudgetMs)
        {
            if (_session == null)
            {
                return;
            }

            float frameMs = 1000f / Mathf.Max(1f, TicksPerSecond);
            int budgetMs = Mathf.Max(0, oneWayBudgetMs);
            if (OnlineLatencyBudgetCapMs > 0)
            {
                budgetMs = Mathf.Min(budgetMs, OnlineLatencyBudgetCapMs);
            }
            _lastSessionLatencyBudgetMs = budgetMs;
            int oneWayDelayFrames = Mathf.CeilToInt(budgetMs / frameMs);
            int roundTripDelayFrames = Mathf.CeilToInt(budgetMs * 2f / frameMs);
            int safetyFrames = Mathf.Max(0, OnlineLatencyBudgetSafetyFrames);
            int configuredLead = Mathf.Max(1, OnlineMaxUnpredictedInputLead);
            int maxLead = Mathf.Max(1, _session.MaxPendingInputFrames - _session.InputLag - 1);
            _session.MaxPredictFrameCount = Mathf.Min(maxLead,
                Mathf.Max(Mathf.Max(1, OnlineMaxPredictFrames), oneWayDelayFrames + _session.InputLag + safetyFrames));
            _session.MaxUnpredictedInputLead = Mathf.Min(maxLead,
                Mathf.Max(configuredLead, roundTripDelayFrames + _session.InputLag + safetyFrames * 2));
        }

        MInput ApplyWeakLocalInputDelay(MInput rawInput)
        {
            if (_mode != MugenMatchMode.NetKcp)
            {
                return rawInput;
            }

            int delayFrames = CurrentWeakInputDelayFrames();
            if (delayFrames <= 0)
            {
                ClearWeakLocalInputDelay();
                return rawInput;
            }

            _weakLocalInputDelayBuffer.Add(rawInput);
            if (_weakLocalInputDelayBuffer.Count <= delayFrames)
            {
                return MInput.None;
            }

            MInput delayed = _weakLocalInputDelayBuffer[0];
            _weakLocalInputDelayBuffer.RemoveAt(0);
            return delayed;
        }

        void ClearWeakLocalInputDelay()
        {
            _weakLocalInputDelayBuffer.Clear();
        }

        int CurrentWeakInputDelayFrames()
        {
            int delayMs = CurrentWeakNetworkDelayMs();
            if (delayMs <= 0)
            {
                return 0;
            }
            float frameMs = 1000f / Mathf.Max(1f, TicksPerSecond);
            return Mathf.CeilToInt(delayMs / frameMs);
        }

        bool IsWeakNetworkFeelSuppressed()
        {
            return WeakNetworkSuppressFeelAssistMs > 0 &&
                   CurrentEffectiveWeakNetworkDelayMs() >= WeakNetworkSuppressFeelAssistMs;
        }

        void RefreshNetControlButtons()
        {
            bool online = _mode == MugenMatchMode.NetKcp;
            if (_weakNetworkButton != null) { _weakNetworkButton.interactable = online; }
            if (_feelButton != null) { _feelButton.interactable = online; }

            SetButtonText(_weakNetworkText, _weakNetworkTmp,
                online ? ("弱网模拟：" + WeakNetworkLabel()) : "弱网模拟：单机");
            SetButtonText(_feelText, _feelTmp,
                online ? ("手感优化：" + (_feelOptimizationEnabled ? "开" : "关")) : "手感优化：单机");
        }

        int WeakNetworkStepCount()
        {
            return WeakNetworkDelayStepsMs != null ? WeakNetworkDelayStepsMs.Length : 0;
        }

        int CurrentWeakNetworkDelayMs()
        {
            if (_weakNetworkStepIndex < 0 || WeakNetworkDelayStepsMs == null || _weakNetworkStepIndex >= WeakNetworkDelayStepsMs.Length)
            {
                return 0;
            }
            return Mathf.Max(0, WeakNetworkDelayStepsMs[_weakNetworkStepIndex]);
        }

        int CurrentEffectiveWeakNetworkDelayMs()
        {
            return Mathf.Max(CurrentWeakNetworkDelayMs(), _remoteWeakDelayMs);
        }

        int CurrentNetworkOneWayBudgetMs()
        {
            int weakOneWayMs = CurrentEffectiveWeakNetworkDelayMs();
            int localRttMs = CurrentLocalLatencyMs();
            int remoteRttMs = CurrentRemoteLatencyMs();
            int measuredRttMs = Mathf.Max(localRttMs, remoteRttMs);
            int measuredOneWayMs = measuredRttMs > 0 ? Mathf.CeilToInt(measuredRttMs * 0.5f) : 0;
            return Mathf.Max(weakOneWayMs, measuredOneWayMs);
        }

        string WeakNetworkLabel()
        {
            int delayMs = CurrentWeakNetworkDelayMs();
            return delayMs > 0 ? "+" + delayMs + "ms" : "关";
        }

        bool CanPredictLogicFrame()
        {
            if (_mode != MugenMatchMode.NetKcp ||
                !_netStarted ||
                _match == null ||
                _match.MatchOver ||
                _match.Engine == null ||
                _match.Round == null)
            {
                return false;
            }

            if (CanPredictNonGameplayFrame())
            {
                return true;
            }

            if (IsWeakNetworkFeelSuppressed())
            {
                return false;
            }

            return ShouldUseRollbackPrediction() &&
                   _match.Round.State == MRoundState.Fight &&
                   _match.Round.FinishType == MFinishType.NotYet;
        }

        bool CanPredictNonGameplayFrame()
        {
            return _mode == MugenMatchMode.NetKcp &&
                   _netStarted &&
                   _match != null &&
                   !_match.MatchOver &&
                   _match.Round != null &&
                   (_match.Round.State != MRoundState.Fight ||
                    _match.Round.FinishType != MFinishType.NotYet);
        }

        bool ShouldUseRollbackPrediction()
        {
            if (!_rollbackPredictionEnabled)
            {
                return false;
            }

            int minBudgetMs = Mathf.Max(0, OnlineRollbackPredictionMinBudgetMs);
            if (minBudgetMs <= 0 || CurrentNetworkOneWayBudgetMs() >= minBudgetMs)
            {
                return true;
            }

            return _session != null &&
                   _session.PendingInputFrames > _session.InputLag + Mathf.Max(1, OnlineMaxPredictFrames);
        }

        void ApplyOnlineFeelAssist(float deltaTime)
        {
            if (!_feelOptimizationEnabled || _mode != MugenMatchMode.NetKcp || !_netStarted ||
                _match == null || _match.MatchOver)
            {
                ResetFeelAssist();
                return;
            }
            if (IsWeakNetworkFeelSuppressed())
            {
                ResetFeelAssist();
                return;
            }
            if (_match.Round == null || _match.Round.State != MRoundState.Fight || _match.Engine == null)
            {
                ResetFeelAssist();
                return;
            }
            if (_session != null && _session.PendingInputFrames <= _session.InputLag + 1 && _session.CanAdvance())
            {
                ResetFeelAssist();
                return;
            }

            int player = Mathf.Clamp(_localPlayerId, 0, _renderers.Length - 1);
            SpriteRenderer renderer = _renderers[player];
            if (renderer == null)
            {
                return;
            }
            MChar localChar = player < _match.Engine.Chars.Count ? _match.Engine.Chars[player] : null;
            if (localChar == null || !localChar.Ctrl || !localChar.KeyCtrl || localChar.Life <= 0)
            {
                ResetFeelAssist();
                return;
            }

            MInput input = SampleLocalPlayerInput();
            Vector3 target = IntentOffset(input);
            float waitingBoost = _session != null
                ? Mathf.Clamp01((_session.PendingInputFrames - _session.InputLag) / 5f)
                : 0f;
            if (_session != null && !_session.CanAdvance())
            {
                waitingBoost = Mathf.Max(waitingBoost, 0.65f);
            }
            target *= 0.35f + 0.65f * waitingBoost;

            float speed = target.sqrMagnitude > _feelOffsets[player].sqrMagnitude
                ? FeelAssistResponse
                : FeelAssistCorrection;
            _feelOffsets[player] = Vector3.Lerp(_feelOffsets[player], target, Damp(speed, deltaTime));

            MInput buttons = input & ~MInput.DirMask;
            if (buttons != MInput.None && (_lastFeelInput & ~MInput.DirMask) == MInput.None)
            {
                _feelAttackPulse = 1f;
            }
            _feelAttackPulse = Mathf.MoveTowards(_feelAttackPulse, 0f, deltaTime * 6f);

            Transform t = renderer.transform;
            t.localPosition += _feelOffsets[player];
            if (_feelAttackPulse > 0f)
            {
                float pulse = _feelAttackPulse * FeelAssistAttackScale;
                t.localScale = new Vector3(t.localScale.x * (1f + pulse), t.localScale.y * (1f + pulse * 0.5f), t.localScale.z);
                Color color = renderer.color;
                color.r = Mathf.Min(1f, color.r + _feelAttackPulse * 0.25f);
                color.g = Mathf.Min(1f, color.g + _feelAttackPulse * 0.08f);
                renderer.color = color;
            }

            _lastFeelInput = input;
        }

        public static bool PresentationColorSelfTest()
        {
            Color tinted = new Color(0.3f, 0.4f, 0.7f, 0.2f);
            Color reset = MugenDrawStateApplier.AuthoritativeColor(MTransType.Default, 255, tinted);
            if (!Approximately(reset.r, 1f) || !Approximately(reset.g, 1f) ||
                !Approximately(reset.b, 1f) || !Approximately(reset.a, 1f))
            {
                return false;
            }

            Color alpha = MugenDrawStateApplier.AuthoritativeColor(MTransType.Add, 128, tinted);
            return Approximately(alpha.r, 1f) && Approximately(alpha.g, 1f) &&
                Approximately(alpha.b, 1f) && Mathf.Abs(alpha.a - 128f / 255f) < 0.001f;
        }

        void ResetFeelAssist()
        {
            _feelOffsets[0] = Vector3.zero;
            _feelOffsets[1] = Vector3.zero;
            _lastFeelInput = MInput.None;
            _feelAttackPulse = 0f;
        }

        Vector3 IntentOffset(MInput input)
        {
            float x = 0f;
            float y = 0f;
            if ((input & MInput.Left) != 0) { x -= FeelAssistMoveOffset; }
            if ((input & MInput.Right) != 0) { x += FeelAssistMoveOffset; }
            if ((input & MInput.Up) != 0) { y += FeelAssistVerticalOffset; }
            if ((input & MInput.Down) != 0) { y -= FeelAssistVerticalOffset * 0.6f; }
            return new Vector3(x, y, 0f);
        }

        static float Damp(float speed, float deltaTime)
        {
            return 1f - Mathf.Exp(-Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime));
        }

        void BindReturnToMainMenuUi()
        {
            Transform root = FindSceneTransform("\u8FD4\u56DE\u4E3B\u83DC\u5355");
            if (root == null)
            {
                return;
            }

            _returnMenuRoot = root.gameObject;
            MoveReturnMenuToOverlay(root as RectTransform);
            _returnMenuButton = root.GetComponentInChildren<Button>(true);
            if (_returnMenuButton != null)
            {
                _returnMenuButton.onClick.RemoveListener(ReturnToSelect);
                _returnMenuButton.onClick.AddListener(ReturnToSelect);
            }

            BindReturnMenuMessageText(root, out _returnMenuText, out _returnMenuTmp);
            _returnMenuRoot.SetActive(false);
            _matchOverUiShown = false;
        }

        void MoveReturnMenuToOverlay(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            Canvas sourceCanvas = root.GetComponentInParent<Canvas>(true);
            Canvas overlay = FindReturnMenuOverlayCanvas();
            if (overlay == null)
            {
                GameObject go = new GameObject(ReturnMenuOverlayCanvasName);
                go.layer = root.gameObject.layer;
                overlay = go.AddComponent<Canvas>();
                overlay.renderMode = RenderMode.ScreenSpaceOverlay;
                overlay.overrideSorting = true;
                overlay.sortingOrder = ReturnMenuSortingOrder;

                CanvasScaler scaler = go.AddComponent<CanvasScaler>();
                CopyCanvasScaler(sourceCanvas != null ? sourceCanvas.GetComponent<CanvasScaler>() : null, scaler);
                go.AddComponent<GraphicRaycaster>();
            }

            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.overrideSorting = true;
            overlay.sortingOrder = ReturnMenuSortingOrder;
            root.SetParent(overlay.transform, false);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = Vector2.zero;
            root.SetAsLastSibling();
        }

        static void CopyCanvasScaler(CanvasScaler sourceScaler, CanvasScaler scaler)
        {
            if (scaler == null)
            {
                return;
            }
            if (sourceScaler != null)
            {
                scaler.uiScaleMode = sourceScaler.uiScaleMode;
                scaler.referenceResolution = sourceScaler.referenceResolution;
                scaler.screenMatchMode = sourceScaler.screenMatchMode;
                scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
                scaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
            }
            else
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280f, 720f);
            }
        }

        static Canvas FindSceneCanvas(string name)
        {
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null &&
                    canvas.gameObject.name == name &&
                    canvas.gameObject.scene.IsValid() &&
                    canvas.gameObject.scene.isLoaded)
                {
                    return canvas;
                }
            }
            return null;
        }

        static Canvas FindReturnMenuOverlayCanvas()
        {
            return FindSceneCanvas(ReturnMenuOverlayCanvasName);
        }

        void ShowMatchOverUi()
        {
            if (_matchOverUiShown)
            {
                return;
            }
            _matchOverUiShown = true;

            ShowReturnMenu(_remoteForfeit ? _returnMenuMessageOverride : MatchWinnerText(), replayAnimation: true);
        }

        void HandleEscapeKey()
        {
            if (_match.MatchOver || _remoteForfeit)
            {
                return;
            }

            if (_returnMenuRoot != null && _returnMenuRoot.activeSelf && _returnMenuDismissible)
            {
                MugenAutoTest.Trace("return_menu_hide", "escape", MugenMatchSetup.NetRoomId, _frame, 0);
                HideReturnMenu();
                return;
            }

            MugenAutoTest.Trace("return_menu_show", "escape", MugenMatchSetup.NetRoomId, _frame, 0);
            ShowReturnMenu("\u79BB\u5F00\u6BD4\u8D5B\uFF1F", replayAnimation: true, dismissible: true);
        }

        void HideReturnMenu()
        {
            _returnMenuDismissible = false;
            if (_returnMenuRoot != null)
            {
                _returnMenuRoot.SetActive(false);
            }
        }

        void ShowReturnMenu(string message, bool replayAnimation = false, bool dismissible = false)
        {
            _returnMenuDismissible = dismissible;
            if (_returnMenuText != null) { _returnMenuText.text = message; }
            if (_returnMenuTmp != null) { _returnMenuTmp.text = message; }
            if (_returnMenuRoot == null)
            {
                return;
            }

            _returnMenuRoot.SetActive(true);
            Animator animator = _returnMenuRoot.GetComponent<Animator>();
            if (replayAnimation && animator != null && animator.runtimeAnimatorController != null)
            {
                animator.Play(0, -1, 0f);
            }
        }

        void EndMatchByRemoteForfeit()
        {
            if (_remoteForfeit || _returningToMainMenu)
            {
                return;
            }

            _remoteForfeit = true;
            _netStarted = false;
            _returnMenuMessageOverride = "\u83B7\u80DC\uFF01\u5BF9\u65B9\u9003\u8DD1";
            ResetFeelAssist();
            CompleteRunLog("remote-forfeit");
            CloseNet(notifyServer: false);
            ShowReturnMenu(_returnMenuMessageOverride, replayAnimation: true);
        }

        static bool IsOpponentLeaveReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return false;
            }
            return reason.IndexOf("left", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   reason.IndexOf("disconnected", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   reason.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        string MatchWinnerText()
        {
            if (_match == null || _match.MatchWinner < 0)
            {
                return "\u5E73\u5C40\uFF01";
            }
            return _match.MatchWinner == 0 ? "\u73A9\u5BB6\u4E00\u83B7\u80DC\uFF01" : "\u73A9\u5BB6\u4E8C\u83B7\u80DC\uFF01";
        }

        static void BindReturnMenuMessageText(Transform root, out Text text, out TMP_Text tmp)
        {
            text = null;
            tmp = null;

            TMP_Text[] tmps = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                if (tmps[i].GetComponentInParent<Button>(true) == null)
                {
                    tmp = tmps[i];
                    return;
                }
            }

            Text[] texts = root.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].GetComponentInParent<Button>(true) == null)
                {
                    text = texts[i];
                    return;
                }
            }
        }

        static void BindSceneButton(string name, UnityEngine.Events.UnityAction callback,
            out Button button, out Text text, out TMP_Text tmp)
        {
            button = null;
            text = null;
            tmp = null;

            Transform target = FindSceneTransform(name);
            if (target == null)
            {
                return;
            }

            button = target.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveListener(callback);
                button.onClick.AddListener(callback);
            }

            text = target.GetComponentInChildren<Text>(true);
            tmp = target.GetComponentInChildren<TMP_Text>(true);
        }

        static void SetButtonText(Text text, TMP_Text tmp, string value)
        {
            if (text != null) { text.text = value; }
            if (tmp != null) { tmp.text = value; }
        }

        static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.0001f;
        }

        static MInput SampleInput(KeyCode up, KeyCode down, KeyCode left, KeyCode right,
            KeyCode a, KeyCode b, KeyCode c,
            KeyCode x = KeyCode.None, KeyCode y = KeyCode.None, KeyCode z = KeyCode.None, KeyCode s = KeyCode.None)
        {
            MInput input = MInput.None;
            if (UnityEngine.Input.GetKey(up)) { input |= MInput.Up; }
            if (UnityEngine.Input.GetKey(down)) { input |= MInput.Down; }
            if (UnityEngine.Input.GetKey(left)) { input |= MInput.Left; }
            if (UnityEngine.Input.GetKey(right)) { input |= MInput.Right; }
            if (UnityEngine.Input.GetKey(a)) { input |= MInput.A; }
            if (UnityEngine.Input.GetKey(b)) { input |= MInput.B; }
            if (UnityEngine.Input.GetKey(c)) { input |= MInput.C; }
            if (x != KeyCode.None && UnityEngine.Input.GetKey(x)) { input |= MInput.X; }
            if (y != KeyCode.None && UnityEngine.Input.GetKey(y)) { input |= MInput.Y; }
            if (z != KeyCode.None && UnityEngine.Input.GetKey(z)) { input |= MInput.Z; }
            if (s != KeyCode.None && UnityEngine.Input.GetKey(s)) { input |= MInput.S; }
            return input;
        }

        static MInput SampleLocalPlayerInput()
        {
            return SampleInput(KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.LeftArrow, KeyCode.RightArrow,
                KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F) |
                MugenMobileInput.Read();
        }

        int CurrentLocalLatencyMs()
        {
            if (_mode != MugenMatchMode.NetKcp)
            {
                if (_latencyProbeMs >= 0)
                {
                    return _latencyProbeMs;
                }
                return _latencyProbe != null && _latencyProbe.LatencyMs > 0 ? _latencyProbe.LatencyMs : -1;
            }
            if (_session == null)
            {
                return -1;
            }
            int measuredLatency = _netPingMs >= 0
                ? _netPingMs
                : (_kcp != null && _kcp.LatencyMs > 0 ? _kcp.LatencyMs : -1);
            int weakDelayMs = CurrentWeakNetworkDelayMs();
            return NormalizeNetLatencyMs(measuredLatency, weakDelayMs);
        }

        int CurrentRemoteLatencyMs()
        {
            if (_mode != MugenMatchMode.NetKcp || _lastRemoteNetStatusRealtime < 0f)
            {
                return -1;
            }
            return NormalizeNetLatencyMs(_remoteLatencyMs, _remoteWeakDelayMs);
        }

        static int NormalizeNetLatencyMs(int reportedLatencyMs, int weakDelayMs)
        {
            int weakLatencyMs = weakDelayMs > 0 ? weakDelayMs * 2 : -1;
            if (reportedLatencyMs < 0)
            {
                return weakLatencyMs;
            }
            return weakLatencyMs > reportedLatencyMs ? weakLatencyMs : reportedLatencyMs;
        }

        void LogNetHealthIfNeeded()
        {
            if (_session == null || _frame - _lastNetHealthLogFrame < NetHealthLogIntervalFrames)
            {
                return;
            }

            int pending = _session.PendingInputFrames;
            int localInputDelayFrames = CurrentWeakInputDelayFrames();
            int effectiveWeakDelayMs = CurrentEffectiveWeakNetworkDelayMs();
            int latencyBudgetMs = CurrentNetworkOneWayBudgetMs();
            int frameMinusSim = _frame - _session.SimulatedFrame;
            if (pending <= _session.InputLag + 6 &&
                _session.RollbackCount == 0 &&
                effectiveWeakDelayMs <= 0 &&
                latencyBudgetMs < WeakNetworkSuppressFeelAssistMs &&
                frameMinusSim <= _session.InputLag + 6)
            {
                return;
            }

            _lastNetHealthLogFrame = _frame;
            Debug.Log(string.Format(
                "[NetHealth] frame={0} sim={1} simLag={2} input={3} pending={4} inputLag={5} predict={6} rollbackPredict={7} maxPredict={8} maxLead={9} stepSim={10} rollback={11} lastRollback={12} localLatency={13}ms remoteLatency={14}ms budget={15}ms weak={16}ms fault={17}",
                _frame,
                _session.SimulatedFrame,
                frameMinusSim,
                _session.InputFrame,
                pending,
                _session.InputLag,
                _session.PredictionEnabled,
                ShouldUseRollbackPrediction(),
                _session.MaxPredictFrameCount,
                _session.MaxUnpredictedInputLead,
                _session.LastStepSimulatedFrames,
                _session.RollbackCount,
                _session.LastRollbackFrame,
                CurrentLocalLatencyMs(),
                CurrentRemoteLatencyMs(),
                latencyBudgetMs,
                effectiveWeakDelayMs,
                _kcp != null && _kcp.Faulted ? _kcp.FaultReason : ""));
            WriteNetDiagnostic("net_health",
                "pending=" + pending +
                " rollback=" + _session.RollbackCount +
                " localInputDelayFrames=" + localInputDelayFrames +
                " effectiveWeakDelayMs=" + effectiveWeakDelayMs +
                " latencyBudgetMs=" + latencyBudgetMs +
                " frameMinusSim=" + frameMinusSim);
        }

        void LogNetStartWaitIfNeeded()
        {
            if (_mode != MugenMatchMode.NetKcp || _netStarted ||
                _frame - _lastNetStartWaitLogFrame < NetHealthLogIntervalFrames)
            {
                return;
            }

            _lastNetStartWaitLogFrame = _frame;
            int queued = _kcp != null ? _kcp.NetworkSimulationPendingPackets : 0;
            string fault = _kcp != null && _kcp.Faulted ? _kcp.FaultReason : "";
            Debug.Log(string.Format(
                "[NetStart] waiting start_match frame={0} room={1} local={2} weakDelay={3}ms simQueued={4} rtt={5}ms fault={6}",
                _frame,
                MugenMatchSetup.NetRoomId,
                _localPlayerId,
                CurrentWeakNetworkDelayMs(),
                queued,
                _kcp != null ? _kcp.LatencyMs : -1,
                fault));
            WriteNetDiagnostic("waiting_start_match",
                "queued=" + queued + " fault=" + fault + " closed=" + _netClosedReason);
        }

        void OpenNetDiagnosticLog()
        {
            if (!SaveNetDiagnosticLogFiles || _mode != MugenMatchMode.NetKcp)
            {
                return;
            }

            try
            {
                string dir = RuntimeLogDirectory("MugenNetLogs");
                Directory.CreateDirectory(dir);
                _netDiagnosticLogPath = Path.Combine(dir, string.Format(CultureInfo.InvariantCulture,
                    "net_{0}_room{1}_p{2}.jsonl",
                    DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture),
                    MugenMatchSetup.NetRoomId,
                    _localPlayerId));
                File.WriteAllText(_netDiagnosticLogPath, string.Empty);
                Debug.Log("[NetDiag] saved " + _netDiagnosticLogPath);
            }
            catch (Exception ex)
            {
                _netDiagnosticLogPath = "";
                Debug.LogWarning("[NetDiag] unavailable: " + ex.Message);
            }
        }

        void WriteNetDiagnostic(string eventName, string detail = "")
        {
            if (string.IsNullOrEmpty(_netDiagnosticLogPath))
            {
                return;
            }

            try
            {
                int pending = _session != null ? _session.PendingInputFrames : -1;
                int simFrame = _session != null ? _session.SimulatedFrame : -1;
                int inputFrame = _session != null ? _session.InputFrame : -1;
                int rollback = _session != null ? _session.RollbackCount : 0;
                int queued = _kcp != null ? _kcp.NetworkSimulationPendingPackets : 0;
                int rtt = _kcp != null ? _kcp.LatencyMs : -1;
                int effectiveWeakDelayMs = CurrentEffectiveWeakNetworkDelayMs();
                int localLatencyMs = CurrentLocalLatencyMs();
                int remoteLatencyMs = CurrentRemoteLatencyMs();
                int latencyBudgetMs = CurrentNetworkOneWayBudgetMs();
                int localInputDelayFrames = CurrentWeakInputDelayFrames();
                int maxPredictFrameCount = _session != null ? _session.MaxPredictFrameCount : -1;
                int maxUnpredictedLead = _session != null ? _session.MaxUnpredictedInputLead : -1;
                int stepSimulatedFrames = _session != null ? _session.LastStepSimulatedFrames : -1;
                int frameMinusSim = simFrame >= 0 ? _frame - simFrame : -1;
                string predictionEnabled = _session != null && _session.PredictionEnabled ? "true" : "false";
                string rollbackPredictionEnabled = _rollbackPredictionEnabled ? "true" : "false";
                string canPredictLogic = CanPredictLogicFrame() ? "true" : "false";
                string canAdvance = _session != null && _session.CanAdvance() ? "true" : "false";
                string json = string.Format(CultureInfo.InvariantCulture,
                    "{{\"unixMs\":{0},\"realtimeMs\":{1},\"roomId\":{2},\"localPlayer\":{3},\"frame\":{4},\"simFrame\":{5},\"inputFrame\":{6},\"pending\":{7},\"inputLag\":{8},\"rollback\":{9},\"rttMs\":{10},\"weakDelayMs\":{11},\"remoteWeakDelayMs\":{12},\"localLatencyMs\":{13},\"remoteLatencyMs\":{14},\"effectiveWeakDelayMs\":{15},\"latencyBudgetMs\":{16},\"localInputDelayFrames\":{17},\"predictionEnabled\":{18},\"rollbackPredictionEnabled\":{19},\"canPredictLogic\":{20},\"canAdvance\":{21},\"maxPredictFrameCount\":{22},\"maxUnpredictedInputLead\":{23},\"stepSimulatedFrames\":{24},\"frameMinusSim\":{25},\"simQueued\":{26},\"netStarted\":{27},\"event\":\"{28}\",\"detail\":\"{29}\"}}",
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Mathf.RoundToInt(Time.realtimeSinceStartup * 1000f),
                    MugenMatchSetup.NetRoomId,
                    _localPlayerId,
                    _frame,
                    simFrame,
                    inputFrame,
                    pending,
                    _session != null ? _session.InputLag : -1,
                    rollback,
                    rtt,
                    CurrentWeakNetworkDelayMs(),
                    _remoteWeakDelayMs,
                    localLatencyMs,
                    remoteLatencyMs,
                    effectiveWeakDelayMs,
                    latencyBudgetMs,
                    localInputDelayFrames,
                    predictionEnabled,
                    rollbackPredictionEnabled,
                    canPredictLogic,
                    canAdvance,
                    maxPredictFrameCount,
                    maxUnpredictedLead,
                    stepSimulatedFrames,
                    frameMinusSim,
                    queued,
                    _netStarted ? "true" : "false",
                    JsonEscape(eventName),
                    JsonEscape(detail ?? string.Empty));
                File.AppendAllText(_netDiagnosticLogPath, json + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[NetDiag] write failed: " + ex.Message);
                _netDiagnosticLogPath = "";
            }
        }

        static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        void OnNetFrameInputs(int frame, MInput[] inputs)
        {
            LogLongPauseIfNeeded(frame, inputs);
            CaptureRunLogFrame(frame, inputs);
        }

        void LogLongPauseIfNeeded(int frame, IReadOnlyList<MInput> inputs)
        {
            if (_match == null || _match.Engine == null || _match.Engine.PauseState == null)
            {
                return;
            }

            bool active = _match.Engine.PauseState.AnyActive;
            if (!active)
            {
                _pauseWarnStartFrame = -1;
                _pauseWarnLogged = false;
                return;
            }

            if (_pauseWarnStartFrame < 0)
            {
                _pauseWarnStartFrame = frame;
            }

            if (_pauseWarnLogged || frame - _pauseWarnStartFrame < LongPauseWarnFrames)
            {
                return;
            }

            _pauseWarnLogged = true;
            MInput p0 = inputs != null && inputs.Count > 0 ? inputs[0] : MInput.None;
            MInput p1 = inputs != null && inputs.Count > 1 ? inputs[1] : MInput.None;
            Debug.Log(string.Format(
                "[暂停诊断] 长暂停 frame={0} duration={1} round={2}/{3} pause={4} super={5} pauseBuf={6} superBuf={7} owner={8}/{9} input0={10} input1={11} p0={12} p1={13}",
                frame,
                frame - _pauseWarnStartFrame,
                _match.Round != null ? _match.Round.State.ToString() : "<null>",
                _match.Round != null ? _match.Round.FinishType.ToString() : "<null>",
                _match.Engine.PauseState.PauseTime,
                _match.Engine.PauseState.SuperTime,
                _match.Engine.PauseState.PauseTimeBuffer,
                _match.Engine.PauseState.SuperTimeBuffer,
                _match.Engine.PauseState.PausePlayerNo,
                _match.Engine.PauseState.SuperPlayerNo,
                (int)p0,
                (int)p1,
                CharDiag(0),
                CharDiag(1)));
        }

        string CharDiag(int index)
        {
            if (_match == null || _match.Engine == null || index < 0 || index >= _match.Engine.Chars.Count)
            {
                return "<missing>";
            }

            MChar c = _match.Engine.Chars[index];
            string commands = c.CommandList != null ? string.Join("|", c.CommandList.ActiveNames()) : "";
            return string.Format(
                "state={0} anim={1} elem={2} time={3} st={4} mt={5} ctrl={6} key={7} pause={8} pmt={9} smt={10} power={11} pos=({12},{13}) vel=({14},{15}) cmd={16}",
                c.StateNo,
                c.AnimNo,
                c.AnimElemNo,
                c.Time,
                c.StateType,
                c.MoveType,
                c.Ctrl,
                c.KeyCtrl,
                c.PauseBool,
                c.PauseMovetime,
                c.SuperMovetime,
                c.Power,
                c.Pos.X.ToFloat().ToString("0.###", CultureInfo.InvariantCulture),
                c.Pos.Y.ToFloat().ToString("0.###", CultureInfo.InvariantCulture),
                c.Vel.X.ToFloat().ToString("0.###", CultureInfo.InvariantCulture),
                c.Vel.Y.ToFloat().ToString("0.###", CultureInfo.InvariantCulture),
                commands);
        }

        void RefreshHudNames()
        {
            if (_hud == null)
            {
                return;
            }
            _hud.SetPlayerNames(CurrentCharacterName(0), CurrentCharacterName(1));
        }

        string CurrentCharacterName(int player)
        {
            List<string> team = player == 0 ? MugenMatchSetup.Team0 : MugenMatchSetup.Team1;
            if (team.Count == 0)
            {
                return "";
            }
            int index = _match != null ? _match.ActiveIndex(player) : 0;
            index = Mathf.Clamp(index, 0, team.Count - 1);
            return MugenChineseText.CharacterName(team[index]);
        }

        void AlignBattleGroundToSceneUi()
        {
            if (!AlignGroundToSceneUi)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            float viewportY = GroundViewportY;
            RectTransform ground = FindSceneRectTransform(GroundUiName);
            if (ground != null)
            {
                viewportY = GroundViewportTop(ground, GroundViewportY);
            }

            if (TryViewportToWorldOnZPlane(cam, Mathf.Clamp01(viewportY), transform.position.z, out Vector3 world))
            {
                Vector3 pos = transform.position;
                pos.y = world.y;
                transform.position = pos;
            }

            RefreshWorldGroundFromSceneUi(cam);

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }

        static float GroundViewportTop(RectTransform rect, float fallback)
        {
            if (rect == null)
            {
                return fallback;
            }
            if (!Mathf.Approximately(rect.anchorMax.y, rect.anchorMin.y))
            {
                return rect.anchorMax.y;
            }
            if (Screen.height <= 0)
            {
                return fallback;
            }

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float top = corners[0].y;
            for (int i = 1; i < corners.Length; i++)
            {
                top = Mathf.Max(top, corners[i].y);
            }
            return Mathf.Clamp01(top / Screen.height);
        }

        static bool TryViewportToWorldOnZPlane(Camera cam, float viewportY, float z, out Vector3 world)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, viewportY, 0f));
            Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, z));
            if (plane.Raycast(ray, out float enter))
            {
                world = ray.GetPoint(enter);
                return true;
            }
            world = Vector3.zero;
            return false;
        }

        static RectTransform FindSceneRectTransform(string name)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t.name != name || !t.gameObject.scene.IsValid() || !t.gameObject.scene.isLoaded)
                {
                    continue;
                }
                RectTransform rect = t as RectTransform;
                if (rect != null && rect.GetComponentInParent<Canvas>(true) != null)
                {
                    return rect;
                }
            }
            return null;
        }

        static Transform FindSceneTransform(string name)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t.name == name && t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded)
                {
                    return t;
                }
            }
            return null;
        }

        void RenderAll()
        {
            if (_match == null || _match.Engine == null)
            {
                return;
            }
            for (int i = 0; i < 2; i++)
            {
                RenderChar(_match.Engine.Chars[i], _match.Engine.Data[i], _renderers[i], FighterSortingOrderBase + i);
            }
        }

        void RenderChar(MChar c, MCharData data, SpriteRenderer renderer, int baseSortingOrder)
        {
            if (renderer == null || !_byData.TryGetValue(data, out Bundle bundle))
            {
                return;
            }

            renderer.flipX = c.Facing.Raw < 0;
            renderer.transform.localPosition = new Vector3(
                c.Pos.X.ToFloat() / PixelsPerUnit,
                -c.Pos.Y.ToFloat() / PixelsPerUnit,
                0f);
            MugenDrawStateApplier.Apply(c, renderer, renderer.transform, PixelsPerUnit, baseSortingOrder);

            if (!data.Anims.TryGetValue(c.AnimNo, out MAnimData anim) || anim.Frames.Length == 0)
            {
                return;
            }
            int elem = Mathf.Clamp(c.AnimElem, 0, anim.Frames.Length - 1);
            MAnimFrame frame = anim.Frames[elem];
            EnsureBuilt(bundle, c.AnimNo);
            long key = MugenSpriteAnimator.Key(frame.SpriteGroup, frame.SpriteImage);
            if (bundle.Source.Cache.TryGetValue(key, out Sprite sprite))
            {
                renderer.sprite = sprite;
            }
        }

        void EnsureBuilt(Bundle bundle, int animNo)
        {
            if (bundle.Built.Contains(animNo))
            {
                return;
            }
            if (bundle.GameAnims.TryGetValue(animNo, out GameData.AnimData anim))
            {
                MugenSpriteLoader.BuildForAnim(bundle.Source, anim);
            }
            bundle.Built.Add(animNo);
        }

        void OnGUI()
        {
            if (_match == null)
            {
                return;
            }

            string text;
            if (_match.MatchOver)
            {
                text = _match.MatchWinner < 0 ? "整场平局" : "胜者：玩家 " + (_match.MatchWinner + 1);
            }
            else if (_remoteForfeit)
            {
                text = "获胜！对方逃跑";
            }
            else if (_mode == MugenMatchMode.NetKcp && !_netStarted)
            {
                text = string.IsNullOrEmpty(_netClosedReason) ? "等待对手进入房间" : "房间已关闭：" + _netClosedReason;
            }
            else
            {
                text = string.Format("第 {0} 小局   我方剩余 {1} / 对手剩余 {2}   模式 {3}",
                    _match.BoutNo, _match.Remaining(0), _match.Remaining(1), MugenChineseText.MatchMode(_mode));
            }

            GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            style.font = MugenChineseText.Font();
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(12f, 130f, 900f, 40f), text, style);
            GUI.Label(new Rect(12f, 168f, 900f, 32f), "Esc：返回主菜单", style);
        }

        void ReturnToSelect()
        {
            if (_returningToMainMenu)
            {
                return;
            }
            _returningToMainMenu = true;
            MugenAutoTest.Trace("return_to_main_menu", "button", MugenMatchSetup.NetRoomId, _frame, 0);
            CloseNet();
            MugenMatchSetup.Clear();
            MugenMatchSetup.ReturnToSelect = null;
            SceneManager.LoadScene(MugenMatchSetup.MainMenuSceneName);
        }

        void OnDestroy()
        {
            if (Application.isMobilePlatform || ForceMobileControls)
            {
                MugenMobileInputUi.Hide();
            }

            CompleteRunLog("view-destroy");
            CloseNet();
            CloseLatencyProbe();
        }

        void SetupRunLog()
        {
            if (!LogFrames && !AutoRunLog)
            {
                return;
            }

            MBattleRunMode mode = _mode == MugenMatchMode.NetKcp
                ? MBattleRunMode.NetKcp
                : (_mode == MugenMatchMode.VersusAI ? MBattleRunMode.LocalAi : MBattleRunMode.LocalTest);
            _runLog = new MBattleRunLogRecorder(mode, "unity-team-battle", 2);

            string p0Agent = _mode == MugenMatchMode.NetKcp && _localPlayerId != 0 ? "remote" : "local";
            string p1Agent = _mode == MugenMatchMode.NetKcp
                ? (_localPlayerId == 1 ? "local" : "remote")
                : (_mode == MugenMatchMode.VersusAI ? "ai" : "local");
            _runLog.SetPlayer(0, PlayerUid(0), p0Agent, MugenMatchSetup.ToCsv(MugenMatchSetup.Team0));
            _runLog.SetPlayer(1, PlayerUid(1), p1Agent, MugenMatchSetup.ToCsv(MugenMatchSetup.Team1));
            Debug.Log(string.Format("[RunLog] start mode={0} localTest={1} p0={2}/{3} p1={4}/{5}",
                mode, mode != MBattleRunMode.NetKcp, PlayerUid(0), p0Agent, PlayerUid(1), p1Agent));
        }

        string PlayerUid(int slot)
        {
            if (_mode == MugenMatchMode.NetKcp)
            {
                return string.Format("room-{0}-p{1}", MugenMatchSetup.NetRoomId, slot);
            }
            return slot == 0 ? "local-p0" : (_mode == MugenMatchMode.VersusAI ? "ai-p1" : "local-p1");
        }

        void CaptureRunLogFrame(int frame, IReadOnlyList<MInput> inputs)
        {
            if (_runLog == null || _match == null || _match.Engine == null)
            {
                return;
            }

            MBattleRunFrame record = _runLog.CaptureFrame(frame, _match.Engine, _match.ComputeHash, inputs);
            if (LogFrames)
            {
                string cmd0 = record.ActiveCommands.Length > 0 ? string.Join("|", record.ActiveCommands[0]) : "";
                string cmd1 = record.ActiveCommands.Length > 1 ? string.Join("|", record.ActiveCommands[1]) : "";
                int in0 = record.Inputs.Length > 0 ? record.Inputs[0] : 0;
                int in1 = record.Inputs.Length > 1 ? record.Inputs[1] : 0;
                Debug.Log(string.Format(
                    "[RunLog] mode={0} localTest={1} frame={2} uid0={3} uid1={4} in0={5} in1={6} cmd0={7} cmd1={8} hash={9}",
                    _mode, _mode != MugenMatchMode.NetKcp, frame, PlayerUid(0), PlayerUid(1), in0, in1, cmd0, cmd1, record.HashHex));
            }

            if (_match.MatchOver)
            {
                CompleteRunLog("match-over");
            }
        }

        void CompleteRunLog(string reason)
        {
            if (_runLog == null)
            {
                return;
            }

            MBattleRunLog log = _runLog.Complete(reason);
            PersistRunLog(log);
            Debug.Log(string.Format("[RunLog] complete mode={0} frames={1} inputChecksum={2} hashChecksum={3} finalHash={4} reason={5}",
                log.Mode, log.Frames.Count, log.InputChecksumHex, log.HashChecksumHex, log.FinalHashHex, log.EndReason));
            _runLog = null;
        }

        void PersistRunLog(MBattleRunLog log)
        {
            if (!SaveRunLogFiles || log == null || log.Frames.Count == 0)
            {
                return;
            }

            try
            {
                string dir = RuntimeLogDirectory("MugenRunLogs");
                Directory.CreateDirectory(dir);
                string name = string.Format("run_{0}_{1}_room{2}_p{3}.json",
                    DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
                    log.Mode,
                    MugenMatchSetup.NetRoomId,
                    _localPlayerId);
                string path = Path.Combine(dir, name);
                File.WriteAllText(path, MBattleRunLogJson.ToJson(log));
                Debug.Log("[RunLog] saved " + path);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RunLog] save failed: " + ex.Message);
            }
        }

        string RuntimeLogDirectory(string leaf)
        {
            try
            {
                MugenAutoTest.EnsureInitialized();
                if (MugenAutoTest.Enabled && !string.IsNullOrEmpty(MugenAutoTest.LogDir))
                {
                    return Path.Combine(MugenAutoTest.LogDir, leaf);
                }
            }
            catch
            {
            }

            return Path.Combine(Application.persistentDataPath, leaf);
        }

        void CloseNet(bool notifyServer = true)
        {
            if (_kcp == null)
            {
                return;
            }

            ClearWeakLocalInputDelay();
            _remoteWeakDelayMs = 0;
            _remoteLatencyMs = -1;
            _lastRemoteNetStatusRealtime = -1f;
            _netPingSent.Clear();
            _netPingMs = -1;
            _netPingTimer = 0f;
            if (notifyServer && MugenMatchSetup.NetRoomId != 0)
            {
                _kcp.SetNetworkSimulation(false, dropPercent: 0, minDelayMs: 0, jitterMs: 0, burstDropPercent: 0);
                _weakNetworkStepIndex = -1;
                RefreshNetControlButtons();
                _kcp.Send(-1, new LeaveRoomMsg
                {
                    RoomId = MugenMatchSetup.NetRoomId,
                    MatchCompleted = _match != null && _match.MatchOver,
                });
                _kcp.Flush();
            }
            WriteNetDiagnostic("close_net", "notifyServer=" + notifyServer);
            _kcp.Close();
            _kcp = null;
            _netChannel = null;
            _session = null;
            MugenMatchSetup.NetTransport = null;
        }
    }
}
