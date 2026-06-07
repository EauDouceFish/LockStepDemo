# 05 Oracle trace、确定性 hash 与回滚审计

## 1. 审计范围与基线

本文件只审计以下四项，不评估角色导入、控制器覆盖、表现层或 Unity 场景：

1. `MBattleTrace` 是否足以作为 Ikemen 差分 Oracle。
2. `MBattleEngine.ComputeHash()` 是否覆盖完整确定性状态。
3. MUGEN 战斗运行时是否具备可用的快照、恢复和重演能力。
4. 如何在 Ikemen GO 上建立可执行的 headless 输入与逐帧差分流水线。

审计对象：

- `Assets/Logic/Mugen/Battle/MBattleTrace.cs`
- `Assets/Logic/Mugen/Battle/MBattleEngine.cs`
- `Assets/Logic/Mugen/Char/Char.cs`
- `Assets/Logic/Mugen/Char/MEntityWorld.cs`
- `Assets/Logic/Mugen/Char/MProjectile.cs`
- `Assets/Logic/Mugen/Char/MPauseState.cs`
- `Assets/Logic/Mugen/Command/MCommandList.cs`
- `Assets/Logic/Mugen/Command/MInputBuffer.cs`
- `Assets/Logic/Framework/Core/World.cs`
- `Assets/Logic/Framework/Core/Hash64.cs`
- 相关 NUnit 测试

Ikemen 参考固定为：

- 仓库提交：`843f7327efda98f6d47edbf38f45420b88cee3c8`
- 关键文件：`src/system.go`、`src/char.go`、`src/input.go`、`src/state.go`、`src/state_clone.go`、`src/rollback.go`

当前工作机没有可用的 Go 命令，因此本次完成的是可执行设计和源码插桩审计，没有实际构建 Ikemen Oracle 二进制。

## 2. 总结论

当前状态不能称为 Oracle 或 MUGEN 回滚已完成。

| 模块 | 结论 | 严重度 |
|---|---|---|
| `MBattleTrace` | 仅是少量字段的调试快照；缺少完整实体、全局状态、事件、输入、版本头和稳定身份映射 | P0 |
| Trace comparer | 漏比已记录字段，按列表下标配对实体，单一全局容差会掩盖错误 | P0 |
| Native hash | 缺 `FrameNo`、多个行为引用、资源身份及 projectile 行为数据；初始 FNV 状态使用错误 | P0 |
| MUGEN snapshot | 不存在 `MBattleEngine` 级 Snapshot/Restore；通用 ECS `WorldSnapshot` 对此战斗引擎无效 | P0 |
| Clone 基础 | `MChar.Clone()` 和 `MEntityWorld.Clone()` 只是局部克隆，没有完成对象图重链 | P0 |
| Rollback orchestration | 没有输入历史、快照环、预测纠正、重演、提交帧和表现事件去重 | P0 |
| Ikemen 差分 | 没有固定输入注入、帧边界 hook、标准 trace 文件和 CI | P0 |
| 现有测试 | 证明同一 C# 实现重复运行可复现；没有证明状态覆盖完整，也没有证明与 Ikemen 等价 | P1 |

正式收口必须同时建立三种不同契约，不能继续混用一个 `Hash` 字段：

1. **Native state hash**：同一 C# 构建的完整可回滚状态必须逐位一致。
2. **Semantic trace/hash**：C# 与 Ikemen 的规范化可观测战斗语义可比较。
3. **Presentation event journal**：音效、震屏等输出可在回滚重演后正确提交且不重复播放。

## 3. 现有 `MBattleTrace` 缺项

### 3.1 Recorder 缺项

当前 `MEntityTrace` 只有：`Id/StateNo/StateTime/AnimNo/AnimElem/Pos/Vel/Life/Power/Ctrl`。

至少缺少：

- 身份：player slot、helper index/type、owner/root/parent/partner/state owner 的稳定身份。
- 状态头：`PrevStateNo`、`StateType`、`PrevStateType`、`MoveType`、`Physics`、`PendingStateNo`。
- 动画：`PrevAnimNo`、`AnimElemTime`、`AnimCurTime`、`AnimTime`、`AnimLoopEnd`、动画资源 owner。
- 命中：`Hitstop`、`PendingLifeDamage`、`HitDef` 摘要、`Ghv` 摘要、guard、hit-by、hit override。
- 连段：`HitCount`、`UniqHitCount`、`GuardCount`、`ReceivedHits`、move contact/hit/guard/reverse。
- 绑定和 target：bind target/time/pos/facing、target 集合、juggle 点。
- 输入：本帧原始输入、命令缓冲摘要、active command 集合、输入计时器。
- 变量：`var/fvar` 和 persistent counter 的稳定摘要；诊断级 trace 还需完整值。
- 暂停：角色 pause flags/movetime 和全局 pause/superpause 状态。
- 全局：RNG seed、`FrameNo`、next entity id、round state、实体创建/删除序列。
- Projectile：只有总数，没有逐实体状态。
- Explod：只有总数，没有逐实体状态。
- Helper：Recorder 会记录，但 comparer 完全不比较。
- 事件：命中、防御、状态切换、projectile contact、创建/删除等诊断事件。
- 运行清单：引擎版本、Ikemen commit、角色/公共状态/舞台内容 hash、localcoord、初始 seed。

### 3.2 Comparer 的明确错误

当前 comparer 存在以下直接漏检：

- 不比较 `MBattleTraceFrame.Hash`。
- 不比较 `Helpers` 数量和内容。
- 不比较 `AnimElem`。
- 不比较 `Power`。
- 不比较 `Ctrl`。
- 不比较 frame number 本身，只用 expected frame 填错误信息。
- 玩家按列表下标配对，实体插入顺序变化后会产生错误配对。
- 所有定点字段共用一个 raw tolerance，无法区分位置、速度、缩放等误差预算。
- 没有 first-difference 上下文窗口、差异分类和最大差异限制。
- expected/actual 数量不同时仍继续按最短列表比较，不能指出缺失的稳定实体 key。

因此现有测试 `RecorderAndComparer_ReportFirstSemanticDifference` 只证明一个字段能报错，不证明 comparer 完整。

## 4. Trace schema v1

### 4.1 文件形式

采用 UTF-8 JSON Lines。每行独立，可流式写入，进程崩溃时仍保留此前帧。

文件由三类记录组成：

1. 一条 `header`。
2. N 条 `frame`。
3. 一条 `end`。

禁止依赖 JSON 对象字段顺序计算 hash。语义 hash 使用第 8 节定义的二进制规范编码。

### 4.2 Header

```json
{"type":"header","schema":"mugen-oracle-trace/v1","producer":{"engine":"ikemen-go","commit":"843f7327efda98f6d47edbf38f45420b88cee3c8","patch":"oracle-v1"},"scenario":{"id":"kfm-vs-kfm-basic-v1","input_sha256":"...","seed":1,"max_steps":600,"start_policy":"natural_match"},"content":{"p1_def_sha256":"...","p2_def_sha256":"...","common_sha256":"...","stage_def_sha256":"..."},"numeric":{"canonical_space":"world320","quantization":"q20-round-even"}}
```

必填约束：

- `schema`、producer commit 和 Oracle patch 版本必须参与 CI 缓存 key。
- 所有会改变战斗逻辑的资源必须有内容 hash，不能只记录路径。
- `seed` 是第一帧执行前的全局 RNG seed。
- `start_policy` 只能为 `natural_match` 或 `forced_fight_fixture`。
- Golden 主线只接受 `natural_match`；后者仅用于控制器级最小复现。

### 4.3 帧边界

统一定义：

- `step=N` 表示已经消费输入脚本第 N 帧，并完成该帧状态机、物理、动画、碰撞、命中和帧末计时。
- `phase` 固定为 `post-combat-tick`。
- C# 在 `MBattleEngine.Tick(inputs)` 返回后采集，记录调用前的 frame 作为 `step`。
- Ikemen 在 `System.action()` 中 `globalCollision()` 和 `globalTick()` 都完成后采集。
- 初始未执行输入的状态如需诊断，单独使用 `type=initial`，不得伪装成 `step=0`。

这条定义用于消除目前 `FrameNo++` 与 Ikemen `tickFrame/tickNextFrame` 的一帧歧义。

### 4.4 Frame

```json
{"type":"frame","step":0,"phase":"post-combat-tick","inputs":[{"slot":0,"bits":24,"analog":[0,0,0,0,0,0]},{"slot":1,"bits":0,"analog":[0,0,0,0,0,0]}],"global":{"rng_seed":282475249,"pause":0,"superpause":0,"round_state":2,"next_entity_id":1000},"entities":[{"key":"p0","kind":"player","id":1,"owner":"p0","state":{"no":20,"prev":0,"time":0,"type":1,"move_type":1,"physics":1,"ctrl":true},"anim":{"no":20,"prev":0,"elem":1,"elem_time":0,"cur_time":0,"remaining":-1,"anim_owner":"p0","sprite_owner":"p0"},"transform":{"localcoord":[320,240],"pos_local_q20":[0,0,0],"pos_world_q20":[0,0,0],"vel_local_q20":[0,0,0],"vel_world_q20":[0,0,0],"facing":1},"vitals":{"life":1000,"power":0},"links":{"p2":"p1","root":"p0","parent":null,"partner":null,"state_owner":"p0","targets":[]},"hashes":{"state":"...","vars":"...","commands":"..."}}],"projectiles":[],"explods":[],"events":[],"hashes":{"native":"...","semantic":"...","global":"...","entities":"..."}}
```

### 4.5 数值编码

跨引擎连续量不能直接比较 C# Q31.32 raw 和 Ikemen float32 bits。

规范值统一为 `q20` 有符号整数：

```text
q20 = round_to_even(value_in_world320_space * 2^20)
```

- Ikemen 同时可在 diagnostic 字段记录原始 float32 bits。
- C# 同时可记录原始 Q31.32 raw。
- 语义比较只使用规范 q20。
- `pos_world = pos_local * localscl`；Ikemen 的 `localscl=320/localcoord`。
- 在 C# 完成 localcoord 前，非 320 角色必须标记 `unsupported_numeric_space`，不能悄悄按 320 比较。

### 4.6 稳定实体 key

跨引擎不得把运行时 raw ID 当作主键，因为两边分配器不必相同。

- 玩家：`p{playerNo}`。
- Helper：`p{ownerPlayerNo}/h{helperIndex}`；同时记录 helper id 和创建序号。
- Projectile：`p{ownerPlayerNo}/proj{ownerCreationOrdinal}`。
- Explod：`p{ownerPlayerNo}/explod{ownerCreationOrdinal}`。

`target/root/parent/state_owner` 等引用都转换为稳定 key。raw ID 只作诊断。如果同 owner 的创建顺序已经不同，应首先报告 `entity-lifecycle` 差异，不再拿错误实体继续比较。

### 4.7 Trace 级别

- `core`：每帧全局、角色、helper、projectile、explod 和 subsystem hash。每次 CI 必跑。
- `diagnostic`：额外输出完整 var/fvar、command buffer、GHV、HitDef、碰撞框摘要。首次差异前后窗口重跑时启用。
- `events`：状态切换、命中、防御、创建、删除、RNG 消费点。用于定位，不作为唯一正确性依据。

## 5. 输入脚本格式

文件采用 JSON，schema 为 `mugen-input-script/v1`：

```json
{
  "schema": "mugen-input-script/v1",
  "scenario": "kfm-vs-kfm-basic-v1",
  "seed": 1,
  "players": 2,
  "start_policy": "natural_match",
  "segments": [
    {"at": 0, "for": 30, "bits": [0, 0]},
    {"at": 30, "for": 8, "bits": [8, 0]},
    {"at": 38, "for": 1, "bits": [24, 0]},
    {"at": 39, "for": 61, "bits": [0, 0]}
  ]
}
```

位定义固定为 Ikemen `InputBits` 的 14 键布局：

| bit | 输入 | bit | 输入 |
|---:|---|---:|---|
| 0 | U | 7 | x |
| 1 | D | 8 | y |
| 2 | L | 9 | z |
| 3 | R | 10 | s |
| 4 | a | 11 | d |
| 5 | b | 12 | w |
| 6 | c | 13 | m |

规则：

- L/R 是屏幕绝对方向，不是 B/F；朝向换算由引擎自己执行。
- segment 必须按 `at` 升序且不可重叠；空洞帧自动填 0。
- 可选 `analog` 为每玩家 6 个 `int8`，范围 `[-128,127]`。
- C# 当前没有 d/w/m 输入位。Oracle 导入器遇到这些位必须显式失败或标记 unsupported，禁止丢位后继续通过。
- 禁用 AI、键盘、摇杆、replay、netplay 的其他输入源，Oracle 输入拥有最高优先级。
- 每条 trace frame 必须回写本帧实际注入的 bits，用于排除输入管线差异。

## 6. Ikemen 插桩方案

### 6.1 最小补丁边界

在固定提交上增加独立 `oracle_trace.go`，并尽量只在现有代码加入四个 hook：

1. `src/main.go`：解析 `-oracle-config/-oracle-input/-oracle-trace/-oracle-maxframes/-oracle-seed`。
2. `src/input.go` 的 `CommandList.InputUpdate`：在 AI/replay/net/rollback/local 分支之前读取 Oracle bits。
3. `src/system.go` 的 `System.action`：在 `globalCollision()` 和 `globalTick()` 后调用 `oracle.RecordPostTick()`。
4. `src/system.go` 完成 `SetupCharRoundStart()` 和 `resetRound()` 后调用 `oracle.RecordHeader()` 并应用最终 seed。

不得把 trace 逻辑散落进控制器和表达式 VM。诊断事件通过小型 event sink 注入状态切换、命中和实体生命周期入口，默认关闭。

### 6.2 为什么 hook 在 `globalTick()` 后

Ikemen 的关键顺序是：

```text
stepRoundState -> stage.action -> charList.action
-> charUpdate -> fightScreen.step -> globalCollision -> globalTick
```

在 `charList.action()` 后立即记录会漏掉本帧碰撞和命中；在 render/update 后记录又混入表现和宿主循环状态。`globalTick()` 后是最接近 C# `MBattleEngine.Tick()` 返回点的战斗语义边界。

### 6.3 Headless 启动

第一阶段可以复用 Quick VS：

```text
Ikemen_GO -p1 <char1> -p2 <char2> -s <stage> -rounds 1 -time -1 \
  -nosound -nomusic -nojoy -oracle-config scenario.json \
  -oracle-input input.json -oracle-trace ikemen.jsonl -oracle-seed 1
```

Linux CI 若图形初始化仍不可避免，先用 `xvfb-run`。最终应增加 build tag `oracle_headless`，跳过窗口、音频和 render，但必须继续调用同一个 `System.action()`，不能另写一套简化战斗循环。

### 6.4 防止插桩改变结果

- trace 不得调用 trigger、RNG 或会懒初始化的数据访问器。
- map 输出前按 key 排序；实体按稳定 key 排序。
- 使用有界 buffered writer，写失败立即终止，不允许丢帧。
- 不记录指针地址、Go map 迭代顺序、格式依赖字符串或 wall clock。
- Oracle patch 自身必须有独立 commit/hash，并写入 header。

### 6.5 不复用 Ikemen 现有 checksum 作为 Oracle

Ikemen 的两个现成 checksum 都不满足本项目的差分要求：

- `GameState.Checksum()` 在 `src/state.go` 中明确标为 debugging，用 `GameState.String()` 的不完整文本做 FNV-32a。
- `RollbackSession.LiveChecksum()` 只检查 RNG、时间和根角色的 life/redlife/dizzy/guard/power/anim，位置甚至因 float 误差被注释掉。

它们适合 Ikemen 自己的轻量 desync 告警，不是完整状态证明，也不能与 C# native hash 对比。Oracle 必须使用本文件定义的独立 semantic trace/hash。

## 7. 比较规则与容差

### 7.1 必须精确相等

- frame/step、输入 bits、RNG seed。
- 实体稳定 key 集合和实体类型。
- state no/time/type/move type/physics/ctrl。
- anim no/elem/elem time/anim time 和资源 owner。
- life、power、juggle、hit counts、pause/hitpause timers。
- target/root/parent/state owner 等关系。
- projectile/explod 的创建、删除、owner、编号、时间和命中计数。
- var/sysvar 等整数值。
- 所有 bool、enum、bit flags。

### 7.2 连续量默认容差

比较 q20 整数，按字段配置，禁止单一全局 tolerance：

| 字段 | 默认绝对容差 | q20 raw | 说明 |
|---|---:|---:|---|
| position world320 | `1/1024` | 1024 | float32 与 Q31.32 长链积分允许极小漂移 |
| velocity/accel | `1/4096` | 256 | 速度差会继续放大，需更严 |
| scale/multiplier | `1/65536` | 16 | 倍率通常直接影响离散结算 |
| collision bounds | `1/4096` | 256 | 超限必须失败 |

额外规则：

- 生命、伤害、能量等最终整数不允许容差。
- 只要连续量超限即失败，不能等到离散状态分叉才失败。
- 报告同时给出首次超限帧和首次离散分叉帧。
- 不允许“累计容差随帧数增长”；否则长期漂移会被合法化。
- 特定已知 Ikemen float bug 只能按 `exception_id + source line + expiry` 建白名单，不能扩大全局容差。

### 7.3 差异分类

比较器输出以下稳定类别：

- `input`
- `entity-lifecycle`
- `state-machine`
- `animation`
- `physics-numeric`
- `hit-resolution`
- `resource-owner`
- `rng`
- `rollback-only`
- `trace-schema`

失败 artifact 保存首次差异前后各 30 帧的双方 diagnostic trace。

## 8. Hash 设计

### 8.1 两种 hash

#### Native state hash

用途：C# 同构客户端不同步检测、snapshot round-trip、rollback resimulation。

- 必须覆盖“会改变未来模拟结果”的全部运行态。
- 不允许容差。
- 算法和字段顺序带显式版本，例如 `mugen-native-state/v1`。
- 资源静态内容不必每帧重哈希，但其 manifest hash 必须进入对局初始 hash。

#### Semantic hash

用途：快速筛选 C# 与 Ikemen 是否需要逐字段比较。

- 从 Trace schema 的规范字段生成。
- 连续量先转换为 q20。
- 稳定实体 key 排序。
- 使用规范二进制编码和 SHA-256；不能直接 hash JSON 文本。
- Semantic hash 相等可快速通过；不等时必须逐字段给出差异，不能只报 hash。

### 8.2 当前 `ComputeHash()` 的问题

1. 使用 `new Hash64()`，初始值为 0；正确 FNV-1a 入口应为 `Hash64.Create()`。
2. 不包含 `FrameNo`。
3. `MChar.WriteHash()` 不包含 `P2/Root/Parent` 引用身份，这些引用会改变 redirect、team 和 helper 行为。
4. `_helperData`/角色资源 owner 身份不在 hash 中，状态表绑定不同但当前值相同时可能同 hash。
5. `MProjectile.WriteHash()` 不包含会影响未来命中的 `HitDef` 内容，不能把行为模板简单排除。
6. `MEntityWorld.WriteHash()` 对 spawn/projectile queue 只写数量、不写内容。若规定只在帧末快照，则必须断言队列为空；否则必须完整 hash。
7. 角色/helper/projectile/explod 未按稳定 ID 排序，hash 依赖容器顺序。
8. CommandList/Input 的 null 与非 null 没有存在位，理论上可产生状态别名。
9. `Hash64.AddString()` 没有长度或终止域，字符串序列缺少无歧义编码。
10. `Clsn1/Clsn2` 被视为派生状态可以不 hash，但必须有“restore 后在任何命中读取前完成重建”的强制不变量。
11. `MFrameEvents` 排除 hash 是合理的，但只在它严格属于可重建输出，且提交层具备回滚去重时成立。

### 8.3 Hash 完整性守卫

手写 `Clone/WriteHash` 必须增加自动化守卫：

- 每个 mutable 字段都登记为 `state`、`derived`、`static`、`presentation` 四类之一。
- 对 `state` 字段做 mutation sensitivity test：只改该字段，native hash 必须变化。
- 对 `derived` 字段做 restore-rebuild test。
- 新增 public mutable 字段而未登记时 CI 直接失败。
- 每个 subsystem 输出独立 hash：global、entities、commands、projectiles、explods，便于定位。

## 9. Snapshot 与 rollback 审计

### 9.1 当前断层

`Assets/Logic/Framework/Core/World.Snapshot()` 只服务旧 ECS `World`。`MBattleEngine` 不使用该 World，因此现有 `SnapshotTests` 和 `GoldenHashTests` 不能证明 MUGEN 战斗可回滚。

`MBattleEngine` 当前没有：

- `Snapshot()`。
- `Restore(snapshot)`。
- snapshot frame/version/resource manifest。
- 输入历史和预测标记。
- 快照环形缓冲。
- 从纠正帧恢复并重演到当前帧的协调器。

### 9.2 对象图必须整体恢复

Battle snapshot 至少包含：

- `FrameNo`、RNG seed、pause state、next entity id。
- 玩家、helper、projectile、explod 的全部运行态。
- helper 对应的 `MCharData` 资源身份。
- command buffers、active commands、input timing。
- spawn queues；或帧末快照不变量“所有 simulation queue 均为空”。
- 所有引用边：P2、Root、Parent、Partner、StateOwner、BindTarget、Targets、Projectile.Owner。

恢复必须采用两阶段：

1. 按稳定 ID 创建所有对象和数据副本。
2. 通过 ID map 重链全部对象引用和共享单例引用（RNG、Pause、World）。

当前 `MChar.Clone()` 保留旧图引用，`MEntityWorld.Clone()` 又浅拷 Helpers，不能直接作为 Battle snapshot。

### 9.3 派生状态

允许不进 snapshot 的字段必须可在恢复后无副作用重建：

- 当前动画帧的 `Clsn1/Clsn2`。
- 只读 `Constants/AnimTable/MCharData` 引用。
- scratch list，例如 `_hitEntities`。
- 本帧 presentation events。

恢复完成后必须先执行 `RebuildDerivedState()`，然后才允许 trigger、碰撞或 hash 读取。

### 9.4 表现事件提交

回滚重演会再次生成声音等事件。正确模型是：

- simulation 每帧生成 `(frame,eventIndex,eventType,payload)`。
- 预测帧事件可选择低延迟预播，但必须可取消；默认先只播放 confirmed frame。
- presentation 以 `(frame,eventIndex)` 去重。
- rollback 到 frame N 时丢弃所有 `frame >= N` 的未确认事件，再由重演重新生成。
- events 不进入 native state hash，但 event journal 本身要做独立确定性测试。

### 9.5 必须新增的回滚测试

1. Snapshot 后任意修改运行态，Restore 后 native hash 完全恢复。
2. 同一 snapshot 可重复 Restore，不被第一次 Restore 污染。
3. 在 0..N 每个可能帧点回滚并重演，最终每帧 hash 与直跑一致。
4. 覆盖 helper/projectile/explod 创建和删除跨越回滚点。
5. 覆盖 random trigger、pause/superpause、custom state、bind 和 target 链。
6. 覆盖输入纠正导致未来实体生命周期改变。
7. 确认表现事件不重复提交。
8. Windows 与 Linux 同输入 native hash 序列一致。

## 10. CI 流程

### 10.1 PR 快速门禁

```text
build C# logic
-> unit/hash field coverage
-> deterministic repeat in fresh processes
-> snapshot round-trip
-> randomized rollback resimulation
-> KFM representative Oracle traces
-> upload first-difference artifact on failure
```

代表脚本至少覆盖：待机、行走、跳跃、普通攻击、搓招、命中、防御、投技、helper、projectile、pause、random。

### 10.2 Nightly corpus

- 固定 Ikemen commit、Go 版本、Oracle patch hash 和构建镜像。
- 遍历角色 corpus 的 manifest，不直接扫描不受控目录。
- 每角色至少运行 neutral、自动生成 command 脚本、受击和双实例对战场景。
- 同时执行 C# native determinism/rollback 和 Ikemen semantic diff。
- 保存通过率、首次差异类别、unsupported 输入/控制器清单。

### 10.3 Golden 管理

- Golden trace 由 Ikemen 固定镜像生成，不手工编辑。
- 更新必须使用显式命令，例如 `regenerate-oracle --ikemen-commit ...`。
- PR 中 Golden 变化必须附带 schema、Ikemen commit、Oracle patch 或角色资源 hash 的变化原因。
- 不允许在普通功能测试中自动接受新输出。

### 10.4 CI artifact

失败时上传：

- 输入脚本和场景 manifest。
- 双方 header。
- 首次差异前后 30 帧 diagnostic trace。
- native/subsystem/semantic hash 序列。
- 比较器结构化报告。
- Ikemen commit、Go 版本、C# commit、OS/arch。

## 11. 实施顺序与验收

### P0-1：先完成 C# 状态契约

- 建立字段分类清单和 hash mutation guard。
- 修正 native hash 初始化与缺项。
- 实现 `MBattleSnapshot` 两阶段复制/重链。
- 完成 rollback resimulation 测试。

验收：任意测试场景在任意帧回滚重演，后续 native hash 序列逐帧完全一致。

### P0-2：替换 Trace v0

- 实现本文件 schema v1、稳定实体 key、subsystem hash 和 diagnostic 模式。
- 比较器按 key 配对、字段级容差、差异分类输出。

验收：故意修改任一 schema 字段都能被对应类别准确检出；不存在记录了却未比较的字段。

### P0-3：Ikemen Oracle

- 在固定提交实现四个最小 hook。
- 用相同 input script 跑 Quick VS/headless。
- 先收口 KFM，再扩大 corpus。

验收：CI 能从零构建 Ikemen、生成 trace、运行 C#、比较并产出可定位 artifact。

### P1：长期稳定性

- Windows/Linux 跨平台 hash。
- 10k 帧长跑和随机 rollback fuzz。
- 非 320 localcoord 数值空间验证。
- corpus nightly 和差异趋势报表。

## 12. 最终判定标准

只有同时满足以下条件，才可以把第 8 项标记为完成：

1. `MBattleEngine` 有真实 snapshot/restore/resim，不再借用旧 ECS 测试作为证明。
2. Native hash 覆盖所有影响未来模拟的状态，并有字段级自动守卫。
3. Trace schema 有版本头、输入、稳定实体身份、完整战斗状态和规范数值空间。
4. Ikemen 固定提交可以在 CI 中由同一输入脚本生成 Golden。
5. 离散字段严格相等，连续字段按字段预算比较，失败可定位到首个字段和首个帧。
6. 回滚重演与直跑 hash 序列一致，表现事件不会重复提交。
7. KFM 代表场景和角色 corpus nightly 都通过或产生显式、可追踪的 unsupported/known-difference 记录。

在这些条件完成前，现有 `MBattleTrace` 应标记为 `prototype/debug trace`，不得作为“任意 MUGEN 角色已完整适配”的证据。
