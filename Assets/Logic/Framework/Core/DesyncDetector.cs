using System.Collections.Generic;
using Lockstep.Logging;

namespace Lockstep.Core
{
    /// <summary>
    /// 不同步检测器（S3 的调试核武器）。两端各自每逻辑帧上报 (frame, worldHash)；
    /// 检测器在"同一帧号"上配对两端哈希，一旦不一致立刻报出帧号 + 双端值，
    /// 把"哪一帧开始分叉"精确钉死，省去靠肉眼比对状态的时间。
    ///
    /// 纯逻辑、不碰 Unity。当前用于 loopback（同进程双 World）；
    /// 将来服务器对账（S20 反作弊）也能复用同一套 Report 接口。
    /// </summary>
    public sealed class DesyncDetector
    {
        struct PendingFrame
        {
            public int ReporterId;
            public ulong Hash;
        }

        // 已上报、还在等另一端配对的帧。配对成功即移除，内存不随帧数增长。
        readonly Dictionary<int, PendingFrame> _pending = new Dictionary<int, PendingFrame>();

        public int CheckedFrames { get; private set; }
        public int DesyncFrames { get; private set; }
        public int FirstDesyncFrame { get; private set; } = -1;

        /// <summary>
        /// 上报某端在某帧的 World 哈希。返回 false 表示该帧检测到不同步。
        /// 第一个到达的端先登记，第二个到达时配对比较并清除该帧记录。
        /// </summary>
        public bool Report(int reporterId, int frame, ulong hash)
        {
            if (_pending.TryGetValue(frame, out PendingFrame waiting))
            {
                _pending.Remove(frame);
                CheckedFrames++;
                if (waiting.Hash != hash)
                {
                    DesyncFrames++;
                    if (FirstDesyncFrame < 0)
                    {
                        FirstDesyncFrame = frame;
                    }
                    LLog.Error(string.Format(
                        "[Desync] 第 {0} 帧分叉: 端{1}=0x{2:X16} vs 端{3}=0x{4:X16}",
                        frame, waiting.ReporterId, waiting.Hash, reporterId, hash));
                    return false;
                }
                return true;
            }

            _pending[frame] = new PendingFrame { ReporterId = reporterId, Hash = hash };
            return true;
        }

        /// <summary>给周期性日志用的一句话摘要。</summary>
        public string Summary()
        {
            if (DesyncFrames == 0)
            {
                return string.Format("[Desync] OK：已比对 {0} 帧，无分叉", CheckedFrames);
            }
            return string.Format("[Desync] 发现 {0}/{1} 帧分叉，首次分叉于第 {2} 帧",
                DesyncFrames, CheckedFrames, FirstDesyncFrame);
        }
    }
}
