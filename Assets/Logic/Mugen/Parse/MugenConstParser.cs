// Behavior-faithful parser for a Mugen character's constant sections.
// 解析 .cns 的 [Data]/[Size]/[Velocity]/[Movement] 段为 MConstants（const(...) 取值来源）。
// 键名照搬 MUGEN 原生格式（ground.front / walk.fwd / jump.neu = x,y 等）。表达式经 MugenExprCompiler 常量求值。
// 与 MugenCnsParser（解析状态表）分工：同一份 cns 文本可分别跑两个解析器。
// See Docs/移植方案_Ikemen.md.
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;

namespace Lockstep.Mugen.Parse
{
    /// <summary>MUGEN .cns 常量段 → MConstants。未出现的字段保留 Ikemen 默认值。</summary>
    public static class MugenConstParser
    {
        enum Section { Other, Data, Size, Velocity, Movement }

        public static MConstants Parse(string text)
        {
            MugenExprCompiler comp = new MugenExprCompiler();
            MConstants k = new MConstants();
            Section section = Section.Other;

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
                    string header = (rb > 0 ? line.Substring(1, rb - 1) : line.Substring(1)).Trim().ToLowerInvariant();
                    section = SectionOf(header);
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq < 0)
                {
                    continue;
                }
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string val = line.Substring(eq + 1).Trim();

                switch (section)
                {
                    case Section.Data: ApplyData(comp, k, key, val); break;
                    case Section.Size: ApplySize(comp, k, key, val); break;
                    case Section.Velocity: ApplyVelocity(comp, k, key, val); break;
                    case Section.Movement: ApplyMovement(comp, k, key, val); break;
                }
            }
            return k;
        }

        static Section SectionOf(string header)
        {
            switch (header)
            {
                case "data": return Section.Data;
                case "size": return Section.Size;
                case "velocity": return Section.Velocity;
                case "movement": return Section.Movement;
                default: return Section.Other;
            }
        }

        static void ApplyData(MugenExprCompiler comp, MConstants k, string key, string val)
        {
            switch (key)
            {
                case "life": k.Life = EvalI(comp, val); break;
                case "power": k.Power = EvalI(comp, val); break;
                case "attack": k.Attack = EvalI(comp, val); break;
                case "defence": k.Defence = EvalI(comp, val); break;
                case "fall.defence_up": k.FallDefenceUp = EvalI(comp, val); break;
                case "liedown.time": k.LiedownTime = EvalI(comp, val); break;
                case "airjuggle": k.Airjuggle = EvalI(comp, val); break;
            }
        }

        static void ApplySize(MugenExprCompiler comp, MConstants k, string key, string val)
        {
            switch (key)
            {
                case "ground.back": k.SizeGroundBack = EvalF(comp, val); break;
                case "ground.front": k.SizeGroundFront = EvalF(comp, val); break;
                case "air.back": k.SizeAirBack = EvalF(comp, val); break;
                case "air.front": k.SizeAirFront = EvalF(comp, val); break;
                case "height": k.SizeHeight = EvalF(comp, val); break;
                case "head.pos": Pair(comp, val, out k.HeadPosX, out k.HeadPosY); break;
                case "mid.pos": Pair(comp, val, out k.MidPosX, out k.MidPosY); break;
            }
        }

        static void ApplyVelocity(MugenExprCompiler comp, MConstants k, string key, string val)
        {
            switch (key)
            {
                case "walk.fwd": k.WalkFwd = EvalF(comp, First(val)); break;
                case "walk.back": k.WalkBack = EvalF(comp, First(val)); break;
                case "run.fwd": Pair(comp, val, out k.RunFwdX, out k.RunFwdY); break;
                case "run.back": Pair(comp, val, out k.RunBackX, out k.RunBackY); break;
                case "jump.neu": Pair(comp, val, out k.JumpNeuX, out k.JumpY); break;   // y → velocity.jump.y
                case "jump.back": k.JumpBack = EvalF(comp, First(val)); break;
                case "jump.fwd": k.JumpFwd = EvalF(comp, First(val)); break;
                case "runjump.fwd": k.RunjumpFwdX = EvalF(comp, First(val)); break;
                case "runjump.back": Pair(comp, val, out k.RunjumpBackX, out k.RunjumpBackY); break;
                case "airjump.neu": Pair(comp, val, out k.AirjumpNeuX, out k.AirjumpY); break;
                case "airjump.back": k.AirjumpBack = EvalF(comp, First(val)); break;
                case "airjump.fwd": k.AirjumpFwd = EvalF(comp, First(val)); break;
            }
        }

        static void ApplyMovement(MugenExprCompiler comp, MConstants k, string key, string val)
        {
            switch (key)
            {
                case "yaccel": k.Yaccel = EvalF(comp, val); break;
                case "stand.friction": k.StandFriction = EvalF(comp, val); break;
                case "crouch.friction": k.CrouchFriction = EvalF(comp, val); break;
                case "stand.friction.threshold": k.StandFrictionThreshold = EvalF(comp, val); break;
                case "crouch.friction.threshold": k.CrouchFrictionThreshold = EvalF(comp, val); break;
                case "airjump.num": k.AirjumpNum = EvalI(comp, val); break;
                case "airjump.height": k.AirjumpHeight = EvalF(comp, val); break;
            }
        }

        // "x, y" → (x, y)；缺第二项时 y 不变（沿用默认）。
        static void Pair(MugenExprCompiler comp, string val, out FFloat x, out FFloat y)
        {
            string[] parts = val.Split(',');
            x = EvalF(comp, parts[0]);
            y = parts.Length > 1 ? EvalF(comp, parts[1]) : FFloat.Zero;
        }

        static string First(string val)
        {
            return val.Split(',')[0];
        }

        static int EvalI(MugenExprCompiler comp, string expr)
        {
            return comp.Compile(expr.Trim()).Run(null).ToI();
        }

        static FFloat EvalF(MugenExprCompiler comp, string expr)
        {
            return comp.Compile(expr.Trim()).Run(null).ToF();
        }

        static string StripComment(string line)
        {
            int semi = line.IndexOf(';');
            return semi >= 0 ? line.Substring(0, semi) : line;
        }
    }
}
