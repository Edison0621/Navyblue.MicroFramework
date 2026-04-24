param(
    [switch]$NoInfra
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$localComponents = Join-Path $root "components\local"
$orderProject = Join-Path $root "samples\OrderService\OrderService.csproj"
$productProject = Join-Path $root "samples\ProductService\ProductService.csproj"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI not found. Please install Docker Desktop first."
}

if (-not (Get-Command dapr -ErrorAction SilentlyContinue)) {
    throw "Dapr CLI not found. Install Dapr CLI first: https://docs.dapr.io/getting-started/install-dapr-cli/"
}

if (-not $NoInfra) {
    Write-Host "Starting local infra services (Redis/Jaeger/OTel)..." -ForegroundColor Cyan
    docker compose up -d redis jaeger otel-collector
}

Write-Host "Starting OrderService with Dapr (localhost components)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "dapr run --app-id orderservice --app-port 5010 --dapr-http-port 3501 --resources-path `"$localComponents`" -- dotnet run --project `"$orderProject`""
)

Write-Host "Starting ProductService with Dapr (localhost components)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList @(
    "-NoExit",
    "-Command",
    "dapr run --app-id productservice --app-port 5127 --dapr-http-port 3502 --resources-path `"$localComponents`" -- dotnet run --project `"$productProject`""
)

Write-Host ""
Write-Host "Local dev mode started." -ForegroundColor Green
Write-Host " - OrderService:   http://localhost:5010"
Write-Host " - ProductService: http://localhost:5127"
Write-Host " - Redis:          localhost:6379"
Write-Host " - Jaeger UI:      http://localhost:16686"
