# Human run. Builds a new lock file from the publisher listings and the Unity
# release API. It needs the network. It writes a file only when -Out names one.
#
# Hash routes per package, in order:
# 1. The zipSHA256 in the listing of the package source.
# 2. The sha256 digest of the release asset on GitHub.
# 3. No route. The lock then holds a null hash with the reason, and the
#    observed hash is printed for the maintainer. Only com.vrcfury.vrcfury
#    has no route today (spec O3).
#
# The licensing client subject is not readable here. The client ships inside
# an installed Editor, and this script downloads the installer only. The lock
# therefore stores the installer signer subject for both subject values, and
# the maintainer confirms the client subject after an Editor install.
param(
    [string]$Out
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
. (Join-Path $PSScriptRoot 'lib/Common.ps1')

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$work = Join-Path ([IO.Path]::GetTempPath()) "amuse-new-lock-$PID"
New-Item -ItemType Directory -Path $work -Force | Out-Null

# Some listings start with a byte order mark. Invoke-RestMethod would then
# return a string, so the raw body is decoded first.
function Get-Json([string]$uri) {
    $body = (Invoke-WebRequest -Uri $uri).Content
    if ($body -is [byte[]]) { $body = [Text.Encoding]::UTF8.GetString($body) }
    return ([string]$body).TrimStart([char]0xFEFF) | ConvertFrom-Json
}

function Get-UnityVersion {
    $line = Select-String -Path (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$'
    if (-not $line -or $line.Matches.Count -lt 1) {
        throw 'The project version file holds no editor version.'
    }
    return $line.Matches[0].Groups[1].Value.Trim()
}

# Builds the Unity part. Downloads the installer, cross-checks the md5 with
# the release API, computes the SHA-256 and reads the signer subject.
function New-UnityLock {
    $version = Get-UnityVersion
    $release = Invoke-RestMethod -Uri "https://services.api.unity.com/unity/editor/release/v1/releases?version=$version&limit=1"
    $result = $release.results | Select-Object -First 1
    if (-not $result) { throw "The release API lists no release for $version." }
    $download = $result.downloads | Where-Object { $_.platform -eq 'WINDOWS' -and $_.type -eq 'EXE' } | Select-Object -First 1
    if (-not $download) { throw "The release API lists no Windows installer for $version." }
    if (-not $download.url.StartsWith('https://download.unity3d.com/')) {
        throw "The installer download host is unexpected: $($download.url)"
    }
    if ($download.integrity -notmatch '^md5-(.+)$') {
        throw 'The release API gave no md5 integrity value.'
    }
    $expectedMd5 = [Text.Encoding]::ASCII.GetString([Convert]::FromBase64String($Matches[1])).ToLowerInvariant()

    $installer = Join-Path $work 'UnitySetup64.exe'
    Invoke-WebRequest -Uri $download.url -OutFile $installer
    $actualMd5 = (Get-FileHash -Algorithm MD5 -Path $installer).Hash.ToLowerInvariant()
    $actualSha256 = (Get-FileHash -Algorithm SHA256 -Path $installer).Hash.ToLowerInvariant()
    $signature = Get-AuthenticodeSignature -FilePath $installer
    Remove-Item -Force $installer

    Say "unity installer for $version"
    Say "- md5 equals the release API value: $($actualMd5 -eq $expectedMd5)"
    Say "- observed installer sha256: $actualSha256"
    Say "- Authenticode status: $($signature.Status)"
    Say "- installer signer subject: $($signature.SignerCertificate.Subject)"
    Say '- the licensing client subject needs an installed Editor. The installer subject stands in for it.'
    if ($actualMd5 -ne $expectedMd5) { throw 'Installer md5 mismatch.' }
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Unity Technologies') {
        throw 'Installer signature is not a valid Unity Technologies signature.'
    }
    return [ordered]@{
        version = $version
        installerSha256 = $actualSha256
        signerSubject = $signature.SignerCertificate.Subject
    }
}

# Finds the listing entry for one package version. Fails closed when the
# source key, the listing or the version is missing.
function Get-ListingEntry([string]$name, [string]$version, [string]$sourceKey, $listings) {
    $property = $listings[$sourceKey].packages.PSObject.Properties[$name]
    if ($property) {
        $versionProperty = $property.Value.versions.PSObject.Properties[$version]
        if ($versionProperty) { return $versionProperty.Value }
    }
    throw "$name $version is not in its listing $sourceKey."
}

# Picks the publisher hash for one entry. Returns the expected hash and the
# route name. An empty expected hash means that no route exists.
function Get-PublisherHash($entry, [string]$name) {
    if ($entry.PSObject.Properties['zipSHA256'] -and $entry.zipSHA256) {
        return @{ Hash = $entry.zipSHA256.ToLowerInvariant(); Route = 'listing zipSHA256' }
    }
    if ($entry.PSObject.Properties['url'] -and $entry.url -match '^https://github\.com/([^/]+)/([^/]+)/releases/download/([^/]+)/([^?]+)') {
        $owner = $Matches[1]
        $repo = $Matches[2]
        $tag = $Matches[3]
        $file = $Matches[4]
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repo/releases/tags/$tag" -Headers @{ 'User-Agent' = 'amuse-ci-new-lock' }
        # One asset object, not an array. The digest test goes through the
        # member name list, so it works on classic and adapter-backed JSON
        # objects alike.
        $asset = $release.assets | Where-Object { $_.name -eq $file } | Select-Object -First 1
        if ($asset -and @($asset.PSObject.Properties.Name) -contains 'digest' -and ([string]$asset.digest -match '^sha256:(.+)$')) {
            return @{ Hash = $Matches[1].ToLowerInvariant(); Route = 'GitHub release digest' }
        }
    }
    Write-Host (Redact "- no publisher hash route found for $name")
    return @{ Hash = $null; Route = 'none' }
}

function New-PackageLock {
    $sources = Get-Content -Path (Join-Path $PSScriptRoot 'sources.json') -Raw | ConvertFrom-Json
    $manifest = Get-Content -Path (Join-Path $root 'Packages/vpm-manifest.json') -Raw | ConvertFrom-Json
    $locked = @($manifest.locked.PSObject.Properties)

    $listings = @{}
    foreach ($property in $sources.sources.PSObject.Properties) {
        $listings[$property.Name] = Get-Json $property.Value
    }

    $packages = [ordered]@{}
    foreach ($entry in $locked) {
        $name = $entry.Name
        $version = [string]$entry.Value.version
        $sourceProperty = $sources.packages.PSObject.Properties[$name]
        if (-not $sourceProperty) { throw "no source key is named for the package: $name" }
        $listingEntry = Get-ListingEntry $name $version $sourceProperty.Value $listings
        $route = Get-PublisherHash $listingEntry $name

        $zip = Join-Path $work "$name.zip"
        Invoke-WebRequest -Uri $listingEntry.url -OutFile $zip
        $observed = (Get-FileHash -Algorithm SHA256 -Path $zip).Hash.ToLowerInvariant()
        Remove-Item -Force $zip

        if ($route.Hash) {
            if ($observed -ne $route.Hash) { throw "hash mismatch for $name $version on route $($route.Route)." }
            Say "- $name $version, route $($route.Route), sha256 $observed"
            $packages[$name] = [ordered]@{ version = $version; sha256 = $observed }
        }
        else {
            Say "- $name $version, no hash route, observed sha256 $observed. The lock holds a null hash for it."
            $packages[$name] = [ordered]@{ version = $version; sha256 = $null; unhashedReason = 'no hash route' }
        }
    }
    return $packages
}

# JSON objects from ConvertFrom-Json and ordered dictionaries from this
# script need different member access. These two helpers work for both.
function Get-MapNames($map) {
    if ($map -is [System.Collections.IDictionary]) { return @($map.Keys) }
    return @($map.PSObject.Properties | ForEach-Object { $_.Name })
}

function Get-MapValue($map, [string]$name) {
    if ($map -is [System.Collections.IDictionary]) { return $map[$name] }
    $property = $map.PSObject.Properties[$name]
    if ($property) { return $property.Value }
    return $null
}

# Prints the changes between the committed lock and the new lock. Hash values
# are printed above by the builders, so the diff names the fields only.
function Show-LockDiff($newLock, $committedLock) {
    if (-not $committedLock) {
        Say 'no committed lock was found. Nothing to diff against.'
        return
    }
    $changes = @()
    foreach ($field in @('version', 'installerSha256', 'signerSubject')) {
        if ([string](Get-MapValue $newLock.unity $field) -ne [string](Get-MapValue $committedLock.unity $field)) {
            $changes += "unity.$field changed"
        }
    }
    if ([string]$newLock.licensingClientSubject -ne [string]$committedLock.licensingClientSubject) {
        $changes += 'licensingClientSubject changed'
    }
    $newNames = @(Get-MapNames $newLock.packages)
    $oldNames = @(Get-MapNames $committedLock.packages)
    foreach ($name in $newNames) {
        if ($oldNames -notcontains $name) { $changes += "package added: $name"; continue }
        $newEntry = Get-MapValue $newLock.packages $name
        $oldEntry = Get-MapValue $committedLock.packages $name
        if ([string](Get-MapValue $newEntry 'version') -ne [string](Get-MapValue $oldEntry 'version')) {
            $changes += "version changed for ${name}: $(Get-MapValue $oldEntry 'version') to $(Get-MapValue $newEntry 'version')"
        }
        if ([string](Get-MapValue $newEntry 'sha256') -ne [string](Get-MapValue $oldEntry 'sha256')) {
            $changes += "sha256 changed for $name"
        }
        $newReason = [string](Get-MapValue $newEntry 'unhashedReason')
        $oldReason = [string](Get-MapValue $oldEntry 'unhashedReason')
        if ($newReason -ne $oldReason) { $changes += "unhashedReason changed for $name" }
    }
    foreach ($name in $oldNames) {
        if ($newNames -notcontains $name) { $changes += "package removed: $name" }
    }
    if ($changes.Count -lt 1) {
        Say 'the new lock equals the committed lock.'
        return
    }
    Say "diff against the committed lock, $($changes.Count) change(s):"
    foreach ($line in $changes) { Say (Redact $line) }
}

try {
    $unity = New-UnityLock
    $packages = New-PackageLock
    $newLock = [ordered]@{
        unity = $unity
        licensingClientSubject = $unity.signerSubject
        packages = $packages
    }

    $committedPath = Join-Path $PSScriptRoot 'lock.json'
    $committedLock = $null
    if (Test-Path $committedPath) {
        $committedLock = Get-Content -Path $committedPath -Raw | ConvertFrom-Json
    }
    Show-LockDiff $newLock $committedLock

    if ($Out) {
        $json = ConvertTo-Json -InputObject $newLock -Depth 6
        Set-Content -Path $Out -Value $json -Encoding utf8
        Say "wrote the lock to $Out"
    }
    else {
        Say 'no -Out path was given. No file was written.'
    }
}
finally {
    Remove-Item -LiteralPath $work -Recurse -Force -ErrorAction SilentlyContinue
}
