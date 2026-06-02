using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Lockstep.Game.Data;
using Lockstep.Import.Air;

namespace Lockstep.View
{
    /// <summary>
    /// 多角色画廊：扫描 ../MugenSource/ 下所有传统角色包（含 .sff+.air），一页并排展示 PerPage 个角色，
    /// 各自循环播放站立动画并自动统一缩放到同高；OnGUI 左右按钮（或 ←/→ 键）翻页。
    /// 懒加载：只加载当前页的角色，翻页时销毁旧的、建新的（避免一次性载入 55MB 的 Terrarian 等）。
    /// 纯表现层调试工具。v1/v2 角色皆可（由 MugenSpriteLoader 自动识别）。
    /// </summary>
    public sealed class MugenGalleryShowcase : MonoBehaviour
    {
        public int PerPage = 4;
        public float Spacing = 3.4f;
        public float TargetHeight = 3.2f;     // 统一把站立精灵缩放到这个世界高度
        public float PixelsPerUnit = 50f;

        readonly List<string> _characterFolders = new List<string>();
        readonly List<GameObject> _spawned = new List<GameObject>();
        readonly List<string> _spawnedNames = new List<string>();
        int _page;

        void Start()
        {
            Application.runInBackground = true;   // 编辑器失焦也持续播放（MCP 截图需要）
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "MugenSource"));
            ScanCharacters(root);
            if (_characterFolders.Count == 0)
            {
                Debug.LogError("[MUGEN] " + root + " 下没找到角色包");
                return;
            }
            SetupCamera();
            Debug.Log(string.Format("[MUGEN] 角色画廊：共 {0} 个角色，{1} 个/页，{2} 页（←/→ 或点按钮翻页）",
                _characterFolders.Count, PerPage, PageCount()));
            ShowPage();
        }

        void ScanCharacters(string root)
        {
            if (!Directory.Exists(root))
            {
                return;
            }
            string[] dirs = Directory.GetDirectories(root);
            System.Array.Sort(dirs);
            foreach (string dir in dirs)
            {
                string name = Path.GetFileName(dir);
                if (name.StartsWith("_"))
                {
                    continue;   // _reference / _downloads
                }
                if (MugenAssetPaths.FirstFile(dir, "*.sff") != null && MugenAssetPaths.FirstFile(dir, "*.air") != null)
                {
                    _characterFolders.Add(dir);
                }
            }
        }

        int PageCount()
        {
            return (_characterFolders.Count + PerPage - 1) / PerPage;
        }

        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                Turn(1);
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                Turn(-1);
            }
        }

        void Turn(int delta)
        {
            int count = PageCount();
            _page = (_page + delta + count) % count;
            ShowPage();
        }

        void ShowPage()
        {
            foreach (GameObject go in _spawned)
            {
                Destroy(go);
            }
            _spawned.Clear();
            _spawnedNames.Clear();

            int start = _page * PerPage;
            for (int slot = 0; slot < PerPage; slot++)
            {
                int index = start + slot;
                if (index >= _characterFolders.Count)
                {
                    break;
                }
                SpawnCharacter(_characterFolders[index], slot);
            }
        }

        void SpawnCharacter(string folder, int slot)
        {
            string airPath = MugenDef.AnimPath(folder);
            string sffPath = MugenDef.SpritePath(folder);
            string name = Path.GetFileName(folder);

            Dictionary<int, AnimData> anims = AirParser.ToDictionary(AirParser.ParseFile(airPath));
            AnimData standing = PickStandingAnim(anims);
            if (standing == null)
            {
                return;
            }

            Dictionary<long, Sprite> sprites = MugenSpriteLoader.Load(sffPath, new[] { standing }, PixelsPerUnit);

            GameObject go = new GameObject(name);
            float x = (slot - (PerPage - 1) / 2f) * Spacing;
            go.transform.position = new Vector3(x, 0f, 0f);
            go.AddComponent<SpriteRenderer>();
            MugenSpriteAnimator animator = go.AddComponent<MugenSpriteAnimator>();
            animator.Play(standing, sprites);

            ScaleToTargetHeight(go);

            _spawned.Add(go);
            _spawnedNames.Add(name);
        }

        static AnimData PickStandingAnim(Dictionary<int, AnimData> anims)
        {
            // 站立 = 动画号 0（MUGEN 约定）；没有就退而求其次取号最小的非空动画
            if (anims.TryGetValue(0, out AnimData zero) && zero.Frames.Length > 0)
            {
                return zero;
            }
            AnimData best = null;
            foreach (KeyValuePair<int, AnimData> pair in anims)
            {
                if (pair.Value.Frames.Length == 0)
                {
                    continue;
                }
                if (best == null || pair.Key < best.Id)
                {
                    best = pair.Value;
                }
            }
            return best;
        }

        void ScaleToTargetHeight(GameObject go)
        {
            SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
            if (renderer.sprite == null)
            {
                return;
            }
            float spriteHeight = renderer.sprite.bounds.size.y;   // 世界单位高（rect/ppu）
            if (spriteHeight <= 0.0001f)
            {
                return;
            }
            float scale = TargetHeight / spriteHeight;
            go.transform.localScale = new Vector3(scale, scale, 1f);
        }

        void SetupCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindObjectOfType<Camera>();
            }
            if (camera == null)
            {
                return;
            }
            camera.orthographic = true;
            float rowHalfWidth = (PerPage - 1) / 2f * Spacing + Spacing * 0.6f;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float sizeForWidth = rowHalfWidth / Mathf.Max(0.1f, aspect);
            float sizeForHeight = TargetHeight * 0.5f + 0.6f;
            camera.orthographicSize = Mathf.Max(sizeForWidth, sizeForHeight);
            camera.transform.position = new Vector3(0f, TargetHeight * 0.5f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        }

        void OnGUI()
        {
            if (_characterFolders.Count == 0)
            {
                return;
            }

            GUIStyle title = new GUIStyle(GUI.skin.label);
            title.fontSize = 18;
            title.normal.textColor = Color.white;
            GUI.Label(new Rect(12f, 8f, 500f, 28f),
                string.Format("MUGEN 角色画廊  第 {0}/{1} 页  （共 {2} 个角色）", _page + 1, PageCount(), _characterFolders.Count), title);

            if (GUI.Button(new Rect(12f, 40f, 90f, 34f), "← 上一页"))
            {
                Turn(-1);
            }
            if (GUI.Button(new Rect(110f, 40f, 90f, 34f), "下一页 →"))
            {
                Turn(1);
            }

            // 每个角色名字标在其屏幕位置下方
            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }
            GUIStyle nameStyle = new GUIStyle(GUI.skin.label);
            nameStyle.fontSize = 15;
            nameStyle.alignment = TextAnchor.MiddleCenter;
            nameStyle.normal.textColor = Color.white;
            for (int i = 0; i < _spawned.Count; i++)
            {
                Vector3 screen = camera.WorldToScreenPoint(_spawned[i].transform.position);
                GUI.Label(new Rect(screen.x - 70f, Screen.height - screen.y + 6f, 140f, 24f), _spawnedNames[i], nameStyle);
            }
        }
    }
}
