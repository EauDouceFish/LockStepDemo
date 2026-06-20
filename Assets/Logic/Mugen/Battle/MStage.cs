// R-STAGE-minimal：决斗场最小建模。当前只做【左右边界】（角色 X 夹取），
// 对齐 Ikemen 舞台 boundleft/boundright + 屏幕边界把角色限制在场内（char.go 位置夹取）。
// 完整舞台（背景层/视差/地面 y/stagevar/cornerpush）后置（见 Docs 路线图 R-STAGE）。
using Lockstep.Math;
using Lockstep.Core;

namespace Lockstep.Mugen.Battle
{
    /// <summary>决斗场（最小）：左右边界。默认关闭（虚空对打，保持旧行为）；决斗场表现层开启并设边界。</summary>
    public sealed class MStage
    {
        /// <summary>是否启用边界夹取。关闭时引擎不夹取（行为同旧）。</summary>
        public bool BoundsEnabled;

        /// <summary>角色 X 世界坐标最小值（MUGEN 单位）。</summary>
        public FFloat BoundLeft = FFloat.FromInt(-200);

        /// <summary>角色 X 世界坐标最大值。</summary>
        public FFloat BoundRight = FFloat.FromInt(200);

        /// <summary>对称设左右边界为 ±half。</summary>
        // Project-specific: demo helper for configuring Ikemen-style boundleft/boundright from one symmetric width.
        public void SetSymmetric(int half)
        {
            BoundLeft = FFloat.FromInt(-half);
            BoundRight = FFloat.FromInt(half);
            BoundsEnabled = true;
        }

        /// <summary>把 X 夹进 [BoundLeft, BoundRight]。返回是否被夹（撞墙）。</summary>
        // Project-specific: minimal direct clamp using Ikemen-style boundleft/boundright names; full camera/corner bounds are not modeled here.
        public bool ClampX(ref FFloat x)
        {
            if (!BoundsEnabled)
            {
                return false;
            }
            if (x < BoundLeft)
            {
                x = BoundLeft;
                return true;
            }
            if (x > BoundRight)
            {
                x = BoundRight;
                return true;
            }
            return false;
        }

        public MStage Clone()
        {
            return new MStage
            {
                BoundsEnabled = BoundsEnabled,
                BoundLeft = BoundLeft,
                BoundRight = BoundRight,
            };
        }

        public void CopyFrom(MStage source)
        {
            if (source == null)
            {
                BoundsEnabled = false;
                BoundLeft = FFloat.FromInt(-200);
                BoundRight = FFloat.FromInt(200);
                return;
            }

            BoundsEnabled = source.BoundsEnabled;
            BoundLeft = source.BoundLeft;
            BoundRight = source.BoundRight;
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddBool(BoundsEnabled);
            hash.AddFixed(BoundLeft);
            hash.AddFixed(BoundRight);
        }
    }
}
