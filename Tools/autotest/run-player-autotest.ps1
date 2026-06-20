param(
    [string]$GameExe = "Build\AutoTest\LockstepActDemo.exe",
    [string]$ServerExe = "Tools\mugen-relay-server\bin\Debug\net8.0\MugenRelayServer.exe",
    [string]$LogRoot = "Logs\autotest",
    [int]$BasePort = 17777,
    [int]$Matches = 3,
    [int]$DurationSeconds = 60,
    [int]$Inputs = 300,
    [int]$RoundSeconds = 8,
    [int]$TimeoutSeconds = 120,
    [int]$ScreenWidth = 0,
    [int]$ScreenHeight = 0,
    [string]$WeakToggleFrames = "",
    [string]$WeakToggleSide = "",
    [string]$EscapeFrames = "",
    [string]$ScreenshotFrames = "",
    [string]$TeamCsv = "",
    [switch]$VisibleClients,
    [switch]$QuitAfterDuration,
    [string]$RunId = ""
)

$ErrorActionPreference = "Stop"

function Resolve-RepoPath([string]$Path) {
    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }
    return Join-Path $PWD $Path
}

function Stop-ProcQuietly($Proc) {
    if ($null -eq $Proc) {
        return
    }
    try {
        $Proc.Refresh()
        if (-not $Proc.HasExited) {
            Stop-Process -Id $Proc.Id -Force -ErrorAction SilentlyContinue
            $Proc.WaitForExit(5000) | Out-Null
        }
    } catch {
    }
}

if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = "player-" + (Get-Date -Format "yyyyMMdd-HHmmss")
}

$gamePath = Resolve-RepoPath $GameExe
$serverPath = Resolve-RepoPath $ServerExe
$rootPath = Resolve-RepoPath $LogRoot
$runPath = Join-Path $rootPath $RunId

if (-not (Test-Path $gamePath)) {
    throw "Game exe not found: $gamePath"
}
if (-not (Test-Path $serverPath)) {
    throw "Server exe not found: $serverPath"
}

New-Item -ItemType Directory -Force -Path $runPath | Out-Null

$summary = @()
for ($match = 1; $match -le $Matches; $match++) {
    $port = $BasePort + $match - 1
    $matchId = "{0:D2}" -f $match
    $matchRunId = "$RunId-match-$matchId"
    $matchPath = Join-Path $runPath "match-$matchId"
    New-Item -ItemType Directory -Force -Path $matchPath | Out-Null

    $serverTrace = Join-Path $matchPath "server.jsonl"
    $serverOut = Join-Path $matchPath "server.out.log"
    $serverErr = Join-Path $matchPath "server.err.log"

    Write-Output "[AutoTest] Starting server match=$match port=$port"
    $serverProc = Start-Process -FilePath $serverPath `
        -ArgumentList @("--port", "$port", "--trace-log", $serverTrace) `
        -RedirectStandardOutput $serverOut `
        -RedirectStandardError $serverErr `
        -WindowStyle Hidden `
        -PassThru

    Start-Sleep -Milliseconds 900

    $clients = @()
    foreach ($side in @("A", "B")) {
        $seed = if ($side -eq "A") { 1000 + $match } else { 2000 + $match }
        $playerLog = Join-Path $matchPath "$side-player.log"
        $clientArgs = @(
            "-logFile", $playerLog,
            "--autotest",
            "--client-id", "$side",
            "--run-id", $matchRunId,
            "--server", "127.0.0.1:$port",
            "--seed", "$seed",
            "--duration", "$DurationSeconds",
            "--inputs", "$Inputs",
            "--round-seconds", "$RoundSeconds",
            "--log-dir", $matchPath
        )
        if (-not [string]::IsNullOrWhiteSpace($WeakToggleFrames) -and
            ([string]::IsNullOrWhiteSpace($WeakToggleSide) -or $WeakToggleSide -eq $side)) {
            $clientArgs += @("--weak-toggle-frames", $WeakToggleFrames)
        }
        if (-not [string]::IsNullOrWhiteSpace($EscapeFrames)) {
            $clientArgs += @("--escape-frames", $EscapeFrames)
        }
        if (-not [string]::IsNullOrWhiteSpace($ScreenshotFrames)) {
            $clientArgs += @("--screenshot-frames", $ScreenshotFrames)
        }
        if (-not [string]::IsNullOrWhiteSpace($TeamCsv)) {
            $clientArgs += @("--team", $TeamCsv)
        }
        if ($QuitAfterDuration) {
            $clientArgs += @("--quit-after-duration")
        }
        if ($ScreenWidth -gt 0 -and $ScreenHeight -gt 0) {
            $clientArgs = @("-screen-width", "$ScreenWidth", "-screen-height", "$ScreenHeight") + $clientArgs
        }
        Write-Output "[AutoTest] Starting client $side match=$match screenOverride=${ScreenWidth}x${ScreenHeight}"
        $clientWindowStyle = if ($VisibleClients) { "Normal" } else { "Hidden" }
        $proc = Start-Process -FilePath $gamePath `
            -ArgumentList $clientArgs `
            -WorkingDirectory (Split-Path $gamePath -Parent) `
            -WindowStyle $clientWindowStyle `
            -PassThru
        Write-Output "[AutoTest] Client $side pid=$($proc.Id) log=$playerLog"
        $clients += [PSCustomObject]@{
            Side = $side
            Proc = $proc
            Log = $playerLog
        }
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $timedOut = $false
    while ((Get-Date) -lt $deadline) {
        $running = @($clients | Where-Object { -not $_.Proc.HasExited })
        if ($running.Count -eq 0) {
            break
        }
        Start-Sleep -Milliseconds 500
    }

    foreach ($client in $clients) {
        if (-not $client.Proc.HasExited) {
            $timedOut = $true
            Write-Output "[AutoTest] Timeout client $($client.Side), killing pid=$($client.Proc.Id)"
            Stop-ProcQuietly $client.Proc
        }
    }

    $clientExitCodes = @{}
    $clientFailed = $false
    foreach ($client in $clients) {
        try {
            $client.Proc.Refresh()
            $exitCode = if ($client.Proc.HasExited) { $client.Proc.ExitCode } else { $null }
            $clientExitCodes[$client.Side] = $exitCode
            if ($exitCode -ne $null -and $exitCode -ne 0) {
                $clientFailed = $true
                Write-Output "[AutoTest] Client $($client.Side) exited with code $exitCode"
            }
        } catch {
            $clientExitCodes[$client.Side] = $null
        }
    }

    Stop-ProcQuietly $serverProc

    $summary += [PSCustomObject]@{
        Match = $match
        Port = $port
        RunId = $matchRunId
        TimedOut = $timedOut
        ClientFailed = $clientFailed
        ClientExitCodes = $clientExitCodes
        LogDir = $matchPath
        ServerTrace = $serverTrace
    }
}

$summaryPath = Join-Path $runPath "run-summary.json"
$summary | ConvertTo-Json -Depth 4 | Set-Content -Path $summaryPath -Encoding UTF8
Write-Output "[AutoTest] Finished run=$RunId logs=$runPath"
Write-Output "[AutoTest] Summary=$summaryPath"
