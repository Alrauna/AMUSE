# Activates the Unity license before vendor code is on disk and returns the
# license at the end of the job. The workflow calls this script from the admin
# copy.
#
# Activate reads the account values from the environment, clears them from
# this process first, checks the licensing client, and then runs the client as
# the low user. After the client exits, the step counts where the credentials
# still appear. It prints counts only.
#
# Public log rules: the script never prints the email or the password. Every
# printed vendor line passes through Redact.
#
# Run it directly with -Action Activate or -Action Return. Dot-sourcing it
# runs nothing.
param(
    [ValidateSet('Activate', 'Return')]
    [string]$Action
)
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Isolation.ps1')

# Prints a small, redacted set of lines. The account values are replaced
# first. Numbers, ids and emails go too.
function Show-RedactedLines([object[]]$lines, [string]$email, [string]$password) {
    foreach ($line in $lines) {
        $text = ([string]$line).Replace($email, '<account>').Replace($password, '<secret>')
        $text = [regex]::Replace((Redact $text), '\d{6,}', '<num>')
        if ($text.Length -gt 200) { $text = $text.Substring(0, 200) }
        Say "  - $text"
    }
}

# Counts only. It prints no value. It looks for the account credentials, and
# for tokens that look like JWTs, in the places where later code in the same
# job could read them.
function Measure-CredentialResidue([string]$email, [string]$password) {
    $paths = Get-RunPaths
    $holders = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and ($_.CommandLine.Contains($password) -or $_.CommandLine.Contains($email)) })
    Say "- processes whose command line holds the email or password: $($holders.Count)"
    $profileData = Join-Path (Get-LowProfile) 'AppData'
    $roots = @($paths.LicenseDir, $profileData, $paths.Out) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
    $scanned = 0
    $emailHits = 0
    $passwordHits = 0
    $tokenHits = 0
    foreach ($rootDir in $roots) {
        $files = @(Get-ChildItem -LiteralPath $rootDir -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Length -lt 20MB } | Select-Object -First 3000)
        foreach ($file in $files) {
            $scanned++
            if (Select-String -LiteralPath $file.FullName -SimpleMatch -Pattern $email -Quiet -ErrorAction SilentlyContinue) { $emailHits++ }
            if (Select-String -LiteralPath $file.FullName -SimpleMatch -Pattern $password -Quiet -ErrorAction SilentlyContinue) { $passwordHits++ }
            if (Select-String -LiteralPath $file.FullName -Pattern 'eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}' -Quiet -ErrorAction SilentlyContinue) { $tokenHits++ }
        }
    }
    Say "- files scanned under the Unity data, the profile data and the out folder: $scanned"
    Say "- files that hold the email: $emailHits, files that hold the password: $passwordHits, files that hold a JWT-shaped token: $tokenHits"
}

# Activates the license with the account email and password. The client takes
# both values as command-line arguments. No project and no vendor code runs
# while the client runs.
function Invoke-LicenseActivation {
    $paths = Get-RunPaths
    # The licensing client takes the account email and password as command-line
    # arguments. Both secrets leave this process environment before the client
    # starts.
    $email = ([string]$env:UNITY_EMAIL).Trim()
    $password = ([string]$env:UNITY_PASSWORD).Trim()
    Remove-Item Env:UNITY_EMAIL -ErrorAction SilentlyContinue
    Remove-Item Env:UNITY_PASSWORD -ErrorAction SilentlyContinue
    $credential = Test-UnityCredential -Email $email -Password $password
    $email = $credential.Email
    $password = $credential.Password
    $client = Find-LicensingClient
    if (-not $client) { throw 'Unity.Licensing.Client.exe was not found in the Editor install.' }
    # The lock file sits next to this script. The admin copy holds its own
    # lock file, so the check always reads the copy that runs.
    $lock = Get-Content -Path (Join-Path $PSScriptRoot 'lock.json') -Raw | ConvertFrom-Json
    $signature = Get-AuthenticodeSignature -FilePath $client.FullName
    Assert-LicensingClientSubject $signature ([string]$lock.licensingClientSubject)
    Say '## license activation'
    Say "- licensing client under the Editor install: $($client.FullName.Substring($paths.UnityHome.Length))"
    Say "- licensing client version: $($client.VersionInfo.ProductVersion), signed by Unity Technologies: True"
    New-Item -ItemType Directory -Force -Path $paths.Out | Out-Null
    $outFile = Join-Path $paths.Out 'license-client.out'
    $errFile = Join-Path $paths.Out 'license-client.err'
    $arguments = ConvertTo-LicensingArguments -Email $email -Password $password
    $run = Start-AsLowUser $client.FullName $arguments $outFile $errFile 600000
    $finished = [bool]$run['Finished']
    $exitCode = [int]$run['ExitCode']
    $lines = @()
    foreach ($file in @($outFile, $errFile)) {
        if (Test-Path -LiteralPath $file) { $lines += @(Get-Content -LiteralPath $file) }
    }
    Remove-Item -Force $outFile, $errFile -ErrorAction SilentlyContinue
    Say "- client finished in time: $finished, exit code: $exitCode, output lines: $($lines.Count)"
    $shown = @($lines | Where-Object { $_ -match 'error|fail|success|activated|invalid|denied|not found|expired' } | Select-Object -First 20)
    Show-RedactedLines $shown $email $password
    if (Test-Path -LiteralPath $paths.LicensePath) {
        Say "- license file present after activation: True, size: $((Get-Item -LiteralPath $paths.LicensePath).Length) bytes"
    }
    else { Say '- license file present after activation: False' }
    if ($exitCode -ne 0) {
        $clientLog = Join-Path (Get-LowProfile) 'AppData\Local\Unity\Unity.Licensing.Client.log'
        if (Test-Path -LiteralPath $clientLog) {
            $logHits = @(Get-Content -LiteralPath $clientLog | Where-Object { $_ -match 'error|fail' } | Select-Object -Last 20)
            Say "- client log, last error lines: $($logHits.Count)"
            Show-RedactedLines $logHits $email $password
        }
    }
    Measure-CredentialResidue $email $password
    if ($exitCode -ne 0) { throw 'The licensing client did not activate the license.' }
}

# Returns the license. The step runs even when the activation never happened.
function Invoke-LicenseReturn {
    $paths = Get-RunPaths
    $client = $null
    if (-not (Test-Path -LiteralPath $paths.LowSecretFile)) { Say '- the low user was never created, so nothing to return' }
    else { $client = Find-LicensingClient }
    if (-not $client) { Say '- no licensing client to return the license with' }
    else {
        New-Item -ItemType Directory -Force -Path $paths.Out | Out-Null
        $outFile = Join-Path $paths.Out 'license-return.out'
        $errFile = Join-Path $paths.Out 'license-return.err'
        $run = Start-AsLowUser $client.FullName '--return-ulf' $outFile $errFile 120000
        if ([bool]$run['Finished']) { Say "- license return exit code: $($run['ExitCode'])" } else { Say '- license return timed out' }
        Remove-Item -Force $outFile, $errFile -ErrorAction SilentlyContinue
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    if ($Action -eq 'Activate') { Invoke-LicenseActivation }
    elseif ($Action -eq 'Return') { Invoke-LicenseReturn }
    else { throw 'The action must be Activate or Return.' }
}
