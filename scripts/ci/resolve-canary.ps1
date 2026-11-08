# Builds the canary plan and rewrites the locked block of the manifest.
#
# The canary floats every vendor package to its newest stable release. This
# script reads the named listing of every package in the locked block of
# Packages/vpm-manifest.json, picks one version per package and writes a
# plan file for install-packages.ps1 in canary mode. It also rewrites the
# locked block of the manifest in the checkout, so the suite-start check of
# the test assembly sees the versions that the canary installs. The rewrite
# never leaves the runner.
#
# The script only reads JSON listings and the GitHub release API. It never
# runs vendor code and it never writes outside the plan file and the
# manifest. It runs in the admin copy, after the activation step.
#
# Version rules:
# - Only stable versions count. A stable version is digits and dots only,
#   with no suffix after a dash.
# - Versions compare per segment as numbers. 10.0.29 beats 2.4.1.
# - A release that is younger than the cooldown of 48 hours is skipped and
#   named. The next lower stable version is then considered.
# - A version with no release date has no cooldown. The plan says
#   dateSource none for it.
#
# Run it directly:
#   resolve-canary.ps1 -Out <plan.json> [-ProjectPath <path>]
# Dot-sourcing it runs nothing and gives the testable functions.
param(
    [string]$Out = '',
    [string]$ProjectPath = ''
)

Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Common.ps1')

# Some listings start with a byte order mark. Invoke-RestMethod would then
# return a string, so the raw body is decoded first. Same helper as in
# install-packages.ps1 and new-lock.ps1.
function Get-Json([string]$uri) {
    $body = (Invoke-WebRequest -Uri $uri).Content
    if ($body -is [byte[]]) { $body = [Text.Encoding]::UTF8.GetString($body) }
    return ([string]$body).TrimStart([char]0xFEFF) | ConvertFrom-Json
}

# Returns the entry of one package version in a listing, or null when the
# name or the version is not in the listing. Same helper as in
# install-packages.ps1.
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

# Returns the entry of one package version and stops when it is missing.
# The message can hold text from a listing, so it passes through Redact.
function Get-VersionEntryOrThrow($listing, [string]$name, [string]$version) {
    $entry = Resolve-ListingEntry $listing $name $version
    if (-not $entry) { throw (Redact "$name $version is not in its own named listing.") }
    return $entry
}

# Returns the version keys of one package in its listing, in listing order.
function Get-VersionNames($listing, [string]$name) {
    $packagesProperty = $listing.PSObject.Properties['packages']
    if (-not $packagesProperty) { throw "The listing has no packages section for $name." }
    $packageProperty = $packagesProperty.Value.PSObject.Properties[$name]
    if (-not $packageProperty) { throw "The listing has no entry for the package: $name" }
    $versionsProperty = $packageProperty.Value.PSObject.Properties['versions']
    if (-not $versionsProperty) { throw "The listing has no versions section for $name." }
    return @($versionsProperty.Value.PSObject.Properties.Name)
}

# Returns the source key that sources.json names for one package. Stops when
# no key is named, so an unknown package can never pick a random listing.
# Same helper as in install-packages.ps1.
function Get-PackageSourceKey([string]$name, $sources) {
    $property = $sources.packages.PSObject.Properties[$name]
    if (-not $property) { throw (Redact "No source key is named for the package: $name") }
    return [string]$property.Value
}

# Returns the listing of one source key. Listings are downloaded once and
# cached in the hashtable that the caller holds. Same helper as in
# install-packages.ps1.
function Get-ListingFromCache([string]$key, $sources, $cache) {
    if (-not $cache.ContainsKey($key)) {
        $urlProperty = $sources.sources.PSObject.Properties[$key]
        if (-not $urlProperty) { throw "No listing url is named for the source key: $key" }
        $cache[$key] = Get-Json ([string]$urlProperty.Value)
    }
    return $cache[$key]
}

# Returns the versions that are stable. A stable version is digits and dots
# only, so a suffix after a dash never counts.
function Get-StableVersions($versions) {
    $result = @()
    foreach ($version in @($versions)) {
        if ($version -match '^\d+(\.\d+)*$') { $result += [string]$version }
    }
    return $result
}

# Compares two stable versions per segment as numbers. Returns a negative
# number when the first is lower, a positive number when it is higher, and
# zero when they are equal. A longer version wins when the shared segments
# are equal, so 1.0.1 beats 1.0.
function Compare-StableVersion([string]$a, [string]$b) {
    $segmentsA = [long[]]($a.Split('.'))
    $segmentsB = [long[]]($b.Split('.'))
    $count = [Math]::Min($segmentsA.Length, $segmentsB.Length)
    for ($i = 0; $i -lt $count; $i++) {
        if ($segmentsA[$i] -lt $segmentsB[$i]) { return -1 }
        if ($segmentsA[$i] -gt $segmentsB[$i]) { return 1 }
    }
    return [Math]::Sign($segmentsA.Length - $segmentsB.Length)
}

# Returns the highest stable version with the cooldown applied.
#   $versions is an array of version strings, in any order.
#   $releaseDates maps a version to its release date. A missing key means
#   that the date is unknown, so the cooldown does not apply.
# The result holds Version, Skipped and DateSource. DateSource is none when
# the chosen version has no known date, and known when it has one. The
# caller replaces known with the real route name, because this function
# cannot see where a date came from.
function Select-CanaryVersion($versions, [datetimeoffset]$now, [int]$cooldownHours = 48, $releaseDates) {
    $remaining = @(Get-StableVersions $versions)
    $skipped = @()
    while ($remaining.Count -gt 0) {
        $best = $remaining[0]
        for ($i = 1; $i -lt $remaining.Count; $i++) {
            if ((Compare-StableVersion $remaining[$i] $best) -gt 0) { $best = $remaining[$i] }
        }
        $date = $null
        if ($null -ne $releaseDates -and $releaseDates.ContainsKey($best)) {
            $date = $releaseDates[$best]
        }
        if ($null -ne $date) {
            $age = $now - $date
            if ($age -lt [TimeSpan]::FromHours($cooldownHours)) {
                $skipped += $best
                $remaining = @($remaining | Where-Object { $_ -ne $best })
                continue
            }
            return @{ Version = $best; Skipped = @($skipped); DateSource = 'known' }
        }
        return @{ Version = $best; Skipped = @($skipped); DateSource = 'none' }
    }
    return @{ Version = $null; Skipped = @($skipped); DateSource = 'none' }
}

# Returns the release date of one listing entry and the route that gave it.
# The route is listing when the entry holds a date field, github when the
# entry holds a GitHub release url, and none when no route exists. The
# GitHub call is unauthenticated and reads the date of one release. The
# name of the package is only for the warning line when the lookup fails.
function Get-ReleaseDate($entry, [string]$name) {
    foreach ($fieldName in 'date', 'published', 'releaseDate') {
        $property = $entry.PSObject.Properties[$fieldName]
        if ($property -and $property.Value) {
            $date = [datetimeoffset]::Parse([string]$property.Value, [Globalization.CultureInfo]::InvariantCulture)
            return @{ Date = $date; Source = 'listing' }
        }
    }
    $urlProperty = $entry.PSObject.Properties['url']
    if ($urlProperty -and ([string]$urlProperty.Value) -match '^https://github\.com/([^/]+)/([^/]+)/releases/download/([^/]+)/') {
        try {
            $release = Invoke-RestMethod `
                -Uri ("https://api.github.com/repos/" + $Matches[1] + "/" + $Matches[2] + "/releases/tags/" + $Matches[3]) `
                -Headers @{ 'User-Agent' = 'amuse-ci-resolve-canary' }
            $publishedProperty = $release.PSObject.Properties['published_at']
            if ($publishedProperty -and $publishedProperty.Value) {
                $date = [datetimeoffset]::Parse([string]$publishedProperty.Value, [Globalization.CultureInfo]::InvariantCulture)
                return @{ Date = $date; Source = 'github' }
            }
        }
        catch {
            # The lookup failed, so the route stays none and the version is
            # kept. The run must say why the cooldown did not apply. One
            # redacted line names the package.
            Say (Redact "- warning: the release date lookup for $name failed. The 48 hour cooldown was skipped for this package.")
        }
    }
    return @{ Date = $null; Source = 'none' }
}

# Picks the version of one package and applies the cooldown. The selection
# runs with no dates first. When the top version has a date route, its date
# joins the selection and the selection runs again. Each pass either keeps
# the top version or skips it and names it, so the loop always ends. The
# result holds Choice, Entry and DateSource, with the real route name.
function Get-CanaryChoice($listing, [string]$name, $stableNames, [datetimeoffset]$now, [int]$cooldownHours) {
    $dates = @{}
    $routes = @{}
    while ($true) {
        $choice = Select-CanaryVersion $stableNames $now $cooldownHours $dates
        if (-not $choice.Version) {
            $skippedText = @($choice.Skipped) -join ', '
            throw (Redact ("No stable version of " + $name + " passed the cooldown of " + $cooldownHours +
                " hours. Skipped: " + $skippedText))
        }
        $entry = Get-VersionEntryOrThrow $listing $name $choice.Version
        if (-not $routes.ContainsKey($choice.Version)) {
            $routes[$choice.Version] = Get-ReleaseDate $entry $name
        }
        $route = $routes[$choice.Version]
        if ($choice.DateSource -eq 'none' -and $route.Source -eq 'none') {
            return @{ Choice = $choice; Entry = $entry; DateSource = 'none' }
        }
        $dates[$choice.Version] = $route.Date
        $next = Select-CanaryVersion $stableNames $now $cooldownHours $dates
        if ($next.Version -eq $choice.Version) {
            # The version is old enough and stays.
            return @{ Choice = @{ Version = $next.Version; Skipped = @($next.Skipped); DateSource = $route.Source };
                Entry = $entry; DateSource = $route.Source }
        }
        # The version is younger than the cooldown. The next pass considers
        # the versions that are still in the race.
    }
}

# Stops the run when a chosen version needs a package that is not in the
# plan. The message names the package and the dependency. The check runs
# before the plan file and the manifest are written, so nothing is half
# done when it throws.
function Assert-PlanDependencies($plan, [string[]]$planNames) {
    foreach ($item in $plan) {
        $depsProperty = $item.ListingEntry.PSObject.Properties['vpmDependencies']
        if (-not $depsProperty) { continue }
        foreach ($depProperty in @($depsProperty.Value.PSObject.Properties)) {
            $depName = [string]$depProperty.Name
            if ($planNames -notcontains $depName) {
                throw (Redact ("The chosen version of " + $item.Name + " needs the dependency " +
                    $depName + ", and " + $depName + " is not in the plan."))
            }
        }
    }
}

# Rewrites the versions of the locked block in the manifest text. Only the
# version values of the named entries change. Every other byte stays, so
# the key order and all other fields survive. The pattern is the pattern
# that the suite-start check reads, so the rewrite and the check agree.
# Stops when a named entry is not in the locked block.
function Update-LockedVersions([string]$manifestText, $versionsByName) {
    $lockedAt = $manifestText.IndexOf('"locked"', [StringComparison]::Ordinal)
    $openAt = -1
    if ($lockedAt -ge 0) { $openAt = $manifestText.IndexOf('{', $lockedAt) }
    if ($openAt -lt 0) { throw 'The manifest has no locked block.' }
    $depth = 0
    $closeAt = -1
    for ($i = $openAt; $i -lt $manifestText.Length; $i++) {
        if ($manifestText[$i] -eq '{') { $depth++ }
        elseif ($manifestText[$i] -eq '}') {
            $depth--
            if ($depth -eq 0) { $closeAt = $i; break }
        }
    }
    if ($closeAt -lt 0) { throw 'The locked block of the manifest is not closed.' }

    $block = $manifestText.Substring($openAt, $closeAt - $openAt + 1)
    $result = New-Object System.Text.StringBuilder
    [void]$result.Append($manifestText.Substring(0, $openAt))
    $position = 0
    $found = @{}
    foreach ($match in [regex]::Matches($block, '"(?<name>[^"]+)"\s*:\s*\{\s*"version"\s*:\s*"(?<version>[^"]*)"')) {
        $name = $match.Groups['name'].Value
        if (-not $versionsByName.ContainsKey($name)) { continue }
        $versionStart = $match.Groups['version'].Index
        $versionLength = $match.Groups['version'].Length
        [void]$result.Append($block.Substring($position, $versionStart - $position))
        [void]$result.Append([string]$versionsByName[$name])
        $position = $versionStart + $versionLength
        $found[$name] = $true
    }
    [void]$result.Append($block.Substring($position))
    [void]$result.Append($manifestText.Substring($closeAt + 1))

    foreach ($name in $versionsByName.Keys) {
        if (-not $found.ContainsKey($name)) {
            throw (Redact "The name $name is not in the locked block of the manifest.")
        }
    }
    return $result.ToString()
}

# Rewrites the manifest file in place. The file is written as UTF-8 without
# a byte order mark, which is the shape of the manifest in the checkout.
function Update-LockedManifest([string]$path, $versionsByName) {
    if (-not (Test-Path -LiteralPath $path)) { throw 'The manifest file is missing.' }
    $text = [IO.File]::ReadAllText($path)
    $updated = Update-LockedVersions $text $versionsByName
    [IO.File]::WriteAllText($path, $updated, [Text.UTF8Encoding]::new($false))
}

if ($MyInvocation.InvocationName -ne '.') {
    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'

    if (-not $Out) { throw 'The plan output path is missing. Pass -Out.' }
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    if (-not $ProjectPath) { $ProjectPath = $repoRoot }
    if (-not (Test-Path -LiteralPath $ProjectPath)) { throw "The project path does not exist: $ProjectPath" }

    $outPath = $Out
    if (-not [IO.Path]::IsPathRooted($outPath)) {
        $outPath = [IO.Path]::GetFullPath((Join-Path (Get-Location).Path $outPath))
    }

    $manifestPath = Join-Path $ProjectPath 'Packages/vpm-manifest.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { throw 'The manifest file is missing.' }
    $manifestText = [IO.File]::ReadAllText($manifestPath)
    $manifest = $manifestText | ConvertFrom-Json
    $lockedProperty = $manifest.PSObject.Properties['locked']
    if (-not $lockedProperty) { throw 'The manifest has no locked block.' }
    $lockedEntries = @($lockedProperty.Value.PSObject.Properties)
    if ($lockedEntries.Count -lt 1) { throw 'The locked block of the manifest is empty.' }

    $sourcesPath = Join-Path $PSScriptRoot 'sources.json'
    $sources = Get-Content -LiteralPath $sourcesPath -Raw | ConvertFrom-Json

    $now = [datetimeoffset]::UtcNow
    $cooldownHours = 48
    $listings = @{}
    $plan = @()
    $versionsByName = @{}

    Say '| package | old | new | date source | skipped younger |'
    Say '| --- | --- | --- | --- | --- |'
    foreach ($property in $lockedEntries) {
        $name = $property.Name
        $versionProperty = $property.Value.PSObject.Properties['version']
        if (-not $versionProperty) { throw (Redact "The locked entry of $name has no version.") }
        $old = [string]$versionProperty.Value

        $sourceKey = Get-PackageSourceKey $name $sources
        $listing = Get-ListingFromCache $sourceKey $sources $listings
        $stableNames = @(Get-StableVersions (Get-VersionNames $listing $name))
        if ($stableNames.Count -lt 1) { throw (Redact "The package $name has no stable version in its listing.") }

        $outcome = Get-CanaryChoice $listing $name $stableNames $now $cooldownHours
        $new = [string]$outcome.Choice.Version
        $skippedYounger = @($outcome.Choice.Skipped)
        $skippedText = if ($skippedYounger.Count -gt 0) { $skippedYounger -join ', ' } else { 'none' }

        $urlProperty = $outcome.Entry.PSObject.Properties['url']
        if (-not $urlProperty -or -not ([string]$urlProperty.Value)) {
            throw (Redact "The chosen version of $name has no download url in its listing entry.")
        }
        $url = [string]$urlProperty.Value

        $plan += @{
            Name = $name
            Old = $old
            New = $new
            Url = $url
            DateSource = $outcome.DateSource
            SkippedYounger = $skippedYounger
            ListingEntry = $outcome.Entry
        }
        $versionsByName[$name] = $new
        Say (Redact "| $name | $old | $new | $($outcome.DateSource) | $skippedText |")
    }

    # Nothing is written before the dependency check. A new transitive
    # dependency outside the plan stops the run here.
    Assert-PlanDependencies $plan @($lockedEntries | ForEach-Object { $_.Name })

    $planPackages = @($plan | ForEach-Object {
        [ordered]@{
            name = $_.Name
            old = $_.Old
            new = $_.New
            url = $_.Url
            dateSource = $_.DateSource
            skippedYounger = @($_.SkippedYounger)
        }
    })
    $planText = (@{ packages = $planPackages } | ConvertTo-Json -Depth 4) + "`n"
    [IO.File]::WriteAllText($outPath, $planText, [Text.UTF8Encoding]::new($false))

    Update-LockedManifest -Path $manifestPath -Versions $versionsByName

    Say "resolve-canary finished for $($plan.Count) packages. The plan file is written."
}
