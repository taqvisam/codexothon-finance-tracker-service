Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,
        [Parameter(Mandatory = $true)]
        [scriptblock]$Command
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan
    & $Command
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed: $Name (exit code: $LASTEXITCODE)"
    }
}

try {
    Push-Location $repoRoot

    Invoke-Step -Name "Stopping old containers" -Command { podman compose down }
    Invoke-Step -Name "Building images" -Command { podman compose build }
    Invoke-Step -Name "Starting containers in detached mode" -Command { podman compose up -d }

    Write-Host ""
    Write-Host "==> Waiting for API startup..." -ForegroundColor Cyan
    Start-Sleep -Seconds 8

    Write-Host ""
    Write-Host "==> Running health check: http://localhost:5000/swagger" -ForegroundColor Cyan
    $response = Invoke-WebRequest -Uri "http://localhost:5000/swagger" -UseBasicParsing -TimeoutSec 15
    if ($response.StatusCode -ne 200) {
        throw "Health check failed: expected HTTP 200, got $($response.StatusCode)"
    }

    Write-Host ""
    Write-Host "Pipeline completed successfully" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ""
    Write-Host "Pipeline failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}
