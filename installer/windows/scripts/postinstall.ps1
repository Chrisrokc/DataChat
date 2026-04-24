<#
.SYNOPSIS
  Post-install configuration for DataChat.

.DESCRIPTION
  Runs after Inno Setup has copied files. Responsibilities:
    - If DbMode=bundled: download and silent-install SQL Server 2025 Express, create an empty
      DataChat database, write a trusted-connection string to appsettings.Production.json.
    - If DbMode=existing: leave connection string empty so the Setup Wizard prompts on first launch.
    - Ensure appsettings.Production.json exists with the HTTP port configured.

  Errors are logged to <InstallDir>\logs\install.log and do not fail the installer outright —
  the Setup Wizard provides a fallback path for the user.
#>
param(
    [Parameter(Mandatory=$true)] [string]$InstallDir,
    [Parameter(Mandatory=$true)] [ValidateSet('bundled','existing')] [string]$DbMode,
    [int]$HttpPort = 5159
)

$ErrorActionPreference = 'Continue'
$logDir  = Join-Path $InstallDir 'logs'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$logFile = Join-Path $logDir 'install.log'

function Write-Log($msg) {
    $line = "[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date), $msg
    Add-Content -Path $logFile -Value $line
    Write-Host $line
}

Write-Log "postinstall starting (DbMode=$DbMode, HttpPort=$HttpPort, InstallDir=$InstallDir)"

# ---------- Tesseract OCR data ----------
# The Tesseract NuGet package ships the Windows native DLLs alongside the binary,
# but the language model file (eng.traineddata) must be downloaded separately.
# ImageParser.cs looks for it in <InstallDir>\tessdata\.
$tessDir = Join-Path $InstallDir 'tessdata'
$tessFile = Join-Path $tessDir 'eng.traineddata'
if (-not (Test-Path $tessFile)) {
    try {
        New-Item -ItemType Directory -Path $tessDir -Force | Out-Null
        # tessdata_fast: ~4MB, recommended speed/accuracy balance.
        # Swap for tessdata (full, ~23MB) or tessdata_best for higher accuracy.
        $tessUrl = 'https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata'
        Write-Log "Downloading Tesseract English model from $tessUrl"
        Invoke-WebRequest -Uri $tessUrl -OutFile $tessFile -UseBasicParsing
        Write-Log "Tesseract data installed at $tessFile"
    } catch {
        Write-Log "Tesseract data download failed: $($_.Exception.Message). OCR for image documents will be disabled."
    }
} else {
    Write-Log "Tesseract data already present at $tessFile (skipping)"
}

$settingsPath = Join-Path $InstallDir 'appsettings.Production.json'
if (Test-Path $settingsPath) {
    try { $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json } catch { $settings = [pscustomobject]@{} }
} else {
    $settings = [pscustomobject]@{}
}

function Ensure-Property($obj, $name, $value) {
    if (-not ($obj.PSObject.Properties.Name -contains $name)) {
        $obj | Add-Member -NotePropertyName $name -NotePropertyValue $value
    }
    return $obj
}

$settings = Ensure-Property $settings 'ConnectionStrings' ([pscustomobject]@{ DefaultConnection = '' })
$settings = Ensure-Property $settings 'Urls' "http://localhost:$HttpPort"
$settings.Urls = "http://localhost:$HttpPort"

if ($DbMode -eq 'bundled') {
    Write-Log "Bundled DB selected — attempting SQL Server 2025 Express install"
    $sqlExe = Join-Path $env:TEMP 'SQLEXPR_x64_ENU.exe'
    $downloadUrl = 'https://go.microsoft.com/fwlink/?linkid=866658'  # SQL Server Express download bootstrapper
    try {
        if (-not (Test-Path $sqlExe)) {
            Write-Log "Downloading SQL Server Express bootstrapper to $sqlExe"
            Invoke-WebRequest -Uri $downloadUrl -OutFile $sqlExe -UseBasicParsing
        }
        Write-Log "Launching SQL Server Express installer (silent, SQLENGINE feature)"
        $p = Start-Process -FilePath $sqlExe -ArgumentList '/QS','/ACTION=Install','/FEATURES=SQLEngine','/INSTANCENAME=SQLEXPRESS','/SQLSVCACCOUNT=NT AUTHORITY\NETWORK SERVICE','/SQLSYSADMINACCOUNTS=BUILTIN\Administrators','/TCPENABLED=1','/IACCEPTSQLSERVERLICENSETERMS' -Wait -PassThru
        Write-Log "SQL Express installer exit code: $($p.ExitCode)"

        $connStr = "Server=localhost\SQLEXPRESS;Database=DataChat;Trusted_Connection=True;TrustServerCertificate=True;"
        $settings.ConnectionStrings.DefaultConnection = $connStr
        Write-Log "Connection string written: $connStr"
    } catch {
        Write-Log "Bundled SQL install failed: $($_.Exception.Message). User will configure via Setup Wizard."
        $settings.ConnectionStrings.DefaultConnection = ''
    }
} else {
    Write-Log "Existing DB selected — leaving connection string empty; Setup Wizard will prompt"
    $settings.ConnectionStrings.DefaultConnection = ''
}

$json = $settings | ConvertTo-Json -Depth 10
Set-Content -Path $settingsPath -Value $json -Encoding UTF8
Write-Log "Wrote $settingsPath"

Write-Log "postinstall complete"
exit 0
