using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace Lockstep.Editor
{
    public static class MugenServerBuild
    {
        const string ServerScene = "Assets/Scenes/MugenRelayServer.unity";
        const string DefaultBuildPath = "Build/MugenServerLinux/MugenServer.x86_64";
        const string DefaultPlayerBuildPath = "Build/AutoTest/LockstepActDemo.exe";
        const string DefaultAndroidBuildPath = "Build/Android/LockstepActDemo.apk";

        public static void BuildLinuxServer()
        {
            string buildPath = ArgValue("-mugenBuildPath") ?? DefaultBuildPath;
            string fullBuildPath = Path.GetFullPath(buildPath);
            string directory = Path.GetDirectoryName(fullBuildPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new[] { ServerScene },
                locationPathName = fullBuildPath,
                target = BuildTarget.StandaloneLinux64,
                options = BuildOptions.EnableHeadlessMode,
            };

#if UNITY_2021_2_OR_NEWER
            options.subtarget = (int)StandaloneBuildSubtarget.Server;
#endif

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("MUGEN server build failed: " + report.summary.result);
            }

            UnityEngine.Debug.Log("MUGEN server build written to " + fullBuildPath);
        }

        public static void BuildWindowsPlayer()
        {
            string buildPath = ArgValue("-mugenBuildPath") ?? DefaultPlayerBuildPath;
            string fullBuildPath = Path.GetFullPath(buildPath);
            string directory = Path.GetDirectoryName(fullBuildPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string[] scenes = EnabledBuildScenes();
            if (scenes.Length == 0)
            {
                throw new Exception("No enabled scenes in EditorBuildSettings.");
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullBuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("MUGEN player build failed: " + report.summary.result);
            }

            UnityEngine.Debug.Log("MUGEN player build written to " + fullBuildPath);
        }

        public static void BuildAndroidPlayer()
        {
            string buildPath = ArgValue("-mugenBuildPath") ?? DefaultAndroidBuildPath;
            string fullBuildPath = Path.GetFullPath(buildPath);
            string directory = Path.GetDirectoryName(fullBuildPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string[] scenes = EnabledBuildScenes();
            if (scenes.Length == 0)
            {
                throw new Exception("No enabled scenes in EditorBuildSettings.");
            }

            EditorUserBuildSettings.buildAppBundle = false;
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = fullBuildPath,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception("MUGEN Android build failed: " + report.summary.result);
            }

            UnityEngine.Debug.Log("MUGEN Android build written to " + fullBuildPath);
        }

        static string[] EnabledBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes;
            int count = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled)
                {
                    count++;
                }
            }

            string[] paths = new string[count];
            int write = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].enabled)
                {
                    paths[write++] = scenes[i].path;
                }
            }
            return paths;
        }

        static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
