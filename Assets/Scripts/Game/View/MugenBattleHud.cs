using UnityEngine;
using UnityEngine.UI;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;

namespace Lockstep.View
{
    /// <summary>
    /// 1v1 战斗 HUD（程序化构建，无需手工 prefab）：左右双层血条（参考 Super Nova 的 realHealth 即时层 + 白条缓降层）、
    /// 中央倒计时、左右回合胜利点、中央回合提示（Round N / Fight! / K.O. / Winner）。
    /// 纯表现：每帧从 MRoundSystem + MChar 读数。逻辑层不知道它存在。
    /// </summary>
    public sealed class MugenBattleHud : MonoBehaviour
    {
        Image _p1Real;
        Image _p1Damage;
        Image _p2Real;
        Image _p2Damage;
        Text _timerText;
        Text _announceText;
        readonly Image[][] _pips = new Image[2][];   // [player][roundIndex] 胜利点

        float _p1DamageFill = 1f;
        float _p2DamageFill = 1f;
        const float DamageFadeSpeed = 0.6f;   // 白条每秒缓降比例

        static readonly Color BackColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        static readonly Color DamageColor = new Color(0.95f, 0.85f, 0.2f, 1f);   // 黄：最近掉血缓降
        static readonly Color HealthColor = new Color(0.85f, 0.15f, 0.15f, 1f);  // 红：当前血
        static readonly Color PipOn = new Color(1f, 0.85f, 0.2f, 1f);
        static readonly Color PipOff = new Color(0.25f, 0.25f, 0.25f, 0.8f);

        public static MugenBattleHud Create(int roundsToWin)
        {
            GameObject canvasGo = new GameObject("BattleHud");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            canvasGo.AddComponent<GraphicRaycaster>();

            MugenBattleHud hud = canvasGo.AddComponent<MugenBattleHud>();
            hud.Build(canvas, roundsToWin);
            return hud;
        }

        void Build(Canvas canvas, int roundsToWin)
        {
            Sprite px = WhiteSprite();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }

            // 左血条（P1）：从中心向左缩（fillOrigin 右）。右血条（P2）镜像。
            BuildBar(canvas.transform, px, true, new Vector2(0f, 1f), new Vector2(20f, -20f), out _p1Damage, out _p1Real);
            BuildBar(canvas.transform, px, false, new Vector2(1f, 1f), new Vector2(-20f, -20f), out _p2Damage, out _p2Real);

            _timerText = MakeText(canvas.transform, font, 40, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(160f, 60f), "99");
            _announceText = MakeText(canvas.transform, font, 64, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(900f, 120f), "");

            // 回合胜利点：每侧 roundsToWin 个。
            for (int p = 0; p < 2; p++)
            {
                _pips[p] = new Image[roundsToWin];
                bool left = p == 0;
                Vector2 anchor = left ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
                for (int r = 0; r < roundsToWin; r++)
                {
                    float dir = left ? 1f : -1f;
                    Vector2 pos = new Vector2(dir * (24f + r * 28f), -64f);
                    Image pip = MakeImage(canvas.transform, px, anchor, pos, new Vector2(18f, 18f));
                    pip.color = PipOff;
                    _pips[p][r] = pip;
                }
            }
        }

        void BuildBar(Transform parent, Sprite px, bool left, Vector2 anchor, Vector2 pos,
            out Image damage, out Image real)
        {
            Vector2 size = new Vector2(480f, 32f);
            Image back = MakeImage(parent, px, anchor, pos, size);
            back.color = BackColor;
            SetPivot(back.rectTransform, left ? new Vector2(0f, 1f) : new Vector2(1f, 1f));

            damage = MakeImage(back.transform, px, new Vector2(0f, 0.5f), Vector2.zero, size);
            ConfigureFill(damage, left);
            damage.color = DamageColor;
            StretchFull(damage.rectTransform);

            real = MakeImage(back.transform, px, new Vector2(0f, 0.5f), Vector2.zero, size);
            ConfigureFill(real, left);
            real.color = HealthColor;
            StretchFull(real.rectTransform);
        }

        static void ConfigureFill(Image img, bool left)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            // P1（左）血从右向左减 → origin 左、填满后从右端掉；这里 origin 设为内侧（左侧 origin=Left, 右侧 origin=Right）。
            img.fillOrigin = left ? (int)Image.OriginHorizontal.Left : (int)Image.OriginHorizontal.Right;
            img.fillAmount = 1f;
        }

        /// <summary>每帧更新 HUD。</summary>
        public void UpdateHud(MRoundSystem round, MChar p1, MChar p2, float deltaTime)
        {
            if (round == null || p1 == null || p2 == null) { return; }

            UpdateBar(_p1Real, ref _p1DamageFill, _p1Damage, p1.Life, p1.LifeMax, deltaTime);
            UpdateBar(_p2Real, ref _p2DamageFill, _p2Damage, p2.Life, p2.LifeMax, deltaTime);

            if (_timerText != null)
            {
                int secs = round.TimerSeconds;
                _timerText.text = secs < 0 ? "--" : secs.ToString();
            }

            for (int p = 0; p < 2; p++)
            {
                int won = p < round.RoundsWon.Length ? round.RoundsWon[p] : 0;
                for (int r = 0; r < _pips[p].Length; r++)
                {
                    _pips[p][r].color = r < won ? PipOn : PipOff;
                }
            }

            if (_announceText != null)
            {
                _announceText.text = AnnouncementFor(round);
            }
        }

        static void UpdateBar(Image real, ref float damageFill, Image damage, int life, int lifeMax, float deltaTime)
        {
            float percent = lifeMax > 0 ? Mathf.Clamp01((float)life / lifeMax) : 0f;
            if (real != null) { real.fillAmount = percent; }
            // 白/黄条缓降到当前血量（参考 Super Nova WhiteHealthFade）。
            if (damageFill < percent) { damageFill = percent; }
            else if (damageFill > percent) { damageFill = Mathf.Max(percent, damageFill - DamageFadeSpeed * deltaTime); }
            if (damage != null) { damage.fillAmount = damageFill; }
        }

        static string AnnouncementFor(MRoundSystem round)
        {
            switch (round.State)
            {
                case MRoundState.Intro:
                    return "Round " + round.RoundNo;
                case MRoundState.Fight:
                    // Fight! 仅在开打头 ~1s 显示。
                    return round.StateTimer < 45 ? "Fight!" : "";
                case MRoundState.PreOver:
                    return "K.O.";
                case MRoundState.Over:
                    if (round.MatchOver)
                    {
                        return round.MatchWinner == 0 ? "Player 1 Wins" : (round.MatchWinner == 1 ? "Player 2 Wins" : "Draw");
                    }
                    return round.Winner == 0 ? "Player 1" : (round.Winner == 1 ? "Player 2" : "Draw");
                default:
                    return "";
            }
        }

        // ── UI 构建工具 ──
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

        static void SetPivot(RectTransform rt, Vector2 pivot)
        {
            rt.pivot = pivot;
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
