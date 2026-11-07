# Stops everything the low user left running, deletes the low user and its
# profile, and removes the license file and the password file. The workflow
# calls this script from the admin copy as the last licensed step.
#
# Run it directly. Dot-sourcing it runs nothing.
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Isolation.ps1')

if ($MyInvocation.InvocationName -ne '.') {
    $paths = Get-RunPaths
    Stop-LowUserProcesses
    Remove-LowUser
    if (Test-Path -LiteralPath $paths.LicensePath) {
        Remove-Item -LiteralPath $paths.LicensePath -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $paths.LowSecretFile) {
        Remove-Item -LiteralPath $paths.LowSecretFile -Force -ErrorAction SilentlyContinue
    }
    Say '- cleanup done'
}
