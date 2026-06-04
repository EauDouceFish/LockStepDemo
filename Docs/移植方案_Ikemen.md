# Ikemen GO 战斗系统 1:1 移植方案（定点化）

> **本文是移植工程的最高纲领。任何 agent 接手先读本文 + `CLAUDE.md §8`。**
> 决策时间 2026-06-03。取代此前"clean-room 简化子集"路线。

---

## 0. 决策与依据

**目标**：把 Ikemen GO 的战斗系统**逻辑/结构 1:1 忠实移植**进本项目，使其能像 Ikemen 一样**直接吃别人的 MUGEN 角色文件（DEF/CNS/CMD/AIR/SFF）并战斗**，同时**保留定点确定性以支持帧同步/回滚**。

**为什么能做（许可证）**：Ikemen GO 源码是 **MIT 许可证**（`_reference/Ikemen-GO/LICENCE.txt`，Copyright 2016 Suehiro 等）。MIT 允许移植/派生，只需**保留版权声明 + 注明出处**，**不传染、不强制开源**。→ 1:1 移植合法。**移植文件头必须保留 Ikemen MIT 版权块**（见 §6）。

**为什么之前不这么做**：误判为 GPL（已纠正）。且 clean-room 简化子集（12 controller / 贪心搓招 / 基线命中）**无法兼容真实角色**——蓄力招/负边缘/防御/hitflag/受击状态机全缺，会误判漏判。

**唯一不可消除的约束（务必理解）**：Ikemen 用 **float**，我们用 **定点 FFloat**。**位级 1:1 不可能**（浮点≠定点）。我们追求的是**逻辑/结构 1:1**：
- **离散量**（状态号/命中与否/命中帧/KO/搓招成立）→ 移植同一套逻辑 → **应与 Ikemen 全等**。
- **连续量**（坐标/速度/三角函数结果）→ 定点近似 → **容差内一致**（差分测试验收）。

---

## 1. 架构总纲（大刀阔斧的改革）

```
战斗实体 = 移植的 Char 结构体（定点化），不再是旧 ECS 组件
  ├─ Char + CharSystemVar 等 Ikemen 运行态（float→FFloat）
  ├─ 实现 深拷贝 Clone() + WriteHash()  → 挂到我们已有的 Snapshot/Desync/回滚底座
  ├─ 表达式 = 移植的 BytecodeExp 字节码 VM（compiler 编译 CNS trigger → 字节码）
  ├─ 状态机 = 移植的 StateBytecode + state runner + common states(数据)
  ├─ StateController = 逐个移植（~90 个战斗相关）
  ├─ 命令系统 = 移植 input.go（charge/~/$/>/+ 全语义）
  └─ HitDef(130 字段) + 命中检测 + gethit 5000-5150 状态机
```

**保留复用（不动）**：
- `Assets/Logic/Framework/`：FFloat/FVector/FMath/FRandom（定点底座）、Hash64、World/Snapshot/Desync（回滚底座）。
- `Assets/Logic/Import/Sff/`：SFF 读取（含已忠实移植的 SFFv2 Lz5/Rle 解码，对应 image.go）。
- `Assets/Scripts/`、`Assets/Editor/`：Unity 表现层 + 导入工具（画廊/showcase/MugenDef 等）。

**废弃/退役（被移植件取代，git 留史）**：
- `Assets/Logic/Game/Expr/`（旧 AST 表达式 VM）→ 由移植的字节码 VM 取代。
- `Assets/Logic/Game/Systems/`（MugenStateMachine/Physics/Anim/Command/Collision/Hit/StateControllerExecutor）→ 由移植的 Char/state runner/controllers 取代。
- `Assets/Logic/Game/Command/`、`Assets/Logic/Game/Components/` 里战斗组件、`Assets/Logic/Game/Data/` 里与 Ikemen 重复的 schema（StateController/HitDef/CommandData/StateDef…）→ 由移植类型取代。
- **退役时机**：不立即删！移植件**跑通对应功能并通过测试后**再删旧件（避免中途破坏可编译/可运行）。退役一个旧件，在进度表标记。

---

## 2. float → 定点 移植契约（逐行移植时的铁律）

1. Go `float32`/`float64`（战斗路径） → `FFloat`。
2. **BytecodeValue**：Ikemen 是 `{vtype, float64 value}` 单字段。我们拆为 `{ValueType vtype; long ival; FFloat fval}`（int 用 long 保精度，float 用 FFloat）。这是**唯一刻意偏离** Ikemen 字段设计之处，因定点 int 范围/精度所需。`ToI()/ToF()/ToB()` 语义照搬。
3. 三角/超越函数（sin/cos/tan/atan/atan2/exp/ln/pow/sqrt）→ 用 FFloat 确定性实现（FixMath.NET Fix64 自带，LUT/多项式）。**这是连续量发散的主要来源，记入 §5 卡点。**
4. `random`/`rand` → 走 `World.Random`(`FRandom`)，**不用 System.Random**。
5. 整数运算保持整数。位运算照搬。
6. Go `[2]float32 Pos/Vel` → `FVector2`（或两个 FFloat）。
7. Go map/slice → C# Dictionary/List/数组。Go 多返回值 → out 参数或元组。
8. Go 的 `interface{}` 派发（StateController）→ C# 抽象基类 + override，或类型 switch（照搬 Ikemen 的 run 分派结构）。
9. **逐行对照移植**：每个移植文件头注明对应 Ikemen 源文件 + 行段，便于 diff 复核。

---

## 3. Unity 目录结构（移植件落位）

新引擎根：`Assets/Logic/Mugen/`（仍属 asmdef `Lockstep.Logic`，noEngineReferences，纯定点纯 C#）。子目录**镜像 Ikemen 模块**：

```
Assets/Logic/Mugen/
  Expr/        BytecodeValue, BytecodeExp(OpCode VM), Compiler         ← bytecode.go(VM部分)+compiler.go(+_functions)
  Char/        Char, CharSystemVar, CharGlobalInfo, 生命周期            ← char.go
  State/       StateBytecode, StateDef, state runner, ChangeState       ← bytecode.go(state部分)+state.go
  StateCtrl/   各 StateController（每个一文件或分组）                    ← bytecode.go(SC部分)
  Command/     CommandList, Command, cmdElem, 输入缓冲与匹配             ← input.go
  Hit/         HitDef, GetHitVar, 命中检测, gethit 状态                  ← char.go(Hit部分)
  Anim/        Animation, AnimationTable, AnimFrame（AIR）              ← anim.go
  Parse/       DEF/CNS/CMD/AIR 文本解析（Ikemen 风格）                   ← 各 load 函数
  System/      引擎循环、全局状态、常量、枚举                             ← system.go/common.go 的战斗相关部分
```
（SFF 读取仍在 `Assets/Logic/Import/Sff/`，对接处在 `Mugen/Anim`。）

测试：`Tests/Lockstep.Logic.Tests/Mugen/`（按模块分目录）。差分基准数据：在 Ikemen 跑出 → 存 `Tests/.../Golden/`。

---

## 4. 移植顺序 + 进度表（依赖驱动；接手者按此推进，做完即勾）

> 状态：⬜未开始 / 🔄进行中 / ✅完成（带 commit）。每个里程碑下可拆 task 多 agent 并行。

| 里程碑 | 内容 | 依赖 | 状态 |
|---|---|---|---|
| **M0** 基础类型 | `BytecodeValue`(定点版) + ValueType | — | ✅ `0bd8303` |
| **M1** 表达式 VM | `OpCode`(155枚举) + `BytecodeOps` + `BytecodeExp`(栈机) + FMath 超越函数 | M0 | ✅ 完成(`3ba75b4` 补短路跳转 OC_jmp/jz/jnz+8bit+jsf8, peek 语义+offset 编码照搬 Ikemen)。dotnet test 113/113 |
| **M2** 编译器 | `compiler.go`(+functions)：CNS trigger 字符串 → BytecodeExp | M1 | ✅ 核心完成(`6a67038`): tokenizer+完整优先级链+字面量/括号/一元/算术/比较/逻辑/位+无参&轴 trigger+函数(abs/floor/trig/ifelse/log)+var/fvar + **statetype/movetype 字母枚举(S/C/A/L,OR列表,!=)** + **`=[a,b]`/`(a,b]` 区间语法** + **redirect 编译(p2/root/parent, OC_run 包裹)**。dotnet test 127/127。**待补(后续里程碑)**: 字符串 `command=`(→M6)、`const(...)`/常量(→M9 角色加载)、p2statetype/prevstatetype/target 编译、更多 trigger |
| **M3** Char 运行态 | `Char`/`CharSystemVar`(定点)、生命周期、Clone/WriteHash 接回滚 | M0 | ✅ 地基完成(`d91ab38`): 核心 trigger + **redirect 机制(VM oc/cur 双上下文+每迭代重置, OC_root/parent/p2/target + OC_run/nordrun + rdreset; MChar Id/P2/Root/Parent/Targets, Clone 浅拷结构引用待 World 重链)** + **CharSystemVar 常用字段+trigger(id/palno/hitpausetime/hit*/move*/animtime/numtarget)** + **MGetHitVar 受击容器(常用字段+Clone/Hash)**。dotnet test 121/121。**待补(→M7)**: CharSystemVar 全字段、gethitvar(...) opcode 接入、HitDef(130字段) |
| **M4** 状态机 | `StateBytecode` + state runner + ChangeState/SelfState + common states | M1,M3 | ✅ 完成(`9584849`): **MTriggerSet(triggerall全AND + trigger1..n 组内AND/组间OR)** + 控制器 **persistent(1每帧/0一次/N每N次)** + **ignorehitpause** + **负状态 -3/-2/-1 每帧跑(移植 actionRun 顺序)** + **SelfStateController** + **common states 回退查找** + ChangeState 同帧重入/statedef 头应用/hitpause 门控。dotnet test 135/135。**待补(后续)**: 嵌套 StateBlock if/else 树、loop/for、persistent=N 精确节奏、-4/+1 Ikemen 扩展态 |
| **M5** StateControllers | 逐个移植（changeState/vel*/pos*/hitDef/…） | M2,M4 | ✅ 核心完成 a06ebd7/431edc3/c736b13: BasicControllers(Null/Vel/Pos/ChangeAnim/Ctrl/Var/StateType) + StatControllers(Life/Power/Turn/AssertSpecial) + **MugenCnsParser 接入(Statedef/State→实例化18类控制器+编译triggerall/triggerN)**。dotnet test 159/159。**待续**: HitBy/Width/Explod/PlaySnd 等更多控制器 |
| **M6** 命令系统 | input.go：CommandList/Command/cmdElem，charge/release/4way/strict/plus 全语义 | M3 | ✅ 完成 a8ace2c/ecb2b13: MCommandModel(输入位+环形缓冲)+MCommandParser(波浪释放/数字蓄力/斜杠按住/$4way/+AND/>严格; 方向大写按钮小写)+MCommandMatcher(末步边沿+向前贪心+朝向相对+蓄力+释放+$子集)+MCommandList(每帧Update/IsActive/Clone/Hash)+MugenCmdParser([Command]段)+编译器 command=name trigger。dotnet test 173/173 |
| **M7** 命中 | HitDef(130字段)+命中检测+GetHitVar+gethit 5000-5150 | M4,M5 | ✅ 核心完成 6c6e593: MHitDef(核心字段)+MClsn(Clsn1×Clsn2朝向镜像重叠)+HitDefController+MHitSystem(hitflag校验+结算:扣血/转向/击退/双方hitstop/GetHitVar填值/守方进5000+movetypeH/攻方movehit+target登记/同招一次)+CnsParser接入 type=HitDef。dotnet test 180/180。**待续**: 完整130字段/守招格挡/projectile/多段命中/gethit 5000-5150 数据态 |
| **M8** 动画/SFF 对接 | `anim.go`(AIR→Animation) + 对接 SFF 读取 → Clsn/精灵 | M3 | ✅ 完成 7f80357: Mugen/Anim/MAnim(MAnimFrame/MAnimData+ComputePacing 算 TotalTime/LoopTime/PreLoopTime)+MAnimSystem(literal 移植 Action 推进:跳零时长帧/末元素停留/循环回绕/curtime 回绕,派生 AnimElemNo/AnimTime,填 Clsn1/Clsn2,动画号变化自检重置,运行态写回 MChar 接回滚)+MAnimImport(AIR→MAnimData 桥接)。MChar 增 5 个动画运行态字段全纳入 Clone/Hash。dotnet test 229/229。**待续**: AnimElemTime(n)/AnimElemNo(time) 复杂查询、interpolate/scale/blend(纯表现)、SFF 像素加载(M11 表现层) |
| **M9** 角色加载+整合 | DEF/CNS/CMD/AIR/SFF 全链路加载 → 跑起 KFM → **差分对账 Ikemen**（离散全等/连续容差） | M2,M5,M6,M7,M8 | 🔄 进行中。✅ M9.1 live pipeline(9c14d6f): Mugen/Battle/(MDefParser/MCharData/MCharLoader/MBattleEngine) 组装 命令→状态机→物理→动画→命中, 真实 KFM 加载并连跑60帧。✅ M9 物理+公共状态(97904e3): MPhysics(摩擦/重力, 移植 posUpdate)+SpawnChar 入场应用 Statedef 头部+接 common1(借 Terrarian)。dotnet test 240/240。**详见 Docs/M9_进度与续作.md**。**余下**: M9.2 Unity 渲染(需用户启 MCP)、物理/走跳补全、M9.3 真差分对账(查 Ikemen headless 抽 trace 可行性)、黄金哈希迁 MChar |
| **M10** 回滚 | 全 Char 状态 snapshot + RollbackPredictor + desync 哈希逐帧对账 | M9 | ⬜ |
| **M11** Unity 表现层对接 | 移植引擎 ↔ 现有 View（精灵/Clsn gizmo/输入采集） | M9 | ⬜ |

**第一个可玩里程碑** = M9（真实 KFM 能动能打、与 Ikemen 离散对账通过）。

> **⭐ M9 之后那条长尾（受击状态机/控制器补全/helper/projectile/回合机/差分对账/决斗场）已拆成可认领的
> TDD 任务队列：见 `Docs/对战完整化路线图.md`。** 含每模块封闭验收 harness、重构评估结论（不做结构性重构，
> 只做保真修补）、多 agent 认领表、决斗场 demo（最后做）。接手长尾工作先读那份。

---

## 5. 卡点/难点登记（遇到就记，先记后解，别卡住主线）

> 格式：`[日期] 模块 — 问题 — 现状/临时处理 — 待解方案`

- `[2026-06-03] Expr — 三角/超越函数定点化` — Ikemen 用 float sin/cos/atan2/exp/ln。定点近似会使连续量与 Ikemen 发散。临时：用 Fix64 自带确定性实现。待解：评估精度，必要时统一 LUT；差分测试连续量用容差而非全等。
- `[2026-06-03] Expr — BytecodeValue 单 float64 拆 int/float` — 见 §2.2，已定方案（long ival + FFloat fval）。
- `[2026-06-03] 全局 — 移植量 ~5 万行` — char.go 14k/bytecode.go 15k/compiler 15k。需多会话多 agent。对策：严格按 §4 依赖顺序，每个 OpCode/controller/trigger 是独立可测单元，增量移植增量测试。
- `[2026-06-03] 架构 — Ikemen 非 ECS、Char 巨struct` — 不强行 ECS 化，照搬 Char struct，仅加 Clone/WriteHash 接回滚。退役旧 ECS 战斗组件。
- `[2026-06-03] Expr — ToI 截断方向` — FFloat.ToInt 是 floor，Ikemen int32(float) 是向零截断。已在 BytecodeValue.ToI 自行向零截断。其它移植处凡 float→int 都要注意此差异。
- `[2026-06-03] Expr — OC_float 编码` — 我方 OC_float 携带 8 字节 FFloat raw（Ikemen 是 float32 bits）。属刻意偏离，M2 编译器必须按此编码产出。
- `[2026-06-03] Expr — 短路跳转` — 已在 M1 commit `3ba75b4` 补 OC_jmp/jz/jnz + 8bit + jsf8，peek 不 pop 与 offset 编码对齐 Ikemen；后续若编译器生成更复杂控制流，需继续加差分用例兜底。

---

## 6. 移植件文件头模板（每个移植文件必须带）

```csharp
// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/<file>.go  (<对应行段/结构名>)
// Adapted to fixed-point (FFloat) for deterministic lockstep/rollback. See Docs/移植方案_Ikemen.md.
```

---

## 7. 工作法（沿用 CLAUDE.md，强调几点）

- **TDD**：每个 OpCode/controller/trigger 移植 → 先写测试（含与 Ikemen 行为对照的离散用例）→ 实现 → `dotnet test` 全绿。
- **commit = ledger**：一个移植单元一个 commit，标 `[M<x>]`；同时更新本文进度表 §4 与 `Docs/执行日志.md`。
- **差分测试**：M9 起，在 `_reference/Ikemen-GO` 跑标准答案存 Golden，离散全等 / 连续容差。
- **诚实边界**：位级永不等于 Ikemen（定点）；长尾非战斗 controller（舞台/剧情/UI）不移植。
