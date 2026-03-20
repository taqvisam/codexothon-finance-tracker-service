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

    Invoke-Step -Name "Stopping and removing containers" -Command { podman compose down --remove-orphans }
    Invoke-Step -Name "Removing compose images and volumes" -Command { podman compose down --rmi all --volumes --remove-orphans }

    Write-Host ""
    Write-Host "Clean completed successfully" -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ""
    Write-Host "Clean failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
finally {
    Pop-Location
}
