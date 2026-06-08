# Battle Completion Status

Generated: 2026-06-08.

## Completed in this pass

- Museum and MugenLive_kfm driver check: both routes use `MBattleEngine` and the MUGEN `MStateMachine`; the museum-specific layer is command preview/UI/rendering, not a separate battle state machine.
- Fixed Ananzi walk-release animation mismatch: `StageVar(info.name)` is now parsed/evaluated, so Ananzi state 0 can run its Ikemen-style idle `ChangeAnim value=0` guard instead of leaving `AnimNo=20`.
- Fixed Noroko state 210 animation loop/twitch: `ChangeAnim` now parses and applies `elem` / `elemtime`, matching Ikemen `changeAnimEx(value, elem, elemtime)` behavior for hit-confirm branches such as `ChangeAnim value=210 elem=4`.
- Added presentation entity/event flow for helpers, projectiles, explods, and visual/audio-like events: runtime state records helper/projectile/explod entities and PalFX/AllPalFX/BGPalFX/AfterImage/EnvShake/MakeDust/GameMakeAnim/PlaySnd style events for trace/UI consumption.
- Added museum rendering for helpers, projectiles, and explods, plus entity/event counters on the dashboard.
- Tightened helper/projectile ownership triggers: `NumHelper` and `NumProj` are now owner-scoped.
- Added/extended HitOverride guard path: `HitOverride` can force guard resolution and is covered by hit-system tests.
- Added tests for animation recovery, StageVar, ChangeAnim elem/elemtime parsing/runtime, visual events, explod lifetime, owner-scoped entity triggers, and HitOverride guard behavior.
- Regenerated battle function audit:
  - C# battle functions: 627.
  - Ikemen GO reference functions: 1435.
  - First 50 C# functions have source-level Ikemen/project-specific annotations and pass the audit scanner.

## Function audit outputs

- `Docs/Generated/battle_logic_functions_csharp.tsv`: scanned C# battle function list and annotation status.
- `Docs/Generated/ikemen_battle_functions_go.tsv`: scanned Ikemen GO function list from `MugenSource/_reference/Ikemen-GO/src`.
- `Docs/Generated/battle_function_audit.md`: C# to Ikemen/reference audit table.
- `Docs/Generated/ikemen_functions_not_name_matched.md`: Ikemen functions without a current name-level C# match.

## Still not valid to claim as complete Ikemen equivalence

- There is still no finished Ikemen headless oracle runner that executes the same scenario in Ikemen and compares C# traces frame by frame.
- PalFX/AfterImage/EnvShake/sound-style controllers currently emit deterministic trace/presentation events; they are not yet full Unity shader/audio/camera implementations matching Ikemen visual output.
- Helper/projectile/explod runtime and museum display are present, but full Ikemen draw priority, bind/remap edge cases, superpause interaction, shadow/transparency, and all removal semantics still need oracle validation.
- ReversalDef, AttackDist, guard distance, HitOverride, juggle, and target ownership have partial logic paths and targeted tests, but not exhaustive Ikemen oracle coverage across arbitrary characters.
- Round/intro/5900/KO/winpose flow is separate from the museum live flow and should not be treated as complete round-system equivalence.

## Validation commands run

- `dotnet test Tests/Lockstep.Logic.Tests/Lockstep.Logic.Tests.csproj --filter "FullyQualifiedName~AnimationRecoveryTests|FullyQualifiedName~BasicControllersTests|FullyQualifiedName~MugenCnsParserTests|FullyQualifiedName~StageVarTriggerTests|FullyQualifiedName~TierBNonEntityControllersTests|FullyQualifiedName~MHitSystemTests|FullyQualifiedName~EntityParamTriggerTests"`
- Result: 55 passed, 0 failed.
- `dotnet test Tests/Lockstep.Logic.Tests/Lockstep.Logic.Tests.csproj`
- Result: 687 passed, 0 failed.
