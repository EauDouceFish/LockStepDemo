# Codex 需要的 MChar 字段

生成日期：2026-06-05。来源：Tier B 非实体 StateController 对照 `../MugenSource/_reference/Ikemen-GO/src/bytecode.go`。

以下字段应声明在 `MChar`，并全部纳入 `Clone()` 与 `WriteHash()`，除非用途明确写为纯表现但仍会影响回放一致性。连续量用 `FFloat`。

- `PauseTime`，`int`，Pause/SuperPause 冻结剩余时间，对应 `setPauseTime/setSuperPauseTime`。
- `PauseMoveTime`，`int`，Pause 期间本角色可行动帧数。
- `SuperPauseTime`，`int`，SuperPause 剩余时间。
- `SuperPauseMoveTime`，`int`，SuperPause 期间本角色可行动帧数。
- `SuperPauseUnhittable`，`bool`，SuperPause 的 `unhittable` 状态。
- `PosFreeze`，`bool`，PosFreeze 控制器设置的本帧位置冻结标志。
- `WidthPlayerFront`，`FFloat`，Width `player/value` 前向角色推挤宽度。
- `WidthPlayerBack`，`FFloat`，Width `player/value` 后向角色推挤宽度。
- `WidthEdgeFront`，`FFloat`，Width `edge/value` 前向边界宽度。
- `WidthEdgeBack`，`FFloat`，Width `edge/value` 后向边界宽度。
- `PlayerPushEnabled`，`bool`，PlayerPush `value` 标志。
- `PushPriority`，`int`，PlayerPush `priority`。
- `PushAffectTeam`，`int`，PlayerPush `affectteam`。
- `ScreenBoundEnabled`，`bool`，ScreenBound `value` 标志。
- `ScreenBoundMoveCameraX`，`bool`，ScreenBound `movecamera` 第一参数。
- `ScreenBoundMoveCameraY`，`bool`，ScreenBound `movecamera` 第二参数。
- `ScreenBoundStageBound`，`bool`，ScreenBound `stagebound`。
- `HitDefGuardDistXFront`，`FFloat`，AttackDist 写入 HitDef guard_dist_x 第一值。
- `HitDefGuardDistXBack`，`FFloat`，AttackDist 写入 HitDef guard_dist_x 第二值。
- `HitDefGuardDistYTop`，`FFloat`，AttackDist 写入 HitDef guard_dist_y 第一值。
- `HitDefGuardDistYBottom`，`FFloat`，AttackDist 写入 HitDef guard_dist_y 第二值。
- `HitDefGuardDistZFront`，`FFloat`，AttackDist 写入 HitDef guard_dist_z 第一值。
- `HitDefGuardDistZBack`，`FFloat`，AttackDist 写入 HitDef guard_dist_z 第二值。
- `HitOverrides`，`MHitOverride[8]`，HitOverride slot 容器，元素含 `Attr/Stateno/Time/ForceAir/ForceGuard/KeepState/GuardFlag/GuardFlagNot/PlayerNo`。
- `MoveContactTime`，`int`，Ikemen `mctime`，MoveHitReset 清零，movecontact/movehit/moveguarded 触发器应从该类字段派生。
- `CounterHit`，`bool`，Ikemen `counterHit`，MoveHitReset 清零。
- `ReversalDefs`，`MReversalDef[]` 或单当前容器，ReversalDef/ModifyReversalDef 的 attr/guardflag/HitDef 子字段。
- `RemapPalSourceGroup`，`int`，RemapPal source group。
- `RemapPalSourceIndex`，`int`，RemapPal source index。
- `RemapPalDestGroup`，`int`，RemapPal dest group。
- `RemapPalDestIndex`，`int`，RemapPal dest index。
- `TransMode`，`int`，Trans 的 TransType 离散值。
- `AlphaSource`，`int`，Trans addalpha source alpha，Clamp 到 0..255。
- `AlphaDest`，`int`，Trans addalpha dest alpha，Clamp 到 0..255。
- `SprPriority`，`int`，SprPriority `value`。
- `LayerNo`，`int`，SprPriority `layerno`。
- `DrawOffsetX`，`FFloat`，Offset `x`。
- `DrawOffsetY`，`FFloat`，Offset `y`。
- `AngleDrawEnabled`，`bool`，AngleDraw 设置 CSF_angledraw。
- `AngleZ`，`FFloat`，AngleSet/Add/Mul/Draw 的主旋转角。
- `AngleX`，`FFloat`，AngleSet/Add/Mul/Draw 的 X 轴旋转角。
- `AngleY`，`FFloat`，AngleSet/Add/Mul/Draw 的 Y 轴旋转角。
- `AngleDrawScaleX`，`FFloat`，AngleDraw `scale` 第一参数累乘。
- `AngleDrawScaleY`，`FFloat`，AngleDraw `scale` 第二参数累乘。
- `AfterImageState`，`MAfterImageState`，AfterImage 参数态，含 time/length/palcolor/palcontrast/trans/palfx 等。
- `AfterImageTime`，`int`，AfterImageTime 写入的剩余时间。
- `PalFXState`，`MPalFXState`，PalFX 参数态，含 time/add/mul/sinadd/color/invert/allpalfx 标志。
- `AllPalFXState`，`MPalFXState`，AllPalFX 全局角色调色态；若全局系统承载，也需进入引擎快照/Hash。
- `BGPalFXState`，`MPalFXState`，BGPalFX 背景调色态；若全局系统承载，也需进入引擎快照/Hash。
- `EnvColor`，`int[3]`，EnvColor RGB。
- `EnvColorTime`，`int`，EnvColor 持续时间。
- `EnvColorUnder`，`bool`，EnvColor under 标志。
- `EnvShakeState`，`MEnvShakeState`，EnvShake/FallEnvShake 环境震动，含 time/ampl/freq/phase/mul/dir。
- `ClipboardText`，`string` 或确定性文本缓冲，DisplayToClipboard/VictoryQuote 需要时使用；若仅调试输出且不影响模拟，可不入 Hash，但需明确。
- `VictoryQuote`，`int`，VictoryQuote `value`。

纯 no-op 直到子系统落地的项目：`PlaySnd/StopSnd/SndPan` 需要声音事件队列时再加 `MSoundEvent[]`；`Explod/ModifyExplod/RemoveExplod/MakeDust/GameMakeAnim` 需要 R-ENT Explod 实体后由实体快照承载；`ForceFeedback` 需要输入设备反馈事件队列时再决定是否入 Hash。
