using System.IO;
using System.Runtime.CompilerServices;

namespace Lockstep.Tests
{
    /// <summary>定位仓库外的 MUGEN 素材（../MugenSource/Terrarian）。素材不在时测试自我跳过。</summary>
    internal static class TestAssets
    {
        // 锚定到本文件(TestAssets.cs)所在目录，与调用方在哪个子目录无关
        // （[CallerFilePath] 在此默认参数处由编译器填本文件路径，因为调用点就在本类内部）。
        static string DemoDir([CallerFilePath] string self = "")
        {
            string dir = Path.GetDirectoryName(self);                           // ...\LockstepActDemo\Tests\Lockstep.Logic.Tests
            return Path.GetFullPath(Path.Combine(dir, "..", "..", ".."));       // ...\demo
        }

        public static string TerrarianDir()
        {
            return Path.Combine(DemoDir(), "MugenSource", "Terrarian");
        }

        public static string Air()
        {
            return Path.Combine(TerrarianDir(), "Terrarian.air");
        }

        public static string Sff()
        {
            return Path.Combine(TerrarianDir(), "Terrarian.sff");
        }

        public static string Cmd()
        {
            return Path.Combine(TerrarianDir(), "Terrarian.cmd");
        }

        public static string Common1Cns()
        {
            return Path.Combine(TerrarianDir(), "common1.cns");
        }

        public static string KfmDir()
        {
            return Path.Combine(DemoDir(), "MugenSource", "kfm");
        }

        public static string KfmSff()
        {
            return Path.Combine(KfmDir(), "kfm.sff");
        }
    }
}
