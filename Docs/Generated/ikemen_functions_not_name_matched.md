# Ikemen Functions Not Name-Matched

This is a name-level gap list generated from selected Ikemen core battle files.
It is not proof that a function is logically absent: some C# implementations are intentionally renamed or split.
Each row still requires manual mapping to one of: implemented-equivalent, implemented-renamed, presentation-only, out-of-scope, or missing.

| Ikemen Function | Location | Receiver | Suggested Review Area |
|---|---:|---|---|
| `newAnimFrame` | `anim.go:30` | `` | animation/resource ownership |
| `ReadAnimFrame` | `anim.go:45` | `` | animation/resource ownership |
| `newAnimation` | `anim.go:203` | `` | animation/resource ownership |
| `ReadAnimation` | `anim.go:222` | `` | animation/resource ownership |
| `ReadAction` | `anim.go:367` | `` | animation/resource ownership |
| `isBlank` | `anim.go:396` | `(a *Animation)` | animation/resource ownership |
| `isCommonFX` | `anim.go:400` | `(a *Animation)` | animation/resource ownership |
| `AnimTime` | `anim.go:409` | `(a *Animation)` | animation/resource ownership |
| `AnimElemTime` | `anim.go:413` | `(a *Animation)` | animation/resource ownership |
| `AnimElemNo` | `anim.go:428` | `(a *Animation)` | animation/resource ownership |
| `GetLength` | `anim.go:473` | `(a *Animation)` | animation/resource ownership |
| `curFrame` | `anim.go:493` | `(a *Animation)` | animation/resource ownership |
| `CurrentFrame` | `anim.go:497` | `(a *Animation)` | animation/resource ownership |
| `drawFrame` | `anim.go:504` | `(a *Animation)` | animation/resource ownership |
| `SetAnimElem` | `anim.go:511` | `(a *Animation)` | animation/resource ownership |
| `animSeek` | `anim.go:544` | `(a *Animation)` | animation/resource ownership |
| `UpdateSprite` | `anim.go:573` | `(a *Animation)` | animation/resource ownership |
| `UpdateInterpolation` | `anim.go:639` | `(a *Animation)` | animation/resource ownership |
| `alphaToBlend` | `anim.go:753` | `(a *Animation)` | animation/resource ownership |
| `pal` | `anim.go:782` | `(a *Animation)` | animation/resource ownership |
| `drawSub1` | `anim.go:813` | `(a *Animation)` | animation/resource ownership |
| `NewAnimationTable` | `anim.go:1075` | `` | animation/resource ownership |
| `readAction` | `anim.go:1081` | `(at AnimationTable)` | animation/resource ownership |
| `resolveCopyAction` | `anim.go:1134` | `(at AnimationTable)` | animation/resource ownership |
| `newSpriteData` | `anim.go:1201` | `` | animation/resource ownership |
| `isBlank` | `anim.go:1209` | `(sd *SpriteData)` | animation/resource ownership |
| `draw` | `anim.go:1228` | `(dl DrawList)` | animation/resource ownership |
| `newShadowSprite` | `anim.go:1391` | `` | animation/resource ownership |
| `draw` | `anim.go:1432` | `(sl ShadowList)` | animation/resource ownership |
| `newReflectionSprite` | `anim.go:1648` | `` | animation/resource ownership |
| `draw` | `anim.go:1673` | `(rl ReflectionList)` | animation/resource ownership |
| `NewAnim` | `anim.go:1934` | `` | animation/resource ownership |
| `Copy` | `anim.go:1958` | `(a *Anim)` | animation/resource ownership |
| `ShallowCopy` | `anim.go:2051` | `(a *Animation)` | animation/resource ownership |
| `SetTile` | `anim.go:2060` | `(a *Anim)` | animation/resource ownership |
| `SetColorKey` | `anim.go:2064` | `(a *Anim)` | animation/resource ownership |
| `SetAlpha` | `anim.go:2068` | `(a *Anim)` | animation/resource ownership |
| `SetLocalcoord` | `anim.go:2072` | `(a *Anim)` | animation/resource ownership |
| `SetPos` | `anim.go:2084` | `(a *Anim)` | animation/resource ownership |
| `AddPos` | `anim.go:2091` | `(a *Anim)` | animation/resource ownership |
| `SetScale` | `anim.go:2096` | `(a *Anim)` | animation/resource ownership |
| `SetWindow` | `anim.go:2103` | `(a *Anim)` | animation/resource ownership |
| `SetVelocity` | `anim.go:2128` | `(a *Anim)` | animation/resource ownership |
| `SetMaxDist` | `anim.go:2136` | `(a *Anim)` | animation/resource ownership |
| `SetAccel` | `anim.go:2141` | `(a *Anim)` | animation/resource ownership |
| `updateVel` | `anim.go:2146` | `(a *Anim)` | animation/resource ownership |
| `Draw` | `anim.go:2213` | `(a *Anim)` | animation/resource ownership |
| `GetLength` | `anim.go:2255` | `(a *Anim)` | animation/resource ownership |
| `NewPreloadedAnims` | `anim.go:2264` | `` | animation/resource ownership |
| `addAnim` | `anim.go:2278` | `(pa PreloadedAnims)` | animation/resource ownership |
| `addSprite` | `anim.go:2282` | `(pa PreloadedAnims)` | animation/resource ownership |
| `updateSff` | `anim.go:2294` | `(pa PreloadedAnims)` | animation/resource ownership |
| `NewStringPool` | `bytecode.go:1056` | `` | expression/controller compiler |
| `ToAny` | `bytecode.go:1116` | `(bv BytecodeValue)` | expression/controller compiler |
| `SetF` | `bytecode.go:1126` | `(bv *BytecodeValue)` | expression/controller compiler |
| `SetI` | `bytecode.go:1134` | `(bv *BytecodeValue)` | expression/controller compiler |
| `SetI64` | `bytecode.go:1138` | `(bv *BytecodeValue)` | expression/controller compiler |
| `SetB` | `bytecode.go:1142` | `(bv *BytecodeValue)` | expression/controller compiler |
| `bvNone` | `bytecode.go:1147` | `` | expression/controller compiler |
| `BytecodeUndefined` | `bytecode.go:1151` | `` | expression/controller compiler |
| `BytecodeFloat` | `bytecode.go:1155` | `` | expression/controller compiler |
| `BytecodeInt` | `bytecode.go:1162` | `` | expression/controller compiler |
| `BytecodeInt64` | `bytecode.go:1166` | `` | expression/controller compiler |
| `BytecodeBool` | `bytecode.go:1170` | `` | expression/controller compiler |
| `PushI` | `bytecode.go:1184` | `(bs *BytecodeStack)` | expression/controller compiler |
| `PushI64` | `bytecode.go:1188` | `(bs *BytecodeStack)` | expression/controller compiler |
| `PushF` | `bytecode.go:1192` | `(bs *BytecodeStack)` | expression/controller compiler |
| `PushB` | `bytecode.go:1196` | `(bs *BytecodeStack)` | expression/controller compiler |
| `Top` | `bytecode.go:1200` | `(bs BytecodeStack)` | expression/controller compiler |
| `Dup` | `bytecode.go:1221` | `(bs *BytecodeStack)` | expression/controller compiler |
| `Alloc` | `bytecode.go:1229` | `(bs *BytecodeStack)` | expression/controller compiler |
| `Float32frombytes` | `bytecode.go:1245` | `` | expression/controller compiler |
| `append` | `bytecode.go:1251` | `(be *BytecodeExp)` | expression/controller compiler |
| `appendValue` | `bytecode.go:1255` | `(be *BytecodeExp)` | expression/controller compiler |
| `appendI32s` | `bytecode.go:1292` | `(be *BytecodeExp)` | expression/controller compiler |
| `appendI32Op` | `bytecode.go:1299` | `(be *BytecodeExp)` | expression/controller compiler |
| `appendI64Op` | `bytecode.go:1305` | `(be *BytecodeExp)` | expression/controller compiler |
| `ReadIntAt` | `bytecode.go:1311` | `(be BytecodeExp)` | expression/controller compiler |
| `ReadPoolStringAt` | `bytecode.go:1318` | `(be BytecodeExp)` | expression/controller compiler |
| `PeekLength` | `bytecode.go:1324` | `(be BytecodeExp)` | expression/controller compiler |
| `JumpToNext` | `bytecode.go:1330` | `(be BytecodeExp)` | expression/controller compiler |
| `rangeCheck` | `bytecode.go:1519` | `(BytecodeExp)` | expression/controller compiler |
| `round` | `bytecode.go:1803` | `(BytecodeExp)` | expression/controller compiler |
| `atan2` | `bytecode.go:1826` | `(BytecodeExp)` | expression/controller compiler |
| `sign` | `bytecode.go:1834` | `(BytecodeExp)` | expression/controller compiler |
| `rad` | `bytecode.go:1847` | `(BytecodeExp)` | expression/controller compiler |
| `deg` | `bytecode.go:1854` | `(BytecodeExp)` | expression/controller compiler |
| `lerp` | `bytecode.go:1861` | `(BytecodeExp)` | expression/controller compiler |
| `run_st` | `bytecode.go:2379` | `(be BytecodeExp)` | expression/controller compiler |
| `run_const` | `bytecode.go:2422` | `(be BytecodeExp)` | expression/controller compiler |
| `run_ex` | `bytecode.go:2948` | `(be BytecodeExp)` | expression/controller compiler |
| `run_ex2` | `bytecode.go:3552` | `(be BytecodeExp)` | expression/controller compiler |
| `run_ex3` | `bytecode.go:4182` | `(be BytecodeExp)` | expression/controller compiler |
| `evalI64` | `bytecode.go:4384` | `(be BytecodeExp)` | expression/controller compiler |
| `evalB` | `bytecode.go:4388` | `(be BytecodeExp)` | expression/controller compiler |
| `evalS` | `bytecode.go:4393` | `(be BytecodeExp)` | expression/controller compiler |
| `newStateBlock` | `bytecode.go:4520` | `` | expression/controller compiler |
| `newStateControllerBase` | `bytecode.go:4716` | `` | expression/controller compiler |
| `beToExp` | `bytecode.go:4720` | `(StateControllerBase)` | expression/controller compiler |
| `fToExp` | `bytecode.go:4724` | `(StateControllerBase)` | expression/controller compiler |
| `iToExp` | `bytecode.go:4733` | `(StateControllerBase)` | expression/controller compiler |
| `i64ToExp` | `bytecode.go:4742` | `(StateControllerBase)` | expression/controller compiler |
| `bToExp` | `bytecode.go:4751` | `(StateControllerBase)` | expression/controller compiler |
| `hasParam` | `bytecode.go:4790` | `(scb StateControllerBase)` | expression/controller compiler |
| `getRedirectedChar` | `bytecode.go:4807` | `` | expression/controller compiler |
| `runSub` | `bytecode.go:4905` | `(sc hitBy)` | expression/controller compiler |
| `isPalFXParam` | `bytecode.go:5875` | `` | expression/controller compiler |
| `runSub` | `bytecode.go:5897` | `(sc palFX)` | expression/controller compiler |
| `parseInterpolation` | `bytecode.go:6400` | `(sc explod)` | expression/controller compiler |
| `isAfterImageParam` | `bytecode.go:7108` | `` | expression/controller compiler |
| `runSub` | `bytecode.go:7134` | `(sc afterImage)` | expression/controller compiler |
| `isHitDefParam` | `bytecode.go:7282` | `` | expression/controller compiler |
| `runSub` | `bytecode.go:7408` | `(sc hitDef)` | expression/controller compiler |
| `newStateBytecode` | `bytecode.go:15136` | `` | expression/controller compiler |
| `init` | `bytecode.go:15147` | `(sb *StateBytecode)` | expression/controller compiler |
| `newStageCamera` | `camera.go:67` | `` | manual review |
| `newCamera` | `camera.go:104` | `` | manual review |
| `Init` | `camera.go:184` | `(c *Camera)` | manual review |
| `ResetTracking` | `camera.go:201` | `(c *Camera)` | manual review |
| `setScreenPos` | `camera.go:210` | `(c *Camera)` | manual review |
| `SaveRestoreTracking` | `camera.go:222` | `(c *Camera)` | manual review |
| `ScaleBound` | `camera.go:257` | `(c *Camera)` | manual review |
| `XBound` | `camera.go:269` | `(c *Camera)` | manual review |
| `BaseScale` | `camera.go:275` | `(c *Camera)` | manual review |
| `GroundLevel` | `camera.go:279` | `(c *Camera)` | manual review |
| `reduceZoomSpeed` | `camera.go:513` | `(c *Camera)` | manual review |
| `keepScreenEdge` | `camera.go:552` | `(c *Camera)` | manual review |
| `keepStageEdge` | `camera.go:568` | `(c *Camera)` | manual review |
| `hardLimit` | `camera.go:580` | `(c *Camera)` | manual review |
| `reduceYScrollSpeed` | `camera.go:587` | `(c *Camera)` | manual review |
| `boundY` | `camera.go:598` | `(c *Camera)` | manual review |
| `draw` | `char.go:207` | `(dc *DebugClsn)` | manual review |
| `init` | `char.go:288` | `(cd *CharData)` | manual review |
| `init` | `char.go:350` | `(cs *CharSize)` | manual review |
| `init` | `char.go:436` | `(cv *CharVelocity)` | manual review |
| `init` | `char.go:497` | `(cm *CharMovement)` | manual review |
| `finalizeParams` | `char.go:828` | `(hd *HitDef)` | manual review |
| `updateStateType` | `char.go:1016` | `(hd *HitDef)` | state machine |
| `testAttr` | `char.go:1022` | `(hd *HitDef)` | manual review |
| `testReversalAttr` | `char.go:1027` | `(hd *HitDef)` | manual review |
| `selectiveReset` | `char.go:1148` | `(ghv *GetHitVar)` | manual review |
| `clearOff` | `char.go:1190` | `(ghv *GetHitVar)` | manual review |
| `chainId` | `char.go:1194` | `(ghv GetHitVar)` | manual review |
| `idMatch` | `char.go:1201` | `(ghv GetHitVar)` | manual review |
| `dropPlayerId` | `char.go:1219` | `(ghv *GetHitVar)` | manual review |
| `addId` | `char.go:1228` | `(ghv *GetHitVar)` | manual review |
| `testAttr` | `char.go:1235` | `(ghv *GetHitVar)` | manual review |
| `newAfterImage` | `char.go:1316` | `` | manual review |
| `setPalColor` | `char.go:1359` | `(ai *AfterImage)` | manual review |
| `setPalHueShift` | `char.go:1365` | `(ai *AfterImage)` | manual review |
| `setPalInvertall` | `char.go:1371` | `(ai *AfterImage)` | manual review |
| `setPalInvertblend` | `char.go:1377` | `(ai *AfterImage)` | manual review |
| `setPalBrightR` | `char.go:1383` | `(ai *AfterImage)` | manual review |
| `setPalBrightG` | `char.go:1389` | `(ai *AfterImage)` | manual review |
| `setPalBrightB` | `char.go:1395` | `(ai *AfterImage)` | manual review |
| `setPalContrastR` | `char.go:1401` | `(ai *AfterImage)` | manual review |
| `setPalContrastG` | `char.go:1407` | `(ai *AfterImage)` | manual review |
| `setPalContrastB` | `char.go:1413` | `(ai *AfterImage)` | manual review |
| `setup` | `char.go:1420` | `(ai *AfterImage)` | manual review |
| `recAfterImg` | `char.go:1484` | `(ai *AfterImage)` | manual review |
| `recAndCue` | `char.go:1543` | `(ai *AfterImage)` | manual review |
| `newExplod` | `char.go:1678` | `` | manual review |
| `initFromChar` | `char.go:1687` | `(e *Explod)` | manual review |
| `setAllPosX` | `char.go:1734` | `(e *Explod)` | manual review |
| `setAllPosY` | `char.go:1738` | `(e *Explod)` | manual review |
| `setAllPosZ` | `char.go:1742` | `(e *Explod)` | manual review |
| `setBind` | `char.go:1746` | `(e *Explod)` | manual review |
| `setPos` | `char.go:1754` | `(e *Explod)` | manual review |
| `matchId` | `char.go:1836` | `(e *Explod)` | manual review |
| `setAnim` | `char.go:1840` | `(e *Explod)` | manual review |
| `setAnimElem` | `char.go:1880` | `(e *Explod)` | manual review |
| `Interpolate` | `char.go:2245` | `(e *Explod)` | manual review |
| `resetInterpolation` | `char.go:2282` | `(e *Explod)` | manual review |
| `trueFacing` | `char.go:2310` | `(e *Explod)` | manual review |
| `newProjectile` | `char.go:2394` | `` | manual review |
| `initFromChar` | `char.go:2404` | `(p *Projectile)` | manual review |
| `setAllPos` | `char.go:2464` | `(p *Projectile)` | manual review |
| `paused` | `char.go:2475` | `(p *Projectile)` | manual review |
| `flagProjCancel` | `char.go:2611` | `(p *Projectile)` | manual review |
| `cancelHits` | `char.go:2623` | `(p *Projectile)` | hit/guard/target/juggle |
| `tradeDetection` | `char.go:2644` | `(p *Projectile)` | manual review |
| `cueDraw` | `char.go:2766` | `(p *Projectile)` | manual review |
| `root` | `char.go:2906` | `(p *Projectile)` | manual review |
| `owner` | `char.go:2910` | `(p *Projectile)` | manual review |
| `warnChar` | `char.go:2915` | `(p *Projectile)` | manual review |
| `loadMovelists` | `char.go:2945` | `` | manual review |
| `newCharGlobalInfo` | `char.go:3027` | `` | manual review |
| `changeStateType` | `char.go:3064` | `(ss *StateState)` | state machine |
| `changeMoveType` | `char.go:3069` | `(ss *StateState)` | manual review |
| `clearHitPauseExecutionToggleFlags` | `char.go:3105` | `(ss *StateState)` | hit/guard/target/juggle |
| `newChar` | `char.go:3337` | `` | manual review |
| `warn` | `char.go:3343` | `(c *Char)` | manual review |
| `panic` | `char.go:3347` | `(c *Char)` | manual review |
| `init` | `char.go:3356` | `(c *Char)` | manual review |
| `clearState` | `char.go:3418` | `(c *Char)` | state machine |
| `clsnOverlapTrigger` | `char.go:3446` | `(c *Char)` | manual review |
| `addChild` | `char.go:3457` | `(c *Char)` | manual review |
| `enemyNearP2Clear` | `char.go:3471` | `(c *Char)` | manual review |
| `prepareNextRound` | `char.go:3477` | `(c *Char)` | manual review |
| `gi` | `char.go:3524` | `(c *Char)` | manual review |
| `stOgi` | `char.go:3529` | `(c *Char)` | manual review |
| `stWgi` | `char.go:3539` | `(c *Char)` | manual review |
| `si` | `char.go:3548` | `(c *Char)` | manual review |
| `ocd` | `char.go:3552` | `(c *Char)` | manual review |
| `applyMapOverrides` | `char.go:3566` | `(c *Char)` | manual review |
| `resetModifyPlayer` | `char.go:3580` | `(c *Char)` | manual review |
| `loadPalettes` | `char.go:4233` | `(c *Char)` | manual review |
| `loadFx` | `char.go:4391` | `(c *Char)` | manual review |
| `clearHitCount` | `char.go:4463` | `(c *Char)` | hit/guard/target/juggle |
| `clearMoveHit` | `char.go:4469` | `(c *Char)` | hit/guard/target/juggle |
| `clearHitDef` | `char.go:4476` | `(c *Char)` | hit/guard/target/juggle |
| `changeAnimEx` | `char.go:4481` | `(c *Char)` | manual review |
| `changeAnim` | `char.go:4523` | `(c *Char)` | manual review |
| `changeAnim2` | `char.go:4551` | `(c *Char)` | manual review |
| `setAnimElem` | `char.go:4560` | `(c *Char)` | manual review |
| `setAnimElemTo` | `char.go:4588` | `(c *Char)` | manual review |
| `validatePlayerNo` | `char.go:4607` | `(c *Char)` | manual review |
| `setCtrl` | `char.go:4618` | `(c *Char)` | state machine |
| `setDizzy` | `char.go:4626` | `(c *Char)` | manual review |
| `setGuardBreak` | `char.go:4634` | `(c *Char)` | hit/guard/target/juggle |
| `scf` | `char.go:4642` | `(c *Char)` | manual review |
| `setSCF` | `char.go:4646` | `(c *Char)` | manual review |
| `unsetSCF` | `char.go:4654` | `(c *Char)` | manual review |
| `csf` | `char.go:4662` | `(c *Char)` | manual review |
| `setCSF` | `char.go:4666` | `(c *Char)` | manual review |
| `unsetCSF` | `char.go:4670` | `(c *Char)` | manual review |
| `setASF` | `char.go:4678` | `(c *Char)` | manual review |
| `unsetASF` | `char.go:4682` | `(c *Char)` | manual review |
| `parent` | `char.go:4686` | `(c *Char)` | manual review |
| `parentExist` | `char.go:4710` | `(c *Char)` | manual review |
| `root` | `char.go:4714` | `(c *Char)` | manual review |
| `stateOwner` | `char.go:4727` | `(c *Char)` | state machine |
| `helperTrigger` | `char.go:4732` | `(c *Char)` | manual review |
| `getHelperChainIndex` | `char.go:4765` | `(c *Char)` | manual review |
| `helperIndexTrigger` | `char.go:4825` | `(c *Char)` | manual review |
| `helperIndexExist` | `char.go:4835` | `(c *Char)` | manual review |
| `indexTrigger` | `char.go:4842` | `(c *Char)` | manual review |
| `targetTrigger` | `char.go:4864` | `(c *Char)` | hit/guard/target/juggle |
| `partner` | `char.go:4889` | `(c *Char)` | manual review |
| `partnerTag` | `char.go:4918` | `(c *Char)` | manual review |
| `enemy` | `char.go:4933` | `(c *Char)` | manual review |
| `enemyNearTrigger` | `char.go:4956` | `(c *Char)` | manual review |
| `p2` | `char.go:4969` | `(c *Char)` | manual review |
| `playerIDTrigger` | `char.go:4981` | `(c *Char)` | manual review |
| `playerIndexTrigger` | `char.go:4991` | `(c *Char)` | manual review |
| `playerTrigger` | `char.go:5001` | `(c *Char)` | manual review |
| `isEnemyOf` | `char.go:5011` | `(c *Char)` | manual review |
| `getAILevel` | `char.go:5034` | `(c *Char)` | manual review |
| `setAILevel` | `char.go:5044` | `(c *Char)` | manual review |
| `alive` | `char.go:5058` | `(c *Char)` | manual review |
| `activelyFighting` | `char.go:5064` | `(c *Char)` | manual review |
| `animElemNo` | `char.go:5071` | `(c *Char)` | manual review |
| `animElemTime` | `char.go:5078` | `(c *Char)` | manual review |
| `animExist` | `char.go:5088` | `(c *Char)` | manual review |
| `selfAnimExist` | `char.go:5102` | `(c *Char)` | manual review |
| `animTime` | `char.go:5109` | `(c *Char)` | manual review |
| `updateCurFrame` | `char.go:5117` | `(c *Char)` | manual review |
| `backEdge` | `char.go:5125` | `(c *Char)` | manual review |
| `backEdgeBodyDist` | `char.go:5132` | `(c *Char)` | manual review |
| `backEdgeDist` | `char.go:5146` | `(c *Char)` | manual review |
| `bottomEdge` | `char.go:5153` | `(c *Char)` | manual review |
| `botBoundBodyDist` | `char.go:5157` | `(c *Char)` | manual review |
| `botBoundDist` | `char.go:5161` | `(c *Char)` | manual review |
| `comboCount` | `char.go:5169` | `(c *Char)` | manual review |
| `command` | `char.go:5176` | `(c *Char)` | manual review |
| `commandByName` | `char.go:5211` | `(c *Char)` | manual review |
| `assertCommand` | `char.go:5219` | `(c *Char)` | manual review |
| `constp` | `char.go:5251` | `(c *Char)` | manual review |
| `ctrl` | `char.go:5255` | `(c *Char)` | state machine |
| `drawgame` | `char.go:5260` | `(c *Char)` | manual review |
| `frontEdge` | `char.go:5264` | `(c *Char)` | manual review |
| `frontEdgeBodyDist` | `char.go:5271` | `(c *Char)` | manual review |
| `frontEdgeDist` | `char.go:5284` | `(c *Char)` | manual review |
| `gameHeight` | `char.go:5291` | `(c *Char)` | manual review |
| `gameWidth` | `char.go:5295` | `(c *Char)` | manual review |
| `getPlayerID` | `char.go:5300` | `(c *Char)` | manual review |
| `runOrderTrigger` | `char.go:5308` | `(c *Char)` | manual review |
| `powerOwner` | `char.go:5319` | `(c *Char)` | manual review |
| `getPower` | `char.go:5330` | `(c *Char)` | manual review |
| `hitDefAttr` | `char.go:5334` | `(c *Char)` | hit/guard/target/juggle |
| `isHelper` | `char.go:5346` | `(c *Char)` | manual review |
| `isPlayerType` | `char.go:5406` | `(c *Char)` | manual review |
| `isHost` | `char.go:5410` | `(c *Char)` | manual review |
| `leftEdge` | `char.go:5453` | `(c *Char)` | manual review |
| `lose` | `char.go:5457` | `(c *Char)` | manual review |
| `loseKO` | `char.go:5464` | `(c *Char)` | manual review |
| `loseTime` | `char.go:5468` | `(c *Char)` | manual review |
| `moveContact` | `char.go:5472` | `(c *Char)` | manual review |
| `moveCountered` | `char.go:5479` | `(c *Char)` | manual review |
| `moveGuarded` | `char.go:5486` | `(c *Char)` | hit/guard/target/juggle |
| `moveHit` | `char.go:5493` | `(c *Char)` | hit/guard/target/juggle |
| `moveReversed` | `char.go:5500` | `(c *Char)` | manual review |
| `numEnemy` | `char.go:5507` | `(c *Char)` | manual review |
| `numExplod` | `char.go:5520` | `(c *Char)` | manual review |
| `numPlayer` | `char.go:5532` | `(c *Char)` | manual review |
| `numText` | `char.go:5548` | `(c *Char)` | manual review |
| `explodVar` | `char.go:5560` | `(c *Char)` | manual review |
| `soundVar` | `char.go:5648` | `(c *Char)` | manual review |
| `numHelper` | `char.go:5750` | `(c *Char)` | manual review |
| `numPartner` | `char.go:5767` | `(c *Char)` | manual review |
| `numProj` | `char.go:5774` | `(c *Char)` | manual review |
| `numProjID` | `char.go:5781` | `(c *Char)` | manual review |
| `numTarget` | `char.go:5799` | `(c *Char)` | hit/guard/target/juggle |
| `palfxvar` | `char.go:5816` | `(c *Char)` | manual review |
| `palfxvar2` | `char.go:5848` | `(c *Char)` | manual review |
| `pauseTimeTrigger` | `char.go:5866` | `(c *Char)` | manual review |
| `canOwnProjectiles` | `char.go:5877` | `(c *Char)` | manual review |
| `projTimeTrigger` | `char.go:5881` | `(c *Char)` | manual review |
| `projCancelTime` | `char.go:5894` | `(c *Char)` | manual review |
| `projContactTime` | `char.go:5900` | `(c *Char)` | manual review |
| `projGuardedTime` | `char.go:5906` | `(c *Char)` | hit/guard/target/juggle |
| `projHitTime` | `char.go:5912` | `(c *Char)` | hit/guard/target/juggle |
| `reversalDefAttr` | `char.go:5918` | `(c *Char)` | manual review |
| `rightEdge` | `char.go:5922` | `(c *Char)` | manual review |
| `roundsExisted` | `char.go:5926` | `(c *Char)` | manual review |
| `roundsWon` | `char.go:5933` | `(c *Char)` | manual review |
| `screenPosX` | `char.go:5943` | `(c *Char)` | manual review |
| `screenPosY` | `char.go:5947` | `(c *Char)` | manual review |
| `screenHeight` | `char.go:5951` | `(c *Char)` | manual review |
| `screenWidth` | `char.go:5962` | `(c *Char)` | manual review |
| `selfStatenoExist` | `char.go:5966` | `(c *Char)` | state machine |
| `stageFrontEdgeDist` | `char.go:5976` | `(c *Char)` | manual review |
| `stageBackEdgeDist` | `char.go:5989` | `(c *Char)` | manual review |
| `teamLeader` | `char.go:6002` | `(c *Char)` | manual review |
| `teamSize` | `char.go:6009` | `(c *Char)` | manual review |
| `getTeamOrder` | `char.go:6026` | `(c *Char)` | manual review |
| `updateTeamOrder` | `char.go:6051` | `(c *Char)` | manual review |
| `changeTagOrder` | `char.go:6068` | `(c *Char)` | manual review |
| `changeTagLeader` | `char.go:6096` | `(c *Char)` | manual review |
| `topEdge` | `char.go:6164` | `(c *Char)` | manual review |
| `topBoundBodyDist` | `char.go:6168` | `(c *Char)` | manual review |
| `topBoundDist` | `char.go:6172` | `(c *Char)` | manual review |
| `win` | `char.go:6176` | `(c *Char)` | manual review |
| `winKO` | `char.go:6183` | `(c *Char)` | manual review |
| `winTime` | `char.go:6187` | `(c *Char)` | manual review |
| `winPerfect` | `char.go:6191` | `(c *Char)` | manual review |
| `winClutch` | `char.go:6195` | `(c *Char)` | manual review |
| `winType` | `char.go:6199` | `(c *Char)` | manual review |
| `getOwnChannels` | `char.go:6205` | `(c *Char)` | manual review |
| `autoTurn` | `char.go:6308` | `(c *Char)` | manual review |
| `updateFBFlip` | `char.go:6326` | `(c *Char)` | manual review |
| `shouldFaceP2` | `char.go:6352` | `(c *Char)` | manual review |
| `stateChange1` | `char.go:6379` | `(c *Char)` | state machine |
| `persistentChangeStateHitpauseCorrection` | `char.go:6495` | `(c *Char)` | hit/guard/target/juggle |
| `stateChange2` | `char.go:6519` | `(c *Char)` | state machine |
| `changeStateEx` | `char.go:6542` | `(c *Char)` | state machine |
| `changeState` | `char.go:6573` | `(c *Char)` | state machine |
| `selfState` | `char.go:6577` | `(c *Char)` | state machine |
| `destroy` | `char.go:6587` | `(c *Char)` | manual review |
| `destroySelf` | `char.go:6633` | `(c *Char)` | manual review |
| `newHelper` | `char.go:6664` | `(c *Char)` | manual review |
| `helperInit` | `char.go:6722` | `(c *Char)` | manual review |
| `spawnExplod` | `char.go:6819` | `(c *Char)` | manual review |
| `getMultipleExplods` | `char.go:6837` | `(c *Char)` | manual review |
| `getSingleExplod` | `char.go:6870` | `(c *Char)` | manual review |
| `explodDrawPal` | `char.go:6887` | `(c *Char)` | manual review |
| `commitExplod` | `char.go:6895` | `(c *Char)` | manual review |
| `explodBindTime` | `char.go:6966` | `(c *Char)` | manual review |
| `removeExplod` | `char.go:6976` | `(c *Char)` | manual review |
| `spawnText` | `char.go:7003` | `(c *Char)` | manual review |
| `getMultipleTexts` | `char.go:7021` | `(c *Char)` | manual review |
| `removeText` | `char.go:7054` | `(c *Char)` | manual review |
| `getAnimSprite` | `char.go:7085` | `(c *Char)` | manual review |
| `getSelfAnimSprite` | `char.go:7100` | `(c *Char)` | manual review |
| `getShadowReflectionSprite` | `char.go:7107` | `(c *Char)` | manual review |
| `getAnim` | `char.go:7127` | `(c *Char)` | manual review |
| `animSpriteSetup` | `char.go:7186` | `(c *Char)` | manual review |
| `posReset` | `char.go:7251` | `(c *Char)` | manual review |
| `setPosX` | `char.go:7268` | `(c *Char)` | manual review |
| `setPosY` | `char.go:7286` | `(c *Char)` | manual review |
| `setPosZ` | `char.go:7294` | `(c *Char)` | manual review |
| `addX` | `char.go:7310` | `(c *Char)` | manual review |
| `addY` | `char.go:7314` | `(c *Char)` | manual review |
| `addZ` | `char.go:7318` | `(c *Char)` | manual review |
| `hitAdd` | `char.go:7322` | `(c *Char)` | hit/guard/target/juggle |
| `spawnProjectile` | `char.go:7357` | `(c *Char)` | manual review |
| `projDrawPal` | `char.go:7457` | `(c *Char)` | manual review |
| `getMultipleProjs` | `char.go:7465` | `(c *Char)` | manual review |
| `getSingleProj` | `char.go:7508` | `(c *Char)` | manual review |
| `baseWidthFront` | `char.go:7525` | `(c *Char)` | manual review |
| `baseWidthBack` | `char.go:7539` | `(c *Char)` | manual review |
| `baseHeightTop` | `char.go:7553` | `(c *Char)` | manual review |
| `baseHeightBottom` | `char.go:7566` | `(c *Char)` | manual review |
| `baseDepthTop` | `char.go:7579` | `(c *Char)` | manual review |
| `baseDepthBottom` | `char.go:7583` | `(c *Char)` | manual review |
| `setWidth` | `char.go:7587` | `(c *Char)` | manual review |
| `setHeight` | `char.go:7597` | `(c *Char)` | manual review |
| `setDepth` | `char.go:7607` | `(c *Char)` | manual review |
| `setWidthEdge` | `char.go:7616` | `(c *Char)` | manual review |
| `setDepthEdge` | `char.go:7622` | `(c *Char)` | manual review |
| `updateClsnScale` | `char.go:7628` | `(c *Char)` | manual review |
| `updateSizeBox` | `char.go:7655` | `(c *Char)` | manual review |
| `sizeBoxToClsn` | `char.go:7675` | `(c *Char)` | manual review |
| `isTargetBound` | `char.go:7694` | `(c *Char)` | hit/guard/target/juggle |
| `initConstants` | `char.go:7698` | `(c *Char)` | manual review |
| `initCnsVar` | `char.go:7804` | `(c *Char)` | manual review |
| `varGet` | `char.go:7811` | `(c *Char)` | manual review |
| `fvarGet` | `char.go:7826` | `(c *Char)` | manual review |
| `sysVarGet` | `char.go:7839` | `(c *Char)` | manual review |
| `sysFvarGet` | `char.go:7852` | `(c *Char)` | manual review |
| `cnsVarSet` | `char.go:7865` | `(c *Char)` | manual review |
| `varSet` | `char.go:7905` | `(c *Char)` | manual review |
| `fvarSet` | `char.go:7915` | `(c *Char)` | manual review |
| `sysVarSet` | `char.go:7925` | `(c *Char)` | manual review |
| `sysFvarSet` | `char.go:7935` | `(c *Char)` | manual review |
| `varAdd` | `char.go:7945` | `(c *Char)` | manual review |
| `fvarAdd` | `char.go:7959` | `(c *Char)` | manual review |
| `sysVarAdd` | `char.go:7973` | `(c *Char)` | manual review |
| `sysFvarAdd` | `char.go:7987` | `(c *Char)` | manual review |
| `varRangeSet` | `char.go:8042` | `(c *Char)` | manual review |
| `fvarRangeSet` | `char.go:8046` | `(c *Char)` | manual review |
| `sysVarRangeSet` | `char.go:8050` | `(c *Char)` | manual review |
| `sysFvarRangeSet` | `char.go:8054` | `(c *Char)` | manual review |
| `setFacing` | `char.go:8058` | `(c *Char)` | manual review |
| `getMultipleStageBg` | `char.go:8068` | `(c *Char)` | manual review |
| `getSingleStageBg` | `char.go:8102` | `(c *Char)` | manual review |
| `numStageBG` | `char.go:8120` | `(c *Char)` | manual review |
| `getTarget` | `char.go:8133` | `(c *Char)` | hit/guard/target/juggle |
| `targetFacing` | `char.go:8161` | `(c *Char)` | hit/guard/target/juggle |
| `targetBind` | `char.go:8176` | `(c *Char)` | hit/guard/target/juggle |
| `bindToTarget` | `char.go:8190` | `(c *Char)` | hit/guard/target/juggle |
| `targetLifeAdd` | `char.go:8218` | `(c *Char)` | hit/guard/target/juggle |
| `targetPowerAdd` | `char.go:8250` | `(c *Char)` | hit/guard/target/juggle |
| `targetDizzyPointsAdd` | `char.go:8261` | `(c *Char)` | hit/guard/target/juggle |
| `targetGuardPointsAdd` | `char.go:8272` | `(c *Char)` | hit/guard/target/juggle |
| `targetRedLifeAdd` | `char.go:8283` | `(c *Char)` | hit/guard/target/juggle |
| `targetScoreAdd` | `char.go:8294` | `(c *Char)` | hit/guard/target/juggle |
| `targetState` | `char.go:8305` | `(c *Char)` | hit/guard/target/juggle |
| `targetVelSetX` | `char.go:8320` | `(c *Char)` | hit/guard/target/juggle |
| `targetVelSetY` | `char.go:8329` | `(c *Char)` | hit/guard/target/juggle |
| `targetVelSetZ` | `char.go:8338` | `(c *Char)` | hit/guard/target/juggle |
| `targetVelAddX` | `char.go:8347` | `(c *Char)` | hit/guard/target/juggle |
| `targetVelAddY` | `char.go:8356` | `(c *Char)` | hit/guard/target/juggle |
| `targetVelAddZ` | `char.go:8365` | `(c *Char)` | hit/guard/target/juggle |
| `targetDrop` | `char.go:8374` | `(c *Char)` | hit/guard/target/juggle |
| `lifeAdd` | `char.go:8464` | `(c *Char)` | manual review |
| `lifeSet` | `char.go:8504` | `(c *Char)` | manual review |
| `setPower` | `char.go:8562` | `(c *Char)` | manual review |
| `powerAdd` | `char.go:8575` | `(c *Char)` | manual review |
| `powerSet` | `char.go:8585` | `(c *Char)` | manual review |
| `dizzyPointsAdd` | `char.go:8589` | `(c *Char)` | manual review |
| `dizzyPointsSet` | `char.go:8601` | `(c *Char)` | manual review |
| `guardPointsAdd` | `char.go:8607` | `(c *Char)` | hit/guard/target/juggle |
| `guardPointsSet` | `char.go:8619` | `(c *Char)` | hit/guard/target/juggle |
| `redLifeAdd` | `char.go:8625` | `(c *Char)` | manual review |
| `redLifeSet` | `char.go:8637` | `(c *Char)` | manual review |
| `score` | `char.go:8654` | `(c *Char)` | manual review |
| `scoreAdd` | `char.go:8661` | `(c *Char)` | manual review |
| `scoreTotal` | `char.go:8668` | `(c *Char)` | manual review |
| `consecutiveWins` | `char.go:8682` | `(c *Char)` | manual review |
| `dizzyEnabled` | `char.go:8689` | `(c *Char)` | manual review |
| `guardBreakEnabled` | `char.go:8707` | `(c *Char)` | hit/guard/target/juggle |
| `redLifeEnabled` | `char.go:8725` | `(c *Char)` | manual review |
| `distY` | `char.go:8766` | `(c *Char)` | manual review |
| `distZ` | `char.go:8780` | `(c *Char)` | manual review |
| `bodyDistY` | `char.go:8818` | `(c *Char)` | manual review |
| `bodyDistZ` | `char.go:8840` | `(c *Char)` | manual review |
| `rdDistX` | `char.go:8855` | `(c *Char)` | manual review |
| `rdDistY` | `char.go:8869` | `(c *Char)` | manual review |
| `rdDistZ` | `char.go:8883` | `(c *Char)` | manual review |
| `p2BodyDistX` | `char.go:8891` | `(c *Char)` | manual review |
| `p2BodyDistY` | `char.go:8903` | `(c *Char)` | manual review |
| `p2BodyDistZ` | `char.go:8913` | `(c *Char)` | manual review |
| `setPauseTime` | `char.go:8921` | `(c *Char)` | manual review |
| `setSuperPauseTime` | `char.go:8939` | `(c *Char)` | manual review |
| `propagateIgnoreDarkenTime` | `char.go:8983` | `(c *Char)` | manual review |
| `getPalfx` | `char.go:9021` | `(c *Char)` | manual review |
| `getPalMap` | `char.go:9039` | `(c *Char)` | manual review |
| `pause` | `char.go:9043` | `(c *Char)` | manual review |
| `hitPause` | `char.go:9047` | `(c *Char)` | hit/guard/target/juggle |
| `angleSet` | `char.go:9051` | `(c *Char)` | manual review |
| `XangleSet` | `char.go:9055` | `(c *Char)` | manual review |
| `YangleSet` | `char.go:9059` | `(c *Char)` | manual review |
| `inputWait` | `char.go:9063` | `(c *Char)` | manual review |
| `makeDust` | `char.go:9080` | `(c *Char)` | manual review |
| `hitFallDamage` | `char.go:9109` | `(c *Char)` | hit/guard/target/juggle |
| `hitFallVel` | `char.go:9116` | `(c *Char)` | hit/guard/target/juggle |
| `hitFallSet` | `char.go:9128` | `(c *Char)` | hit/guard/target/juggle |
| `remapPal` | `char.go:9143` | `(c *Char)` | manual review |
| `forceRemapPal` | `char.go:9229` | `(c *Char)` | manual review |
| `getDrawPal` | `char.go:9266` | `(c *Char)` | manual review |
| `drawPal` | `char.go:9275` | `(c *Char)` | manual review |
| `remapSprite` | `char.go:9286` | `(c *Char)` | manual review |
| `remapSpritePreset` | `char.go:9296` | `(c *Char)` | manual review |
| `mapSet` | `char.go:9309` | `(c *Char)` | manual review |
| `mapReset` | `char.go:9368` | `(c *Char)` | manual review |
| `appendDialogue` | `char.go:9427` | `(c *Char)` | manual review |
| `appendToClipboard` | `char.go:9434` | `(c *Char)` | manual review |
| `inGuardState` | `char.go:9450` | `(c *Char)` | hit/guard/target/juggle |
| `gravity` | `char.go:9455` | `(c *Char)` | manual review |
| `getStandFriction` | `char.go:9459` | `(c *Char)` | manual review |
| `getCrouchFriction` | `char.go:9466` | `(c *Char)` | manual review |
| `checkCornerPush` | `char.go:9474` | `(c *Char)` | manual review |
| `posUpdate` | `char.go:9549` | `(c *Char)` | manual review |
| `hasTargetOfHitdef` | `char.go:9640` | `(c *Char)` | hit/guard/target/juggle |
| `targetAddSctrl` | `char.go:9649` | `(c *Char)` | hit/guard/target/juggle |
| `setBindTime` | `char.go:9665` | `(c *Char)` | manual review |
| `setBindToId` | `char.go:9673` | `(c *Char)` | manual review |
| `trackableByCamera` | `char.go:9759` | `(c *Char)` | manual review |
| `xScreenBound` | `char.go:9763` | `(c *Char)` | manual review |
| `zDepthBound` | `char.go:9785` | `(c *Char)` | manual review |
| `xPlatformBound` | `char.go:9800` | `(c *Char)` | manual review |
| `gethitBindClear` | `char.go:9813` | `(c *Char)` | hit/guard/target/juggle |
| `dropTargets` | `char.go:9820` | `(c *Char)` | hit/guard/target/juggle |
| `removeTarget` | `char.go:9845` | `(c *Char)` | hit/guard/target/juggle |
| `exitTarget` | `char.go:9855` | `(c *Char)` | hit/guard/target/juggle |
| `offsetX` | `char.go:9873` | `(c *Char)` | manual review |
| `offsetY` | `char.go:9877` | `(c *Char)` | manual review |
| `flattenClsnProxies` | `char.go:9882` | `(c *Char)` | manual review |
| `sizeToBox` | `char.go:9917` | `(c *Char)` | manual review |
| `getAnySizeBox` | `char.go:9934` | `(c *Char)` | manual review |
| `getClsn` | `char.go:9943` | `(c *Char)` | manual review |
| `projClsnCheck` | `char.go:10025` | `(c *Char)` | manual review |
| `projClsnCheckSingle` | `char.go:10054` | `(c *Char)` | manual review |
| `projClsnOverlapTrigger` | `char.go:10118` | `(c *Char)` | manual review |
| `clsnCheck` | `char.go:10132` | `(c *Char)` | manual review |
| `clsnCheckSingle` | `char.go:10172` | `(c *Char)` | manual review |
| `hitByAttrTrigger` | `char.go:10237` | `(c *Char)` | hit/guard/target/juggle |
| `checkHitBySlot` | `char.go:10251` | `(c *Char)` | hit/guard/target/juggle |
| `checkHitByAllSlots` | `char.go:10279` | `(c *Char)` | hit/guard/target/juggle |
| `attrCheck` | `char.go:10314` | `(c *Char)` | manual review |
| `hittableByChar` | `char.go:10421` | `(c *Char)` | hit/guard/target/juggle |
| `hitResultCheck` | `char.go:10507` | `(c *Char)` | hit/guard/target/juggle |
| `actionPrepare` | `char.go:11417` | `(c *Char)` | state machine |
| `actionRun` | `char.go:11611` | `(c *Char)` | state machine |
| `actionFinish` | `char.go:11872` | `(c *Char)` | state machine |
| `track` | `char.go:11927` | `(c *Char)` | manual review |
| `cueDebugDraw` | `char.go:12311` | `(c *Char)` | manual review |
| `cueDraw` | `char.go:12508` | `(c *Char)` | manual review |
| `delete` | `char.go:12815` | `(cl *CharList)` | manual review |
| `replace` | `char.go:12846` | `(cl *CharList)` | manual review |
| `commandUpdate` | `char.go:12867` | `(cl *CharList)` | manual review |
| `updateRunOrder` | `char.go:12949` | `(cl *CharList)` | manual review |
| `xScreenBound` | `char.go:13031` | `(cl *CharList)` | manual review |
| `hitDetectionPlayer` | `char.go:13061` | `(cl *CharList)` | hit/guard/target/juggle |
| `hitDetectionProjectile` | `char.go:13288` | `(cl *CharList)` | hit/guard/target/juggle |
| `pushDetection` | `char.go:13475` | `(cl *CharList)` | manual review |
| `collisionDetection` | `char.go:13728` | `(cl *CharList)` | manual review |
| `cueDraw` | `char.go:13791` | `(cl *CharList)` | manual review |
| `enemyNear` | `char.go:13814` | `(cl *CharList)` | manual review |
| `newCharCompiler` | `compiler.go:35` | `` | expression/controller compiler |
| `tokenizer` | `compiler.go:485` | `(c *CharCompiler)` | expression/controller compiler |
| `tokenizerCS` | `compiler.go:490` | `(*CharCompiler)` | expression/controller compiler |
| `isOperator` | `compiler.go:629` | `(*CharCompiler)` | expression/controller compiler |
| `operator` | `compiler.go:659` | `(c *CharCompiler)` | expression/controller compiler |
| `integer2` | `compiler.go:676` | `(c *CharCompiler)` | expression/controller compiler |
| `number` | `compiler.go:696` | `(c *CharCompiler)` | expression/controller compiler |
| `trgAttr` | `compiler.go:809` | `(c *CharCompiler)` | expression/controller compiler |
| `checkOpeningParenthesis` | `compiler.go:884` | `(c *CharCompiler)` | expression/controller compiler |
| `checkOpeningParenthesisCS` | `compiler.go:893` | `(c *CharCompiler)` | expression/controller compiler |
| `checkClosingParenthesis` | `compiler.go:901` | `(c *CharCompiler)` | expression/controller compiler |
| `checkEquality` | `compiler.go:909` | `(c *CharCompiler)` | expression/controller compiler |
| `intRange` | `compiler.go:931` | `(c *CharCompiler)` | expression/controller compiler |
| `compareValues` | `compiler.go:1001` | `(c *CharCompiler)` | expression/controller compiler |
| `readOldProjectileID` | `compiler.go:1209` | `(c *CharCompiler)` | expression/controller compiler |
| `parseOldAnimElemStyle` | `compiler.go:1254` | `(c *CharCompiler)` | expression/controller compiler |
| `contiguousOperator` | `compiler.go:5284` | `(c *CharCompiler)` | expression/controller compiler |
| `expPostNot` | `compiler.go:5300` | `(c *CharCompiler)` | expression/controller compiler |
| `expPow` | `compiler.go:5350` | `(c *CharCompiler)` | expression/controller compiler |
| `expMldv` | `compiler.go:5382` | `(c *CharCompiler)` | expression/controller compiler |
| `expAdsb` | `compiler.go:5410` | `(c *CharCompiler)` | expression/controller compiler |
| `expGrls` | `compiler.go:5435` | `(c *CharCompiler)` | expression/controller compiler |
| `expEqne` | `compiler.go:5556` | `(c *CharCompiler)` | expression/controller compiler |
| `expAnd` | `compiler.go:5641` | `(c *CharCompiler)` | expression/controller compiler |
| `expXor` | `compiler.go:5645` | `(c *CharCompiler)` | expression/controller compiler |
| `expOr` | `compiler.go:5649` | `(c *CharCompiler)` | expression/controller compiler |
| `expBoolAnd` | `compiler.go:5653` | `(c *CharCompiler)` | expression/controller compiler |
| `expBoolXor` | `compiler.go:5693` | `(c *CharCompiler)` | expression/controller compiler |
| `expBoolOr` | `compiler.go:5697` | `(c *CharCompiler)` | expression/controller compiler |
| `argExpression` | `compiler.go:5760` | `(c *CharCompiler)` | expression/controller compiler |
| `fullExpression` | `compiler.go:5779` | `(c *CharCompiler)` | expression/controller compiler |
| `parseTriggerNumber` | `compiler.go:5790` | `` | expression/controller compiler |
| `parseSection` | `compiler.go:5808` | `(c *CharCompiler)` | expression/controller compiler |
| `stateSec` | `compiler.go:5991` | `(c *CharCompiler)` | expression/controller compiler |
| `stateParam` | `compiler.go:6010` | `(c *CharCompiler)` | expression/controller compiler |
| `getDataPrefix` | `compiler.go:6024` | `(c *CharCompiler)` | expression/controller compiler |
| `paramAnimtype` | `compiler.go:6129` | `(c *CharCompiler)` | expression/controller compiler |
| `paramHittype` | `compiler.go:6181` | `(c *CharCompiler)` | expression/controller compiler |
| `paramPostype` | `compiler.go:6223` | `(c *CharCompiler)` | expression/controller compiler |
| `paramSpace` | `compiler.go:6279` | `(c *CharCompiler)` | expression/controller compiler |
| `paramProjection` | `compiler.go:6303` | `(c *CharCompiler)` | expression/controller compiler |
| `paramSaveData` | `compiler.go:6323` | `(c *CharCompiler)` | expression/controller compiler |
| `paramTrans` | `compiler.go:6345` | `(c *CharCompiler)` | expression/controller compiler |
| `paramClsnType` | `compiler.go:6457` | `(c *CharCompiler)` | expression/controller compiler |
| `paramStringList` | `compiler.go:6480` | `(c *CharCompiler)` | expression/controller compiler |
| `stateDef` | `compiler.go:6507` | `(c *CharCompiler)` | expression/controller compiler |
| `cnsStringArray` | `compiler.go:6642` | `` | expression/controller compiler |
| `stateCompileCNS` | `compiler.go:6729` | `(c *CharCompiler)` | expression/controller compiler |
| `wrongClosureToken` | `compiler.go:7052` | `(c *CharCompiler)` | expression/controller compiler |
| `nextLine` | `compiler.go:7059` | `(c *CharCompiler)` | expression/controller compiler |
| `scan` | `compiler.go:7068` | `(c *CharCompiler)` | expression/controller compiler |
| `needToken` | `compiler.go:7085` | `(c *CharCompiler)` | expression/controller compiler |
| `readString` | `compiler.go:7095` | `(c *CharCompiler)` | expression/controller compiler |
| `readSentenceLine` | `compiler.go:7105` | `(c *CharCompiler)` | expression/controller compiler |
| `readSentence` | `compiler.go:7140` | `(c *CharCompiler)` | expression/controller compiler |
| `statementEnd` | `compiler.go:7160` | `(c *CharCompiler)` | expression/controller compiler |
| `readKeyValue` | `compiler.go:7169` | `(c *CharCompiler)` | expression/controller compiler |
| `varNameCheck` | `compiler.go:7202` | `(c *CharCompiler)` | expression/controller compiler |
| `varNames` | `compiler.go:7214` | `(c *CharCompiler)` | expression/controller compiler |
| `inclNumVars` | `compiler.go:7246` | `(c *CharCompiler)` | expression/controller compiler |
| `scanI32` | `compiler.go:7254` | `(c *CharCompiler)` | expression/controller compiler |
| `scanStateDef` | `compiler.go:7266` | `(c *CharCompiler)` | expression/controller compiler |
| `callFunc` | `compiler.go:7610` | `(c *CharCompiler)` | expression/controller compiler |
| `stateCompileZSS` | `compiler.go:7900` | `(c *CharCompiler)` | expression/controller compiler |
| `charWarn` | `compiler.go:8320` | `(c *CharCompiler)` | expression/controller compiler |
| `hitBySub` | `compiler_functions.go:14` | `(c *CharCompiler)` | expression/controller compiler |
| `hitBy` | `compiler_functions.go:112` | `(c *CharCompiler)` | expression/controller compiler |
| `notHitBy` | `compiler_functions.go:122` | `(c *CharCompiler)` | expression/controller compiler |
| `assertSpecial` | `compiler_functions.go:132` | `(c *CharCompiler)` | expression/controller compiler |
| `playSnd` | `compiler_functions.go:357` | `(c *CharCompiler)` | expression/controller compiler |
| `changeState` | `compiler_functions.go:473` | `(c *CharCompiler)` | expression/controller compiler |
| `selfState` | `compiler_functions.go:480` | `(c *CharCompiler)` | expression/controller compiler |
| `tagIn` | `compiler_functions.go:487` | `(c *CharCompiler)` | expression/controller compiler |
| `tagOut` | `compiler_functions.go:526` | `(c *CharCompiler)` | expression/controller compiler |
| `destroySelf` | `compiler_functions.go:556` | `(c *CharCompiler)` | expression/controller compiler |
| `changeAnim` | `compiler_functions.go:615` | `(c *CharCompiler)` | expression/controller compiler |
| `changeAnim2` | `compiler_functions.go:622` | `(c *CharCompiler)` | expression/controller compiler |
| `helper` | `compiler_functions.go:629` | `(c *CharCompiler)` | expression/controller compiler |
| `ctrlSet` | `compiler_functions.go:840` | `(c *CharCompiler)` | expression/controller compiler |
| `explodSub` | `compiler_functions.go:851` | `(c *CharCompiler)` | expression/controller compiler |
| `explod` | `compiler_functions.go:1114` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyExplod` | `compiler_functions.go:1128` | `(c *CharCompiler)` | expression/controller compiler |
| `gameMakeAnim` | `compiler_functions.go:1151` | `(c *CharCompiler)` | expression/controller compiler |
| `posSet` | `compiler_functions.go:1204` | `(c *CharCompiler)` | expression/controller compiler |
| `posAdd` | `compiler_functions.go:1215` | `(c *CharCompiler)` | expression/controller compiler |
| `velSet` | `compiler_functions.go:1226` | `(c *CharCompiler)` | expression/controller compiler |
| `velAdd` | `compiler_functions.go:1237` | `(c *CharCompiler)` | expression/controller compiler |
| `velMul` | `compiler_functions.go:1248` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyShadow` | `compiler_functions.go:1259` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyReflection` | `compiler_functions.go:1341` | `(c *CharCompiler)` | expression/controller compiler |
| `palFX` | `compiler_functions.go:1526` | `(c *CharCompiler)` | expression/controller compiler |
| `allPalFX` | `compiler_functions.go:1537` | `(c *CharCompiler)` | expression/controller compiler |
| `bgPalFX` | `compiler_functions.go:1544` | `(c *CharCompiler)` | expression/controller compiler |
| `afterImageSub` | `compiler_functions.go:1562` | `(c *CharCompiler)` | expression/controller compiler |
| `afterImage` | `compiler_functions.go:1629` | `(c *CharCompiler)` | expression/controller compiler |
| `afterImageTime` | `compiler_functions.go:1636` | `(c *CharCompiler)` | expression/controller compiler |
| `parseHitFlag` | `compiler_functions.go:1665` | `(c *CharCompiler)` | expression/controller compiler |
| `hitDefSub` | `compiler_functions.go:1693` | `(c *CharCompiler)` | expression/controller compiler |
| `hitDef` | `compiler_functions.go:2254` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyHitDef` | `compiler_functions.go:2265` | `(c *CharCompiler)` | expression/controller compiler |
| `reversalDef` | `compiler_functions.go:2276` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyReversalDef` | `compiler_functions.go:2310` | `(c *CharCompiler)` | expression/controller compiler |
| `projectileSub` | `compiler_functions.go:2342` | `(c *CharCompiler)` | expression/controller compiler |
| `projectile` | `compiler_functions.go:2520` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyProjectile` | `compiler_functions.go:2530` | `(c *CharCompiler)` | expression/controller compiler |
| `width` | `compiler_functions.go:2553` | `(c *CharCompiler)` | expression/controller compiler |
| `sprPriority` | `compiler_functions.go:2589` | `(c *CharCompiler)` | expression/controller compiler |
| `varSetOlderSub` | `compiler_functions.go:2606` | `(c *CharCompiler)` | expression/controller compiler |
| `varSetNewerSub` | `compiler_functions.go:2676` | `(c *CharCompiler)` | expression/controller compiler |
| `varSetSub` | `compiler_functions.go:2749` | `(c *CharCompiler)` | expression/controller compiler |
| `varSet` | `compiler_functions.go:2813` | `(c *CharCompiler)` | expression/controller compiler |
| `varAdd` | `compiler_functions.go:2824` | `(c *CharCompiler)` | expression/controller compiler |
| `parentVarSet` | `compiler_functions.go:2835` | `(c *CharCompiler)` | expression/controller compiler |
| `parentVarAdd` | `compiler_functions.go:2846` | `(c *CharCompiler)` | expression/controller compiler |
| `rootVarSet` | `compiler_functions.go:2857` | `(c *CharCompiler)` | expression/controller compiler |
| `rootVarAdd` | `compiler_functions.go:2868` | `(c *CharCompiler)` | expression/controller compiler |
| `turn` | `compiler_functions.go:2879` | `(c *CharCompiler)` | expression/controller compiler |
| `targetFacing` | `compiler_functions.go:2891` | `(c *CharCompiler)` | expression/controller compiler |
| `targetBind` | `compiler_functions.go:2914` | `(c *CharCompiler)` | expression/controller compiler |
| `bindToTarget` | `compiler_functions.go:2941` | `(c *CharCompiler)` | expression/controller compiler |
| `targetLifeAdd` | `compiler_functions.go:3002` | `(c *CharCompiler)` | expression/controller compiler |
| `targetState` | `compiler_functions.go:3041` | `(c *CharCompiler)` | expression/controller compiler |
| `targetVelSet` | `compiler_functions.go:3064` | `(c *CharCompiler)` | expression/controller compiler |
| `targetVelAdd` | `compiler_functions.go:3095` | `(c *CharCompiler)` | expression/controller compiler |
| `targetPowerAdd` | `compiler_functions.go:3126` | `(c *CharCompiler)` | expression/controller compiler |
| `targetDrop` | `compiler_functions.go:3149` | `(c *CharCompiler)` | expression/controller compiler |
| `lifeAdd` | `compiler_functions.go:3168` | `(c *CharCompiler)` | expression/controller compiler |
| `lifeSet` | `compiler_functions.go:3191` | `(c *CharCompiler)` | expression/controller compiler |
| `powerAdd` | `compiler_functions.go:3202` | `(c *CharCompiler)` | expression/controller compiler |
| `powerSet` | `compiler_functions.go:3213` | `(c *CharCompiler)` | expression/controller compiler |
| `hitVelSet` | `compiler_functions.go:3224` | `(c *CharCompiler)` | expression/controller compiler |
| `screenBound` | `compiler_functions.go:3247` | `(c *CharCompiler)` | expression/controller compiler |
| `posFreeze` | `compiler_functions.go:3283` | `(c *CharCompiler)` | expression/controller compiler |
| `envShake` | `compiler_functions.go:3304` | `(c *CharCompiler)` | expression/controller compiler |
| `hitOverride` | `compiler_functions.go:3335` | `(c *CharCompiler)` | expression/controller compiler |
| `pause` | `compiler_functions.go:3390` | `(c *CharCompiler)` | expression/controller compiler |
| `superPause` | `compiler_functions.go:3417` | `(c *CharCompiler)` | expression/controller compiler |
| `trans` | `compiler_functions.go:3482` | `(c *CharCompiler)` | expression/controller compiler |
| `playerPush` | `compiler_functions.go:3496` | `(c *CharCompiler)` | expression/controller compiler |
| `stateTypeSet` | `compiler_functions.go:3544` | `(c *CharCompiler)` | expression/controller compiler |
| `angleDraw` | `compiler_functions.go:3631` | `(c *CharCompiler)` | expression/controller compiler |
| `angleSet` | `compiler_functions.go:3658` | `(c *CharCompiler)` | expression/controller compiler |
| `angleAdd` | `compiler_functions.go:3681` | `(c *CharCompiler)` | expression/controller compiler |
| `angleMul` | `compiler_functions.go:3704` | `(c *CharCompiler)` | expression/controller compiler |
| `envColor` | `compiler_functions.go:3727` | `(c *CharCompiler)` | expression/controller compiler |
| `displayToClipboard` | `compiler_functions.go:3799` | `(c *CharCompiler)` | expression/controller compiler |
| `appendToClipboard` | `compiler_functions.go:3806` | `(c *CharCompiler)` | expression/controller compiler |
| `clearClipboard` | `compiler_functions.go:3813` | `(c *CharCompiler)` | expression/controller compiler |
| `makeDust` | `compiler_functions.go:3825` | `(c *CharCompiler)` | expression/controller compiler |
| `attackDist` | `compiler_functions.go:3850` | `(c *CharCompiler)` | expression/controller compiler |
| `attackMulSet` | `compiler_functions.go:3877` | `(c *CharCompiler)` | expression/controller compiler |
| `defenceMulSet` | `compiler_functions.go:3922` | `(c *CharCompiler)` | expression/controller compiler |
| `fallEnvShake` | `compiler_functions.go:3963` | `(c *CharCompiler)` | expression/controller compiler |
| `hitFallDamage` | `compiler_functions.go:3975` | `(c *CharCompiler)` | expression/controller compiler |
| `hitFallVel` | `compiler_functions.go:3987` | `(c *CharCompiler)` | expression/controller compiler |
| `hitFallSet` | `compiler_functions.go:3999` | `(c *CharCompiler)` | expression/controller compiler |
| `varRangeSet` | `compiler_functions.go:4028` | `(c *CharCompiler)` | expression/controller compiler |
| `remapPal` | `compiler_functions.go:4065` | `(c *CharCompiler)` | expression/controller compiler |
| `stopSnd` | `compiler_functions.go:4084` | `(c *CharCompiler)` | expression/controller compiler |
| `sndPan` | `compiler_functions.go:4099` | `(c *CharCompiler)` | expression/controller compiler |
| `varRandom` | `compiler_functions.go:4122` | `(c *CharCompiler)` | expression/controller compiler |
| `gravity` | `compiler_functions.go:4141` | `(c *CharCompiler)` | expression/controller compiler |
| `bindToParent` | `compiler_functions.go:4174` | `(c *CharCompiler)` | expression/controller compiler |
| `bindToRoot` | `compiler_functions.go:4181` | `(c *CharCompiler)` | expression/controller compiler |
| `removeExplod` | `compiler_functions.go:4188` | `(c *CharCompiler)` | expression/controller compiler |
| `explodBindTime` | `compiler_functions.go:4214` | `(c *CharCompiler)` | expression/controller compiler |
| `moveHitReset` | `compiler_functions.go:4242` | `(c *CharCompiler)` | expression/controller compiler |
| `hitAdd` | `compiler_functions.go:4254` | `(c *CharCompiler)` | expression/controller compiler |
| `offset` | `compiler_functions.go:4269` | `(c *CharCompiler)` | expression/controller compiler |
| `victoryQuote` | `compiler_functions.go:4288` | `(c *CharCompiler)` | expression/controller compiler |
| `zoom` | `compiler_functions.go:4303` | `(c *CharCompiler)` | expression/controller compiler |
| `forceFeedback` | `compiler_functions.go:4338` | `(c *CharCompiler)` | expression/controller compiler |
| `assertAnalogVector` | `compiler_functions.go:4398` | `(c *CharCompiler)` | expression/controller compiler |
| `assertInput` | `compiler_functions.go:4433` | `(c *CharCompiler)` | expression/controller compiler |
| `changeMovelist` | `compiler_functions.go:4504` | `(c *CharCompiler)` | expression/controller compiler |
| `dialogue` | `compiler_functions.go:4519` | `(c *CharCompiler)` | expression/controller compiler |
| `dizzyPointsAdd` | `compiler_functions.go:4561` | `(c *CharCompiler)` | expression/controller compiler |
| `dizzyPointsSet` | `compiler_functions.go:4580` | `(c *CharCompiler)` | expression/controller compiler |
| `dizzySet` | `compiler_functions.go:4591` | `(c *CharCompiler)` | expression/controller compiler |
| `guardBreakSet` | `compiler_functions.go:4602` | `(c *CharCompiler)` | expression/controller compiler |
| `guardPointsAdd` | `compiler_functions.go:4613` | `(c *CharCompiler)` | expression/controller compiler |
| `guardPointsSet` | `compiler_functions.go:4632` | `(c *CharCompiler)` | expression/controller compiler |
| `lifebarAction` | `compiler_functions.go:4643` | `(c *CharCompiler)` | expression/controller compiler |
| `loadState` | `compiler_functions.go:4715` | `(c *CharCompiler)` | expression/controller compiler |
| `mapSetSub` | `compiler_functions.go:4724` | `(c *CharCompiler)` | expression/controller compiler |
| `mapSet` | `compiler_functions.go:4851` | `(c *CharCompiler)` | expression/controller compiler |
| `mapAdd` | `compiler_functions.go:4863` | `(c *CharCompiler)` | expression/controller compiler |
| `parentMapSet` | `compiler_functions.go:4875` | `(c *CharCompiler)` | expression/controller compiler |
| `parentMapAdd` | `compiler_functions.go:4887` | `(c *CharCompiler)` | expression/controller compiler |
| `rootMapSet` | `compiler_functions.go:4899` | `(c *CharCompiler)` | expression/controller compiler |
| `rootMapAdd` | `compiler_functions.go:4911` | `(c *CharCompiler)` | expression/controller compiler |
| `teamMapSet` | `compiler_functions.go:4923` | `(c *CharCompiler)` | expression/controller compiler |
| `teamMapAdd` | `compiler_functions.go:4935` | `(c *CharCompiler)` | expression/controller compiler |
| `mapReset` | `compiler_functions.go:4947` | `(c *CharCompiler)` | expression/controller compiler |
| `matchRestart` | `compiler_functions.go:4983` | `(c *CharCompiler)` | expression/controller compiler |
| `playBgm` | `compiler_functions.go:5123` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyBGCtrl` | `compiler_functions.go:5208` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyBGCtrl3d` | `compiler_functions.go:5283` | `(c *CharCompiler)` | expression/controller compiler |
| `modifySnd` | `compiler_functions.go:5302` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyBgm` | `compiler_functions.go:5369` | `(c *CharCompiler)` | expression/controller compiler |
| `printToConsole` | `compiler_functions.go:5396` | `(c *CharCompiler)` | expression/controller compiler |
| `redLifeAdd` | `compiler_functions.go:5403` | `(c *CharCompiler)` | expression/controller compiler |
| `redLifeSet` | `compiler_functions.go:5422` | `(c *CharCompiler)` | expression/controller compiler |
| `remapSprite` | `compiler_functions.go:5433` | `(c *CharCompiler)` | expression/controller compiler |
| `roundTimeAdd` | `compiler_functions.go:5465` | `(c *CharCompiler)` | expression/controller compiler |
| `roundTimeSet` | `compiler_functions.go:5480` | `(c *CharCompiler)` | expression/controller compiler |
| `saveLoadFileSub` | `compiler_functions.go:5491` | `(c *CharCompiler)` | expression/controller compiler |
| `saveFile` | `compiler_functions.go:5516` | `(c *CharCompiler)` | expression/controller compiler |
| `loadFile` | `compiler_functions.go:5523` | `(c *CharCompiler)` | expression/controller compiler |
| `saveState` | `compiler_functions.go:5530` | `(c *CharCompiler)` | expression/controller compiler |
| `scoreAdd` | `compiler_functions.go:5538` | `(c *CharCompiler)` | expression/controller compiler |
| `shaderSet` | `compiler_functions.go:5552` | `(c *CharCompiler)` | expression/controller compiler |
| `storyboard` | `compiler_functions.go:5567` | `(c *CharCompiler)` | expression/controller compiler |
| `targetDizzyPointsAdd` | `compiler_functions.go:5583` | `(c *CharCompiler)` | expression/controller compiler |
| `targetGuardPointsAdd` | `compiler_functions.go:5606` | `(c *CharCompiler)` | expression/controller compiler |
| `targetRedLifeAdd` | `compiler_functions.go:5629` | `(c *CharCompiler)` | expression/controller compiler |
| `targetScoreAdd` | `compiler_functions.go:5652` | `(c *CharCompiler)` | expression/controller compiler |
| `textSub` | `compiler_functions.go:5671` | `(c *CharCompiler)` | expression/controller compiler |
| `text` | `compiler_functions.go:5807` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyText` | `compiler_functions.go:5817` | `(c *CharCompiler)` | expression/controller compiler |
| `removeText` | `compiler_functions.go:5832` | `(c *CharCompiler)` | expression/controller compiler |
| `createPlatform` | `compiler_functions.go:5859` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyStageVar` | `compiler_functions.go:5933` | `(c *CharCompiler)` | expression/controller compiler |
| `cameraCtrl` | `compiler_functions.go:6229` | `(c *CharCompiler)` | expression/controller compiler |
| `height` | `compiler_functions.go:6262` | `(c *CharCompiler)` | expression/controller compiler |
| `depth` | `compiler_functions.go:6277` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyPlayer` | `compiler_functions.go:6313` | `(c *CharCompiler)` | expression/controller compiler |
| `assertCommand` | `compiler_functions.go:6423` | `(c *CharCompiler)` | expression/controller compiler |
| `getHitVarSet` | `compiler_functions.go:6447` | `(c *CharCompiler)` | expression/controller compiler |
| `groundLevelOffset` | `compiler_functions.go:6638` | `(c *CharCompiler)` | expression/controller compiler |
| `targetAdd` | `compiler_functions.go:6653` | `(c *CharCompiler)` | expression/controller compiler |
| `transformClsn` | `compiler_functions.go:6668` | `(c *CharCompiler)` | expression/controller compiler |
| `transformSprite` | `compiler_functions.go:6695` | `(c *CharCompiler)` | expression/controller compiler |
| `modifyStageBG` | `compiler_functions.go:6735` | `(c *CharCompiler)` | expression/controller compiler |
| `shaderSub` | `compiler_functions.go:6873` | `(c *CharCompiler)` | expression/controller compiler |
| `shiftInput` | `compiler_functions.go:6923` | `(c *CharCompiler)` | expression/controller compiler |
| `overrideClsn` | `compiler_functions.go:7000` | `(c *CharCompiler)` | expression/controller compiler |
| `null` | `compiler_functions.go:7023` | `(c *CharCompiler)` | expression/controller compiler |
| `newPalFXDef` | `image.go:47` | `` | palette/palfx/render resource |
| `newPalFX` | `image.go:77` | `` | palette/palfx/render resource |
| `clearWithNeg` | `image.go:81` | `(pf *PalFX)` | palette/palfx/render resource |
| `getSynFx` | `image.go:93` | `(pf *PalFX)` | palette/palfx/render resource |
| `getFxPal` | `image.go:117` | `(pf *PalFX)` | palette/palfx/render resource |
| `getFinalPalFx` | `image.go:169` | `(pf *PalFX)` | palette/palfx/render resource |
| `sinAdd` | `image.go:204` | `(pf *PalFX)` | palette/palfx/render resource |
| `sinMul` | `image.go:217` | `(pf *PalFX)` | palette/palfx/render resource |
| `sinColor` | `image.go:230` | `(pf *PalFX)` | palette/palfx/render resource |
| `sinHueshift` | `image.go:243` | `(pf *PalFX)` | palette/palfx/render resource |
| `interpolationUpdate` | `image.go:256` | `(pf *PalFX)` | palette/palfx/render resource |
| `synthesize` | `image.go:309` | `(pf *PalFX)` | palette/palfx/render resource |
| `setColor` | `image.go:368` | `(pf *PalFX)` | palette/palfx/render resource |
| `newPaldata` | `image.go:387` | `` | palette/palfx/render resource |
| `init` | `image.go:405` | `(pl *PaletteList)` | palette/palfx/render resource |
| `SetSource` | `image.go:413` | `(pl *PaletteList)` | palette/palfx/render resource |
| `NewPal` | `image.go:434` | `(pl *PaletteList)` | palette/palfx/render resource |
| `ResetRemap` | `image.go:454` | `(pl *PaletteList)` | palette/palfx/render resource |
| `GetPalMap` | `image.go:460` | `(pl *PaletteList)` | palette/palfx/render resource |
| `SwapPalMap` | `image.go:466` | `(pl *PaletteList)` | palette/palfx/render resource |
| `SelectablePalIndex` | `image.go:476` | `(pl *PaletteList)` | palette/palfx/render resource |
| `Pal32ToBytes` | `image.go:489` | `` | palette/palfx/render resource |
| `NewTextureFromPalette` | `image.go:507` | `` | palette/palfx/render resource |
| `readActPalette` | `image.go:521` | `` | palette/palfx/render resource |
| `isBlank` | `image.go:583` | `(s *Sprite)` | palette/palfx/render resource |
| `newSprite` | `image.go:587` | `` | palette/palfx/render resource |
| `loadFromSff` | `image.go:592` | `` | palette/palfx/render resource |
| `shareCopy` | `image.go:718` | `(s *Sprite)` | palette/palfx/render resource |
| `GetPal` | `image.go:739` | `(s *Sprite)` | palette/palfx/render resource |
| `GetPalTex` | `image.go:749` | `(s *Sprite)` | palette/palfx/render resource |
| `SetPxl` | `image.go:774` | `(s *Sprite)` | palette/palfx/render resource |
| `SetRaw` | `image.go:787` | `(s *Sprite)` | palette/palfx/render resource |
| `readHeader` | `image.go:794` | `(s *Sprite)` | palette/palfx/render resource |
| `readPcxHeader` | `image.go:819` | `(s *Sprite)` | palette/palfx/render resource |
| `RlePcxDecode` | `image.go:861` | `(s *Sprite)` | palette/palfx/render resource |
| `Rle8Decode` | `image.go:1027` | `(s *Sprite)` | palette/palfx/render resource |
| `Rle5Decode` | `image.go:1055` | `(s *Sprite)` | palette/palfx/render resource |
| `Lz5Decode` | `image.go:1099` | `(s *Sprite)` | palette/palfx/render resource |
| `readV2` | `image.go:1176` | `(s *Sprite)` | palette/palfx/render resource |
| `CachePalTex` | `image.go:1264` | `(s *Sprite)` | palette/palfx/render resource |
| `Draw` | `image.go:1290` | `(s *Sprite)` | palette/palfx/render resource |
| `newSff` | `image.go:1438` | `` | palette/palfx/render resource |
| `removeSFFCache` | `image.go:1457` | `` | palette/palfx/render resource |
| `findActiveSff` | `image.go:1465` | `` | palette/palfx/render resource |
| `loadSff` | `image.go:1494` | `` | palette/palfx/render resource |
| `preloadSff` | `image.go:1604` | `` | palette/palfx/render resource |
| `loadPalettes` | `image.go:1895` | `(s *Sff)` | palette/palfx/render resource |
| `ReadPalette` | `image.go:1963` | `(s *Sff)` | palette/palfx/render resource |
| `loadActPalettes` | `image.go:2019` | `(s *Sff)` | palette/palfx/render resource |
| `GetSprite` | `image.go:2060` | `(s *Sff)` | palette/palfx/render resource |
| `cloneSpriteWithPal` | `image.go:2068` | `(s *Sff)` | palette/palfx/render resource |
| `captureScreen` | `image.go:2080` | `` | palette/palfx/render resource |
| `IsDirectionPress` | `input.go:54` | `(ck CommandStepKey)` | command/input |
| `IsDirectionRelease` | `input.go:58` | `(ck CommandStepKey)` | command/input |
| `IsButtonPress` | `input.go:62` | `(ck CommandStepKey)` | command/input |
| `IsButtonRelease` | `input.go:66` | `(ck CommandStepKey)` | command/input |
| `NewShortcutKey` | `input.go:82` | `` | command/input |
| `Test` | `input.go:94` | `(sk ShortcutKey)` | command/input |
| `OnKeyReleased` | `input.go:113` | `` | command/input |
| `OnKeyPressed` | `input.go:124` | `` | command/input |
| `OnTextEntered` | `input.go:162` | `` | command/input |
| `GetKeyboardState` | `input.go:167` | `` | command/input |
| `GetJoystickState` | `input.go:197` | `` | command/input |
| `set` | `input.go:286` | `(kc *KeyConfig)` | command/input |
| `KeysToBits` | `input.go:377` | `(ibit *InputBits)` | command/input |
| `BitsToKeys` | `input.go:395` | `(ibit InputBits)` | command/input |
| `NewCommandKeyRemap` | `input.go:421` | `` | command/input |
| `NewInputReader` | `input.go:431` | `` | command/input |
| `LocalInput` | `input.go:439` | `(ir *InputReader)` | command/input |
| `LocalAnalogInput` | `input.go:478` | `(ir *InputReader)` | command/input |
| `SocdResolution` | `input.go:496` | `(ir *InputReader)` | command/input |
| `ButtonAssistCheck` | `input.go:633` | `(ir *InputReader)` | command/input |
| `NewInputBuffer` | `input.go:680` | `` | command/input |
| `updateInputTime` | `input.go:695` | `(ib *InputBuffer)` | command/input |
| `LastChangeTime` | `input.go:1779` | `(__ *InputBuffer)` | command/input |
| `Buttons` | `input.go:1866` | `(ai *AiInput)` | command/input |
| `U` | `input.go:1921` | `(ai *AiInput)` | command/input |
| `D` | `input.go:1922` | `(ai *AiInput)` | command/input |
| `L` | `input.go:1923` | `(ai *AiInput)` | command/input |
| `R` | `input.go:1924` | `(ai *AiInput)` | command/input |
| `a` | `input.go:1925` | `(ai *AiInput)` | command/input |
| `b` | `input.go:1926` | `(ai *AiInput)` | command/input |
| `c` | `input.go:1927` | `(ai *AiInput)` | command/input |
| `x` | `input.go:1928` | `(ai *AiInput)` | command/input |
| `y` | `input.go:1929` | `(ai *AiInput)` | command/input |
| `z` | `input.go:1930` | `(ai *AiInput)` | command/input |
| `s` | `input.go:1931` | `(ai *AiInput)` | command/input |
| `d` | `input.go:1932` | `(ai *AiInput)` | command/input |
| `w` | `input.go:1933` | `(ai *AiInput)` | command/input |
| `m` | `input.go:1934` | `(ai *AiInput)` | command/input |
| `IsSingleDirection` | `input.go:1945` | `(cs *CommandStep)` | command/input |
| `EqualSteps` | `input.go:1993` | `(cs CommandStep)` | command/input |
| `newCommand` | `input.go:2025` | `` | command/input |
| `ReadCommandSymbols` | `input.go:2030` | `(c *Command)` | command/input |
| `AutoGreaterExpand` | `input.go:2307` | `(c *Command)` | command/input |
| `NewCommandList` | `input.go:2556` | `` | command/input |
| `AddCommand` | `input.go:2571` | `(cl *CommandList)` | command/input |
| `InputUpdate` | `input.go:2600` | `(cl *CommandList)` | command/input |
| `NormalizeAxes` | `input.go:2778` | `` | command/input |
| `Assert` | `input.go:2792` | `(cl *CommandList)` | command/input |
| `ClearName` | `input.go:2811` | `(cl *CommandList)` | command/input |
| `BufReset` | `input.go:2849` | `(cl *CommandList)` | command/input |
| `At` | `input.go:2881` | `(cl *CommandList)` | command/input |
| `GetState` | `input.go:2898` | `(cl *CommandList)` | command/input |
| `CopyList` | `input.go:2909` | `(cl *CommandList)` | command/input |
| `IsControllerButtonPressed` | `input.go:2925` | `(cl *CommandList)` | command/input |
| `withoutTildeKey` | `input.go:2982` | `` | command/input |
| `autoGenerateExtendedCommand` | `input.go:2997` | `` | command/input |
| `NewNormalizer` | `sound.go:38` | `` | sound/presentation event |
| `Stream` | `sound.go:44` | `(n *Normalizer)` | sound/presentation event |
| `Err` | `sound.go:71` | `(n *Normalizer)` | sound/presentation event |
| `process` | `sound.go:82` | `(n *NormalizerLR)` | sound/presentation event |
| `WithSpeakerLock` | `sound.go:104` | `` | sound/presentation event |
| `newSwapSeeker` | `sound.go:118` | `` | sound/presentation event |
| `Stream` | `sound.go:142` | `(sw *SwapSeeker)` | sound/presentation event |
| `Seek` | `sound.go:149` | `(sw *SwapSeeker)` | sound/presentation event |
| `Position` | `sound.go:157` | `(sw *SwapSeeker)` | sound/presentation event |
| `Len` | `sound.go:164` | `(sw *SwapSeeker)` | sound/presentation event |
| `Err` | `sound.go:171` | `(sw *SwapSeeker)` | sound/presentation event |
| `newBufferSeeker` | `sound.go:187` | `` | sound/presentation event |
| `Stream` | `sound.go:194` | `(b *BufferSeeker)` | sound/presentation event |
| `Seek` | `sound.go:201` | `(b *BufferSeeker)` | sound/presentation event |
| `Position` | `sound.go:214` | `(b *BufferSeeker)` | sound/presentation event |
| `Len` | `sound.go:216` | `(b *BufferSeeker)` | sound/presentation event |
| `Err` | `sound.go:218` | `(b *BufferSeeker)` | sound/presentation event |
| `newStreamLooper` | `sound.go:232` | `` | sound/presentation event |
| `Stream` | `sound.go:249` | `(l *StreamLooper)` | sound/presentation event |
| `Err` | `sound.go:283` | `(b *StreamLooper)` | sound/presentation event |
| `Len` | `sound.go:287` | `(b *StreamLooper)` | sound/presentation event |
| `Position` | `sound.go:291` | `(b *StreamLooper)` | sound/presentation event |
| `Seek` | `sound.go:295` | `(b *StreamLooper)` | sound/presentation event |
| `newBgm` | `sound.go:319` | `` | sound/presentation event |
| `Stop` | `sound.go:323` | `(bgm *Bgm)` | sound/presentation event |
| `Open` | `sound.go:334` | `(bgm *Bgm)` | sound/presentation event |
| `loadSoundFont` | `sound.go:542` | `` | sound/presentation event |
| `SetPaused` | `sound.go:555` | `(bgm *Bgm)` | sound/presentation event |
| `UpdateVolume` | `sound.go:564` | `(bgm *Bgm)` | sound/presentation event |
| `SetFreqMul` | `sound.go:589` | `(bgm *Bgm)` | sound/presentation event |
| `OpenFromStreamer` | `sound.go:612` | `(bgm *Bgm)` | sound/presentation event |
| `SetLoopPoints` | `sound.go:660` | `(bgm *Bgm)` | sound/presentation event |
| `Seek` | `sound.go:683` | `(bgm *Bgm)` | sound/presentation event |
| `readSound` | `sound.go:706` | `` | sound/presentation event |
| `GetStreamer` | `sound.go:755` | `(s *Sound)` | sound/presentation event |
| `newSnd` | `sound.go:769` | `` | sound/presentation event |
| `findActiveSnd` | `sound.go:775` | `` | sound/presentation event |
| `LoadSnd` | `sound.go:793` | `` | sound/presentation event |
| `LoadSndFiltered` | `sound.go:810` | `` | sound/presentation event |
| `stop` | `sound.go:897` | `(s *Snd)` | sound/presentation event |
| `loadFromSnd` | `sound.go:902` | `` | sound/presentation event |
| `Stream` | `sound.go:930` | `(s *SoundEffect)` | sound/presentation event |
| `Err` | `sound.go:954` | `(s *SoundEffect)` | sound/presentation event |
| `IsPlaying` | `sound.go:1034` | `(s *SoundChannel)` | sound/presentation event |
| `SetPaused` | `sound.go:1038` | `(s *SoundChannel)` | sound/presentation event |
| `SetVolume` | `sound.go:1047` | `(s *SoundChannel)` | sound/presentation event |
| `SetPan` | `sound.go:1053` | `(s *SoundChannel)` | sound/presentation event |
| `SetPriority` | `sound.go:1061` | `(s *SoundChannel)` | sound/presentation event |
| `SetFreqMul` | `sound.go:1067` | `(s *SoundChannel)` | sound/presentation event |
| `SetLoopPoints` | `sound.go:1088` | `(s *SoundChannel)` | sound/presentation event |
| `newSoundChannels` | `sound.go:1117` | `` | sound/presentation event |
| `SetSize` | `sound.go:1124` | `(s *SoundChannels)` | sound/presentation event |
| `Request` | `sound.go:1151` | `(s *SoundChannels)` | sound/presentation event |
| `reserveChannel` | `sound.go:1249` | `(s *SoundChannels)` | sound/presentation event |
| `IsPlaying` | `sound.go:1296` | `(s *SoundChannels)` | sound/presentation event |
| `Stop` | `sound.go:1309` | `(s *SoundChannels)` | sound/presentation event |
| `StopAll` | `sound.go:1319` | `(s *SoundChannels)` | sound/presentation event |
| `newStageProps` | `stage.go:17` | `` | manual review |
| `newBackGround` | `stage.go:153` | `` | manual review |
| `changeAnim` | `stage.go:500` | `(bg *backGround)` | manual review |
| `newBgCtrl` | `stage.go:744` | `` | manual review |
| `xEnable` | `stage.go:863` | `(bgc *bgCtrl)` | manual review |
| `yEnable` | `stage.go:867` | `(bgc *bgCtrl)` | manual review |
| `si` | `stage.go:893` | `(s *Stage)` | manual review |
| `newStage` | `stage.go:945` | `` | manual review |
| `loadStage` | `stage.go:978` | `` | manual review |
| `getBg` | `stage.go:1550` | `(s *Stage)` | manual review |
| `get3DBg` | `stage.go:1561` | `(s *Stage)` | manual review |
| `get3DAnim` | `stage.go:1572` | `(s *Stage)` | manual review |
| `bgCtrlAction` | `stage.go:1584` | `(s *Stage)` | manual review |
| `runBgCtrl` | `stage.go:1613` | `(s *Stage)` | manual review |
| `paused` | `stage.go:1768` | `(s *Stage)` | manual review |
| `draw` | `stage.go:1882` | `(s *Stage)` | manual review |
| `destroy` | `stage.go:1958` | `(s *Stage)` | manual review |
| `warn` | `stage.go:1966` | `(s *Stage)` | manual review |
| `modifyBGCtrl3d` | `stage.go:2072` | `(s *Stage)` | manual review |
| `isRunningInsideAppBundle` | `system.go:360` | `` | round/system/entity lifecycle |
| `init` | `system.go:366` | `(s *System)` | round/system/entity lifecycle |
| `shutdown` | `system.go:524` | `(s *System)` | round/system/entity lifecycle |
| `getViewport` | `system.go:538` | `` | round/system/entity lifecycle |
| `middleOfMatch` | `system.go:558` | `(s *System)` | round/system/entity lifecycle |
| `skipMotifScaling` | `system.go:562` | `(s *System)` | round/system/entity lifecycle |
| `getFightAspect` | `system.go:572` | `(s *System)` | round/system/entity lifecycle |
| `getMotifAspect` | `system.go:592` | `(s *System)` | round/system/entity lifecycle |
| `getCurrentAspect` | `system.go:598` | `(s *System)` | round/system/entity lifecycle |
| `setGameSize` | `system.go:606` | `(s *System)` | round/system/entity lifecycle |
| `applyFightAspect` | `system.go:632` | `(s *System)` | round/system/entity lifecycle |
| `captureAspectState` | `system.go:672` | `(s *System)` | round/system/entity lifecycle |
| `restoreAspectState` | `system.go:681` | `(s *System)` | round/system/entity lifecycle |
| `wrapDrawWithAspectState` | `system.go:688` | `(s *System)` | round/system/entity lifecycle |
| `shouldPersistMotifAspect` | `system.go:701` | `(s *System)` | round/system/entity lifecycle |
| `enterMotifAspect` | `system.go:705` | `(s *System)` | round/system/entity lifecycle |
| `leaveMotifAspect` | `system.go:712` | `(s *System)` | round/system/entity lifecycle |
| `eventUpdate` | `system.go:719` | `(s *System)` | round/system/entity lifecycle |
| `runMainThreadTask` | `system.go:729` | `(s *System)` | round/system/entity lifecycle |
| `keepAlive` | `system.go:740` | `(s *System)` | round/system/entity lifecycle |
| `await` | `system.go:768` | `(s *System)` | round/system/entity lifecycle |
| `renderFrame` | `system.go:827` | `(s *System)` | round/system/entity lifecycle |
| `tickSound` | `system.go:917` | `(s *System)` | round/system/entity lifecycle |
| `restorePauseVolume` | `system.go:946` | `(s *System)` | round/system/entity lifecycle |
| `resetRemapInput` | `system.go:959` | `(s *System)` | round/system/entity lifecycle |
| `loaderReset` | `system.go:965` | `(s *System)` | round/system/entity lifecycle |
| `loadStart` | `system.go:970` | `(s *System)` | round/system/entity lifecycle |
| `synchronize` | `system.go:975` | `(s *System)` | round/system/entity lifecycle |
| `anyHardButton` | `system.go:985` | `(s *System)` | round/system/entity lifecycle |
| `anyHardButton` | `system.go:1003` | `(s *System)` | round/system/entity lifecycle |
| `anyButton` | `system.go:1028` | `(s *System)` | round/system/entity lifecycle |
| `buttonController` | `system.go:1041` | `(s *System)` | round/system/entity lifecycle |
| `stepCommandLists` | `system.go:1052` | `(s *System)` | round/system/entity lifecycle |
| `uiResetTokenGuard` | `system.go:1065` | `(s *System)` | round/system/entity lifecycle |
| `uiRepeatShouldFire` | `system.go:1071` | `(s *System)` | round/system/entity lifecycle |
| `uiControllerKey` | `system.go:1083` | `(s *System)` | round/system/entity lifecycle |
| `uiConsumeTokenThisFrame` | `system.go:1093` | `(s *System)` | round/system/entity lifecycle |
| `uiRawInput` | `system.go:1107` | `(s *System)` | round/system/entity lifecycle |
| `uiIsKeyAction` | `system.go:1223` | `(s *System)` | round/system/entity lifecycle |
| `uiDefaultTokens` | `system.go:1234` | `(s *System)` | round/system/entity lifecycle |
| `uiSetConfigDefaults` | `system.go:1258` | `(s *System)` | round/system/entity lifecycle |
| `uiRegisterCommand` | `system.go:1314` | `(s *System)` | round/system/entity lifecycle |
| `uiApplyCommandRegistry` | `system.go:1343` | `(s *System)` | round/system/entity lifecycle |
| `uiEnsureCommandLists` | `system.go:1358` | `(s *System)` | round/system/entity lifecycle |
| `netplay` | `system.go:1401` | `(s *System)` | round/system/entity lifecycle |
| `escExit` | `system.go:1405` | `(s *System)` | round/system/entity lifecycle |
| `matchOver` | `system.go:1420` | `(s *System)` | round/system/entity lifecycle |
| `anyChar` | `system.go:1426` | `(s *System)` | round/system/entity lifecycle |
| `playerID` | `system.go:1437` | `(s *System)` | round/system/entity lifecycle |
| `playerIDExist` | `system.go:1457` | `(s *System)` | round/system/entity lifecycle |
| `playerIndex` | `system.go:1465` | `(s *System)` | round/system/entity lifecycle |
| `playerIndexExist` | `system.go:1490` | `(s *System)` | round/system/entity lifecycle |
| `getCharRoot` | `system.go:1499` | `(s *System)` | round/system/entity lifecycle |
| `playerNoExist` | `system.go:1510` | `(s *System)` | round/system/entity lifecycle |
| `palfxvar` | `system.go:1519` | `(s *System)` | round/system/entity lifecycle |
| `palfxvar2` | `system.go:1555` | `(s *System)` | round/system/entity lifecycle |
| `screenHeight` | `system.go:1578` | `(s *System)` | round/system/entity lifecycle |
| `screenWidth` | `system.go:1583` | `(s *System)` | round/system/entity lifecycle |
| `roundEnded` | `system.go:1587` | `(s *System)` | round/system/entity lifecycle |
| `roundNoDamage` | `system.go:1592` | `(s *System)` | round/system/entity lifecycle |
| `gameTime` | `system.go:1597` | `(s *System)` | round/system/entity lifecycle |
| `roundState` | `system.go:1613` | `(s *System)` | round/system/entity lifecycle |
| `introState` | `system.go:1630` | `(s *System)` | round/system/entity lifecycle |
| `outroState` | `system.go:1656` | `(s *System)` | round/system/entity lifecycle |
| `roundOver` | `system.go:1682` | `(s *System)` | round/system/entity lifecycle |
| `roundStateTicks` | `system.go:1686` | `(s *System)` | round/system/entity lifecycle |
| `roundIsSingle` | `system.go:1691` | `(s *System)` | round/system/entity lifecycle |
| `maxDrawsReached` | `system.go:1695` | `(s *System)` | round/system/entity lifecycle |
| `roundIsFinal` | `system.go:1702` | `(s *System)` | round/system/entity lifecycle |
| `winnerTeam` | `system.go:1718` | `(s *System)` | round/system/entity lifecycle |
| `gsf` | `system.go:1736` | `(s *System)` | round/system/entity lifecycle |
| `setGSF` | `system.go:1740` | `(s *System)` | round/system/entity lifecycle |
| `unsetGSF` | `system.go:1744` | `(s *System)` | round/system/entity lifecycle |
| `appendToConsole` | `system.go:1748` | `(s *System)` | round/system/entity lifecycle |
| `printToConsole` | `system.go:1755` | `(s *System)` | round/system/entity lifecycle |
| `luaQueuePreDraw` | `system.go:1767` | `(s *System)` | round/system/entity lifecycle |
| `luaQueueLayerDraw` | `system.go:1774` | `(s *System)` | round/system/entity lifecycle |
| `luaFlushDrawQueue` | `system.go:1791` | `(s *System)` | round/system/entity lifecycle |
| `luaDiscardDrawQueue` | `system.go:1805` | `(s *System)` | round/system/entity lifecycle |
| `printBytecodeError` | `system.go:1815` | `(s *System)` | round/system/entity lifecycle |
| `loadTime` | `system.go:1825` | `(s *System)` | round/system/entity lifecycle |
| `updateZScale` | `system.go:1838` | `(s *System)` | round/system/entity lifecycle |
| `zEnabled` | `system.go:1853` | `(s *System)` | round/system/entity lifecycle |
| `drawposXYfromZ` | `system.go:1858` | `(s *System)` | round/system/entity lifecycle |
| `posZtoYoffset` | `system.go:1867` | `(s *System)` | round/system/entity lifecycle |
| `zAxisOverlap` | `system.go:1873` | `(s *System)` | round/system/entity lifecycle |
| `initPlayerID` | `system.go:1953` | `(s *System)` | round/system/entity lifecycle |
| `pruneCharId` | `system.go:1990` | `(s *System)` | round/system/entity lifecycle |
| `newCharId` | `system.go:2003` | `(s *System)` | round/system/entity lifecycle |
| `resetGblEffect` | `system.go:2043` | `(s *System)` | round/system/entity lifecycle |
| `clearAllCharSounds` | `system.go:2055` | `(s *System)` | round/system/entity lifecycle |
| `stopAllCharSounds` | `system.go:2062` | `(s *System)` | round/system/entity lifecycle |
| `softenAllSound` | `system.go:2068` | `(s *System)` | round/system/entity lifecycle |
| `restoreAllVolume` | `system.go:2090` | `(s *System)` | round/system/entity lifecycle |
| `clearMatchSound` | `system.go:2109` | `(s *System)` | round/system/entity lifecycle |
| `clearAllSound` | `system.go:2124` | `(s *System)` | round/system/entity lifecycle |
| `clearPlayerAssets` | `system.go:2131` | `(s *System)` | round/system/entity lifecycle |
| `handleStageSwap` | `system.go:2160` | `(s *System)` | round/system/entity lifecycle |
| `updateMusicMaps` | `system.go:2197` | `(s *System)` | round/system/entity lifecycle |
| `resetRound` | `system.go:2238` | `(s *System)` | round/system/entity lifecycle |
| `debugPaused` | `system.go:2346` | `(s *System)` | round/system/entity lifecycle |
| `tickFrame` | `system.go:2351` | `(s *System)` | round/system/entity lifecycle |
| `tickNextFrame` | `system.go:2357` | `(s *System)` | round/system/entity lifecycle |
| `tickInterpolation` | `system.go:2363` | `(s *System)` | round/system/entity lifecycle |
| `addFrameTime` | `system.go:2376` | `(s *System)` | round/system/entity lifecycle |
| `resetFrameTime` | `system.go:2396` | `(s *System)` | round/system/entity lifecycle |
| `resetMatchData` | `system.go:2404` | `(s *System)` | round/system/entity lifecycle |
| `charUpdate` | `system.go:2415` | `(s *System)` | round/system/entity lifecycle |
| `globalCollision` | `system.go:2427` | `(s *System)` | round/system/entity lifecycle |
| `posReset` | `system.go:2439` | `(s *System)` | round/system/entity lifecycle |
| `runIntroSkip` | `system.go:2449` | `(s *System)` | round/system/entity lifecycle |
| `clearSpriteData` | `system.go:2487` | `(s *System)` | round/system/entity lifecycle |
| `screenleft` | `system.go:2514` | `(s *System)` | round/system/entity lifecycle |
| `screenright` | `system.go:2518` | `(s *System)` | round/system/entity lifecycle |
| `projectileUpdate` | `system.go:2717` | `(s *System)` | round/system/entity lifecycle |
| `projectilePrune` | `system.go:2735` | `(s *System)` | round/system/entity lifecycle |
| `explodUpdate` | `system.go:2753` | `(s *System)` | round/system/entity lifecycle |
| `explodPrune` | `system.go:2808` | `(s *System)` | round/system/entity lifecycle |
| `charTextsUpdate` | `system.go:2824` | `(s *System)` | round/system/entity lifecycle |
| `charTextsPrune` | `system.go:2850` | `(s *System)` | round/system/entity lifecycle |
| `globalTick` | `system.go:2865` | `(s *System)` | round/system/entity lifecycle |
| `getSlowtime` | `system.go:2878` | `(s *System)` | round/system/entity lifecycle |
| `timeElapsed` | `system.go:2885` | `(s *System)` | round/system/entity lifecycle |
| `timeRemaining` | `system.go:2894` | `(s *System)` | round/system/entity lifecycle |
| `timeTotal` | `system.go:2901` | `(s *System)` | round/system/entity lifecycle |
| `matchEndDialoguePending` | `system.go:2912` | `(s *System)` | round/system/entity lifecycle |
| `shouldStartMatchEndDialogue` | `system.go:2932` | `(s *System)` | round/system/entity lifecycle |
| `holdPostMatchForDialogue` | `system.go:2942` | `(s *System)` | round/system/entity lifecycle |
| `roundEndDecision` | `system.go:3180` | `(s *System)` | round/system/entity lifecycle |
| `draw` | `system.go:3316` | `(s *System)` | round/system/entity lifecycle |
| `drawCharTexts` | `system.go:3477` | `(s *System)` | round/system/entity lifecycle |
| `drawTop` | `system.go:3485` | `(s *System)` | round/system/entity lifecycle |
| `drawDebugText` | `system.go:3515` | `(s *System)` | round/system/entity lifecycle |
| `runMatch` | `system.go:3618` | `(s *System)` | round/system/entity lifecycle |
| `SetupCharRoundStart` | `system.go:3824` | `(s *System)` | round/system/entity lifecycle |
| `runNextRound` | `system.go:3948` | `(s *System)` | round/system/entity lifecycle |
| `gameLogicSpeed` | `system.go:4012` | `(s *System)` | round/system/entity lifecycle |
| `gameRenderSpeed` | `system.go:4018` | `(s *System)` | round/system/entity lifecycle |
| `debugModeAllowed` | `system.go:4032` | `(s *System)` | round/system/entity lifecycle |
| `inRollback` | `system.go:4040` | `(s *System)` | round/system/entity lifecycle |
| `isSpeculativeFrame` | `system.go:4047` | `(s *System)` | round/system/entity lifecycle |
| `Save` | `system.go:4065` | `(bk *RoundStartBackup)` | round/system/entity lifecycle |
| `newSelectChar` | `system.go:4240` | `` | round/system/entity lifecycle |
| `newSelectStage` | `system.go:4263` | `` | round/system/entity lifecycle |
| `newSelect` | `system.go:4289` | `` | round/system/entity lifecycle |
| `GetCharNo` | `system.go:4304` | `(s *Select)` | round/system/entity lifecycle |
| `GetChar` | `system.go:4315` | `(s *Select)` | round/system/entity lifecycle |
| `ValidatePalette` | `system.go:4324` | `(s *Select)` | round/system/entity lifecycle |
| `SelectStage` | `system.go:4348` | `(s *Select)` | round/system/entity lifecycle |
| `GetStage` | `system.go:4350` | `(s *Select)` | round/system/entity lifecycle |
| `getDefaultDefPathInZip` | `system.go:4361` | `` | round/system/entity lifecycle |
| `AddChar` | `system.go:4368` | `(s *Select)` | round/system/entity lifecycle |
| `AddStage` | `system.go:4690` | `(s *Select)` | round/system/entity lifecycle |
| `AddSelectedChar` | `system.go:4889` | `(s *Select)` | round/system/entity lifecycle |
| `ClearSelected` | `system.go:4913` | `(s *Select)` | round/system/entity lifecycle |
| `newLoader` | `system.go:4938` | `` | round/system/entity lifecycle |
| `loadPlayerChar` | `system.go:4943` | `(l *Loader)` | round/system/entity lifecycle |
| `loadAttachedChar` | `system.go:4947` | `(l *Loader)` | round/system/entity lifecycle |
| `loadCharacter` | `system.go:4952` | `(l *Loader)` | round/system/entity lifecycle |
| `prepareTurnsFaces` | `system.go:5185` | `(l *Loader)` | round/system/entity lifecycle |
| `loadStage` | `system.go:5283` | `(l *Loader)` | round/system/entity lifecycle |
| `runTread` | `system.go:5472` | `(l *Loader)` | round/system/entity lifecycle |
| `setDefaultPhase` | `system.go:5501` | `(es *EnvShake)` | round/system/entity lifecycle |
| `getOffset` | `system.go:5524` | `(es *EnvShake)` | round/system/entity lifecycle |
| `apply` | `system.go:5620` | `(z *ZoomEffect)` | round/system/entity lifecycle |
| `saveCharVars` | `system.go:5660` | `(s *System)` | round/system/entity lifecycle |
| `restoreCharVars` | `system.go:5685` | `(s *System)` | round/system/entity lifecycle |
| `cleanCustomShaders` | `system.go:5705` | `(s *System)` | round/system/entity lifecycle |
| `shouldHideWithBars` | `system.go:5733` | `(s *System)` | round/system/entity lifecycle |
