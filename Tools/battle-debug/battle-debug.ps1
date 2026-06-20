<#
.SYNOPSIS
  战斗调试修改器 CLI（PowerShell）。把命令写进 Unity 端 MugenBattleDebugBridge 轮询的命令文件，
  并能读回状态 JSON。配合场景里挂了 MugenVersusView(EnableDebugBridge=true) 的运行中 Unity 使用。

.EXAMPLE
  ./battle-debug.ps1 skipintro                 # 跳过出场直接开打
  ./battle-debug.ps1 sethpp 1 10               # P2 残血到 10%
  ./battle-debug.ps1 win 0                      # 强制 P1 胜
  ./battle-debug.ps1 -State                     # 打印当前对局状态 JSON
  ./battle-debug.ps1 -Watch                     # 每 0.5s 刷新状态
  ./battle-debug.ps1 -Help                      # 引擎内命令清单
#>
param(
  [switch] $State,
  [switch] $Watch,
  [switch] $Help,
  [Parameter(Position = 0, ValueFromRemainingArguments = $true)] [string[]] $Command
)

# 调试目录：默认工程 Temp/battle_debug，可用环境变量 BATTLE_DEBUG_DIR 覆盖（避免与位置参数冲突）。
$ErrorActionPreference = 'Stop'
if ($env:BATTLE_DEBUG_DIR) {
  $Dir = $env:BATTLE_DEBUG_DIR
} else {
  $Dir = Join-Path $PSScriptRoot '..\..\Temp\battle_debug'
}
$cmdPath   = Join-Path $Dir 'cmd.txt'
$statePath = Join-Path $Dir 'state.json'

function Show-State {
  if (Test-Path $statePath) {
    Get-Content $statePath -Raw
  } else {
    Write-Warning "状态文件未生成（Unity 是否在运行且 EnableDebugBridge=true？）: $statePath"
  }
}

function Send-Cmd([string] $line) {
  if (-not (Test-Path $Dir)) {
    Write-Warning "调试目录不存在，Unity 端尚未创建桥: $Dir"
    New-Item -ItemType Directory -Force -Path $Dir | Out-Null
  }
  # 追加一行命令（Unity 端读后清空；UTF8 无 BOM）
  Add-Content -Path $cmdPath -Value $line -Encoding utf8
  Write-Host "→ 已发送: $line"
}

if ($Help)  { Send-Cmd 'help'; return }
if ($Watch) {
  Write-Host "监视状态（Ctrl+C 退出）: $statePath"
  while ($true) { Clear-Host; Show-State; Start-Sleep -Milliseconds 500 }
  return
}
if ($State) { Show-State; return }

if (-not $Command -or $Command.Count -eq 0) {
  Write-Host "用法: battle-debug.ps1 <命令...> | -State | -Watch | -Help"
  Write-Host "命令: skipintro | sethp <i> <v> | sethpp <i> <pct> | damage <i> <v> | heal <i> | kill <i> |"
  Write-Host "      power <i> <v> | god <i> on|off | setstate <i> <no> | pos <i> <x> <y> | face <i> l|r |"
  Write-Host "      sep <units> | timer <sec> | win <i> | resetround | pause | resume | step [n]"
  return
}

Send-Cmd ($Command -join ' ')
