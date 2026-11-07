# Copies the project to the private folder of the low user and prepares the
# profile folders of the package manager. The workflow calls this script from
# the admin copy, after the vendor packages are installed in the workspace.
#
# The low user works on a private copy. It never gets write access to the
# workspace. This script edits nothing in the workspace.
#
# Run it directly. Dot-sourcing it runs nothing.
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Isolation.ps1')

if ($MyInvocation.InvocationName -ne '.') {
    $paths = Get-RunPaths
    & robocopy $paths.Root $paths.ProjectPath /E /XD .git /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE." }
    Say "- project copied for the low user, robocopy exit code: $LASTEXITCODE"
    $global:LASTEXITCODE = 0
    # The Editor and the package manager write to folders under the profile of
    # the user. The Editor makes these folders only when it uses its default
    # log file.
    $local = Join-Path (Get-LowProfile) 'AppData\Local'
    foreach ($folder in @('Unity\Editor', 'Unity\cache')) {
        New-Item -ItemType Directory -Force -Path (Join-Path $local $folder) | Out-Null
    }
    Say '- profile folders for the package manager prepared'
}
