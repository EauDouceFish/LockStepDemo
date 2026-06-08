# LLM 交接压缩 Prompt

把下面内容作为新 LLM 接手本项目的首条上下文。不要声称“任意 MUGEN 角色 Ikemen 等价完成”，除非 Ikemen trace 对账已接入并通过。

## 当前目标

项目在 `D:\Desktop\demo\LockstepActDemo`。目标是把 Ikemen GO/MUGEN 战斗逻辑移植到 C# Unity，逻辑层保持定点确定性，Unity 展馆能导入 `D:\Desktop\demo\MugenSource` 下任意合法 DEF 角色并做可玩/可测展示。

## 当前可靠事实

- 逻辑层主入口：`Assets/Logic/Mugen`。
- Unity 展馆：`Assets/Scripts/Game/View/MugenMuseumDashboard.cs`。
- 真实语料：`D:\Desktop\demo\MugenSource`，当前 12 个角色。
- 测试入口：`dotnet test Tests\Lockstep.Logic.Tests\Lockstep.Logic.Tests.csproj`。
- Ikemen 参考：`D:\Desktop\demo\MugenSource\_reference\Ikemen-GO`，但当前没有真正 headless JSONL oracle trace 接入。
- 已有本地 TDD 覆盖表达式、命令、状态机、命中主干、helper/projectile/explod 记录、snapshot/hash、展馆可玩输入和招式说明。
- 2026-06-07 修复：`[Statedef 5150, 0]` 之前被误解析成 state 0，导致展馆开局倒地；现在 `MugenCnsParser` 取 `Statedef` 后第一个整数。
- 2026-06-07 修复：展馆招式按钮现在用 `MMovePreviewSession` 逐帧播放 one-shot 输入脚本，进入目标 state 后清 command runtime，避免返回 state0 后重复触发同一招。
- 2026-06-08 修复：展馆没有独立状态机，和 `MugenLiveView` 共用 `MBattleEngine/MStateMachine`；卡动作主因在展馆外层按钮/preview 驱动。现在轻拳/轻脚按钮优先走 CMD 命令 preview，所有按钮输入由 `Update()` 定步消费；`MMovePreviewSession` 只报告自然恢复/timeout，不在 Logic 层强制切 state0。展馆层收到 timeout 后只做 presentation recovery，并在 UI 状态栏暴露 timeout。

## 不能再误报的边界

- 当前测试大量是 C# 自洽测试，不是 Ikemen oracle。
- “全角色全招式矩阵”是诊断/覆盖率，不等于全招式 Ikemen 等价通过。
- 展馆只显示 P1/P2 主 SpriteRenderer；helper/projectile/explod/spark/sound/palfx/collision box 还没完整表现。
- controller 还有 parsed-only 或简化项，尤其表现/环境/辅助类与部分战斗语义类。

## 明天优先清单

1. Ikemen headless oracle：固定输入注入，post-combat tick 输出 JSONL，C# 同 schema 比较。
2. 状态机/控制器保真：梳理 `MStateMachine` 与 Ikemen `char.go action/stateChange` 的剩余差异，逐项 TDD。
3. Controller 缺口：优先 `ReversalDef`、`AttackDist`、`HitOverride`、guard distance；表现类标为 presentation capability，不能伪装完整。
4. AIR/SFF/localcoord/palette：Copy Action、palette/ACT、sprite owner、localcoord 缩放、动画偏移。
5. 展馆表现层：helper/projectile/explod、碰撞框、声音、palfx、失败 trace/首次差异面板。
6. HitDef/受击/投技：target ownership、juggle、guard/reversal、GetHitVar reset policy。
7. 回合流：intro/5900/KO/winpose 接进展馆 live session。

## 开发纪律

- 每个模块先写 `Tests/Lockstep.Logic.Tests/...` 下的 TDD 用例，再实现。
- 每次改 Unity 展馆必须用 Unity MCP refresh + PlayMode screenshot/execute_code 验收。
- 修改表现层不要直接写 state/pos/power；只通过 `MBattleEngine.Tick`、命令输入、或明确标记的 debug preview session。
- 展馆如果为了演示从 timeout state 回 state0，必须留在 View 层并显示诊断；不得把这类兜底写进 `MStateMachine` 或核心战斗语义。
- 做不到的项写进 `Docs/MUGEN完整适配缺口总表.md`，不要用“完成”包装。
