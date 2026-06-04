using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Lockstep.Math;
using Lockstep.Mugen.Battle;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.Parse;
using Lockstep.Mugen.State;
using Lockstep.Tests;

namespace Lockstep.Logic.Tests.Mugen.Battle
{
    /// <summary>
    /// 用项目里现有 10 个角色的真实文件逐个跑 R-GHV 受击机：
    /// 加载（cns/air/cmd + 标准 common1 作引擎内建公共态）→ 站立被普攻命中 → 进受击 5000 区 → 走完恢复。
    /// 验证移植的受击状态机对各家真实角色数据（var 偏移/动画表/常量）都成立，而非只对 KFM。
    /// </summary>
    [TestFixture]
    public sealed class AllCharactersGetHitTests
    {
        static readonly string[] Characters =
        {
            "Ananzi", "Animus", "Final", "Gustavo", "Hashi",
            "Janos", "Maxine", "Noroko", "Peketo", "Shar-Makai",
        };

        static FFloat F(int v) => FFloat.FromInt(v);

        // 读取角色 .def 解析出的状态/常量/动画/命令文本，用标准 common1 当引擎内建公共态装配。
        static MCharData LoadChar(string name)
        {
            string dir = TestAssets.CharDir(name);
            if (!Directory.Exists(dir))
            {
                return null;
            }
            string defPath = PickMainDef(dir);
            if (defPath == null)
            {
                return null;
            }
            MDefFiles files = MDefParser.ParseFiles(File.ReadAllText(defPath));

            List<string> stateTexts = new List<string>();
            if (files.St != null)
            {
                foreach (string st in files.St)
                {
                    string p = Path.Combine(dir, st);
                    if (File.Exists(p))
                    {
                        stateTexts.Add(File.ReadAllText(p));
                    }
                }
            }
            string cnsText = ReadIfExists(dir, files.Cns);
            string airText = ReadIfExists(dir, files.Anim);
            string cmdText = ReadIfExists(dir, files.Cmd);
            string common = File.Exists(TestAssets.Common1Cns()) ? File.ReadAllText(TestAssets.Common1Cns()) : null;

            return MCharLoader.Load(stateTexts, cnsText, common, airText, cmdText, name);
        }

        static string ReadIfExists(string dir, string file)
        {
            if (string.IsNullOrEmpty(file))
            {
                return null;
            }
            string p = Path.Combine(dir, file);
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        // 主 .def = [Files] 同时含 cmd 与 cns 的那个（排除 ending/intro 故事板 def）。
        static string PickMainDef(string dir)
        {
            string[] defs = Directory.GetFiles(dir, "*.def");
            string fallback = null;
            foreach (string d in defs)
            {
                MDefFiles f = MDefParser.ParseFiles(File.ReadAllText(d));
                if (!string.IsNullOrEmpty(f.Cns) && !string.IsNullOrEmpty(f.Cmd))
                {
                    return d;
                }
                if (!string.IsNullOrEmpty(f.Cns) && fallback == null)
                {
                    fallback = d;
                }
            }
            return fallback;
        }

        // 仅保留非负状态（隔离各角色命令/AI 负状态，专测受击周期本身）。
        static Dictionary<int, MStateDef> NonNegative(Dictionary<int, MStateDef> src)
        {
            Dictionary<int, MStateDef> outp = new Dictionary<int, MStateDef>();
            foreach (KeyValuePair<int, MStateDef> kv in src)
            {
                if (kv.Key >= 0)
                {
                    outp[kv.Key] = kv.Value;
                }
            }
            return outp;
        }

        static MHitDef StandingPunch()
        {
            return new MHitDef
            {
                Active = true, HitHigh = true, HitLow = true, HitAir = true, HitDown = true,
                HitDamage = 100, P1PauseTime = 4, P2PauseTime = 4,
                GroundHitTime = 6, AirHitTime = 8, GroundSlideTime = 3,
                GroundVelX = F(-4), AnimType = MReaction.Medium, GroundType = MHitType.High,
            };
        }

        [Test, TestCaseSource(nameof(Characters))]
        public void Character_LoadsAndHasGetHitCommonStates(string name)
        {
            MCharData data = LoadChar(name);
            if (data == null)
            {
                Assert.Ignore($"角色 {name} 素材缺失，跳过。");
            }
            Assert.That(data.States.Count, Is.GreaterThan(0), $"{name}: 状态非空");
            Assert.That(data.Anims.ContainsKey(0), Is.True, $"{name}: 含站立动画 0");
            Assert.That(data.CommonStates.ContainsKey(5000), Is.True, $"{name}: 公共态含立受击 5000");
            Assert.That(data.CommonStates.ContainsKey(5001), Is.True, $"{name}: 含滑行 5001");
        }

        [Test, TestCaseSource(nameof(Characters))]
        public void Character_WhenHitStanding_RunsGetHitCycleAndRecovers(string name)
        {
            MCharData data = LoadChar(name);
            if (data == null)
            {
                Assert.Ignore($"角色 {name} 素材缺失，跳过。");
            }

            MChar def = MCharLoader.SpawnChar(data, 2, startStateNo: 0, startAnimNo: 0);
            def.Facing = -FFloat.One;
            def.Pos = new FVector3(F(30), F(0), F(0));
            def.Clsn2 = new[] { new MClsnBox(F(-20), F(-80), F(20), F(0)) };
            def.StateType = 1;   // 站立
            int startLife = def.Life;

            // 合成攻方一拳命中（直接走 MHitSystem，绕过攻方动画/命令）。
            MChar atk = new MChar
            {
                Id = 1, Facing = FFloat.One, StateType = 1, Life = 1000, LifeMax = 1000,
                Pos = new FVector3(F(0), F(0), F(0)), Clsn1 = new[] { new MClsnBox(F(10), F(-80), F(40), F(0)) },
                HitDef = StandingPunch(),
            };
            Assert.IsTrue(MHitSystem.TryHit(atk, def), $"{name}: 站立被普攻命中");
            Assert.That(def.PendingStateNo, Is.EqualTo(5000), $"{name}: 路由进立受击 5000");

            // 用角色自身状态（非负）+ 标准公共态（非负）跑受击周期。
            Dictionary<int, MStateDef> states = NonNegative(data.States);
            Dictionary<int, MStateDef> common = NonNegative(data.CommonStates);

            MStateMachine sm = new MStateMachine();
            bool sawGetHit = false;
            bool recovered = false;
            for (int f = 0; f < 90; f++)
            {
                sm.RunFrame(def, states, common);
                if (def.StateNo >= 5000 && def.StateNo <= 5160)
                {
                    sawGetHit = true;
                }
                else if (sawGetHit && def.StateNo < 5000)
                {
                    recovered = true;
                    break;
                }
            }
            Assert.IsTrue(sawGetHit, $"{name}: 进入受击状态机 5000-5160");
            // 实际伤害 = 100 ÷ finalDefense（finalDefense = 角色 [Data] defence / 100）；
            // 多数角色 defence=100 → 扣 100，少数（如 Final defence=200）按其防御常量半伤。
            int defenceBase = def.Constants != null ? def.Constants.Defence : 100;
            int expectedDamage = (int)System.Math.Round(10000.0 / defenceBase, System.MidpointRounding.AwayFromZero);
            Assert.That(def.Life, Is.EqualTo(startLife - expectedDamage),
                $"{name}: 受击掉 {expectedDamage} 血（按 defence={defenceBase} 缩放）");
            Assert.IsTrue(recovered, $"{name}: 受击周期走完，脱离受击态恢复（StateNo<5000）");
        }
    }
}
