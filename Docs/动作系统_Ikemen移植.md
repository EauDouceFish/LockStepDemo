# 动作战斗系统：Ikemen 1:1 复刻（2026-06-04）

> 复刻 Ikemen GO 引擎硬编码的"基础动作系统"——站/走/蹲/跳/落地的状态转移。
> 这部分**不在角色 .cns 数据里**：MUGEN/Ikemen 引擎按输入边沿 + ctrl 直接驱动 `changeState`，
> 角色的公共状态（common1）只负责进入这些保留状态后的"状态内行为"（动画/摩擦/速度）。
> 之前误以为走跳缺失是数据问题（挤牙膏式补），实为整块引擎动作系统未移植。本文是该系统的复刻规格。

## 1. 为什么走跳"凭空消失"

排查 `common1.cns.zss`（Ikemen 公共状态）+ `common.cmd` + Terrarian `common1.cns`：
- 公共状态 `[Statedef 0/10/11/20/40/50]` **只有状态内行为**（state 20 走路：`command="holdfwd"→velset`；
  state 50 空中：改动画），**没有进入它们的 `changeState`**，也没有 `Statedef -1/-2/-3` 做这件事。
- `common.cmd` 的 holdfwd/holdback/holddown/holdup 命令体**留空**（由角色 cmd 覆盖，如 kfm `holdfwd=/$F`）。
- 结论：**站立→走/蹲/跳、空中→落地 全是引擎硬编码转移**，源在 `char.go`，不在数据。

## 2. 复刻源 → 模块对照（均 MIT，保留版权头）

| 模块 | Ikemen 源 | 我方实现 | 测试 |
|---|---|---|---|
| **A 输入边沿缓冲** | `input.go` `InputBuffer` (672) + `updateInputTime` (695) | `Mugen/Command/MInputBuffer.cs` | `MInputBufferTests`(7) |
| **B 动作禁制标志** | `char.go` `ASF_no{jump,crouch,stand,airjump,brake,walk,hardcodedkeys}` | `Char/MAssertFlag.cs` + `Parse/MugenCnsParser` | `MAssertFlagTests`(3) |
| **C 硬编码基础动作** | `char.go` `actionPrepare` 11435-11481 | `Battle/MActionSystem.cs` `Prepare` | `MActionSystemTests`(13) |
| **C2 空中落地** | `char.go` `actionRun` 11717-11723（posUpdate 后） | `MActionSystem.LandCheck` | `MActionSystemTests`(+3) |
| **D ctrl/keyctrl** | `char.go` `ctrl()` 5255 + 回合起始授权 | `MChar.Control()`/`KeyCtrl` + `MBattleEngine.StartRound` | `MActionSystemTests`(+1) |
| **E 管线整合** | `char.go` actionPrepare→actionRun→update 顺序 | `Battle/MBattleEngine.Tick` 重排 | `MBattleEngineTests` RealKfm(5) |

## 3. 关键语义（逐条对齐 Ikemen）

### 3.1 输入边沿缓冲（A）
- 每方向/按钮一个**有符号帧计数**：按住第 N 帧 = `+N`，松开第 N 帧 = `-N`（`updateInputTime` 内 `update` 闭包）。
- 引擎硬编码键读：`Fb>0`（本帧持前进）、`Ub>0`（持上）、`Ub==1`（**本帧刚按上**，空跳边沿）、`Db>0`（持下）。
- **B/F（后/前）由 L/R 按朝向推导**：面右 `F=Right,B=Left`；面左互换（`fbFlip`）。U/D、B/F 各做 SOCD 对消（同轴双向同按→中立）。

### 3.2 硬编码基础动作（C，`actionPrepare`）
门控：`keyctrl[0] && !asf(nohardcodedkeys)`，转移块再要 `ctrl()`。按 Ikemen **else-if 顺序**：
1. 跳(40)：`!nojump && stateType=S && Ub>0 && ss.no!=40`
2. 空跳(45)：`!noairjump && stateType=A && Ub==1 && pos.y<=-airjump.height && airJumpCount<airjump.num`，命中则 `airJumpCount++`
3. 立转蹲(10)：`!nocrouch && stateType=S && Db>0`（非 run 态先清 `vel.x`）
4. 蹲转立(12)：`!nostand && stateType=C && Db<=0`
5. 走(20)：`!nowalk && stateType=S && (Fb>0)!=(Bb>0)`（inguarddist 未接，等价无敌人靠近的 XOR 解）
6. **刹车(20→0)**：`!nobrake && ss.no=20 && (Bb>0)==(Fb>0)`——**不需要 ctrl**（Ikemen 注释 "Braking is special"）。
- 块外无条件：`stateType!=A → airJumpCount=0`（落地清空跳）。
- 我方只设 `PendingStateNo`（changeState 的缓冲），由 `MStateMachine` 同帧应用（Ikemen 是即时 changeState）。

### 3.3 空中落地（C2，`actionRun` posUpdate 之后）
`physics==A && vel.y>0 && pos.y>=groundLevel(0) && ss.no!=105 → changeState(52)`。
**缺此则跳起后无限下落**（实测 y 冲到 170 万）——地面夹取在 MUGEN 同样是引擎硬编码转移，不在 .cns。
我方在 `MBattleEngine.Tick` 物理步之后调 `LandCheck`。

### 3.4 ctrl 授予（D）
`ctrl()` = `SCF_ctrl && !standby/dizzy/guardbreak`，由回合开始逻辑授予。我方无回合状态机，
`StartRound()` 给玩家角色 `KeyCtrl=Ctrl=true`（faithful shim，对应 RoundState 进入活动期）。
否则直接 spawn 进 state 0 的角色 `ctrl=false`，所有硬编码动作被挡——这正是之前"走不动"的根因。

### 3.5 每帧管线顺序（E）
`MBattleEngine.Tick`：①输入缓冲(命令环形+边沿) → ②actionPrepare(硬编码转移) → ③状态机(应用 Pending+负状态+当前态)
→ ④物理 + LandCheck → ⑤动画 → ⑥命中。AssertFlags 时序：actionPrepare 读**上帧**标志，RunFrame 起始清零本帧。

## 4. 验收（dotnet test 271/271，0 跳过）

- 单元（合成）：边沿计数/朝向 B/F/SOCD（7）；标志解析（3）；各转移逐条（jump/airjump/crouch/stand/walk/brake/落地/越界/无ctrl/非玩家/落地清计数/StartRound，16）。
- 端到端（真实 KFM + 借 Terrarian common1）：持前进→走 20 前移；持后→走 20；持上→空中态升空；**完整跳跃弧线升空后落回地面 y 收敛**；走中松开→刹车回 0；含硬编码动作的管线确定性哈希一致。
- Unity 实测（MCP execute_code 注入合成输入）：walk dx=48；jump apex=-84.4、landed、finalY=0.0。

## 5. 诚实边界 / 未做

- **守招(120)**：需 `inguarddist`（敌方攻击中 + 距离）+ `SCF_guard`，单角色 demo 无法触发，故未入 else-if 链（deferred，结构已留）。
- **inguarddist**：未接，走路用 `(Fb>0)!=(Bb>0)` 的无敌人解（持后即后退走，而非守招）。
- **回合状态机**：intro/5900/fight/KO/round 未做，`StartRound` 是授 ctrl 的最小 shim。
- **跑(100/105)/空跑跳(runjump)**：跑由角色 cmd 的 `Statedef -1`（FF/BB → 100/105）驱动，属角色数据，本次不涉；105 落地特例已在 LandCheck 排除。
- **down/getup(5110→5120)、state140 循环**：actionRun 里另两条硬编码转移（受击倒地相关），受击系统完善时再补。
- 落地后 `pos.y` 收敛在地面附近（≤8，多为 0），未做亚像素 snap——与 Ikemen 一致（land 态 velset + stand velset y=0 收敛）。
