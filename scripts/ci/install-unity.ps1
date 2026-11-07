# Installs the Unity Editor for the gate and the canary.
#
# The version comes from lock.json. The download url and the md5 come from
# the vendor release API. The installer must match the locked SHA-256, the
# API md5 and the locked signer subject before it starts. The install is
# silent and goes into RUNNER_TEMP\UnityEditor, or into a folder under the
# system temp folder when RUNNER_TEMP is not set. The installer file is
# deleted after the install.
#
# Dot-source this file to get its functions. Run it directly to install.
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'lib/Common.ps1')

# Picks the Windows installer download from one release API response and
# reads its md5 from the integrity value. Pure helper, so the tests can run
# it on fixture objects with no network. The host must be the vendor download
# host, so a moved url can never send the installer bytes.
function Select-UnityDownload($releaseResponse) {
    $results = @($releaseResponse.results)
    if ($results.Count -lt 1) { throw 'The release API returned no release.' }
    $downloads = @($results[0].downloads | Where-Object { $_.platform -eq 'WINDOWS' -and $_.type -eq 'EXE' })
    if ($downloads.Count -lt 1) { throw 'The release API returned no Windows installer download.' }
    $download = $downloads[0]
    if (-not ([string]$download.url).StartsWith('https://download.unity3d.com/')) {
        throw (Redact "Unexpected installer host: $($download.url)")
    }
    if (([string]$download.integrity) -notmatch '^md5-(.+)$') {
        throw 'The release API gave no md5 integrity value.'
    }
    $md5 = [Text.Encoding]::ASCII.GetString([Convert]::FromBase64String($Matches[1])).ToLowerInvariant()
    return @{ Url = [string]$download.url; Md5 = $md5 }
}

# Asks the vendor release API for the installer of one version.
function Get-UnityDownload([string]$version) {
    $release = Invoke-RestMethod "https://services.api.unity.com/unity/editor/release/v1/releases?version=$version&limit=1"
    return Select-UnityDownload $release
}

# Checks one downloaded installer before it starts. The order is the locked
# SHA-256, the md5 from the release API, a valid Authenticode signature and
# then the exact signer subject of the lock. A mismatch names the failed
# check and prints the observed SHA-256 and subject. Both are public values,
# so the maintainer can start a lock rotation with them.
function Assert-UnityInstaller([string]$path, $lock, [string]$expectedMd5) {
    if (-not (Test-Path -LiteralPath $path)) { throw 'The installer file is missing.' }
    $expectedSha256 = ([string]$lock.unity.installerSha256).ToLowerInvariant()
    $observedSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    if ($observedSha256 -ne $expectedSha256) {
        throw "Installer SHA-256 does not match the lock. observed sha256 $observedSha256, expected sha256 $expectedSha256"
    }
    $observedMd5 = (Get-FileHash -Algorithm MD5 -LiteralPath $path).Hash.ToLowerInvariant()
    if ($observedMd5 -ne ([string]$expectedMd5).ToLowerInvariant()) {
        throw "Installer md5 does not match the release API. observed md5 $observedMd5, observed sha256 $observedSha256"
    }
    $signature = Get-AuthenticodeSignature -FilePath $path
    $status = 'None'
    $observedSubject = ''
    if ($signature) {
        $status = [string]$signature.Status
        if ($signature.SignerCertificate) { $observedSubject = [string]$signature.SignerCertificate.Subject }
    }
    if ($status -ne 'Valid') {
        throw "Installer signature is not valid. status $status, observed sha256 $observedSha256, signer subject: $observedSubject"
    }
    if ($observedSubject -cne ([string]$lock.unity.signerSubject)) {
        throw "Installer signer subject does not match the lock. observed sha256 $observedSha256, signer subject: $observedSubject"
    }
}

# Runs one installer file silently into the target folder. Split from
# Install-Unity so the tests can watch that a failed check never reaches it.
function Invoke-UnityInstaller([string]$installerPath, [string]$targetDir) {
    Start-Process -FilePath $installerPath -ArgumentList '/S', "/D=$targetDir" -Wait
}

# Downloads the installer, checks it, installs it, deletes the installer file
# and checks that the Editor binary exists. Every check throws before the
# installer starts.
function Install-Unity($lock, [string]$targetDir, [string]$workDir) {
    $clock = [System.Diagnostics.Stopwatch]::StartNew()
    $version = [string]$lock.unity.version
    $download = Get-UnityDownload $version
    $installer = Join-Path $workDir 'UnitySetup64.exe'
    Invoke-WebRequest -Uri $download.Url -OutFile $installer
    Assert-UnityInstaller -Path $installer -Lock $lock -ExpectedMd5 $download.Md5
    Invoke-UnityInstaller -InstallerPath $installer -TargetDir $targetDir
    Remove-Item -LiteralPath $installer -Force
    $unityExe = Join-Path $targetDir 'Editor\Unity.exe'
    if (-not (Test-Path -LiteralPath $unityExe)) { throw 'Unity.exe is missing after install.' }
    Say "install-unity finished for $version."
    Say "- elapsed: $([int]$clock.Elapsed.TotalMinutes) min"
    $drive = Get-PSDrive -Name ($targetDir.Substring(0, 1))
    Say "- free disk: $([int]($drive.Free / 1GB)) GB"
}

if ($MyInvocation.InvocationName -ne '.') {
    $lockPath = Join-Path $PSScriptRoot 'lock.json'
    $lock = Get-Content -Path $lockPath -Raw | ConvertFrom-Json
    $work = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { [IO.Path]::GetTempPath() }
    $targetDir = Join-Path $work 'UnityEditor'
    Install-Unity -Lock $lock -TargetDir $targetDir -WorkDir $work
}
