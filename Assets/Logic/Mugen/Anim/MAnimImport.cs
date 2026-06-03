// AIR→引擎动画桥接：把导入管线产出的 Game.Data.AnimData(AirParser 输出) 转为引擎自有 MAnimData。
// 这是新引擎对"共享导入层(Import/Air + Game.Data)"的唯一耦合点（单向、加载期），便于日后随
// AnimData 迁移整体上移。Clsn(ClsnBox)→MClsnBox 字段同构直拷。See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using GameData = Lockstep.Game.Data;
using Lockstep.Mugen.Hit;

namespace Lockstep.Mugen.Anim
{
    /// <summary>AIR 导入数据 → 引擎动画数据的转换器（加载期一次性）。</summary>
    public static class MAnimImport
    {
        /// <summary>转换单段动画并预算节拍量。</summary>
        public static MAnimData FromAir(GameData.AnimData src)
        {
            MAnimData dst = new MAnimData
            {
                No = src.Id,
                LoopStart = src.LoopStart,
            };
            if (src.Frames != null)
            {
                dst.Frames = new MAnimFrame[src.Frames.Length];
                for (int i = 0; i < src.Frames.Length; i++)
                {
                    dst.Frames[i] = FromAirFrame(src.Frames[i]);
                }
            }
            dst.ComputePacing();
            return dst;
        }

        /// <summary>转换整张动画表（动画号→数据）。</summary>
        public static Dictionary<int, MAnimData> FromAirTable(IEnumerable<GameData.AnimData> anims)
        {
            Dictionary<int, MAnimData> table = new Dictionary<int, MAnimData>();
            if (anims != null)
            {
                foreach (GameData.AnimData a in anims)
                {
                    if (a != null)
                    {
                        table[a.Id] = FromAir(a);
                    }
                }
            }
            return table;
        }

        static MAnimFrame FromAirFrame(GameData.AnimFrame f)
        {
            return new MAnimFrame
            {
                SpriteGroup = f.SpriteGroup,
                SpriteImage = f.SpriteImage,
                OffX = f.OffX,
                OffY = f.OffY,
                Time = f.Duration,
                Flip = (int)f.Flip,
                Clsn1 = ConvertClsn(f.Clsn1),
                Clsn2 = ConvertClsn(f.Clsn2),
            };
        }

        static MClsnBox[] ConvertClsn(GameData.ClsnBox[] src)
        {
            if (src == null)
            {
                return null;
            }
            MClsnBox[] dst = new MClsnBox[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                dst[i] = new MClsnBox(src[i].X1, src[i].Y1, src[i].X2, src[i].Y2);
            }
            return dst;
        }
    }
}
