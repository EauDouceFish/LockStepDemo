using System.Text;
using Lockstep.Math;

namespace Lockstep.Core
{
    /// <summary>
    /// 逻辑层配置的运行时形态：全部定点数，由 LogicConfigAsset.ToLogicData() 构造。
    /// 逻辑层（World / System）只读这个，永不接触 float。
    /// </summary>
    public class GameConfigData
    {
        public int LogicTickHz;
        public FFloat MapHalfWidth;
        public FFloat MapHalfHeight;
        public FFloat MoveStepPerFrame;
        public FVector3[] InitialPositions;

        /// <summary>
        /// 导出配置——用于两端比对确定性。
        /// 格式：可读值(raw)，可读值给人看，raw 才是逐位比对的地面真相。
        /// </summary>
        public string DumpRaw()
        {
            var sb = new StringBuilder();
            sb.Append("Hz=").Append(LogicTickHz);
            sb.Append(" halfW=").Append(F(MapHalfWidth));
            sb.Append(" halfH=").Append(F(MapHalfHeight));
            sb.Append(" moveStep=").Append(F(MoveStepPerFrame));
            if (InitialPositions != null)
            {
                for (int i = 0; i < InitialPositions.Length; i++)
                {
                    sb.Append(" spawn").Append(i).Append("=(")
                      .Append(F(InitialPositions[i].X)).Append(',')
                      .Append(F(InitialPositions[i].Y)).Append(')');
                }
            }
            return sb.ToString();
        }

        // 可读值(raw)，例如 13.500(57982058496)
        static string F(FFloat v) => $"{v}({v.Raw})";
    }
}
