# Demo Showcase Readiness - 2026-06-13

## Verdict

Current status: showcase-ready for a controlled MUGEN roster and basic online battle demo.

Do not claim: arbitrary MUGEN character full compatibility or complete Ikemen-equivalent logic semantics.

## Checklist

| Area | Status | Notes |
|---|---|---|
| Character discovery/import | Partial | DEF/CNS/CMD/AIR/SFF supported for current roster and smoke-tested external packages. Select screen now surfaces compatibility diagnostics instead of silently hiding parser gaps. |
| Arbitrary MUGEN character support | Not complete | Remaining gaps include long-tail controllers/triggers, localcoord/resource ownership, full ACT/SND/audio, AIR copy-action details, strict import diagnostics, and Ikemen oracle comparison. |
| SFF rendering | Partial+ | SFFv1 and SFFv2 directory loading exist. Unity sprite loader now accepts SFFv2 PNG formats 10/11/12 instead of skipping them. |
| Basic battle loop | Pass for supported subset | Team battle, health UI, round UI, imported character data, deterministic run logs, and downloaded-character smoke tests are in place for supported characters. |
| KCP online | Pass demo-level | Pure .NET relay builds, KCP matchmaking/input relay exists, and server-side input validation is covered by tests. |
| Weak network simulation | Pass demo-level | BattleScene weak-net button is wired through client transport drop/delay/jitter simulation. |
| Online hand-feel optimization | Pass demo-level | Prediction/rollback and visual assist can be toggled separately from raw online mode. |
| Rollback framework | Pass demo-level | Match/round snapshots, restore, prediction buffer, confirmed-frame rollback, and hash checks are implemented and covered by focused MUGEN tests. |
| Lifecycle rollback | Pass demo-level | Team/round lifecycle state is snapshot/restored; stale lifecycle callbacks are guarded in tests. |
| Ikemen logic parity | Not complete | Code has many Ikemen references and targeted fixes, but there is no completed Ikemen headless oracle trace proving frame-by-frame equivalence. |
| Simple anti-cheat | Pass demo-level | Content/protocol hash, input validation, duplicate/conflict rejection, and client hash reports exist. This is not strong anti-cheat against compromised clients. |
| Unity runtime validation | Blocked this pass | Logic tests and relay build pass. Unity batchmode compile/playmode was not rerun because the project is already open in Unity. |

## Improvements Completed In This Pass

- Added selected-team compatibility diagnostics on the select screen.
- Fixed `DestroySelf` so helper destruction terminates the current controller chain.
- Added focused tests for `DestroySelf` helper/player behavior.
- Enabled Unity sprite construction for SFFv2 PNG formats 10/11/12.
- Hardened SFF true-color sprite construction with pre-validation before texture allocation.
- Aligned KCP content hash with the protocol/common baseline so unrelated local roster differences do not block matchmaking.

## Validation

- `dotnet test Tests\Lockstep.Logic.Tests\Lockstep.Logic.Tests.csproj --no-restore --filter "FullyQualifiedName~Mugen"`: 726 passed.
- `dotnet build Tools\mugen-relay-server\MugenRelayServer.csproj --no-restore`: passed, 0 warnings, 0 errors.
- `git diff --check -- Assets Tests Docs Tools`: passed.

## Remaining Non-Negotiable Gaps

- Build an Ikemen oracle runner that produces comparable semantic traces for fixed scenarios.
- Convert compatibility diagnostics into a strict import mode for release validation.
- Finish long-tail controller/trigger parity and resource ownership/localcoord semantics.
- Persist signed run logs with build id, roster hash, seed, frame hashes, and verification result.
- Perform real two-device KCP validation against the deployed relay with weak-net toggles.
