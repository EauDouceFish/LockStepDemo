# LockstepActDemo

Unity 2022.3.55f1。横版 2D 帧同步格斗 demo。**网易火影忍者手游项目组实习生的岗前培训课题**——主题 "动作游戏中网络同步方案研究：帧同步与状态同步"。

> 5/10 24:00 简版已交付（KCP 双进程联机 + 4 态状态机），现在是 **polish 期**，目标推进到接近商业品质。

---

## 当前模式（**重要，与旧版不同**）

当前是 **工业级编码模式**，**不是**教学循环。
- Claude 直接写代码（不再走"我解释 → 你写 → 我 review"循环）
- 代码必须满足 [腾讯 C# 规范](../../C:/Users/25087/.claude/projects/D--Desktop-demo-LockstepActDemo/memory/feedback_csharp_style.md)：Allman `{}`、禁 var、禁单字母命名等
- 旧版"速通无防御性编程、无注释"风格 **作废**，按腾讯规范来

旧的教学循环 skill 还在 `.claude/skills/lockstep-tutor/SKILL.md` 里，**默认不激活**；用户明确说 "切回教学模式 / 这块我自己写" 时再启用。

---

## 项目方向（2026-05-26 校正）

**作废**（旧版定的、和当前不一致）：
- ~~严格对标火影手游 → 无主动跳跃，Y 仅受击飞驱动~~
- ~~单 Attack 按钮 + 多段连击~~

**当前**：
- 简化版 **横版 2D 格斗**（参考 Streets of Rage / KOF）
- 美术换成 [Streets of Fight 像素素材](C:\Users\25087\Downloads\Streets of Fight files\Streets of Fight files\Assets)（Brawler Girl + Enemy Punk + tileset/props）
- **有主动跳跃**（Jump / JumpKick / DiveKick），素材已经给了 jump.png
- 攻击拆 LP / HP / K 三键（Jab / Punch / Kick + 空中变形）
- 状态机重做为 **8 元状态 + 数据驱动 AttackTable**（见 [plan-fighting-state-machine](../../C:/Users/25087/.claude/projects/D--Desktop-demo-LockstepActDemo/memory/plan_fighting_state_machine.md)）

**坐标轴语义（保留旧版火影式伪 3D）**：
- `X` = 横向（左右走 + Facing）
- `Y` = 纵深（前后/远近，街机的双行步道）
- `Z` = 高度（**现在用作跳跃**，不再只是被击飞）
- 渲染：`screenY = Y + Z`，`sortingOrder ∝ -Y`，shadow 永远贴 `(X, Y, 0)`

---

## 技术栈（保留，**v1 已落地**）

| 模块 | 选择 |
|---|---|
| 定点数 | `asik/FixedMath.Net` 的 Fix64.cs（Q31.32, Apache 2.0） |
| 数学层 | F 前缀手写包装（`Lockstep.Math.*`，在 `Assets/Logic/Framework/Math/`） |
| 网络 | `limpo1989/kcp-csharp`，Loopback 模式（本机双开）/ KCP（跨进程 UDP） |
| 渲染 | Unity Sprite + Animator（仅表现层，逻辑层禁 UnityEngine） |
| 架构 | 自写极简 ECS（World / Entity / Component / System），rollback-ready 但 v1 没实现 |

### 三大铁律（继续生效）

1. 逻辑层（`Assets/Logic/`，受 asmdef `Lockstep.Logic` 隔离）严禁：
   - `float` / `double`
   - `UnityEngine.*`（Vector / Mathf / Random / Time / Physics）
   - `System.Random`
   - 单例
2. 一切跨子系统调用走接口（`IInputProvider` / `ITransport` / `IPredictor` / `ISnapshotSystem` / `IRandomProvider` / `IAudioPlayer`）。v1 用 Null 实现，v2 换真实现，业务 System 不动。
3. 所有 Component 实现 `IComponent.Clone()`；引用类型字段**必须深拷**（否则 Snapshot 会被串）。

---

## 已交付（5/10 23:00 状态）

- ✅ Framework：Core / Math / Input / Network / Predict 五层骨架
- ✅ Server：`Room` + `LockstepServer`（邮局模型，30Hz tick，input lag = 2 帧，输入到齐或超时广播）
- ✅ Client：`LockstepClient` 主循环 + `FrameInputBuffer` + `NullPredictor`
- ✅ Transport：`LoopbackHub` 本机双开 + `KcpClient/ServerTransport` 跨进程 UDP
- ✅ Bootstrap 三模式：Loopback / HostAndPlay / ClientConnect
- ✅ 战斗 v1：4 态状态机（Idle/Walk/Attack/Hit）+ 命中判定 + HP 100 / Damage 10 / K.O. log
- ✅ View：`BattleScene` + `PlayerView`（火影式 sortingOrder）+ `CameraFollow` + GameConfig SO + Resources prefab

操作（v1）：双方 WASD 走 + J 攻击。

---

## Polish 期清单（5/10 → 5/24 半月，**已超期到 5/26**）

| 优先级 | 模块 | 备注 |
|---|---|---|
| P0 | 战斗系统重做（数据驱动 AttackTable）| 当前主线，见 `plan-fighting-state-machine` |
| P0 | 报告 + 录屏 | 5/10 简版未交，待补 |
| P0 | 美术接入（Streets of Fight 素材）| 当前主线 |
| P0 | 回滚框架（Phase 8）| ISnapshotable + 内存快照 |
| P0 | 预测 + RollbackPredictor | 基于回滚框架 |
| P0 | Debug 工具（hash 对账 + 分叉定位）| |
| P1 | 多平台（Android + 虚拟摇杆）| |
| P1 | 弱网优化（抖动缓冲 / 自适应 input lag）| |
| 砍 | iOS / 完整 Replay / UI 美化 | 时间不够 |

---

## 文件 / 记忆指引

- 历史课程归档：`Lesson/Lesson01-06_*.txt` + `Day1_Summary.txt`（可直接挪进研究报告 3/4/6/7 节）
- 教学模式 skill（默认不激活）：`.claude/skills/lockstep-tutor/SKILL.md`
- 持久化记忆（每次启动会自动读）：`C:\Users\25087\.claude\projects\D--Desktop-demo-LockstepActDemo\memory\`
  - `feedback_csharp_style.md` — 腾讯 C# 规范（当前主规约）
  - `plan_fighting_state_machine.md` — 状态机重做方案
  - `project_overview.md` — 项目概览与坐标轴
  - `user_profile.md`、`project_tech_stack.md`、`project_architecture.md`、`project_art_workflow.md` 等

旧 deadline（5/10）已过，**当前没有硬 deadline**，目标是接近商业品质。
