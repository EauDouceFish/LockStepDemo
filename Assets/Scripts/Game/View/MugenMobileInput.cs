using Lockstep.Mugen.Command;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Lockstep.View
{
    public sealed class MugenMobileInput : MonoBehaviour
    {
        public static MugenMobileInput Instance { get; private set; }

        MInput _buttons;
        MInput _direction;
        MInput _keyboard;

        public MInput Current => _direction | _buttons | _keyboard;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) { Instance = null; }
        }

        public void SetStick(Vector2 value)
        {
            MInput direction = MInput.None;
            if (value.y > 0.35f) { direction |= MInput.Up; }
            if (value.y < -0.35f) { direction |= MInput.Down; }
            if (value.x < -0.35f) { direction |= MInput.Left; }
            if (value.x > 0.35f) { direction |= MInput.Right; }
            _direction = direction;
        }

        public void SetButton(MInput button, bool down)
        {
            if (down) { _buttons |= button; }
            else { _buttons &= ~button; }
        }

        public void SetKeyboard(MInput input)
        {
            _keyboard = input;
        }

        public void Clear()
        {
            _direction = MInput.None;
            _buttons = MInput.None;
            _keyboard = MInput.None;
        }

        void OnDisable()
        {
            Clear();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) { Clear(); }
        }

        public static MInput Read()
        {
            return Instance != null ? Instance.Current : MInput.None;
        }

        public static MInput ReadKeyboard()
        {
            MInput input = MInput.None;
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow)) { input |= MInput.Up; }
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow)) { input |= MInput.Down; }
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)) { input |= MInput.Left; }
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) { input |= MInput.Right; }
            if (UnityEngine.Input.GetKey(KeyCode.A)) { input |= MInput.X; }
            if (UnityEngine.Input.GetKey(KeyCode.S)) { input |= MInput.Y; }
            if (UnityEngine.Input.GetKey(KeyCode.D)) { input |= MInput.Z; }
            if (UnityEngine.Input.GetKey(KeyCode.Z)) { input |= MInput.A; }
            if (UnityEngine.Input.GetKey(KeyCode.X)) { input |= MInput.B; }
            if (UnityEngine.Input.GetKey(KeyCode.C)) { input |= MInput.C; }
            return input;
        }
    }

    public sealed class MugenVirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        public RectTransform Background;
        public RectTransform Knob;
        public float MaxRadius = 80f;
        public float DeadZone = 12f;

        Vector2 _startKnobPos;
        bool _pointerDown;

        void Start()
        {
            if (Knob != null) { _startKnobPos = Knob.anchoredPosition; }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDown = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Background == null || Knob == null) { return; }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Background, eventData.position, eventData.pressEventCamera, out Vector2 localPos))
            {
                float distance = localPos.magnitude;
                if (distance > MaxRadius)
                {
                    localPos = localPos.normalized * MaxRadius;
                    distance = MaxRadius;
                }

                Knob.anchoredPosition = localPos;
                MugenMobileInput.Instance?.SetStick(distance > DeadZone ? localPos / MaxRadius : Vector2.zero);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pointerDown = false;
            if (Knob != null) { Knob.anchoredPosition = _startKnobPos; }
            MugenMobileInput.Instance?.SetStick(Vector2.zero);
        }

        public void SetVisualDirection(MInput input)
        {
            if (_pointerDown || Knob == null)
            {
                return;
            }

            Vector2 value = Vector2.zero;
            if ((input & MInput.Left) != 0) { value.x -= 1f; }
            if ((input & MInput.Right) != 0) { value.x += 1f; }
            if ((input & MInput.Up) != 0) { value.y += 1f; }
            if ((input & MInput.Down) != 0) { value.y -= 1f; }
            if (value.sqrMagnitude > 1f)
            {
                value.Normalize();
            }
            Knob.anchoredPosition = _startKnobPos + value * MaxRadius;
        }
    }

    public sealed class MugenVirtualButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public MInput Button = MInput.A;

        Vector3 _originalScale;
        bool _pointerDown;
        bool _keyboardDown;

        void Start()
        {
            _originalScale = transform.localScale;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDown = true;
            MugenMobileInput.Instance?.SetButton(Button, true);
            ApplyVisual();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pointerDown = false;
            MugenMobileInput.Instance?.SetButton(Button, false);
            ApplyVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerDown = false;
            MugenMobileInput.Instance?.SetButton(Button, false);
            ApplyVisual();
        }

        public void SetKeyboardDown(bool down)
        {
            if (_keyboardDown == down)
            {
                return;
            }
            _keyboardDown = down;
            ApplyVisual();
        }

        void ApplyVisual()
        {
            transform.localScale = (_pointerDown || _keyboardDown) ? _originalScale * 0.92f : _originalScale;
        }
    }

    public sealed class MugenKeyboardInputBridge : MonoBehaviour
    {
        MugenVirtualButton[] _buttons;
        MugenVirtualJoystick _joystick;
        Transform _root;

        public void Bind(Transform root, MugenVirtualJoystick joystick)
        {
            _root = root;
            _joystick = joystick;
            RefreshButtons();
        }

        void Update()
        {
            if (_buttons == null)
            {
                RefreshButtons();
            }

            MInput keyboard = MugenMobileInput.ReadKeyboard();
            MugenMobileInput.Instance?.SetKeyboard(keyboard);

            if (_buttons != null)
            {
                for (int i = 0; i < _buttons.Length; i++)
                {
                    MugenVirtualButton button = _buttons[i];
                    if (button != null)
                    {
                        button.SetKeyboardDown((keyboard & button.Button) != 0);
                    }
                }
            }

            if (_joystick != null)
            {
                _joystick.SetVisualDirection(keyboard & MInput.DirMask);
            }
        }

        void RefreshButtons()
        {
            Transform source = _root != null ? _root : transform;
            _buttons = source.GetComponentsInChildren<MugenVirtualButton>(true);
        }
    }

    public static class MugenMobileInputUi
    {
        static GameObject _canvasGo;
        static GameObject _sceneJoystickGo;

        public static bool EnsureSceneControls()
        {
            Transform root = FindSceneTransform("舍弃");
            if (root == null)
            {
                root = FindSceneTransform("\u6309\u952E");
            }
            if (root == null)
            {
                return false;
            }

            EnsureEventSystem();
            root.gameObject.SetActive(true);
            if (MugenMobileInput.Instance == null)
            {
                root.gameObject.AddComponent<MugenMobileInput>();
            }
            _canvasGo = root.gameObject;

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                GameObject go = buttons[i].gameObject;
                go.SetActive(true);
                MugenVirtualButton virtualButton = go.GetComponent<MugenVirtualButton>();
                if (virtualButton == null)
                {
                    virtualButton = go.AddComponent<MugenVirtualButton>();
                }
                virtualButton.Button = SceneButtonInput(go.name);
            }

            Transform joystickTransform = FindSceneTransform("Joystick");
            if (joystickTransform == null && _sceneJoystickGo == null)
            {
                Transform parent = root.GetComponentInParent<Canvas>(true) != null
                    ? root.GetComponentInParent<Canvas>(true).transform
                    : root;
                _sceneJoystickGo = CreateJoystick(parent);
                _sceneJoystickGo.name = "BattleSceneJoystick";
                joystickTransform = _sceneJoystickGo.transform;
            }
            if (_sceneJoystickGo != null)
            {
                _sceneJoystickGo.SetActive(true);
            }
            MugenVirtualJoystick joystick = joystickTransform != null ? joystickTransform.GetComponent<MugenVirtualJoystick>() : null;
            if (joystickTransform != null && joystick == null)
            {
                joystick = joystickTransform.gameObject.AddComponent<MugenVirtualJoystick>();
                RectTransform rect = joystickTransform as RectTransform;
                joystick.Background = rect;
                Transform knob = FindChildRecursive(joystickTransform, "Knob");
                joystick.Knob = knob as RectTransform;
            }
            MugenKeyboardInputBridge bridge = root.GetComponent<MugenKeyboardInputBridge>() ?? root.gameObject.AddComponent<MugenKeyboardInputBridge>();
            bridge.Bind(root, joystick);

            return true;
        }

        public static void Ensure()
        {
            if (MugenMobileInput.Instance != null)
            {
                if (_canvasGo != null) { _canvasGo.SetActive(true); }
                return;
            }
            EnsureEventSystem();

            _canvasGo = new GameObject("MobileInputCanvas");
            Object.DontDestroyOnLoad(_canvasGo);
            Canvas canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            _canvasGo.AddComponent<GraphicRaycaster>();
            _canvasGo.AddComponent<MugenMobileInput>();

            GameObject joystickGo = CreateJoystick(canvas.transform);
            AddButtons(canvas.transform);
            MugenKeyboardInputBridge bridge = _canvasGo.AddComponent<MugenKeyboardInputBridge>();
            bridge.Bind(canvas.transform, joystickGo.GetComponent<MugenVirtualJoystick>());
        }

        public static void Hide()
        {
            if (MugenMobileInput.Instance != null) { MugenMobileInput.Instance.Clear(); }
            if (_canvasGo != null) { _canvasGo.SetActive(false); }
            if (_sceneJoystickGo != null) { _sceneJoystickGo.SetActive(false); }
        }

        static void AddButtons(Transform parent)
        {
            CreateButton(parent, "A\nZ", MInput.A, new Vector2(-285f, 110f));
            CreateButton(parent, "B\nX", MInput.B, new Vector2(-190f, 185f));
            CreateButton(parent, "C\nC", MInput.C, new Vector2(-95f, 110f));
            CreateButton(parent, "X\nA", MInput.X, new Vector2(-380f, 185f));
            CreateButton(parent, "Y\nS", MInput.Y, new Vector2(-285f, 260f));
            CreateButton(parent, "Z\nD", MInput.Z, new Vector2(-190f, 335f));
            CreateButton(parent, "S\nF", MInput.S, new Vector2(-70f, 255f));
        }

        /*
            CreateButton(parent, "轻脚\na", MInput.A, new Vector2(-285f, 110f));
            CreateButton(parent, "重脚\nb", MInput.B, new Vector2(-190f, 185f));
            CreateButton(parent, "三脚\nc", MInput.C, new Vector2(-95f, 110f));
            CreateButton(parent, "轻拳\nx", MInput.X, new Vector2(-380f, 185f));
            CreateButton(parent, "重拳\ny", MInput.Y, new Vector2(-285f, 260f));
            CreateButton(parent, "三拳\nz", MInput.Z, new Vector2(-190f, 335f));
            CreateButton(parent, "开始", MInput.S, new Vector2(-70f, 255f));
        }

        */

        static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) { return; }
            GameObject eventSystem = new GameObject("EventSystem");
            Object.DontDestroyOnLoad(eventSystem);
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        static GameObject CreateJoystick(Transform parent)
        {
            GameObject root = new GameObject("Joystick");
            root.transform.SetParent(parent, false);
            RectTransform rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0f, 0f);
            rootRt.anchoredPosition = new Vector2(160f, 140f);
            rootRt.sizeDelta = new Vector2(180f, 180f);

            Image bg = root.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.18f);

            GameObject knobGo = new GameObject("Knob");
            knobGo.transform.SetParent(root.transform, false);
            Image knob = knobGo.AddComponent<Image>();
            knob.color = new Color(1f, 1f, 1f, 0.45f);
            RectTransform knobRt = knob.rectTransform;
            knobRt.anchorMin = knobRt.anchorMax = new Vector2(0.5f, 0.5f);
            knobRt.sizeDelta = new Vector2(72f, 72f);
            knobRt.anchoredPosition = Vector2.zero;

            MugenVirtualJoystick joystick = root.AddComponent<MugenVirtualJoystick>();
            joystick.Background = rootRt;
            joystick.Knob = knobRt;
            return root;
        }

        static void CreateButton(Transform parent, string label, MInput button, Vector2 offset)
        {
            GameObject go = new GameObject("Button" + label);
            go.transform.SetParent(parent, false);
            Image image = go.AddComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.25f);
            RectTransform rt = image.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(86f, 86f);

            MugenVirtualButton virtualButton = go.AddComponent<MugenVirtualButton>();
            virtualButton.Button = button;

            Font font = MugenChineseText.Font();
            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            Text text = textGo.AddComponent<Text>();
            text.font = font;
            text.text = label;
            text.fontSize = label.Length > 3 ? 20 : 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
        }

        static MInput SceneButtonInput(string name)
        {
            string key = string.IsNullOrEmpty(name) ? "" : name.Trim().ToUpperInvariant();
            if (key == "A" || key.Contains("BUTTONA")) { return MInput.A; }
            if (key == "B" || key.Contains("BUTTONB")) { return MInput.B; }
            if (key == "C" || key.Contains("BUTTONC")) { return MInput.C; }
            if (key == "X" || key.Contains("BUTTONX")) { return MInput.X; }
            if (key == "Y" || key.Contains("BUTTONY")) { return MInput.Y; }
            if (key == "Z" || key.Contains("BUTTONZ")) { return MInput.Z; }
            if (key == "S" || key.Contains("BUTTONS")) { return MInput.S; }
            if (string.IsNullOrEmpty(name)) { return MInput.None; }
            if (name.Contains("轻脚")) { return MInput.A; }
            if (name.Contains("重脚")) { return MInput.B; }
            if (name.Contains("轻拳")) { return MInput.X; }
            if (name.Contains("重拳")) { return MInput.Y; }
            if (name.Contains("SkillBtn_01")) { return MInput.C; }
            if (name.Contains("SkillBtn_02")) { return MInput.Z; }
            if (name.Contains("SkillBtn_03")) { return MInput.S; }
            return MInput.None;
        }

        static Transform FindSceneTransform(string name)
        {
            Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t.name == name && t.gameObject.scene.IsValid() && t.gameObject.scene.isLoaded)
                {
                    return t;
                }
            }
            return null;
        }

        static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) { return null; }
            if (root.name == name) { return root; }
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), name);
                if (found != null) { return found; }
            }
            return null;
        }
    }
}
