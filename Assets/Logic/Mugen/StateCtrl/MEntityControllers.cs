// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go Helper/DestroySelf StateController + char.go newHelper/destroySelf。
// R-ENT 实体类控制器（Claude 所属文件，与 Codex 的 TierBNonEntityControllers.cs 分开避免冲突）。
// Adapted to fixed-point lockstep. See Docs/移植方案_Ikemen.md.
using Lockstep.Math;
using Lockstep.Mugen.Char;
using Lockstep.Mugen.Expr;
using Lockstep.Mugen.State;

namespace Lockstep.Mugen.StateCtrl
{
    /// <summary>
    /// Helper：请求创建一个 helper 实体（移植 bytecode.go helper.Run + char.go newHelper）。
    /// 入队到 MEntityWorld，引擎 DrainSpawns 时据 owner 角色数据造实体（v1 用 owner 同一角色文件）。
    /// </summary>
    public sealed class HelperController : MStateController
    {
        public BytecodeExp StateNo;   // 初始状态号（默认 0）
        public BytecodeExp Id;        // helper id（= helpertype，ishelper(id)/numhelper(id) 匹配）
        public BytecodeExp PosX;
        public BytecodeExp PosY;
        public BytecodeExp PosType;   // 0=p1 相对（v1 仅此）
        public BytecodeExp Facing;    // 1/-1 相对 owner
        public BytecodeExp KeyCtrl;

        public override bool Run(MChar character)
        {
            int stateNo = StateNo != null ? StateNo.Run(character).ToI() : 0;
            int id = Id != null ? Id.Run(character).ToI() : 0;
            FFloat px = PosX != null ? PosX.Run(character).ToF() : FFloat.Zero;
            FFloat py = PosY != null ? PosY.Run(character).ToF() : FFloat.Zero;
            int facing = Facing != null && Facing.Run(character).ToI() < 0 ? -1 : 1;
            bool keyCtrl = KeyCtrl != null && KeyCtrl.Run(character).ToB();
            character.RequestHelper(stateNo, id, px, py, facing, keyCtrl);
            return false;   // 不切自身状态
        }
    }

    /// <summary>变量写入目标（ParentVarSet→Parent，RootVarSet→Root）。</summary>
    public enum MVarTarget { Parent, Root }

    /// <summary>
    /// ParentVarSet/ParentVarAdd/RootVarSet/RootVarAdd：把值写到 parent/root 的变量。
    /// 值表达式在本实体(helper)上下文求值，结果写到目标角色（对齐 MUGEN parentvarset 语义）。
    /// </summary>
    public sealed class RelayVarSetController : MStateController
    {
        public MVarTarget Target;
        public int Index;
        public bool IsFloat;
        public bool IsAdd;
        public BytecodeExp Value;

        public override bool Run(MChar character)
        {
            if (Value == null) { return false; }
            MChar target = Target == MVarTarget.Parent ? character.Parent : character.Root;
            if (target == null) { return false; }
            if (IsFloat)
            {
                FFloat v = Value.Run(character).ToF();
                if (IsAdd) { target.FloatVars.TryGetValue(Index, out FFloat cur); v = cur + v; }
                target.FloatVars[Index] = v;
            }
            else
            {
                int v = Value.Run(character).ToI();
                if (IsAdd) { target.IntVars.TryGetValue(Index, out int cur); v = cur + v; }
                target.IntVars[Index] = v;
            }
            return false;
        }
    }

    /// <summary>DestroySelf：标记本实体移除（移植 char.go destroySelf）。仅对 helper 等实体生效，玩家忽略。</summary>
    public sealed class DestroySelfController : MStateController
    {
        public override bool Run(MChar character)
        {
            if (character.IsHelper)
            {
                character.Destroyed = true;
            }
            return false;
        }
    }

    /// <summary>
    /// Projectile：发射弹幕实体（移植 bytecode.go projectile.Run）。捕获运动/生命周期参数（projid/velocity/accel/
    /// offset/projremovetime/projanim）。HitDef 命中归切片 3b。
    /// </summary>
    public sealed class ProjectileController : MStateController
    {
        public BytecodeExp ProjId;
        public BytecodeExp VelX;
        public BytecodeExp VelY;
        public BytecodeExp AccelX;
        public BytecodeExp AccelY;
        public BytecodeExp PosX;
        public BytecodeExp PosY;
        public BytecodeExp RemoveTime;
        public BytecodeExp ProjAnim;
        public Lockstep.Mugen.Hit.MHitDef HitDef;   // 弹幕自带 HitDef（parser 从同段 hitdef 参数 BuildHitDef 填）

        public override bool Run(MChar character)
        {
            int projId = ProjId != null ? ProjId.Run(character).ToI() : 0;
            FFloat vx = VelX != null ? VelX.Run(character).ToF() : FFloat.Zero;
            FFloat vy = VelY != null ? VelY.Run(character).ToF() : FFloat.Zero;
            FFloat ax = AccelX != null ? AccelX.Run(character).ToF() : FFloat.Zero;
            FFloat ay = AccelY != null ? AccelY.Run(character).ToF() : FFloat.Zero;
            FFloat px = PosX != null ? PosX.Run(character).ToF() : FFloat.Zero;
            FFloat py = PosY != null ? PosY.Run(character).ToF() : FFloat.Zero;
            int removeTime = RemoveTime != null ? RemoveTime.Run(character).ToI() : -1;
            int animNo = ProjAnim != null ? ProjAnim.Run(character).ToI() : 0;
            // 每发弹幕一份独立 HitDef（克隆模板，避免共享被改）。
            Lockstep.Mugen.Hit.MHitDef hd = HitDef != null ? HitDef.Clone() : null;
            character.RequestProjectile(projId, vx, vy, ax, ay, px, py, removeTime, animNo, hd);
            return false;
        }
    }
}
