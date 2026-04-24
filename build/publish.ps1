<#
.SYNOPSIS
  Publishes self-contained single-file builds of DataChat.Web.

.DESCRIPTION
  Defaults to win-x64 (what the Windows installer consumes). Pass other RIDs to cross-publish.

.EXAMPLE
  pwsh build/publish.ps1
  pwsh build/publish.ps1 -Rids win-x64,osx-arm64
#>
param(
    [string[]]$Rids = @('win-x64')
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$project  = Join-Path $repoRoot 'src/Presentation/DataChat.Web/DataChat.Web.csproj'
$outBase  = Join-Path $repoRoot 'build/out'

Write-Host "==> Publishing DataChat.Web for RIDs: $($Rids -join ', ')"

foreach ($rid in $Rids) {
    $outDir = Join-Path $outBase $rid
    Write-Host "--> $rid -> $outDir"
    if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }

    dotnet publish $project `
        -c Release `
        -r $rid `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=embedded `
        -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "Publish failed for $rid" }
}

Write-Host "==> Done. Artifacts in $outBase"
