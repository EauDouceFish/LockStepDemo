# 战斗调试修改器（Battle Debug Modifier）

给 agent / 开发者一个统一面板，运行时直接改对局：改血、上帝模式、跳过出场、设倒计时、强制胜负、
设状态/位置、暂停/单步、复位。覆盖 Ikemen 1v1 各环节的快速到达，便于逐环节验证（见
`Docs/Ikemen_1v1对战环节全解_对照移植.md`）。

## 三种用法

1. **OnGUI 作弊面板**（鼠标）：场景里 `MugenVersusView.EnableDebugBridge = true`（默认开），
   运行时右上角 "Debug ▲" 展开按钮面板。
2. **文件 / CLI 桥**（PowerShell / bash / 任意会写文件的 agent）：
   - 命令文件 `Temp/battle_debug/cmd.txt`：每行一条命令，Unity 端每 0.1s 轮询、执行后清空。
   - 状态文件 `Temp/battle_debug/state.json`：Unity 端每帧写出当前对局快照（回合态/双方血量/状态/位置）。
   - 日志 `Temp/battle_debug/log.txt`：命令执行回显。
3. **MCP**：截图验证时，agent 用 CLI 写命令 → 截图 → 读 `state.json` 断言（见 Task 3）。

## CLI

```powershell
# PowerShell
Tools/battle-debug/battle-debug.ps1 skipintro
Tools/battle-debug/battle-debug.ps1 sethpp 1 10
Tools/battle-debug/battle-debug.ps1 win 0
Tools/battle-debug/battle-debug.ps1 -State      # 打印 state.json
Tools/battle-debug/battle-debug.ps1 -Watch      # 0.5s 刷新
```

```bash
# bash
Tools/battle-debug/battle-debug.sh skipintro
Tools/battle-debug/battle-debug.sh sethpp 1 10
Tools/battle-debug/battle-debug.sh --state
```

## 命令清单

| 命令 | 作用 |
|---|---|
| `state` | 导出当前对局 JSON |
| `sethp <i> <v>` / `sethpp <i> <pct>` | 设血量 / 设血量百分比 |
| `damage <i> <v>` / `heal <i>` / `kill <i>` | 扣血 / 回满 / 秒杀 |
| `power <i> <v>` | 设能量 |
| `god <i> on\|off` | 上帝模式（每帧回满血） |
| `setstate <i> <no>` | 强制切状态号（如 5000 受击、5150 KO 躺地） |
| `pos <i> <x> <y>` / `face <i> l\|r` / `sep <units>` | 设位置 / 朝向 / 面对面间距 |
| `skipintro` | 跳过出场直接进 Fight 并授控 |
| `timer <sec>` | 设回合倒计时（秒） |
| `win <i>` | 强制 i 方胜（对手秒杀，走真实回合判定流程） |
| `resetround` | 复位当前局面（双方满血/回站立/Fight） |
| `pause` / `resume` / `step [n]` | 暂停 / 继续 / 单步 n 帧 |

`<i>` = 角色索引（0=P1，1=P2）。

## ⚠️ 注意

- **仅调试用**：会破坏 lockstep 确定性。**联机 / 回放 / 黄金哈希对账时必须关**（`EnableDebugBridge=false`）。
- 调试逻辑（`MBattleDebugController`）是纯 C# 定点、无 IO，已被 `dotnet test` 覆盖（`MBattleDebugControllerTests`）。
  文件/Unity IO 在表现层 `MugenBattleDebugBridge`，不进逻辑层、不进哈希路径。
