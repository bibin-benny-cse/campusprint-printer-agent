<#
==============================================================================
 XeroxGo Agent Setup Manager
 Usage:
   irm https://raw.githubusercontent.com/bibin-benny-cse/campusprint-printer-agent/main/install.ps1 | iex
==============================================================================
#>

param(
    [string]$Action = "",
    [switch]$Uninstall,
    [switch]$Install,
    [switch]$Silent
)

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13
$ErrorActionPreference = 'Stop'

$RepoOwner = "bibin-benny-cse"
$RepoName  = "campusprint-printer-agent"
$ReleaseBaseUrl = "https://github.com/$RepoOwner/$RepoName/releases/latest/download"

function Get-InstalledInfo {
    $uninstallRegKeys = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1",
        "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1"
    )
    $installDir = $null
    $uninstExe = $null

    foreach ($reg in $uninstallRegKeys) {
        if (Test-Path $reg) {
            $props = Get-ItemProperty -Path $reg -ErrorAction SilentlyContinue
            if ($props.InstallLocation -and (Test-Path $props.InstallLocation)) {
                $installDir = $props.InstallLocation
            }
            if ($props.UninstallString) {
                $rawExe = $props.UninstallString.Trim('"')
                if (Test-Path $rawExe) {
                    $uninstExe = $rawExe
                }
            }
        }
    }

    if (-not $installDir) {
        $candidates = @(
            (Join-Path $env:LOCALAPPDATA "Programs\XeroxGo Agent"),
            (Join-Path $env:ProgramFiles "XeroxGo Agent"),
            (Join-Path ${env:ProgramFiles(x86)} "XeroxGo Agent")
        )
        foreach ($cand in $candidates) {
            if ($cand -and (Test-Path (Join-Path $cand "XeroxGo.PrinterAgent.exe"))) {
                $installDir = $cand
                break
            }
        }
    }

    if (-not $uninstExe -and $installDir) {
        $candUninst = Join-Path $installDir "unins000.exe"
        if (Test-Path $candUninst) {
            $uninstExe = $candUninst
        }
    }

    return [PSCustomObject]@{
        InstallDir   = $installDir
        UninstallExe = $uninstExe
    }
}

function Invoke-NativeUninstall {
    Write-Host ""
    Write-Host "XeroxGo Agent Uninstaller" -ForegroundColor Cyan
    Write-Host "-------------------------" -ForegroundColor DarkGray

    $installed = Get-InstalledInfo

    if ($installed.UninstallExe -and (Test-Path $installed.UninstallExe)) {
        Write-Host "Running uninstaller..."
        $args = @("/SILENT", "/VERYSILENT", "/SUPPRESSMSGBOXES")
        Start-Process -FilePath $installed.UninstallExe -ArgumentList $args -Wait
        Start-Sleep -Seconds 1
    } else {
        # Fallback cleanup in case files were manually deleted or corrupted
        Write-Host "Stopping processes..."
        $procs = Get-Process -Name "XeroxGo.PrinterAgent", "XeroxGoAgent-Setup" -ErrorAction SilentlyContinue
        if ($procs) {
            $procs | Stop-Process -Force
            Start-Sleep -Seconds 1
        }

        Write-Host "Cleaning installation directories..."
        $dirs = @(
            $installed.InstallDir,
            (Join-Path $env:LOCALAPPDATA "Programs\XeroxGo Agent"),
            (Join-Path $env:ProgramFiles "XeroxGo Agent"),
            (Join-Path ${env:ProgramFiles(x86)} "XeroxGo Agent"),
            (Join-Path $env:LOCALAPPDATA "XeroxGo")
        ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique

        foreach ($dir in $dirs) {
            Remove-Item -Path $dir -Recurse -Force -ErrorAction SilentlyContinue
        }

        Write-Host "Cleaning registry..."
        $regKeys = @(
            "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1",
            "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{D37F619C-5582-4BC2-91DE-7A419266F3A1}_is1"
        )
        foreach ($rk in $regKeys) {
            if (Test-Path $rk) { Remove-Item -Path $rk -Recurse -Force -ErrorAction SilentlyContinue }
        }
        Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "XeroxGoPrinterAgent" -Force -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path "HKLM:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "XeroxGoPrinterAgent" -Force -ErrorAction SilentlyContinue

        Write-Host "Cleaning certificates..."
        & certutil -user -delstore "TrustedPublisher" "XeroxGo Technologies" 2>$null | Out-Null
        & certutil -user -delstore "Root" "XeroxGo Technologies" 2>$null | Out-Null
    }

    Write-Host ""
    Write-Host "Uninstall complete." -ForegroundColor Green
    Write-Host ""
}

function Invoke-Install {
    Write-Host ""
    Write-Host "XeroxGo Agent Setup" -ForegroundColor Cyan
    Write-Host "-------------------" -ForegroundColor DarkGray

    # 1. Prepare temporary directory
    $tempDir = Join-Path $env:TEMP "XeroxGoInstall"
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }

    $installerPath = Join-Path $tempDir "XeroxGoAgent-Setup.exe"
    $certPath      = Join-Path $tempDir "XeroxGo-Publisher-Certificate.cer"

    # 2. Download installer
    $installerUrl = "$ReleaseBaseUrl/XeroxGoAgent-Setup.exe"
    Write-Host "Downloading installer..."
    try {
        Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
    } catch {
        Write-Host "Error: Failed to download installer from: $installerUrl" -ForegroundColor Red
        return
    }

    # 3. Register publisher certificate for current user
    $certUrl = "$ReleaseBaseUrl/XeroxGo-Publisher-Certificate.cer"
    try {
        Invoke-WebRequest -Uri $certUrl -OutFile $certPath -UseBasicParsing
        if (Get-Command Import-Certificate -ErrorAction SilentlyContinue) {
            Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\TrustedPublisher" | Out-Null
            Import-Certificate -FilePath $certPath -CertStoreLocation "Cert:\CurrentUser\Root" -ErrorAction SilentlyContinue | Out-Null
        } else {
            & certutil -user -addstore "TrustedPublisher" $certPath | Out-Null
        }
    } catch {}

    # 4. Remove Mark of the Web
    if (Get-Command Unblock-File -ErrorAction SilentlyContinue) {
        Unblock-File -Path $installerPath -ErrorAction SilentlyContinue
    }

    # 5. Launch installer
    Write-Host "Starting setup wizard..." -ForegroundColor Green
    Write-Host ""
    $installArgs = if ($Silent) { @("/SILENT", "/VERYSILENT", "/SUPPRESSMSGBOXES") } else { @() }
    Start-Process -FilePath $installerPath -ArgumentList $installArgs
}

# Determine Action
$installed = Get-InstalledInfo

if ($Uninstall -or ($Action -eq 'uninstall') -or ($env:XEROXGO_ACTION -eq 'uninstall') -or ($env:UNINSTALL -eq '1')) {
    Invoke-NativeUninstall
} elseif ($Install -or ($Action -eq 'install')) {
    Invoke-Install
} elseif ($installed.InstallDir) {
    Write-Host ""
    Write-Host "XeroxGo Agent Setup" -ForegroundColor Cyan
    Write-Host "-------------------" -ForegroundColor DarkGray
    Write-Host "An existing installation was found." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  1) Update / Reinstall"
    Write-Host "  2) Uninstall"
    Write-Host "  3) Cancel"
    Write-Host ""
    $choice = Read-Host "Select an option [1-3]"
    switch ($choice) {
        "1" {
            Invoke-Install
        }
        "2" {
            Invoke-NativeUninstall
        }
        default {
            Write-Host "Cancelled." -ForegroundColor DarkGray
        }
    }
} else {
    Invoke-Install
}
