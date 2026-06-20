#!/usr/bin/env bash
# 战斗调试修改器 CLI（bash）。把命令写进 Unity 端 MugenBattleDebugBridge 轮询的命令文件，并能读回状态 JSON。
# 用法:
#   ./battle-debug.sh skipintro            # 跳过出场直接开打
#   ./battle-debug.sh sethpp 1 10          # P2 残血到 10%
#   ./battle-debug.sh win 0                # 强制 P1 胜
#   ./battle-debug.sh --state              # 打印当前对局状态 JSON
#   ./battle-debug.sh --watch              # 每 0.5s 刷新状态
#   ./battle-debug.sh --help               # 引擎内命令清单
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DIR="${BATTLE_DEBUG_DIR:-$SCRIPT_DIR/../../Temp/battle_debug}"
CMD_PATH="$DIR/cmd.txt"
STATE_PATH="$DIR/state.json"

show_state() {
  if [[ -f "$STATE_PATH" ]]; then
    cat "$STATE_PATH"; echo
  else
    echo "状态文件未生成（Unity 是否在运行且 EnableDebugBridge=true？）: $STATE_PATH" >&2
  fi
}

send_cmd() {
  mkdir -p "$DIR"
  printf '%s\n' "$*" >> "$CMD_PATH"
  echo "→ 已发送: $*"
}

case "${1:-}" in
  --help|-h)   send_cmd "help"; exit 0 ;;
  --state|-s)  show_state; exit 0 ;;
  --watch|-w)
    echo "监视状态（Ctrl+C 退出）: $STATE_PATH"
    while true; do clear; show_state; sleep 0.5; done ;;
  "" )
    echo "用法: battle-debug.sh <命令...> | --state | --watch | --help"
    echo "命令: skipintro | sethp <i> <v> | sethpp <i> <pct> | damage <i> <v> | heal <i> | kill <i> |"
    echo "      power <i> <v> | god <i> on|off | setstate <i> <no> | pos <i> <x> <y> | face <i> l|r |"
    echo "      sep <units> | timer <sec> | win <i> | resetround | pause | resume | step [n]"
    exit 0 ;;
  *) send_cmd "$*" ;;
esac
