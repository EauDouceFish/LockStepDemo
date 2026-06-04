// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go (hitDef StateController) + char.go (setHitdef)。
// 激活攻方 HitDef（参数已在 CnsParser/构造时填好），设 MoveType=A，清旧 targets（新招重新可命中各目标）。
// See Docs/移植方案_Ikemen.md.
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Hit;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.StateCtrl
{
    /// <summary>HitDef 控制器：激活角色的攻击判定。Params 已解析为 MHitDef 模板。</summary>
    public sealed class HitDefController : MStateController
    {
        public MHitDef Template = new MHitDef();   // 由 CnsParser 填字段

        public override bool Run(MChar c)
        {
            CopyInto(Template, c.HitDef);
            c.HitDef.Active = true;
            c.MoveType = 4;          // A 攻击中
            c.Targets.Clear();       // 新 HitDef：清空已命中目标，使本招可重新命中
            return false;
        }

        static void CopyInto(MHitDef src, MHitDef dst)
        {
            dst.Attr = src.Attr;
            dst.HitHigh = src.HitHigh; dst.HitLow = src.HitLow; dst.HitAir = src.HitAir; dst.HitDown = src.HitDown;
            dst.GuardHigh = src.GuardHigh; dst.GuardLow = src.GuardLow; dst.GuardAir = src.GuardAir;
            dst.GuardHitTime = src.GuardHitTime; dst.GuardCtrlTime = src.GuardCtrlTime; dst.GuardVelX = src.GuardVelX;
            dst.HitDamage = src.HitDamage; dst.GuardDamage = src.GuardDamage;
            dst.P1PauseTime = src.P1PauseTime; dst.P2PauseTime = src.P2PauseTime;
            dst.GroundHitTime = src.GroundHitTime; dst.AirHitTime = src.AirHitTime; dst.GroundSlideTime = src.GroundSlideTime;
            dst.GroundVelX = src.GroundVelX; dst.GroundVelY = src.GroundVelY;
            dst.AirVelX = src.AirVelX; dst.AirVelY = src.AirVelY;
            dst.AnimType = src.AnimType; dst.AirAnimType = src.AirAnimType; dst.FallAnimType = src.FallAnimType;
            dst.GroundType = src.GroundType; dst.AirType = src.AirType; dst.Fall = src.Fall;
            dst.P1StateNo = src.P1StateNo; dst.P2StateNo = src.P2StateNo; dst.NumHits = src.NumHits;
            dst.YAccel = src.YAccel; dst.FallXVel = src.FallXVel; dst.FallYVel = src.FallYVel;
            dst.FallRecover = src.FallRecover; dst.FallRecoverTime = src.FallRecoverTime;
            dst.Kill = src.Kill; dst.GuardKill = src.GuardKill; dst.FallKill = src.FallKill;
            dst.ForceStand = src.ForceStand;
            dst.HitGetPower = src.HitGetPower; dst.HitGivePower = src.HitGivePower;
            dst.GuardGetPower = src.GuardGetPower; dst.GuardGivePower = src.GuardGivePower;
        }
    }
}
