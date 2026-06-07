# Ikemen 对照架构审计与 7-9 进度

参考基线：Ikemen GO `843f732`。本项目是确定性定点数 C# 实现，不复制 Go 文件布局，但必须保留其语义边界。

## 动画资源所有权

Ikemen 不是简单的“两张动画表”。运行角色持有 `playerNo`、`animPN`、`spritePN`，状态字节码持有状态所有者 `playerNo`；`ChangeAnim2` 默认从状态所有者取动画，再分别解析动画与精灵资源所有者。核心是稳定 ID 和资源注册表，不是两个可变字典引用。

当前 `MChar.StateOwner + AnimTable` 能覆盖基础自定义状态，但不能完整表达 `animPN/spritePN/readplayerid`。不应继续在 `MChar` 上追加 `OwnAnimTable/StateAnimTable` 一类补丁字段。后续应引入对局级玩家资源注册表，并让运行实体保存资源所有者 ID。

## 架构判断

### 保留

- `MCharData` 作为加载后只读配置，`MChar` 作为回滚运行态，方向正确。
- CNS 编译为表达式字节码、控制器对象再运行，符合 C# 工程习惯，无需照抄 Ikemen 的 Go 大 switch。
- `MEntityWorld` 统一管理 helper/projectile/explod 的生命周期，比控制器静态旁路表可靠。

### 必须收敛

- `MChar` 已承担状态、命中、输入、暂停、实体关系、表现参数等过多职责。不要按字段数量机械拆类；先按“资源上下文、战斗运行态、帧输出”建立边界。
- 未实现控制器不能静默变成 `NullController` 或永久 parameter-only。导入阶段应产出兼容性报告，区分“解析失败、已解析未执行、完整支持”。
- 角色 DEF、CNS、AIR、SFF、SND、ACT 的路径装配曾散落在测试和 View。应统一由角色包适配器负责 IO，逻辑层只接收已读取的文本/字节和纯定义对象。
- localcoord 不能用导入时统一乘法解决。Ikemen 在运行时结合角色 `localscl` 与状态所有者 localcoord 换算；自定义状态尤其依赖该上下文。

## 7. 角色资源链

已完成：

- DEF `[Info]`/`[Files]` 解析：localcoord、pal.defaults、sound、pal1..palN。
- ACT 768 字节反序调色板导入，与 Ikemen `readActPalette` 一致。
- Elecbyte SND 目录和 WAV 子文件索引，与 Ikemen `LoadSndFiltered` 结构一致。
- PlaySnd/StopSnd/SndPan 输出确定性帧事件，逻辑层不直接操作音频设备。
- 每个角色包携带兼容性报告，未知控制器与“参数已解析但 Run 仍为空”的控制器分开统计。

未伪装为完成：非 320 角色的运行时坐标换算、RemapPal/PalFX 的完整展示链、Unity 音频事件消费者。

## 8. Oracle

已完成 C# 侧帧记录与比较器：状态、动画、位置、速度、生命、能量、实体数量和整局哈希；离散字段严格比较，定点字段允许显式 raw tolerance。

Ikemen 侧真实 trace 尚未生成。当前机器没有 Go 工具链，因此不能把 C# 自比测试称为差分 Oracle 完成。下一步是在锁定提交 `843f732` 上增加同字段 headless trace，再把双方 JSON 接入比较器。

## 9. 全角色冒烟

`MugenSource` 中所有可识别主 DEF 的角色均进行完整负状态加载、双角色 240 帧方向/六键/Start 输入和双实例逐帧哈希一致性检查。该测试证明“不会崩且确定”，不证明每个招式语义已与 Ikemen 一致；后者必须由第 8 项差分 trace 收口。

当前 12 个角色没有未知控制器类型，但存在大量 parsed-only 控制器。多数是表现项：`PalFX`、`AfterImage`、`EnvShake`、`Trans`、`SprPriority`。真正影响战斗语义、应优先补齐的是 `ReversalDef` 与 `AttackDist`；它们分别出现在 Animus/KFM/Noroko 与 Animus/Gustavo/Noroko。未完成这两项前，不能宣称“任意角色动作状态机完整适配”。
