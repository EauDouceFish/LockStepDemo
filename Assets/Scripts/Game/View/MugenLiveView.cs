using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Lockstep.Math;
using GameData = Lockstep.Game.Data;
using Lockstep.Import.Air;
using Lockstep.Mugen.Anim;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Command;

namespace Lockstep.View
{
    /// <summary>
    /// M9.2 —— 把已移植的 <see cref="MBattleEngine"/> 接到 Unity 表现层：表现层做文件 IO + 键盘输入 +
    /// 取精灵绘制，逻辑层（纯定点）只负责每帧 Tick。这是新引擎第一次"能在编辑器里看到动"的入口。
    ///
    /// 每帧（按 60 tick/s 定步累加）：采输入 → engine.Tick → 读 MChar.AnimNo + 当前帧(group,image) 取精灵摆位。
    /// 单角色（KFM + 借 Terrarian 公共状态）。方向键走路，命令链路已具备（common1 Statedef 20 + kfm holdfwd/holdback）。
    /// 逻辑层禁 UnityEngine/float；本类在表现层，定点→float 仅在此边界转换。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class MugenLiveView : MonoBehaviour
    {
        public string CharacterFolder = "kfm";
        public string CommonFolder = "Terrarian";   // KFM 目录无 common1，借标准公共状态（与测试一致）
        public float PixelsPerUnit = 50f;
        public float TicksPerSecond = 60f;

        SpriteRenderer _renderer;
        MBattleEngine _engine;
        MCharData _data;
        MugenSpriteLoader.Source _source;
        readonly Dictionary<int, GameData.AnimData> _gameAnims = new Dictionary<int, GameData.AnimData>();
        readonly HashSet<int> _builtAnims = new HashSet<int>();
        readonly List<MInput> _inputs = new List<MInput> { MInput.None };
        float _accumulator;
        int _frame;

        void Start()
        {
            _renderer = GetComponent<SpriteRenderer>();

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

            // 逻辑层装配：文本进，纯定点引擎出。
            string cnsText = File.ReadAllText(cnsPath);
            string commonText = File.Exists(commonPath) ? File.ReadAllText(commonPath) : null;
            _data = MCharLoader.Load(
                new[] { cnsText }, cnsText, commonText,
                File.ReadAllText(airPath), File.Exists(cmdPath) ? File.ReadAllText(cmdPath) : null,
                CharacterFolder);

            MChar kfm = MCharLoader.SpawnChar(_data, 0, startStateNo: 0, startAnimNo: 0);
            _engine = new MBattleEngine();
            _engine.Add(kfm, _data);
            _engine.LinkPair();
            _engine.StartRound();   // 授予 ctrl/keyctrl，引擎硬编码基础动作（走/跳/蹲）才生效

            // 表现层精灵：打开 SFF，动画按需懒构建（复用已测的 MugenSpriteLoader）。
            _source = MugenSpriteLoader.Open(sffPath, PixelsPerUnit);
            List<GameData.AnimData> anims = AirParser.ParseFile(airPath);
            for (int i = 0; i < anims.Count; i++)
            {
                _gameAnims[anims[i].Id] = anims[i];
            }

            Application.runInBackground = true;   // 失焦也 tick，否则 MCP 截图时动画冻结
            Debug.Log(string.Format(
                "[MUGEN] Live: {0} 已接入引擎。状态 {1} 个、动画 {2} 个、命令 {3} 条。方向键走路，Z/X/C 出招。",
                CharacterFolder, _data.States.Count, _data.Anims.Count, _data.Commands.Count) +
                "  拳 A/S/D  脚 Z/X/C");
            Render();
        }

        void Update()
        {
            if (_engine == null)
            {
                return;
            }
            _accumulator += Time.deltaTime * TicksPerSecond;
            // 定步推进：每攒够一个逻辑帧就 Tick 一次（与 60Hz 锁步对齐）。
            int guard = 0;
            while (_accumulator >= 1f && guard < 8)
            {
                _accumulator -= 1f;
                guard++;
                _inputs[0] = SampleInput();
                _engine.Tick(_inputs);
                _frame++;
            }
            Render();
        }

        // 键盘 → MInput（方向存原始 L/R，B/F 由引擎按朝向转）。
        // MUGEN 6 键布局：拳 x/y/z = A/S/D（家位行），脚 a/b/c = Z/X/C（底行）。
        // KFM 用 x/y(拳) 与 a/b(脚)，三档 z/c 备用。缺拳键时所有拳招按不出（KFM 站立轻/强拳=command "x"/"y"）。
        static MInput SampleInput()
        {
            MInput input = MInput.None;
            if (UnityEngine.Input.GetKey(KeyCode.UpArrow)) { input |= MInput.Up; }
            if (UnityEngine.Input.GetKey(KeyCode.DownArrow)) { input |= MInput.Down; }
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow)) { input |= MInput.Left; }
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow)) { input |= MInput.Right; }
            if (UnityEngine.Input.GetKey(KeyCode.A)) { input |= MInput.X; }   // 轻拳
            if (UnityEngine.Input.GetKey(KeyCode.S)) { input |= MInput.Y; }   // 强拳
            if (UnityEngine.Input.GetKey(KeyCode.D)) { input |= MInput.Z; }   // 第三拳
            if (UnityEngine.Input.GetKey(KeyCode.Z)) { input |= MInput.A; }   // 轻脚
            if (UnityEngine.Input.GetKey(KeyCode.X)) { input |= MInput.B; }   // 强脚
            if (UnityEngine.Input.GetKey(KeyCode.C)) { input |= MInput.C; }   // 第三脚
            return input;
        }

        void Render()
        {
            MChar c = _engine.Chars[0];
            _renderer.flipX = c.Facing.Raw < 0;

            // 位置始终更新——即使动画/精灵缺失也绝不冻结角色（曾因切到不存在的动画号 return 致"浮空"）。
            // 定点 → float 仅在此转换。MUGEN 坐标 Y 向下为正、地面 y=0 → Unity 取负；除 PPU 归一到精灵尺度。
            float unitX = c.Pos.X.ToFloat() / PixelsPerUnit;
            float unitY = -c.Pos.Y.ToFloat() / PixelsPerUnit;
            transform.localPosition = new Vector3(unitX, unitY, 0f);

            // 施加绘制态（旋转/缩放/透明度/绘制顺序/offset；Batch A 控制器写入，每帧重置后重新断言）。
            MugenDrawStateApplier.Apply(c, _renderer, transform, PixelsPerUnit, 0);

            if (!_data.Anims.TryGetValue(c.AnimNo, out MAnimData anim) || anim.Frames.Length == 0)
            {
                return;   // 仅跳过精灵刷新，位置已更新
            }
            int elem = Mathf.Clamp(c.AnimElem, 0, anim.Frames.Length - 1);
            MAnimFrame frame = anim.Frames[elem];

            EnsureBuilt(c.AnimNo);
            long key = MugenSpriteAnimator.Key(frame.SpriteGroup, frame.SpriteImage);
            if (_source.Cache.TryGetValue(key, out Sprite sprite))
            {
                _renderer.sprite = sprite;
            }
        }

        // 某动画号首次显示时才构建其精灵（懒加载，避免一次性建全角色上千张）。
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
            MChar c = _engine.Chars[0];
            string text = string.Format(
                "Live 帧 {0}\nStateNo {1}   AnimNo {2}   Elem {3}\nPhysics {4}  Type {5}  Ctrl {6}\nPos ({7:0.0},{8:0.0})  Vel ({9:0.00},{10:0.00})  Facing {11}\n方向键移动  拳 A/S/D  脚 Z/X/C",
                _frame, c.StateNo, c.AnimNo, c.AnimElem, c.Physics, c.StateType, c.Ctrl,
                c.Pos.X.ToFloat(), c.Pos.Y.ToFloat(), c.Vel.X.ToFloat(), c.Vel.Y.ToFloat(),
                c.Facing.Raw < 0 ? -1 : 1);
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 16;
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(12f, 10f, 700f, 120f), text, style);
        }
    }
}
