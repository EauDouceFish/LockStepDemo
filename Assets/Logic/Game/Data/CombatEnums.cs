namespace Lockstep.Game.Data
{
    /// <summary>MUGEN Statedef 姿势类型（type）。</summary>
    public enum StateType : byte { Unchanged = 0, Stand, Crouch, Air, LieDown }

    /// <summary>MUGEN movetype（动作类型）。</summary>
    public enum MoveType : byte { Unchanged = 0, Idle, Attack, BeingHit }

    /// <summary>MUGEN physics（物理类型）。</summary>
    public enum Physics : byte { Unchanged = 0, Stand, Crouch, Air, None }

    /// <summary>
    /// 状态控制器类型。v1 子集（见 架构设计 §10）。
    /// 扩展 = 加一个枚举值 + 一个 handler，地基不动（安全简化）。
    /// </summary>
    public enum ControllerType : byte
    {
        Null = 0,
        ChangeState,
        SelfState,
        ChangeAnim,
        VelSet,
        VelAdd,
        PosSet,
        PosAdd,
        CtrlSet,
        StateTypeSet,
        HitDef,
        Turn,
        Width,
        PlaySnd,
        VarSet,
        VarAdd,
        AssertSpecial,
        Pause,
        NotImplemented,
    }

    /// <summary>HitDef 攻击等级（MUGEN attr 第二段 N/S/H）。</summary>
    public enum AttackClass : byte { Normal = 0, Special, Hyper }

    /// <summary>HitDef 攻击种类（MUGEN attr 第二段 A/T/P）。</summary>
    public enum AttackKind : byte { Attack = 0, Throw, Projectile }

    /// <summary>动画帧翻转标记。</summary>
    [System.Flags]
    public enum FlipFlags : byte { None = 0, Horizontal = 1, Vertical = 2 }
}
