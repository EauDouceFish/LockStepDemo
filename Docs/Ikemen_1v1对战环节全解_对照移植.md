# Ikemen 1v1 对战环节全解（源码对照 + 本引擎移植映射）

> 权威依据：`../MugenSource/_reference/Ikemen-GO/src/system.go`（回合状态机）、
> `data/common1.cns.zss`（角色侧硬编码状态）、`char.go`（受击/倒地/起身/死亡）。
> 本引擎对照实现：`Assets/Logic/Mugen/Battle/MRoundSystem.cs` + `MBattleEngine.cs`。
> 目的：列清一局 1v1 从开场到结算的**每个环节**、Ikemen 在哪实现、本引擎现状（✅有 / ⚠️简化 / ❌缺）。
>
> **▶ 2026-06-09（续34）更新**：`MRoundSystem` 已从"State+StateTimer 多字段简化主干"**重写为 Ikemen 的单个有符号
> 计数器 `Intro`（= sys.intro）模型**，`State` 改为从 `Intro` 区间派生（对齐 `roundState()`）。`StepRoundState()` 1:1 移植
> `system.go:2959`。已修正：①双 KO 恒平局 ②胜/负/平局姿态 180/170/175（旧只送胜者）③over_hittime 控制权窗口
> ④超时按血量百分比 ⑤新增 `MFinishType`/`MWinType`。验收 `dotnet test 727/727`，新增 `MRoundIkemenFidelityTests` 锁定。
> 下方各环节"本引擎"现状已据此更新。

---

## 0. 一张总表：1v1 完整时间线

Ikemen 用**单个有符号计数器 `sys.intro`** 驱动整局（不是多段独立计时器）。`intro` 从正的大数倒数穿过 0 再变负，
`roundState()` 只是对 `intro` 区间的分类：

```
intro 值                         roundState   阶段             本引擎 MRoundState
──────────────────────────────────────────────────────────────────────────────
> ctrl_time+1                    0  Pre-intro  淡入/READY 前      Intro(StateTimer<IntroTime)
ctrl_time+1 .. ctrl_time         1  Intro      出场动作/对话      Intro
ctrl_time .. 1 (announce→FIGHT)  1→2 过渡       "Round 1" / "FIGHT" Intro 末
== 0                             2  Fight       战斗（授 ctrl）    Fight
< 0 .. -over_waittime            3  PreOver     已分胜负缓冲       PreOver
< -over_waittime                 4  Over        胜利/失败姿态      Over
< -(over_waittime+overTime)      —  roundOver  本回合结束→下一局   AdvanceAfterRound
```

关键配置（`fightScreen.round.*`，对应本引擎 `MRoundSystem` 字段）：

| Ikemen | 含义 | 本引擎字段 | 默认 |
|---|---|---|---|
| `round.ctrl_time` | 出场到授控的帧数 | `IntroTime` | 60 |
| `round.over_waittime` | KO 到 win pose 的缓冲 | `OverWaitTime` | 45 |
| `round.over_hittime` | KO 后仍可改判结果的窗口 | （未建模） | — |
| `winposetime` | 胜利姿态时长 | `WinPoseTime` | 90 |
| `maxRoundTime`/`curRoundTime` | 回合倒计时（tick） | `RoundTime`/`Timer` | 99×60 |
| `matchWins` | 几胜制 | `RoundsToWin` | 2 |

---

## 1. 环节逐条拆解

### ① 淡入 / 回合横幅（Pre-intro，RoundState 0）
- **Ikemen**：`stepRoundState()` system.go:2975，`intro > ctrl_time` 时只 `intro--`，跑 `fadeIn`。
  `introState()`==1。`fightScreen` 显示 "Round 1" 横幅动画（motif 驱动）。
- **本引擎**：`MRoundState.Intro` 期，`StateTimer` 累加；横幅是 HUD 表现层（`MugenBattleHud`）。
- 状态：⚠️ 逻辑有（计时），横幅 UI 程序化占位，未做 motif 动画。

### ② 出场动作（Intro，RoundState 1）
- **Ikemen**：`intro == ctrl_time+1` 进入玩家入场（`introState()`==2）。角色被送进 **State 190**（common1.cns.zss:446）
  → `190` 若有 anim190 播放 → `time=0` 跳 **191**（角色可 override 写自定义出场）→ `191 time=0` 跳 **State 0**（站立）。
  支持开场**对话**（`motif.di` dialogue）。
- **本引擎**：`BeginRoundIntro()` 把角色送入 `IntroStateCandidates {190,191}` 首个存在态；
  `EnterFight()` 时仍在入场态的角色 `QueueTransition(0)` 回中立。MRoundSystem.cs:226/115。
- 状态：✅ 骨架对齐（190/191→0）；❌ 开场对话未做（1v1 非必需）。

### ③ "FIGHT!" 喊话 + 授控（Intro→Fight 边界）
- **Ikemen**：`intro == ctrl_time` 做 `posReset()`（system.go:2985）；`intro` 倒到 **0** 时（:2991）对每个 alive 角色
  `setCtrl(true)` 并 `selfState(0)`，`unsetSCF(SCF_over_alive)`。`introState()`==3 是 "Round N" 喊，==4 是 "FIGHT" 喊。
- **本引擎**：`EnterFight()`（MRoundSystem.cs:111）调 `_engine.StartRound()` 授 ctrl/keyctrl，计时 `Timer=RoundTime` 起算。
- 状态：✅ 授控 + 计时起点对齐；⚠️ "FIGHT" 喊话只在 HUD 占位。

### ④ 战斗（Fight，RoundState 2）—— 核心循环
- **Ikemen**：`intro==0`。每帧：`curRoundTime--`（除非 `GSF_timerfreeze`/super/pause，system.go:3012）；
  跑角色状态机、命中、受击、暂停、超杀停顿。`finishType==FT_NotYet`。
- **本引擎**：`Fight` 期 `Timer--`，`_engine.Tick(inputs)` 推进底层（InputBuffer→StateMachine→Physics→Anim→Collision→Hit）。
  `CheckRoundEnd()` 每帧检测 KO/超时。
- 状态：✅ 主循环成立。子系统进度见 `project_lockstep_act_demo` 记忆（R-HITDEF 等）。

### ⑤ 扣血 / 受击（贯穿 Fight）
- **Ikemen 受击状态机**（common1.cns.zss + char.go）：
  - `5000` 站立受击（抖动）、`5010` 蹲受击、`5020` 空中受击、`5030` 空中受击过渡、`5050` 空中坠落、`5070` 被绊倒。
  - 命中时 `selfState(50xx)`，`movetype=H`，吃 `getHitVar`（伤害/硬直/击退/animType）。
- **本引擎**：`HitSystem` 消费 `PendingHitC`→扣 `Life`、设硬直/击退、切 `state 5000`+`MoveType=H`+双方 hitstop（见 CLAUDE.md §8.3）。
  R-HITDEF chunk-1/2/3 已落（kill/能量/攻防倍率/numhits/fall.damage）。
- 状态：✅ 离散主干完成；⚠️ spark/sound/envshake/cornerpush/juggle 待子系统（R-CTRL-fx 等）。

### ⑥ 倒地 / 击倒（Fall → Down）
- **Ikemen**：被打出 `fall` 后走 `5050`（空中坠落）→落地 `5080`（着地反弹）→`5100`（落地）→`5110`（躺地）。
  char.go:11707 注释：Mugen 强制角色在 `5110` 至少停 1 帧。
- **本引擎**：受击机 R-GHV 已建（FallTime/Ghv）；倒地具体 5080/5100/5110 链由角色 common 状态驱动，引擎提供 statetype L + 物理。
- 状态：⚠️ 有受击/fall 数据，完整 5080→5110 链依赖角色 common1 状态加载。

### ⑦ 起身（Get-up，State 5120）
- **Ikemen**：char.go:11708 —— `ss.no==5110 && time>=1 && ghv.down_recovertime<=0 && alive()` → `changeState(5120)`。
  `5120`（common1.cns.zss:840）`movetype: I`（恢复行动），起身动画结束回 0 站立。
- **本引擎**：⚠️ 起身判定（down_recovertime→5120）逻辑层有受击恢复字段，完整 5110→5120→0 链待 common 状态接入实测。
- 状态：⚠️ 待验证。

### ⑧ 死亡 / KO（State 5150）
- **Ikemen**：`life<=0` 且被打入 `5150`（common1.cns.zss:857，`sprpriority:-3 ctrl:0`）= 躺地 KO。
  char.go:11920 `ss.no==5150 && !SCF_over_ko` → 标 `SCF_over_ko`（system.go:26 注释 "Has reached state 5150"）。
  注意：Mugen 里**实际 KO 不强制要求进 5150**（roundEndDecision 用 `alive()` 判，:3213）。
- **本引擎**：`CheckRoundEnd()` 用 `p.Life<=0` 判 KO（MRoundSystem.cs:138），不依赖角色是否真进 5150。
- 状态：✅ KO 判定对齐 Ikemen（按 alive/life 而非状态号）。

### ⑨ 胜负判定（finishType / winType）
- **Ikemen**：`roundEndDecision()`（system.go:3180）。
  - **KO**：`ko[loser]` = 该队全员/队长 `!alive()`。单杀→`FT_KO` winTeam=活方；双杀→`FT_DKO` winTeam=-1。
  - **Time over**：`curRoundTime==0`（:3233）比双方血量百分比，高者胜 `FT_TO`，相等 `FT_TODraw`。
  - **winType 修饰**：`SetPerfect()`（满血赢）/`SetClutch()`（残血翻盘，`clutch_threshold`）/`WT_Time`/`WT_Hyper`/`WT_Special`（终结技类型）。
- **本引擎**：`DecideWinner(ko0,ko1,life0,life1)`（MRoundSystem.cs:155）：双 KO 取血高/平局；单 KO 活方胜。
  超时 `p0.Life>p1.Life?0:1`（:149）。
- 状态：✅ 胜负主干对齐；❌ winType 修饰（Perfect/Clutch/终结技图标）未建模（HUD 装饰，非必需）。

### ⑩ 超时（Time Over）
- **Ikemen**：见⑨ time over 分支；`winType=WT_Time`。`curRoundTime` 在 Fight 期递减到 0 触发。
- **本引擎**：`CheckRoundEnd()`：`Timer<=0` → 比血量定胜负（MRoundSystem.cs:146）。
- 状态：✅ 已实现。

### ⑪ 胜负缓冲（PreOver，RoundState 3）
- **Ikemen**：`intro<0`。`outroState()` 1→2→3：先双方仍可动（可能双 KO，:1673）→结果不可改（over_hittime，:1670）→失控但未进 win 态（:1667）。
  `over_hittime` 后累加连胜计数（system.go:3028）。
- **本引擎**：`EnterPreOver()`（MRoundSystem.cs:164）双方立即收 ctrl/keyctrl，`StateTimer` 累到 `OverWaitTime`。
- 状态：⚠️ 简化（无 over_hittime 双 KO 窗口；立即收控而非渐进）。1v1 视觉影响小。

### ⑫ 胜利 / 失败姿态（Over，RoundState 4）
- **Ikemen**：`stepRoundState()` :3144 —— `winposetime<=0` 时给每个角色：
  `win()`→`selfState(180)`、`lose()`→`selfState(170)`、否则 `selfState(175)`（draw）。
  `170`（lose）/`175`（draw，fallback 170）/`180`（win，角色 override）见 common1.cns.zss:428/434。
  `winwaittime` 控制进入 RoundState 4 的就绪检查（角色得站稳 anim5，:3086）。
- **本引擎**：`EnterOver()`（MRoundSystem.cs:176）记分 `RoundsWon[Winner]++`，胜者送 `WinPoseStateCandidates {180,181,195}` 首个存在态。
- 状态：✅ 胜者 win pose + 记分；⚠️ 败者 170/平局 175 未显式送态（胜者优先，够用）。

### ⑬ 跳过胜利姿态（玩家按键）
- **Ikemen**：`!winskipped && winposetime<0 && anyButton() && !GSF_roundnotskip` → 快进到 fadeoutStart（system.go:3046）。
- **本引擎**：❌ 未做（Over 满 `WinPoseTime` 自动推进）。低优先。

### ⑭ 回合切换 / 下一局（roundOver → 下一 round）
- **Ikemen**：`roundOver()`（:1682）`intro < -(over_waittime+overTime)`。`matchOver()`（:1420）判几胜制满足。
  未结束则重置进入下一回合（重置 intro、角色复位、win icon 累加）。`s.round++`。
- **本引擎**：`AdvanceAfterRound()`（MRoundSystem.cs:234）：`RoundsWon[Winner]>=RoundsToWin`→`MatchOver`；
  否则 `RoundNo++`、`Winner=-1`、`Timer=RoundTime`、回 Intro、`ResetCombatant`（满血/回站立 0/清 Ghv/收 ctrl）→ `BeginRoundIntro()` 再鞠躬。
- 状态：✅ 几胜制 + 回合复位对齐。注意：Power（能量）跨回合保留（MUGEN 行为，:259）。

### ⑮ 整场结算（Match Over）
- **Ikemen**：`matchOver()` true → `winnerTeam()`（:1718）定胜队。`postMatchFlg`、连胜 `consecutiveWins`、胜场图标、可能进 storyboard。
- **本引擎**：`MatchOver=true`、`MatchWinner=Winner`（MRoundSystem.cs:238）。后续（continue/胜利画面）未做。
- 状态：✅ 标记整场结束 + 胜者；❌ 结算画面/continue 未做（demo 非必需）。

---

## 2. 缺口清单（按可玩优先级）

| 优先级 | 缺口 | 位置 |
|---|---|---|
| 高 | 完整 5080→5110→5120 倒地起身链实测 | 角色 common1 状态接入 |
| ✅ | ~~败者 170 / 平局 175 送态~~（续34 完成，`SendPoses`） | MRoundSystem |
| ✅ | ~~over_hittime 双 KO 窗口 / 控制权暂留~~（续34 完成，进 Over 才收控） | MRoundSystem |
| 中 | over_hittime 期 hit 系统伤害门控（`roundNoDamage()`） | R-HIT 接入 MRoundSystem 状态 |
| 中 | spark/sound/envshake/cornerpush（受击反馈） | R-CTRL-fx / R-SND |
| 低 | winType Clutch（残血翻盘）+ 终结技图标（Hyper/Special） | MRoundSystem/HUD（Perfect/Time 已做） |
| 低 | 跳过胜利姿态、continue、结算画面 | — |

> **续34 已闭合**：双 KO 平局、胜/负/平姿态、控制权窗口、超时血量百分比、finishType/winType（Perfect/Time）。
> 见 `Assets/Logic/Mugen/Battle/MRoundSystem.cs` 与 `Tests/.../MRoundIkemenFidelityTests.cs`。

> 这些缺口都**不阻塞最小可玩**（KFM vs Terrarian 无音效特效对打）。可玩瓶颈在表现层装配 + P2 输入 + 命中验证，已在推进。
