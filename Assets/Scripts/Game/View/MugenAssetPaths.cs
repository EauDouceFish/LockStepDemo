using System.IO;

namespace Lockstep.View
{
    /// <summary>表现层小工具：在角色素材目录里按通配符找第一个文件。</summary>
    public static class MugenAssetPaths
    {
        public static string FirstFile(string dir, string pattern)
        {
            if (!Directory.Exists(dir))
            {
                return null;
            }
            string[] files = Directory.GetFiles(dir, pattern);
            return files.Length > 0 ? files[0] : null;
        }
    }
}
