using UnityEngine;
using Lockstep.Core;
using Lockstep.Game.Components;
using Lockstep.Game.States;

namespace Lockstep.Game.View
{
    /// <summary>
    /// 表现层：每帧把逻辑层的 TransformC + StateMachineC 同步到 Unity Transform / Animator。
    /// 注意：本组件**只读**逻辑层数据，不反向影响逻辑（否则破坏确定性）。
    ///
    /// 坐标映射（火影式伪 3D，根/Visual 分离）：
    ///   根 GO     位置 = (Pos.X, Pos.Y, 0)                       ← 地面投影点；Base/shadow 贴这里
    ///   Visual    localPos = prefab baseline + (0, Pos.Z, 0)     ← 跳跃高度叠加在 prefab 调好的 baseline 上
    ///   sortingOrder ∝ -Pos.Y（Y 越大越靠后画，**用逻辑 Y 不含 Z**）
    ///
    /// 动画映射：
    ///   Attack → Animator state name = ActiveMoveC.Id.ToString()（Jab/Punch/Kick/JumpKick/DiveKick）
    ///   其他 → Animator state name = PlayerStateId.ToString()（Idle/Walk/Jump/Hurt/KO）
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        public const int BaseSortingOrder = 1000;

        public SpriteRenderer Body;
        public Animator Animator;
        public Entity Bound { get; set; }

        string _lastAnimState;
        Vector3 _bodyBaseLocalPos;

        void Awake()
        {
            if (Body != null)
            {
                _bodyBaseLocalPos = Body.transform.localPosition;
            }
        }

        void LateUpdate()
        {
            if (Bound == null)
            {
                return;
            }
            TransformC transformC = Bound.Get<TransformC>();
            if (transformC == null)
            {
                return;
            }

            float x = transformC.Pos.X.ToFloat();
            float y = transformC.Pos.Y.ToFloat();
            float z = transformC.Pos.Z.ToFloat();

            transform.position = new Vector3(x, y, 0f);

            if (Body != null)
            {
                Body.transform.localPosition = new Vector3(
                    _bodyBaseLocalPos.x,
                    _bodyBaseLocalPos.y + z,
                    _bodyBaseLocalPos.z);
                Body.sortingOrder = BaseSortingOrder - (int)(y * 100f);
                Body.flipX = transformC.FacingX.ToFloat() < 0f;
            }

            UpdateAnimation();
        }

        void UpdateAnimation()
        {
            if (Animator == null)
            {
                return;
            }
            StateMachineC sm = Bound.Get<StateMachineC>();
            if (sm == null)
            {
                return;
            }

            string animState;
            if (sm.Current == PlayerStateId.Attack)
            {
                ActiveMoveC active = Bound.Get<ActiveMoveC>();
                animState = (active != null) ? active.Id.ToString() : PlayerStateId.Idle.ToString();
            }
            else
            {
                animState = sm.Current.ToString();
            }

            if (animState == _lastAnimState)
            {
                return;
            }
            _lastAnimState = animState;
            Animator.Play(animState, 0, 0f);
        }
    }
}
