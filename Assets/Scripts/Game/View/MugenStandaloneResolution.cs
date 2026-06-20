using System.Collections;
using UnityEngine;

namespace Lockstep.View
{
    public sealed class MugenStandaloneResolution : MonoBehaviour
    {
        const int Width = 1920;
        const int Height = 1080;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
#if UNITY_STANDALONE && !UNITY_EDITOR
            if (HasCommandLineResolutionOverride())
            {
                Debug.Log("[Resolution] skip forced standalone resolution because command line controls window size");
                return;
            }
            GameObject root = new GameObject(nameof(MugenStandaloneResolution));
            DontDestroyOnLoad(root);
            root.AddComponent<MugenStandaloneResolution>();
#endif
        }

        void Awake()
        {
            Apply("awake");
            StartCoroutine(ApplyForStartupFrames());
        }

        IEnumerator ApplyForStartupFrames()
        {
            for (int i = 0; i < 10; i++)
            {
                yield return null;
                Apply("startup-frame-" + i);
            }
        }

        static void Apply(string reason)
        {
            if (Screen.width == Width && Screen.height == Height &&
                Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                return;
            }

            int beforeWidth = Screen.width;
            int beforeHeight = Screen.height;
            FullScreenMode beforeMode = Screen.fullScreenMode;
            Screen.SetResolution(Width, Height, FullScreenMode.Windowed);
            Debug.Log(string.Format("[Resolution] force {0}x{1} windowed reason={2} actualBefore={3}x{4} mode={5}",
                Width, Height, reason, beforeWidth, beforeHeight, beforeMode));
        }

        static bool HasCommandLineResolutionOverride()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == "--autotest" ||
                    arg == "-screen-width" || arg == "-screen-height" ||
                    arg == "-screen-fullscreen" || arg == "-popupwindow")
                {
                    return true;
                }
            }
            return false;
        }
    }
}
