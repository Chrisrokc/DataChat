<#
.SYNOPSIS
  Stops and removes the DataChat Windows Service. Called from Inno Setup's [UninstallRun]
  as a fallback if sc.exe commands alone don't fully clean up.
#>
param(
    [string]$ServiceName = 'DataChat'
)

$ErrorActionPreference = 'SilentlyContinue'
sc.exe stop   $ServiceName | Out-Null
sc.exe delete $ServiceName | Out-Null
netsh advfirewall firewall delete rule name="DataChat HTTP" | Out-Null
exit 0
