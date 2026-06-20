using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lockstep.View
{
    public sealed class MugenMainMenu : MonoBehaviour
    {
        static readonly Color BgColor = new Color(0.1f, 0.12f, 0.16f, 1f);
        static readonly Color ButtonColor = new Color(0.18f, 0.78f, 1f, 1f);
        static readonly Color TextColor = new Color(1f, 0.86f, 0.28f, 1f);

        Canvas _canvas;
        GameObject _serverPanel;
        TMP_Text _serverInfoTmp;
        Text _serverInfoText;
        MugenServerLatencyProbe _latencyProbe;
        int _latencyServerIndex = -1;

        void Awake()
        {
            Application.targetFrameRate = 60;
            MugenMatchSetup.ApplySelectedServer();
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = BgColor;
            }
        }

        void Start()
        {
            if (!BindSceneUi())
            {
                BuildUi();
            }
            StartLatencyProbe();
            if (MugenAutoTest.Enabled)
            {
                MugenAutoTest.Trace("scene_main_menu", "auto enter select");
                StartCoroutine(AutoEnterSelectNextFrame());
            }
        }

        void Update()
        {
            if (_latencyProbe != null)
            {
                _latencyProbe.Update();
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                EnterSelect();
            }
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) && _serverPanel != null && _serverPanel.activeSelf)
            {
                _serverPanel.SetActive(false);
            }
        }

        bool BindSceneUi()
        {
            EnsureEventSystem();

            Canvas sceneCanvas = FindObjectOfType<Canvas>();
            Button startButton = FindSceneButton("开始游戏按钮", requireActive: true);
            Button serverButton = FindSceneButton("选择服务器按钮", requireActive: true);
            Transform panelTransform = FindSceneTransform("服务器选择栏", requireActive: false);
            Transform infoTransform = FindSceneTransform("当前服务器info", requireActive: false);

            if (sceneCanvas == null || startButton == null)
            {
                return false;
            }

            _canvas = sceneCanvas;

            startButton.onClick.RemoveListener(EnterSelect);
            startButton.onClick.AddListener(EnterSelect);
            EventSystem.current.SetSelectedGameObject(startButton.gameObject);

            if (serverButton != null)
            {
                serverButton.onClick.RemoveListener(ToggleServerPanel);
                serverButton.onClick.AddListener(ToggleServerPanel);
            }

            if (panelTransform != null)
            {
                _serverPanel = panelTransform.gameObject;
                BuildServerOptions(panelTransform);
                _serverPanel.SetActive(false);
            }

            if (infoTransform != null)
            {
                _serverInfoTmp = infoTransform.GetComponent<TMP_Text>();
                _serverInfoText = infoTransform.GetComponent<Text>();
            }
            RefreshServerInfo();
            return true;
        }

        void BuildServerOptions(Transform panel)
        {
            Button template = FindChildButton(panel, "服务器选择按钮");
            if (template == null)
            {
                return;
            }

            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                Transform child = panel.GetChild(i);
                if (child != template.transform && child.name.StartsWith("ServerOption_", System.StringComparison.Ordinal))
                {
                    Destroy(child.gameObject);
                }
            }

            template.gameObject.SetActive(false);
            for (int i = 0; i < MugenMatchSetup.Servers.Length; i++)
            {
                MugenMatchSetup.ServerEndpoint server = MugenMatchSetup.Servers[i];
                Button button = Instantiate(template, panel);
                button.name = "ServerOption_" + server.Id;
                button.gameObject.SetActive(true);
                SetButtonLabel(button, server.ButtonText);
                int index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectServer(index));
            }
        }

        void ToggleServerPanel()
        {
            if (_serverPanel == null)
            {
                return;
            }
            _serverPanel.SetActive(!_serverPanel.activeSelf);
        }

        void SelectServer(int index)
        {
            MugenMatchSetup.SelectServer(index);
            StartLatencyProbe();
            RefreshServerInfo();
            if (_serverPanel != null)
            {
                _serverPanel.SetActive(false);
            }
        }

        void RefreshServerInfo()
        {
            MugenMatchSetup.ServerEndpoint server = MugenMatchSetup.SelectedServer;
            if (server == null)
            {
                return;
            }

            string text = MugenMatchSetup.FormatSelectedServerStatus();
            if (_serverInfoTmp != null)
            {
                _serverInfoTmp.text = text;
            }
            if (_serverInfoText != null)
            {
                _serverInfoText.text = text;
            }
        }

        void StartLatencyProbe()
        {
            MugenMatchSetup.ApplySelectedServer();
            MugenMatchSetup.ServerEndpoint server = MugenMatchSetup.SelectedServer;
            if (server == null)
            {
                return;
            }

            _latencyServerIndex = MugenMatchSetup.SelectedServerIndex;
            if (_latencyProbe == null)
            {
                _latencyProbe = new MugenServerLatencyProbe(latencyMs =>
                {
                    MugenMatchSetup.SetServerLatencyMs(_latencyServerIndex, latencyMs);
                    RefreshServerInfo();
                });
            }
            _latencyProbe.Start(server.Host, server.Port);
            RefreshServerInfo();
        }

        static Button FindSceneButton(string name, bool requireActive)
        {
            Transform target = FindSceneTransform(name, requireActive);
            return target != null ? target.GetComponent<Button>() : null;
        }

        static Transform FindSceneTransform(string name, bool requireActive)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform target = transforms[i];
                if (target == null || target.name != name)
                {
                    continue;
                }
                if (!target.gameObject.scene.IsValid())
                {
                    continue;
                }
                if (requireActive && !target.gameObject.activeInHierarchy)
                {
                    continue;
                }
                return target;
            }
            return null;
        }

        static Button FindChildButton(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].name == name)
                {
                    return buttons[i];
                }
            }
            return null;
        }

        static void SetButtonLabel(Button button, string value)
        {
            TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = value;
            }

            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = value;
            }
        }

        void BuildUi()
        {
            EnsureEventSystem();
            Font font = MugenChineseText.Font();

            GameObject canvasGo = new GameObject("MainMenuCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            Image bg = new GameObject("Background").AddComponent<Image>();
            bg.transform.SetParent(_canvas.transform, false);
            bg.color = BgColor;
            RectTransform bgRt = bg.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            Text title = MakeText(font, "MUGEN Lockstep Demo", 42, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.58f), new Vector2(760f, 80f));
            title.color = TextColor;

            Button start = MakeButton(font, "进入选人", new Vector2(0.5f, 0.38f), new Vector2(260f, 72f), EnterSelect);
            EventSystem.current.SetSelectedGameObject(start.gameObject);
        }

        Text MakeText(Font font, string text, int size, TextAnchor align, Vector2 anchor, Vector2 sizeDelta)
        {
            GameObject go = new GameObject("Txt");
            go.transform.SetParent(_canvas.transform, false);
            Text label = go.AddComponent<Text>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = Vector2.zero;
            return label;
        }

        Button MakeButton(Font font, string text, Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            GameObject go = new GameObject("Btn_" + text);
            go.transform.SetParent(_canvas.transform, false);
            Image image = go.AddComponent<Image>();
            image.color = ButtonColor;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);

            RectTransform rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            Text label = MakeText(font, text, 26, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), size);
            label.transform.SetParent(go.transform, false);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.color = Color.white;
            return button;
        }

        void EnterSelect()
        {
            MugenMatchSetup.Clear();
            SceneManager.LoadScene(MugenMatchSetup.SelectSceneName);
        }

        IEnumerator AutoEnterSelectNextFrame()
        {
            yield return null;
            MugenAutoTest.Trace("enter_select_request", MugenMatchSetup.SelectSceneName);
            EnterSelect();
        }

        void OnDestroy()
        {
            if (_latencyProbe != null)
            {
                _latencyProbe.Dispose();
                _latencyProbe = null;
            }
        }

        static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) { return; }
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }
    }
}
