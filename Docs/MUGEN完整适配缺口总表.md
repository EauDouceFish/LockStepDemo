# MUGEN 完整适配缺口总表

基线：Ikemen GO `843f7327efda98f6d47edbf38f45420b88cee3c8`。

“完成”必须同时满足：单元测试、12 角色语料回归、C# semantic trace、Ikemen 同输入 trace。只满足“能加载/不崩/确定性自比”不算完整适配。

## P0-A 表达式与命令语言

- [ ] 编译器返回结构化诊断；未知标识符/函数、剩余 token、括号/逗号错误不得静默变 0。
- [ ] `&&`、`||`、`cond` 实现 Ikemen 短路控制流。
- [x] 修复真实 `MChar` 生产路径的 `target(id,index)` 双参数 redirect、独立 `sysvar/sysfvar`、`stateowner` redirect。
- [ ] `trigger1` 必填及 trigger 组连续性与 Ikemen 一致。
- [ ] 删除双表达式/双命令运行管线，只保留战斗实际使用的一套。
- [ ] 按 Ikemen `Command.Step` 重写命令状态机，覆盖 `~N` 释放、`>`、同帧完成、charge、steptime。
- [x] 解析阶段补齐重复方向 tap 展开：`F,F`/`B,B` 按 MUGEN 读入语义转为 release-retap，可驱动 dash 类命令。
- [ ] 读取 `[Defaults]`、pause/hitpause command buffer、SOCD 配置并写入 trace/header hash。
- [ ] 补齐语料实际使用的 trigger；未知 trigger 进入 compatibility report，strict import 失败。

## P0-B 状态机与资源所有权

- [ ] 建立稳定 `PlayerResourceRegistry`；运行实体保存 state/anim/sprite owner ID。
- [ ] 删除通过永久替换 `MChar.AnimTable` 实现 `ChangeAnim2` 的方案。
- [x] 注册资源路径下，`ChangeAnim`、`ChangeAnim2`、`animexist`、`selfanimexist`、custom state 按 owner 解析；`animexist` 查当前动画 owner，`selfanimexist` 查自身 owner。
- [ ] 删除 standalone legacy 分支中通过永久替换 `MChar.AnimTable` 实现 `ChangeAnim2` 的兼容路径。
- [ ] Transition 改为原子值对象，一次性切 owner/state/anim/ctrl 并执行 state init。
- [ ] SelfState 同帧回自身状态表，不继续使用旧外国 state dictionary。
- [ ] persistent、ignorehitpause、continue、state re-entry/reset 逐项对齐。
- [ ] ParameterOnly no-op 改为显式 unsupported/presentation capability，不允许伪装完整支持。

## P0-C localcoord 与角色资源包

- [ ] 建立生产级 `MCharacterPackageManifest/Loader`，统一 DEF/CNS/CMD/AIR/SFF/SND/ACT IO 与诊断。
- [ ] localcoord 在运行时结合 state owner/character owner 换算位置、速度、常量、碰撞框、动画偏移。
- [ ] SFFv2 PNG 10/11/12 解码，linked sprite/palette、alpha、色深完整支持。
- [ ] SFFv1 palette link/same-palette/外部 ACT/remap 完整支持。
- [ ] AIR 支持 Copy Action、完整插值/帧参数/重复 action 规则。
- [ ] ACT/SND 进入生产角色包；Unity 实现音频 channel、pan、loop、stop-on-gethit/change-state 消费端。
- [ ] 文件编码、大小写、语言覆盖、搜索路径和共享 data 目录兼容。

## P0-D HitDef、受击与实体事务

- [ ] `MHitDefSpec` 保存表达式，控制器运行时求值并执行 Ikemen reset/finalize。
- [ ] 补齐模拟相关 HitDef 字段、state/attack attr mask、persist 生命周期。
- [ ] guard 输入、guard distance、guard state 与 hit result 闭环。
- [ ] HitOverride/ReversalDef 接入接触结算；AttackDist 接入 guard distance。
- [ ] 统一 collision query，支持 localcoord、scale、深度和 HitDef clsn 规则。
- [ ] 每 defender 帧内 hit accumulator，正确累计 GHV、kill clamp、KO 路由和 life commit。
- [ ] GetHitVar 完整字段与 reset policy。
- [ ] target 按 `ghv.hitid` 过滤，不把 entity ID 当 target hit ID。
- [ ] juggle/chain/nochain/targetedBy/helper inheritjuggle 完整实现。
- [ ] hitpause/pause/superpause 使用统一 entity execution mask，物理/动画/碰撞一致冻结。
- [ ] helper/projectile/explod 查询按 owner/root 隔离；生命周期、动画碰撞、多 hit、remove 条件完整。
- [ ] round context 每帧发布；下一回合集中过滤并重置实体、target、HitDef、pause。

## P0-E Trace、Hash 与回滚

- [ ] Trace schema `mugen-oracle-trace/v1`：版本头、资源 hash、输入、全局态、稳定实体 key、状态/动画 owner、实体、事件。
- [ ] comparer 比较所有已记录字段；实体按稳定 key，不按列表下标；字段级容差。
- [x] native hash 使用规范 FNV 初值（`Hash64.Create()`），并有回归测试防止退回零初值。
- [ ] native hash 覆盖所有影响未来模拟的状态，加入 frame/resource manifest，并建立字段级 mutation guard。
- [x] native hash 已加入 `FrameNo`、projectile `HitDef/Owner/Clsn1` 与 helper/projectile spawn queue 内容。
- [x] `MBattleEngineSnapshot` 整体克隆对象图并两阶段重链，覆盖玩家、helper、projectile、explod、spawn queues 与核心引用边。
- [ ] snapshot 恢复后重建派生状态，并建立字段级 mutation guard。
- [ ] 输入历史、快照环、重演、confirmed frame、表现事件提交/去重。
- [ ] Ikemen 固定提交插桩：固定输入注入，post-combat-tick 输出 JSONL semantic trace。
- [ ] C# 与 Ikemen scenario runner、差异分类和 CI golden 流程。

## P1 表现与 Ikemen 扩展

- [ ] PalFX/AllPalFX/BGPalFX、AfterImage、Trans、Angle、EnvShake、SprPriority、Offset。
- [ ] Text/Tag/Stage 修改类控制器按“纯 MUGEN”与“Ikemen 扩展”分能力等级。
- [ ] 多人/team/playerindex/playerid/helperindex redirect 与 tag/team mode。
- [ ] 展馆展示 helper/projectile/explod、碰撞框、声音和失败 trace。

## 全招式验收

- [x] 建立全角色 `[Command]` 定义激活矩阵：逐角色逐 command 生成输入序列并验证 command active；`time=1` AI 伪随机长命令显式分类为不可人工激活项。
- [ ] 从 `[Command]` 与 `Statedef -1` 建立 command-to-transition 图，不把 command 名直接等同招式。
- [ ] 为每条可达招式生成输入序列和前置条件：站立、蹲、空中、能量、距离、受击/防御。
- [ ] 每个招式从全新 session 或可靠 snapshot 起跑。
- [ ] 验收：command active、目标 state 可达、动画 owner 正确、退出路径正常、无 unsupported diagnostic。
- [ ] 同一场景在 Ikemen 与 C# 输出 trace 并收口。

## Unity 展馆验收

- [ ] 一个数据驱动场景，每角色一页，不复制角色专用场景。
- [ ] 页面显示资源/兼容报告/命令图/招式结果/trace 差异。
- [ ] 自动模式使用固定逻辑 tick；交互模式独立。
- [ ] 角色与木桩、全部实体、碰撞框和当前 state/anim/owner 可视化。
- [ ] 12 个现有角色全部招式执行完毕；新增任意 DEF 可自动出现新页面并运行同一验证流程。

## 完成定义

只有所有 P0 勾选、语料 strict import 无未豁免诊断、全招式矩阵通过、Ikemen trace 无未解释差异、Unity 展馆验收通过，才可声明“导入任意目标版本 MUGEN 角色可完整适配”。
