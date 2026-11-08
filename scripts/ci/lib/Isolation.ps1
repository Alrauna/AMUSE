# Isolation helpers for the licensed job. The job runs the activation, the
# Editor and the tests as a local user that is not an administrator. This
# file holds the measured behavior of the spike runs. Every right, grant and
# environment detail was measured. Do not change the rights.
#
# The scripts of the licensed job must not derive the repository root from
# this file location. Later steps run the admin copy of this folder, so the
# root always comes from the environment. See Get-RunPaths.
#
# Dot-source this file to get its functions. The file has no parameters and
# runs no code on load.
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'Common.ps1')

# The paths of one licensed job run. The values are the same values that the
# spike used in its low user mode. Every later script of this task reads its
# paths from here, so no script derives a path from its own location except
# the local fallback of Root.
function Get-RunPaths {
    $root = if ($env:GITHUB_WORKSPACE) { $env:GITHUB_WORKSPACE } else { (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path }
    $work = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
    $unityHome = Join-Path $work 'UnityEditor'
    $licenseDir = Join-Path $env:ProgramData 'Unity'
    $lowRoot = Join-Path $env:SystemDrive 'amuse-low'
    return @{
        Root = $root
        Work = $work
        UnityHome = $unityHome
        UnityExe = (Join-Path $unityHome 'Editor\Unity.exe')
        LicenseDir = $licenseDir
        LicensePath = (Join-Path $licenseDir 'Unity_lic.ulf')
        LowUser = 'amuseci'
        LowRoot = $lowRoot
        Out = (Join-Path $lowRoot 'out')
        ProjectPath = (Join-Path $lowRoot 'project')
        LowSecretFile = (Join-Path $work 'lowuser.cred')
        AdminScripts = (Join-Path $work 'admin-scripts')
    }
}

# The profile folder of the low user on this machine.
function Get-LowProfile {
    $paths = Get-RunPaths
    return (Join-Path $env:SystemDrive "Users\$($paths.LowUser)")
}

# Runs icacls with the given arguments and fails the step on a non-zero exit.
function Invoke-Icacls([string[]]$arguments, [string]$label) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    & icacls @arguments | Out-Null
    $code = $LASTEXITCODE
    Say "- icacls $label exit code: $code, $([int]$timer.Elapsed.TotalSeconds) s"
    if ($code -ne 0) { throw "icacls failed for $label." }
}

# Builds the deny write rule for one identity. The rule names each right on
# purpose. A deny rule that includes the right to synchronize makes every
# open of the folder fail, even a read, and the package manager server then
# stops. The rule is never applied here.
function New-DenyWriteRule([string]$identity) {
    $rights = [Security.AccessControl.FileSystemRights]'WriteData, AppendData, WriteExtendedAttributes, WriteAttributes, DeleteSubdirectoriesAndFiles, Delete'
    $inherit = [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit'
    return New-Object Security.AccessControl.FileSystemAccessRule($identity, $rights, $inherit, [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Deny)
}

# Denies write rights to the low user on a folder and everything below it.
# The Users group already holds read and execute on the runner folders, so no
# grant is needed.
function Add-DenyWrite([string]$path, [string]$label) {
    $rule = New-DenyWriteRule (Get-RunPaths).LowUser
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $acl = Get-Acl -LiteralPath $path
    $acl.AddAccessRule($rule)
    Set-Acl -LiteralPath $path -AclObject $acl
    Say "- deny write rule for the low user on $label set, $([int]$timer.Elapsed.TotalSeconds) s"
}

# Creates the low user with a random password. The password goes into a file
# that only Administrators can read. The function returns nothing.
function New-LowUser {
    $paths = Get-RunPaths
    $lowUser = $paths.LowUser
    $bytes = New-Object byte[] 24
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $generated = 'Aa1!' + [Convert]::ToBase64String($bytes).Replace('+', 'x').Replace('/', 'y').Replace('=', 'z')
    # The /y switch answers the prompt that net user shows for a password
    # longer than 14 characters.
    & net user $lowUser $generated /add /y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'The low user could not be created.' }
    Set-Content -Path $paths.LowSecretFile -Value $generated -NoNewline
    $generated = $null
    Invoke-Icacls @($paths.LowSecretFile, '/inheritance:r', '/grant:r', 'Administrators:F', 'SYSTEM:F') 'credential file'
    $inAdministrators = @(& net localgroup Administrators) -match ('^' + [regex]::Escape($lowUser) + '$')
    Say "- low user created, member of Administrators: $([bool]$inAdministrators)"
}

# Gives the low user its rights. Modify on its own folder and on the license
# folder. Read and execute on the Editor install. Nothing else.
function Grant-LowAccess {
    $paths = Get-RunPaths
    $lowUser = $paths.LowUser
    New-Item -ItemType Directory -Force -Path $paths.LowRoot, $paths.Out, $paths.LicenseDir | Out-Null
    Invoke-Icacls @($paths.LowRoot, '/grant', "${lowUser}:(OI)(CI)M") 'low folder'
    Invoke-Icacls @($paths.LicenseDir, '/grant', "${lowUser}:(OI)(CI)M") 'license folder'
    Invoke-Icacls @($paths.UnityHome, '/grant', "${lowUser}:(OI)(CI)RX") 'Editor install'
}

# Reads the password file and returns the credential of the low user.
function Get-LowCredential {
    $paths = Get-RunPaths
    $secret = (Get-Content -Path $paths.LowSecretFile -Raw).Trim()
    return New-Object System.Management.Automation.PSCredential($paths.LowUser, (ConvertTo-SecureString $secret -AsPlainText -Force))
}

# A process started with another user's credential would get a copy of this
# environment. Point the profile variables at the low user and blank every
# variable that names a runner file or an Actions service. The function is
# pure. It reads only the given values and never reads a secret.
function Get-LowEnvironment([hashtable]$baseEnvironment, [string]$lowProfile) {
    $paths = Get-RunPaths
    $lowUser = $paths.LowUser
    $local = Join-Path $lowProfile 'AppData\Local'
    # The rest of the base environment flows through. Only the profile
    # variables are repointed and the runner variables are blanked.
    $variables = @{}
    foreach ($key in @($baseEnvironment.Keys)) { $variables[$key] = [string]$baseEnvironment[$key] }
    $variables['USERNAME'] = $lowUser
    $variables['USERPROFILE'] = $lowProfile
    $variables['HOME'] = $lowProfile
    $variables['HOMEPATH'] = "\Users\$lowUser"
    $variables['LOCALAPPDATA'] = $local
    $variables['APPDATA'] = (Join-Path $lowProfile 'AppData\Roaming')
    $variables['TEMP'] = (Join-Path $local 'Temp')
    $variables['TMP'] = (Join-Path $local 'Temp')
    foreach ($key in @($baseEnvironment.Keys)) {
        if ($key -match '^(GITHUB|RUNNER|ACTIONS|ImageOS|ImageVersion)') { $variables[$key] = '' }
    }
    return $variables
}

# Stops a process tree with taskkill.
function Stop-Tree([int]$processId) {
    & taskkill /PID $processId /T /F 2>&1 | Out-Null
}

# Starts a program as the low user and waits for it. Output goes to files, so
# the caller does not care about the console of the other user.
function Start-AsLowUser([string]$file, [string]$arguments, [string]$stdoutPath, [string]$stderrPath, [int]$timeoutMs) {
    $paths = Get-RunPaths
    $baseEnvironment = @{}
    foreach ($item in @(Get-ChildItem Env:)) { $baseEnvironment[$item.Name] = [string]$item.Value }
    $parameters = @{
        FilePath = $file
        ArgumentList = $arguments
        PassThru = $true
        RedirectStandardOutput = $stdoutPath
        RedirectStandardError = $stderrPath
        Credential = Get-LowCredential
        LoadUserProfile = $true
        WorkingDirectory = $paths.Out
        Environment = Get-LowEnvironment $baseEnvironment (Get-LowProfile)
    }
    $process = Start-Process @parameters
    try { $cachedHandle = $process.Handle } catch { $cachedHandle = $null }
    $finished = $process.WaitForExit($timeoutMs)
    if ($finished) { $process.WaitForExit() } else { Stop-Tree $process.Id }
    $exitCode = -1
    if ($finished) { $exitCode = $process.ExitCode }
    return @{ Finished = $finished; ExitCode = $exitCode }
}

# Stops every process that the low user left running.
function Stop-LowUserProcesses {
    $lowUser = (Get-RunPaths).LowUser
    foreach ($candidate in @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue)) {
        $owner = Invoke-CimMethod -InputObject $candidate -MethodName GetOwner -ErrorAction SilentlyContinue
        if ($owner -and $owner.User -eq $lowUser) { Stop-Tree ([int]$candidate.ProcessId) }
    }
}

# Deletes the low user account and its profile folder.
function Remove-LowUser {
    $paths = Get-RunPaths
    & net user $paths.LowUser /delete 2>&1 | Out-Null
    $profileDir = Get-LowProfile
    if (Test-Path -LiteralPath $profileDir) {
        Remove-Item -LiteralPath $profileDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# Copies the scripts folder of this job to a folder in the work directory and
# removes every inherited right. Only Administrators and SYSTEM can read it
# after this. Later steps run the copy, so the workspace holds no file that
# an administrator step runs.
function Copy-AdminScripts {
    $paths = Get-RunPaths
    $source = Split-Path $PSScriptRoot -Parent
    if (Test-Path -LiteralPath $paths.AdminScripts) { Remove-Item -Recurse -Force -LiteralPath $paths.AdminScripts }
    Copy-Item -Recurse -Path $source -Destination $paths.AdminScripts
    Invoke-Icacls @($paths.AdminScripts, '/inheritance:r', '/grant:r', 'Administrators:(OI)(CI)F', 'SYSTEM:(OI)(CI)F') 'admin scripts'
}

# Finds the licensing client in the Editor install. Returns nothing when the
# install is missing.
function Find-LicensingClient {
    $paths = Get-RunPaths
    if (-not (Test-Path -LiteralPath $paths.UnityHome)) { return $null }
    $expected = Join-Path $paths.UnityHome 'Editor\Data\Resources\Licensing\Client\Unity.Licensing.Client.exe'
    if (Test-Path -LiteralPath $expected) { return Get-Item -LiteralPath $expected }
    return Get-ChildItem -Path $paths.UnityHome -Recurse -Filter 'Unity.Licensing.Client.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
}

# The account password goes to this program. Stop unless Unity Technologies
# signed it and the signer subject equals the locked subject. Both messages
# name the observed subject, which is a public value, so the maintainer can
# start a lock rotation with it.
function Assert-LicensingClientSubject($signature, [string]$expectedSubject) {
    $status = 'None'
    $observedSubject = ''
    if ($signature) {
        $status = [string]$signature.Status
        if ($signature.SignerCertificate) { $observedSubject = [string]$signature.SignerCertificate.Subject }
    }
    if ($status -ne 'Valid') {
        throw "The licensing client signature is not valid. status $status, signer subject: $observedSubject"
    }
    if ($observedSubject -cne $expectedSubject) {
        throw "The licensing client signer subject does not match the lock. signer subject: $observedSubject"
    }
}

# Derives the runner root folder from the path of the Runner.Worker process.
# The root is the grandparent folder of the worker executable. Returns
# nothing when no usable path is given. The -WorkerPath parameter is the
# seam that the tests use, so no process starts here.
function Get-RunnerRoot([string]$WorkerPath) {
    if (-not $WorkerPath) { return $null }
    $parent = Split-Path $WorkerPath -Parent
    if (-not $parent) { return $null }
    return Split-Path $parent -Parent
}

# Builds the list of places that the probe tests. The license folder is the
# one place where the low user may write. Every other place is a place that
# an administrator step runs from, so the low user must not write there.
function Get-ProbeTargets {
    $paths = Get-RunPaths
    $targets = @(
        @{ Label = 'admin scripts folder'; Path = $paths.AdminScripts; AllowWrite = $false }
        @{ Label = 'admin scripts file'; Path = (Join-Path $paths.AdminScripts 'prepare-run.ps1'); AllowWrite = $false }
        @{ Label = 'workspace'; Path = $paths.Root; AllowWrite = $false }
        @{ Label = 'runner temp folder'; Path = $paths.Work; AllowWrite = $false }
        @{ Label = 'runner actions folder'; Path = (Join-Path (Split-Path $paths.Work -Parent) '_actions'); AllowWrite = $false }
        @{ Label = 'Editor install'; Path = $paths.UnityHome; AllowWrite = $false }
        @{ Label = 'license folder'; Path = $paths.LicenseDir; AllowWrite = $true }
    )
    $worker = Get-Process -Name 'Runner.Worker' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($worker -and $worker.Path) {
        $runnerRoot = Get-RunnerRoot -WorkerPath $worker.Path
        $targets += @(
            @{ Label = 'runner root'; Path = $runnerRoot; AllowWrite = $false }
            @{ Label = 'runner bin'; Path = (Join-Path $runnerRoot 'bin'); AllowWrite = $false }
            @{ Label = 'runner externals'; Path = (Join-Path $runnerRoot 'externals'); AllowWrite = $false }
        )
    }
    return $targets
}

# Turns the facts of the probe into a list of violations. An empty list means
# the run is healthy. A missing result is a violation too, because a probe
# that tested nothing proved nothing. The list holds fixed words only, so the
# caller can print it into the public log.
function Get-IsolationVerdict($facts) {
    $violations = @()
    $processes = @($facts['RunnerProcesses'])
    if ($processes.Count -lt 1) {
        $violations += 'the probe found no runner process, so it proved nothing'
    }
    foreach ($process in $processes) {
        if ($process['Readable']) {
            $violations += "a runner process can be read for memory: $($process['Name'])"
        }
    }
    $writable = @($facts['Writable'])
    if ($writable.Count -lt 1) {
        $violations += 'the probe tested no place for writes, so it proved nothing'
    }
    foreach ($entry in $writable) {
        if ($entry['Writable'] -and -not $entry['AllowWrite']) {
            $violations += "a place that administrators use can be written: $($entry['Label'])"
        }
    }
    return $violations
}
