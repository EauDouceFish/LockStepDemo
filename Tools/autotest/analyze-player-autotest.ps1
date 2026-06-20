param(
    [Parameter(Mandatory = $true)]
    [string]$RunPath
)

$ErrorActionPreference = "Stop"

function Read-JsonLines([string]$Path) {
    $items = [System.Collections.Generic.List[object]]::new()
    if (-not (Test-Path $Path)) {
        return @()
    }
    $stream = [System.IO.File]::OpenRead((Resolve-Path $Path))
    $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::UTF8, $true, 65536)
    try {
        while (($line = $reader.ReadLine()) -ne $null) {
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }
            try {
                $items.Add(($line | ConvertFrom-Json))
            } catch {
                $items.Add([PSCustomObject]@{
                    parseError = $_.Exception.Message
                    raw = $line
                })
            }
        }
    } finally {
        $reader.Dispose()
        $stream.Dispose()
    }
    return $items.ToArray()
}

function Percentile($Values, [double]$P) {
    $array = @($Values | Sort-Object)
    if ($array.Count -eq 0) {
        return $null
    }
    $index = [int][System.Math]::Ceiling(($P / 100.0) * $array.Count) - 1
    $index = [System.Math]::Max(0, [System.Math]::Min($array.Count - 1, $index))
    return [int]$array[$index]
}

function Count-ClientEvent($Events, [string]$Name) {
    return @($Events | Where-Object { $_.event -eq $Name -or $_.clientEvent -eq $Name }).Count
}

function Convert-ToBool($Value) {
    if ($Value -is [bool]) {
        return $Value
    }
    if ($Value -is [string]) {
        return $Value -ieq "true"
    }
    return [bool]$Value
}

function As-Array($Value) {
    if ($null -eq $Value) {
        return @()
    }
    return @($Value)
}

function Read-JsonFile([string]$Path) {
    if (-not (Test-Path $Path)) {
        return $null
    }
    try {
        return Get-Content -Path $Path -Raw | ConvertFrom-Json
    } catch {
        return [PSCustomObject]@{
            parseError = $_.Exception.Message
            path = $Path
        }
    }
}

function Max-Number($Values) {
    $array = @($Values | Where-Object { $_ -ne $null })
    if ($array.Count -eq 0) {
        return $null
    }
    return [int](($array | Measure-Object -Maximum).Maximum)
}

function Min-Number($Values) {
    $array = @($Values | Where-Object { $_ -ne $null })
    if ($array.Count -eq 0) {
        return $null
    }
    return [int](($array | Measure-Object -Minimum).Minimum)
}

function Get-RunLogSummary([string]$MatchDir) {
    $files = @(Get-ChildItem -Path $MatchDir -Recurse -File -Filter "run_*.json" -ErrorAction SilentlyContinue)
    $parseErrors = 0
    $completed = 0
    $frameCounts = @()
    $finalHashes = @()
    $missingFinalHashes = 0
    $hashChecksums = @()
    $missingHashChecksums = 0
    $stateNos = @()
    $pauseFrozenSamples = 0
    $hitstopSamples = 0
    $pushLogSamples = 0

    foreach ($file in $files) {
        $log = Read-JsonFile $file.FullName
        if ($null -eq $log -or $log.parseError -ne $null) {
            $parseErrors++
            continue
        }

        if (Convert-ToBool $log.completed) {
            $completed++
        }
        if (-not [string]::IsNullOrWhiteSpace($log.finalHashHex)) {
            $finalHashes += $log.finalHashHex
        } else {
            $missingFinalHashes++
        }
        if (-not [string]::IsNullOrWhiteSpace($log.hashChecksumHex)) {
            $hashChecksums += $log.hashChecksumHex
        } else {
            $missingHashChecksums++
        }

        $frames = @(As-Array $log.frames)
        $frameCounts += $frames.Count
        foreach ($frame in $frames) {
            $entities = @(As-Array $frame.entities)
            foreach ($entity in $entities) {
                if ($entity.stateNo -ne $null) {
                    $stateNos += [int]$entity.stateNo
                }
                if (Convert-ToBool $entity.pauseBool) {
                    $pauseFrozenSamples++
                }
                if ($entity.hitstop -ne $null -and [int]$entity.hitstop -gt 0) {
                    $hitstopSamples++
                }
                if ($entity.playerPushEnabled -ne $null -or $entity.widthPlayerFrontRaw -ne $null -or
                    $entity.widthPlayerBackRaw -ne $null) {
                    $pushLogSamples++
                }
            }
        }
    }

    $uniqueStates = @($stateNos | Sort-Object -Unique)
    $commonStatesSeen = @($uniqueStates | Where-Object { $_ -eq 100 -or $_ -eq 105 -or $_ -eq 106 })
    $uniqueFinalHashes = @($finalHashes | Sort-Object -Unique)
    $uniqueHashChecksums = @($hashChecksums | Sort-Object -Unique)

    return [PSCustomObject]@{
        Files = $files.Count
        ParseErrors = $parseErrors
        Completed = $completed
        MinFrames = (Min-Number $frameCounts)
        MaxFrames = (Max-Number $frameCounts)
        UniqueFinalHashes = $uniqueFinalHashes.Count
        MissingFinalHashes = $missingFinalHashes
        UniqueHashChecksums = $uniqueHashChecksums.Count
        MissingHashChecksums = $missingHashChecksums
        StateCount = $uniqueStates.Count
        Common100105106Seen = $commonStatesSeen.Count
        PauseFrozenSamples = $pauseFrozenSamples
        HitstopSamples = $hitstopSamples
        PushLogSamples = $pushLogSamples
    }
}

function Get-NetDiagSummary([string]$MatchDir) {
    $files = @(Get-ChildItem -Path $MatchDir -Recurse -File -Filter "net_*.jsonl" -ErrorAction SilentlyContinue)
    $events = @()
    foreach ($file in $files) {
        $events += @(Read-JsonLines $file.FullName)
    }

    $parseErrors = @($events | Where-Object { $_.parseError -ne $null }).Count
    $weakChanged = @($events | Where-Object { $_.event -eq "weak_network_changed" }).Count
    $remoteStatus = @($events | Where-Object { $_.event -eq "remote_net_status" }).Count
    $maxWeak = Max-Number (@($events | ForEach-Object { $_.weakDelayMs }))
    $maxEffectiveWeak = Max-Number (@($events | ForEach-Object { $_.effectiveWeakDelayMs }))
    $maxLatencyBudget = Max-Number (@($events | ForEach-Object { $_.latencyBudgetMs }))

    return [PSCustomObject]@{
        Files = $files.Count
        Events = $events.Count
        ParseErrors = $parseErrors
        WeakChangedEvents = $weakChanged
        RemoteStatusEvents = $remoteStatus
        MaxWeakDelayMs = $maxWeak
        MaxEffectiveWeakDelayMs = $maxEffectiveWeak
        MaxLatencyBudgetMs = $maxLatencyBudget
    }
}

$runFullPath = if ([System.IO.Path]::IsPathRooted($RunPath)) { $RunPath } else { Join-Path $PWD $RunPath }
if (-not (Test-Path $runFullPath)) {
    throw "Run path not found: $runFullPath"
}

$results = @()
foreach ($dir in Get-ChildItem -Path $runFullPath -Directory -Filter "match-*") {
    $serverPath = Join-Path $dir.FullName "server.jsonl"
    $server = @(Read-JsonLines $serverPath)
    $clientA = @(Read-JsonLines (Join-Path $dir.FullName "A.jsonl"))
    $clientB = @(Read-JsonLines (Join-Path $dir.FullName "B.jsonl"))
    $allClient = @($clientA + $clientB)

    $parseErrors = @($server | Where-Object { $_.parseError -ne $null }).Count
    $inputRecv = @($server | Where-Object {
        $_.event -eq "recv" -and $_.msgType -eq "MugenInput" -and
        $_.clientUnixMs -ne $null -and $_.clientUnixMs -gt 0 -and $_.frame -ne $null
    })
    $latencies = @($inputRecv | ForEach-Object { [int]($_.serverUnixMs - $_.clientUnixMs) })
    $inputA = @($inputRecv | Where-Object { $_.clientId -eq "A" }).Count
    $inputB = @($inputRecv | Where-Object { $_.clientId -eq "B" }).Count

    $serverTrace = @($server | Where-Object { $_.clientEvent -ne $null })
    $runLog = Get-RunLogSummary $dir.FullName
    $netDiag = Get-NetDiagSummary $dir.FullName
    $matchOverEvents = Count-ClientEvent $serverTrace "match_over"
    $matchClosedEvents = Count-ClientEvent $serverTrace "match_closed"
    $applicationQuitEvents = Count-ClientEvent $serverTrace "application_quit"
    $weakToggleEvents = Count-ClientEvent $allClient "weak_network_toggle"
    $weakToggleRequested = @($allClient | Where-Object {
        ($_.event -eq "autotest_init" -or $_.clientEvent -eq "autotest_init") -and
        $_.detail -match "--weak-toggle-frames"
    }).Count
    $roomClosed = @($server | Where-Object { $_.msgType -eq "RoomClosed" })
    $hashMismatch = @($roomClosed | Where-Object { $_.reason -like "hash mismatch*" } | Select-Object -First 1)
    $lastState = @($server | Where-Object {
        $_.queue -ne $null -and $_.rooms -ne $null -and $_.queue -ge 0 -and $_.rooms -ge 0
    } | Select-Object -Last 1)

    $missing = @()
    foreach ($name in @("scene_main_menu", "scene_select", "button_match", "match_request", "match_found", "load_begin", "load_ready", "activate_battle_scene", "scene_battle", "battle_room_ready", "start_match")) {
        if ((Count-ClientEvent $serverTrace $name) -eq 0 -and (Count-ClientEvent $allClient $name) -eq 0) {
            $missing += $name
        }
    }
    if ($inputA -eq 0) { $missing += "server_input_A" }
    if ($inputB -eq 0) { $missing += "server_input_B" }
    if (($matchOverEvents + $matchClosedEvents) -eq 0) { $missing += "terminal_match_event" }
    if ($applicationQuitEvents -eq 0) { $missing += "application_quit" }
    if ($parseErrors -gt 0) { $missing += "server_json_parse_errors=$parseErrors" }

    $timedOut = $false
    $clientFailed = $false
    $clientExitCodes = $null
    $summaryPath = Join-Path $runFullPath "run-summary.json"
    if (Test-Path $summaryPath) {
        $runSummary = Get-Content -Path $summaryPath -Raw | ConvertFrom-Json
        if (-not ($runSummary -is [System.Array])) {
            $runSummary = @($runSummary)
        }
        $entry = @($runSummary | Where-Object { $_.LogDir -eq $dir.FullName -or $_.LogDir -eq $dir.ToString() } | Select-Object -First 1)
        if ($entry.Count -gt 0) {
            $timedOut = Convert-ToBool $entry[0].TimedOut
            $clientFailed = Convert-ToBool $entry[0].ClientFailed
            $clientExitCodes = $entry[0].ClientExitCodes
        }
    }
    if ($clientFailed) { $missing += "client_failed" }
    if ($timedOut) { $missing += "timeout" }
    if ($hashMismatch.Count -gt 0) { $missing += "hash_mismatch" }
    if ($runLog.Files -lt 2) { $missing += "runlog_pair" }
    if ($runLog.ParseErrors -gt 0) { $missing += "runlog_parse_errors=$($runLog.ParseErrors)" }
    if ($runLog.Completed -lt 2) { $missing += "runlog_completed_pair" }
    if ($runLog.MinFrames -ne $null -and $runLog.MinFrames -lt 30) { $missing += "runlog_short=$($runLog.MinFrames)" }
    if ($runLog.MissingFinalHashes -gt 0) { $missing += "runlog_final_hash_missing=$($runLog.MissingFinalHashes)" }
    if ($runLog.UniqueFinalHashes -gt 1) { $missing += "runlog_final_hash_diverged" }
    if ($runLog.MissingHashChecksums -gt 0) { $missing += "runlog_hash_checksum_missing=$($runLog.MissingHashChecksums)" }
    if ($runLog.UniqueHashChecksums -gt 1) { $missing += "runlog_hash_checksum_diverged" }
    if ($netDiag.ParseErrors -gt 0) { $missing += "netdiag_parse_errors=$($netDiag.ParseErrors)" }
    if ($weakToggleRequested -gt 0 -or $weakToggleEvents -gt 0) {
        if ($weakToggleEvents -eq 0) { $missing += "weak_toggle_not_observed" }
        if ($netDiag.Files -eq 0) { $missing += "weak_netdiag_missing" }
        if ($netDiag.WeakChangedEvents -eq 0 -or $netDiag.MaxEffectiveWeakDelayMs -le 0) {
            $missing += "weak_delay_not_observed"
        }
        if ($netDiag.RemoteStatusEvents -eq 0) { $missing += "weak_remote_status_missing" }
    }

    $results += [PSCustomObject]@{
        Match = $dir.Name
        TimedOut = $timedOut
        ClientFailed = $clientFailed
        ClientExitCodes = $clientExitCodes
        ServerEvents = $server.Count
        ServerParseErrors = $parseErrors
        ServerTraceEvents = $serverTrace.Count
        ClientEvents = $allClient.Count
        InputsReceivedA = $inputA
        InputsReceivedB = $inputB
        MatchOverEvents = $matchOverEvents
        MatchClosedEvents = $matchClosedEvents
        ApplicationQuitEvents = $applicationQuitEvents
        WeakToggleEvents = $weakToggleEvents
        WeakToggleRequested = $weakToggleRequested
        LatencyCount = $latencies.Count
        LatencyP50Ms = Percentile $latencies 50
        LatencyP95Ms = Percentile $latencies 95
        LatencyP99Ms = Percentile $latencies 99
        LastQueue = if ($lastState.Count -gt 0) { $lastState[-1].queue } else { $null }
        LastRooms = if ($lastState.Count -gt 0) { $lastState[-1].rooms } else { $null }
        RunLogFiles = $runLog.Files
        RunLogCompleted = $runLog.Completed
        RunLogMinFrames = $runLog.MinFrames
        RunLogMaxFrames = $runLog.MaxFrames
        RunLogUniqueFinalHashes = $runLog.UniqueFinalHashes
        RunLogMissingFinalHashes = $runLog.MissingFinalHashes
        RunLogUniqueHashChecksums = $runLog.UniqueHashChecksums
        RunLogMissingHashChecksums = $runLog.MissingHashChecksums
        RunLogStateCount = $runLog.StateCount
        RunLogCommon100105106Seen = $runLog.Common100105106Seen
        RunLogPauseFrozenSamples = $runLog.PauseFrozenSamples
        RunLogHitstopSamples = $runLog.HitstopSamples
        RunLogPushSamples = $runLog.PushLogSamples
        NetDiagFiles = $netDiag.Files
        NetDiagEvents = $netDiag.Events
        NetDiagWeakChangedEvents = $netDiag.WeakChangedEvents
        NetDiagRemoteStatusEvents = $netDiag.RemoteStatusEvents
        NetDiagMaxWeakDelayMs = $netDiag.MaxWeakDelayMs
        NetDiagMaxEffectiveWeakDelayMs = $netDiag.MaxEffectiveWeakDelayMs
        NetDiagMaxLatencyBudgetMs = $netDiag.MaxLatencyBudgetMs
        RoomClosedReasons = @($roomClosed | ForEach-Object { $_.reason } | Sort-Object -Unique)
        FirstHashMismatch = if ($hashMismatch.Count -gt 0) { $hashMismatch[0].reason } else { $null }
        Missing = $missing
        ServerTrace = $serverPath
    }
}

$out = Join-Path $runFullPath "audit-summary.json"
$results | ConvertTo-Json -Depth 6 | Set-Content -Path $out -Encoding UTF8
$results | Format-Table Match,TimedOut,ServerTraceEvents,InputsReceivedA,InputsReceivedB,LatencyP50Ms,LatencyP95Ms,LatencyP99Ms,RunLogFiles,RunLogMinFrames,RunLogUniqueFinalHashes,NetDiagFiles,NetDiagMaxEffectiveWeakDelayMs,LastQueue,LastRooms -AutoSize
foreach ($result in $results) {
    if ($result.Missing.Count -gt 0) {
        Write-Host "[Audit] $($result.Match) missing: $($result.Missing -join ', ')"
    }
}
Write-Host "[Audit] Summary=$out"

$failed = @($results | Where-Object {
    $_.TimedOut -or $_.ClientFailed -or $_.FirstHashMismatch -ne $null -or $_.Missing.Count -gt 0
})
if ($failed.Count -gt 0) {
    Write-Error "[Audit] Failed matches: $($failed.Match -join ', ')"
    exit 1
}
