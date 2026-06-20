using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Lockstep.Game.Data;
using Lockstep.Import.Air;

namespace Lockstep.View
{
    /// <summary>
    /// 运行期 MUGEN 角色演示加载器：解析 AIR + 运行期解码 SFF → 驱动 MugenSpriteAnimator 循环播一段动画。
    /// 角色无关——把任意 SFF v1 角色放到 ../MugenSource/&lt;folder&gt;/ 即可（Terrarian 现成，KFM 放进去同样可用）。
    /// 精灵构建委托给 MugenSpriteLoader（已被 dotnet test 验证的解码核心）；本类是表现层薄壳。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MugenCharacterView : MonoBehaviour
    {
        public string CharacterFolder = "Terrarian";
        public int AnimNo = 0;
        public float PixelsPerUnit = 50f;

        void Start()
        {
            string baseDir = Path.Combine(MugenAssetPaths.MugenRoot(), CharacterFolder);
            string airPath = MugenDef.AnimPath(baseDir);
            string sffPath = MugenDef.SpritePath(baseDir);
            if (airPath == null || sffPath == null)
            {
                Debug.LogError("[MUGEN] 找不到 .air/.sff，目录：" + baseDir);
                return;
            }

            Dictionary<int, AnimData> anims = AirParser.ToDictionary(AirParser.ParseFile(airPath));
            if (!anims.TryGetValue(AnimNo, out AnimData anim))
            {
                Debug.LogError("[MUGEN] 角色没有动画号 " + AnimNo);
                return;
            }

            Application.runInBackground = true;   // 否则编辑器失焦时 Play 不 tick，动画会冻结（MCP 截图尤甚）
            Dictionary<long, Sprite> sprites = MugenSpriteLoader.Load(sffPath, new[] { anim }, PixelsPerUnit);

            MugenSpriteAnimator animator = GetComponent<MugenSpriteAnimator>();
            if (animator == null)
            {
                animator = gameObject.AddComponent<MugenSpriteAnimator>();
            }
            animator.Play(anim, sprites);
            Debug.Log(string.Format("[MUGEN] {0} 动画 {1}：{2} 帧 / {3} 张精灵 已加载",
                CharacterFolder, AnimNo, anim.Frames.Length, sprites.Count));
        }
    }
}
