using System;
using UnityEngine;
using Lockstep.Client;
using Lockstep.Core;
using Lockstep.Game.Components;

namespace Lockstep.Game.View
{
    public class BattleScene : MonoBehaviour
    {
        [Header("Wiring")]
        public Bootstrap Bootstrap;
        public GameConfig Config;

        [Header("Optional: arena background to auto-fit")]
        public SpriteRenderer ArenaBackground;
        public bool AutoFitArena = true;

        public event Action<Transform> OnLocalPlayerSpawned;

        public float MapHalfWidth { get; private set; }
        public float MapHalfHeight { get; private set; }

        Transform _playerRoot;
        GameObject[] _loadedPrefabs;
        LockstepClient _boundClient;

        void Awake()
        {
            _playerRoot = new GameObject("Players").transform;
            _playerRoot.SetParent(transform, false);
        }

        void Start()
        {
            if (Config == null) { Debug.LogError("[BattleScene] Config 未配置"); return; }
            if (Bootstrap == null) { Debug.LogError("[BattleScene] Bootstrap 未配置"); return; }
            if (Bootstrap.LogicConfig == null) { Debug.LogError("[BattleScene] Bootstrap.LogicConfig 未配置"); return; }

            LoadResources();
            MapHalfWidth = Bootstrap.LogicConfig.MapHalfWidthView;
            MapHalfHeight = Bootstrap.LogicConfig.MapHalfHeightView;
            FitArena();

            if (Bootstrap.LocalClient != null) HookClient(Bootstrap.LocalClient);
            else Bootstrap.OnLocalClientReady += HookClient;
        }

        void HookClient(LockstepClient client)
        {
            if (_boundClient == client) return;
            _boundClient = client;

            if (client.State == ClientState.Playing)
            {
                BindAll(client.World);
                return;
            }
            client.OnStateChanged += (a, b) =>
            {
                if (b == ClientState.Playing) BindAll(client.World);
            };
        }

        void LoadResources()
        {
            _loadedPrefabs = new GameObject[Config.PlayerPrefabPaths.Length];
            for (int i = 0; i < Config.PlayerPrefabPaths.Length; i++)
            {
                _loadedPrefabs[i] = Resources.Load<GameObject>(Config.PlayerPrefabPaths[i]);
                if (_loadedPrefabs[i] == null)
                    Debug.LogError($"[BattleScene] Resources/{Config.PlayerPrefabPaths[i]} 加载失败");
            }
        }

        void FitArena()
        {
            if (!AutoFitArena || ArenaBackground == null || ArenaBackground.sprite == null) return;
            var size = ArenaBackground.sprite.bounds.size;
            float targetW = MapHalfWidth * 2f;
            float targetH = MapHalfHeight * 2f;
            ArenaBackground.transform.localScale = new Vector3(
                targetW / Mathf.Max(0.001f, size.x),
                targetH / Mathf.Max(0.001f, size.y),
                1f);
            ArenaBackground.sortingOrder = -10000;
        }

        void BindAll(World w)
        {
            int localIndex = _boundClient != null ? _boundClient.LocalPlayerId : 0;

            foreach (var e in w.Entities)
            {
                var tag = e.Get<PlayerTagC>();
                if (tag == null) continue;
                if (tag.PlayerIndex >= _loadedPrefabs.Length) continue;
                var prefab = _loadedPrefabs[tag.PlayerIndex];
                if (prefab == null) continue;

                var go = Instantiate(prefab, _playerRoot);
                go.name = $"Player_{e.Id}";

                var view = go.GetComponent<PlayerView>();
                if (view == null)
                {
                    Debug.LogError($"[BattleScene] {prefab.name} prefab 缺少 PlayerView 组件");
                    continue;
                }
                view.Bound = e;

                if (tag.PlayerIndex == localIndex)
                    OnLocalPlayerSpawned?.Invoke(go.transform);
            }
        }
    }
}
