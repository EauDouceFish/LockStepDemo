namespace Lockstep.Game.Data
{
    /// <summary>
    /// 搓招输入符号。方向用数字小键盘记法的语义命名；按钮 a/b/c/x/y/z。
    /// 骨架先列基础符号；按住(~)/释放/同时(+) 等修饰后续用 flags 扩展。
    /// </summary>
    public enum InputSymbol : byte
    {
        // 方向
        Neutral = 0,
        Back, DownBack, Down, DownFwd, Fwd, UpFwd, Up, UpBack,
        // 按钮
        BtnA, BtnB, BtnC, BtnX, BtnY, BtnZ, BtnStart,
    }

    /// <summary>一条搓招指令（≈ CMD [Command]）。</summary>
    public sealed class CommandData
    {
        public string Name;
        public InputSymbol[] Motion;   // 例：DownBack? 这里存 Down,DownFwd,Fwd,BtnA = 波动拳
        public int TimeWindow;         // 整个指令完成的最大帧数（CMD time）
        public int BufferTime;         // 成功后保持有效的帧数（CMD buffer.time）
    }
}
