using UnityEngine;
using Lockstep.Core;
using Lockstep.Math;

namespace Lockstep.Game
{
    /// <summary>
    /// 逻辑层配置——唯一真源。所有字段都是整数（毫为单位，1000 = 1.0），
    /// 运行时用纯整数运算转成定点数 FFloat，保证两端结果完全一致。
    /// 严禁在此出现 float / Resources 调用 —— 那会引入不同步。
    /// </summary>
    [CreateAssetMenu(menuName = "Lockstep/LogicConfig", fileName = "LogicConfig")]
    public class LogicConfigAsset : ScriptableObject
    {
        [Header("逻辑帧率（必须与 LockstepClient.LogicTickHz 一致）")]
        public int logicTickHz = 30;

        [Header("移动速度（毫单位/秒，6000 = 6.0/秒）")]
        public int moveSpeedMilliPerSec = 6000;

        [Header("地图半尺寸（毫单位，13500 = 13.5）")]
        public int mapHalfWidthMilli = 13500;
        public int mapHalfHeightMilli = 3000;

        [Header("出生点：玩家0 在 -X，玩家1 在 +X（毫单位）")]
        public int spawnHalfSeparationMilli = 4000;

        const int Milli = 1000;

        /// <summary>纯整数运算 → 定点数。这是确定性的保证点。</summary>
        public GameConfigData ToLogicData()
        {
            var milli = FFloat.FromInt(Milli);
            var hz = FFloat.FromInt(logicTickHz);
            var spawnX = FFloat.FromInt(spawnHalfSeparationMilli) / milli;

            return new GameConfigData
            {
                LogicTickHz = logicTickHz,
                MapHalfWidth = FFloat.FromInt(mapHalfWidthMilli) / milli,
                MapHalfHeight = FFloat.FromInt(mapHalfHeightMilli) / milli,
                MoveStepPerFrame = FFloat.FromInt(moveSpeedMilliPerSec) / milli / hz,
                InitialPositions = new[]
                {
                    new FVector3(-spawnX, FFloat.Zero, FFloat.Zero),
                    new FVector3(spawnX, FFloat.Zero, FFloat.Zero),
                },
            };
        }

        // ↓ 仅供表现层（相机/场景自适应）使用，不参与逻辑，可以用 float。
        public float MapHalfWidthView => mapHalfWidthMilli / (float)Milli;
        public float MapHalfHeightView => mapHalfHeightMilli / (float)Milli;
    }
}
