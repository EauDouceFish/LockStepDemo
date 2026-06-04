using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lockstep.View;

namespace Lockstep.EditorTools
{
    /// <summary>
    /// 一键创建 MUGEN 角色动画 Demo 场景（编辑器工具）。生成正交相机 + 一个挂 MugenCharacterView 的角色，
    /// 按 Play 即可看动画循环。改 MugenCharacterView.AnimNo（0=站立、20=走路）切动画；
    /// CharacterFolder 改成放在 ../MugenSource/ 下的其它角色（如 kfm）即可换人。
    /// </summary>
    public static class MugenDemoMenu
    {
        [MenuItem("MUGEN/Demo/创建动画 Demo 场景 (Terrarian)")]
        public static void CreateTerrarianDemo()
        {
            CreateDemo("Terrarian", 0);
        }

        [MenuItem("MUGEN/Demo/创建动画 Demo 场景 (kfm)")]
        public static void CreateKfmDemo()
        {
            CreateDemo("kfm", 0);
        }

        [MenuItem("MUGEN/Demo/创建 Live 引擎场景 (kfm)")]
        public static void CreateKfmLive()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject character = new GameObject("kfm_Live");
            character.transform.position = Vector3.zero;
            character.AddComponent<MugenLiveView>();   // M9.2：MBattleEngine 逐帧驱动 + 键盘输入

            SetupCamera();

            string scenePath = SceneDir() + "/MugenLive_kfm.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[MUGEN] Live 引擎场景已建：" + scenePath
                + " —— 按 Play 看 KFM 经新引擎站立/行走。方向键走路，Z/X/C 出招。");
        }

        [MenuItem("MUGEN/Demo/创建全动作展示场景 (Terrarian)")]
        public static void CreateTerrarianShowcase()
        {
            CreateShowcase("Terrarian");
        }

        [MenuItem("MUGEN/Demo/创建全动作展示场景 (kfm)")]
        public static void CreateKfmShowcase()
        {
            CreateShowcase("kfm");
        }

        [MenuItem("MUGEN/Demo/创建角色画廊场景 (全部角色)")]
        public static void CreateGallery()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject gallery = new GameObject("CharacterGallery");
            gallery.transform.position = Vector3.zero;
            gallery.AddComponent<MugenGalleryShowcase>();   // 自身在 Start 里扫描角色 + 配相机

            string scenePath = SceneDir() + "/MugenGallery.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[MUGEN] 角色画廊场景已建：" + scenePath
                + " —— 按 Play 一页展示 4 个角色站立动画。←/→ 或点屏上按钮翻页。");
        }

        static void CreateShowcase(string characterFolder)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject character = new GameObject(characterFolder + "_Showcase");
            character.transform.position = Vector3.zero;
            MugenAnimShowcase showcase = character.AddComponent<MugenAnimShowcase>();
            showcase.CharacterFolder = characterFolder;

            SetupCamera();

            string scenePath = SceneDir() + "/MugenShowcase_" + characterFolder + ".unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[MUGEN] 全动作展示场景已建：" + scenePath
                + " —— 按 Play 依次循环播放全部动画。←/→ 手动切，空格暂停。");
        }

        static void CreateDemo(string characterFolder, int animNo)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject character = new GameObject(characterFolder);
            character.transform.position = Vector3.zero;
            MugenCharacterView view = character.AddComponent<MugenCharacterView>();
            view.CharacterFolder = characterFolder;
            view.AnimNo = animNo;

            SetupCamera();

            string scenePath = SceneDir() + "/MugenDemo_" + characterFolder + ".unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[MUGEN] Demo 场景已建：" + scenePath + " —— 按 Play 看 " + characterFolder
                + " 动画。改 AnimNo=20 看走路。换角色：把素材放 ../MugenSource/ 并改 CharacterFolder。");
        }

        static void SetupCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = Object.FindObjectOfType<Camera>();
            }
            if (camera == null)
            {
                return;
            }
            camera.orthographic = true;
            camera.orthographicSize = 1.4f;                       // 拉近：角色脚在 y=0，相机对准其上半身高度
            camera.transform.position = new Vector3(0f, 1.0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;      // 否则背景永远是天空盒，纯色不显示
            camera.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
        }

        static string SceneDir()
        {
            string sceneDir = "Assets/Scenes";
            if (!Directory.Exists(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
            }
            return sceneDir;
        }
    }
}
