using System.Collections.Generic;
using System.IO;
using UnityEngine;
using GameData = Lockstep.Game.Data;
using Lockstep.Import.Air;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.View
{
    /// <summary>
    /// R-2P —— 双人对战表现层：把 <see cref="MBattleEngine"/>（双角色双向命中）+ <see cref="MRoundSystem"/>
    /// 接到 Unity。两个 KFM 同场，P1 键盘控制（方向 + Z/X/C 出招），P2 为木桩（无输入），命中后 P2 进受击。
    /// 逻辑层纯定点；定点→float 仅在本表现层边界转换。单 SFF 源两个 SpriteRenderer 共享精灵缓存。
    /// </summary>
    public sealed class MugenVersusView : MonoBehaviour
    {
        public string CharacterFolder = "kfm";
        public string CommonFolder = "Terrarian";
        public float PixelsPerUnit = 50f;
        public float TicksPerSecond = 60f;
        public float StartSeparation = 50f;   // 两人初始间距（MUGEN 单位，各距中心一半）

        MBattleEngine _engine;
        MRoundSystem _round;
        MCharData _data;
        MugenSpriteLoader.Source _source;
        readonly Dictionary<int, GameData.AnimData> _gameAnims = new Dictionary<int, GameData.AnimData>();
        readonly HashSet<int> _builtAnims = new HashSet<int>();
        readonly SpriteRenderer[] _renderers = new SpriteRenderer[2];
        readonly List<MInput> _inputs = new List<MInput> { MInput.None, MInput.None };
        float _accumulator;
        int _frame;

        void Start()
        {
            string mugenRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "MugenSource"));
            string charDir = Path.Combine(mugenRoot, CharacterFolder);
            string cnsPath = Path.Combine(charDir, "kfm.cns");
            string cmdPath = Path.Combine(charDir, "kfm.cmd");
            string airPath = MugenDef.AnimPath(charDir);
            string sffPath = MugenDef.SpritePath(charDir);
            string commonPath = Path.Combine(mugenRoot, CommonFolder, "common1.cns");

            if (!File.Exists(cnsPath) || airPath == null || sffPath == null)
            {
                Debug.LogError("[MUGEN] 缺角色文件（kfm.cns/.air/.sff），目录：" + charDir);
                return;
            }

            string cnsText = File.ReadAllText(cnsPath);
            string commonText = File.Exists(commonPath) ? File.ReadAllText(commonPath) : null;
            _data = MCharLoader.Load(
                new[] { cnsText }, cnsText, commonText,
                File.ReadAllText(airPath), File.Exists(cmdPath) ? File.ReadAllText(cmdPath) : null,
                CharacterFolder);

            MChar p1 = MCharLoader.SpawnChar(_data, 0, startStateNo: 0, startAnimNo: 0);
            MChar p2 = MCharLoader.SpawnChar(_data, 1, startStateNo: 0, startAnimNo: 0);
            // 面对面摆位：P1 在左面右，P2 在右面左（MUGEN 坐标，地面 y=0）。
            p1.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.FromInt(-(int)(StartSeparation / 2f)), Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            p1.Facing = Lockstep.Math.FFloat.One;
            p2.Pos = new Lockstep.Math.FVector3(Lockstep.Math.FFloat.FromInt((int)(StartSeparation / 2f)), Lockstep.Math.FFloat.Zero, Lockstep.Math.FFloat.Zero);
            p2.Facing = -Lockstep.Math.FFloat.One;

            _engine = new MBattleEngine();
            _engine.Add(p1, _data);
            _engine.Add(p2, _data);
            _engine.LinkPair();
            _round = new MRoundSystem(_engine) { IntroTime = 30 };   // 短入场，便于演示

            _source = MugenSpriteLoader.Open(sffPath, PixelsPerUnit);
            List<GameData.AnimData> anims = AirParser.ParseFile(airPath);
            for (int i = 0; i < anims.Count; i++)
            {
                _gameAnims[anims[i].Id] = anims[i];
            }

            for (int i = 0; i < 2; i++)
            {
                GameObject go = new GameObject("P" + (i + 1));
                go.transform.SetParent(transform, false);
                _renderers[i] = go.AddComponent<SpriteRenderer>();
                _renderers[i].sortingOrder = i;
            }

            Application.runInBackground = true;
            Debug.Log(string.Format(
                "[MUGEN] Versus: 两个 {0} 同场。P1 方向键走位 + Z/X/C 出招命中 P2（木桩）。状态 {1}/公共 {2}。",
                CharacterFolder, _data.States.Count, _data.CommonStates.Count));
            RenderAll();
        }

        void Update()
        {
            if (_engine == null)
            {
                return;
            }
            _accumulator += Time.deltaTime * TicksPerSecond;
            int guard = 0;
            while (_accumulator >= 1f && guard < 8)
            {
                _accumulator -= 1f;
                guard++;
                _inputs[0] = SampleInput();
                _inputs[1] = MInput.None;   // P2 木桩
                _round.Tick(_inputs);
                _frame++;
            }
            RenderAll();
        }

        static MInput SampleInput()
        {
            MInput input = MInput.None;
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow)) { input |= MInput.Up; }
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow)) { input |= MInput.Down; }
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)) { input |= MInput.Left; }
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) { input |= MInput.Right; }
            if (UnityEngine.Input.GetKey(KeyCode.Z)) { input |= MInput.A; }
            if (UnityEngine.Input.GetKey(KeyCode.X)) { input |= MInput.B; }
            if (UnityEngine.Input.GetKey(KeyCode.C)) { input |= MInput.C; }
            return input;
        }

        void RenderAll()
        {
            for (int i = 0; i < 2; i++)
            {
                RenderChar(_engine.Chars[i], _renderers[i]);
            }
        }

        void RenderChar(MChar c, SpriteRenderer renderer)
        {
            renderer.flipX = c.Facing.Raw < 0;
            float unitX = c.Pos.X.ToFloat() / PixelsPerUnit;
            float unitY = -c.Pos.Y.ToFloat() / PixelsPerUnit;
            renderer.transform.localPosition = new Vector3(unitX, unitY, 0f);

            if (!_data.Anims.TryGetValue(c.AnimNo, out MAnimData anim) || anim.Frames.Length == 0)
            {
                return;
            }
            int elem = Mathf.Clamp(c.AnimElem, 0, anim.Frames.Length - 1);
            MAnimFrame frame = anim.Frames[elem];
            EnsureBuilt(c.AnimNo);
            long key = MugenSpriteAnimator.Key(frame.SpriteGroup, frame.SpriteImage);
            if (_source.Cache.TryGetValue(key, out Sprite sprite))
            {
                renderer.sprite = sprite;
            }
        }

        void EnsureBuilt(int animNo)
        {
            if (_builtAnims.Contains(animNo))
            {
                return;
            }
            if (_gameAnims.TryGetValue(animNo, out GameData.AnimData ga))
            {
                MugenSpriteLoader.BuildForAnim(_source, ga);
            }
            _builtAnims.Add(animNo);
        }

        void OnGUI()
        {
            if (_engine == null)
            {
                return;
            }
            MChar p1 = _engine.Chars[0];
            MChar p2 = _engine.Chars[1];
            string text = string.Format(
                "Versus 帧 {0}   回合态 {1}\nP1  State {2}  Life {3}  Ctrl {4}\nP2  State {5}  Life {6}  MoveType {7}\nP1 方向键走位 + Z/X/C 出招命中 P2",
                _frame, _round.State, p1.StateNo, p1.Life, p1.Ctrl,
                p2.StateNo, p2.Life, p2.MoveType);
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 16;
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(12f, 10f, 760f, 120f), text, style);
        }
    }
}
