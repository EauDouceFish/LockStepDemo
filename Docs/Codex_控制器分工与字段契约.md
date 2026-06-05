# Codex 分工：Tier B 控制器 + MChar 字段契约

> 本文是 Claude 与 Codex 并行做「Tier B/C 全量」的协作契约。Claude 做 Tier C 触发器 + R-ENT + 引擎核心 +
> 实体类控制器；Codex 做 Tier B「非实体」控制器。文件所有权见 CLAUDE §5 与下方。

## 文件所有权（严格，避免冲突）
- **Codex 只编辑**：`Assets/Logic/Mugen/StateCtrl/*.cs`（新控制器类，勿动 HitControllers.cs / AttackMulControllers.cs）
  + `Assets/Logic/Mugen/Parse/MugenCnsParser.cs` 的 `BuildController()` 派发分支。测试放
  `Tests/Lockstep.Logic.Tests/Mugen/StateCtrl/`。
- **Claude 只编辑**：`Char.cs`、`MugenExprCompiler.cs`、`OpCode.cs`、`MStateMachine.cs`、`MBattleEngine.cs`、
  `MActionSystem.cs`、common1、`Tests/.../Mugen/{Expr,Char,Battle,Hit}/`。

## MChar 字段请求协议（避免命名返工）
Codex 实现「需要在 MChar 上存运行态」的控制器时，**不要自己在 Char.cs 加字段**。改为在本文件
「## 字段请求队列」里追加一行（名 + 类型 + 用途 + 是否纳入 Clone/Hash），Claude 看到后统一添加并提交，
Codex 再 rebase 使用。字段命名以 Ikemen `char.go` 原义为准。

## 建议施工波次（最大化并行、最小化阻塞）
- **Wave 1（零新字段，立即可做）**：PlaySnd / StopSnd / SndPan / MakeDust / GameMakeAnim / ForceFeedback /
  DisplayToClipboard / VictoryQuote / EnvShake / FallEnvShake / EnvColor / Explod / ModifyExplod / RemoveExplod
  —— 这些对确定性模拟**无可观测副作用**，忠实复刻 = 正确解析参数 + 不改 sim 状态（与 Ikemen 对 sim 的影响一致=0）。
  VarRandom（用 `c.Rng.Rand(min,max)`，Rng 已接入）/ VarRangeSet（写 IntVars）/ MoveHitReset（清现有
  MoveContact/MoveHit/MoveGuarded/MoveReversed）/ RemapPal（最小存态）也属此波。
- **Wave 2（需新字段，先在下方队列登记）**：Trans / SprPriority / Offset / AngleDraw·Set·Add·Mul /
  AfterImage·AfterImageTime / PalFX·AllPalFX·BGPalFX / Width / PlayerPush / ScreenBound / HitOverride /
  ReversalDef / AttackDist。

## 「纯表现 no-op」的判定标准（诚实 1:1）
某控制器若其唯一效果是音频 / 屏震 / 余像 / 调色板 / 手柄震动 / 剪贴板 / 胜利台词等**不进入确定性模拟回路**的，
逻辑层忠实实现 = 解析全部参数字段（结构正确）+ 不产生可观测 sim 副作用。渲染/播放由表现层后续接。
此判定须在控制器类头注写明（标 `// 逻辑层 no-op：唯一效果在表现层，不影响确定性 sim`）。

## 字段请求队列（Codex 追加，Claude 处理）
<!-- 格式：- [ ] FieldName : Type — 用途（Clone:是/否, Hash:是/否） @控制器 -->
（暂空）

## 已完成登记（Codex 更新）
<!-- - ✅ R-CTRL-snd PlaySnd/StopSnd/SndPan（commit xxx, dotnet test N/N） -->
（暂空）
