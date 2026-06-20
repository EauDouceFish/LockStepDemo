# External MUGEN Character Smoke Report

Generated: 2026-06-12.

## Scope

This report validates the current demo importer and deterministic battle log path against five public MUGEN character repositories cloned under `D:\Desktop\demo\MugenSource\_downloads`.

The smoke test does not claim full Ikemen parity or full legal redistributability. It checks that each downloaded package can be parsed, spawned into a battle against KFM, driven through a short complete scripted round, recorded through `MBattleRunLogRecorder`, and replay-verified through `MBattleRunLogVerifier`.

## Character Set

| Character | Local Folder | DEF | License Status |
|---|---|---|---|
| Code Fu Man | `_downloads/CodeFuMan` | `cfm.def` | `LICENSE` present in repository |
| DGBlossom | `_downloads/dgblossom-mugen` | `DGBlossom.def` | `LICENSE.md` present in repository |
| DGButtercup | `_downloads/dgbuttercup-mugen` | `dgbuttercup.def` | `LICENSE.md` present in repository |
| NarutoHokage | `_downloads/NarutoHokage` | `Naruto Nanadaime.def` | Public repository; no explicit license found |
| Invisible | `_downloads/invisible-character` | `Invisible.def` | Public archived repository; no explicit license found |

Source repository pages checked:

- https://github.com/Jesuszilla/CodeFuMan
- https://github.com/dg410a/dgblossom-mugen
- https://github.com/dg410a/dgbuttercup-mugen
- https://github.com/BillsLasa/2D-Naruto-Hokage-Fighter---M.U.G.E.N.
- https://github.com/mugen-launcher/invisible-character

Minecraft-themed note: the current online/source pass did not find a clearly licensed public Minecraft-themed MUGEN character repository suitable for automated import. Do not list a Minecraft character as open-source until a license file or explicit permission is confirmed.

## Test Harness

Test file:

- `Tests/Lockstep.Logic.Tests/Mugen/Battle/DownloadedCharacterRunLogTests.cs`

Scenario:

- P0: downloaded character, `uid=local-test-<name>`, `agent=script`.
- P1: local KFM opponent, `uid=ai-kfm`, `agent=ai-script`.
- Mode: `MBattleRunMode.LocalTest`.
- Round: short single-round timeover-compatible match (`RoundTime=90`, small over windows).
- Inputs: deterministic virtual input table for both players.
- Stage: symmetric X clamp at 160 logic units.
- Verification: replay recorded frame inputs through a fresh round and compare every recorded engine hash.

Latest focused result:

- `dotnet test Tests/Lockstep.Logic.Tests/Lockstep.Logic.Tests.csproj --no-restore --filter "FullyQualifiedName~DownloadedCharacterRunLogTests"`
- Passed: 5.
- Failed: 0.
- Skipped: 0.

Per-character run output from the latest focused pass:

| Character | Frames | Final Hash | Input Checksum |
|---|---:|---|---|
| CodeFuMan | 94 | `54f4c33f8e3bf85f` | `ef26b8302a34cd98` |
| DGBlossom | 94 | `91ccbd268f84f9c6` | `ef26b8302a34cd98` |
| DGButtercup | 94 | `8ebf1d49ce09fbc5` | `ef26b8302a34cd98` |
| NarutoHokage | 94 | `a94902d4d7f5194c` | `ef26b8302a34cd98` |
| Invisible | 94 | `5cd4eae8c2a2fb21` | `ef26b8302a34cd98` |

## What This Proves

- The loader can resolve the DEF/CNS/CMD/AIR paths for the five downloaded packages on this workspace.
- The battle loop can run real downloaded character data through a complete short round without runtime exceptions.
- The run log records local-test status, player uid/agent/character labels, raw input bits, active command names, state summaries, hashes, and checksums.
- Logged inputs replay to the same per-frame simulation hash for the tested scenarios.
- Stage edge clamping is active in the scenario, so basic wall bounds are included in the smoke coverage.

## Remaining Gaps

- This is not an Ikemen oracle comparison. It only proves deterministic self-consistency of the C# engine.
- A short timeover round is a stable MVP definition of "complete round"; it is not proof that every character can naturally KO with authored moves.
- Full server audit logs still need JSON persistence, build/roster hashes, seed metadata, and content signatures.
- Two repositories in this set have no explicit license file. Treat them as source-available test inputs, not redistributable open-source assets.
- Full cornerpush, screenbound, camera, playerpush, sound, palette, and long-tail controller parity remain open.
