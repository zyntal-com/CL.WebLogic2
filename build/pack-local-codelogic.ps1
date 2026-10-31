<#
.SYNOPSIS
    Packs the sibling CodeLogic and CodeLogic.Libs working trees into a local NuGet feed so
    WebLogic can build and test against unreleased fixes.

.DESCRIPTION
    Every run uses a fresh version (5.0.0-local.<timestamp>) so the NuGet cache never serves a stale
    copy, and records it in <feed>/version.txt. Build WebLogic against it with
    -p:CodeLogicLocal=true (or the environment variable CodeLogicLocal=true); see Directory.Packages.props.
    The feed lives under artifacts/ (git-ignored). Development only: never release against it.

.EXAMPLE
    ./build/pack-local-codelogic.ps1
    dotnet test -p:CodeLogicLocal=true
#>
param(
    [string]$CodeLogicPath = (Join-Path $PSScriptRoot '../../CodeLogic'),
    [string]$LibsPath = (Join-Path $PSScriptRoot '../../CodeLogic.Libs'),
    [string]$Feed = (Join-Path $PSScriptRoot '../artifacts/codelogic-local')
)

$ErrorActionPreference = 'Stop'

$codeLogicProject = Join-Path $CodeLogicPath 'src/CodeLogic.csproj'
if (-not (Test-Path $codeLogicProject)) { throw "CodeLogic not found at $CodeLogicPath" }
if (-not (Test-Path (Join-Path $LibsPath 'version.txt'))) { throw "CodeLogic.Libs not found at $LibsPath" }

if (Test-Path $Feed) { Remove-Item $Feed -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Feed | Out-Null
$Feed = (Resolve-Path $Feed).Path

$version = "5.0.0-local.$(Get-Date -Format 'yyyyMMddHHmmss')"
$common = @('-c', 'Release', '-o', $Feed, '--nologo', '-v', 'q', "-p:Version=$version", "-p:PackageVersion=$version")

Write-Host "Packing CodeLogic $version"
dotnet pack $codeLogicProject @common
if ($LASTEXITCODE -ne 0) { throw 'Packing CodeLogic failed' }

# Libraries build against the CodeLogic package just packed, not the sibling project, so the
# packages reference each other exactly as published ones do.
$libs = Get-ChildItem $LibsPath -Directory -Filter 'CL.*' |
    ForEach-Object { Join-Path $_.FullName "$($_.Name).csproj" } |
    Where-Object { Test-Path $_ }
foreach ($project in $libs) {
    Write-Host "Packing $(Split-Path $project -Leaf)"
    dotnet pack $project @common -p:CodeLogicFromNuGet=true "-p:CodeLogicPackageVersion=$version" "-p:RestoreAdditionalProjectSources=$Feed"
    if ($LASTEXITCODE -ne 0) { throw "Packing $project failed" }
}

Set-Content -Path (Join-Path $Feed 'version.txt') -Value $version -Encoding ascii
Write-Host "Local feed ready: $Feed ($version). Build with -p:CodeLogicLocal=true."
