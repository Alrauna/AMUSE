[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Temporary workaround for NDMF 1.14.4 packaging Dependencies~ (which Unity ignores)
# instead of a Unity-importable Dependencies directory in standalone projects.
# Upstream context: https://github.com/bdunderscore/ndmf/pull/738
$expectedVersion = '1.14.4'
$expectedFiles = @(
    '0Harmony.dll',
    '0Harmony.dll.meta',
    '0Harmony.LICENSE.txt',
    '0Harmony.LICENSE.txt.meta',
    'System.Buffers.dll',
    'System.Buffers.dll.meta',
    'System.Buffers.LICENSE.TXT',
    'System.Buffers.LICENSE.TXT.meta',
    'System.Collections.Immutable.dll',
    'System.Collections.Immutable.dll.meta',
    'System.Collections.Immutable.LICENSE.TXT',
    'System.Collections.Immutable.LICENSE.TXT.meta',
    'System.Memory.dll',
    'System.Memory.dll.meta',
    'System.Memory.LICENSE.TXT',
    'System.Memory.LICENSE.TXT.meta',
    'System.Numerics.Vectors.dll',
    'System.Numerics.Vectors.dll.meta',
    'System.Numerics.Vectors.LICENSE.TXT',
    'System.Numerics.Vectors.LICENSE.TXT.meta',
    'System.Runtime.CompilerServices.Unsafe.dll',
    'System.Runtime.CompilerServices.Unsafe.dll.meta',
    'System.Runtime.CompilerServices.Unsafe.LICENSE.TXT',
    'System.Runtime.CompilerServices.Unsafe.LICENSE.TXT.meta'
)

function Assert-ExpectedFiles {
    param([Parameter(Mandatory)][string] $Path)

    $items = @(Get-ChildItem -LiteralPath $Path -Force)
    $unsupportedItems = @($items | Where-Object { $_.PSIsContainer -or ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) })
    if ($unsupportedItems.Count -ne 0) {
        throw "Unexpected directory or link in NDMF standalone dependencies: $($unsupportedItems.Name -join ', ')"
    }

    $actualFiles = @($items.Name | Sort-Object)
    $differences = @(Compare-Object -ReferenceObject ($expectedFiles | Sort-Object) -DifferenceObject $actualFiles)
    if ($differences.Count -ne 0) {
        throw "Unexpected NDMF $expectedVersion dependency layout at ${Path}: $($differences | Out-String)"
    }
}

function Assert-IdenticalDirectories {
    param(
        [Parameter(Mandatory)][string] $Source,
        [Parameter(Mandatory)][string] $Destination
    )

    Assert-ExpectedFiles -Path $Destination
    foreach ($fileName in $expectedFiles) {
        $sourceFile = Join-Path $Source $fileName
        $destinationFile = Join-Path $Destination $fileName
        $sourceHash = (Get-FileHash -LiteralPath $sourceFile -Algorithm SHA256).Hash
        $destinationHash = (Get-FileHash -LiteralPath $destinationFile -Algorithm SHA256).Hash
        if ($sourceHash -ne $destinationHash) {
            throw "Generated NDMF dependency is stale or partial: $destinationFile"
        }
    }
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$packageRoot = Join-Path $projectRoot 'Packages/nadena.dev.ndmf'
$packageManifestPath = Join-Path $packageRoot 'package.json'
$sourcePath = Join-Path $packageRoot 'Dependencies~'
$destinationPath = Join-Path $packageRoot 'Dependencies'
$stagingPath = Join-Path $packageRoot 'Dependencies.bootstrap-tmp'

if (-not (Test-Path -LiteralPath $packageManifestPath -PathType Leaf)) {
    throw "Resolved NDMF package not found at $packageRoot. Restore VPM dependencies first."
}

$packageManifest = Get-Content -LiteralPath $packageManifestPath -Raw | ConvertFrom-Json
if ($packageManifest.name -ne 'nadena.dev.ndmf' -or $packageManifest.version -ne $expectedVersion) {
    throw "Expected resolved nadena.dev.ndmf $expectedVersion, found '$($packageManifest.name)' '$($packageManifest.version)'."
}

if (-not (Test-Path -LiteralPath $sourcePath -PathType Container)) {
    throw "Expected NDMF standalone dependency source is missing: $sourcePath"
}

$sourceItem = Get-Item -LiteralPath $sourcePath -Force
if ($sourceItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
    throw "NDMF standalone dependency source must be a real directory: $sourcePath"
}
Assert-ExpectedFiles -Path $sourcePath

if (Test-Path -LiteralPath $stagingPath) {
    throw "Stale NDMF bootstrap staging state exists: $stagingPath"
}

if (Test-Path -LiteralPath $destinationPath) {
    $destinationItem = Get-Item -LiteralPath $destinationPath -Force
    if (-not $destinationItem.PSIsContainer -or ($destinationItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw "Generated NDMF Dependencies must be a real directory, not a file or link: $destinationPath"
    }

    Assert-IdenticalDirectories -Source $sourcePath -Destination $destinationPath
    Write-Output "NDMF $expectedVersion standalone dependencies are already bootstrapped."
    exit 0
}

$createdStaging = $false
try {
    New-Item -ItemType Directory -Path $stagingPath | Out-Null
    $createdStaging = $true
    foreach ($fileName in $expectedFiles) {
        Copy-Item -LiteralPath (Join-Path $sourcePath $fileName) -Destination $stagingPath
    }
    Assert-IdenticalDirectories -Source $sourcePath -Destination $stagingPath
    Move-Item -LiteralPath $stagingPath -Destination $destinationPath
    $createdStaging = $false
} catch {
    if ($createdStaging -and (Test-Path -LiteralPath $stagingPath)) {
        Remove-Item -LiteralPath $stagingPath -Recurse -Force
    }
    throw
}

Assert-IdenticalDirectories -Source $sourcePath -Destination $destinationPath
Write-Output "Bootstrapped NDMF $expectedVersion standalone dependencies at $destinationPath."
