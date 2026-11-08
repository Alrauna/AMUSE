# Decides whether a change needs the licensed Unity job. The answer is true
# unless every changed path is on a short allow list of paths that cannot
# change a test result. The decision is made from the paths of the diff, never
# from a label, a title or a commit message, because the author controls those.
#
# The answer fails closed. A push, an unknown event, an empty diff, an odd path
# and a git error all mean the Unity job runs or the step fails. The validate
# job accepts a skipped Unity job only when this step says false.
#
# Git rename detection is off, so a move of code into the allow list lists the
# old path too. The old path is not on the list, so the job runs.
#
# Dot-source this file to get its functions. Run it directly in a workflow step.
param()
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Common.ps1')

# Paths that cannot change a test result. Each pattern is anchored and
# case-sensitive. Nothing under Packages, Assets, ProjectSettings or
# scripts/ci is here. The workflow files pr.yml and canary.yml are not here
# either, because they define the gate itself.
function Get-SafePathPattern {
    return @(
        '^docs/.+\.(md|png|jpg|jpeg|gif|svg)$',
        '^(README|CHANGELOG|CONTRIBUTING)\.md$',
        '^\.github/dependabot\.yml$',
        '^\.github/workflows/(build-listing|release)\.yml$'
    )
}

# True when the Unity job must run for this list of changed paths.
function Test-UnityNeeded($paths) {
    $list = @($paths | Where-Object { $null -ne $_ })
    if ($list.Count -lt 1) { return $true }
    foreach ($path in $list) {
        $text = [string]$path
        # A path with a traversal part, a backslash, a leading slash, a space
        # at an edge or a control character is never safe. An empty path
        # is never safe either.
        if ($text.Length -lt 1 -or $text -cmatch '[\x00-\x1f\\]' -or $text.StartsWith('/') -or
            $text.Trim() -ne $text -or $text -cmatch '(^|/)\.\.(/|$)') {
            return $true
        }
        $safe = $false
        foreach ($pattern in (Get-SafePathPattern)) {
            if ($text -cmatch $pattern) { $safe = $true; break }
        }
        if (-not $safe) { return $true }
    }
    return $false
}

# The changed paths between a base and the head of a repository. The -z form
# keeps names with spaces or non-ASCII letters unquoted. Rename detection is off.
function Get-ChangedPath([string]$Base, [string]$RepoPath = '.') {
    $raw = & git -C $RepoPath diff --name-only -z --no-renames $Base HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Reading the changed paths from git failed.' }
    # git -z separates names with NUL. PowerShell may keep the NUL characters
    # inside one string or split at them, so both separators are handled.
    $text = ($raw -join "`n")
    return @(($text -split "[`0`n]") | Where-Object { $_ -ne '' })
}

# Only a pull request against the default branch can skip the Unity job. A
# pull request against another branch could stack on unreviewed work, and a
# later change of its base branch starts no new run. A push to the main
# branch always runs the job, and so does any other event. A missing branch
# name means doubt, so the job runs.
function Get-UnityNeededForEvent([string]$EventName, $Paths, [string]$BaseBranch = '', [string]$DefaultBranch = '') {
    if ($EventName -ne 'pull_request') { return $true }
    if (-not $BaseBranch -or -not $DefaultBranch -or $BaseBranch -cne $DefaultBranch) { return $true }
    return (Test-UnityNeeded $Paths)
}

if ($MyInvocation.InvocationName -ne '.') {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    Set-StrictMode -Version Latest

    $eventName = [string]$env:GITHUB_EVENT_NAME
    $needed = $true
    if ($eventName -eq 'pull_request') {
        # The base branch name arrives through the environment, like in the
        # whitespace check of the cheap job.
        if (-not $env:PR_BASE) { throw 'The pull request names no base branch.' }
        if (-not $env:DEFAULT_BRANCH) { throw 'The event names no default branch.' }
        $base = "refs/remotes/origin/$($env:PR_BASE)"
        & git rev-parse --verify --quiet $base | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'The base commit is missing from the checkout.' }
        $paths = @(Get-ChangedPath -Base $base)
        $needed = Get-UnityNeededForEvent -EventName $eventName -Paths $paths -BaseBranch $env:PR_BASE -DefaultBranch $env:DEFAULT_BRANCH
        Say "changed paths: $($paths.Count)"
    }
    else {
        Say "event: $eventName. The Unity job always runs for this event."
    }

    $answer = if ($needed) { 'true' } else { 'false' }
    Say "the Unity job is needed: $answer"
    if (-not $env:GITHUB_OUTPUT) { throw 'The runner gave no output file.' }
    Add-Content -LiteralPath $env:GITHUB_OUTPUT -Value "unity_needed=$answer"
}
