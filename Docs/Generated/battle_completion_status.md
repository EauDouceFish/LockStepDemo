# Battle Completion Status

Generated: 2026-06-12.

## Current Demo Baseline

The Unity demo now has a playable MUGEN-style loop:

- Character discovery from `MugenSource` through DEF/SFF/AIR/CNS/CMD loaders.
- DEF package resolution prefers `<folder>.def`, then DEF files with CMD+CNS entries, then a stable fallback; case-insensitive lookup handles common Windows/MUGEN filename mismatches.
- Select screen with portraits plus a lightweight lobby flow: local versus, AI versus, Online mode, server display, Find Match, Matching, and Cancel.
- Client build settings now include only `Assets/Scenes/MugenTeamSelect.unity`; `MugenRelayServer.unity` is excluded from the player client.
- Android client now forces landscape orientation in PlayerSettings and at runtime, and the lobby buttons use high-contrast pink/sky-blue/gold styling with visible pressed/highlight states.
- 3v3 turn-based team battle through `MTeamMatch` and `MugenTeamBattleView`.
- MUGEN-style command buffer, state machine, hit pipeline, gethit states, knockdown/recovery, helper/projectile/explod runtime paths, and deterministic hash coverage.
- Mobile overlay input based on KiHan-style virtual joystick plus A/B/C/X/Y/Z/S buttons.
- KCP matchmaking MVP: queue, compatible pairing, room assignment, ready/start, input relay, cancel, leave, timeout, request id filtering, and a protocol/common compatibility hash.
- Round logic now allows a single KO to upgrade to double KO while still inside the `over_hittime` window, including rollback of a stale single-KO point if scoring already happened.
- Per-frame battle run logs now capture mode, local-test flag, player uid/agent/character, raw inputs, active command names, player state summaries, frame hash, and input/hash checksums. The verifier can replay either a raw `MBattleEngine` or a higher-level round/team driver.
- Standing guard entry now has the missing hardcoded `inguarddist + back` closure into state 120 and clears the `Guarding` flag when the current frame no longer satisfies that gate.
- External downloaded-character smoke coverage imports five public MUGEN repositories from `MugenSource/_downloads`, runs a short scripted complete round against KFM, records a run log, and replays the log to verify deterministic hashes.
- Runtime character data lookup now supports `Application.persistentDataPath/MugenSource`, `StreamingAssets/MugenSource` when it is a normal directory, desktop-adjacent `MugenSource`, and the current Editor layout.
- If no loadable characters are found, the lobby still refreshes mode controls and shows the expected `MugenSource` path instead of making the mode button appear inert.
- Android MVP builds include `Assets/Resources/MugenSourceBundle.bytes`, a compressed full demo roster that extracts on first launch to `persistentDataPath/MugenSource`.

## Network MVP Completed On 2026-06-12

- `MugenMatchServerCore` is a pure C# room/match core for small party scale: two-player rooms, multiple simultaneous rooms, in-memory only.
- `Tools/mugen-relay-server/MugenRelayServer` is the primary pure .NET UDP/KCP relay for port `7777`; the Unity relay scene remains as a fallback.
- `MugenRoutedNetChannel` separates lockstep input messages from control messages so start/close packets are not swallowed by input polling.
- `MugenLockstepSession` has input backpressure so long missing-remote-input stalls cannot overwrite unsimulated ring-buffer frames.
- `FindMatch` / `MatchFound` / `CancelMatch` / `RoomClosed` carry request identity where needed.
- Ready mismatch closes the room for both players instead of leaving the opponent waiting.
- Client compatibility hash covers the shared protocol/common baseline instead of the whole local character folder list, so unrelated local characters do not block matching.

Validation:

- Focused network/lockstep tests: 19 passed, 0 failed.
- Focused network/lockstep tests after lobby button update: 19 passed, 0 failed.
- Pure .NET relay build: passed, 0 warnings, 0 errors.
- Singapore VPS check: `mugen-server.service` active and UDP `0.0.0.0:7777` listening.
- Unity MCP PlayMode smoke after full Resources bundle: extracted resource bundle, `entries=12`, `names=Ananzi|Animus|Final|Gustavo|Hashi|Janos|kfm|Maxine|Noroko|Peketo|Shar-Makai|Terrarian`, `bg=D4D4D4`, Console errors 0.
- Full logic suite: 767 passed, 0 failed.
- Focused Battle slice after annotation batch 1: 194 passed, 0 failed.
- Focused round-system tests after `over_hittime` DKO fixes: 23 passed, 0 failed.
- Focused battle/log/guard/stage/net slice after run-log and guard updates: 56 passed, 0 failed.
- Downloaded public-character import/run-log replay smoke: 5 passed, 0 failed.
- Full logic suite after run-log verifier hardening, guard-state persistence, external-character smoke, and docs updates: 780 passed, 0 failed.
- `git diff --check`: passed.
- `dotnet build Assembly-CSharp.csproj --no-restore`: blocked by stale generated Unity project entries for missing historical source files and unresolved Unity package assemblies before reaching a useful compile signal. The cleaned `MugenTeamBattleView.cs` still needs Unity Editor refresh/compile validation.

## Function Audit Status

Generated audit files:

- `Docs/Generated/battle_logic_functions_csharp.tsv`
- `Docs/Generated/ikemen_battle_functions_go.tsv`
- `Docs/Generated/battle_function_audit.md`
- `Docs/Generated/ikemen_functions_not_name_matched.md`

Current scanner result after annotation batches and the latest audit rerun:

- C# battle logic functions: 743.
- Annotated with scanner-recognized Ikemen/project references: 743.
- Still `needs-reference`: 0.
- At 50 functions per subagent batch, the remaining work is 0 batches.
- Batch 1 covered 52 additional functions in `Assets/Logic/Mugen/Battle`.
- Batches 2-4 covered 150 additional functions across `Mugen.Char`, `Mugen.Command`, and Battle debug/trace/AI code.
- Latest batches covered the remaining detected gaps in `Mugen.Expr` and `Mugen.StateCtrl`, and the generated TSV now reports full scanner coverage.

Important: `battle_function_audit.md` automatic candidates are name matches only. They are not equivalence claims until a human/source-level note is added near the C# function.

## Still Not Valid To Claim

- Not yet valid to claim "arbitrary MUGEN character is fully Ikemen-equivalent." The demo imports and runs many real characters, but compatibility still depends on controller/trigger coverage.
- No completed Ikemen headless oracle runner comparing the same scenario frame by frame against C# traces.
- Visual/audio effects are deterministic presentation events and partial Unity rendering, not full Ikemen shader/audio/camera parity.
- Networking is small-room delay lockstep. It is not rollback, not reconnect-capable, and not 10 players in one battle.
- Rollback has engine snapshot/restore primitives and deterministic hashes, but there is still no production rollback coordinator that owns input history, snapshot cadence, resimulation, and match-level state restoration.
- Run logs are currently in-memory diagnostic objects plus Unity `Debug.Log` lines. They are not yet persisted as signed JSON artifacts with build/version metadata, roster hashes, seed, or server upload policy.
- The run-log verifier now rejects incomplete logs, non-contiguous frame indices, input checksum mismatches, hash checksum mismatches, final hash mismatches, and per-frame replay hash mismatches. Segment/windowed log verification is not implemented yet.
- Stage bounds now clamp X position and stop wall velocity, but full Ikemen-style camera/screenbound/playerpush/cornerpush parity is still open.
- Matchmaking no longer rejects peers because of unrelated local roster differences; if a peer lacks the opponent's selected character folder, the battle load still fails locally and needs a user-facing import report.
- Server deployment to `dev@47.84.193.58` is complete for the pure .NET relay server. systemd service `mugen-server.service` is active and listening on UDP `7777`.
- Minecraft-themed MUGEN character search did not produce a clearly licensed public source repository in the current pass. The downloaded validation set therefore uses five public GitHub repositories, with license status documented separately.

## Highest-Value Remaining Work

- Keep source-level Ikemen reference annotations current when adding new battle functions.
- Add a Unity-facing import compatibility report before entering battle, so broken/missing DEF/CNS/CMD assets are shown clearly to the user.
- Harden long-tail MUGEN controllers/triggers that still fall back or parse-only.
- Validate two external phone clients against the deployed VPS relay.
- Add an Ikemen oracle trace path for a small fixed roster and use it to classify true semantic gaps.
- Persist run logs to JSON on client/server and include local-test/player uid/AI/script labels, move order, checksums, build id, roster ids, and verification result.
