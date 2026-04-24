param(
    [ValidateSet("infra", "order", "product")]
    [string]$Node = "infra",
    [string]$EnvFile = ".env",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")

switch ($Node) {
    "infra" {
        $workDir = Join-Path $root "deploy\infra"
        $composeFile = "docker-compose.infra.yml"
    }
    "order" {
        $workDir = Join-Path $root "deploy\order-node"
        $composeFile = "docker-compose.order.yml"
    }
    "product" {
        $workDir = Join-Path $root "deploy\product-node"
        $composeFile = "docker-compose.product.yml"
    }
}

$args = @("--env-file", $EnvFile, "-f", $composeFile, "up", "-d")
if (-not $NoBuild) {
    $args += "--build"
}

Write-Host "Starting $Node node with compose file $composeFile..." -ForegroundColor Cyan
Push-Location $workDir
try {
    docker compose @args
    Write-Host ""
    docker compose --env-file $EnvFile -f $composeFile ps
}
finally {
    Pop-Location
}
