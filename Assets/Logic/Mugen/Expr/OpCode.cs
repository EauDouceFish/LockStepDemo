// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/bytecode.go  (OpCode const block, OC_var..OC_rdreset)
// Adapted for fixed-point port. See Docs/移植方案_Ikemen.md.
namespace Lockstep.Mugen.Expr
{
    /// <summary>
    /// 表达式字节码操作码（对应 Ikemen OpCode，按相同顺序故字节值一致）。
    /// 前段(OC_var..OC_ifelse) 为通用栈/算术/逻辑/控制流，M1 实现；
    /// OC_time 起为依赖 Char 的 trigger 读取（M3+）；末尾 OC_const_..OC_ex3_ 为子表跳转标记。
    /// </summary>
    public enum OpCode : byte
    {
        OC_var = 0, OC_sysvar, OC_fvar, OC_sysfvar, OC_localvar,
        OC_int8, OC_int, OC_int64, OC_float,
        OC_pop, OC_dup, OC_swap,
        OC_run, OC_nordrun,
        OC_jsf8, OC_jmp8, OC_jz8, OC_jnz8, OC_jmp, OC_jz, OC_jnz,
        OC_eq, OC_ne, OC_gt, OC_ge, OC_lt, OC_le,
        OC_range_ii, OC_range_ie, OC_range_ei, OC_range_ee,
        OC_neg, OC_blnot, OC_bland, OC_blxor, OC_blor,
        OC_not, OC_and, OC_xor, OC_or,
        OC_add, OC_sub, OC_mul, OC_div, OC_mod, OC_pow,
        OC_abs, OC_exp, OC_ln, OC_log,
        OC_cos, OC_sin, OC_tan, OC_acos, OC_asin, OC_atan,
        OC_floor, OC_ceil, OC_ifelse,
        // ── 以下为依赖 Char 的 trigger 读取（M3+ 接入）──
        OC_time, OC_animtime, OC_animelemtime, OC_animelemno, OC_animelem,
        OC_statetype, OC_movetype, OC_ctrl, OC_command, OC_random, OC_name,
        OC_pos_x, OC_pos_y, OC_vel_x, OC_vel_y, OC_vel_z,
        OC_screenpos_x, OC_screenpos_y, OC_facing,
        OC_p2dist_x, OC_p2dist_y, OC_p2bodydist_x, OC_p2bodydist_y,
        OC_anim, OC_animexist, OC_selfanimexist,
        OC_alive, OC_life, OC_lifemax, OC_power, OC_powermax, OC_canrecover,
        OC_roundstate, OC_roundswon, OC_ishelper, OC_numhelper,
        OC_numexplod, OC_numprojid, OC_numproj, OC_numtext,
        OC_teammode, OC_teamside, OC_hitdefattr,
        OC_inguarddist, OC_movecontact, OC_movehit, OC_moveguarded, OC_movereversed,
        OC_projcontacttime, OC_projhittime, OC_projguardedtime, OC_projcanceltime,
        OC_backedge, OC_backedgedist, OC_backedgebodydist,
        OC_frontedge, OC_frontedgedist, OC_frontedgebodydist,
        OC_leftedge, OC_rightedge, OC_topedge, OC_bottomedge,
        OC_camerapos_x, OC_camerapos_y, OC_camerazoom,
        OC_gamewidth, OC_gameheight, OC_screenwidth, OC_screenheight,
        OC_stateno, OC_prevstateno, OC_id, OC_playeridexist, OC_gametime,
        OC_numtarget, OC_jugglepoints, OC_numenemy, OC_numpartner, OC_ailevel, OC_palno,
        OC_hitcount, OC_uniqhitcount, OC_hitpausetime,
        OC_hitover, OC_hitshakeover, OC_hitfall,
        OC_hitvel_x, OC_hitvel_y, OC_hitvel_z,
        // ── redirect（M3+）──
        OC_player, OC_parent, OC_root, OC_helper, OC_target, OC_partner,
        OC_enemy, OC_enemynear, OC_playerid, OC_playerindex, OC_helperindex,
        OC_p2, OC_stateowner, OC_rdreset,
        // ── 子表跳转标记（其后跟子 opcode 字节）──
        OC_const_, OC_st_, OC_ex_, OC_ex2_, OC_ex3_,
        OC_stagevar_info_name,
        OC_animelemno_time,
        OC_physics,
    }
}
