using System.Text;
using Lockstep.Game;
using Lockstep.Input;
using Lockstep.Logging;
using Lockstep.Math;

namespace Lockstep.Core
{
    /// <summary>
    /// S2 自检：验证 World.Snapshot/Restore 正确，且回滚后"重算"是确定性的。
    /// 这是回滚框架的地基测试 —— 纯逻辑，住在 Lockstep.Logic 程序集里，不碰 Unity。
    /// </summary>
    public static class SnapshotSelfTest
    {
        const int WarmupFrames = 25;   // 先跑到第 25 帧再快照
        const int ProbeFrames = 15;    // 快照后再跑 15 帧

        public static string Run()
        {
            // 自检期间静音逻辑层日志，避免战斗日志刷屏
            var savedSink = LLog.Sink;
            LLog.Sink = null;
            try
            {
                return RunInner();
            }
            finally
            {
                LLog.Sink = savedSink;
            }
        }

        static string RunInner()
        {
            var sb = new StringBuilder();
            sb.AppendLine("===== [S2] World 快照/还原 自检 =====");

            var w = BuildWorld();

            // 1. 跑到第 N 帧，记指纹 + 哈希
            for (int f = 0; f < WarmupFrames; f++) Step(w, f);
            string fpN = w.Fingerprint();
            ulong hashN = w.ComputeHash();
            sb.Append("第 ").Append(WarmupFrames).Append(" 帧: ").AppendLine(fpN);

            // 2. 快照
            var snap = w.Snapshot();

            // 3. 再跑 ProbeFrames 帧
            for (int f = WarmupFrames; f < WarmupFrames + ProbeFrames; f++) Step(w, f);
            string fpAfter = w.Fingerprint();
            ulong hashAfter = w.ComputeHash();
            sb.Append("第 ").Append(WarmupFrames + ProbeFrames).Append(" 帧: ").AppendLine(fpAfter);

            // 4. 还原 —— 应当精确回到第 N 帧
            w.Restore(snap);
            string fpRestored = w.Fingerprint();
            ulong hashRestored = w.ComputeHash();
            bool restoreOk = fpRestored == fpN;

            // 5. 用同样的输入重算 —— 应当得到和第一次一模一样的结果
            for (int f = WarmupFrames; f < WarmupFrames + ProbeFrames; f++) Step(w, f);
            string fpResim = w.Fingerprint();
            ulong hashResim = w.ComputeHash();
            bool resimOk = fpResim == fpAfter;

            // 6. 再还原一次 —— 验证快照可反复使用、未被污染
            w.Restore(snap);
            bool reusableOk = w.Fingerprint() == fpN;

            // 7. 哈希路径独立校验：还原 / 重算后哈希要复现，且 N 帧与 N+probe 帧哈希必须不同
            //    （后者防止"哈希忽略状态变化"这种退化实现蒙混过关）
            bool hashRestoreOk = hashRestored == hashN;
            bool hashResimOk = hashResim == hashAfter;
            bool hashSensitiveOk = hashN != hashAfter;
            bool hashOk = hashRestoreOk && hashResimOk && hashSensitiveOk;

            sb.Append("[1] 还原回第 N 帧 : ").AppendLine(restoreOk ? "PASS" : "FAIL <<<");
            sb.Append("[2] 重算结果一致   : ").AppendLine(resimOk ? "PASS" : "FAIL <<<");
            sb.Append("[3] 快照可复用     : ").AppendLine(reusableOk ? "PASS" : "FAIL <<<");
            sb.Append("[4] 哈希确定且敏感 : ").AppendLine(hashOk ? "PASS" : "FAIL <<<");
            if (!restoreOk)
            {
                sb.Append("    期望 ").AppendLine(fpN);
                sb.Append("    实际 ").AppendLine(fpRestored);
            }
            if (!resimOk)
            {
                sb.Append("    首次 ").AppendLine(fpAfter);
                sb.Append("    重算 ").AppendLine(fpResim);
            }
            if (!hashOk)
            {
                sb.Append("    hashN=0x").Append(hashN.ToString("X16"))
                  .Append(" restored=0x").Append(hashRestored.ToString("X16"))
                  .Append(" after=0x").Append(hashAfter.ToString("X16"))
                  .Append(" resim=0x").AppendLine(hashResim.ToString("X16"));
            }
            sb.Append("===== 自检: ")
              .Append(restoreOk && resimOk && reusableOk && hashOk ? "全部 PASS" : "存在 FAIL")
              .Append(" =====");
            return sb.ToString();
        }

        static World BuildWorld()
        {
            var w = new World();
            w.Init(0xC0FFEE);
            w.Config = MakeConfig();
            new BattleGameLogic().Build(w, 2);
            return w;
        }

        static void Step(World w, int frame)
        {
            w.CurrentInputs = InputAt(frame);
            w.Tick();
        }

        // 脚本化输入：玩家0 先右移后出 Jab（轻拳），玩家1 静止 —— 让状态真的发生变化
        static FrameInput[] InputAt(int frame)
        {
            FrameInput p0 = new FrameInput();
            if (frame < 18)
            {
                p0.MoveX = 1;
            }
            else
            {
                p0.Buttons = (byte)InputButton.LightPunch;
            }
            return new FrameInput[] { p0, new FrameInput() };
        }

        static GameConfigData MakeConfig() => new GameConfigData
        {
            LogicTickHz = 30,
            MapHalfWidth = FFloat.FromInt(13),
            MapHalfHeight = FFloat.FromInt(3),
            MoveStepPerFrame = FFloat.One / FFloat.FromInt(5),
            InitialPositions = new[]
            {
                new FVector3(-FFloat.FromInt(4), FFloat.Zero, FFloat.Zero),
                new FVector3(FFloat.FromInt(4), FFloat.Zero, FFloat.Zero),
            },
        };
    }
}
