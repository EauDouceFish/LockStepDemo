# Unity 角色展馆与展示层审计

审计日期：2026-06-06  
审计范围：`Assets/Scripts/Game/View/`、`Assets/Scenes/MugenLive_kfm.unity`、`Assets/Scenes/MugenVersus_kfm.unity`，以及展示层直接依赖的 MUGEN 逻辑输出契约。  
约束：本次只做静态审计和设计，不修改场景、不运行 Unity、不执行测试。

## 1. 结论

现有代码已经证明三件事：SFF/AIR 可以在 Unity 显示、`MBattleEngine` 可以由 MonoBehaviour 驱动、KFM 可以进入双人对战。但它仍是验证原型，不是可扩展的“角色展馆”。

逻辑层与 Unity 的物理依赖方向是正确的：`Lockstep.Logic.asmdef` 设置了 `noEngineReferences=true`，战斗逻辑不引用 Unity；声音也通过 `MFrameEvents` 输出，而非在逻辑层操作音频设备。主要问题出在反方向：当前 View 直接承担文件系统扫描、DEF 猜测、角色装配、战斗时钟、输入、测试判定、资源缓存和 IMGUI，且直接读取、部分修改 `MChar`。继续在这些 MonoBehaviour 上加按钮和列表会形成补丁式架构。

建议新建单一展馆场景，但不要新建一个更大的 `MugenGalleryShowcase`。应拆为“角色包适配器、展馆会话、招式测试器、只读展示模型、Unity Presenter”五个边界。现有 Live/Versus 最终只保留为展馆会话的薄预设入口。

## 2. 现状审计

### 2.1 已有展示入口

| 入口 | 当前能力 | 判断 |
|---|---|---|
| `MugenCharacterView` | 指定一个 AIR 动画直接播放 | 资源查看器，不验证状态机 |
| `MugenAnimShowcase` | 遍历全部 AIR 动画号 | 动画资产检查器，不代表招式能按出 |
| `MugenGalleryShowcase` | 扫描目录、每页并排展示若干角色的站立动画 | 视觉画廊雏形，未加载 CNS/CMD，不能验证招式 |
| `MugenLiveView` | 单角色、键盘输入、真实 `MBattleEngine.Tick` | 状态机调试原型，角色路径硬编码且职责过多 |
| `MugenVersusView` | 双 KFM、回合系统、P1 键盘、P2 木桩 | 双人战斗原型，仍只适配单角色资源源 |

`MugenGallery.unity` 绑定的是 `MugenGalleryShowcase`，因此现有“画廊”实际展示的是 AIR 动画，而不是角色命令、负状态和状态切换结果。

### 2.2 严重问题

#### P0：现有 View 不能正确加载任意角色包

`MugenLiveView` 和 `MugenVersusView` 固定读取 `kfm.cns`、`kfm.cmd`，并用 `Terrarian/common1.cns` 兜底。它们没有按主 DEF 的 `cns/cmd/st/stcommon/anim/sprite/sound/pal` 清单装配角色。逻辑层已有纯解析器 `MDefParser`，测试侧也已有更完整的 `MugenCharacterPackageTestLoader`，但生产展示层没有同等的角色包 adapter。

后果：文件名不是 `kfm.*`、存在多个 `stN`、大小写不一致、使用自带 `stcommon` 或非默认资源路径的角色会被错误加载或直接失败。这个问题必须先解决，UI 才有资格声称“任意角色一页”。

#### P0：当前 trace 无法证明“所有招式能按出”

`MBattleTraceRecorder` 目前记录玩家/Helper 的状态、动画、位置、速度、生命和能量，但比较器只比较玩家的部分字段。现状缺少：

- 本帧输入和匹配成功的 command 名称；
- 状态切换来源：负状态号、控制器序号、控制器类型、目标状态；
- `PrevStateNo`、`AnimElem`、`Ctrl`、`MoveType`、`StateType`、`Physics` 的完整比较；
- Helper 的逐字段比较；
- Projectile/Explod 的 ID、owner、动画、位置、生命周期和命中结果；
- Hit/guard/reversal、实体生成销毁、声音/特效等事件；
- 导入兼容性诊断和资源缺失；
- trace 头部中的角色包指纹、引擎版本、随机种子、测试场景和输入脚本版本。

仅观察 `StateNo` 是否变化会产生假阳性：基础走路、受击、回合切换也会改变状态。仅观察动画是否变化同样不成立，因为 AIR 动画可被直接播放，但 CMD 可能从未匹配。

#### P0：动画资源所有权会造成展示假失败

Live/Versus 使用单个 `_data.Anims` 和单个 `_source` 渲染所有角色。自定义状态、`ChangeAnim2`、Helper 或后续按 Ikemen 语义引入的 `animPN/spritePN` 可能让“状态数据所有者、动画所有者、精灵所有者”不同。若 View 继续按当前角色自身 `_data/_source` 查图，逻辑已正确切换但 Unity 会显示旧图或空图，展馆会错误报告为招式失败。

展示层必须消费逻辑输出的稳定资源所有者 ID，再通过对局级资源注册表定位动画和精灵源，不能自行猜测。

### 2.3 高优先问题

#### P1：MonoBehaviour 职责聚合且重复

Live/Versus 各自重复角色路径解析、文本读取、`MCharLoader` 装配、AIR 字典、SFF 懒构建、键盘映射、60 Hz 累加器、角色渲染和 IMGUI。两者已经产生输入键位不一致：Live 把拳映射到 `A/S/D`、脚映射到 `Z/X/C`，Versus 只映射 `Z/X/C` 到 `A/B/C`。

这不是抽几个私有方法能解决的问题。文件 IO、战斗会话、输入源、测试编排和 Unity 渲染属于不同变化原因，应拆成独立对象。

#### P1：实时 `Update` 不适合作为自动验收时钟

当前逻辑按 `Time.deltaTime * TicksPerSecond` 累加，并设置每帧最多补 8 tick。卡顿超过上限时会丢模拟进度；切页、加载大 SFF、截图或编辑器失焦都可能改变测试节奏。

展馆必须有两种时钟：

- `InteractiveClock`：只供人工操作，可继续使用实时累加；
- `DeterministicTestClock`：每次显式推进一个逻辑帧，完成一次招式测试后才允许 Presenter 刷新。

自动测试不得依赖 Unity 帧率、`Update` 调度或按键轮询。

#### P1：展示层直接观察和修改运行对象

Versus 在 View 中直接写 `MChar.Pos/Facing`，Live/Versus 在渲染和 UI 中直接遍历 `engine.Chars`。初始化位置属于会话配置，读取运行对象也应通过只读快照。否则 UI 很容易在后续增加“重置、切状态、补能量”按钮时直接修改模拟态，绕开哈希、trace 和回滚语义。

#### P1：展示覆盖面与逻辑实体不一致

Live 只渲染 `Chars[0]`，Versus 只渲染两个玩家。Helper、Projectile、Explod 和声音帧事件均未消费。对于依赖召唤物、飞行道具或 Explod 表达攻击主体的招式，状态机可能完全正确，但展馆画面会像“没出招”。

#### P1：现有命令模型不足以建立可靠的招式目录

`MCommandDef` 只描述命令名和输入步骤；状态编译后没有保存原始 CNS 文件、段名、控制器行号、原始 trigger 文本，也没有 command 到 ChangeState/Helper/Projectile 的显式索引。一个 command 名可能重复定义，也可能只是 hold、AI 辅助或内部组合键，不应全部当作“招式”。

展馆需要导入期 source map。不能通过“状态号大于 200”“动画号变化”之类经验规则猜招式。

### 2.4 中优先问题

- `MugenGalleryShowcase` 以“目录内存在任意 SFF+AIR”识别角色，未验证主 DEF，可能把故事板或残缺资源当角色。
- `MugenDef` 与逻辑层 `MDefParser` 重复解析 DEF，规则已经分叉；前者还会用目录内第一个匹配文件兜底。
- 固定 `PixelsPerUnit` 和按 Sprite 高度统一缩放适合资产预览，但不适合状态机验收；它会掩盖 `localcoord`、绘制偏移和碰撞坐标问题。
- IMGUI 适合临时调试，不适合虚拟化长列表、筛选、失败展开、时间轴和可复制报告。展馆应使用 UI Toolkit。
- 当前场景是 KFM 专用序列化配置，角色切换必须改 Inspector 或复制场景，不符合“每角色一页”的数据驱动目标。

## 3. 正确的层次边界

```text
MugenSource 文件系统
        |
        v
FileSystemMugenCharacterCatalog          Unity/Application adapter
MugenCharacterPackageAdapter             只负责路径、编码、字节、资源缓存
        |
        v
MDefParser + MCharLoader + MBattleEngine Lockstep.Logic
        |
        v
MExhibitSnapshot + MTraceStream          只读跨层契约
        |
        v
MugenMuseumPresenter / UI Toolkit        Unity presentation
```

### 3.1 逻辑层负责

- DEF/CNS/CMD/AIR 的纯数据解析和运行语义；
- 命令匹配、状态机、物理、命中和实体生命周期；
- 确定性逐帧 trace 与语义事件；
- 招式测试所需的输入序列模型和结果判定模型；
- 不依赖 Unity 的兼容性报告、状态覆盖率和差分结果。

### 3.2 Unity/Application adapter 负责

- 发现主 DEF，按 DEF 相对路径和大小写规则读取所有文件；
- 文本编码识别、SFF/SND/ACT 字节读取和 Unity 资源对象缓存；
- 把 `MCharacterPackageInput` 交给逻辑装配器；
- 把逻辑的声音、精灵、Explod 等语义事件转换为 Unity 表现；
- 不解释 trigger，不决定招式是否成功，不直接写 `MChar`。

### 3.3 Presenter 负责

- 将只读 `MExhibitSnapshot` 映射到角色 Sprite、列表和详情面板；
- 发出“选择角色、运行招式、暂停、重试、导出报告”等意图；
- 不持有 `MBattleEngine`，不执行文件 IO，不构造输入序列。

## 4. 建议模块

| 模块 | 责任 | 禁止事项 |
|---|---|---|
| `IMugenCharacterCatalog` | 枚举合法主 DEF，输出稳定 character ID 和概要 | 不加载战斗引擎 |
| `MugenCharacterPackageAdapter` | 完整读取 DEF 引用资源，构建包输入和表现资源句柄 | 不解析战斗语义 |
| `MugenExhibitSession` | 创建/重置引擎、配置双方位置/能量/距离、逐帧推进 | 不引用 Unity |
| `MMoveCatalogBuilder` | 从 source map 建立 command、入口控制器和候选状态的关系 | 不按状态号猜测 |
| `MCommandInputSynthesizer` | 把 `MCommandDef` 转成面向角色朝向的确定性逐帧输入 | 不调用 `UnityEngine.Input` |
| `MMoveTestRunner` | 在多种前置条件下运行单招、收集 trace、判定结果 | 不渲染 |
| `MExhibitSnapshotBuilder` | 将运行态和测试结果投影成不可变 DTO | 不暴露 `MChar` 引用 |
| `MugenBattlePresenter` | 玩家、Helper、Projectile、Explod 的 Unity 映射 | 不改变模拟态 |
| `MugenMuseumPresenter` | 页面导航、筛选、测试控制、失败详情 | 不读取文件 |

不建议为每个角色实例化一整套常驻引擎。页面切换时保留角色包元数据和轻量测试摘要，当前选中角色才创建战斗会话和纹理缓存；离页后释放 Sprite/AudioClip，避免大角色包长期占内存。

## 5. “每角色一页”场景设计

建议最终只保留一个 `MugenCharacterMuseum.unity`：

```text
MugenCharacterMuseum
├── Main Camera
├── Directional Light
├── MuseumRoot
│   ├── SessionHost               MugenExhibitSessionHost
│   ├── PresentationRoot          玩家/Helper/Projectile/Explod 视图池
│   ├── StageRoot                 地面、原点、边界与碰撞框调试层
│   └── AudioRoot                 SND 事件消费者
└── UI
    └── UIDocument
        ├── CharacterSidebar      角色列表、搜索、兼容等级
        ├── CharacterHeader       名称、DEF、localcoord、资源状态
        ├── ViewportToolbar       人工/自动、暂停、逐帧、重置、碰撞框
        ├── MoveTable             招式、命令、状态入口、测试结果
        ├── TraceTimeline         输入/command/状态/动画/事件时间轴
        └── FailureDrawer         首差异、前置条件、trace、导入诊断
```

角色侧栏是页面导航，不应真的创建 N 个 Unity 页面对象。选中角色后，用同一套 Presenter 绑定新的只读页面模型。

### 5.1 角色页内容

- 角色信息：显示名、主 DEF、MUGEN 版本、localcoord、SFF/SND/调色板状态；
- 兼容性：未知控制器、parsed-only 控制器、未知 trigger、缺失文件、降级项；
- 招式表：招式标签、command 文本、输入图示、候选入口状态、前置条件、测试状态；
- 实时视图：P1、测试木桩 P2、所有子实体、碰撞框和逻辑坐标；
- 验证摘要：通过、条件通过、不可达、逻辑失败、表现失败、Oracle 差异；
- 可复现信息：seed、起始配置、逐帧输入、首次差异帧、trace 文件路径/ID。

## 6. 招式目录与自动按键

### 6.1 不能把全部 `[Command]` 直接等同于全部招式

命令表通常包含方向保持、单键别名、AI 辅助和多个同名定义。可靠目录应从状态控制流反向建立：

1. 保存 CNS/CMD source map：文件、Statedef、控制器索引、原始 trigger 和参数位置；
2. 找出引用 `command="..."` 的控制器；
3. 识别会产生可观察行为的入口：ChangeState、SelfState、Helper、Projectile 等；
4. 将一个招式表示为“command + 入口控制器 + 前置条件集合”，而不是仅用 command 名；
5. 同名命令保留多个输入变体，由合成器分别尝试并记录最终采用者。

当前 bytecode 只适合执行，不适合反编译 UI。source map 应在编译时旁挂调试元数据，不能让 UI 解析 bytecode。

### 6.2 输入合成规则

`MCommandInputSynthesizer` 应直接消费 `MCommandDef.Steps`，处理：

- B/F 随角色 facing 映射到 Left/Right；
- `~` 释放、蓄力时间、`/` 保持、`$` 四向、`>` 紧邻、`+` 同帧组合；
- `time` 总窗口和 `buffer.time`；
- 输入前后的 neutral 帧；
- 同一脚本镜像后结果一致。

每次招式测试必须创建全新会话或恢复可靠快照，不能在上一个招式结束态继续测试。

### 6.3 前置条件矩阵

“按不出”可能只是前置条件不满足。测试器至少覆盖：

- 站立、下蹲、空中；
- 近距、中距、远距；
- 0、半格、满格能量；
- P2 正常、防御、受击可连段；
- 左右朝向；
- 必要时的 Helper/模式变量前置。

无法从 source map 或运行覆盖确认前置条件的项目标为“不可达/待人工条件”，不能直接记为引擎失败。

## 7. 结果判定

一次招式通过至少要形成以下证据链：

1. **InputAccepted**：逐帧输入按计划送入；
2. **CommandMatched**：目标 command 在预期窗口激活；
3. **EntryTriggered**：目标负状态控制器实际执行；
4. **StateEntered**：进入候选状态或产生目标实体；
5. **StateProgressed**：状态时间、动画或实体生命周期正常推进，无重入上限/无效状态；
6. **OutcomeObserved**：命中、防御、Projectile、Helper 或预期结束状态发生；
7. **PresentationResolved**：所有逻辑动画/精灵/声音引用均被 Unity adapter 解析；
8. **Deterministic**：相同 seed 和输入重跑 trace/hash 一致；
9. **OracleMatched**：存在 Ikemen trace 时，关键语义字段在约定容差内一致。

建议状态枚举：

| 状态 | 含义 |
|---|---|
| `Passed` | 完整证据链通过 |
| `PassedWithDegradation` | 状态机正确，但缺少非战斗表现 |
| `UnreachablePrerequisite` | 已知条件未满足，不能判引擎失败 |
| `ImportFailure` | DEF/资源/语法装配失败 |
| `CommandFailure` | 输入送达但 command 未匹配 |
| `TransitionFailure` | command 匹配但入口控制器/状态未触发 |
| `RuntimeFailure` | 进入后卡死、异常状态、实体错误或非确定 |
| `PresentationFailure` | 逻辑通过但 Unity 资源无法解析/显示/播放 |
| `OracleMismatch` | 本引擎可运行，但与 Ikemen 首次语义差异明确 |

## 8. Trace 收口要求

当前 `MBattleTrace` 可作为骨架，但不能直接供展馆验收。建议扩展为版本化事件流：

```text
MTraceHeader
  schemaVersion, engineRevision, characterFingerprint,
  seed, localcoord, scenarioId, inputScriptHash, oracleRevision

MTraceFrame
  frame, input[], activeCommands[], entities[], transitions[],
  hits[], lifecycleEvents[], presentationEvents[], hash

MEntityTrace
  stableId, kind, rootId, parentId, stateOwnerId,
  stateNo, prevStateNo, stateTime, stateType, moveType, physics, ctrl,
  animOwnerId, spriteOwnerId, animNo, animElem, animElemTime,
  pos, vel, facing, life, power, hit/contact flags

MStateTransitionTrace
  entityId, fromState, toState, sourceState,
  controllerIndex, controllerType, sourceFile, sourceLine, reason
```

比较器必须：

- 按 stable ID 比较实体，而不是按列表序号；
- 比较 Helper、Projectile、Explod 的详细字段；
- 比较 `AnimElem/Power/Ctrl` 等 Recorder 已采集但当前遗漏的字段；
- 支持“严格字段、定点容差字段、仅诊断字段”三类策略；
- 首差异之外保留有限窗口，例如差异前后各 10 帧；
- 把导入降级与运行差异分开，避免 UI 只显示一个笼统红叉。

展馆只消费 trace/snapshot，不应自行推断状态切换原因。

## 9. 对现有代码的处理建议

### 保留并复用

- `MDefParser`：作为唯一 DEF 语义解析器；
- `MCharLoader`：继续保持纯文本/纯数据输入；
- `MugenSpriteLoader`：放在 Unity adapter 内，增加资源所有者 ID 缓存；
- `MBattleEngine`、`MRoundSystem`：由 `MugenExhibitSession` 驱动；
- `MFrameEvents`：扩展为通用表现事件出口；
- `MBattleTrace`：按第 8 节扩展，不另造一套 UI 日志。

### 降级为薄入口

- `MugenLiveView`：保留“单角色人工调试”预设，只配置 session 和 presenter；
- `MugenVersusView`：保留“双人木桩”预设，不再自行加载和渲染；
- `MugenGalleryShowcase`：其角色分页概念并入展馆，AIR-only 模式保留为“资源预览”标签；
- `MugenAnimShowcase`：作为动画资产子页，不作为招式通过证据。

### 不应继续扩展

- 不在 MonoBehaviour 中继续追加角色文件读取分支；
- 不复制第三份 DEF 主文件选择逻辑；
- 不让 UI 直接设置 StateNo、Pos、Power 或 Ctrl；
- 不用动画号变化代替 command/状态入口证据；
- 不把所有角色和全部纹理一次性常驻场景。

## 10. 实施顺序

1. 建立生产级 `MugenCharacterPackageAdapter`，让展馆和测试共用相同主 DEF/路径规则；
2. 为解析器增加 source map 和 command 到入口控制器索引；
3. 扩展版本化 trace，先补齐 command、transition、实体和事件；
4. 实现无 Unity 依赖的 `MCommandInputSynthesizer/MMoveTestRunner`；
5. 实现 `MugenExhibitSession` 和不可变 snapshot；
6. 建立统一角色/子实体 Presenter 与资源所有者注册表；
7. 创建 UI Toolkit 展馆场景和角色页；
8. 接入 Ikemen Oracle trace，展示首次差异与上下文窗口；
9. 将 Live/Versus/Gallery 改成薄预设并删除重复装配代码。

在 1-5 完成前先搭场景，只会把当前原型问题包装成更复杂的 UI。

## 11. 验收标准

- 将一个文件名、大小写、`stN` 数量均不同的新角色目录放入 `MugenSource`，无需改场景或 C# 即出现在角色列表；
- 每个角色页显示完整导入诊断，不静默忽略不支持项；
- 每个识别出的招式都有确定性输入脚本、前置条件、运行 trace 和明确结果分类；
- UI 能区分“command 没匹配”“状态没切”“逻辑通过但精灵缺失”“与 Ikemen 不一致”；
- Helper/Projectile/Explod 招式在逻辑和画面中都可观察；
- 页面切换不会改变测试 hash，低帧率不会改变自动测试结果；
- Presenter 不引用或修改 `MChar`，逻辑层不引用 Unity；
- Live、Versus、自动测试使用同一个角色包 adapter 和同一个战斗会话核心；
- Oracle 存在时显示首差异字段、帧号、前后输入与状态上下文，而不是只显示最终 hash 不同。

## 12. 文件证据

- `Assets/Scripts/Game/View/MugenLiveView.cs`：文件 IO、角色装配、输入、时钟、渲染和 IMGUI 聚合在一个类；
- `Assets/Scripts/Game/View/MugenVersusView.cs`：重复上述流程，并在 View 内直接设置角色位置/朝向；
- `Assets/Scripts/Game/View/MugenGalleryShowcase.cs`：仅扫描 SFF/AIR 并播放站立动画；
- `Assets/Scripts/Game/View/MugenAnimShowcase.cs`：只遍历 AIR 动画，不经过 CMD/CNS 状态机；
- `Assets/Scripts/Game/View/MugenDef.cs`：与逻辑层 DEF 解析重复；
- `Assets/Logic/Mugen/Parse/MDefParser.cs`：已有可作为唯一语义入口的纯解析器；
- `Assets/Logic/Mugen/Battle/MBattleTrace.cs`：已有 trace 骨架，但字段和比较范围不足；
- `Assets/Logic/Mugen/Char/MFrameEvents.cs`：逻辑到表现层的正确事件边界雏形；
- `Assets/Logic/Lockstep.Logic.asmdef`：逻辑程序集无 Unity 引用，边界方向正确；
- `Tests/Lockstep.Logic.Tests/Mugen/MugenCharacterPackageTestLoader.cs`：测试侧已有比生产 View 更完整的角色包装配逻辑，应抽取规则而非复制。
