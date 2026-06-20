# worker20 验证链路审计 2026-06-20

范围：只审计 view/debug/regression harness/logging 与最终验证链路，不修改网络协议或核心战斗推进。

## 本轮必须证明的行为

| 行为 | 脚本级证明 | AutoTest/日志证明 |
| --- | --- | --- |
| 动作后摇不过快 | `AnimationRecoveryTests`、`KfmCommandBufferSemanticsTests`、`BasicControllersTests` 的 recovery 断言 | `MBattleRunLog` 记录 `stateNo/animNo/animElemNo/animTime/time/ctrl/keyCtrl/activeCommands`，可追 KFM 招式进入、恢复和是否连发 |
| KFM/common `100/105/106` | `MCharLoader` fallback + KFM/common 交集测试；主线过滤建议含 `AnimationRecoveryTests` 与 command tests | runlog 汇总 `RunLogCommon100105106Seen`，若 AutoTest 随机输入触发，可作为额外覆盖；不强制作为 AutoTest 成败条件 |
| 穿人推挤 | `MPhysicsTests`、StateCtrl width/playerpush 相关测试 | runlog 记录 `posXRaw/posYRaw/facingRaw/widthPlayer*Raw/playerPushEnabled/pushPriority/pushAffectTeam`，用于事后定位穿人/推挤异常 |
| hitpause/pause 冻结 | `PauseFreezeTests`、`MoveContactTimingTests`、状态机 ignorehitpause 测试 | runlog 记录 `hitstop/pauseBool/pauseMovetime/superMovetime/posFreeze/acttmp`，analyzer 输出 freeze sample 计数，不把随机 AutoTest 是否命中 pause 作为硬门槛 |
| 弱网不 hash mismatch | `MugenLockstepSessionTests`、`MugenMatchServerCoreTests` | `analyze-player-autotest.ps1` 检查 timeout/client failure/hash mismatch；弱网 run 还要求看到 `weak_network_toggle`、netdiag 弱网延迟和远端状态 |

## 链路修复

- `MugenTeamBattleView` 在 AutoTest 模式下把 `MugenRunLogs` 和 `MugenNetLogs` 保存到当前 match 日志目录，避免证据落到 LocalLow 后 analyzer 找不到。
- `MBattleRunLog` 扩展每实体调试字段：动画元素、pause/hitstop、pos freeze、推挤宽度和 playerpush 状态。
- `analyze-player-autotest.ps1` 新增 runlog/netdiag 解析，失败条件包括缺两端 runlog、runlog 未完成、runlog hash/checksum 分叉、弱网 toggle 后未观测到弱网延迟/远端状态。

## 建议主线命令

```powershell
dotnet test Tests\Lockstep.Logic.Tests\Lockstep.Logic.Tests.csproj --no-restore --filter "FullyQualifiedName~BattleRunLogTests|FullyQualifiedName~AnimationRecoveryTests|FullyQualifiedName~PauseFreezeTests|FullyQualifiedName~MPhysicsTests|FullyQualifiedName~KfmCommandBufferSemanticsTests|FullyQualifiedName~MugenLockstepSessionTests|FullyQualifiedName~MugenMatchServerCoreTests"
```

```powershell
Tools\autotest\run-player-autotest.ps1 -Matches 1 -DurationSeconds 60 -RoundSeconds 8 -Inputs 300 -TimeoutSeconds 140 -RunId worker20-validation-normal
Tools\autotest\analyze-player-autotest.ps1 -RunPath Logs\autotest\worker20-validation-normal
```

```powershell
$frames = (5..24) -join ','
Tools\autotest\run-player-autotest.ps1 -Matches 1 -DurationSeconds 70 -RoundSeconds 8 -Inputs 300 -TimeoutSeconds 180 -WeakToggleFrames $frames -WeakToggleSide A -RunId worker20-validation-weak
Tools\autotest\analyze-player-autotest.ps1 -RunPath Logs\autotest\worker20-validation-weak
```

## 剩余风险

- AutoTest 随机输入不保证每次触发 KFM `100/105/106`、命中 pause 或推挤极限场景；这些仍以脚本级测试作为硬证明，AutoTest 负责端到端稳定性和可诊断证据。
- runlog 是诊断证据，不是回放 oracle；真正确定性仍由每帧 hash、server hash report 和 `MBattleRunLogVerifier` 覆盖。
