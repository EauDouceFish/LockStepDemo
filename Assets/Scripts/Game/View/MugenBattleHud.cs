using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;

namespace Lockstep.View
{
    /// <summary>
    /// Battle HUD binder. BattleScene uses hand-authored UI by name; older demo scenes fall back to
    /// a small generated HUD so existing showcases keep running.
    /// </summary>
    public sealed class MugenBattleHud : MonoBehaviour
    {
        Image _p1Real;
        Image _p1White;
        Image _p2Real;
        Image _p2White;
        Text _timerText;
        Text _announceText;
        Text _latencyText;
        Text _p1DescText;
        Text _p2DescText;
        TMP_Text _timerTmp;
        TMP_Text _announceTmp;
        TMP_Text _latencyTmp;
        TMP_Text _p1DescTmp;
        TMP_Text _p2DescTmp;
        GameObject _announceRoot;
        readonly Image[][] _pips = new Image[2][];
        float _p1WhiteFill = 1f;
        float _p2WhiteFill = 1f;
        bool _announcementInitialized;
        bool _announcementVisible;
        float _announcementHideTimer;
        int _announcementRoundNo;
        int _announcementWinner;
        MRoundState _announcementState;
        MFinishType _announcementFinishType;
        string _announcementText;

        const float WhiteFadeSpeed = 0.6f;
        const float AnnouncementVisibleSeconds = 1.35f;

        static readonly Color BackColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        static readonly Color HealthColor = new Color(0.85f, 0.15f, 0.15f, 1f);
        static readonly Color PipOn = new Color(1f, 0.85f, 0.2f, 1f);
        static readonly Color PipOff = new Color(0.25f, 0.25f, 0.25f, 0.8f);

        public static MugenBattleHud Create(int roundsToWin)
        {
            MugenBattleHud sceneHud = TryBindSceneHud(roundsToWin);
            if (sceneHud != null)
            {
                return sceneHud;
            }

            GameObject canvasGo = new GameObject("BattleHud");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasGo.AddComponent<GraphicRaycaster>();

            MugenBattleHud hud = canvasGo.AddComponent<MugenBattleHud>();
            hud.BuildFallback(canvas, roundsToWin);
            return hud;
        }

        public void SetPlayerNames(string p1Name, string p2Name)
        {
            SetText(_p1DescText, _p1DescTmp, "\u73A9\u5BB6\u4E00:" + p1Name);
            SetText(_p2DescText, _p2DescTmp, "\u73A9\u5BB6\u4E8C:" + p2Name);
        }

        /*
            SetText(_p1DescText, _p1DescTmp, "玩家一：" + p1Name);
            SetText(_p2DescText, _p2DescTmp, "玩家二：" + p2Name);
        }

        */

        public void UpdateHud(MRoundSystem round, MChar p1, MChar p2, float deltaTime)
        {
            UpdateHud(round, p1, p2, deltaTime, MugenMatchSetup.Mode, -1, -1);
        }

        public void UpdateHud(MRoundSystem round, MChar p1, MChar p2, float deltaTime,
            MugenMatchMode mode, int latencyMs, int displayRoundNo)
        {
            UpdateHud(round, p1, p2, deltaTime, mode, latencyMs, -1, displayRoundNo);
        }

        public void UpdateHud(MRoundSystem round, MChar p1, MChar p2, float deltaTime,
            MugenMatchMode mode, int localLatencyMs, int remoteLatencyMs, int displayRoundNo)
        {
            if (round == null || p1 == null || p2 == null) { return; }

            UpdateBar(_p1Real, ref _p1WhiteFill, _p1White, p1.Life, p1.LifeMax, deltaTime);
            UpdateBar(_p2Real, ref _p2WhiteFill, _p2White, p2.Life, p2.LifeMax, deltaTime);

            int secs = round.TimerSeconds;
            SetText(_timerText, _timerTmp, secs < 0 ? "--" : secs.ToString());

            if (_latencyText != null || _latencyTmp != null)
            {
                string latency = mode == MugenMatchMode.NetKcp
                    ? "\u6211\u65B9\u5EF6\u8FDF\uFF1A" + LatencyValue(localLatencyMs) +
                      "\uFF0C\u5BF9\u624B\u5EF6\u8FDF\uFF1A" + LatencyValue(remoteLatencyMs)
                    : (localLatencyMs >= 0
                        ? "\u5EF6\u8FDF:" + localLatencyMs.ToString() + "ms"
                        : "\u5EF6\u8FDF:--");
                SetText(_latencyText, _latencyTmp, latency);
            }

            /*
                string latency = mode == MugenMatchMode.NetKcp
                    ? "延迟：" + Mathf.Max(0, latencyMs).ToString() + "ms"
                    : "单机";
                SetText(_latencyText, _latencyTmp, latency);
            }

            */

            for (int p = 0; p < 2; p++)
            {
                if (_pips[p] == null) { continue; }
                int won = p < round.RoundsWon.Length ? round.RoundsWon[p] : 0;
                for (int r = 0; r < _pips[p].Length; r++)
                {
                    _pips[p][r].color = r < won ? PipOn : PipOff;
                }
            }

            int roundNo = displayRoundNo > 0 ? displayRoundNo : round.RoundNo;
            UpdateAnnouncement(round, roundNo, deltaTime);
        }

        static MugenBattleHud TryBindSceneHud(int roundsToWin)
        {
            Transform leftRoot = FindSceneTransform("HealthUI_Left");
            Transform rightRoot = FindSceneTransform("HealthUI_Right");
            Transform timer = FindSceneTransform("\u8BA1\u65F6\u6587\u672C");
            Transform announce = FindSceneTransform("\u56DE\u5408\u72B6\u6001\u6587\u672C");
            Transform roundBg = FindSceneTransform("\u56DE\u5408\u80CC\u666F");

            if (leftRoot == null || rightRoot == null || timer == null)
            {
                return null;
            }

            Canvas canvas = leftRoot.GetComponentInParent<Canvas>(true);
            GameObject host = canvas != null ? canvas.gameObject : leftRoot.gameObject;
            MugenBattleHud hud = host.GetComponent<MugenBattleHud>() ?? host.AddComponent<MugenBattleHud>();
            hud._p1Real = FindChildImage(leftRoot, "RealHealth");
            hud._p1White = FindChildImage(leftRoot, "WhiteHealth");
            hud._p2Real = FindChildImage(rightRoot, "RealHealth");
            hud._p2White = FindChildImage(rightRoot, "WhiteHealth");
            hud.BindText(timer, out hud._timerText, out hud._timerTmp);
            hud.BindText(announce, out hud._announceText, out hud._announceTmp);
            hud._announceRoot = roundBg != null ? roundBg.gameObject :
                (announce != null ? announce.gameObject : null);
            hud.BindText(FindSceneTransform("\u5EF6\u8FDF\u6587\u672C"), out hud._latencyText, out hud._latencyTmp);
            hud.BindText(FindChildRecursive(leftRoot, "Desc"), out hud._p1DescText, out hud._p1DescTmp);
            hud.BindText(FindChildRecursive(rightRoot, "Desc"), out hud._p2DescText, out hud._p2DescTmp);
            hud.ConfigureSceneBar(hud._p1Real, true);
            hud.ConfigureSceneBar(hud._p1White, true);
            hud.ConfigureSceneBar(hud._p2Real, false);
            hud.ConfigureSceneBar(hud._p2White, false);
            hud.SetText(hud._latencyText, hud._latencyTmp, "\u5EF6\u8FDF:--");
            hud.HideAnnouncement();
            return hud;
        }

        /*
        static MugenBattleHud TryBindSceneHud(int roundsToWin)
        {
            Transform leftRoot = FindSceneTransform("HealthUI_Left");
            Transform rightRoot = FindSceneTransform("HealthUI_Right");
            Transform timer = FindSceneTransform("计时文本");
            Transform announce = FindSceneTransform("回合状态文本");

            if (leftRoot == null || rightRoot == null || timer == null || announce == null)
            {
                return null;
            }

            Canvas canvas = leftRoot.GetComponentInParent<Canvas>(true);
            GameObject host = canvas != null ? canvas.gameObject : leftRoot.gameObject;
            MugenBattleHud hud = host.GetComponent<MugenBattleHud>() ?? host.AddComponent<MugenBattleHud>();
            hud._p1Real = FindChildImage(leftRoot, "RealHealth");
            hud._p1White = FindChildImage(leftRoot, "WhiteHealth");
            hud._p2Real = FindChildImage(rightRoot, "RealHealth");
            hud._p2White = FindChildImage(rightRoot, "WhiteHealth");
            hud.BindText(timer, out hud._timerText, out hud._timerTmp);
            hud.BindText(announce, out hud._announceText, out hud._announceTmp);
            hud.BindText(FindSceneTransform("延迟文本"), out hud._latencyText, out hud._latencyTmp);
            hud.BindText(FindChildRecursive(leftRoot, "Desc"), out hud._p1DescText, out hud._p1DescTmp);
            hud.BindText(FindChildRecursive(rightRoot, "Desc"), out hud._p2DescText, out hud._p2DescTmp);
            hud.ConfigureSceneBar(hud._p1Real, true);
            hud.ConfigureSceneBar(hud._p1White, true);
            hud.ConfigureSceneBar(hud._p2Real, false);
            hud.ConfigureSceneBar(hud._p2White, false);
            hud.SetText(hud._latencyText, hud._latencyTmp, "单机");
            hud.SetText(hud._announceText, hud._announceTmp, "第一回合");
            return hud;
        }

        */

        void ConfigureSceneBar(Image image, bool left)
        {
            if (image == null) { return; }
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = left ? (int)Image.OriginHorizontal.Left : (int)Image.OriginHorizontal.Right;
            image.fillAmount = 1f;
            image.color = (image == _p1Real || image == _p2Real) ? HealthColor : Color.white;
        }

        void BuildFallback(Canvas canvas, int roundsToWin)
        {
            Sprite px = WhiteSprite();
            Font font = MugenChineseText.Font();

            BuildFallbackBar(canvas.transform, px, true, new Vector2(0f, 1f), new Vector2(20f, -20f),
                out _p1White, out _p1Real);
            BuildFallbackBar(canvas.transform, px, false, new Vector2(1f, 1f), new Vector2(-20f, -20f),
                out _p2White, out _p2Real);

            _timerText = MakeText(canvas.transform, font, 40, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(160f, 60f), "60");
            _latencyText = MakeText(canvas.transform, font, 20, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(20f, -74f), new Vector2(520f, 32f), "\u5EF6\u8FDF:--");
            _announceText = MakeText(canvas.transform, font, 64, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 120f), "第一回合");

            for (int p = 0; p < 2; p++)
            {
                _pips[p] = new Image[roundsToWin];
                bool left = p == 0;
                Vector2 anchor = left ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                for (int r = 0; r < roundsToWin; r++)
                {
                    float dir = left ? 1f : -1f;
                    Image pip = MakeImage(canvas.transform, px, anchor,
                        new Vector2(dir * (24f + r * 28f), -64f), new Vector2(18f, 18f));
                    pip.color = PipOff;
                    _pips[p][r] = pip;
                }
            }
        }

        void BuildFallbackBar(Transform parent, Sprite px, bool left, Vector2 anchor, Vector2 pos,
            out Image white, out Image real)
        {
            Vector2 size = new Vector2(480f, 32f);
            Image back = MakeImage(parent, px, anchor, pos, size);
            back.color = BackColor;
            back.rectTransform.pivot = left ? new Vector2(0f, 1f) : new Vector2(1f, 1f);

            white = MakeImage(back.transform, px, new Vector2(0f, 0.5f), Vector2.zero, size);
            white.type = Image.Type.Filled;
            white.fillMethod = Image.FillMethod.Horizontal;
            white.fillOrigin = left ? (int)Image.OriginHorizontal.Left : (int)Image.OriginHorizontal.Right;
            white.color = Color.white;
            StretchFull(white.rectTransform);

            real = MakeImage(back.transform, px, new Vector2(0f, 0.5f), Vector2.zero, size);
            real.type = Image.Type.Filled;
            real.fillMethod = Image.FillMethod.Horizontal;
            real.fillOrigin = left ? (int)Image.OriginHorizontal.Left : (int)Image.OriginHorizontal.Right;
            real.color = HealthColor;
            StretchFull(real.rectTransform);
        }

        static void UpdateBar(Image real, ref float whiteFill, Image white, int life, int lifeMax, float deltaTime)
        {
            float percent = lifeMax > 0 ? Mathf.Clamp01((float)life / lifeMax) : 0f;
            if (real != null) { real.fillAmount = percent; }
            if (whiteFill < percent) { whiteFill = percent; }
            else if (whiteFill > percent) { whiteFill = Mathf.Max(percent, whiteFill - WhiteFadeSpeed * deltaTime); }
            if (white != null) { white.fillAmount = whiteFill; }
        }

        void UpdateAnnouncement(MRoundSystem round, int roundNo, float deltaTime)
        {
            if (round == null)
            {
                HideAnnouncement();
                return;
            }

            if (!_announcementInitialized)
            {
                _announcementInitialized = true;
                _announcementRoundNo = int.MinValue;
                _announcementState = (MRoundState)(-1);
                _announcementFinishType = (MFinishType)(-1);
                _announcementWinner = int.MinValue;
                HideAnnouncement();
            }

            MRoundState state = round.State;
            if (round.FinishType != MFinishType.NotYet)
            {
                if (_announcementFinishType != round.FinishType || _announcementWinner != round.Winner)
                {
                    ShowAnnouncement(FinishLabel(round));
                    _announcementFinishType = round.FinishType;
                    _announcementWinner = round.Winner;
                }
            }
            else if (roundNo != _announcementRoundNo)
            {
                ShowAnnouncement("Round " + roundNo.ToString());
                _announcementRoundNo = roundNo;
                _announcementFinishType = MFinishType.NotYet;
                _announcementWinner = -1;
            }
            else if (state == MRoundState.Fight && _announcementState != MRoundState.Fight)
            {
                ShowAnnouncement("FIGHT");
            }

            _announcementState = state;
            if (_announcementVisible)
            {
                _announcementHideTimer -= Mathf.Max(0f, deltaTime);
                if (_announcementHideTimer <= 0f)
                {
                    HideAnnouncement();
                }
            }
        }

        void ShowAnnouncement(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                HideAnnouncement();
                return;
            }
            if (_announcementText != value)
            {
                SetText(_announceText, _announceTmp, value);
                _announcementText = value;
            }
            if (_announceRoot != null && !_announceRoot.activeSelf)
            {
                _announceRoot.SetActive(true);
            }
            _announcementVisible = true;
            _announcementHideTimer = AnnouncementVisibleSeconds;
        }

        void HideAnnouncement()
        {
            _announcementVisible = false;
            _announcementHideTimer = 0f;
            if (_announceRoot != null && _announceRoot.activeSelf)
            {
                _announceRoot.SetActive(false);
            }
        }

        static string FinishLabel(MRoundSystem round)
        {
            string winner = WinnerLabel(round.Winner);
            switch (round.FinishType)
            {
                case MFinishType.TimeOver:
                    return "TIME OVER " + winner;
                case MFinishType.TimeDraw:
                    return "TIME OVER DRAW";
                case MFinishType.Ko:
                    return "KO " + winner;
                case MFinishType.DoubleKo:
                    return "DOUBLE KO";
                default:
                    return "";
            }
        }

        static string WinnerLabel(int winner)
        {
            return winner == 0 ? "P1" : (winner == 1 ? "P2" : "DRAW");
        }

        static string LatencyValue(int latencyMs)
        {
            return latencyMs >= 0 ? latencyMs.ToString() + "ms" : "--";
        }

        /*
        static string AnnouncementFor(MRoundSystem round, int roundNo)
        {
            if (round != null && round.FinishType != MFinishType.NotYet)
            {
                string winner = WinnerLabel(round.Winner);
                switch (round.FinishType)
                {
                    case MFinishType.TimeOver:
                        return "时间结束！" + winner + "获胜";
                    case MFinishType.TimeDraw:
                        return "时间结束！平局";
                    case MFinishType.Ko:
                        return "KO！" + winner + "获胜";
                    case MFinishType.DoubleKo:
                        return "KO！平局";
                }
            }
            return RoundLabel(roundNo);
        }

        static string RoundLabel(int roundNo)
        {
            switch (roundNo)
            {
                case 1: return "第一回合";
                case 2: return "第二回合";
                case 3: return "第三回合";
                default: return "第" + roundNo.ToString() + "回合";
            }
        }

        static string WinnerLabel(int winner)
        {
            return winner == 0 ? "玩家一" : (winner == 1 ? "玩家二" : "平局");
        }

        */

        void BindText(Transform target, out Text text, out TMP_Text tmp)
        {
            text = null;
            tmp = null;
            if (target == null) { return; }
            text = target.GetComponent<Text>();
            tmp = target.GetComponent<TMP_Text>();
        }

        void SetText(Text text, TMP_Text tmp, string value)
        {
            if (text != null) { text.text = value; }
            if (tmp != null) { tmp.text = value; }
        }

        static Image FindChildImage(Transform root, string name)
        {
            Transform child = FindChildRecursive(root, name);
            return child != null ? child.GetComponent<Image>() : null;
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

        static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) { return null; }
            if (root.name == name) { return root; }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null) { return found; }
            }
            return null;
        }

        static Image MakeImage(Transform parent, Sprite sprite, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = new GameObject("Img", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return img;
        }

        static Text MakeText(Transform parent, Font font, int fontSize, TextAnchor align,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, string initial)
        {
            GameObject go = new GameObject("Txt", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = Color.white;
            t.text = initial;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rt = t.rectTransform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return t;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Sprite _whiteSprite;

        static Sprite WhiteSprite()
        {
            if (_whiteSprite != null) { return _whiteSprite; }
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return _whiteSprite;
        }
    }
}
