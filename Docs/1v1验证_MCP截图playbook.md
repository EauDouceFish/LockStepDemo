# 1v1 对战逐环节 MCP 截图验证 Playbook（重启会话后执行）

> 前置：**从 `D:\Desktop\demo` 重启 Claude Code**，让 `mcp__UnityMCP__*` 工具加载（本会话已把 UnityMCP
> 注册进该工程 scope，`claude mcp list` 显示 ✔ Connected，但工具需重启才进会话）。Unity Editor 须开着工程。

## 0. 准备
1. Unity 打开 `LockstepActDemo`，建/开一个挂了 `MugenVersusView`（`EnableDebugBridge=true`）的场景，进 Play。
   - 若无现成场景：用菜单 `MUGEN/Demo/*` 或新建空场景挂 `MugenVersusView` 组件。
2. 确认调试桥就绪：Console 出现 `[BattleDebug] 桥已就绪`，且 `Temp/battle_debug/state.json` 开始刷新。
3. 验证手段：CLI 写命令 + MCP 截图 + 读 `state.json` 三方对账。
   - 命令：`Tools/battle-debug/battle-debug.ps1 <cmd>`（或 `.sh`）。
   - 状态：读 `Temp/battle_debug/state.json`（回合态/双方血量/状态号/位置）。
   - 画面：`mcp__UnityMCP__manage_editor`（截 Game 视图）或等价截图工具。

## 1. 逐环节验证清单（每条：发命令 → 截图 → 读 state.json 断言）

| # | 环节 | 命令 | 截图应见 | state.json 断言 |
|---|---|---|---|---|
| 1 | 出场（Intro） | （开局自动） | 双方鞠躬/入场动画 | `round.state=="Intro"` |
| 2 | FIGHT 授控 | `skipintro` | "FIGHT" + 站立可动 | `round.state=="Fight"`，chars[].ctrl=true |
| 3 | 走位/命中扣血 | 键盘出招 或 `damage 1 200` | P2 血条下降/受击 | chars[1].life 减少 |
| 4 | 受击态 | `setstate 1 5000` | P2 播放站立受击 | chars[1].state==5000 |
| 5 | 倒地 | `setstate 1 5110` | P2 躺地 | chars[1].state==5110 |
| 6 | 起身 | （5110 后等恢复）或 `setstate 1 0` | P2 起身→站立 | chars[1].state 回 0 |
| 7 | KO 死亡 | `kill 1`（Fight 期） | P2 倒地 KO + KO 横幅 | round.winner==0，state→PreOver |
| 8 | 胜利姿态 | 等 PreOver→Over | P1 win pose | round.state=="Over"，roundsWon[0]++ |
| 9 | 回合切换 | 等 Over 结束 | "Round 2" + 双方满血复位 | round.roundNo==2，chars 满血 |
| 10 | 超时 | `heal 0; heal 1; timer 1` | TIME OVER + 血高者胜 | curRoundTime→0，winner=血高方 |
| 11 | 整场结算 | 连胜两局（`win 0` ×2 跨回合） | 整场胜利 | round.matchOver==true，matchWinner==0 |
| 12 | HUD | 全程 | 双层血条/倒计时/回合点正确 | 与 state.json 数值一致 |

## 2. 自动化建议
- 用 `pause`+`step` 单步定格关键帧再截图（避免动画一闪而过）。
- 每步把 `state.json` 与截图一并记录，产出《1v1验证报告.md》（环节 / 命令 / 期望 / 实测 / 截图 / 结论）。
- 发现不符 → 对照 `Docs/Ikemen_1v1对战环节全解_对照移植.md` 的「缺口清单」定位是已知简化还是 bug。

## 3. 通过标准
环节 1–12 截图 + state.json 全部符合预期（已知简化项标注即可，不算失败）→ Task 3 完成 → 解锁 Task 4（KCP loopback）。

## 4. Task 4 前瞻（KCP）——验证通过后
现状：传输层 `ITransport`/`LoopbackHub`/`KcpClient·ServerTransport` + `Plugins/Kcp` 已就绪；
**缺**一个 lockstep 会话层：采本地输入→经 ITransport 交换→凑齐双方第 N 帧输入→喂 `MRoundSystem.Tick`（带延迟缓冲/帧门控）。
路径：① 先 `LoopbackHub` 本机双端验确定性（两端 `engine.ComputeHash()` 逐帧相等）→ ② 换 KCP 同机 loopback → ③ 上新加坡服务器。
