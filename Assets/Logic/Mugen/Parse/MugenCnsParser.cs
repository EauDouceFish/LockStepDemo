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
            return Parse(text, null);
        }

        public static Dictionary<int, MStateDef> Parse(string text, MCompatibilityReport report)
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

                    FlushController(comp, current, ref ctrlType, triggerAll, triggerGroups, p, report);

                    if (headerLower.StartsWith("statedef"))
                    {
                        current = new MStateDef { No = ParseStateHeaderNo(header) };
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

            FlushController(comp, current, ref ctrlType, triggerAll, triggerGroups, p, report);
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
            Dictionary<string, string> p, MCompatibilityReport report)
        {
            if (ctrlType != null && state != null)
            {
                MStateController c = BuildController(comp, ctrlType, p, report);
                if (c != null)
                {
                    if (report != null && c is ParameterOnlyController &&
                        c.GetType().GetMethod(nameof(MStateController.Run)).DeclaringType == typeof(ParameterOnlyController))
                    {
                        report.AddParsedOnlyController(ctrlType);
                    }
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
        static MStateController BuildController(MugenExprCompiler comp, string type, Dictionary<string, string> p,
            MCompatibilityReport report)
        {
            switch (type)
            {
                case "null": return new NullController();
                case "helper":
                {
                    BytecodeExp[] helperPos = ExprList(comp, p, "pos");
                    return new HelperController
                    {
                        StateNo = Expr(comp, p, "stateno"),
                        Id = Expr(comp, p, "id"),
                        PosX = helperPos != null && helperPos.Length > 0 ? helperPos[0] : null,
                        PosY = helperPos != null && helperPos.Length > 1 ? helperPos[1] : null,
                        PosType = Expr(comp, p, "postype"),
                        Facing = Expr(comp, p, "facing"),
                        KeyCtrl = Expr(comp, p, "keyctrl"),
                    };
                }
                case "destroyself": return new DestroySelfController();
                case "parentvarset": return BuildRelayVar(comp, p, MVarTarget.Parent, false);
                case "parentvaradd": return BuildRelayVar(comp, p, MVarTarget.Parent, true);
                case "rootvarset": return BuildRelayVar(comp, p, MVarTarget.Root, false);
                case "rootvaradd": return BuildRelayVar(comp, p, MVarTarget.Root, true);
                case "projectile":
                {
                    BytecodeExp[] vel = ExprList(comp, p, "velocity");
                    BytecodeExp[] accel = ExprList(comp, p, "accel");
                    BytecodeExp[] off = ExprList(comp, p, "offset");
                    return new ProjectileController
                    {
                        ProjId = Expr(comp, p, "projid"),
                        VelX = vel != null && vel.Length > 0 ? vel[0] : null,
                        VelY = vel != null && vel.Length > 1 ? vel[1] : null,
                        AccelX = accel != null && accel.Length > 0 ? accel[0] : null,
                        AccelY = accel != null && accel.Length > 1 ? accel[1] : null,
                        PosX = off != null && off.Length > 0 ? off[0] : null,
                        PosY = off != null && off.Length > 1 ? off[1] : null,
                        RemoveTime = Expr(comp, p, "projremovetime"),
                        ProjAnim = Expr(comp, p, "projanim"),
                        HitDef = BuildHitDef(comp, p),   // 弹幕 HitDef 从同段 hitdef 参数构建
                    };
                }
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
                case "changeanim2":
                    return new ChangeAnim2Controller { Value = Expr(comp, p, "value"), Elem = Expr(comp, p, "elem"), ElemTime = Expr(comp, p, "elemtime") };
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
                    return WithParams(new PauseController
                    {
                        Time = Expr(comp, p, "time"),
                        MoveTime = Expr(comp, p, "movetime"),
                        PauseBg = Expr(comp, p, "pausebg"),
                        EndCmdBufTime = Expr(comp, p, "endcmdbuftime"),
                    }, p);
                case "superpause":
                    return WithParams(new SuperPauseController
                    {
                        Time = Expr(comp, p, "time"),
                        MoveTime = Expr(comp, p, "movetime"),
                        PauseBg = Expr(comp, p, "pausebg"),
                        EndCmdBufTime = Expr(comp, p, "endcmdbuftime"),
                        Darken = Expr(comp, p, "darken"),
                        Brightness = Expr(comp, p, "brightness"),
                        Anim = ExprList(comp, p, "anim"),
                        Position = ExprList(comp, p, "pos"),
                        PowerAdd = Expr(comp, p, "poweradd"),
                        P2DefMul = Expr(comp, p, "p2defmul"),
                        Unhittable = Expr(comp, p, "unhittable"),
                        Sound = ExprList(comp, p, "sound"),
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
                        GuardFlag = Expr(comp, p, "guardflag"),
                        GuardFlagNot = Expr(comp, p, "guardflag.not"),
                        ForceAir = Expr(comp, p, "forceair"),
                        ForceGuard = Expr(comp, p, "forceguard"),
                        KeepState = Expr(comp, p, "keepstate"),
                    }, p);
                case "movehitreset": return new MoveHitResetController();
                case "reversaldef":
                    return WithParams(new ReversalDefController
                    {
                        Attr = p.TryGetValue("attr", out string reversalAttr) ? MugenCodes.Attr(reversalAttr) : 0,
                        GuardFlag = p.TryGetValue("guardflag", out string reversalGuardFlag) ? ParseHitFlagMask(reversalGuardFlag) : 0,
                        GuardFlagNot = p.TryGetValue("guardflag.not", out string reversalGuardFlagNot) ? ParseHitFlagMask(reversalGuardFlagNot) : 0,
                        Template = BuildHitDef(comp, p),
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
                case "trans": return WithParams(new TransController { Trans = ExprList(comp, p, "trans"), TransText = Get(p, "trans") }, p);
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
                case "afterimage": return WithParams(FillAfterImage(new AfterImageController(), comp, p), p);
                case "afterimagetime": return WithParams(new AfterImageTimeController { Time = Expr(comp, p, "time") }, p);
                case "palfx": return WithParams(FillPalFX(new PalFXController(), comp, p), p);
                case "allpalfx": return WithParams(FillPalFX(new AllPalFXController(), comp, p), p);
                case "bgpalfx":
                    return WithParams(FillPalFX(new BGPalFXController { Id = Expr(comp, p, "id"), Index = Expr(comp, p, "index") }, comp, p), p);
                case "envcolor":
                    return WithParams(new EnvColorController { Value = ExprList(comp, p, "value"), Time = Expr(comp, p, "time"), Under = Expr(comp, p, "under") }, p);
                case "playsnd": return WithParams(FillPlaySnd(new PlaySndController(), comp, p), p);
                case "stopsnd": return WithParams(new StopSndController { Channel = Expr(comp, p, "channel") }, p);
                case "sndpan":
                    return WithParams(new SndPanController { Channel = Expr(comp, p, "channel"), Pan = Expr(comp, p, "pan"), AbsPan = Expr(comp, p, "abspan") }, p);
                case "explod": return WithParams(FillExplod(new ExplodController(), comp, p), p);
                case "modifyexplod": return WithParams(FillExplod(new ModifyExplodController { Index = Expr(comp, p, "index") }, comp, p), p);
                case "removeexplod":
                    return WithParams(new RemoveExplodController { Id = Expr(comp, p, "id"), Index = Expr(comp, p, "index") }, p);
                case "makedust":
                    return WithParams(new MakeDustController { Spacing = Expr(comp, p, "spacing"), Position = ExprList(comp, p, "pos"), Position2 = ExprList(comp, p, "pos2") }, p);
                case "gamemakeanim":
                    return WithParams(new GameMakeAnimController { Value = ExprList(comp, p, "value"), Position = ExprList(comp, p, "pos"), Under = Expr(comp, p, "under") }, p);
                case "envshake":
                    return WithParams(new EnvShakeController
                    {
                        Time = Expr(comp, p, "time"),
                        Amplitude = Expr(comp, p, "ampl"),
                        Frequency = Expr(comp, p, "freq"),
                        Multiplier = Expr(comp, p, "mul"),
                        Phase = Expr(comp, p, "phase"),
                        Direction = Expr(comp, p, "dir"),
                    }, p);
                case "fallenvshake": return WithParams(new FallEnvShakeController(), p);
                case "forcefeedback":
                    return WithParams(new ForceFeedbackController { Time = Expr(comp, p, "time"), Waveform = Expr(comp, p, "waveform"), Intensity = Expr(comp, p, "intensity") }, p);
                case "displaytoclipboard":
                    return WithParams(new DisplayToClipboardController { Params = ExprList(comp, p, "params"), Text = Expr(comp, p, "text") }, p);
                case "appendtoclipboard":
                    return WithParams(new AppendToClipboardController { Params = ExprList(comp, p, "params"), Text = Expr(comp, p, "text") }, p);
                case "clearclipboard": return WithParams(new ClearClipboardController(), p);
                case "victoryquote": return WithParams(new VictoryQuoteController { Value = Expr(comp, p, "value") }, p);
                case "targetstate":
                    return new TargetStateController { Id = Expr(comp, p, "id"), Index = Expr(comp, p, "index"), Value = Expr(comp, p, "value") };
                case "targetbind":
                    return new TargetBindController { Id = Expr(comp, p, "id"), Index = Expr(comp, p, "index"), Time = Expr(comp, p, "time"), Position = ExprList(comp, p, "pos") };
                case "targetlifeadd":
                    return new TargetLifeAddController
                    {
                        Id = Expr(comp, p, "id"),
                        Index = Expr(comp, p, "index"),
                        Absolute = Expr(comp, p, "absolute"),
                        Kill = Expr(comp, p, "kill"),
                        Dizzy = Expr(comp, p, "dizzy"),
                        RedLife = Expr(comp, p, "redlife"),
                        Value = Expr(comp, p, "value"),
                    };
                case "targetpoweradd":
                    return new TargetPowerAddController { Id = Expr(comp, p, "id"), Index = Expr(comp, p, "index"), Value = Expr(comp, p, "value") };
                case "targetvelset":
                    return new TargetVelSetController { Id = Expr(comp, p, "id"), Index = Expr(comp, p, "index"), X = Expr(comp, p, "x"), Y = Expr(comp, p, "y"), Z = Expr(comp, p, "z") };
                case "targetveladd":
                    return new TargetVelAddController { Id = Expr(comp, p, "id"), Index = Expr(comp, p, "index"), X = Expr(comp, p, "x"), Y = Expr(comp, p, "y"), Z = Expr(comp, p, "z") };
                case "targetfacing":
                    return new TargetFacingController { Id = Expr(comp, p, "id"), Index = Expr(comp, p, "index"), Value = Expr(comp, p, "value") };
                case "targetdrop":
                    return new TargetDropController { ExcludeId = Expr(comp, p, "excludeid"), KeepOne = Expr(comp, p, "keepone") };
                case "bindtoparent":
                    return new BindToParentController { Time = Expr(comp, p, "time"), Facing = Expr(comp, p, "facing"), Position = ExprList(comp, p, "pos") };
                case "bindtoroot":
                    return new BindToRootController { Time = Expr(comp, p, "time"), Facing = Expr(comp, p, "facing"), Position = ExprList(comp, p, "pos") };
                case "bindtotarget":
                    return new BindToTargetController { Id = Expr(comp, p, "id"), Index = Expr(comp, p, "index"), Time = Expr(comp, p, "time"), Position = ExprList(comp, p, "pos"), PosZ = Expr(comp, p, "posz") };
                case "text": return WithParams(FillText(new TextController(), comp, p), p);
                case "modifytext": return WithParams(FillText(new ModifyTextController { Index = Expr(comp, p, "index") }, comp, p), p);
                case "removetext":
                    return WithParams(new RemoveTextController { Id = Expr(comp, p, "id"), Index = Expr(comp, p, "index") }, p);
                case "tagin":
                    return WithParams(new TagInController
                    {
                        StateNo = Expr(comp, p, "stateno"),
                        PartnerStateNo = Expr(comp, p, "partnerstateno"),
                        Self = Expr(comp, p, "self"),
                        Partner = Expr(comp, p, "partner"),
                        Ctrl = Expr(comp, p, "ctrl"),
                        PartnerCtrl = Expr(comp, p, "partnerctrl"),
                        Leader = Expr(comp, p, "leader"),
                        MemberNo = Expr(comp, p, "memberno"),
                    }, p);
                case "tagout":
                    return WithParams(new TagOutController
                    {
                        Self = Expr(comp, p, "self"),
                        Partner = Expr(comp, p, "partner"),
                        StateNo = Expr(comp, p, "stateno"),
                        PartnerStateNo = Expr(comp, p, "partnerstateno"),
                        MemberNo = Expr(comp, p, "memberno"),
                    }, p);
                case "modifystagevar": return WithParams(new ModifyStageVarController(), p);
                default:
                    report?.AddUnknownController(type);
                    return new NullController();
            }
        }

        // VarSet/VarAdd：支持 v=N value=expr(int) / fv=N value=expr(float) / var(N)=expr / fvar(N)=expr
        // ParentVarSet/RootVarSet 等：复用 BuildVarSet 的 index/isFloat/value 提取，改写到 parent/root。
        static MStateController BuildRelayVar(MugenExprCompiler comp, Dictionary<string, string> p,
            MVarTarget target, bool isAdd)
        {
            MStateController built = BuildVarSet(comp, p, isAdd);
            if (built is VarSetController vs)
            {
                return new RelayVarSetController { Target = target, Index = vs.Index, IsFloat = vs.IsFloat, IsAdd = false, Value = vs.Value };
            }
            if (built is VarAddController va)
            {
                return new RelayVarSetController { Target = target, Index = va.Index, IsFloat = va.IsFloat, IsAdd = true, Value = va.Value };
            }
            return new NullController();
        }

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
            if (p.TryGetValue("p2getp1state", out string g1s)) { hd.P2GetP1State = EvalI(comp, g1s) != 0; }
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
            if (p.TryGetValue("air.juggle", out string aj)) { hd.AirJuggle = EvalI(comp, aj); }

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

        static TController WithParams<TController>(TController controller, Dictionary<string, string> p) where TController : ParameterOnlyController
        {
            controller.Parameters = new Dictionary<string, string>(p);
            return controller;
        }

        static int ParseHitFlagMask(string text)
        {
            MugenCodes.HitFlags(text, out bool high, out bool low, out bool air, out bool down);
            int mask = 0;
            if (high) { mask |= 1; }
            if (low) { mask |= 2; }
            if (air) { mask |= 4; }
            if (down) { mask |= 8; }
            return mask;
        }

        static PalFXController FillPalFX(PalFXController controller, MugenExprCompiler comp, Dictionary<string, string> p)
        {
            FillPalFXParams(controller.PalFX, comp, p);
            return controller;
        }

        static AllPalFXController FillPalFX(AllPalFXController controller, MugenExprCompiler comp, Dictionary<string, string> p)
        {
            FillPalFXParams(controller.PalFX, comp, p);
            return controller;
        }

        static BGPalFXController FillPalFX(BGPalFXController controller, MugenExprCompiler comp, Dictionary<string, string> p)
        {
            FillPalFXParams(controller.PalFX, comp, p);
            return controller;
        }

        static void FillPalFXParams(PalFXParamSet target, MugenExprCompiler comp, Dictionary<string, string> p)
        {
            target.Time = Expr(comp, p, "time");
            target.Color = Expr(comp, p, "color");
            target.Add = ExprList(comp, p, "add");
            target.Mul = ExprList(comp, p, "mul");
            target.SinAdd = ExprList(comp, p, "sinadd");
            target.SinMul = ExprList(comp, p, "sinmul");
            target.SinColor = ExprList(comp, p, "sincolor");
            target.SinHue = ExprList(comp, p, "sinhue");
            target.InvertAll = Expr(comp, p, "invertall");
            target.InvertBlend = Expr(comp, p, "invertblend");
            target.Hue = Expr(comp, p, "hue");
        }

        static AfterImageController FillAfterImage(AfterImageController controller, MugenExprCompiler comp, Dictionary<string, string> p)
        {
            FillAfterImageParams(controller.AfterImage, comp, p);
            return controller;
        }

        static void FillAfterImageParams(AfterImageParamSet target, MugenExprCompiler comp, Dictionary<string, string> p)
        {
            target.Time = Expr(comp, p, "time");
            target.Trans = ExprList(comp, p, "trans");
            target.Length = Expr(comp, p, "length");
            target.TimeGap = Expr(comp, p, "timegap");
            target.FrameGap = Expr(comp, p, "framegap");
            target.PalColor = Expr(comp, p, "palcolor");
            target.PalHue = Expr(comp, p, "palhue");
            target.PalInvertAll = Expr(comp, p, "palinvertall");
            target.PalInvertBlend = Expr(comp, p, "palinvertblend");
            target.PalBright = ExprList(comp, p, "palbright");
            target.PalContrast = ExprList(comp, p, "palcontrast");
            target.PalPostBright = ExprList(comp, p, "palpostbright");
            target.PalAdd = ExprList(comp, p, "paladd");
            target.PalMul = ExprList(comp, p, "palmul");
            target.IgnoreHitPause = Expr(comp, p, "ignorehitpause");
        }

        static PlaySndController FillPlaySnd(PlaySndController controller, MugenExprCompiler comp, Dictionary<string, string> p)
        {
            string soundValue = Get(p, "value");
            if (!string.IsNullOrWhiteSpace(soundValue))
            {
                string[] parts = soundValue.Split(',');
                string group = parts[0].Trim();
                if (group.Length > 1 && (group[0] == 's' || group[0] == 'S' || group[0] == 'f' || group[0] == 'F'))
                {
                    controller.CommonBank = group[0] == 'f' || group[0] == 'F';
                    group = group.Substring(1).TrimStart();
                }
                controller.Value = new BytecodeExp[parts.Length];
                controller.Value[0] = comp.Compile(group);
                for (int index = 1; index < parts.Length; index++)
                {
                    controller.Value[index] = comp.Compile(parts[index].Trim());
                }
            }
            controller.Channel = Expr(comp, p, "channel");
            controller.LowPriority = Expr(comp, p, "lowpriority");
            controller.Pan = Expr(comp, p, "pan");
            controller.AbsPan = Expr(comp, p, "abspan");
            controller.Volume = Expr(comp, p, "volume");
            controller.VolumeScale = Expr(comp, p, "volumescale");
            controller.FreqMul = Expr(comp, p, "freqmul");
            controller.Loop = Expr(comp, p, "loop");
            controller.Priority = Expr(comp, p, "priority");
            controller.LoopStart = Expr(comp, p, "loopstart");
            controller.LoopEnd = Expr(comp, p, "loopend");
            controller.StartPosition = Expr(comp, p, "startposition");
            controller.LoopCount = Expr(comp, p, "loopcount");
            controller.StopOnGetHit = Expr(comp, p, "stopongethit");
            controller.StopOnChangeState = Expr(comp, p, "stoponchangestate");
            return controller;
        }

        static TController FillExplod<TController>(TController controller, MugenExprCompiler comp, Dictionary<string, string> p) where TController : ExplodController
        {
            controller.Anim = ExprList(comp, p, "anim");
            controller.OwnPal = Expr(comp, p, "ownpal");
            controller.RemapPal = ExprList(comp, p, "remappal");
            controller.Id = Expr(comp, p, "id");
            controller.Facing = Expr(comp, p, "facing");
            controller.VFacing = Expr(comp, p, "vfacing");
            controller.Position = ExprList(comp, p, "pos");
            controller.Random = ExprList(comp, p, "random");
            controller.PosType = Expr(comp, p, "postype");
            controller.Velocity = ExprList(comp, p, "vel");
            controller.Friction = ExprList(comp, p, "friction");
            controller.Accel = ExprList(comp, p, "accel");
            controller.Scale = ExprList(comp, p, "scale");
            controller.BindTime = Expr(comp, p, "bindtime");
            controller.RemoveTime = Expr(comp, p, "removetime");
            controller.SuperMove = Expr(comp, p, "supermove");
            controller.SuperMoveTime = Expr(comp, p, "supermovetime");
            controller.PauseMoveTime = Expr(comp, p, "pausemovetime");
            controller.SprPriority = Expr(comp, p, "sprpriority");
            controller.LayerNo = Expr(comp, p, "layerno");
            controller.Under = Expr(comp, p, "under");
            controller.OnTop = Expr(comp, p, "ontop");
            controller.Shadow = ExprList(comp, p, "shadow");
            controller.RemoveOnGetHit = Expr(comp, p, "removeongethit");
            controller.RemoveOnChangeState = Expr(comp, p, "removeonchangestate");
            controller.HideWithBars = Expr(comp, p, "hidewithbars");
            controller.Trans = ExprList(comp, p, "trans");
            controller.AnimElem = Expr(comp, p, "animelem");
            controller.AnimElemTime = Expr(comp, p, "animelemtime");
            controller.AnimFreeze = Expr(comp, p, "animfreeze");
            controller.Angle = Expr(comp, p, "angle");
            controller.YAngle = Expr(comp, p, "yangle");
            controller.XAngle = Expr(comp, p, "xangle");
            controller.XShear = Expr(comp, p, "xshear");
            controller.Projection = Expr(comp, p, "projection");
            controller.FocalLength = Expr(comp, p, "focallength");
            controller.ExplodIgnoreHitPause = Expr(comp, p, "ignorehitpause");
            controller.BindId = Expr(comp, p, "bindid");
            controller.Space = Expr(comp, p, "space");
            controller.Window = ExprList(comp, p, "window");
            FillExplodInterpolation(controller.Interpolation, comp, p);
            controller.AnimPlayerNo = Expr(comp, p, "animplayerno");
            controller.SpritePlayerNo = Expr(comp, p, "spriteplayerno");
            controller.SyncParams = Expr(comp, p, "syncparams");
            controller.SyncLayer = Expr(comp, p, "synclayer");
            controller.SyncId = Expr(comp, p, "syncid");
            controller.Shader = Expr(comp, p, "shader");
            controller.ShaderParam = ExprList(comp, p, "shaderparam");
            FillAfterImageParams(controller.AfterImage, comp, p);
            FillPalFXParams(controller.PalFX, comp, p);
            return controller;
        }

        static void FillExplodInterpolation(ExplodInterpolationParamSet target, MugenExprCompiler comp, Dictionary<string, string> p)
        {
            target.Time = Expr(comp, p, "interpolation.time");
            target.AnimElem = Expr(comp, p, "interpolation.animelem");
            target.Position = ExprList(comp, p, "interpolation.pos");
            target.Scale = ExprList(comp, p, "interpolation.scale");
            target.Angle = Expr(comp, p, "interpolation.angle");
            target.Alpha = ExprList(comp, p, "interpolation.alpha");
            target.FocalLength = Expr(comp, p, "interpolation.focallength");
            target.XShear = Expr(comp, p, "interpolation.xshear");
            target.PalFXMul = ExprList(comp, p, "interpolation.pfx.mul");
            target.PalFXAdd = ExprList(comp, p, "interpolation.pfx.add");
            target.PalFXColor = Expr(comp, p, "interpolation.pfx.color");
            target.PalFXHue = Expr(comp, p, "interpolation.pfx.hue");
        }

        static TController FillText<TController>(TController controller, MugenExprCompiler comp, Dictionary<string, string> p) where TController : TextController
        {
            controller.Removetime = Expr(comp, p, "removetime");
            controller.LayerNo = Expr(comp, p, "layerno");
            controller.Params = ExprList(comp, p, "params");
            controller.Font = ExprList(comp, p, "font");
            controller.LocalCoord = ExprList(comp, p, "localcoord");
            controller.Bank = Expr(comp, p, "bank");
            controller.Align = Expr(comp, p, "align");
            controller.TextSpacing = ExprList(comp, p, "textspacing");
            controller.TextDelay = Expr(comp, p, "textdelay");
            controller.Text = Expr(comp, p, "text");
            controller.Position = ExprList(comp, p, "pos");
            controller.Velocity = ExprList(comp, p, "velocity");
            controller.MaxDist = ExprList(comp, p, "maxdist");
            controller.Friction = ExprList(comp, p, "friction");
            controller.Accel = ExprList(comp, p, "accel");
            controller.Angle = Expr(comp, p, "angle");
            controller.XAngle = Expr(comp, p, "xangle");
            controller.YAngle = Expr(comp, p, "yangle");
            controller.Projection = Expr(comp, p, "projection");
            controller.FocalLength = Expr(comp, p, "focallength");
            controller.Scale = ExprList(comp, p, "scale");
            controller.Color = ExprList(comp, p, "color");
            controller.XShear = Expr(comp, p, "xshear");
            controller.HideWithBars = Expr(comp, p, "hidewithbars");
            controller.Id = Expr(comp, p, "id");
            FillPalFXParams(controller.PalFX, comp, p);
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

        static int ParseStateHeaderNo(string header)
        {
            if (string.IsNullOrEmpty(header))
            {
                return 0;
            }

            int i = 0;
            while (i < header.Length && !char.IsWhiteSpace(header[i]))
            {
                i++;
            }

            while (i < header.Length)
            {
                while (i < header.Length && header[i] != '-' && !char.IsDigit(header[i]))
                {
                    i++;
                }
                int start = i;
                if (i < header.Length && header[i] == '-')
                {
                    i++;
                }
                int digits = i;
                while (i < header.Length && char.IsDigit(header[i]))
                {
                    i++;
                }
                if (i > digits &&
                    int.TryParse(header.Substring(start, i - start), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out int r))
                {
                    return r;
                }
            }

            return 0;
        }

        static string StripComment(string line)
        {
            int semi = line.IndexOf(';');
            return semi >= 0 ? line.Substring(0, semi) : line;
        }
    }
}
