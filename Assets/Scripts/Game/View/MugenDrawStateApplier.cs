using UnityEngine;
using Lockstep.Mugen.Char;

namespace Lockstep.View
{
    /// <summary>
    /// 表现层：把逻辑层 MChar 的绘制态（Batch A 的 anglerot/scale/trans/alpha/sprpriority/offset）
    /// 施加到 Unity SpriteRenderer。逻辑层只产出确定性绘制参数，这里是唯一的定点→渲染边界。
    ///
    /// 已接：偏移、旋转(AngleDraw 标志)、AngleDraw 缩放、透明度近似(alpha src)、绘制顺序(sprPriority/layerNo)。
    /// 留待调试会话完善：真正的加法/减法混合(需自定义材质)、调色板 RemapPal/PalFX(需 palette 重建)。
    /// </summary>
    public static class MugenDrawStateApplier
    {
        public static void Apply(MChar c, SpriteRenderer renderer, Transform t, float pixelsPerUnit, int baseSortingOrder)
        {
            if (c == null || renderer == null || t == null)
            {
                return;
            }

            // 绘制 offset 叠加到已设的基准位置（MUGEN Y 向下为正 → Unity 取负）。每帧基准被重设，offset 不累积。
            Vector3 pos = t.localPosition;
            pos.x += c.OffsetX.ToFloat() / pixelsPerUnit;
            pos.y += -c.OffsetY.ToFloat() / pixelsPerUnit;
            t.localPosition = pos;

            // 旋转：仅当 AngleDraw 标志置位（对照 char.go CSF_angledraw）。facing<0 翻转 Z 角符号（char.go:12553）。
            if (c.AngleDraw)
            {
                float sign = c.Facing.Raw < 0 ? -1f : 1f;
                t.localRotation = Quaternion.Euler(0f, 0f, c.AngleRot.ToFloat() * sign);
            }
            else
            {
                t.localRotation = Quaternion.identity;
            }

            // AngleDraw 缩放（默认 1,1，每帧由逻辑层重置）。
            t.localScale = new Vector3(c.AngleDrawScaleX.ToFloat(), c.AngleDrawScaleY.ToFloat(), 1f);

            // 透明度：Default/None 不透明；Add/Sub 暂用 alpha src 近似（真正混合模式需自定义材质，留待调试会话）。
            Color col = renderer.color;
            col.a = (c.Trans == MTransType.Default || c.Trans == MTransType.None)
                ? 1f
                : Mathf.Clamp01(c.AlphaSrc / 255f);
            renderer.color = col;

            // 绘制顺序：base + layerNo*1000 + sprPriority（高 sprPriority 靠前）。
            renderer.sortingOrder = baseSortingOrder + c.LayerNo * 1000 + c.SprPriority;
        }
    }
}
