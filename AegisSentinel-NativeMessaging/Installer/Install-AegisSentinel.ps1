#Requires -RunAsAdministrator
<#
.SYNOPSIS
    AegisSentinel Silent Installer
    Registers the Native Messaging Host, Chrome policy, and Toast AUMID.

.DESCRIPTION
    Run this script ONCE after building the solution.
    It performs all registry writes needed for Chrome to find and
    launch the C# host automatically.

.PARAMETER ExtensionId
    The Chrome Extension ID (40-character string from chrome://extensions).
    Pass this after loading the unpacked extension in Developer Mode.

.PARAMETER InstallDir
    Target installation directory. Default: C:\Program Files\AegisSentinel

.PARAMETER ApiKey
    OpenAI API key. Stored as a user-scoped environment variable.
    If omitted, you must set AEGIS_OPENAI_API_KEY manually later.

.EXAMPLE
    .\Install-AegisSentinel.ps1 `
        -ExtensionId "abcdefghijklmnopqrstuvwxyz123456" `
        -ApiKey "sk-..."
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[a-p]{32}$')]
    [string]$ExtensionId,

    [string]$InstallDir = "C:\Program Files\AegisSentinel",

    [string]$ApiKey = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$HostName     = "com.aegissentinel.host"
$HostExe      = Join-Path $InstallDir "AegisSentinel.NativeHost.exe"
$ManifestPath = Join-Path $InstallDir "$HostName.json"
$IconPath     = Join-Path $InstallDir "shield.ico"

# ─────────────────────────────────────────────────────────────────────────────
# STEP 0 — Validate install directory
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "`n[AegisSentinel Installer]" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray

if (-not (Test-Path $HostExe)) {
    Write-Error @"
Host executable not found at: $HostExe

Build the project first:
  dotnet publish ./NativeHost/AegisSentinel.NativeHost.csproj `
      -c Release -r win-x64 --self-contained true `
      -p:PublishSingleFile=true -o "$InstallDir"
"@
    exit 1
}

Write-Host "✓ Host executable found:  $HostExe" -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# STEP 1 — Write Native Messaging Host manifest JSON
# ─────────────────────────────────────────────────────────────────────────────
$manifestJson = @"
{
  "name": "$HostName",
  "description": "AegisSentinel Privacy Policy Analyser — Native Messaging Host",
  "path": "$($HostExe -replace '\\', '\\')",
  "type": "stdio",
  "allowed_origins": [
    "chrome-extension://$ExtensionId/"
  ]
}
"@

Set-Content -Path $ManifestPath -Value $manifestJson -Encoding UTF8 -Force
Write-Host "✓ Manifest written:       $ManifestPath" -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# STEP 2 — Register manifest in Windows Registry (HKLM = all users)
# ─────────────────────────────────────────────────────────────────────────────
$regPath = "HKLM:\SOFTWARE\Google\Chrome\NativeMessagingHosts\$HostName"
New-Item -Path $regPath -Force | Out-Null
Set-ItemProperty -Path $regPath -Name "(Default)" -Value $ManifestPath
Write-Host "✓ Registry (Chrome):      $regPath" -ForegroundColor Green

# Also register for Edge (Chromium-based, uses same protocol)
$edgeRegPath = "HKLM:\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\$HostName"
New-Item -Path $edgeRegPath -Force | Out-Null
Set-ItemProperty -Path $edgeRegPath -Name "(Default)" -Value $ManifestPath
Write-Host "✓ Registry (Edge):        $edgeRegPath" -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# STEP 3 — Force-install extension via Chrome Group Policy
# ─────────────────────────────────────────────────────────────────────────────
$policyPath = "HKLM:\SOFTWARE\Policies\Google\Chrome\ExtensionInstallForcelist"
New-Item -Path $policyPath -Force | Out-Null

# Find the next available numeric key
$existingKeys = (Get-ItemProperty -Path $policyPath -ErrorAction SilentlyContinue).PSObject.Properties |
    Where-Object { $_.Name -match '^\d+$' } |
    ForEach-Object { [int]$_.Name }

$nextIndex = if ($existingKeys) { ($existingKeys | Measure-Object -Maximum).Maximum + 1 } else { 1 }

Set-ItemProperty -Path $policyPath -Name "$nextIndex" `
    -Value "$ExtensionId;https://clients2.google.com/service/update2/crx"

Write-Host "✓ Extension policy key:   $policyPath [$nextIndex]" -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# STEP 4 — Register App User Model ID for Toast Notifications
#           (Required for Win32 apps to show OS-level toasts)
# ─────────────────────────────────────────────────────────────────────────────
$aumidPath = "HKCU:\SOFTWARE\Classes\AppUserModelId\$HostName"
New-Item -Path $aumidPath -Force | Out-Null
Set-ItemProperty -Path $aumidPath -Name "DisplayName" -Value "AegisSentinel"
Set-ItemProperty -Path $aumidPath -Name "IconBackgroundColor" -Value "FF1E3A5F"

if (Test-Path $IconPath) {
    Set-ItemProperty -Path $aumidPath -Name "IconUri" -Value $IconPath
}

Write-Host "✓ Toast AUMID registered: $aumidPath" -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# STEP 5 — Store API key as user environment variable (never machine-scope)
# ─────────────────────────────────────────────────────────────────────────────
if ($ApiKey -ne "") {
    [System.Environment]::SetEnvironmentVariable(
        "AEGIS_OPENAI_API_KEY", $ApiKey, [System.EnvironmentVariableTarget]::User)
    Write-Host "✓ API key stored:         AEGIS_OPENAI_API_KEY (User scope)" -ForegroundColor Green
} else {
    Write-Host "⚠ API key NOT set. Run later:" -ForegroundColor Yellow
    Write-Host '    [System.Environment]::SetEnvironmentVariable("AEGIS_OPENAI_API_KEY","sk-...","User")' `
        -ForegroundColor DarkYellow
}

# ─────────────────────────────────────────────────────────────────────────────
# STEP 6 — Add Windows Firewall exception (outbound HTTPS for OpenAI)
# ─────────────────────────────────────────────────────────────────────────────
$fwRuleName = "AegisSentinel Native Host (Outbound HTTPS)"
if (-not (Get-NetFirewallRule -DisplayName $fwRuleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule `
        -DisplayName $fwRuleName `
        -Direction Outbound `
        -Action Allow `
        -Protocol TCP `
        -RemotePort 443 `
        -Program $HostExe `
        -Profile Any | Out-Null
    Write-Host "✓ Firewall rule created:  $fwRuleName" -ForegroundColor Green
} else {
    Write-Host "✓ Firewall rule exists:   $fwRuleName" -ForegroundColor DarkGreen
}

# ─────────────────────────────────────────────────────────────────────────────
# DONE
# ─────────────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor DarkGray
Write-Host "✅  Installation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Restart Chrome (chrome://restart)" -ForegroundColor White
Write-Host "  2. Navigate to any Privacy Policy page" -ForegroundColor White
Write-Host "  3. Watch for the shield badge and Windows Toast notification" -ForegroundColor White
Write-Host ""
Write-Host "Verify the host is reachable:" -ForegroundColor Cyan
Write-Host "  chrome://extensions/ → AegisSentinel → Inspect views: background page" -ForegroundColor White
Write-Host ""

# ─────────────────────────────────────────────────────────────────────────────
# UNINSTALL helper (run with -Uninstall switch)
# ─────────────────────────────────────────────────────────────────────────────
function Uninstall-AegisSentinel {
    Write-Host "`n[Uninstalling AegisSentinel]" -ForegroundColor Yellow

    Remove-Item -Path "HKLM:\SOFTWARE\Google\Chrome\NativeMessagingHosts\$HostName"    -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "HKLM:\SOFTWARE\Microsoft\Edge\NativeMessagingHosts\$HostName"   -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "HKCU:\SOFTWARE\Classes\AppUserModelId\$HostName"                -Recurse -Force -ErrorAction SilentlyContinue
    Remove-NetFirewallRule -DisplayName $fwRuleName -ErrorAction SilentlyContinue
    [System.Environment]::SetEnvironmentVariable("AEGIS_OPENAI_API_KEY", $null, "User")

    Write-Host "✓ Registry keys removed" -ForegroundColor Green
    Write-Host "✓ Firewall rule removed" -ForegroundColor Green
    Write-Host "✓ API key cleared"       -ForegroundColor Green
    Write-Host "`nManually remove: $InstallDir" -ForegroundColor Yellow
}
