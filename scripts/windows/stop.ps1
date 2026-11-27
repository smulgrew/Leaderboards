# LeadersBoards: stop everything that start.bat started.
# Your database data is kept (it lives in a Docker volume), so next time you start, your boards are still there.

$root    = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$pidFile = Join-Path $root '.run\windows.pids'

# Always use THIS repo's docker-compose.yml. If a COMPOSE_FILE environment variable is set
# (often left over from another project), docker would otherwise use that project's file.
Remove-Item Env:COMPOSE_FILE, Env:COMPOSE_PROJECT_NAME -ErrorAction SilentlyContinue
$composeArgs = @('compose', '-f', (Join-Path $root 'docker-compose.yml'), '-p', 'leadersboards')


Write-Host ""
Write-Host "LeadersBoards - stopping local dev environment" -ForegroundColor White

# Close the API and Expo windows. /T also stops the programs running inside them (dotnet, node).
if (Test-Path $pidFile) {
    foreach ($id in Get-Content $pidFile) { & taskkill /PID $id /T /F *> $null }
    Remove-Item $pidFile -Force
    Write-Host "  [ok] Closed the API and Expo windows" -ForegroundColor Green
} else {
    Write-Host "  No API/Expo windows recorded (already closed?)"
}

# Stop (not delete) the Postgres container.
if (Get-Command docker -ErrorAction SilentlyContinue) {
    & docker info *> $null
    if ($LASTEXITCODE -eq 0) {
        Push-Location $root
        try { & docker @composeArgs stop } finally { Pop-Location }
        Write-Host "  [ok] Database stopped (your data is kept)" -ForegroundColor Green
    } else {
        Write-Host "  Docker isn't running, so the database is already stopped."
    }
}

Write-Host ""
Write-Host "Done. Docker Desktop itself is still open; quit it from the system tray if you like."
