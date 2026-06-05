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
}
