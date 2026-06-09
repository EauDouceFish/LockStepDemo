# 2026-06-09 parse-only 控制器全实现（Batch A-E）+ 环境搭建

## 环境
- Go 1.23.4 装到 `D:\go-sdk\go`，持久化用户环境变量（GOROOT/GOPATH=D:\go-workspace/GOCACHE/PATH）。
- Ikemen full build 阻塞点：`./src` 需 `GOEXPERIMENT=arenas` + `-mod=mod`，过 Ikemen 自身代码后卡在 CGO 原生依赖（pkg-config / SDL2 头 / ffmpeg-reisen）。结论：full Ikemen 当 oracle 不现实，需另写只导入纯战斗逻辑的 headless trace harness。
- Unity MCP 本会话未注册（Unity 在跑、装了 com.coplaydev.unity-mcp，但会话无 .mcp.json）。

## parse-only 核实
真 parse-only = `ParameterOnlyController` 子类未 override Run（parser 自动登记 `AddParsedOnlyController`）。共 18 个，几乎全是纯表现/调试；原归"表现"的 PalFX/AfterImage/PlaySnd/Explod/EnvShake 等其实已发事件非空。

## Batch A 视觉变换组（逻辑层，dotnet test 697）
- `MChar` 加绘制态：AngleRot/X/Y、AngleDrawScaleX/Y、AngleDraw 标志、Trans(MTransType)、AlphaSrc/Dst、SprPriority/LayerNo、OffsetX/Y、WinQuote，入 Clone/Hash。
- `ResetFrameDrawState()`（char.go:11542，每帧 Prepare 前重置，anglerot/sprpriority/winquote 跨帧保留）。
- 实现 AngleDraw/AngleSet/AngleAdd/AngleMul/Trans/SprPriority/Offset 的 Run（bytecode.go 1:1）。
- 解析器修 trans 文本→类型+默认 alpha→alpha 覆盖（compiler.go:6380）。
- 测试 DrawStateControllerTests 10 例；修 CompatibilityReportTests 改用 ModifyStageVar。
- commit 649f7b4。

## Batch B/C（逻辑层，dotnet test 706）
- VictoryQuote→WinQuote；EnvColor→系统级事件；RemapPal→MChar.RemapPalTable(就地覆盖)+事件；Text/ModifyText/RemoveText→文本事件；Clipboard×3→调试事件；ForceFeedback→震动事件。
- MFrameEvents 加 9 个事件类型；新增 MRemapPalEntry。
- 测试 PresentationControllerTests 9 例。

## Batch D 推迟（诚实标注）
- TagIn/TagOut 需 R-TAG 队伍轮换子系统（1v1 无替补席）；ModifyStageVar 需 R-STAGE 舞台子系统。3 个保持 parse-only，参数已捕获，已写明所需子系统。1v1 不触发。

## Batch E Unity 渲染（⚠️未实测）
- 新增 `Scripts/Game/View/MugenDrawStateApplier.cs`：把绘制态施加到 SpriteRenderer（offset/旋转 AngleDraw/缩放/透明度近似/绘制顺序）。
- 接入 MugenLiveView.Render、MugenVersusView.RenderChar。
- **dotnet test 不覆盖表现层；Unity MCP 未连 → 渲染正确性未验证，留待调试会话**：加法/减法混合需自定义材质、调色板 RemapPal/PalFX 需 palette 重建、旋转+flipX 叠加需编辑器核对。
- commit 8a99d34。

## 验收
- dotnet test 706 全绿（687→697→706，+19 新测试 + Batch A 前 687 基线）。
