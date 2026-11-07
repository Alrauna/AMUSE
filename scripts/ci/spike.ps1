# THROWAWAY spike for docs/superpowers/specs/2026-11-06-ci-test-gate-design.md,
# section 7. It answers S1 to S11 and prints facts into the job summary. The
# repository is public, so the script never prints a raw Unity log or a raw
# licensing client output. It prints only allowlisted, redacted facts.
param(
    [Parameter(Mandatory)]
    [ValidateSet('InstallUnity', 'PrepareMode', 'ActivateLicense', 'InstallPackages', 'PrepareProject', 'MemoryProbe',
        'Probe', 'ProductCaptureTests', 'ProductShaderTests', 'ResearchTests', 'ReturnLicense', 'Cleanup')]
    [string]$Phase
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = if ($env:GITHUB_WORKSPACE) { $env:GITHUB_WORKSPACE } else { (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path }
$work = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$unityHome = Join-Path $work 'UnityEditor'
$unityExe = Join-Path $unityHome 'Editor\Unity.exe'
$licenseDir = Join-Path $env:ProgramData 'Unity'
$licensePath = Join-Path $licenseDir 'Unity_lic.ulf'

# Mode admin runs the Editor and the tests as the runner account. Mode lowuser runs
# them as a local user that is not an administrator. Open decision O6 asks which is safe.
$mode = if ($env:SPIKE_MODE) { $env:SPIKE_MODE } else { 'admin' }
if ($mode -notin @('admin', 'lowuser')) { throw 'SPIKE_MODE must be admin or lowuser.' }
$lowUser = 'amuseci'
$lowRoot = Join-Path $env:SystemDrive 'amuse-low'
$out = if ($mode -eq 'lowuser') { Join-Path $lowRoot 'out' } else { Join-Path $work 'out' }
$projectPath = if ($mode -eq 'lowuser') { Join-Path $lowRoot 'project' } else { $root }
$lowSecretFile = Join-Path $work 'lowuser.cred'
$adminScripts = Join-Path $work 'admin-scripts'
$clock = [System.Diagnostics.Stopwatch]::StartNew()

function Say([string]$text) {
    Write-Host $text
    if ($env:GITHUB_STEP_SUMMARY) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $text }
}

function Say-Resources {
    $drive = Get-PSDrive -Name ($root.Substring(0, 1))
    Say "- free disk: $([int]($drive.Free / 1GB)) GB"
}

# Last line of defense for the few lines that are printed. Two colons are split
# so that no printed line can act as a workflow command.
function Redact([string]$line) {
    $line = $line.Replace('::', ': :')
    $line = [regex]::Replace($line, '[\w.+-]+@[\w-]+(\.[\w-]+)+', '<email>')
    $line = [regex]::Replace($line, '[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}', '<guid>')
    $line = [regex]::Replace($line, '[0-9a-fA-F]{24,}', '<hex>')
    $line = [regex]::Replace($line, '[A-Za-z0-9+/=_-]{40,}', '<token>')
    return $line
}

function Get-UnityVersion {
    $line = Select-String -Path (Join-Path $root 'ProjectSettings/ProjectVersion.txt') -Pattern '^m_EditorVersion: (.+)$'
    return $line.Matches[0].Groups[1].Value.Trim()
}

function Get-Json([string]$uri) {
    # Some listings start with a byte order mark. Invoke-RestMethod then returns a string.
    $body = (Invoke-WebRequest -Uri $uri).Content
    if ($body -is [byte[]]) { $body = [Text.Encoding]::UTF8.GetString($body) }
    return ([string]$body).TrimStart([char]0xFEFF) | ConvertFrom-Json
}

function Invoke-Icacls([string[]]$arguments, [string]$label) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    & icacls @arguments | Out-Null
    $code = $LASTEXITCODE
    Say "- icacls $label exit code: $code, $([int]$timer.Elapsed.TotalSeconds) s"
    if ($code -ne 0) { throw "icacls failed for $label." }
}

function Get-LocalAppData {
    if ($mode -eq 'lowuser') { return (Join-Path $env:SystemDrive "Users\$lowUser\AppData\Local") }
    return $env:LOCALAPPDATA
}

function Get-LowCredential {
    $secret = (Get-Content -Path $lowSecretFile -Raw).Trim()
    return New-Object System.Management.Automation.PSCredential($lowUser, (ConvertTo-SecureString $secret -AsPlainText -Force))
}

# A process started with another user's credential would get a copy of this
# environment. Point the profile variables at the low user and blank every
# variable that names a runner file or an Actions service.
function Get-LowEnvironment {
    $profileDir = Join-Path $env:SystemDrive "Users\$lowUser"
    $local = Join-Path $profileDir 'AppData\Local'
    $variables = @{
        USERNAME = $lowUser; USERPROFILE = $profileDir; HOME = $profileDir; HOMEPATH = "\Users\$lowUser"
        LOCALAPPDATA = $local; APPDATA = (Join-Path $profileDir 'AppData\Roaming')
        TEMP = (Join-Path $local 'Temp'); TMP = (Join-Path $local 'Temp')
    }
    foreach ($item in @(Get-ChildItem Env:)) {
        if ($item.Name -match '^(GITHUB|RUNNER|ACTIONS|ImageOS|ImageVersion)') { $variables[$item.Name] = '' }
    }
    return $variables
}

function Stop-Tree([int]$processId) {
    & taskkill /PID $processId /T /F 2>&1 | Out-Null
}

# Starts a program in the chosen mode and waits for it. Output goes to files in both
# modes, so the rest of the script does not care which mode runs.
function Start-Tool([string]$file, [string]$argumentString, [string]$stdoutPath, [string]$stderrPath, [int]$timeoutMs) {
    $parameters = @{
        FilePath = $file; ArgumentList = $argumentString; PassThru = $true
        RedirectStandardOutput = $stdoutPath; RedirectStandardError = $stderrPath
    }
    if ($mode -eq 'lowuser') {
        $parameters.Credential = Get-LowCredential
        $parameters.LoadUserProfile = $true
        $parameters.WorkingDirectory = $out
        $parameters.Environment = Get-LowEnvironment
    }
    else { $parameters.NoNewWindow = $true }
    $process = Start-Process @parameters
    try { $cachedHandle = $process.Handle } catch { $cachedHandle = $null }
    $finished = $process.WaitForExit($timeoutMs)
    if ($finished) { $process.WaitForExit() } else { Stop-Tree $process.Id }
    return [pscustomobject]@{ Finished = $finished; ExitCode = $(if ($finished) { $process.ExitCode } else { -1 }) }
}

function Invoke-Unity([string[]]$unityArgs, [string]$logPath, [int]$timeoutMs) {
    $all = @('-batchmode', '-force-d3d11', '-projectPath', $projectPath, '-logFile', $logPath) + $unityArgs
    $result = Start-Tool $unityExe ($all -join ' ') (Join-Path $out 'unity.stdout') (Join-Path $out 'unity.stderr') $timeoutMs
    return $result.ExitCode
}

# Prints only allowlisted, redacted facts. Returns shader and compile error counts.
function Show-LogFacts([string]$logPath) {
    if (-not (Test-Path $logPath)) { Say "No log at the expected path."; return [pscustomobject]@{ Shader = 0; Compile = 0 } }
    $lines = @(Get-Content -Path $logPath)
    $shader = @($lines | Where-Object { $_ -match 'Shader error in' })
    $compile = @($lines | Where-Object { $_ -match 'error CS\d+|Scripts have compiler errors|Compilation failed' })
    $device = @($lines | Where-Object { $_ -match '^Direct3D:|^\s+(Version|Renderer|Vendor):' } | Select-Object -First 6)
    $licenseLines = @($lines | Where-Object { $_ -match 'icens' })
    $noLicense = @($lines | Where-Object { $_ -match 'No valid Unity Editor license found' }).Count
    Write-Host '::group::allowlisted Unity log facts'
    $device | ForEach-Object { Write-Host (Redact $_) }
    $shader | Select-Object -First 20 | ForEach-Object { Write-Host (Redact $_) }
    $compile | Select-Object -First 20 | ForEach-Object { Write-Host (Redact $_) }
    Write-Host '::endgroup::'
    Say "- log lines about licensing: $($licenseLines.Count), 'no valid license' lines: $noLicense"
    Say "- shader error lines: $($shader.Count), compile error lines: $($compile.Count)"
    return [pscustomobject]@{ Shader = $shader.Count; Compile = $compile.Count }
}

function Find-LicensingClient {
    if (-not (Test-Path $unityHome)) { return $null }
    $expected = Join-Path $unityHome 'Editor\Data\Resources\Licensing\Client\Unity.Licensing.Client.exe'
    if (Test-Path $expected) { return Get-Item $expected }
    return Get-ChildItem -Path $unityHome -Recurse -Filter 'Unity.Licensing.Client.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
}

# The account password goes to this program. Stop unless Unity Technologies signed it.
function Assert-UnitySigned([string]$path) {
    $signature = Get-AuthenticodeSignature -FilePath $path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Unity Technologies') {
        throw "The licensing client is not signed by Unity Technologies. Status: $($signature.Status)."
    }
}

# Prints the lines with the account values replaced. Numbers, ids and emails go too.
function Show-RedactedLines([object[]]$lines, [string]$email, [string]$password) {
    foreach ($line in $lines) {
        $text = ([string]$line).Replace($email, '<account>').Replace($password, '<secret>')
        $text = [regex]::Replace((Redact $text), '\d{6,}', '<num>')
        if ($text.Length -gt 200) { $text = $text.Substring(0, 200) }
        Say "  - $text"
    }
}

# Counts only. It prints no value. It looks for the account credentials, and for
# tokens that look like JWTs, in the places where later code in the same job could read them.
function Measure-CredentialResidue([string]$email, [string]$password) {
    $holders = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and ($_.CommandLine.Contains($password) -or $_.CommandLine.Contains($email)) })
    Say "- processes whose command line holds the email or password: $($holders.Count)"
    $profileData = if ($mode -eq 'lowuser') { Join-Path $env:SystemDrive "Users\$lowUser\AppData" } else { Split-Path (Get-LocalAppData) -Parent }
    $roots = @((Join-Path $env:ProgramData 'Unity'), $profileData, $out) | Where-Object { $_ -and (Test-Path $_) }
    $scanned = 0
    $hits = 0
    $tokenHits = 0
    foreach ($rootDir in $roots) {
        $files = @(Get-ChildItem -Path $rootDir -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Length -lt 20MB } | Select-Object -First 3000)
        foreach ($file in $files) {
            $scanned++
            if (Select-String -Path $file.FullName -SimpleMatch -Pattern $password, $email -Quiet -ErrorAction SilentlyContinue) { $hits++ }
            if (Select-String -Path $file.FullName -Pattern 'eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}' -Quiet -ErrorAction SilentlyContinue) { $tokenHits++ }
        }
    }
    Say "- files scanned under the Unity data, the profile data and the out folder: $scanned"
    Say "- files that hold the email or password: $hits, files that hold a JWT-shaped token: $tokenHits"
}

# Spike only. Prints a small, redacted set of licensing lines. They show why the
# editor refuses the license. Numbers, ids and emails are removed first.
function Show-LicenseFacts([string]$logPath) {
    if (-not (Test-Path $logPath)) { return }
    $pattern = 'error|fail|no valid|not found|activat|expired|invalid|denied|entitle|binding|machine'
    $hits = @(Get-Content -Path $logPath | Where-Object { $_ -match 'icens' -and $_ -match $pattern } | Select-Object -First 25)
    Say "- licensing diagnostics, $($hits.Count) lines:"
    foreach ($hit in $hits) {
        $text = Redact $hit
        $text = [regex]::Replace($text, '\d{6,}', '<num>')
        if ($text.Length -gt 200) { $text = $text.Substring(0, 200) }
        Say "  - $text"
    }
}

function Read-NUnitResult([string]$xmlPath) {
    if (-not (Test-Path $xmlPath)) { throw 'The result file is missing.' }
    [xml]$xml = Get-Content -Path $xmlPath -Raw
    $run = $xml.'test-run'
    $failed = @($xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object { $_.fullname })
    return [pscustomobject]@{
        Total = [int]$run.total; Passed = [int]$run.passed; Failed = [int]$run.failed
        Skipped = [int]$run.skipped; Inconclusive = [int]$run.inconclusive; FailedNames = $failed
    }
}

function Invoke-TestRun([string]$assembly, [string]$filter, [string]$name, [int]$timeoutMs) {
    $log = Join-Path $out "unity-$name.log"
    $xmlPath = Join-Path $out "TestResults-$name.xml"
    $unityArgs = @('-runTests', '-testPlatform', 'EditMode', '-assemblyNames', $assembly, '-testResults', $xmlPath)
    if ($filter) { $unityArgs += @('-testFilter', $filter) }
    $exit = Invoke-Unity $unityArgs $log $timeoutMs
    Say "### $name"
    Say "- unity exit code: $exit"
    Say "- elapsed: $([int]$clock.Elapsed.TotalMinutes) min"
    Say-Resources
    $facts = Show-LogFacts $log
    Remove-Item -Force $log -ErrorAction SilentlyContinue
    $result = Read-NUnitResult $xmlPath
    Say "- tests: total $($result.Total), passed $($result.Passed), failed $($result.Failed), skipped $($result.Skipped), inconclusive $($result.Inconclusive)"
    $result.FailedNames | Select-Object -First 40 | ForEach-Object { Say "  - FAILED $_" }
    if ($result.Total -eq 0) { throw 'The run reported zero tests.' }
    if ($result.Failed -gt 0 -or $exit -ne 0) { throw "The run failed (exit $exit)." }
    if ($facts.Shader -gt 0 -or $facts.Compile -gt 0) { throw 'The log holds shader or compile errors.' }
}

switch ($Phase) {

    'InstallUnity' {
        $version = Get-UnityVersion
        $release = Invoke-RestMethod "https://services.api.unity.com/unity/editor/release/v1/releases?version=$version&limit=1"
        $download = @($release.results[0].downloads | Where-Object { $_.platform -eq 'WINDOWS' -and $_.type -eq 'EXE' })[0]
        if (-not $download.url.StartsWith('https://download.unity3d.com/')) { throw "Unexpected installer host: $($download.url)" }
        if ($download.integrity -notmatch '^md5-(.+)$') { throw 'The release API gave no md5 integrity value.' }
        $expectedMd5 = [Text.Encoding]::ASCII.GetString([Convert]::FromBase64String($Matches[1])).ToLowerInvariant()
        $installer = Join-Path $work 'UnitySetup64.exe'
        Invoke-WebRequest -Uri $download.url -OutFile $installer
        $actualMd5 = (Get-FileHash -Algorithm MD5 -Path $installer).Hash.ToLowerInvariant()
        $actualSha256 = (Get-FileHash -Algorithm SHA256 -Path $installer).Hash.ToLowerInvariant()
        $signature = Get-AuthenticodeSignature -FilePath $installer
        $signedByUnity = $signature.Status -eq 'Valid' -and $signature.SignerCertificate.Subject -match 'Unity Technologies'
        Say "## S1 Unity installer $version"
        Say "- md5 equals the release API value: $($actualMd5 -eq $expectedMd5)"
        Say "- observed installer sha256: $actualSha256"
        Say "- Authenticode status: $($signature.Status), signed by Unity Technologies: $signedByUnity"
        Say "- signer subject: $($signature.SignerCertificate.Subject)"
        if ($actualMd5 -ne $expectedMd5) { throw 'Installer md5 mismatch.' }
        if (-not $signedByUnity) { throw 'Installer signature is not a valid Unity Technologies signature.' }
        Start-Process -FilePath $installer -ArgumentList '/S', "/D=$unityHome" -Wait
        Remove-Item -Force $installer
        if (-not (Test-Path $unityExe)) { throw 'Unity.exe is missing after install.' }
        Say "- installed, elapsed $([int]$clock.Elapsed.TotalMinutes) min"
        Say-Resources
    }

    'PrepareMode' {
        New-Item -ItemType Directory -Force -Path $out | Out-Null
        Say "## mode: $mode"
        Say "- image version: $env:ImageVersion, session id: $([Diagnostics.Process]::GetCurrentProcess().SessionId), session name: $env:SESSIONNAME"
        # Later steps run these copies. The workspace stays writable for the project, so
        # nothing that an administrator step runs may live in it.
        if (Test-Path $adminScripts) { Remove-Item -Recurse -Force $adminScripts }
        Copy-Item -Recurse -Path (Join-Path $root 'scripts/ci') -Destination $adminScripts
        Invoke-Icacls @($adminScripts, '/inheritance:r', '/grant:r', 'Administrators:(OI)(CI)F', 'SYSTEM:(OI)(CI)F') 'admin scripts'
        if ($mode -eq 'lowuser') {
            $bytes = New-Object byte[] 24
            [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
            $generated = 'Aa1!' + [Convert]::ToBase64String($bytes).Replace('+', 'x').Replace('/', 'y').Replace('=', 'z')
            # The /y switch answers the prompt that net user shows for a password longer than 14 characters.
            & net user $lowUser $generated /add /y | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'The low user could not be created.' }
            Set-Content -Path $lowSecretFile -Value $generated -NoNewline
            $generated = $null
            Invoke-Icacls @($lowSecretFile, '/inheritance:r', '/grant:r', 'Administrators:F', 'SYSTEM:F') 'credential file'
            $inAdministrators = @(& net localgroup Administrators) -match ('^' + [regex]::Escape($lowUser) + '$')
            Say "- low user created, member of Administrators: $([bool]$inAdministrators)"
            # The low user gets its own folder for the project and the output, and read access to
            # the Editor install. It gets no write access anywhere under the runner folders.
            New-Item -ItemType Directory -Force -Path $lowRoot, $out, $licenseDir | Out-Null
            Invoke-Icacls @($lowRoot, '/grant', "${lowUser}:(OI)(CI)M") 'low folder'
            Invoke-Icacls @($licenseDir, '/grant', "${lowUser}:(OI)(CI)M") 'license folder'
            Invoke-Icacls @($unityHome, '/grant', "${lowUser}:(OI)(CI)RX") 'Editor install'
            $denyRights = '(WD,AD,WEA,WA,D,DC)'
            Invoke-Icacls @((Split-Path $work -Parent), '/deny', "${lowUser}:(OI)(CI)$denyRights") 'agent work folder deny'
            $worker = Get-Process -Name 'Runner.Worker' -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($worker -and $worker.Path) {
                $runnerRoot = Split-Path (Split-Path $worker.Path -Parent) -Parent
                Invoke-Icacls @($runnerRoot, '/deny', "${lowUser}:(OI)(CI)$denyRights") 'runner folder deny'
            }
            else { Say '- no Runner.Worker process found, so the runner folder deny was skipped' }
        }
        else { Say '- admin mode: the Editor and the tests run as the runner account' }
    }

    'PrepareProject' {
        if ($mode -eq 'lowuser') {
            # The low user works on a private copy. It never gets write access to the workspace.
            & robocopy $root $projectPath /E /XD .git /NFL /NDL /NJH /NJS /NP | Out-Null
            if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE." }
            Say "- project copied for the low user, robocopy exit code: $LASTEXITCODE"
            $global:LASTEXITCODE = 0
        }
    }

    'ActivateLicense' {
        # The licensing client takes the account email and password as command-line
        # arguments. Both secrets leave this process environment before the client starts.
        $email = ([string]$env:UNITY_EMAIL).Trim()
        $password = ([string]$env:UNITY_PASSWORD).Trim()
        Remove-Item Env:UNITY_EMAIL -ErrorAction SilentlyContinue
        Remove-Item Env:UNITY_PASSWORD -ErrorAction SilentlyContinue
        if ([string]::IsNullOrWhiteSpace($email) -or [string]::IsNullOrWhiteSpace($password)) { throw 'UNITY_EMAIL or UNITY_PASSWORD is empty.' }
        if ($email -notmatch '^[A-Za-z0-9._+-]+@[A-Za-z0-9.-]+$') { throw 'UNITY_EMAIL has an unexpected shape.' }
        # No shell reads this command line, so most special characters are safe.
        # A quote or a space would end the quoted argument early, so stop on those.
        if ($password -match '["\s]') {
            throw "UNITY_PASSWORD must not hold a quote or a space. Has a quote: $($password.Contains('"')). Has whitespace inside: $($password -match '\s')."
        }
        $client = Find-LicensingClient
        if (-not $client) { throw 'Unity.Licensing.Client.exe was not found in the Editor install.' }
        Assert-UnitySigned $client.FullName
        Say '## S2 license activation'
        Say "- licensing client under the Editor install: $($client.FullName.Substring($unityHome.Length))"
        Say "- licensing client version: $($client.VersionInfo.ProductVersion), signed by Unity Technologies: True"
        $outFile = Join-Path $out 'license-client.out'
        $errFile = Join-Path $out 'license-client.err'
        # A backslash at the end of the password would escape the closing quote, so double it.
        $quotedPassword = [regex]::Replace($password, '(\\+)$', '$1$1')
        $arguments = "--activate-ulf --username `"$email`" --password `"$quotedPassword`" --include-personal"
        $run = Start-Tool $client.FullName $arguments $outFile $errFile 600000
        $finished = $run.Finished
        $exitCode = $run.ExitCode
        $lines = @()
        foreach ($file in @($outFile, $errFile)) { if (Test-Path $file) { $lines += @(Get-Content -Path $file) } }
        Remove-Item -Force $outFile, $errFile -ErrorAction SilentlyContinue
        Say "- client finished in time: $finished, exit code: $exitCode, output lines: $($lines.Count)"
        $shown = @($lines | Where-Object { $_ -match 'error|fail|success|activated|invalid|denied|not found|expired' } | Select-Object -First 20)
        Show-RedactedLines $shown $email $password
        if (Test-Path $licensePath) { Say "- license file present after activation: True, size: $((Get-Item $licensePath).Length) bytes" }
        else { Say '- license file present after activation: False' }
        if ($exitCode -ne 0) {
            $clientLog = Join-Path (Get-LocalAppData) 'Unity\Unity.Licensing.Client.log'
            if (Test-Path $clientLog) {
                $logHits = @(Get-Content -Path $clientLog | Where-Object { $_ -match 'error|fail' } | Select-Object -Last 20)
                Say "- client log, last error lines: $($logHits.Count)"
                Show-RedactedLines $logHits $email $password
            }
        }
        Measure-CredentialResidue $email $password
        if ($exitCode -ne 0) { throw 'The licensing client did not activate the license.' }
    }

    'InstallPackages' {
        # Each package has one named source. A listing that shadows another cannot win.
        $sources = [ordered]@{
            vrchat = 'https://vrchat.github.io/packages/index.json'
            nadena = 'https://vpm.nadena.dev/vpm.json'
            poiyomi = 'https://poiyomi.github.io/vpm/index.json'
            liltoon = 'https://lilxyzw.github.io/vpm-repos/vpm.json'
            aao = 'https://vpm.anatawa12.com/vpm.json'
            d4rk = 'https://d4rkc0d3r.github.io/vpm-repos/main.json'
            vrcfury = 'https://vcc.vrcfury.com/'
            redsim = 'https://redsim.github.io/vpmlisting/index.json'
            ltcgi = 'https://vpm.pimaker.at/index.json'
        }
        $packageSource = @{
            'nadena.dev.ndmf' = 'nadena'; 'nadena.dev.modular-avatar' = 'nadena'
            'com.vrchat.base' = 'vrchat'; 'com.vrchat.avatars' = 'vrchat'
            'com.poiyomi.toon' = 'poiyomi'; 'com.poiyomi.thryeditor' = 'poiyomi'
            'jp.lilxyzw.liltoon' = 'liltoon'; 'com.anatawa12.avatar-optimizer' = 'aao'
            'd4rkpl4y3r.d4rkavataroptimizer' = 'd4rk'; 'com.vrcfury.vrcfury' = 'vrcfury'
            'red.sim.lightvolumes' = 'redsim'; 'at.pimaker.ltcgi' = 'ltcgi'
        }
        $listings = @{}
        foreach ($key in $sources.Keys) { $listings[$key] = Get-Json $sources[$key] }
        $manifest = Get-Content -Path (Join-Path $root 'Packages/vpm-manifest.json') -Raw | ConvertFrom-Json
        $locked = @($manifest.locked.PSObject.Properties | ForEach-Object { [pscustomobject]@{ Name = $_.Name; Version = $_.Value.version } })
        $lockedNames = @($locked | ForEach-Object { $_.Name })
        Say '## S8 vendor packages'
        Say '| package | version | source | hash route | verified |'
        Say '| --- | --- | --- | --- | --- |'
        $failures = @()
        foreach ($package in $locked) {
            $sourceKey = $packageSource[$package.Name]
            if (-not $sourceKey) { $failures += "$($package.Name): no source is named for it"; continue }
            $entry = $null
            $property = $listings[$sourceKey].packages.PSObject.Properties[$package.Name]
            if ($property) {
                $versionProperty = $property.Value.versions.PSObject.Properties[$package.Version]
                if ($versionProperty) { $entry = $versionProperty.Value }
            }
            if (-not $entry) { $failures += "$($package.Name) $($package.Version): not in its listing"; Say "| $($package.Name) | $($package.Version) | $sourceKey | none | no |"; continue }
            $expected = $null; $route = 'none'
            if ($entry.PSObject.Properties['zipSHA256'] -and $entry.zipSHA256) { $expected = $entry.zipSHA256.ToLowerInvariant(); $route = 'listing zipSHA256' }
            elseif ($entry.url -match '^https://github\.com/([^/]+)/([^/]+)/releases/download/([^/]+)/([^?]+)') {
                $owner = $Matches[1]; $repo = $Matches[2]; $tag = $Matches[3]; $file = $Matches[4]
                try {
                    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$owner/$repo/releases/tags/$tag" -Headers @{ 'User-Agent' = 'amuse-ci-spike' }
                    $asset = @($release.assets | Where-Object { $_.name -eq $file })[0]
                    if ($asset -and $asset.digest -match '^sha256:(.+)$') { $expected = $Matches[1].ToLowerInvariant(); $route = 'GitHub release digest' }
                }
                catch { $route = 'GitHub digest lookup failed' }
            }
            $zip = Join-Path $work "$($package.Name).zip"
            Invoke-WebRequest -Uri $entry.url -OutFile $zip
            $actual = (Get-FileHash -Algorithm SHA256 -Path $zip).Hash.ToLowerInvariant()
            $verified = "no hash available, observed $actual"
            if ($expected) {
                if ($actual -ne $expected) { $failures += "$($package.Name) $($package.Version): hash mismatch"; $verified = 'MISMATCH' } else { $verified = 'yes' }
            }
            Say "| $($package.Name) | $($package.Version) | $sourceKey | $route | $verified |"
            $target = Join-Path $root "Packages/$($package.Name)"
            if (Test-Path $target) { Remove-Item -Recurse -Force $target }
            Expand-Archive -Path $zip -DestinationPath $target
            Remove-Item -Force $zip
        }
        # Dependencies that the locked block does not list.
        $missing = @()
        foreach ($package in $locked) {
            $packageJsonPath = Join-Path $root "Packages/$($package.Name)/package.json"
            if (-not (Test-Path $packageJsonPath)) { continue }
            $packageJson = Get-Content -Path $packageJsonPath -Raw | ConvertFrom-Json
            if ($packageJson.PSObject.Properties['vpmDependencies']) {
                foreach ($dependency in @($packageJson.vpmDependencies.PSObject.Properties | ForEach-Object { $_.Name })) {
                    if ($lockedNames -notcontains $dependency) { $missing += "$($package.Name) needs $dependency" }
                }
            }
        }
        Say "- vpm dependencies outside the locked block: $(if ($missing.Count) { $missing -join ', ' } else { 'none' })"
        # Keep git-fetched tooling and Linux toolchains out of the CI project.
        $unityManifestPath = Join-Path $root 'Packages/manifest.json'
        $unityManifest = Get-Content -Path $unityManifestPath -Raw | ConvertFrom-Json
        foreach ($name in @($unityManifest.dependencies.PSObject.Properties | ForEach-Object { $_.Name })) {
            if ($name -eq 'com.coplaydev.unity-mcp' -or $name -like 'com.unity.toolchain.*') {
                $unityManifest.dependencies.PSObject.Properties.Remove($name)
            }
        }
        $unityManifest | ConvertTo-Json -Depth 10 | Set-Content -Path $unityManifestPath -Encoding utf8
        # The graphics probe lives outside Assets in the repository.
        $probeDir = Join-Path $root 'Assets/_SpikeProbe/Editor'
        New-Item -ItemType Directory -Force -Path $probeDir | Out-Null
        Copy-Item -Path (Join-Path $PSScriptRoot 'spike/SpikeProbe.cs') -Destination $probeDir
        Say "- elapsed $([int]$clock.Elapsed.TotalMinutes) min"
        Say-Resources
        if ($failures.Count) { throw ($failures -join '; ') }
    }

    'MemoryProbe' {
        # Runs as the chosen mode. It measures two things and prints counts only.
        # 1. Whether this mode can open the runner processes with the right to read memory.
        # 2. Whether this mode can write to the places that administrator steps run from.
        # It reads no memory and changes no file content.
        $runnerRoot = $null
        $worker = Get-Process -Name 'Runner.Worker' -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($worker -and $worker.Path) { $runnerRoot = Split-Path (Split-Path $worker.Path -Parent) -Parent }
        $targets = @(
            @{ label = 'admin scripts folder'; path = $adminScripts },
            @{ label = 'admin scripts file'; path = (Join-Path $adminScripts 'spike.ps1') },
            @{ label = 'workspace'; path = $root },
            @{ label = 'runner temp folder'; path = $work },
            @{ label = 'runner actions folder'; path = (Join-Path (Split-Path $work -Parent) '_actions') },
            @{ label = 'Editor install'; path = $unityHome },
            @{ label = 'license folder'; path = $licenseDir }
        )
        if ($runnerRoot) {
            $targets += @{ label = 'runner root'; path = $runnerRoot }
            $targets += @{ label = 'runner bin'; path = (Join-Path $runnerRoot 'bin') }
            $targets += @{ label = 'runner externals'; path = (Join-Path $runnerRoot 'externals') }
        }
        $targetsFile = Join-Path $out 'probe-targets.json'
        ConvertTo-Json -InputObject $targets | Set-Content -Path $targetsFile
        $probeScript = Join-Path $out 'probe.ps1'
        $probeBody = @'
param([string]$TargetsFile)
$ErrorActionPreference = 'Continue'
try {
    Add-Type -Namespace Probe -Name Native -MemberDefinition '[DllImport("kernel32.dll", SetLastError=true)] public static extern System.IntPtr OpenProcess(uint access, bool inherit, int pid); [DllImport("kernel32.dll")] public static extern bool CloseHandle(System.IntPtr handle);'
} catch { "add-type failed" }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
"identity: $($identity.Name)"
"is administrator: $(([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))"
"profile folder set: $($env:USERPROFILE -like ('*' + ($identity.Name -split '\\')[-1] + '*')), github file variables blank: $([string]::IsNullOrEmpty($env:GITHUB_ENV) -and [string]::IsNullOrEmpty($env:GITHUB_PATH))"
$own = [Probe.Native]::OpenProcess(0x0410, $false, $PID)
"control, own process readable: $($own -ne [IntPtr]::Zero)"
if ($own -ne [IntPtr]::Zero) { [void][Probe.Native]::CloseHandle($own) }
foreach ($name in 'Runner.Worker', 'Runner.Listener') {
    $found = @(Get-Process -Name $name -ErrorAction SilentlyContinue)
    $readable = 0
    $codes = @()
    foreach ($p in $found) {
        $h = [Probe.Native]::OpenProcess(0x0410, $false, $p.Id)
        if ($h -ne [IntPtr]::Zero) { $readable++; [void][Probe.Native]::CloseHandle($h) }
        else { $codes += [Runtime.InteropServices.Marshal]::GetLastWin32Error() }
    }
    "$name processes found: $($found.Count), readable for memory: $readable, error codes: $($codes -join ',')"
}
$targets = @(Get-Content -Path $TargetsFile -Raw | ConvertFrom-Json)
foreach ($t in $targets) {
    $ok = $false
    if (Test-Path -LiteralPath $t.path -PathType Container) {
        $marker = Join-Path $t.path ('.amuse-write-test-' + [guid]::NewGuid().ToString('N'))
        try { [IO.File]::WriteAllText($marker, 'x'); Remove-Item -LiteralPath $marker -Force; $ok = $true } catch { }
    }
    elseif (Test-Path -LiteralPath $t.path) {
        try { $s = [IO.File]::Open($t.path, 'Open', 'Write'); $s.Close(); $ok = $true } catch { }
    }
    else { "writable, $($t.label): path not found"; continue }
    "writable, $($t.label): $ok"
}
'@
        Set-Content -Path $probeScript -Value $probeBody
        $pwsh = (Get-Command pwsh).Source
        $stdoutPath = Join-Path $out 'probe.out'
        $run = Start-Tool $pwsh "-NoProfile -File `"$probeScript`" -TargetsFile `"$targetsFile`"" $stdoutPath (Join-Path $out 'probe.err') 180000
        Say '## O6 memory read and write probe'
        Say "- mode: $mode, finished: $($run.Finished), exit code: $($run.ExitCode)"
        if (Test-Path $stdoutPath) { Get-Content -Path $stdoutPath | ForEach-Object { Say "- $(Redact $_)" } }
        $errPath = Join-Path $out 'probe.err'
        if ((Test-Path $errPath) -and (Get-Item $errPath).Length -gt 0) { Say "- the probe wrote $((Get-Item $errPath).Length) bytes of error output" }
    }

    'Probe' {
        $log = Join-Path $out 'unity-probe.log'
        $exit = Invoke-Unity @('-executeMethod', 'SpikeProbe.Run') $log 6600000
        Say '## S3 S4 S5 probe'
        $adapters = @(Get-CimInstance Win32_VideoController -ErrorAction SilentlyContinue | ForEach-Object { $_.Name })
        Say "- video controllers on the runner: $($adapters -join ', ')"
        Say "- unity exit code: $exit"
        Say "- elapsed: $([int]$clock.Elapsed.TotalMinutes) min"
        Say-Resources
        $facts = Show-LogFacts $log
        $line = Get-Content -Path $log | Where-Object { $_ -match 'SPIKE-PROBE ' } | Select-Object -First 1
        if (-not $line) {
            Show-LicenseFacts $log
            $log2 = Join-Path $out 'unity-license-check.log'
            $check = Start-Tool $unityExe "-batchmode -nographics -quit -logFile `"$log2`"" (Join-Path $out 'check.stdout') (Join-Path $out 'check.stderr') 600000
            Say "- no-graphics license check exit code: $($check.ExitCode)"
            Show-LicenseFacts $log2
            Remove-Item -Force $log2 -ErrorAction SilentlyContinue
        }
        Remove-Item -Force $log -ErrorAction SilentlyContinue
        if (-not $line) { Say '- no probe line: the editor did not run the probe. The counters above show license, compile and shader state.'; throw 'No probe result.' }
        $result = ($line -replace '^.*SPIKE-PROBE ', '') | ConvertFrom-Json
        Say "- device type: $($result.deviceType)"
        Say "- device name: $(Redact $result.deviceName)"
        Say "- async readback supported: $($result.asyncReadback)"
        Say "- R8_UNorm render supported: $($result.r8Render), read pixels supported: $($result.r8ReadPixels)"
        Say "- blit and readback worked: $($result.blitReadbackOk), value: $($result.blitReadbackValue), error: $(Redact $result.error)"
        if ($result.deviceType -ne 'Direct3D11') { throw "Device type is $($result.deviceType), not Direct3D11." }
        if (-not ($result.asyncReadback -and $result.r8Render -and $result.r8ReadPixels -and $result.blitReadbackOk)) { throw 'The alpha capture path does not work on this device.' }
        if ($facts.Shader -gt 0 -or $facts.Compile -gt 0) { throw 'The log holds shader or compile errors.' }
    }

    'ProductCaptureTests' {
        Invoke-TestRun 'Alrauna.Amuse.Tests.Editor' 'Alrauna.Amuse.Tests.Editor.Host.UnityAlphaFieldEvidenceTests' 'product-capture' 3300000
    }

    'ProductShaderTests' {
        Invoke-TestRun 'Alrauna.Amuse.Tests.Editor' 'Alrauna.Amuse.Tests.Editor.Semantics' 'product-semantics' 5100000
    }

    'ResearchTests' {
        Invoke-TestRun 'Alrauna.Amuse.Research.Tests.Editor' '' 'research' 3300000
    }

    'ReturnLicense' {
        $client = $null
        if ($mode -eq 'lowuser' -and -not (Test-Path $lowSecretFile)) { Say '- the low user was never created, so nothing to return' }
        else { $client = Find-LicensingClient }
        if (-not $client) { Say '- no licensing client to return the license with' }
        else {
            New-Item -ItemType Directory -Force -Path $out | Out-Null
            $outFile = Join-Path $out 'license-return.out'
            $errFile = Join-Path $out 'license-return.err'
            $run = Start-Tool $client.FullName '--return-ulf' $outFile $errFile 120000
            if ($run.Finished) { Say "- license return exit code: $($run.ExitCode)" } else { Say '- license return timed out' }
            Remove-Item -Force $outFile, $errFile -ErrorAction SilentlyContinue
        }
    }

    'Cleanup' {
        if ($mode -eq 'lowuser') {
            # Stop everything the low user left running, then remove the user.
            foreach ($candidate in @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue)) {
                $owner = Invoke-CimMethod -InputObject $candidate -MethodName GetOwner -ErrorAction SilentlyContinue
                if ($owner -and $owner.User -eq $lowUser) { Stop-Tree ([int]$candidate.ProcessId) }
            }
            & net user $lowUser /delete 2>&1 | Out-Null
        }
        if (Test-Path $licensePath) { Remove-Item -Force $licensePath -ErrorAction SilentlyContinue }
        if (Test-Path $lowSecretFile) { Remove-Item -Force $lowSecretFile -ErrorAction SilentlyContinue }
    }
}
