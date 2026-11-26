<#
.SYNOPSIS
    PrIMA Proactive Monitoring - Windows Services Deployment & Management Script
.DESCRIPTION
    Installs, configures, and manages PrIMA API and PrIMA Detectors as native Windows Services
    with automatic crash recovery and firewall configuration for hospital on-premise deployments.
.PARAMETER Action
    Operation to perform: Install, Uninstall, Start, Stop, Restart, Status. Default is Install.
.PARAMETER InstallPath
    Root directory where published PrIMA binaries are deployed. Default: C:\PrIMA.
.PARAMETER ApiServiceName
    Name of the PrIMA API Windows Service. Default: PrIMAApi.
.PARAMETER DetectorServiceName
    Name of the PrIMA Detectors Windows Service. Default: PrIMADetectors.
.PARAMETER ApiPort
    TCP port for PrIMA Web API Kestrel listener. Default: 5000.
.PARAMETER DetectorPort
    TCP port for PrIMA Detectors webhook listener. Default: 5090.
.EXAMPLE
    .\install-prima-service.ps1 -Action Install -InstallPath "C:\PrIMA"
.EXAMPLE
    .\install-prima-service.ps1 -Action Status
#>

[CmdletBinding()]
param(
    [ValidateSet('Install', 'Uninstall', 'Start', 'Stop', 'Restart', 'Status')]
    [string]$Action = 'Install',

    [string]$InstallPath = 'C:\PrIMA',
    [string]$ApiServiceName = 'PrIMAApi',
    [string]$DetectorServiceName = 'PrIMADetectors',
    [int]$ApiPort = 5000,
    [int]$DetectorPort = 5090
)

$ErrorActionPreference = 'Stop'

function Test-IsElevated {
    $currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-IsElevated)) {
    Write-Error "This script must be executed from an elevated PowerShell console (Run as Administrator)."
    exit 1
}

$ApiBinaryPath = Join-Path $InstallPath "api\PrIMA.Api.exe"
$DetectorBinaryPath = Join-Path $InstallPath "detectors\PrIMA.Detectors.exe"

function Install-PrIMAServices {
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " Installing PrIMA Windows Services in $InstallPath" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan

    # 1. Verify Binaries
    if (-not (Test-Path $ApiBinaryPath)) {
        Write-Warning "PrIMA API binary not found at: $ApiBinaryPath"
        Write-Host "Please ensure 'dotnet publish -c Release -o $InstallPath\api' has been executed." -ForegroundColor Yellow
    }
    if (-not (Test-Path $DetectorBinaryPath)) {
        Write-Warning "PrIMA Detectors binary not found at: $DetectorBinaryPath"
        Write-Host "Please ensure 'dotnet publish -c Release -o $InstallPath\detectors' has been executed." -ForegroundColor Yellow
    }

    # 2. Configure PrIMA Web API & Supervisor Service
    Write-Host "`n[1/4] Configuring $ApiServiceName..." -ForegroundColor Green
    $existingApi = Get-Service -Name $ApiServiceName -ErrorAction SilentlyContinue
    if ($existingApi) {
        Write-Host "Service $ApiServiceName exists. Stopping and removing..." -ForegroundColor Yellow
        Stop-Service -Name $ApiServiceName -Force -ErrorAction SilentlyContinue
        & sc.exe delete $ApiServiceName | Out-Null
        Start-Sleep -Seconds 2
    }

    & sc.exe create $ApiServiceName binPath= "$ApiBinaryPath" start= delayed-auto DisplayName= "PrIMA Proactive Monitoring - API & Supervisor" | Out-Null
    & sc.exe description $ApiServiceName "Hospital proactive invariant monitoring Web API, SLA escalation, and silence supervisor background worker." | Out-Null
    
    # Configure crash recovery: restart service after 60s (up to 3 times, reset fail count after 24 hours)
    & sc.exe failure $ApiServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
    Write-Host "  -> Service $ApiServiceName created with automatic recovery (60s restart)." -ForegroundColor Gray

    # 3. Configure PrIMA Detectors Runner Service
    Write-Host "`n[2/4] Configuring $DetectorServiceName..." -ForegroundColor Green
    $existingDetector = Get-Service -Name $DetectorServiceName -ErrorAction SilentlyContinue
    if ($existingDetector) {
        Write-Host "Service $DetectorServiceName exists. Stopping and removing..." -ForegroundColor Yellow
        Stop-Service -Name $DetectorServiceName -Force -ErrorAction SilentlyContinue
        & sc.exe delete $DetectorServiceName | Out-Null
        Start-Sleep -Seconds 2
    }

    & sc.exe create $DetectorServiceName binPath= "$DetectorBinaryPath" start= delayed-auto DisplayName= "PrIMA Proactive Monitoring - Detector Runner" | Out-Null
    & sc.exe description $DetectorServiceName "Autonomous SIMRS non-intrusive detector runner and Kestrel webhook verification listener." | Out-Null
    
    # Configure crash recovery: restart service after 60s
    & sc.exe failure $DetectorServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
    Write-Host "  -> Service $DetectorServiceName created with automatic recovery (60s restart)." -ForegroundColor Gray

    # 4. Configure Windows Firewall Rules
    Write-Host "`n[3/4] Configuring Windows Firewall Rules..." -ForegroundColor Green
    
    # Inbound rule for API port (Kestrel HTTP)
    $apiRuleName = "PrIMA-Api-Port-$ApiPort"
    Remove-NetFirewallRule -DisplayName $apiRuleName -ErrorAction SilentlyContinue
    New-NetFirewallRule -DisplayName $apiRuleName `
                        -Direction Inbound `
                        -LocalPort $ApiPort `
                        -Protocol TCP `
                        -Action Allow `
                        -Description "Allows incoming traffic to PrIMA Web API backend" | Out-Null
    Write-Host "  -> Inbound firewall rule created for TCP Port $ApiPort" -ForegroundColor Gray

    # Inbound rule for Detector Webhook port
    $detRuleName = "PrIMA-Detector-Webhook-Port-$DetectorPort"
    Remove-NetFirewallRule -DisplayName $detRuleName -ErrorAction SilentlyContinue
    New-NetFirewallRule -DisplayName $detRuleName `
                        -Direction Inbound `
                        -LocalPort $DetectorPort `
                        -Protocol TCP `
                        -Action Allow `
                        -Description "Allows incoming webhook trigger calls to PrIMA Detector Runner" | Out-Null
    Write-Host "  -> Inbound firewall rule created for TCP Port $DetectorPort" -ForegroundColor Gray

    # 5. Start Services if binaries are present
    Write-Host "`n[4/4] Starting Services..." -ForegroundColor Green
    if (Test-Path $ApiBinaryPath) {
        Start-Service -Name $ApiServiceName
        Write-Host "  -> $ApiServiceName is $($((Get-Service -Name $ApiServiceName).Status))." -ForegroundColor Cyan
    } else {
        Write-Host "  -> Skipping start of $ApiServiceName (binary not yet placed)." -ForegroundColor Yellow
    }

    if (Test-Path $DetectorBinaryPath) {
        Start-Service -Name $DetectorServiceName
        Write-Host "  -> $DetectorServiceName is $($((Get-Service -Name $DetectorServiceName).Status))." -ForegroundColor Cyan
    } else {
        Write-Host "  -> Skipping start of $DetectorServiceName (binary not yet placed)." -ForegroundColor Yellow
    }

    Write-Host "`nInstallation completed successfully." -ForegroundColor Green
}

function Uninstall-PrIMAServices {
    Write-Host "Uninstalling PrIMA Windows Services..." -ForegroundColor Yellow
    
    foreach ($svcName in @($DetectorServiceName, $ApiServiceName)) {
        $svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
        if ($svc) {
            Write-Host "Stopping and deleting $svcName..." -ForegroundColor Gray
            Stop-Service -Name $svcName -Force -ErrorAction SilentlyContinue
            & sc.exe delete $svcName | Out-Null
            Write-Host "Service $svcName removed." -ForegroundColor Green
        }
    }

    Remove-NetFirewallRule -DisplayName "PrIMA-Api-Port-$ApiPort" -ErrorAction SilentlyContinue
    Remove-NetFirewallRule -DisplayName "PrIMA-Detector-Webhook-Port-$DetectorPort" -ErrorAction SilentlyContinue
    Write-Host "Firewall rules removed." -ForegroundColor Green
    Write-Host "Uninstallation complete." -ForegroundColor Green
}

function Show-ServiceStatus {
    Write-Host "`n=== PrIMA Windows Services Status ===" -ForegroundColor Cyan
    foreach ($svcName in @($ApiServiceName, $DetectorServiceName)) {
        $svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
        if ($svc) {
            $color = if ($svc.Status -eq 'Running') { 'Green' } else { 'Red' }
            Write-Host ("{0,-20} : {1}" -f $svc.Name, $svc.Status) -ForegroundColor $color
        } else {
            Write-Host ("{0,-20} : NOT INSTALLED" -f $svcName) -ForegroundColor DarkGray
        }
    }
    Write-Host ""
}

switch ($Action) {
    'Install'   { Install-PrIMAServices }
    'Uninstall' { Uninstall-PrIMAServices }
    'Start'     { 
        Start-Service -Name $ApiServiceName -ErrorAction SilentlyContinue
        Start-Service -Name $DetectorServiceName -ErrorAction SilentlyContinue
        Show-ServiceStatus
    }
    'Stop'      { 
        Stop-Service -Name $DetectorServiceName -Force -ErrorAction SilentlyContinue
        Stop-Service -Name $ApiServiceName -Force -ErrorAction SilentlyContinue
        Show-ServiceStatus
    }
    'Restart'   { 
        Restart-Service -Name $ApiServiceName -Force -ErrorAction SilentlyContinue
        Restart-Service -Name $DetectorServiceName -Force -ErrorAction SilentlyContinue
        Show-ServiceStatus
    }
    'Status'    { Show-ServiceStatus }
}
