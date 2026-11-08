# Prepares the licensed job to run as a local user that is not an
# administrator. The workflow calls this script once, from the checkout copy,
# before any other licensed step. Every later licensed step runs the admin
# copy of this scripts folder.
#
# The step creates the admin copy, the low user, the rights of the low user
# and the deny rules on the runner folders. The rights and the deny rules are
# the measured values of the spike runs. Do not change them.
#
# Run it directly. Dot-sourcing it runs nothing.
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Isolation.ps1')

if ($MyInvocation.InvocationName -ne '.') {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    $paths = Get-RunPaths
    Say '## prepare run'
    New-Item -ItemType Directory -Force -Path $paths.Out | Out-Null
    Copy-AdminScripts
    New-LowUser
    Grant-LowAccess
    # The deny rules block writes. The Users group already holds read and
    # execute on these folders, so no grant is needed.
    Add-DenyWrite (Split-Path $paths.Work -Parent) 'agent folder'
    $worker = Get-Process -Name 'Runner.Worker' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($worker -and $worker.Path) {
        $runnerRoot = Get-RunnerRoot -WorkerPath $worker.Path
        Add-DenyWrite $runnerRoot 'runner folder'
    }
    else { Say '- no Runner.Worker process found, so the runner folder deny was skipped' }
}
