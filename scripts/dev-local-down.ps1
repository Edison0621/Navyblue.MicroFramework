$ErrorActionPreference = "Stop"

Write-Host "Stopping local Dapr sidecars..." -ForegroundColor Cyan
if (Get-Command dapr -ErrorAction SilentlyContinue) {
    dapr stop --app-id orderservice | Out-Null
    dapr stop --app-id productservice | Out-Null
}

Write-Host "Stopping local infra services (Redis/Jaeger/OTel)..." -ForegroundColor Cyan
docker compose stop redis jaeger otel-collector

Write-Host ""
Write-Host "Done. Local localhost mode stopped." -ForegroundColor Green
