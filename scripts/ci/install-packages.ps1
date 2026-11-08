# Installs the vendor packages into a Unity project.
#
# Modes:
# - gate: the package list, the versions and the expected hashes come from
#   lock.json. The download url comes from the package named listing for the
#   locked version. The gate never reads a hash from a listing or from GitHub.
# - canary: -PlanPath names a plan file from the canary planner. The script
#   reads name, new and url from each plan entry. The expected hash comes
#   from the publisher at run time: the listing zipSHA256, else the release
#   asset digest on GitHub. A package with no publisher hash is allowed and
#   its observed SHA-256 is printed.
#
# Dot-source this file to get its functions. Run it directly to install.
# Zips are downloaded into RUNNER_TEMP, or into the system temp folder when
# RUNNER_TEMP is not set. Each zip is deleted after extraction.
param(
    [ValidateSet('gate', 'canary')]
    [string]$Mode = '',
    [string]$PlanPath = '',
    [string]$ProjectPath = ''
)

Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Common.ps1')

# Some listings start with a byte order mark. Invoke-RestMethod would then
# return a string, so the raw body is decoded first. Moved from new-lock.ps1.
function Get-Json([string]$uri) {
    $body = (Invoke-WithRetry { Invoke-WebRequest -Uri $uri }).Content
    if ($body -is [byte[]]) { $body = [Text.Encoding]::UTF8.GetString($body) }
    return ([string]$body).TrimStart([char]0xFEFF) | ConvertFrom-Json
}

# Returns the entry of one package version in a listing, or null when the
# name or the version is not in the listing.
function Resolve-ListingEntry($listing, [string]$name, [string]$version) {
    $packagesProperty = $listing.PSObject.Properties['packages']
    if (-not $packagesProperty) { return $null }
    $packageProperty = $packagesProperty.Value.PSObject.Properties[$name]
    if (-not $packageProperty) { return $null }
    $versionsProperty = $packageProperty.Value.PSObject.Properties['versions']
    if (-not $versionsProperty) { return $null }
    $versionProperty = $versionsProperty.Value.PSObject.Properties[$version]
    if (-not $versionProperty) { return $null }
    return $versionProperty.Value
}

# Same as Resolve-ListingEntry but stops the job when the entry is missing.
# The message names the package so a vendor regression is easy to see. The
# message can hold vendor text, so it passes through Redact.
function Get-ListingEntryOrThrow($listing, [string]$name, [string]$version) {
    $entry = Resolve-ListingEntry $listing $name $version
    if (-not $entry) { throw (Redact "$name $version is not in its own named listing.") }
    return $entry
}

# Returns the source key that sources.json names for one package. Stops when
# no key is named, so an unknown package can never pick a random listing.
function Get-PackageSourceKey([string]$name, $sources) {
    $property = $sources.packages.PSObject.Properties[$name]
    if (-not $property) { throw (Redact "No source key is named for the package: $name") }
    return [string]$property.Value
}

# Returns the listing of one source key. Listings are downloaded once and
# cached in the hashtable that the caller holds.
function Get-ListingFromCache([string]$key, $sources, $cache) {
    if (-not $cache.ContainsKey($key)) {
        $urlProperty = $sources.sources.PSObject.Properties[$key]
        if (-not $urlProperty) { throw (Redact "No listing url is named for the source key: $key") }
        $cache[$key] = Get-Json ([string]$urlProperty.Value)
    }
    return $cache[$key]
}

# Picks the publisher hash for one listing entry. Returns the expected hash
# and the route name. A null hash means that no route exists. The listing
# route wins over the GitHub route, so the release API is only called when
# the entry holds no zipSHA256. Moved from new-lock.ps1.
function Get-PublisherHash($entry) {
    $zipProperty = $entry.PSObject.Properties['zipSHA256']
    if ($zipProperty -and ([string]$zipProperty.Value)) {
        return @{ Hash = ([string]$zipProperty.Value).ToLowerInvariant(); Route = 'listing zipSHA256' }
    }
    $urlProperty = $entry.PSObject.Properties['url']
    if ($urlProperty -and ([string]$urlProperty.Value) -match '^https://github\.com/([^/]+)/([^/]+)/releases/download/([^/]+)/([^?]+)') {
        $owner = $Matches[1]
        $repo = $Matches[2]
        $tag = $Matches[3]
        $file = $Matches[4]
        $release = Invoke-WithRetry { Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repo/releases/tags/$tag" -Headers @{ 'User-Agent' = 'amuse-ci-install-packages' } }
        $asset = $release.assets | Where-Object { $_.name -eq $file } | Select-Object -First 1
        if ($asset -and @($asset.PSObject.Properties.Name) -contains 'digest' -and ([string]$asset.digest -match '^sha256:(.+)$')) {
            return @{ Hash = $Matches[1].ToLowerInvariant(); Route = 'GitHub release digest' }
        }
    }
    return @{ Hash = $null; Route = 'none' }
}

# Reads the package entries of the lock file for gate mode. Returns one
# object per package with the name, the version and the expected hash. An
# entry with no hash and no reason stops the job. An entry with a null hash
# and a reason keeps a null expected hash, so the observed hash gets printed.
function Get-GatePackages($lock) {
    $packagesProperty = $lock.PSObject.Properties['packages']
    if (-not $packagesProperty) { throw 'The lock file has no packages section.' }
    $result = @()
    foreach ($property in @($packagesProperty.Value.PSObject.Properties)) {
        $name = $property.Name
        $entry = $property.Value
        $versionProperty = $entry.PSObject.Properties['version']
        if (-not $versionProperty -or -not ([string]$versionProperty.Value)) {
            throw (Redact "The lock entry has no version for $name.")
        }
        $expected = $null
        $hashProperty = $entry.PSObject.Properties['sha256']
        if ($hashProperty -and ([string]$hashProperty.Value)) {
            $expected = ([string]$hashProperty.Value).ToLowerInvariant()
        }
        if (-not $expected) {
            $reasonProperty = $entry.PSObject.Properties['unhashedReason']
            if (-not $reasonProperty -or -not ([string]$reasonProperty.Value)) {
                throw (Redact "The lock entry for $name has no hash and no unhashedReason.")
            }
        }
        $result += [pscustomobject]@{
            Name = $name
            Version = [string]$versionProperty.Value
            ExpectedHash = $expected
        }
    }
    return $result
}

# Builds one row of the result table. The identifier columns can hold text
# from the lock, the plan or a vendor listing, so they pass through Redact.
# The verified cell is added after Redact, because it holds the observed
# hash of a null-hash package and the log must keep that hash readable.
function New-PackageRow([string]$name, [string]$version, [string]$sourceKey, [string]$route, [string]$verified) {
    $prefix = Redact "| $name | $version | $sourceKey | $route | "
    return "$prefix$verified |"
}

# Redacts one failure line but keeps a plain SHA-256 shape readable. Only a
# run of exactly 64 lowercase hex digits passes through, because that is the
# shape of the zip hashes that the maintainer compares on a mismatch. Every
# other part of the line goes through Redact.
function Redact-KeepingHash([string]$line) {
    $hashes = @([regex]::Matches($line, '(?<![0-9a-fA-F])[0-9a-f]{64}(?![0-9a-fA-F])') | ForEach-Object { $_.Value })
    $masked = $line
    for ($i = 0; $i -lt $hashes.Count; $i++) {
        $masked = $masked.Replace($hashes[$i], "[[amuse-hash$i]]")
    }
    $redacted = Redact $masked
    for ($i = 0; $i -lt $hashes.Count; $i++) {
        $redacted = $redacted.Replace("[[amuse-hash$i]]", $hashes[$i])
    }
    return $redacted
}

# Computes the SHA-256 of a downloaded zip and compares it with the expected
# hash. Stops with the package name on a mismatch. An empty expected hash
# means that the caller has no route, so the observed hash is returned for
# the log and nothing is compared. The mismatch message keeps a hash that
# matches the plain SHA-256 shape readable, so the maintainer can compare
# the two values in the public log. Any other text in the message is
# redacted, and the failure line is redacted again in the loop catch.
function Assert-ZipHash([string]$path, [string]$expected, [string]$name) {
    if (-not (Test-Path -LiteralPath $path)) { throw (Redact "The downloaded zip is missing for $name.") }
    $observed = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    if ($expected -and $observed -ne $expected.ToLowerInvariant()) {
        $expectedShape = if ($expected -cmatch '^[0-9a-f]{64}$') { $expected } else { '<redacted>' }
        $observedShape = if ($observed -cmatch '^[0-9a-f]{64}$') { $observed } else { '<redacted>' }
        throw "hash mismatch for $(Redact $name): expected $expectedShape, observed $observedShape"
    }
    return $observed
}

# Builds the verified cell of the result table. The caller only reaches the
# yes case after the hash check passed. A null expected hash keeps the
# observed hash in the log, so the hash stays visible for the maintainer.
function Get-VerifiedCell([string]$observed, [string]$expected) {
    if (-not $expected) { return "no hash available, observed $observed" }
    return 'yes'
}

# Reads the vpmDependencies of every installed package.json under one
# packages folder. Returns one line per dependency that is outside the
# allowed names, shaped as "needing package needs dependency".
function Find-MissingDependencies([string]$packagesDir, [string[]]$allowedNames) {
    $missing = @()
    if (-not (Test-Path -LiteralPath $packagesDir)) { return $missing }
    foreach ($dir in @(Get-ChildItem -LiteralPath $packagesDir -Directory)) {
        $packageJsonPath = Join-Path $dir.FullName 'package.json'
        if (-not (Test-Path -LiteralPath $packageJsonPath)) { continue }
        $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
        $needing = $dir.Name
        $nameProperty = $packageJson.PSObject.Properties['name']
        if ($nameProperty -and ([string]$nameProperty.Value)) { $needing = [string]$nameProperty.Value }
        $vpmProperty = $packageJson.PSObject.Properties['vpmDependencies']
        if (-not $vpmProperty) { continue }
        foreach ($dependency in @($vpmProperty.Value.PSObject.Properties | ForEach-Object { $_.Name })) {
            if ($allowedNames -notcontains $dependency) { $missing += "$needing needs $dependency" }
        }
    }
    return $missing
}

# Collects the missing-dependency lines for the failure list. The lines hold
# names from package.json files inside vendor zips, so every line passes
# through Redact before it is stored or printed.
function Get-DependencyFailures([string]$packagesDir, [string[]]$allowedNames) {
    $failures = @()
    foreach ($line in @(Find-MissingDependencies -PackagesDir $packagesDir -AllowedNames $allowedNames)) {
        $failures += Redact $line
    }
    return $failures
}

# Removes the git-fetched Unity MCP entry and the Linux toolchain entries
# from one Unity manifest file. The CI project must not fetch them. Returns
# the removed names. The file is written only when something was removed.
function Edit-UnityManifest([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { throw 'The Unity manifest file is missing.' }
    $manifest = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $dependenciesProperty = $manifest.PSObject.Properties['dependencies']
    if (-not $dependenciesProperty) { throw 'The Unity manifest has no dependencies section.' }
    $removed = @()
    foreach ($name in @($dependenciesProperty.Value.PSObject.Properties | ForEach-Object { $_.Name })) {
        if ($name -eq 'com.coplaydev.unity-mcp' -or $name -like 'com.unity.toolchain.*') {
            $dependenciesProperty.Value.PSObject.Properties.Remove($name)
            $removed += $name
        }
    }
    if ($removed.Count -gt 0) {
        $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $path -Encoding utf8
    }
    return $removed
}

# Extracts one package zip into the Packages folder of the project and
# deletes the zip afterwards. A folder of the same name is replaced first,
# so no stale file survives a reinstall.
function Install-ExtractedZip([string]$zip, [string]$name, [string]$projectPath) {
    $target = Join-Path $projectPath "Packages/$name"
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    Expand-Archive -LiteralPath $zip -DestinationPath $target -Force
    Remove-Item -LiteralPath $zip -Force
}

if ($MyInvocation.InvocationName -ne '.') {
    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'

    if ($Mode -ne 'gate' -and $Mode -ne 'canary') { throw 'The mode is required. Use gate or canary.' }
    if ($Mode -eq 'canary' -and -not $PlanPath) { throw 'Canary mode needs a plan file. Pass -PlanPath.' }

    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    if (-not $ProjectPath) { $ProjectPath = $repoRoot }
    if (-not (Test-Path -LiteralPath $ProjectPath)) { throw "The project path does not exist: $ProjectPath" }

    $sourcesPath = Join-Path $PSScriptRoot 'sources.json'
    $sources = Get-Content -LiteralPath $sourcesPath -Raw | ConvertFrom-Json

    $plan = @()
    if ($Mode -eq 'gate') {
        $lockPath = Join-Path $PSScriptRoot 'lock.json'
        if (-not (Test-Path -LiteralPath $lockPath)) { throw 'The lock file is missing.' }
        $lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
        $plan = @(Get-GatePackages $lock)
    }
    else {
        if (-not (Test-Path -LiteralPath $PlanPath)) { throw 'The plan file is missing.' }
        $planJson = Get-Content -LiteralPath $PlanPath -Raw | ConvertFrom-Json
        $planProperty = $planJson.PSObject.Properties['packages']
        if (-not $planProperty) { throw 'The plan file has no packages section.' }
        $plan = @($planProperty.Value | ForEach-Object {
            [pscustomobject]@{
                Name = [string]$_.name
                Version = [string]$_.new
                Url = [string]$_.url
                ExpectedHash = $null
            }
        })
    }
    $allowedNames = @($plan | ForEach-Object { $_.Name })

    $tempBase = [string]$env:RUNNER_TEMP
    if ([string]::IsNullOrEmpty($tempBase)) { $tempBase = [IO.Path]::GetTempPath() }
    $workDir = Join-Path $tempBase "amuse-install-packages-$PID"
    New-Item -ItemType Directory -Path $workDir -Force | Out-Null

    $listings = @{}
    $failures = @()
    try {
        Say '| package | version | source | hash route | verified |'
        Say '| --- | --- | --- | --- | --- |'
        foreach ($item in $plan) {
            $name = $item.Name
            $version = $item.Version
            $sourceKey = 'none'
            $route = 'none'
            try {
                $sourceKey = Get-PackageSourceKey $name $sources
                $listing = Get-ListingFromCache $sourceKey $sources $listings
                $expected = $item.ExpectedHash
                if ($Mode -eq 'gate') {
                    # The lock holds the expected hash. The listing is read
                    # for the download url only, never for a hash.
                    $entry = Get-ListingEntryOrThrow $listing $name $version
                    $urlProperty = $entry.PSObject.Properties['url']
                    if (-not $urlProperty -or -not ([string]$urlProperty.Value)) {
                        throw (Redact "$name $version has no download url in its listing entry.")
                    }
                    $url = [string]$urlProperty.Value
                    $route = 'lock'
                }
                else {
                    $url = $item.Url
                    if (-not $url) { throw (Redact "$name $version has no download url in the plan.") }
                    $entry = Resolve-ListingEntry $listing $name $version
                    if (-not $entry) { $entry = [pscustomobject]@{ url = $url } }
                    $publisher = Get-PublisherHash $entry
                    $expected = $publisher.Hash
                    $route = $publisher.Route
                }
                $zip = Join-Path $workDir "$name.zip"
                Invoke-WithRetry { Invoke-WebRequest -Uri $url -OutFile $zip }
                $observed = Assert-ZipHash -Path $zip -Expected $expected -Name $name
                $verified = Get-VerifiedCell $observed $expected
                Say (New-PackageRow $name $version $sourceKey $route $verified)
                Install-ExtractedZip -Zip $zip -Name $name -ProjectPath $ProjectPath
            }
            catch {
                $failures += Redact-KeepingHash "$name $version failed: $($_.Exception.Message)"
                Say (New-PackageRow $name $version $sourceKey $route 'no')
            }
        }

        $packagesRoot = Join-Path $ProjectPath 'Packages'
        $missing = @(Get-DependencyFailures -PackagesDir $packagesRoot -AllowedNames $allowedNames)
        if ($missing.Count -gt 0) {
            foreach ($line in $missing) { $failures += $line }
            Say (Redact "- vpm dependencies outside the allowed set: $($missing -join ', ')")
        }
        else {
            Say '- vpm dependencies outside the allowed set: none'
        }

        $removed = @(Edit-UnityManifest (Join-Path $packagesRoot 'manifest.json'))
        Say (Redact "- removed from the Unity manifest: $(if ($removed.Count -gt 0) { $removed -join ', ' } else { 'none' })")

        if ($failures.Count -gt 0) { throw ($failures -join '; ') }
        Say "install-packages finished for $($plan.Count) packages."
    }
    finally {
        Remove-Item -LiteralPath $workDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
