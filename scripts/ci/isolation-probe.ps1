# Runs the isolation probe as the low user and fails the step when the probe
# finds a way out. The workflow calls this script from the admin copy, after
# the project copy is prepared and before the Editor runs.
#
# The probe tests two things. Whether the low user can open the memory of the
# runner processes. Whether the low user can write to any place that an
# administrator step runs from. The license folder is the one allowed write.
# It reads no memory and it leaves no file behind.
#
# The admin copy is not readable by the low user. The probe body therefore
# goes into the out folder of the low user, and the low user writes its facts
# there too. This driver reads the facts and prints counts and yes or no
# answers only.
#
# Run it directly. Dot-sourcing it runs nothing.
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Isolation.ps1')

if ($MyInvocation.InvocationName -ne '.') {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    $paths = Get-RunPaths
    New-Item -ItemType Directory -Force -Path $paths.Out | Out-Null
    $targetsFile = Join-Path $paths.Out 'probe-targets.json'
    $probeScript = Join-Path $paths.Out 'probe.ps1'
    $factsFile = Join-Path $paths.Out 'probe-facts.json'
    $stdoutPath = Join-Path $paths.Out 'probe.out'
    $stderrPath = Join-Path $paths.Out 'probe.err'

    $targets = @(Get-ProbeTargets)
    ConvertTo-Json -InputObject $targets -Depth 4 | Set-Content -Path $targetsFile

    # The probe body runs as the low user. It opens its own process as a
    # control, so a broken method can never look like a clean run. Then it
    # tests the runner processes for memory reads and the target list for
    # writes. It writes one facts file and nothing else.
    $probeBody = @'
param([string]$TargetsFile, [string]$FactsFile)
$ErrorActionPreference = 'Continue'
try {
    Add-Type -Namespace Probe -Name Native -MemberDefinition '[DllImport("kernel32.dll", SetLastError=true)] public static extern System.IntPtr OpenProcess(uint access, bool inherit, int pid); [DllImport("kernel32.dll")] public static extern bool CloseHandle(System.IntPtr handle);'
} catch { }
$control = $false
$own = [Probe.Native]::OpenProcess(0x0410, $false, $PID)
if ($own -ne [IntPtr]::Zero) { $control = $true; [void][Probe.Native]::CloseHandle($own) }
if (-not $control) { throw 'The probe could not open its own process, so the method is broken.' }
$processes = @()
foreach ($name in 'Runner.Worker', 'Runner.Listener') {
    $found = @(Get-Process -Name $name -ErrorAction SilentlyContinue)
    foreach ($p in $found) {
        $readable = $false
        $h = [Probe.Native]::OpenProcess(0x0410, $false, $p.Id)
        if ($h -ne [IntPtr]::Zero) { $readable = $true; [void][Probe.Native]::CloseHandle($h) }
        $processes += @{ Name = $name; Readable = $readable }
    }
}
$writable = @()
foreach ($t in @(Get-Content -Path $TargetsFile -Raw | ConvertFrom-Json)) {
    $ok = $false
    if (Test-Path -LiteralPath $t.Path -PathType Container) {
        $marker = Join-Path $t.Path ('.amuse-write-test-' + [guid]::NewGuid().ToString('N'))
        try { [IO.File]::WriteAllText($marker, 'x'); Remove-Item -LiteralPath $marker -Force; $ok = $true } catch { }
    }
    elseif (Test-Path -LiteralPath $t.Path) {
        try { $s = [IO.File]::Open($t.Path, 'Open', 'Write'); $s.Close(); $ok = $true } catch { }
    }
    $writable += @{ Label = [string]$t.Label; AllowWrite = [bool]$t.AllowWrite; Writable = $ok }
}
@{ RunnerProcesses = $processes; Writable = $writable } | ConvertTo-Json -Depth 4 | Set-Content -Path $FactsFile
'@
    Set-Content -Path $probeScript -Value $probeBody

    $pwsh = (Get-Command pwsh).Source
    $arguments = "-NoProfile -File `"$probeScript`" -TargetsFile `"$targetsFile`" -FactsFile `"$factsFile`""
    $run = Start-AsLowUser $pwsh $arguments $stdoutPath $stderrPath 180000
    Say '- isolation probe run'
    Say "- probe finished: $([bool]$run['Finished']), exit code: $($run['ExitCode'])"
    if ((Test-Path -LiteralPath $stderrPath) -and (Get-Item -LiteralPath $stderrPath).Length -gt 0) {
        Say "- the probe wrote $((Get-Item -LiteralPath $stderrPath).Length) bytes of error output"
    }
    if (-not [bool]$run['Finished']) { throw 'The probe did not finish in time.' }
    if (-not (Test-Path -LiteralPath $factsFile)) { throw 'The probe wrote no facts file.' }

    $parsed = Get-Content -LiteralPath $factsFile -Raw | ConvertFrom-Json
    $facts = @{
        RunnerProcesses = @($parsed.RunnerProcesses | ForEach-Object { @{ Name = [string]$_.Name; Readable = [bool]$_.Readable } })
        Writable = @($parsed.Writable | ForEach-Object { @{ Label = [string]$_.Label; AllowWrite = [bool]$_.AllowWrite; Writable = [bool]$_.Writable } })
    }
    $processes = @($facts['RunnerProcesses'])
    $readable = @($processes | Where-Object { $_['Readable'] })
    Say "- runner processes tested: $($processes.Count), readable for memory: $($readable.Count)"
    $writable = @($facts['Writable'])
    $writableFound = @($writable | Where-Object { $_['Writable'] })
    Say "- places tested for writes: $($writable.Count), writable: $($writableFound.Count)"

    $violations = @(Get-IsolationVerdict $facts)
    if ($violations.Count -gt 0) {
        Say "- isolation violations: $($violations.Count)"
        foreach ($violation in $violations) { Say "  - $violation" }
        throw 'The isolation probe found violations.'
    }
    Say '- isolation violations: 0'
    Say '- isolation verdict: clean'
}
