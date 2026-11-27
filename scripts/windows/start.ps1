# LeadersBoards: start everything for local development on Windows.
# Normally you run this by double-clicking start.bat in the repo root.
#
# What it does, in order:
#   1. Checks Docker Desktop, the .NET 10 SDK and Node.js are installed (and tells you where to get them if not).
#   2. Starts Docker Desktop if it isn't running, then starts the Postgres database container.
#   3. Opens a window running the API (dotnet run).
#   4. Opens a window running the Expo dev server (shows the QR code for your phone).
# Run stop.bat to shut it all down again.

# 'Continue' (not 'Stop'): Windows PowerShell 5.1 treats any text a program writes to stderr as a
# fatal error under 'Stop', and docker/dotnet write progress there. We check $LASTEXITCODE instead.
$ErrorActionPreference = 'Continue'

# Repo root = two folders up from this script (scripts\windows\start.ps1).
$root    = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$apiDir  = Join-Path $root 'backend\LeadersBoards.Api'
$appDir  = Join-Path $root 'mobile'
$runDir  = Join-Path $root '.run'          # where we remember the window process ids for stop.bat
$pidFile = Join-Path $runDir 'windows.pids'

# Always use THIS repo's docker-compose.yml. If a COMPOSE_FILE environment variable is set
# (often left over from another project), docker would otherwise use that project's file.
Remove-Item Env:COMPOSE_FILE, Env:COMPOSE_PROJECT_NAME -ErrorAction SilentlyContinue
$composeArgs = @('compose', '-f', (Join-Path $root 'docker-compose.yml'), '-p', 'leadersboards')


function Say($text)  { Write-Host "  $text" }
function Ok($text)   { Write-Host "  [ok] $text" -ForegroundColor Green }
function Step($text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }

# Prints a friendly error, offers to open the download page, then stops the script.
function Fail($problem, $fix, $url) {
    Write-Host ""
    Write-Host "  [problem] $problem" -ForegroundColor Red
    if ($fix) { Write-Host "  [fix]     $fix" -ForegroundColor Yellow }
    if ($url) {
        Write-Host "            $url" -ForegroundColor Yellow
        $answer = Read-Host "  Open that page in your browser now? (y/n)"
        if ($answer -match '^[yY]') { Start-Process $url }
    }
    exit 1
}

# Is a command (like "docker") available on the PATH?
function Has($name) { [bool](Get-Command $name -ErrorAction SilentlyContinue) }

# Windows only reads PATH when a program starts. If you installed something after opening
# VS Code or a terminal, that window doesn't see it yet. This re-reads PATH from the registry
# so this script sees freshly installed tools without a restart.
function Refresh-Path {
    $machine = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $user    = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$machine;$user"
}

Write-Host ""
Write-Host "LeadersBoards - starting local dev environment" -ForegroundColor White
Write-Host "Repo: $root"

Refresh-Path

# ---------------------------------------------------------------- prerequisites
Step "Checking what's installed"

# Docker
if (-not (Has 'docker')) {
    Fail "Docker isn't installed (or isn't on your PATH)." `
         "Install Docker Desktop, start it once so it finishes setting up, then run start.bat again." `
         "https://www.docker.com/products/docker-desktop/"
}
Ok "Docker found"

# .NET 10 SDK. Also look in the default install folder in case PATH is out of date.
if (-not (Has 'dotnet')) {
    $defaultDotnet = Join-Path $env:ProgramFiles 'dotnet'
    if (Test-Path (Join-Path $defaultDotnet 'dotnet.exe')) { $env:Path = "$defaultDotnet;$env:Path" }
}
if (-not (Has 'dotnet')) {
    Fail ".NET isn't installed (this is the 'dotnet.exe not found' error)." `
         "Install the .NET 10 SDK (x64, the 'SDK' column, not 'Runtime'). Then close and reopen VS Code so it picks it up." `
         "https://dotnet.microsoft.com/download/dotnet/10.0"
}
$sdks = (& dotnet --list-sdks) 2>$null
if (-not ($sdks -match '^10\.')) {
    Fail "The .NET SDK is installed, but not version 10. Found: $(if ($sdks) { ($sdks -join ', ') } else { 'runtime only, no SDK' })" `
         "Install the .NET 10 SDK (you can keep other versions installed alongside it)." `
         "https://dotnet.microsoft.com/download/dotnet/10.0"
}
Ok ".NET 10 SDK found"

# Node.js 20 or newer (the LTS download is fine)
if (-not (Has 'node')) {
    Fail "Node.js isn't installed." `
         "Install the LTS version of Node.js. Then close and reopen VS Code." `
         "https://nodejs.org/en/download"
}
$nodeVersion = (& node --version).TrimStart('v')
if ([int]($nodeVersion.Split('.')[0]) -lt 20) {
    Fail "Node.js $nodeVersion is too old; the app needs 20 or newer." `
         "Install the current LTS version of Node.js." `
         "https://nodejs.org/en/download"
}
Ok "Node.js $nodeVersion found"

# ---------------------------------------------------------------- docker + database
Step "Starting the database"

# "docker info" only succeeds when the Docker engine is actually running.
function Docker-Running { & docker info *> $null; return ($LASTEXITCODE -eq 0) }

if (-not (Docker-Running)) {
    $dockerDesktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if (Test-Path $dockerDesktop) {
        Say "Docker Desktop isn't running. Starting it (this can take a minute the first time)..."
        Start-Process $dockerDesktop
    } else {
        Say "Docker isn't running. Please open Docker Desktop from the Start menu."
    }
    $waited = 0
    while (-not (Docker-Running)) {
        if ($waited -ge 180) {
            Fail "Docker Desktop didn't become ready after 3 minutes." `
                 "Open Docker Desktop, wait until it says 'Engine running' in the bottom-left, then run start.bat again." `
                 $null
        }
        Start-Sleep -Seconds 3; $waited += 3
        Write-Host "." -NoNewline
    }
    Write-Host ""
}
Ok "Docker is running"

Push-Location $root
try {
    # -d = "detached": run in the background and give us the prompt back.
    & docker @composeArgs up -d
    if ($LASTEXITCODE -ne 0) {
        Fail "Couldn't start the Postgres container." `
             "If the error above mentions port 5432, another Postgres is already using it. Stop that one (or uninstall it) and try again." `
             $null
    }

    # The container starts instantly, but Postgres takes a few seconds before it accepts connections.
    Say "Waiting for Postgres to accept connections..."
    $waited = 0
    while ($true) {
        & docker @composeArgs exec -T db pg_isready -U postgres *> $null
        if ($LASTEXITCODE -eq 0) { break }
        if ($waited -ge 60) { Fail "Postgres didn't become ready within 60 seconds." "Run 'docker compose -p leadersboards logs db' in the repo folder to see why." $null }
        Start-Sleep -Seconds 2; $waited += 2
    }
} finally { Pop-Location }
Ok "Postgres is ready on localhost:5432"

# ---------------------------------------------------------------- mobile dependencies
Step "Checking the mobile app's packages"
if (-not (Test-Path (Join-Path $appDir 'node_modules'))) {
    Say "First run: installing packages with 'npm install' (a few minutes, like a NuGet restore)..."
    Push-Location $appDir
    try {
        & npm install
        if ($LASTEXITCODE -ne 0) { Fail "'npm install' failed. See the messages above." $null $null }
    } finally { Pop-Location }
}
Ok "Packages installed"

# ---------------------------------------------------------------- API + Expo windows
Step "Opening the API and Expo windows"

# If stop.bat wasn't run last time, close the old windows first so you don't end up with two APIs.
if (Test-Path $pidFile) {
    foreach ($id in Get-Content $pidFile) { & taskkill /PID $id /T /F *> $null }
    Remove-Item $pidFile -Force
}
New-Item -ItemType Directory -Force -Path $runDir | Out-Null

# Each window is a new PowerShell. -NoExit keeps it open if the program crashes so you can read the error.
$apiCmd  = "`$Host.UI.RawUI.WindowTitle = 'LeadersBoards API'; Set-Location '$apiDir'; dotnet run"
$expoCmd = "`$Host.UI.RawUI.WindowTitle = 'LeadersBoards Expo'; Set-Location '$appDir'; npx expo start"

$api  = Start-Process powershell -ArgumentList '-NoExit', '-NoProfile', '-Command', $apiCmd  -PassThru
$expo = Start-Process powershell -ArgumentList '-NoExit', '-NoProfile', '-Command', $expoCmd -PassThru
Set-Content -Path $pidFile -Value @($api.Id, $expo.Id)
Ok "API window opened (first build takes ~30 seconds)"
Ok "Expo window opened (scan its QR code with your phone)"

# Wait for the API to answer its health check so you know it really started.
Say "Waiting for the API at http://localhost:5271/health ..."
$waited = 0
$healthy = $false
while ($waited -lt 120) {
    try {
        Invoke-WebRequest -Uri 'http://localhost:5271/health' -UseBasicParsing -TimeoutSec 2 | Out-Null
        $healthy = $true; break
    } catch { Start-Sleep -Seconds 2; $waited += 2 }
}
if ($healthy) { Ok "API is up" }
else { Write-Host "  [warn] The API hasn't answered yet. Check the 'LeadersBoards API' window for errors." -ForegroundColor Yellow }

Write-Host ""
Write-Host "All started. Scan the QR code in the Expo window with Expo Go (same Wi-Fi as this PC)." -ForegroundColor Green
Write-Host "Run stop.bat when you're done."
