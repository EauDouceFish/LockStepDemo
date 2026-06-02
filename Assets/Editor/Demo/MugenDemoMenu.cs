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

        static void CreateDemo(string characterFolder, int animNo)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            GameObject character = new GameObject(characterFolder);
            character.transform.position = Vector3.zero;
            MugenCharacterView view = character.AddComponent<MugenCharacterView>();
            view.CharacterFolder = characterFolder;
            view.AnimNo = animNo;

            Camera camera = Camera.main;
            if (camera != null)
            {
                camera.orthographic = true;
                camera.orthographicSize = 3f;
                camera.transform.position = new Vector3(0f, 1.5f, -10f);
                camera.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
            }

            string sceneDir = "Assets/Scenes";
            if (!Directory.Exists(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
            }
            string scenePath = sceneDir + "/MugenDemo_" + characterFolder + ".unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log("[MUGEN] Demo 场景已建：" + scenePath + " —— 按 Play 看 " + characterFolder
                + " 动画。改 AnimNo=20 看走路。换角色：把素材放 ../MugenSource/ 并改 CharacterFolder。");
        }
    }
}
