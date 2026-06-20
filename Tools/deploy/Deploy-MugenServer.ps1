param(
    [string]$Server = "root@8.163.135.18",
    [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string]$BuildDir = "",
    [string]$UnityPath = "",
    [switch]$Build,
    [switch]$UnityBuild,
    [string]$RemoteDir = "/home/dev/mugen-server",
    [string]$ServiceName = "mugen-server",
    [string]$ServiceUser = "dev",
    [int]$Port = 7777,
    [string]$AuditHttpHost = "127.0.0.1",
    [int]$AuditHttpPort = 17778
)

$ErrorActionPreference = "Stop"

function Info($message) {
    Write-Host "[deploy] $message"
}

function Require-Command($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Required command not found: $name"
    }
}

Require-Command ssh
Require-Command scp
Require-Command tar

if ([string]::IsNullOrWhiteSpace($BuildDir)) {
    $BuildDir = Join-Path $ProjectPath "Build\MugenRelayServerLinux"
}
$resolvedBuildDir = Resolve-Path -LiteralPath $BuildDir -ErrorAction SilentlyContinue
if ($resolvedBuildDir) {
    $BuildDir = $resolvedBuildDir.Path
}

if ($Build -and -not $UnityBuild) {
    Require-Command dotnet
    $project = Join-Path $ProjectPath "Tools\mugen-relay-server\MugenRelayServer.csproj"
    if (-not (Test-Path -LiteralPath $project)) {
        throw "Relay server project not found: $project"
    }

    $BuildDir = Join-Path $ProjectPath "Build\MugenRelayServerLinux"
    Info "publishing standalone .NET relay server..."
    dotnet publish $project -c Release -r linux-x64 --self-contained true -o $BuildDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed"
    }
    $BuildDir = (Resolve-Path -LiteralPath $BuildDir).Path
}
elseif ($Build -and $UnityBuild) {
    if ([string]::IsNullOrWhiteSpace($UnityPath)) {
        $UnityPath = "C:\Program Files\Unity\Hub\Editor\2022.3.55f1\Editor\Unity.exe"
    }
    if (-not (Test-Path -LiteralPath $UnityPath)) {
        throw "Unity executable not found: $UnityPath. Pass -UnityPath explicitly."
    }

    $buildExe = Join-Path $ProjectPath "Build\MugenServerLinux\MugenServer.x86_64"
    $buildLog = Join-Path $ProjectPath "Logs\mugen-server-build.log"
    New-Item -ItemType Directory -Force -Path (Split-Path $buildLog) | Out-Null
    Info "building Linux headless server with Unity..."
    & $UnityPath `
        -batchmode `
        -quit `
        -projectPath $ProjectPath `
        -executeMethod Lockstep.Editor.MugenServerBuild.BuildLinuxServer `
        -mugenBuildPath $buildExe `
        -logFile $buildLog
    if ($LASTEXITCODE -ne 0) {
        throw "Unity build failed. See $buildLog"
    }
    $BuildDir = (Resolve-Path -LiteralPath (Split-Path $buildExe)).Path
}

if ([string]::IsNullOrWhiteSpace($BuildDir) -or -not (Test-Path -LiteralPath $BuildDir)) {
    throw "Build directory not found. Build first in Unity or run with -Build. Expected: $BuildDir"
}

$serverExeName = ""
$serverArgs = "--port `$PORT --trace-log `$REMOTE_DIR/logs/server-trace.jsonl --audit-http-host $AuditHttpHost --audit-http-port $AuditHttpPort"
if (Test-Path -LiteralPath (Join-Path $BuildDir "MugenRelayServer")) {
    $serverExeName = "MugenRelayServer"
}
elseif (Test-Path -LiteralPath (Join-Path $BuildDir "MugenServer.x86_64")) {
    $serverExeName = "MugenServer.x86_64"
    $serverArgs = "-batchmode -nographics -port `$PORT -logFile `$REMOTE_DIR/logs/server.log"
}
else {
    throw "Invalid server build. Expected MugenRelayServer or MugenServer.x86_64 under $BuildDir"
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("mugen-server-deploy-" + [Guid]::NewGuid().ToString("N"))
$staging = Join-Path $tempRoot "mugen-server"
New-Item -ItemType Directory -Force -Path $staging | Out-Null
Info "staging build..."
Copy-Item -Path (Join-Path $BuildDir "*") -Destination $staging -Recurse -Force

$archive = Join-Path $tempRoot "mugen-server.tgz"
Push-Location $tempRoot
try {
    tar -czf $archive -C $tempRoot "mugen-server"
}
finally {
    Pop-Location
}

$installScript = Join-Path $tempRoot "install-mugen-server.sh"
$installText = @"
#!/usr/bin/env bash
set -euo pipefail

REMOTE_DIR="$RemoteDir"
SERVICE_NAME="$ServiceName"
PORT="$Port"
USER_NAME="$ServiceUser"
EXECUTABLE="$serverExeName"

if ! id -u "`$USER_NAME" >/dev/null 2>&1; then
  sudo useradd -m -s /bin/bash "`$USER_NAME"
fi

mkdir -p "`$REMOTE_DIR/releases" "`$REMOTE_DIR/logs"
tar -xzf "`$REMOTE_DIR/releases/mugen-server.tgz" -C "`$REMOTE_DIR"
chmod +x "`$REMOTE_DIR/mugen-server/`$EXECUTABLE"
sudo chown -R "`$USER_NAME:`$USER_NAME" "`$REMOTE_DIR"

sudo tee "/etc/systemd/system/`$SERVICE_NAME.service" >/dev/null <<SERVICE
[Unit]
Description=MUGEN KCP Match Server
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=`$USER_NAME
WorkingDirectory=`$REMOTE_DIR/mugen-server
Environment=MUGEN_SERVER_PORT=`$PORT
ExecStart=`$REMOTE_DIR/mugen-server/`$EXECUTABLE $serverArgs
Restart=always
RestartSec=3
KillSignal=SIGINT

[Install]
WantedBy=multi-user.target
SERVICE

if command -v ufw >/dev/null 2>&1; then
  sudo ufw allow "`$PORT/udp" || true
fi

sudo systemctl daemon-reload
sudo systemctl enable --now "`$SERVICE_NAME.service"
sudo systemctl restart "`$SERVICE_NAME.service"
sudo systemctl --no-pager --full status "`$SERVICE_NAME.service" || true
echo "Logs: sudo journalctl -u `$SERVICE_NAME -f"
"@
$installText = $installText -replace "`r`n", "`n"
[System.IO.File]::WriteAllText($installScript, $installText, [System.Text.UTF8Encoding]::new($false))

Info "uploading to $Server..."
ssh $Server "mkdir -p '$RemoteDir/releases' '$RemoteDir/logs'"
scp $archive "${Server}:$RemoteDir/releases/mugen-server.tgz"
scp $installScript "${Server}:/tmp/install-mugen-server.sh"

Info "installing systemd service on remote host..."
ssh -tt $Server "bash /tmp/install-mugen-server.sh"

Info "done. UDP port: $Port"
Info "remote logs: ssh $Server `"sudo journalctl -u $ServiceName -f`""
