using UnityEngine;
using Lockstep.Math;

namespace Lockstep.Game.View
{
    /// <summary>
    /// 定点向量 → Unity 向量的转换。只供表现层使用。
    /// 放在表现层程序集，逻辑层（Lockstep.Logic）碰不到 —— 这是有意的。
    /// </summary>
    public static class FMathUnityExt
    {
        public static Vector2 ToUnity(this FVector2 v)
            => new Vector2(v.X.ToFloat(), v.Y.ToFloat());

        public static Vector3 ToUnity3D(this FVector2 v, float z = 0f)
            => new Vector3(v.X.ToFloat(), v.Y.ToFloat(), z);

        public static Vector3 ToUnity(this FVector3 v)
            => new Vector3(v.X.ToFloat(), v.Y.ToFloat(), v.Z.ToFloat());
    }
}
