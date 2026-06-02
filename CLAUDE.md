# LockstepActDemo — Agent 宪法（每次开工必读）

Unity 2022.3.55f1 + .NET 8（测试）。**MUGEN 化定点帧同步格斗引擎**。
网易火影忍者手游项目组实习岗前课题，主题《动作游戏中网络同步方案研究：帧同步与状态同步》。

> 本文是所有协作者（含 AI agent）的**强制规约**。设计细节见 `Docs/架构设计.md`，
> 步骤/任务见 `Docs/细化计划.md`，进度见 `Docs/执行日志.md`。冲突时以本文 + 这三份 Docs 为准。

---

## 0. 当前方向（2026-06-02，覆盖一切旧描述）

把项目从"6 态硬编码状态机"大改为 **MUGEN 化引擎**：

```
Ikemen GO 引擎结构（Statedef + Controller + 表达式 VM，只抄设计/读源码，不 fork、不复制 Go、不引浮点）
  + MUGEN 资源导入管线（SFF/AIR/CMD/CNS → 自有数据格式）
  + 自有定点确定性（FFloat + hash 对账 + 回滚框架）
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

## 8. 当前进度

Phase 0（工程化地基）已完成：.NET 测试工程、数据 schema 骨架、表达式 VM 最小实现、新增组件、第一个黄金哈希测试（`dotnet test` 12/12 绿）。
**下一步：Phase 1 导入管线（AIR + SFF），让 MUGEN 角色在 Unity 动起来。** 详见 `Docs/细化计划.md`。
