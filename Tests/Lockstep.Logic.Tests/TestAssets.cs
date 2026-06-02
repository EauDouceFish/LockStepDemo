using System.IO;
using System.Runtime.CompilerServices;

namespace Lockstep.Tests
{
    /// <summary>定位仓库外的 MUGEN 素材（../MugenSource/Terrarian）。素材不在时测试自我跳过。</summary>
    internal static class TestAssets
    {
        public static string TerrarianDir([CallerFilePath] string thisFile = "")
        {
            // thisFile = ...\LockstepActDemo\Tests\Lockstep.Logic.Tests\TestAssets.cs
            string dir = Path.GetDirectoryName(thisFile);                       // ...\Tests\Lockstep.Logic.Tests
            string repo = Path.GetFullPath(Path.Combine(dir, "..", ".."));      // ...\LockstepActDemo
            string demo = Path.GetFullPath(Path.Combine(repo, ".."));           // ...\demo
            return Path.Combine(demo, "MugenSource", "Terrarian");
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

        public static string KfmDir([CallerFilePath] string thisFile = "")
        {
            string dir = Path.GetDirectoryName(thisFile);
            string repo = Path.GetFullPath(Path.Combine(dir, "..", ".."));
            string demo = Path.GetFullPath(Path.Combine(repo, ".."));
            return Path.Combine(demo, "MugenSource", "kfm");
        }

        public static string KfmSff()
        {
            return Path.Combine(KfmDir(), "kfm.sff");
        }
    }
}
