# Checks that a release zip keeps the test content out. The gate builds the
# zip from the package folder, leaves out the same entries as the release
# step in the release workflow, and then applies the two zip rules of that
# step. The check fails the run when an entry matches a test shape or when
# the production editor assembly is missing. The gate makes no unitypackage,
# so the meta list rule of the release step does not apply here.
#
# The zip is written into the runner temp folder, or into the system temp
# folder when the runner temp folder is not set. The script deletes the zip
# when it is done. It writes no file anywhere else.
#
# Dot-source this file to get its functions. Run it directly to check.
param([string]$PackagePath)
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Common.ps1')

# The package folder that the release step zips. A constant, so a moved
# package can never slip past this check. The run body reads it here.
$releasePackageFolder = 'Packages/com.alrauna.amuse'

# Applies the two zip rules of the release step to a list of zip entry
# names. Backslashes become forward slashes first, so a zip built on
# Windows behaves like the zip of the release step. Returns one string per
# violation. An empty list is a violation, because a check on an empty zip
# proves nothing. The rules match at any depth, because the rule of the
# release step matches at any depth.
function Test-ReleaseZip([string[]]$entries) {
    $testsPattern = '(^|/)Tests(/|\.meta$)'
    $editorAsmdef = 'Editor/Alrauna.Amuse.Editor.asmdef'
    $violations = [System.Collections.Generic.List[string]]::new()
    $normalized = @()
    if ($null -ne $entries) {
        $normalized = @($entries | ForEach-Object { $_.Replace('\', '/') })
    }
    if ($normalized.Count -lt 1) {
        $violations.Add('The zip holds no entries. It proves nothing.')
        return @($violations)
    }
    foreach ($entry in $normalized) {
        if ($entry -cmatch $testsPattern) {
            $violations.Add("The zip holds test content: $entry")
        }
    }
    if (-not ($normalized -ccontains $editorAsmdef)) {
        $violations.Add('The zip is missing the production editor assembly.')
    }
    return @($violations)
}

# Builds the release zip from the package folder with System.IO.Compression.
# Leaves out the same entries as the release step: the top-level Tests
# folder and the Tests.meta file. A Tests folder deeper in the tree is not
# left out here, so the check above still catches it. The zip holds file
# entries with forward slash names, relative to the package folder.
function New-ReleaseZip([string]$packagePath, [string]$zipPath) {
    if (-not (Test-Path -LiteralPath $packagePath)) { throw 'The package folder is missing.' }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    $zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        $files = @(Get-ChildItem -LiteralPath $packagePath -Recurse -File | Sort-Object FullName)
        foreach ($file in $files) {
            $entryName = [IO.Path]::GetRelativePath($packagePath, $file.FullName).Replace('\', '/')
            # The release step drops the top-level Tests folder with a
            # case-sensitive pattern. The check must drop exactly the same
            # entries, so a lowercase tests folder stays in the zip.
            if ($entryName -cmatch '^Tests/' -or $entryName -ceq 'Tests.meta') { continue }
            # The static call, because PowerShell does not resolve the
            # extension method on the archive instance.
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file.FullName, $entryName)
        }
    } finally {
        $zip.Dispose()
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'

    if (-not $PackagePath) {
        $root = $env:GITHUB_WORKSPACE
        if (-not $root) {
            # The script sits in scripts/ci, so the repo root is two levels up.
            $root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
        }
        if (-not (Test-Path -LiteralPath (Join-Path $root 'ProjectSettings/ProjectVersion.txt'))) {
            throw 'The repo root is wrong. ProjectVersion.txt is missing at the root.'
        }
        $PackagePath = Join-Path $root $releasePackageFolder
    }
    if (-not (Test-Path -LiteralPath $PackagePath)) {
        throw 'The package folder is missing.'
    }

    $work = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
    $zipPath = Join-Path $work 'amuse-release-check.zip'
    try {
        New-ReleaseZip -PackagePath $PackagePath -ZipPath $zipPath
        $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $entries = @($zip.Entries | ForEach-Object { $_.FullName })
        } finally {
            $zip.Dispose()
        }
        $violations = @(Test-ReleaseZip $entries)
        if ($violations.Count -gt 0) {
            foreach ($violation in $violations) {
                Say (Redact $violation)
            }
            throw "The release zip check failed with $($violations.Count) violations."
        }
        Say "- release exclusion check passed, $($entries.Count) entries"
    } finally {
        if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    }
}
