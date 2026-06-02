using UnityEngine;

namespace Lockstep.Game.View
{
    /// <summary>
    /// 表现层配置。只放美术 / 渲染相关字段。
    /// 逻辑相关配置（地图尺寸、移动速度、出生点）一律在 LogicConfigAsset，不要放这里。
    /// </summary>
    [CreateAssetMenu(menuName = "Lockstep/GameConfig", fileName = "GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [Header("玩家显示高度（世界单位）")]
        public float PlayerWorldHeight = 1.5f;

        [Header("Resources/ 下玩家 prefab 路径")]
        public string[] PlayerPrefabPaths =
        {
            "Players/PlayerBlue",
            "Players/PlayerRed",
        };
    }
}
