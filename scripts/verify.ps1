# Local gate for the maintainer. This is the same install and test run as
# the CI gate, but on this machine and in a temporary clone. It uses the
# license of the local Unity Hub. It is the fallback when the remote lane
# is broken (spec D11).
#
# The clone holds the committed state of the current branch. Uncommitted
# changes are not in the clone. When the working tree holds uncommitted
# changes in the folders that the gate checks, the script prints a warning.
#
# The script runs the check scripts and the install and test scripts of the
# clone, never the copies in the working tree. The gate then checks what
# was committed. Each step runs as a separate child process, so a failure
# stops the run with a non-zero exit and the step is named.
#
# What this script never does: it creates no low user, it touches no
# license, and it prints no environment variables.
#
# Modes:
# - default: all five steps, two Editor test runs included.
# - -CheckOnly: the lock check and the release exclusion check only, then
#   a summary. No download starts and no Editor starts. This is the fast
#   check before a push.
# - -KeepClone: the temporary clone stays for reading afterwards.
#
# Dot-source this file to get Find-LocalUnity. Run it directly to run the
# gate:
#   pwsh -NoProfile -File scripts/verify.ps1 [-CheckOnly] [-KeepClone] [-UnityPath <path>]
param(
    [string]$UnityPath = '',
    [switch]$CheckOnly,
    [switch]$KeepClone
)

# Finds the Unity Editor for one version. Order: the explicit path when it
# is given and exists, then the version folder under each search root as
# <root>\<version>\Editor\Unity.exe. When no search root is passed, the
# defaults are the Unity Hub folder under Program Files and the folder that
# UNITY_HUB_EDITOR_PATH names, when that variable is set. Throws a message
# in simple English when nothing is found.
function Find-LocalUnity {
    param(
        [string]$version,
        [string]$unityPath = '',
        [string[]]$searchRoots = @()
    )
    if ($unityPath -and (Test-Path -LiteralPath $unityPath)) { return $unityPath }
    if (-not $version) {
        throw 'No Unity version is known. Pass -UnityPath with the full path to Unity.exe.'
    }
    $roots = @($searchRoots | Where-Object { $_ })
    if ($roots.Count -lt 1) {
        if ($env:ProgramFiles) { $roots += (Join-Path $env:ProgramFiles 'Unity/Hub/Editor') }
        if ($env:UNITY_HUB_EDITOR_PATH) { $roots += $env:UNITY_HUB_EDITOR_PATH }
    }
    foreach ($root in $roots) {
        $candidate = Join-Path (Join-Path $root $version) 'Editor/Unity.exe'
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    throw "No Unity Editor was found for version $version. Pass -UnityPath with the full path to Unity.exe."
}

if ($MyInvocation.InvocationName -ne '.') {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    Set-StrictMode -Version Latest

    . (Join-Path $PSScriptRoot 'ci/lib/Common.ps1')

    Say 'local gate: the committed state of the current branch runs in a temporary clone.'
    Say 'uncommitted changes are not in the clone.'

    # The root comes from git, never from a fixed path.
    $root = [string](& git -C $PSScriptRoot rev-parse --show-toplevel)
    if (-not $root) { throw 'This script must run inside the repository working tree.' }

    # Warning, not an error: the clone misses these uncommitted changes.
    $dirty = @(& git -C $root diff HEAD --name-only -- scripts .github 'Packages/com.alrauna.amuse' 'Packages/vpm-manifest.json')
    if ($LASTEXITCODE -ne 0) {
        Say 'warning: reading the changed files failed, so the warning below can be incomplete.'
    }
    elseif ($dirty.Count -gt 0) {
        Say 'warning: the working tree has uncommitted changes in the gate folders. The clone does not include them.'
    }

    $clonePath = Join-Path ([IO.Path]::GetTempPath()) "amuse-verify-$PID"

    try {
        Say 'cloning the committed state into a new folder under the system temp folder.'
        & git clone --quiet --no-hardlinks -- $root $clonePath
        if ($LASTEXITCODE -ne 0) { throw 'Cloning the repository failed.' }

        $cloneCi = Join-Path $clonePath 'scripts/ci'

        # The Unity version comes from the lock file of the clone.
        $lockVersion = [string]((Get-Content -LiteralPath (Join-Path $cloneCi 'lock.json') -Raw | ConvertFrom-Json).unity.version)
        if (-not $lockVersion) { throw 'The lock file of the clone holds no Unity version.' }

        $pwsh = (Get-Process -Id $PID).Path
        if (-not $pwsh) { $pwsh = 'pwsh' }

        # One step is one child process. A non-zero exit stops the run and
        # the step name is in the message.
        function Invoke-GateStep([int]$number, [string]$label, [string]$file, [string[]]$arguments) {
            Say "step $number of 5: $label"
            & $pwsh -NoProfile -File $file @arguments
            if ($LASTEXITCODE -ne 0) { throw "step $number failed: $label" }
        }

        Invoke-GateStep 1 'check the lock' (Join-Path $cloneCi 'check-lock.ps1') @()
        Invoke-GateStep 2 'check the release exclusion' (Join-Path $cloneCi 'check-release-exclusion.ps1') @('-PackagePath', (Join-Path $clonePath 'Packages/com.alrauna.amuse'))

        if ($CheckOnly) {
            Say 'check only passed: 2 of 5 steps ran. No download started and no Editor started.'
            return
        }

        # The Editor must be found before anything is downloaded.
        $unityExe = Find-LocalUnity $lockVersion $UnityPath @()

        Invoke-GateStep 3 'install the vendor packages' (Join-Path $cloneCi 'install-packages.ps1') @('-Mode', 'gate', '-ProjectPath', $clonePath)
        Invoke-GateStep 4 'run the product tests' (Join-Path $cloneCi 'run-tests.ps1') @('-ProjectPath', $clonePath, '-UnityPath', $unityExe, '-Assembly', 'Alrauna.Amuse.Tests.Editor')
        Invoke-GateStep 5 'run the research tests' (Join-Path $cloneCi 'run-tests.ps1') @('-ProjectPath', $clonePath, '-UnityPath', $unityExe, '-Assembly', 'Alrauna.Amuse.Research.Tests.Editor')

        Say 'local gate passed: all 5 steps passed in the temporary clone.'
    }
    finally {
        if ($KeepClone) {
            Say "the clone is kept: $(Split-Path $clonePath -Leaf) in the system temp folder."
        }
        elseif (Test-Path -LiteralPath $clonePath) {
            Remove-Item -LiteralPath $clonePath -Recurse -Force
        }
    }
}
