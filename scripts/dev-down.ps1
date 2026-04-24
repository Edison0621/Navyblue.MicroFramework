$ErrorActionPreference = "Stop"

Write-Host "Stopping local stack..." -ForegroundColor Cyan
docker compose down

Write-Host ""
Write-Host "Current services:" -ForegroundColor Cyan
docker compose ps
