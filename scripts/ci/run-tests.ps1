# Runs one EditMode test assembly in the Unity Editor. The gate, the canary
# and the local check all use this script.
#
# The Editor arguments, the wait time and the printed facts follow the spike
# runs that passed on the hosted runner. The Editor always gets -force-d3d11
# and never -nographics, because the tests need a Direct3D 11 device. The
# three opt-in fixture variables of the repo stay unset, so this script
# removes them from the environment before the Editor starts and restores
# them after the run.
#
# The verdict comes from Complete-TestRun in lib/Common.ps1 and from nothing
# else. The raw Editor log is read for its counts and is then deleted. The
# script never prints it.
#
# With -AsLowUser the Editor runs as the low user through Start-AsLowUser,
# and the log and the result file go into the output folder of the low user.
# The default project is then the private project copy of the low user.
# Without -AsLowUser the Editor runs in this session, and the log and the
# result file go into a temp folder under the system temp folder. The script
# deletes that folder at the end. The default project is then the checkout
# root. -ProjectPath overrides both defaults, so the local check can name
# its own clone. -UnityPath points at another Editor install.
#
# Dot-source this file to get its functions. Run it directly to run tests.
param(
    [string]$Assembly = '',
    [string]$ProjectPath = '',
    [switch]$AsLowUser,
    [string]$UnityPath = '',
    [int]$TimeoutMinutes = 55
)

Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Common.ps1')
. (Join-Path $PSScriptRoot 'lib/Isolation.ps1')

# Builds the Editor arguments for one EditMode test run. The order and the
# values follow the spike runs. Pure helper, so the tests can check every
# argument without an Editor.
function Get-UnityTestArguments([string]$projectPath, [string]$logPath, [string]$assembly, [string]$resultsPath) {
    return @(
        '-batchmode',
        '-force-d3d11',
        '-projectPath', $projectPath,
        '-logFile', $logPath,
        '-runTests', '-testPlatform', 'EditMode',
        '-assemblyNames', $assembly,
        '-testResults', $resultsPath
    )
}

# Starts the Editor in this session and waits for it. The behavior follows
# Start-Tool of the spike in its session mode. The output goes to files, so
# no Editor text reaches the console. The function returns the exit code, or
# -1 after a timeout, when the process tree is stopped.
function Start-EditorProcess([string]$file, [string]$arguments, [string]$stdoutPath, [string]$stderrPath, [int]$timeoutMs) {
    $process = Start-Process -FilePath $file -ArgumentList $arguments -PassThru -NoNewWindow `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    try { $null = $process.Handle } catch { }
    $finished = $process.WaitForExit($timeoutMs)
    if ($finished) { $process.WaitForExit() } else { Stop-Tree $process.Id }
    return $(if ($finished) { $process.ExitCode } else { -1 })
}

if ($MyInvocation.InvocationName -ne '.') {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'

    if ([string]::IsNullOrWhiteSpace($Assembly)) {
        throw 'The assembly name is missing. Pass -Assembly with the name of one test assembly.'
    }

    # The opt-in fixtures of the repo. An unset variable gives a named skip,
    # so the gate keeps them unset.
    $fixtureVariables = @('AMUSE_DAO_INTEGRATION', 'AMUSE_REAL_THRY_LOCK', 'AMUSE_PRODUCTION_ORDER_DISPATCH')

    # Every path comes from Get-RunPaths. This script derives no path from
    # its own location, because later steps run the admin copy of it.
    $paths = Get-RunPaths
    $unityExe = $paths.UnityExe
    if ($UnityPath) { $unityExe = $UnityPath }
    if (-not (Test-Path -LiteralPath $unityExe)) { throw 'The Unity Editor executable is missing at the expected path.' }

    $projectPath = $paths.Root
    $out = $paths.Out
    if (-not $AsLowUser) {
        $out = Join-Path ([IO.Path]::GetTempPath()) "amuse-run-tests-$PID"
        New-Item -ItemType Directory -Path $out -Force | Out-Null
    }
    else { $projectPath = $paths.ProjectPath }
    if ($ProjectPath) { $projectPath = $ProjectPath }

    $logPath = Join-Path $out 'unity-tests.log'
    $resultsPath = Join-Path $out 'TestResults.xml'
    $stdoutPath = Join-Path $out 'unity.stdout'
    $stderrPath = Join-Path $out 'unity.stderr'
    $timeoutMs = $TimeoutMinutes * 60 * 1000

    # The fixtures stay unset while the Editor runs. The values of the
    # caller come back after the run.
    $savedFixtures = @{}
    foreach ($name in $fixtureVariables) {
        $value = [Environment]::GetEnvironmentVariable($name)
        if ($null -ne $value) { $savedFixtures[$name] = $value }
        [Environment]::SetEnvironmentVariable($name, $null)
    }

    try {
        $arguments = Get-UnityTestArguments $projectPath $logPath $Assembly $resultsPath
        Say (Redact "- runtests: EditMode run of $Assembly starts")
        if ($AsLowUser) {
            $run = Start-AsLowUser $unityExe ($arguments -join ' ') $stdoutPath $stderrPath $timeoutMs
            $unityExit = [int]$run.ExitCode
        }
        else {
            $unityExit = Start-EditorProcess $unityExe ($arguments -join ' ') $stdoutPath $stderrPath $timeoutMs
        }
        Say "- unity exit code: $unityExit"
        $logFacts = Get-LogFacts $logPath
        Say "- shader error lines: $($logFacts.Shader), compile error lines: $($logFacts.Compile)"
        Say "- log lines about licensing: $($logFacts.Licensing)"
        Remove-Item -LiteralPath $logPath -Force -ErrorAction SilentlyContinue
        Complete-TestRun $resultsPath $Assembly $unityExit $logFacts
        Say '- runtests: the run is green'
    }
    finally {
        foreach ($name in @($savedFixtures.Keys)) {
            [Environment]::SetEnvironmentVariable($name, [string]$savedFixtures[$name])
        }
        if (-not $AsLowUser) { Remove-Item -LiteralPath $out -Recurse -Force -ErrorAction SilentlyContinue }
    }
}
