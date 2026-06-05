// Behavior-faithful CNS parser for the ported Mugen engine.
// 把 MUGEN .cns 文本解析为 MStateDef + 实例化的 MStateController(M5) + 编译的 MTriggerSet(M4)。
// 触发语义: triggerall 全 AND; 同号 triggerN 多行 AND; 组间 OR。表达式走 MugenExprCompiler(M2)。
// See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using System.Globalization;
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.State;
using Lockstep.Mugen.StateCtrl;

namespace Lockstep.Mugen.Parse
{
    /// <summary>MUGEN .cns → 状态表(MStateDef)。容错：未知控制器降级为 Null、未知 trigger 经编译器降级为 0。</summary>
    public static class MugenCnsParser
    {
        enum Mode { None, Statedef, State }

        public static Dictionary<int, MStateDef> Parse(string text)
        {
            MugenExprCompiler comp = new MugenExprCompiler();
            Dictionary<int, MStateDef> states = new Dictionary<int, MStateDef>();

            Mode mode = Mode.None;
            MStateDef current = null;

            string ctrlType = null;
            List<string> triggerAll = new List<string>();
            SortedDictionary<int, List<string>> triggerGroups = new SortedDictionary<int, List<string>>();
            Dictionary<string, string> p = new Dictionary<string, string>();

            string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string raw in lines)
            {
                string line = StripComment(raw).Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (line[0] == '[')
                {
                    int rb = line.IndexOf(']');
                    string header = (rb > 0 ? line.Substring(1, rb - 1) : line.Substring(1)).Trim();
                    string headerLower = header.ToLowerInvariant();

                    FlushController(comp, current, ref ctrlType, triggerAll, triggerGroups, p);

                    if (headerLower.StartsWith("statedef"))
                    {
                        current = new MStateDef { No = ParseTrailingInt(header) };
                        states[current.No] = current;
                        mode = Mode.Statedef;
                    }
                    else if (headerLower.StartsWith("state ") || headerLower == "state")
                    {
                        mode = current != null ? Mode.State : Mode.None;
                    }
                    else
                    {
                        current = null;
                        mode = Mode.None;
                    }
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq < 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();

                if (mode == Mode.Statedef && current != null)
                {
                    ApplyStatedefField(comp, current, key, val);
                }
                else if (mode == Mode.State)
                {
                    if (key == "type")
                    {
                        ctrlType = val.ToLowerInvariant();
                    }
                    else if (key == "triggerall")
                    {
                        triggerAll.Add(val);
                    }
                    else if (key.StartsWith("trigger") && key.Length > 7 &&
                             int.TryParse(key.Substring(7), NumberStyles.Integer, CultureInfo.InvariantCulture, out int gi))
                    {
                        if (!triggerGroups.TryGetValue(gi, out List<string> g))
                        {
                            g = new List<string>();
                            triggerGroups[gi] = g;
                        }
                        g.Add(val);
                    }
                    else
                    {
                        p[key] = val;
                    }
                }
            }

            FlushController(comp, current, ref ctrlType, triggerAll, triggerGroups, p);
            return states;
        }

        // ───────── 状态头部 ─────────
        // type/movetype/physics 是字母枚举（字面量码）；其余参数在 MUGEN 可为表达式（anim=40+var(11)），
        // 编译后存 BytecodeExp，进入状态时由 MStateDef.RunInit 用角色上下文求值（对齐 Ikemen stateDef.Run）。
        static void ApplyStatedefField(MugenExprCompiler comp, MStateDef s, string key, string val)
        {
            switch (key)
            {
                case "type": s.StateType = MugenCodes.StateType(val); break;
                case "movetype": s.MoveType = MugenCodes.MoveType(val); break;
                case "physics": s.Physics = MugenCodes.Physics(val); break;
                case "ctrl": s.Ctrl = comp.Compile(val); break;
                case "anim": s.Anim = comp.Compile(val); break;
                case "facep2": s.Facep2 = comp.Compile(val); break;
                case "juggle": s.Juggle = comp.Compile(val); break;
                case "poweradd": s.PowerAdd = comp.Compile(val); break;
                case "velset":
                {
                    string[] parts = val.Split(',');
                    if (parts.Length > 0 && parts[0].Trim().Length > 0) { s.VelSetX = comp.Compile(parts[0].Trim()); }
                    if (parts.Length > 1 && parts[1].Trim().Length > 0) { s.VelSetY = comp.Compile(parts[1].Trim()); }
                    if (parts.Length > 2 && parts[2].Trim().Length > 0) { s.VelSetZ = comp.Compile(parts[2].Trim()); }
                    break;
                }
            }
        }

        // ───────── 控制器收尾 ─────────
        static void FlushController(MugenExprCompiler comp, MStateDef state, ref string ctrlType,
            List<string> triggerAll, SortedDictionary<int, List<string>> triggerGroups,
            Dictionary<string, string> p)
        {
            if (ctrlType != null && state != null)
            {
                MStateController c = BuildController(comp, ctrlType, p);
                if (c != null)
                {
                    c.Triggers = BuildTriggers(comp, triggerAll, triggerGroups);
                    c.Persistent = ParseFirstInt(Get(p, "persistent"), 1);
                    c.IgnoreHitPause = ParseFirstInt(Get(p, "ignorehitpause"), 0) != 0;
                    state.Controllers.Add(c);
                }
            }
            ctrlType = null;
            triggerAll.Clear();
            triggerGroups.Clear();
            p.Clear();
        }

        static MTriggerSet BuildTriggers(MugenExprCompiler comp, List<string> triggerAll,
            SortedDictionary<int, List<string>> triggerGroups)
        {
            MTriggerSet set = new MTriggerSet();
            for (int i = 0; i < triggerAll.Count; i++)
            {
                set.TriggerAll.Add(comp.Compile(triggerAll[i]));
            }
            foreach (KeyValuePair<int, List<string>> g in triggerGroups)
            {
                List<BytecodeExp> group = new List<BytecodeExp>();
                for (int i = 0; i < g.Value.Count; i++)
                {
                    group.Add(comp.Compile(g.Value[i]));
                }
                set.Groups.Add(group);
            }
            return set;
        }

        // ───────── 控制器工厂 ─────────
        static MStateController BuildController(MugenExprCompiler comp, string type, Dictionary<string, string> p)
        {
            switch (type)
            {
                case "null": return new NullController();
                case "changestate":
                    return new ChangeStateController { Value = Expr(comp, p, "value"), Ctrl = IntP(p, "ctrl", -1), Anim = IntP(p, "anim", -1) };
                case "selfstate":
                    return new SelfStateController { Value = Expr(comp, p, "value"), Ctrl = IntP(p, "ctrl", -1), Anim = IntP(p, "anim", -1) };
                case "velset": return new VelSetController { X = Expr(comp, p, "x"), Y = Expr(comp, p, "y") };
                case "veladd": return new VelAddController { X = Expr(comp, p, "x"), Y = Expr(comp, p, "y") };
                case "velmul": return new VelMulController { X = Expr(comp, p, "x"), Y = Expr(comp, p, "y"), Z = Expr(comp, p, "z") };
                case "posset": return new PosSetController { X = Expr(comp, p, "x"), Y = Expr(comp, p, "y") };
                case "posadd": return new PosAddController { X = Expr(comp, p, "x"), Y = Expr(comp, p, "y") };
                case "changeanim": return new ChangeAnimController { Value = Expr(comp, p, "value") };
                case "ctrlset": return new CtrlSetController { Value = Expr(comp, p, "value") };
                case "varset": return BuildVarSet(comp, p, false);
                case "varadd": return BuildVarSet(comp, p, true);
                case "statetypeset":
                    return new StateTypeSetController
                    {
                        StateType = p.ContainsKey("statetype") ? MugenCodes.StateType(p["statetype"]) : -1,
                        MoveType = p.ContainsKey("movetype") ? MugenCodes.MoveType(p["movetype"]) : -1,
                        Physics = p.ContainsKey("physics") ? MugenCodes.Physics(p["physics"]) : -1,
                        CtrlExpr = Expr(comp, p, "ctrl"),
                    };
                case "lifeadd": return new LifeAddController { Value = Expr(comp, p, "value"), Kill = IntP(p, "kill", 1) != 0 };
                case "lifeset": return new LifeSetController { Value = Expr(comp, p, "value") };
                case "poweradd": return new PowerAddController { Value = Expr(comp, p, "value") };
                case "powerset": return new PowerSetController { Value = Expr(comp, p, "value") };
                case "attackmulset":
                    return new AttackMulSetController { Value = Expr(comp, p, "value"), Damage = Expr(comp, p, "damage") };
                case "defencemulset":
                case "defensemulset":
                    return new DefenceMulSetController
                    {
                        Value = Expr(comp, p, "value"),
                        OnHit = Expr(comp, p, "onhit"),
                        MulType = Expr(comp, p, "multype"),
                    };
                case "turn": return new TurnController();
                case "assertspecial": return new AssertSpecialController { Flags = ParseFlags(p) };
                case "hitby": return BuildHitBy(p, false);
                case "nothitby": return BuildHitBy(p, true);
                case "hitdef": return new HitDefController { Template = BuildHitDef(comp, p) };
                case "hitvelset": return new HitVelSetController { X = Expr(comp, p, "x"), Y = Expr(comp, p, "y"), Z = Expr(comp, p, "z") };
                case "hitadd": return new HitAddController { Value = Expr(comp, p, "value") };
                case "hitfallset":
                    return new HitFallSetController
                    {
                        Value = Expr(comp, p, "value"),
                        XVelocity = Expr(comp, p, "xvel"),
                        YVelocity = Expr(comp, p, "yvel"),
                        ZVelocity = Expr(comp, p, "zvel"),
                    };
                case "hitfallvel": return new HitFallVelController();
                case "hitfalldamage": return new HitFallDamageController();
                case "gravity": return new GravityController();
                case "pause":
                    return WithParams(new PauseController { Time = Expr(comp, p, "time"), MoveTime = Expr(comp, p, "movetime") }, p);
                case "superpause":
                    return WithParams(new SuperPauseController
                    {
                        Time = Expr(comp, p, "time"),
                        MoveTime = Expr(comp, p, "movetime"),
                        PowerAdd = Expr(comp, p, "poweradd"),
                        P2DefMul = Expr(comp, p, "p2defmul"),
                    }, p);
                case "posfreeze":
                    return WithParams(new PosFreezeController { Value = Expr(comp, p, "value") }, p);
                case "width":
                    return WithParams(new WidthController
                    {
                        Value = ExprList(comp, p, "value"),
                        Player = ExprList(comp, p, "player"),
                        Edge = ExprList(comp, p, "edge"),
                    }, p);
                case "playerpush":
                    return WithParams(new PlayerPushController
                    {
                        Value = Expr(comp, p, "value"),
                        Priority = Expr(comp, p, "priority"),
                        AffectTeam = Expr(comp, p, "affectteam"),
                    }, p);
                case "screenbound":
                    return WithParams(new ScreenBoundController
                    {
                        Value = Expr(comp, p, "value"),
                        MoveCamera = ExprList(comp, p, "movecamera"),
                        StageBound = Expr(comp, p, "stagebound"),
                    }, p);
                case "attackdist":
                    return WithParams(new AttackDistController
                    {
                        XValues = ExprList(comp, p, "x"),
                        YValues = ExprList(comp, p, "y"),
                        ZValues = ExprList(comp, p, "z"),
                    }, p);
                case "hitoverride":
                    return WithParams(new HitOverrideController
                    {
                        Attr = p.TryGetValue("attr", out string hitOverrideAttr) ? MugenCodes.Attr(hitOverrideAttr) : 0,
                        Slot = Expr(comp, p, "slot"),
                        StateNo = Expr(comp, p, "stateno"),
                        Time = Expr(comp, p, "time"),
                        ForceAir = Expr(comp, p, "forceair"),
                        ForceGuard = Expr(comp, p, "forceguard"),
                        KeepState = Expr(comp, p, "keepstate"),
                    }, p);
                case "movehitreset": return new MoveHitResetController();
                case "reversaldef":
                    return WithParams(new ReversalDefController
                    {
                        Attr = p.TryGetValue("attr", out string reversalAttr) ? MugenCodes.Attr(reversalAttr) : 0,
                    }, p);
                case "varrandom":
                    return new VarRandomController { Index = Expr(comp, p, "v"), Range = ExprList(comp, p, "range") };
                case "varrangeset":
                    return new VarRangeSetController
                    {
                        First = Expr(comp, p, "first"),
                        Last = Expr(comp, p, "last"),
                        Value = Expr(comp, p, "value"),
                        FloatValue = Expr(comp, p, "fvalue"),
                    };
                case "remappal":
                    return WithParams(new RemapPalController { Source = ExprList(comp, p, "source"), Dest = ExprList(comp, p, "dest") }, p);
                case "trans": return WithParams(new TransController(), p);
                case "sprpriority":
                    return WithParams(new SprPriorityController { Value = Expr(comp, p, "value"), LayerNo = Expr(comp, p, "layerno") }, p);
                case "offset":
                    return WithParams(new OffsetController { XOffset = Expr(comp, p, "x"), YOffset = Expr(comp, p, "y") }, p);
                case "angledraw":
                    return WithParams(new AngleDrawController
                    {
                        Value = Expr(comp, p, "value"),
                        XAngle = Expr(comp, p, "x"),
                        YAngle = Expr(comp, p, "y"),
                        Scale = ExprList(comp, p, "scale"),
                    }, p);
                case "angleset":
                    return WithParams(new AngleSetController { Value = Expr(comp, p, "value"), XAngle = Expr(comp, p, "x"), YAngle = Expr(comp, p, "y") }, p);
                case "angleadd":
                    return WithParams(new AngleAddController { Value = Expr(comp, p, "value"), XAngle = Expr(comp, p, "x"), YAngle = Expr(comp, p, "y") }, p);
                case "anglemul":
                    return WithParams(new AngleMulController { Value = Expr(comp, p, "value"), XAngle = Expr(comp, p, "x"), YAngle = Expr(comp, p, "y") }, p);
                case "afterimage": return WithParams(new AfterImageController { Time = Expr(comp, p, "time") }, p);
                case "afterimagetime": return WithParams(new AfterImageTimeController { Time = Expr(comp, p, "time") }, p);
                case "palfx": return WithParams(new PalFXController { Time = Expr(comp, p, "time") }, p);
                case "allpalfx": return WithParams(new AllPalFXController { Time = Expr(comp, p, "time") }, p);
                case "bgpalfx": return WithParams(new BGPalFXController { Time = Expr(comp, p, "time") }, p);
                case "envcolor":
                    return WithParams(new EnvColorController { Value = ExprList(comp, p, "value"), Time = Expr(comp, p, "time"), Under = Expr(comp, p, "under") }, p);
                case "playsnd": return WithParams(new PlaySndController(), p);
                case "stopsnd": return WithParams(new StopSndController(), p);
                case "sndpan": return WithParams(new SndPanController(), p);
                case "explod": return WithParams(new ExplodController(), p);
                case "modifyexplod": return WithParams(new ModifyExplodController(), p);
                case "removeexplod":
                    return WithParams(new RemoveExplodController { Id = Expr(comp, p, "id"), Index = Expr(comp, p, "index") }, p);
                case "makedust": return WithParams(new MakeDustController(), p);
                case "gamemakeanim": return WithParams(new GameMakeAnimController(), p);
                case "envshake": return WithParams(new EnvShakeController(), p);
                case "fallenvshake": return WithParams(new FallEnvShakeController(), p);
                case "forcefeedback": return WithParams(new ForceFeedbackController(), p);
                case "displaytoclipboard": return WithParams(new DisplayToClipboardController(), p);
                case "victoryquote": return WithParams(new VictoryQuoteController { Value = Expr(comp, p, "value") }, p);
                default: return new NullController();   // 未知控制器降级（容错）
            }
        }

        // VarSet/VarAdd：支持 v=N value=expr(int) / fv=N value=expr(float) / var(N)=expr / fvar(N)=expr
        static MStateController BuildVarSet(MugenExprCompiler comp, Dictionary<string, string> p, bool isAdd)
        {
            int index = 0;
            bool isFloat = false;
            BytecodeExp value = null;

            if (p.ContainsKey("v")) { index = ParseFirstInt(p["v"], 0); isFloat = false; value = Expr(comp, p, "value"); }
            else if (p.ContainsKey("fv")) { index = ParseFirstInt(p["fv"], 0); isFloat = true; value = Expr(comp, p, "value"); }
            else
            {
                foreach (KeyValuePair<string, string> kv in p)
                {
                    if (kv.Key.StartsWith("var(") || kv.Key.StartsWith("fvar("))
                    {
                        isFloat = kv.Key.StartsWith("fvar(");
                        index = ExtractParenInt(kv.Key);
                        value = comp.Compile(kv.Value);
                        break;
                    }
                }
            }

            if (isAdd)
            {
                return new VarAddController { Index = index, IsFloat = isFloat, Value = value };
            }
            return new VarSetController { Index = index, IsFloat = isFloat, Value = value };
        }

        // HitBy/NotHitBy：value=攻击类别(SCA, AA 形式)、time=持续帧。解析为常量塞控制器。
        static MStateController BuildHitBy(Dictionary<string, string> p, bool isNot)
        {
            int attr = p.TryGetValue("value", out string v) ? MugenCodes.Attr(v) : (int)MAttackType.All;
            int time = IntP(p, "time", 1);   // MUGEN 默认 time=1
            return new HitByController { Attr = attr, Time = time, IsNot = isNot };
        }

        // HitDef：解析核心字段(表达式按常量求值，适配字面量参数)。
        static MHitDef BuildHitDef(MugenExprCompiler comp, Dictionary<string, string> p)
        {
            MHitDef hd = new MHitDef();
            if (p.TryGetValue("attr", out string attr)) { hd.Attr = MugenCodes.Attr(attr); }
            if (p.TryGetValue("guardflag", out string gf))
            {
                MugenCodes.HitFlags(gf, out hd.GuardHigh, out hd.GuardLow, out hd.GuardAir, out _);
            }
            if (p.TryGetValue("guard.velocity", out string gv)) { hd.GuardVelX = EvalF(comp, gv.Split(',')[0]); }
            if (p.TryGetValue("guard.hittime", out string ghtt)) { hd.GuardHitTime = EvalI(comp, ghtt); }
            if (p.TryGetValue("guard.ctrltime", out string gct)) { hd.GuardCtrlTime = EvalI(comp, gct); }
            if (p.TryGetValue("hitflag", out string hf))
            {
                MugenCodes.HitFlags(hf, out hd.HitHigh, out hd.HitLow, out hd.HitAir, out hd.HitDown);
            }
            if (p.TryGetValue("damage", out string dmg))
            {
                string[] d = dmg.Split(',');
                hd.HitDamage = EvalI(comp, d[0]);
                hd.GuardDamage = d.Length > 1 ? EvalI(comp, d[1]) : hd.HitDamage;
            }
            if (p.TryGetValue("pausetime", out string pt))
            {
                string[] t = pt.Split(',');
                hd.P1PauseTime = EvalI(comp, t[0]);
                hd.P2PauseTime = t.Length > 1 ? EvalI(comp, t[1]) : hd.P1PauseTime;
            }
            ParseVel(comp, p, "ground.velocity", out hd.GroundVelX, out hd.GroundVelY);
            ParseVel(comp, p, "air.velocity", out hd.AirVelX, out hd.AirVelY);
            if (p.TryGetValue("ground.hittime", out string ght)) { hd.GroundHitTime = EvalI(comp, ght); }
            if (p.TryGetValue("air.hittime", out string aht)) { hd.AirHitTime = EvalI(comp, aht); }
            if (p.TryGetValue("ground.slidetime", out string gst)) { hd.GroundSlideTime = EvalI(comp, gst); }
            if (p.TryGetValue("animtype", out string at)) { hd.AnimType = ParseReaction(at); }
            if (p.TryGetValue("ground.type", out string gt)) { hd.GroundType = ParseHitType(gt); }
            if (p.TryGetValue("fall", out string fl)) { hd.Fall = EvalI(comp, fl) != 0; }
            if (p.TryGetValue("p1stateno", out string p1s)) { hd.P1StateNo = EvalI(comp, p1s); }
            if (p.TryGetValue("p2stateno", out string p2s)) { hd.P2StateNo = EvalI(comp, p2s); }
            if (p.TryGetValue("numhits", out string nh)) { hd.NumHits = EvalI(comp, nh); }
            if (p.TryGetValue("hitonce", out string ho)) { hd.HitOnce = EvalI(comp, ho); }
            // hitonce 默认：未显式给出（-1）时，投技(attr 含 throw 类)默认 1，否则 0（char.go:848）。
            if (hd.HitOnce < 0)
            {
                hd.HitOnce = (hd.Attr & (int)MAttackType.AT) != 0 ? 1 : 0;
            }

            // 空中/fall 反应字段（air.type 默认随 ground.type，char.go:907；air.animtype 默认随 animtype）。
            hd.AirType = hd.GroundType;
            hd.AirAnimType = hd.AnimType;
            if (p.TryGetValue("air.type", out string aty)) { hd.AirType = ParseHitType(aty); }
            if (p.TryGetValue("air.animtype", out string aat)) { hd.AirAnimType = ParseReaction(aat); }
            // fall.animtype 默认：air.animtype 为 Up 则 Up，否则 Back（MUGEN 规范）。
            hd.FallAnimType = hd.AirAnimType == MReaction.Up ? MReaction.Up : MReaction.Back;
            if (p.TryGetValue("fall.animtype", out string fat)) { hd.FallAnimType = ParseReaction(fat); }
            if (p.TryGetValue("yaccel", out string ya)) { hd.YAccel = EvalF(comp, ya); }
            if (p.TryGetValue("fall.xvelocity", out string fxv)) { hd.FallXVel = EvalF(comp, fxv); }
            if (p.TryGetValue("fall.yvelocity", out string fyv)) { hd.FallYVel = EvalF(comp, fyv); }
            if (p.TryGetValue("fall.recover", out string frc)) { hd.FallRecover = EvalI(comp, frc) != 0; }
            if (p.TryGetValue("fall.recovertime", out string frt)) { hd.FallRecoverTime = EvalI(comp, frt); }
            if (p.TryGetValue("fall.damage", out string fdmg)) { hd.FallDamage = EvalI(comp, fdmg); }

            // 击打倒地分支：down.velocity 默认随 air.velocity（char.go:892-894）；down.hittime 默认 20；down.bounce 默认 0。
            hd.DownVelX = hd.AirVelX;
            hd.DownVelY = hd.AirVelY;
            ParseVel(comp, p, "down.velocity", out FFloat dvx, out FFloat dvy);
            if (p.ContainsKey("down.velocity")) { hd.DownVelX = dvx; hd.DownVelY = dvy; }
            if (p.TryGetValue("down.hittime", out string dht)) { hd.DownHitTime = EvalI(comp, dht); }
            if (p.TryGetValue("down.bounce", out string db)) { hd.DownBounce = EvalI(comp, db) != 0; }

            // forcestand：默认 = 有 Y 击退速度（char.go:911）。
            hd.ForceStand = hd.GroundVelY != FFloat.Zero;
            if (p.TryGetValue("forcestand", out string fs)) { hd.ForceStand = EvalI(comp, fs) != 0; }

            // KO 阻止标志（默认全 1，char.go:774/775/792）。
            if (p.TryGetValue("kill", out string kl)) { hd.Kill = EvalI(comp, kl) != 0; }
            if (p.TryGetValue("guard.kill", out string gkl)) { hd.GuardKill = EvalI(comp, gkl) != 0; }
            if (p.TryGetValue("fall.kill", out string fkl)) { hd.FallKill = EvalI(comp, fkl) != 0; }

            FillPowerDefaults(comp, p, hd);
            return hd;
        }

        // 能量获取/给予（char.go:931-961）：getpower/givepower 各为 "命中值[,被防值]"；
        // 未显式给出则按 lifetopowermul 常量推默认：命中攻方 0.7×dmg（超必杀 0）、命中守方 0.6×dmg、
        // 被防值 = 命中值×0.5。整数截断（Go int32() 向零取整）。
        static void FillPowerDefaults(MugenExprCompiler comp, Dictionary<string, string> p, MHitDef hd)
        {
            bool hyper = (hd.Attr & (int)MAttackType.Hyper) != 0;
            int defHitGet = hyper ? 0 : TruncMul(hd.HitDamage, 7, 10);   // default.attack.lifetopowermul=0.7（超必杀 super=0）
            int defHitGive = TruncMul(hd.HitDamage, 6, 10);              // default/super.gethit.lifetopowermul=0.6

            hd.HitGetPower = defHitGet;
            hd.HitGivePower = defHitGive;
            bool getGuardSet = false;
            bool giveGuardSet = false;
            if (p.TryGetValue("getpower", out string gp))
            {
                string[] parts = gp.Split(',');
                hd.HitGetPower = EvalI(comp, parts[0]);
                if (parts.Length > 1) { hd.GuardGetPower = EvalI(comp, parts[1]); getGuardSet = true; }
            }
            if (p.TryGetValue("givepower", out string gvp))
            {
                string[] parts = gvp.Split(',');
                hd.HitGivePower = EvalI(comp, parts[0]);
                if (parts.Length > 1) { hd.GuardGivePower = EvalI(comp, parts[1]); giveGuardSet = true; }
            }
            if (!getGuardSet) { hd.GuardGetPower = TruncMul(hd.HitGetPower, 1, 2); }    // ×0.5
            if (!giveGuardSet) { hd.GuardGivePower = TruncMul(hd.HitGivePower, 1, 2); } // ×0.5
        }

        // 整数 value×num/den 向零取整（对齐 Go int32(float×int) 截断语义；value 可负）。
        static int TruncMul(int value, int num, int den)
        {
            long scaled = (long)value * num;
            return (int)(scaled / den);
        }

        static void ParseVel(MugenExprCompiler comp, Dictionary<string, string> p, string key, out FFloat x, out FFloat y)
        {
            x = FFloat.Zero;
            y = FFloat.Zero;
            if (p.TryGetValue(key, out string v))
            {
                string[] parts = v.Split(',');
                x = EvalF(comp, parts[0]);
                if (parts.Length > 1) { y = EvalF(comp, parts[1]); }
            }
        }

        static MReaction ParseReaction(string v)
        {
            switch (v.Trim().ToLowerInvariant())
            {
                case "medium": return MReaction.Medium;
                case "hard": return MReaction.Hard;
                case "back": return MReaction.Back;
                case "up": return MReaction.Up;
                case "diagup": return MReaction.DiagUp;
                default: return MReaction.Light;
            }
        }

        static MHitType ParseHitType(string v)
        {
            switch (v.Trim().ToLowerInvariant())
            {
                case "low": return MHitType.Low;
                case "trip": return MHitType.Trip;
                case "none": return MHitType.None;
                default: return MHitType.High;
            }
        }

        static int EvalI(MugenExprCompiler comp, string expr)
        {
            return comp.Compile(expr.Trim()).Run(null).ToI();
        }

        static FFloat EvalF(MugenExprCompiler comp, string expr)
        {
            return comp.Compile(expr.Trim()).Run(null).ToF();
        }

        static int ParseFlags(Dictionary<string, string> p)
        {
            int flags = 0;
            foreach (KeyValuePair<string, string> kv in p)
            {
                if (kv.Key == "flag" || (kv.Key.StartsWith("flag") && kv.Key.Length > 4))
                {
                    flags |= FlagCode(kv.Value.Trim().ToLowerInvariant());
                }
            }
            return flags;
        }

        static int FlagCode(string name)
        {
            switch (name)
            {
                case "intro": return (int)MAssertFlag.Intro;
                case "invisible": return (int)MAssertFlag.Invisible;
                case "noautoturn": return (int)MAssertFlag.NoAutoTurn;
                case "nostandguard": return (int)MAssertFlag.NoStandGuard;
                case "nocrouchguard": return (int)MAssertFlag.NoCrouchGuard;
                case "noairguard": return (int)MAssertFlag.NoAirGuard;
                case "nowalk": return (int)MAssertFlag.NoWalk;
                case "nojugglecheck": return (int)MAssertFlag.NoJuggleCheck;
                case "unguardable": return (int)MAssertFlag.Unguardable;
                case "noko": return (int)MAssertFlag.NoKO;
                case "noshadow": return (int)MAssertFlag.NoShadow;
                case "noautoguard": return (int)MAssertFlag.NoAutoGuard;
                case "nojump": return (int)MAssertFlag.NoJump;
                case "nocrouch": return (int)MAssertFlag.NoCrouch;
                case "nostand": return (int)MAssertFlag.NoStand;
                case "noairjump": return (int)MAssertFlag.NoAirJump;
                case "nobrake": return (int)MAssertFlag.NoBrake;
                case "nohardcodedkeys": return (int)MAssertFlag.NoHardcodedKeys;
                case "postroundinput": return (int)MAssertFlag.PostRoundInput;
                default: return 0;
            }
        }

        // ───────── 取值/编译辅助 ─────────
        static BytecodeExp Expr(MugenExprCompiler comp, Dictionary<string, string> p, string key)
        {
            return p.TryGetValue(key, out string v) ? comp.Compile(v) : null;
        }

        static BytecodeExp[] ExprList(MugenExprCompiler comp, Dictionary<string, string> p, string key)
        {
            if (!p.TryGetValue(key, out string v))
            {
                return null;
            }
            string[] parts = v.Split(',');
            BytecodeExp[] values = new BytecodeExp[parts.Length];
            for (int index = 0; index < parts.Length; index++)
            {
                values[index] = comp.Compile(parts[index].Trim());
            }
            return values;
        }

        static T WithParams<T>(T controller, Dictionary<string, string> p) where T : ParameterOnlyController
        {
            controller.Parameters = new Dictionary<string, string>(p);
            return controller;
        }

        static string Get(Dictionary<string, string> p, string key)
        {
            return p.TryGetValue(key, out string v) ? v : null;
        }

        static int IntP(Dictionary<string, string> p, string key, int def)
        {
            return p.TryGetValue(key, out string v) ? ParseFirstInt(v, def) : def;
        }

        static int ParseFirstInt(string v, int def)
        {
            if (string.IsNullOrEmpty(v))
            {
                return def;
            }
            string first = v.Split(',')[0].Trim();
            return int.TryParse(first, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : def;
        }

        static int ExtractParenInt(string key)
        {
            int lp = key.IndexOf('(');
            int rp = key.IndexOf(')');
            if (lp >= 0 && rp > lp)
            {
                return int.TryParse(key.Substring(lp + 1, rp - lp - 1).Trim(), out int r) ? r : 0;
            }
            return 0;
        }

        static int ParseTrailingInt(string header)
        {
            int i = header.Length - 1;
            while (i >= 0 && (char.IsDigit(header[i]) || header[i] == '-'))
            {
                i--;
            }
            string tail = header.Substring(i + 1).Trim();
            return int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out int r) ? r : 0;
        }

        static string StripComment(string line)
        {
            int semi = line.IndexOf(';');
            return semi >= 0 ? line.Substring(0, semi) : line;
        }
    }
}
