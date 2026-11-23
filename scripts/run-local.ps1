<#
.SYNOPSIS
    Starts the Healthcare Multi-Tenant Platform local development environment.
.DESCRIPTION
    Launches SQL Server, Service Bus Emulator, Member Service, Claims Ingestion,
    Prometheus, and Grafana via Docker Compose.
#>

[CmdletBinding()]
param(
    [switch]$Build,
    [switch]$Down
)

$ErrorActionPreference = "Stop"

# Verify Docker availability
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "`n[ERROR] Docker CLI was not found in your system PATH." -ForegroundColor Red
    Write-Host "To run the containerized local development environment, please install Docker Desktop:" -ForegroundColor Yellow
    Write-Host "  winget install Docker.DockerDesktop" -ForegroundColor White
    Write-Host "Alternatively, run with WSL 2 or Podman." -ForegroundColor White
    Write-Host "`nMicroservice project files and unit tests can also be run directly via .NET SDK:" -ForegroundColor Cyan
    Write-Host "  dotnet build HealthcarePlatform.sln" -ForegroundColor White
    Write-Host "  dotnet test HealthcarePlatform.sln" -ForegroundColor White
    exit 1
}

# Verify Docker daemon is running
try {
    $dockerInfo = docker info 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n[ERROR] Docker daemon is not running. Please start Docker Desktop and retry." -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "`n[ERROR] Failed to communicate with Docker daemon: $_" -ForegroundColor Red
    exit 1
}

if ($Down) {
    Write-Host "Stopping local healthcare development containers..." -ForegroundColor Yellow
    docker compose -f ./docker/docker-compose.yml down
    return
}

Write-Host "Starting Azure Healthcare Multi-Tenant POC Environment..." -ForegroundColor Green
$composeArgs = @("-f", "./docker/docker-compose.yml", "up", "-d")
if ($Build) {
    $composeArgs += "--build"
}

docker compose @composeArgs

Write-Host "`nLocal Environment Endpoints:" -ForegroundColor Cyan
Write-Host "  Member Service Swagger          : http://localhost:5001/swagger" -ForegroundColor White
Write-Host "  Claims Ingestion Swagger        : http://localhost:5002/swagger" -ForegroundColor White
Write-Host "  Prometheus Web UI               : http://localhost:9090" -ForegroundColor White
Write-Host "  Grafana Dashboard               : http://localhost:3000 (admin / HealthcareSecureGrafana2026!)" -ForegroundColor White
Write-Host "  SQL Server                      : localhost:1433 (sa / HealthcareStrongP@ss2026!)" -ForegroundColor White
