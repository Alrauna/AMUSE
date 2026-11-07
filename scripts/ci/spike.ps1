# THROWAWAY spike for docs/superpowers/specs/2026-11-06-ci-test-gate-design.md,
# section 7. It answers S1 to S8 and prints facts into the job summary. The
# repository is public, so the script never prints a raw Unity log. It prints
# only allowlisted facts and counters. Licensing lines are counted, never shown.
param(
    [Parameter(Mandatory)]
    [ValidateSet('InstallUnity', 'InstallPackages', 'PlaceLicense', 'Probe',
        'ProductCaptureTests', 'ProductShaderTests', 'ResearchTests', 'Cleanup')]
    [string]$Phase
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$work = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
$unityHome = Join-Path $work 'UnityEditor'
$unityExe = Join-Path $unityHome 'Editor\Unity.exe'
$licenseDir = Join-Path $env:ProgramData 'Unity'
$licensePath = Join-Path $licenseDir 'Unity_lic.ulf'
$clock = [System.Diagnostics.Stopwatch]::StartNew()

function Say([string]$text) {
    Write-Host $text
    if ($env:GITHUB_STEP_SUMMARY) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $text }
}

function Say-Resources {
    $drive = Get-PSDrive -Name ($root.Substring(0, 1))
    Say "- free disk: $([int]($drive.Free / 1GB)) GB"
}

# Last line of defense for the few lines that are printed.
function Redact([string]$line) {
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

function Invoke-Unity([string[]]$unityArgs, [string]$logPath) {
    $all = @('-batchmode', '-force-d3d11', '-projectPath', $root, '-logFile', $logPath) + $unityArgs
    $process = Start-Process -FilePath $unityExe -ArgumentList $all -PassThru -Wait -NoNewWindow
    return $process.ExitCode
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

function Invoke-TestRun([string]$assembly, [string]$filter, [string]$name) {
    $log = Join-Path $work "unity-$name.log"
    $xmlPath = Join-Path $work "TestResults-$name.xml"
    $unityArgs = @('-runTests', '-testPlatform', 'EditMode', '-assemblyNames', $assembly, '-testResults', $xmlPath)
    if ($filter) { $unityArgs += @('-testFilter', $filter) }
    $exit = Invoke-Unity $unityArgs $log
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
                foreach ($dependency in $packageJson.vpmDependencies.PSObject.Properties.Name) {
                    if ($lockedNames -notcontains $dependency) { $missing += "$($package.Name) needs $dependency" }
                }
            }
        }
        Say "- vpm dependencies outside the locked block: $(if ($missing.Count) { $missing -join ', ' } else { 'none' })"
        # Keep git-fetched tooling and Linux toolchains out of the CI project.
        $unityManifestPath = Join-Path $root 'Packages/manifest.json'
        $unityManifest = Get-Content -Path $unityManifestPath -Raw | ConvertFrom-Json
        foreach ($name in @($unityManifest.dependencies.PSObject.Properties.Name)) {
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

    'PlaceLicense' {
        if ([string]::IsNullOrWhiteSpace($env:UNITY_LICENSE)) { throw 'UNITY_LICENSE is empty.' }
        New-Item -ItemType Directory -Force -Path $licenseDir | Out-Null
        [IO.File]::WriteAllText($licensePath, $env:UNITY_LICENSE, (New-Object Text.UTF8Encoding($false)))
        Say '## S2 license'
        Say '- license file placed. The probe step shows whether the editor accepts it.'
    }

    'Probe' {
        $log = Join-Path $work 'unity-probe.log'
        $exit = Invoke-Unity @('-executeMethod', 'SpikeProbe.Run') $log
        Say '## S3 S4 S5 probe'
        Say "- unity exit code: $exit"
        Say "- elapsed: $([int]$clock.Elapsed.TotalMinutes) min"
        Say-Resources
        $facts = Show-LogFacts $log
        $line = Get-Content -Path $log | Where-Object { $_ -match 'SPIKE-PROBE ' } | Select-Object -First 1
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
        Invoke-TestRun 'Alrauna.Amuse.Tests.Editor' 'Alrauna.Amuse.Tests.Editor.Host.UnityAlphaFieldEvidenceTests' 'product-capture'
    }

    'ProductShaderTests' {
        Invoke-TestRun 'Alrauna.Amuse.Tests.Editor' 'Alrauna.Amuse.Tests.Editor.Semantics' 'product-semantics'
    }

    'ResearchTests' {
        Invoke-TestRun 'Alrauna.Amuse.Research.Tests.Editor' '' 'research'
    }

    'Cleanup' {
        if (Test-Path $licensePath) { Remove-Item -Force $licensePath }
    }
}
