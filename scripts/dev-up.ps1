param(
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

Write-Host "Starting local stack with Docker Compose..." -ForegroundColor Cyan

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI not found. Please install Docker Desktop first."
}

# Pull base images first to reduce flaky build failures on slow networks.
Write-Host "Pulling .NET base images..." -ForegroundColor Yellow
docker pull mcr.microsoft.com/dotnet/aspnet:10.0
docker pull mcr.microsoft.com/dotnet/sdk:10.0

$composeArgs = @("compose", "up", "-d")
if (-not $NoBuild) {
    $composeArgs += "--build"
}

Write-Host "Running: docker $($composeArgs -join ' ')" -ForegroundColor Yellow
docker @composeArgs

Write-Host ""
Write-Host "Current services:" -ForegroundColor Cyan
docker compose ps

Write-Host ""
Write-Host "Done. Useful URLs:" -ForegroundColor Green
Write-Host " - Ops Portal:   http://localhost:5000"
Write-Host " - OrderService: http://localhost:5001"
Write-Host " - ProductService: http://localhost:5002"
Write-Host " - AuthService:  http://localhost:5004"
Write-Host " - UserService:  http://localhost:5005"
Write-Host " - GatewayService: http://localhost:5006"
Write-Host " - AuditService: http://localhost:5007"
Write-Host " - CatalogService: http://localhost:5008"
Write-Host " - InventoryService: http://localhost:5009"
Write-Host " - NotificationService: http://localhost:5010"
Write-Host " - JobService: http://localhost:5011"
Write-Host " - PromotionService: http://localhost:5012"
Write-Host " - PlatformAdmin: http://localhost:5013"
Write-Host " - Jaeger UI:    http://localhost:16686"
