# MUGEN Server One-Click Deploy

Generated: 2026-06-12.

Target VPS:

- `dev@47.84.193.58`
- UDP port: `7777`
- Remote directory: `/home/dev/mugen-server`
- systemd service: `mugen-server.service`

Current deployment status:

- Deployed on 2026-06-12.
- `mugen-server.service` is active.
- The process is listening on `0.0.0.0:7777/udp`.
- Runtime command: `/home/dev/mugen-server/mugen-server/MugenRelayServer --port 7777`.

## What Gets Deployed

The default deploy path uses the pure .NET relay server, so the VPS does not need Unity or .NET installed:

- Console host: `Tools/mugen-relay-server/Program.cs`
- Publish target: `linux-x64`, self-contained
- Match core: `Assets/Logic/Mugen/Battle/Net/MugenMatchServerCore.cs`
- Codec/protocol: `Assets/Logic/Framework/Network/*.cs`
- KCP core: `Assets/Plugins/Kcp/KCP.cs`

The older Unity relay scene is still present as a fallback:

- Scene: `Assets/Scenes/MugenRelayServer.unity`
- Server component: `Assets/Scripts/Network/MugenRelayServer.cs`

The server does matchmaking, room assignment, ready/start, leave/cancel/timeout, and KCP input relay. It does not run battle simulation; both clients run deterministic local lockstep.

## One Command

From `D:\Desktop\demo\LockstepActDemo`:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\deploy\Deploy-MugenServer.ps1 -Build
```

That command publishes the standalone .NET relay server and deploys it to the Singapore VPS. Unity is not required on the VPS.

If you already published a server package locally:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\deploy\Deploy-MugenServer.ps1 `
  -BuildDir .\Build\MugenRelayServerLinux
```

The Unity headless relay is only a fallback path. Use it only when you intentionally want a Unity server build:

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\deploy\Deploy-MugenServer.ps1 `
  -UnityBuild `
  -UnityPath "C:\Program Files\Unity\Hub\Editor\2022.3.55f1\Editor\Unity.exe"
```

## What The Script Does

1. Optionally publishes the standalone .NET relay server via:
   `dotnet publish Tools/mugen-relay-server/MugenRelayServer.csproj -c Release -r linux-x64 --self-contained true`.
2. Packs:
   - `MugenRelayServer`
   - its self-contained .NET runtime files
3. Uploads to:
   `/home/dev/mugen-server/releases/mugen-server.tgz`
4. Installs:
   `/etc/systemd/system/mugen-server.service`
5. Starts/restarts:
   `mugen-server.service`
6. Opens UDP `7777` through `ufw` when `ufw` exists.

## Server Commands

Quick status:

```bash
ssh dev@47.84.193.58 "systemctl is-active mugen-server && sudo systemctl status --no-pager --full mugen-server"
```

Check UDP listener:

```bash
ssh dev@47.84.193.58 "sudo ss -lunp | grep ':7777'"
```

Recent logs:

```bash
ssh dev@47.84.193.58 "sudo journalctl -u mugen-server -n 120 --no-pager"
```

Live logs while two clients are matching:

```bash
ssh dev@47.84.193.58 "sudo journalctl -u mugen-server -f"
```

Detailed match logs were enabled on 2026-06-12. A healthy room should show this control-message chain:

```text
recv from=<id> FindMatch ...
send to=<id> MatchFound room=<room> local=0 ...
send to=<id> MatchFound room=<room> local=1 ...
recv from=<id> RoomReady room=<room> local=0 seed=<seed>
recv from=<id> RoomReady room=<room> local=1 seed=<seed>
send to=<id> StartMatch room=<room> startFrame=0
send to=<id> StartMatch room=<room> startFrame=0
```

If the game freezes after entering a room:

- `MatchFound` exists but no `RoomReady`: that client is stuck or failed while loading the battle scene/characters.
- `RoomReady` then `RoomClosed reason=ready mismatch`: room id, player id, player count, or seed differs between client and server.
- `StartMatch` is sent but the client still waits: the issue is in client control-message polling or lockstep startup.
- Connections later show `reason=timeout`: the client stopped sending UDP/KCP packets after room entry.

Restart:

```bash
ssh dev@47.84.193.58 "sudo systemctl restart mugen-server"
```

Start:

```bash
ssh dev@47.84.193.58 "sudo systemctl start mugen-server"
```

Stop:

```bash
ssh dev@47.84.193.58 "sudo systemctl stop mugen-server"
```

Open UDP manually if needed:

```bash
ssh dev@47.84.193.58 "sudo ufw allow 7777/udp"
```

Manual local server smoke test:

```powershell
dotnet run --project .\Tools\mugen-relay-server\MugenRelayServer.csproj -- --port 17777
```

Cloud firewall/security group must also allow inbound UDP `7777`.

## Client Flow

1. Build or run two Unity clients from the same project/content version.
2. Open the team select screen. This screen is the current lightweight lobby.
3. Pick 3 P1 characters on each client.
4. Click `AI/Local/Online` until the mode button shows `Online`, or press `3`.
5. Confirm the lobby shows `Server 47.84.193.58:7777`.
6. Click `Find Match` on both clients, or press `Enter`.
7. While waiting, the button changes to `Matching` and `Cancel` appears.
8. When the server pairs both clients, both clients enter battle automatically.
9. In battle, clients exchange lockstep inputs through the relay.

Expected server-side sequence:

1. Each client sends `FindMatch`.
2. Server sends `MatchFound`.
3. Clients send `RoomReady`.
4. Server sends `StartMatch`.
5. During the fight, clients relay frame inputs through KCP.

If matching does not happen, first check:

- Both clients use the same build/content and therefore the same content hash.
- UDP `7777` is open in both the cloud security group and the VPS firewall.
- Server logs via `sudo journalctl -u mugen-server -f`.

## Client Build

Build Settings should contain the playable client flow scenes:

- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/MugenTeamSelect.unity`
- `Assets/Scenes/BattleScene.unity`

Do not include `Assets/Scenes/MugenRelayServer.unity` in the player client. That scene is only a Unity fallback server scene; the live VPS uses the pure .NET relay server.

Current MCP validation on 2026-06-15:

- Unity instance: `LockstepActDemo@31e0d1f6995b885f`
- Active scene: `Assets/Scenes/MugenTeamSelect.unity`
- Build scenes: `Assets/Scenes/MainMenu.unity`, `Assets/Scenes/MugenTeamSelect.unity`, `Assets/Scenes/BattleScene.unity`
- Console errors: 0
- PlayMode lobby smoke after full Resources bundle: `entries=10`, `names=Ananzi|Animus|Final|Hashi|Janos|kfm|Maxine|Noroko|Peketo|Shar-Makai`, `bg=D4D4D4`

Android phone build steps:

1. Open `File > Build Settings`.
2. Select `Android`.
3. Click `Switch Platform` if needed.
4. Confirm `Scenes In Build` contains `MainMenu`, `MugenTeamSelect`, and `BattleScene`, in that order.
5. Build an APK/AAB.
6. Install the same build on both phones or on one phone plus one editor/client.
7. The APK now includes `Assets/Resources/MugenSourceBundle.bytes`; on first launch it extracts the bundled roster to app storage.

Bundled roster:

- `Ananzi`
- `Animus`
- `Final`
- `Gustavo`
- `Hashi`
- `Janos`
- `kfm`
- `Maxine`
- `Noroko`
- `Peketo`
- `Shar-Makai`
- `Terrarian`

The package excludes `_downloads` and `_reference` to avoid shipping external caches and Ikemen source references.

The runtime character search order is now:

1. `MUGEN_SOURCE` environment variable, when present.
2. `Application.persistentDataPath/MugenSource`.
3. `Application.streamingAssetsPath/MugenSource`, when it is a normal directory.
4. `Application.dataPath/../MugenSource`, useful for desktop builds beside the executable.
5. `Application.dataPath/../../MugenSource`, useful for the current Editor layout.

Manual `adb` push is optional now. Use it only if you want to override or add more characters:

```powershell
$pkg = "com.DefaultCompany.LockstepActDemo"
adb shell mkdir -p /sdcard/Android/data/$pkg/files/MugenSource
adb push D:\Desktop\demo\MugenSource\kfm /sdcard/Android/data/$pkg/files/MugenSource/kfm
adb push D:\Desktop\demo\MugenSource\Maxine /sdcard/Android/data/$pkg/files/MugenSource/Maxine
adb push D:\Desktop\demo\MugenSource\Gustavo /sdcard/Android/data/$pkg/files/MugenSource/Gustavo
```

Both clients must have the same `Terrarian/common1.cns` because the matchmaking content hash includes the shared common file. The bundled Resources package already satisfies this.

## Phone Client Troubleshooting

If the app opens in portrait:

- Rebuild after the 2026-06-12 mobile fix. The project now sets Android orientation to `LandscapeLeft`, disables portrait autorotation, and the lobby script also forces `ScreenOrientation.LandscapeLeft` at runtime.

If no characters appear:

- Rebuild after the full Resources bundle change. The APK now contains `MugenSourceBundle.bytes` and should show all 12 bundled characters after first launch.
- The lobby now shows the first expected data path on screen when it finds no loadable characters.
- If an old empty `MugenSource` was created by a previous build, clear app data or reinstall the app before testing the new APK.

Check the installed package and data directory:

```powershell
adb shell pm list packages | findstr Lockstep
adb shell run-as com.DefaultCompany.LockstepActDemo pwd
```

If `run-as` works, internal app storage is the most reliable path for development builds:

```powershell
adb push D:\Desktop\demo\MugenSource\kfm /data/local/tmp/kfm
adb push D:\Desktop\demo\MugenSource\Maxine /data/local/tmp/Maxine
adb push D:\Desktop\demo\MugenSource\Gustavo /data/local/tmp/Gustavo
adb shell run-as com.DefaultCompany.LockstepActDemo mkdir -p files/MugenSource
adb shell run-as com.DefaultCompany.LockstepActDemo cp -r /data/local/tmp/kfm files/MugenSource/kfm
adb shell run-as com.DefaultCompany.LockstepActDemo cp -r /data/local/tmp/Maxine files/MugenSource/Maxine
adb shell run-as com.DefaultCompany.LockstepActDemo cp -r /data/local/tmp/Gustavo files/MugenSource/Gustavo
```

If `run-as` is blocked, use the external app files path:

```powershell
adb shell mkdir -p /sdcard/Android/data/com.DefaultCompany.LockstepActDemo/files/MugenSource
adb push D:\Desktop\demo\MugenSource\kfm /sdcard/Android/data/com.DefaultCompany.LockstepActDemo/files/MugenSource/kfm
adb push D:\Desktop\demo\MugenSource\Maxine /sdcard/Android/data/com.DefaultCompany.LockstepActDemo/files/MugenSource/Maxine
adb push D:\Desktop\demo\MugenSource\Gustavo /sdcard/Android/data/com.DefaultCompany.LockstepActDemo/files/MugenSource/Gustavo
```

If the `Mode` button appears unresponsive:

- In the previous build, the no-character state returned before refreshing button labels. Rebuild after the 2026-06-12 mobile fix.
- The button label should now cycle through `AI`, `Online`, and `Local` even when no characters are loaded.

## SSH Authentication

The deploy script uses `ssh` and `scp`. If `ssh dev@47.84.193.58` asks for a password in your terminal, this agent cannot type it interactively through the tool. Use one of these options:

1. Run the deploy command yourself in PowerShell and enter the password when prompted.
2. Configure an SSH key once, then let the agent rerun the deploy script:

```powershell
ssh-keygen -t ed25519 -C "mugen-demo" 
type $env:USERPROFILE\.ssh\id_ed25519.pub | ssh dev@47.84.193.58 "mkdir -p ~/.ssh && cat >> ~/.ssh/authorized_keys && chmod 700 ~/.ssh && chmod 600 ~/.ssh/authorized_keys"
```

After that, this should work without a password:

```powershell
ssh -o BatchMode=yes dev@47.84.193.58 "echo ssh-ok"
```

## Current Limits

- Two-player rooms only.
- Multiple concurrent rooms are supported in memory.
- No account/auth/reconnect.
- No persistent match database.
- Server logs are .NET/systemd logs; structured JSON battle logs are still a future step.
- The deploy script assumes SSH/SCP work and that `dev` can run `sudo` for systemd installation.
