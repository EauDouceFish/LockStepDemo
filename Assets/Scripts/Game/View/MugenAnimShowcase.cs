using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Lockstep.Game.Data;
using Lockstep.Import.Air;

namespace Lockstep.View
{
    /// <summary>
    /// 全动作展示：把角色 AIR 里的所有动画号按序逐个循环播放，每个播 SecondsPerAnim 秒后自动切下一个。
    /// 屏幕左上显示当前动画号/进度；←/→ 手动切，空格暂停自动轮播。纯表现层调试工具，不碰逻辑层。
    /// 现可直接对 SFFv1 角色（Terrarian）跑；KFM(SFFv2) 待 SFFv2 读取器后可用。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MugenAnimShowcase : MonoBehaviour
    {
        public string CharacterFolder = "Terrarian";
        public float PixelsPerUnit = 50f;
        public float SecondsPerAnim = 2f;

        List<AnimData> _anims;
        MugenSpriteLoader.Source _source;
        MugenSpriteAnimator _animator;
        int _index;
        float _timer;
        bool _paused;

        void Start()
        {
            string baseDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "MugenSource", CharacterFolder));
            string airPath = MugenAssetPaths.FirstFile(baseDir, "*.air");
            string sffPath = MugenAssetPaths.FirstFile(baseDir, "*.sff");
            if (airPath == null || sffPath == null)
            {
                Debug.LogError("[MUGEN] 找不到 .air/.sff，目录：" + baseDir);
                return;
            }

            _anims = AirParser.ParseFile(airPath);
            _anims.Sort((AnimData a, AnimData b) => a.Id.CompareTo(b.Id));
            if (_anims.Count == 0)
            {
                Debug.LogError("[MUGEN] AIR 里没有动画：" + airPath);
                return;
            }

            Application.runInBackground = true;   // 否则编辑器失焦时 Play 不 tick，动画/轮播会冻结（MCP 截图尤甚）
            _source = MugenSpriteLoader.Open(sffPath, PixelsPerUnit);
            _animator = GetComponent<MugenSpriteAnimator>();
            if (_animator == null)
            {
                _animator = gameObject.AddComponent<MugenSpriteAnimator>();
            }
            Debug.Log(string.Format("[MUGEN] {0} 全动作展示：共 {1} 个动画号，每个播 {2}s（←/→ 切换，空格暂停）",
                CharacterFolder, _anims.Count, SecondsPerAnim));
            ShowCurrent();
        }

        void Update()
        {
            if (_anims == null)
            {
                return;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                _paused = !_paused;
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                Step(1);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Step(-1);
            }
            if (_paused)
            {
                return;
            }
            _timer += Time.deltaTime;
            if (_timer >= SecondsPerAnim)
            {
                Step(1);
            }
        }

        void Step(int delta)
        {
            _index = (_index + delta + _anims.Count) % _anims.Count;
            _timer = 0f;
            ShowCurrent();
        }

        void ShowCurrent()
        {
            AnimData anim = _anims[_index];
            MugenSpriteLoader.BuildForAnim(_source, anim);
            _animator.Play(anim, _source.Cache);
        }

        void OnGUI()
        {
            if (_anims == null)
            {
                return;
            }
            AnimData anim = _anims[_index];
            string text = string.Format("[{0}/{1}]  AnimNo = {2}   帧数 {3}{4}\n←/→ 切换    空格 {5}",
                _index + 1, _anims.Count, anim.Id, anim.Frames.Length,
                anim.Frames.Length == 0 ? "  (空动画)" : string.Empty,
                _paused ? "继续" : "暂停");
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 18;
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(12f, 10f, 600f, 60f), text, style);
        }
    }
}
