# Compares the committed lock file with the repository state. No network.
#
# Dot-source this file to get Test-LockMatchesManifest. Run the file directly
# to check the repository files. The direct run exits 1 and prints one line
# per difference. A matching lock prints nothing and exits 0.
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Common.ps1')

# Returns one line per difference. An empty list means the lock matches.
# The check compares:
# - every locked package name and version,
# - the Unity version from the project version file,
# - the null rule: a null hash needs a reason, and only com.vrcfury.vrcfury
#   may hold one (spec O3),
# - that every locked name maps to a source key that exists in sources.json.
function Test-LockMatchesManifest {
    param(
        [string]$LockPath,
        [string]$ManifestPath,
        [string]$VersionFilePath,
        [string]$SourcesPath
    )
    $differences = @()

    $lock = Get-Content -Path $LockPath -Raw | ConvertFrom-Json
    $sources = Get-Content -Path $SourcesPath -Raw | ConvertFrom-Json
    $manifest = Get-Content -Path $ManifestPath -Raw | ConvertFrom-Json

    $versionLine = Select-String -Path $VersionFilePath -Pattern '^m_EditorVersion: (.+)$'
    if (-not $versionLine -or $versionLine.Matches.Count -lt 1) {
        throw "The project version file holds no editor version: $VersionFilePath"
    }
    $projectVersion = $versionLine.Matches[0].Groups[1].Value.Trim()
    if ([string]$lock.unity.version -ne $projectVersion) {
        $differences += "unity version differs: lock $($lock.unity.version), project $projectVersion"
    }

    $lockNames = @($lock.packages.PSObject.Properties | ForEach-Object { $_.Name })
    $locked = @($manifest.locked.PSObject.Properties)
    $lockedNames = @($locked | ForEach-Object { $_.Name })

    foreach ($name in $lockedNames) {
        if ($lockNames -notcontains $name) {
            $differences += "package is missing from the lock: $name"
            continue
        }
        $lockEntry = $lock.packages.PSObject.Properties[$name].Value
        $manifestEntry = ($locked | Where-Object { $_.Name -eq $name } | Select-Object -First 1).Value
        if ([string]$lockEntry.version -ne [string]$manifestEntry.version) {
            $differences += "version differs for ${name}: lock $($lockEntry.version), manifest $($manifestEntry.version)"
        }

        $sourceProperty = $sources.packages.PSObject.Properties[$name]
        if (-not $sourceProperty) {
            $differences += "no source key is named for the package: $name"
        }
        elseif (-not ($sources.sources.PSObject.Properties[$sourceProperty.Value])) {
            $differences += "the source key of the package is not in sources: $name, key $($sourceProperty.Value)"
        }

        $reasonProperty = $lockEntry.PSObject.Properties['unhashedReason']
        $hasReason = $reasonProperty -and $reasonProperty.Value
        if ($null -eq $lockEntry.sha256) {
            if (-not $hasReason) {
                $differences += "null hash without a reason: $name"
            }
            elseif ($name -ne 'com.vrcfury.vrcfury') {
                $differences += "a null hash is allowed only for com.vrcfury.vrcfury: $name"
            }
        }
        elseif ($hasReason) {
            $differences += "a hash and a reason are both set: $name"
        }
    }

    foreach ($name in $lockNames) {
        if ($lockedNames -notcontains $name) {
            $differences += "package is in the lock but not in the manifest: $name"
        }
    }

    return @($differences)
}

if ($MyInvocation.InvocationName -ne '.') {
    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    $repoLock = Join-Path $PSScriptRoot 'lock.json'
    if (-not (Test-Path $repoLock)) {
        Say 'lock check failed: the lock file is missing.'
        exit 1
    }
    $repoSources = Join-Path $PSScriptRoot 'sources.json'
    $repoManifest = Join-Path $PSScriptRoot '..\..\Packages\vpm-manifest.json'
    $repoVersionFile = Join-Path $PSScriptRoot '..\..\ProjectSettings\ProjectVersion.txt'
    $found = @(Test-LockMatchesManifest -LockPath $repoLock -ManifestPath $repoManifest -VersionFilePath $repoVersionFile -SourcesPath $repoSources)
    if ($found.Count -gt 0) {
        Say "lock check failed with $($found.Count) difference(s):"
        foreach ($line in $found) { Say (Redact $line) }
        exit 1
    }
    Say 'lock check passed: the lock matches the repository.'
    exit 0
}
