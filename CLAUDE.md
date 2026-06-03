# LockstepActDemo — Agent 宪法（每次开工必读）

Unity 2022.3.55f1 + .NET 8（测试）。**MUGEN 化定点帧同步格斗引擎**。
网易火影忍者手游项目组实习岗前课题，主题《动作游戏中网络同步方案研究：帧同步与状态同步》。

> 本文是所有协作者（含 AI agent）的**强制规约**。设计细节见 `Docs/架构设计.md`，
> 步骤/任务见 `Docs/细化计划.md`，进度见 `Docs/执行日志.md`。冲突时以本文 + 这三份 Docs 为准。

---

## 0. 当前方向（2026-06-02，覆盖一切旧描述）

把项目从"6 态硬编码状态机"大改为 **MUGEN 化引擎**：

```
【2026-06-03 起】Ikemen GO 战斗系统 1:1 忠实移植（逻辑/结构照搬 Go 源码，float→定点 FFloat）
  + MUGEN 资源导入管线（SFF/AIR/CMD/CNS）
  + 定点确定性（FFloat + hash 对账 + 回滚框架）
目标：像 Ikemen 一样直接吃别人的 MUGEN 角色文件战斗 + 保回滚。Ikemen MIT 许可，移植合法。
详见 Docs/移植方案_Ikemen.md（最高纲领）。
注：原"只抄设计、不复制 Go"的 clean-room 路线已废弃（无法兼容真实角色）。
```

- **战斗模拟 0 Lua**：角色行为 = 数据，被 C# 定点表达式 VM 解释。**禁止**为战斗逻辑引入 Lua/XLua。
- **v1 只做 MUGEN-2D 模式**：X 横向 + Y 高度（MUGEN 原生坐标，上为负），纯 2D 平面对拳。
  火影纵深 3D 是 **v2 独立支路**，21 天内不碰。模式专属逻辑隔离在 4 个 seam（Physics/Collision/输入映射/View），命名带 `Mode2D`，共享层不掺 `if(mode)`。
- 第一个测试角色 = **KFM（功夫男）**，先跑规整角色，复杂角色后置。
- 旧描述（火影伪 3D 主线、Streets of Fight 美术、8 态 AttackTable、教学模式）**全部作废**。

---

## 1. 三条铁律（编译期 / 评审强制，不可破）

1. **逻辑层纯定点、纯 C#**：`Assets/Logic/`（asmdef `Lockstep.Logic`，noEngineReferences）严禁
   `float`/`double`、`UnityEngine.*`、`System.Random`、Lua、单例。数值走 `FFloat`，随机走 `World.Random`(`FRandom`)。
   表达式 VM 内部运算也必须全 `FFloat`。
2. **一切跨子系统调用走接口**（`IInputProvider`/`ITransport`/`IPredictor`/`IExpressionVM`/...）。v1 可挂 Null/Static 实现。
3. **Component = 纯数据 + snapshot-safe**：每个 `IComponent` 实现 `Clone()`（引用/数组字段**深拷**）+ `WriteHash()`（只混整数 raw）。
   **System / VM 无静态可变状态**——运行时态全写回 Component，否则回滚串状态。

> 导入工具（`Assets/Editor/`）不受此约束（产出的是数据，可用 float/IO）。

---

## 2. 工作法（TDD + 差分测试 + 黄金哈希）

- **测试先行**：每个工作单元先写会失败的测试 → 实现 → 全绿。无测试的代码不合并。
- **测试工程**：`Tests/Lockstep.Logic.Tests`（.NET，link 编入 Logic + Fix64 源码）。验收只跑
  `dotnet test`（脱 Unity，秒级）。**新增逻辑代码必须能被它编到**（即保持零 Unity 依赖）。
- **测试按模块分目录（强制）**：测试文件**不许平铺在工程根**。每个模块在 `Tests/Lockstep.Logic.Tests/`
  下有自己的子目录，**镜像被测源码的模块树** `Assets/Logic/<模块>/`。例：
  `Assets/Logic/Mugen/Expr/` → `Tests/.../Mugen/Expr/`，`.../Mugen/Char/` → `.../Mugen/Char/`。
  新模块先建好命名的测试子目录再往里写用例；csproj 递归 glob 编译，移动/新建文件无需改 csproj。
  差分基准数据存 `Tests/.../Golden/`。（根目录现存的旧 clean-room 平铺测试属退役件，删一个退一个，不再新增平铺测试。）
- **差分测试**：以 `../MugenSource/_reference/Ikemen-GO`（只读 Oracle）为标准答案。离散量（状态号/动画帧/命中/KO）必须**全等**，连续量（位置/速度）**容差内**一致。
- **黄金哈希**：固定输入跑 N 帧，断言 `World.ComputeHash()` == 预录值。改了行为就回填新值并在 commit 说明。

---

## 3. 完成定义（DoD）—— 五条全过才算完，不全过不进下一个

1. 该任务的测试全绿
2. 整套 `dotnet test` 全绿（不回归）
3. 黄金哈希测试通过
4. 符合腾讯 C# 规范（Allman `{}`、禁 `var`、禁单字母命名、补注释）
5. 写 commit + `Docs/执行日志.md` 一条

---

## 4. 提交规范（commit = ledger）

每个工作单元一个 commit：
```
<type>(<模块>): <一句话做了啥>  [T<编号>]

- 关键改动点
- 验收：跑了哪些测试 + 结果
Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
`type` ∈ feat/fix/refactor/test/chore/docs。同时在 `Docs/执行日志.md` 追加人类可读摘要（绝对日期）。

---

## 5. 多 agent 协议

- 一个 agent 认领一个 `Docs/细化计划.md` 里的 `T*`，在 **git worktree 隔离**中干。
- 无依赖的任务可并行多开；依赖见各阶段说明。
- 收尾按 DoD 5 条；冲突由黄金哈希测试兜底回归。

---

## 6. 技术栈

| 模块 | 选择 |
|---|---|
| 定点数 | `FixMath.NET` 的 Fix64（Q31.32），F 前缀包装在 `Assets/Logic/Framework/Math/` |
| 网络 | KCP（跨进程）/ Loopback（本机双开）；v1 延迟锁步，回滚 = Stretch |
| 渲染 | Unity Sprite/Animator（仅表现层，逻辑层禁 UnityEngine） |
| 架构 | 自写极简 ECS（World/Entity/Component/System），Snapshot/Hash/Desync 已就绪、rollback-ready |

---

## 7. 关键路径

- 原始 MUGEN 素材：`../MugenSource/<角色>/`（仓库外，已 gitignore）
- Ikemen 只读 Oracle：`../MugenSource/_reference/Ikemen-GO/src/`（`bytecode.go` controller、`compiler.go` 表达式、`char.go` 运行时、`anim.go` AIR、`image.go` SFF）
- 设计/计划/日志：`Docs/`
- 持久记忆（每次启动自动读）：`C:\Users\25087\.claude\projects\D--Desktop-demo-LockstepActDemo\memory\`（`feedback_csharp_style.md` 是 C# 规范）

---

## 8. 当前进度 + 接手导航（每个 agent 开工先读这节）

### 8.1 文档地图（"东西都在哪"）
| 你要找 | 在哪 |
|---|---|
| **强制规约/铁律/工作法** | 本文 `CLAUDE.md`（§1 铁律、§2 工作法、§3 DoD、§4 提交规范） |
| **架构基准**（分层、ECS、定点、模式 seam） | `Docs/架构设计.md` |
| **⭐ 当前最高纲领：Ikemen 1:1 移植方案**（2026-06-03 起，取代旧 clean-room 路线） | `Docs/移植方案_Ikemen.md`（架构/float→定点契约/目录/移植顺序+进度表/卡点登记） |
| **技术路线 + 任务拆分**（旧 clean-room Phase/T，已被移植路线取代，仅留史） | `Docs/细化计划.md` |
| **进度流水账**（人类可读，按时间） | `Docs/执行日志.md`（最新一条在末尾“RESUME/续N”） |
| **commit 流水**（= 第二份 ledger） | `git log --oneline`（每个 T 一个 commit，带 `[T*]`） |
| **跨会话记忆**（背景/决策/坑） | `C:\Users\25087\.claude\projects\D--Desktop-demo\memory\`（`project_lockstep_act_demo.md` 是主进度记忆）。注意另有 `...\D--Desktop-demo-LockstepActDemo\memory\feedback_csharp_style.md` = C# 规范 |
| **MUGEN 素材 / Ikemen Oracle** | 见 §7 |
| **测试入口** | `cd Tests/Lockstep.Logic.Tests && dotnet test`（脱 Unity，秒级） |
| **Unity 实测** | Unity MCP（`mcp__UnityMCP__*` 工具，会话启动时加载）；菜单 `MUGEN/Demo/*` 建 demo/showcase/画廊场景 |

### 8.2 已完成（都在 git，`dotnet test` 45/45 绿）
- **Phase 0** 地基：.NET 测试工程、数据 schema、表达式 VM、ECS 组件、黄金哈希。
- **Phase 1** 导入：AIR 解析、SFFv1(PcxDecoder)、**SFFv2(SffV2Reader：Lz5/Rle8/Rle5/raw + 调色板库)**、AnimAdvance、ClsnWorld。
- **Phase 2** 引擎：表达式 VM + WorldEvalContext + MugenStateC + StateControllerExecutor(12 controller) + StateMachine/Physics/Anim System + CmdParser + CnsParser。
- **表现层工具**（`Assets/Scripts/Game/View/` + `Assets/Editor/Demo/`）：`MugenSpriteLoader`(v1/v2 自动分派) / `MugenDef`(读 .def [Files] 取正确 sprite/anim) / 单角色 demo / 全动作 showcase / **多角色画廊**。素材：12 角色（11 SFFv1 + kfm SFFv2），KFM 像素级渲染验证通过。
- 已知局限：SFFv1 未加载外部 `.act` 调色板（Janos 因此显黑剪影，已查实非 bug，详见执行日志/记忆）。

### 8.3 ✅ Phase 3（搓招+碰撞+命中）已完成（dotnet test 64/64）
- **T3.1 CommandSystem**：`CommandInputC`(方向+按钮 60 帧环形缓冲) + `CommandMatcher`(方向按朝向转相对 + 序列贪心匹配，末符号当前帧边沿触发) + `CommandSystem`→写 `CommandStateC.Active`。
- **T3.2 HitDef 控制器**：`StateControllerExecutor` case HitDef 激活 `HitDefStateC`；`ApplyTransition` 切状态停用。
- **T3.3 CollisionSystem**：`ClsnWorld.AnyOverlap`(Clsn1×Clsn2 facing 镜像) + `PendingHitC` + `CollisionSystem`(HitTargetsBits 防一招多次命中)。
- **T3.4 HitSystem**：消费 PendingHitC → 扣血/硬直/击退(朝向镜像)/切 BeingHit+state 5000/双方 hitstop。
- 关键事实：`FrameInput`=MoveX/MoveY+Buttons(方向在 MoveX/MoveY 不在 Buttons)；`TransformC.Pos`=FVector3、`FacingX`=FFloat(±1)；当前帧=`Characters[id].Anims[AnimNo].Frames[FrameIndex]`。
- **系统执行顺序**（组装 live pipeline 时）：InputBuffer/Command → StateMachine → Physics → Anim → Collision → Hit。

### 8.4 ▶▶ 重大方向变更（2026-06-03）：转 **Ikemen GO 1:1 移植（定点化）**
- **决策**：放弃 clean-room 简化子集，改为把 Ikemen 战斗系统**逻辑/结构 1:1 忠实移植**进来（float→定点 FFloat，挂回滚底座），目标=**直接吃别人的 MUGEN 角色文件并战斗 + 保回滚**。Ikemen MIT 许可，移植合法（保留版权头）。
- **一切以 `Docs/移植方案_Ikemen.md` 为准**（含 §4 进度表、§5 卡点登记）。接手者读那份。
- **新引擎根**：`Assets/Logic/Mugen/`（镜像 Ikemen 模块：Expr/Char/State/StateCtrl/Command/Hit/Anim/Parse/System）。
- **进度**：M0（BytecodeValue 定点版）🔄起步、已起（dotnet test 70/70）。下一步 M1 表达式字节码 VM（BytecodeExp OpCode 执行器）。
- **退役中（移植件取代后才删，git 留史）**：旧 clean-room 战斗码 `Assets/Logic/Game/Expr|Systems|Command` + Phase0-3 成果（ExpressionVM/各 System/CommandMatcher/HitSystem…）。**保留**：Framework(定点/Hash/Snapshot)、Import/Sff、Unity 表现层。
- 旧 `BattleGameLogic`(PlayerStates 栈) 仍是更早的遗留，勿混。
