using System;

namespace Lockstep.Logging
{
    /// <summary>
    /// 逻辑层日志出口。逻辑层（World / System / States）不能直接调 UnityEngine.Debug——
    /// 那会把逻辑代码绑死到 Unity，也无法在无头服务器上运行。
    /// 逻辑层只调 LLog；由表现层（Bootstrap）在启动时把 Sink 接到 UnityEngine.Debug.Log。
    /// 这就是桥接模式：逻辑层只依赖抽象，具体实现由外层注入。
    /// </summary>
    public static class LLog
    {
        public static Action<string> Sink;
        public static Action<string> WarnSink;
        public static Action<string> ErrorSink;

        public static void Log(string msg) => Sink?.Invoke(msg);
        public static void Warn(string msg) => (WarnSink ?? Sink)?.Invoke(msg);
        public static void Error(string msg) => (ErrorSink ?? Sink)?.Invoke(msg);
    }
}
