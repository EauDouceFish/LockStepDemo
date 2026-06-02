using UnityEngine;
using Lockstep.Game.Data;
using Lockstep.Math;

namespace Lockstep.View
{
    /// <summary>
    /// 在场景里画当前动画帧的 Clsn1(红=攻击框) / Clsn2(蓝=受击框)。
    /// 用已测的 <see cref="ClsnWorld"/> 计算世界 AABB。挂在与 MugenSpriteAnimator 同一物体上。
    /// MUGEN 高度轴上为负，这里画 gizmo 时翻 Y 让框显示在角色上方（仅调试可视，非逻辑）。
    /// </summary>
    [RequireComponent(typeof(MugenSpriteAnimator))]
    public sealed class ClsnGizmo : MonoBehaviour
    {
        public bool Draw = true;

        MugenSpriteAnimator _animator;

        void Awake()
        {
            _animator = GetComponent<MugenSpriteAnimator>();
        }

        void OnDrawGizmos()
        {
            if (!Draw)
            {
                return;
            }
            if (_animator == null)
            {
                _animator = GetComponent<MugenSpriteAnimator>();
            }
            AnimFrame frame = _animator != null ? _animator.CurrentDisplayFrame : null;
            if (frame == null)
            {
                return;
            }

            int facing = _animator.Facing;
            DrawBoxes(frame.Clsn2, new Color(0.2f, 0.5f, 1f), facing);   // 受击框：蓝
            DrawBoxes(frame.Clsn1, new Color(1f, 0.2f, 0.2f), facing);   // 攻击框：红
        }

        void DrawBoxes(ClsnBox[] boxes, Color color, int facing)
        {
            if (boxes == null)
            {
                return;
            }
            Gizmos.color = color;
            FFloat originX = FFloat.Zero;
            FFloat originY = FFloat.Zero;
            FFloat facingF = facing < 0 ? FFloat.MinusOne : FFloat.One;
            Vector3 basePos = transform.position;

            for (int i = 0; i < boxes.Length; i++)
            {
                RectAabb rect = ClsnWorld.ToWorld(boxes[i], originX, originY, facingF);
                float minX = rect.MinX.ToFloat();
                float maxX = rect.MaxX.ToFloat();
                // MUGEN 高度上为负 → 翻成 Unity 向上为正
                float lowY = -rect.MaxY.ToFloat();
                float highY = -rect.MinY.ToFloat();

                Vector3 center = basePos + new Vector3((minX + maxX) * 0.5f, (lowY + highY) * 0.5f, 0f);
                Vector3 size = new Vector3(Mathf.Abs(maxX - minX), Mathf.Abs(highY - lowY), 0.01f);
                Gizmos.DrawWireCube(center, size);
            }
        }
    }
}
