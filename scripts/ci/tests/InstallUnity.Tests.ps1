# Tests for scripts/ci/install-unity.ps1. Run them with Pester 5.7.1:
# pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.7.1; Invoke-Pester scripts/ci/tests/InstallUnity.Tests.ps1 -Output Detailed"
Set-StrictMode -Version Latest

Describe 'Unity install' {
    BeforeAll {
        Set-StrictMode -Version Latest
        . (Join-Path $PSScriptRoot '../install-unity.ps1')
        # The tests below must fail when a function is missing. They must not
        # pass because a throw test silently swallowed the error.
        foreach ($name in 'Select-UnityDownload', 'Get-UnityDownload', 'Assert-UnityInstaller',
            'Invoke-UnityInstaller', 'Install-Unity') {
            Get-Command $name -ErrorAction Stop | Out-Null
        }

        $tempDir = Join-Path ([IO.Path]::GetTempPath()) "amuse-install-unity-tests-$PID"
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

        # A real small file with real computed hashes stands in for the installer.
        $installerPath = Join-Path $tempDir 'UnitySetup64.exe'
        Set-Content -LiteralPath $installerPath -Value 'installer bytes for the gate tests'
        $fileSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $installerPath).Hash.ToLowerInvariant()
        $fileMd5 = (Get-FileHash -Algorithm MD5 -LiteralPath $installerPath).Hash.ToLowerInvariant()

        $lockFixture = @{
            unity = @{
                version = '2022.3.22f1'
                installerSha256 = $fileSha256
                signerSubject = 'CN=Unity Technologies SF, O=Unity Technologies SF'
            }
        }

        # The integrity value of the release API is md5 followed by base64.
        $fixtureMd5 = '0123456789abcdef'
        $fixtureMd5Base64 = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes($fixtureMd5))
        $fixtureIntegrity = "md5-$fixtureMd5Base64"

        function New-ReleaseResponse([string]$url, [string]$integrity) {
            return @{
                results = @(@{
                    downloads = @(@{ platform = 'WINDOWS'; type = 'EXE'; url = $url; integrity = $integrity })
                })
            }
        }

        function New-Signature([string]$status, [string]$subject) {
            return [pscustomobject]@{
                Status = $status
                SignerCertificate = [pscustomobject]@{ Subject = $subject }
            }
        }

        # Default signature mock: valid and with the exact fixture subject.
        # Single tests override it with their own mock when they need another
        # status or another subject.
        Mock Get-AuthenticodeSignature {
            New-Signature 'Valid' 'CN=Unity Technologies SF, O=Unity Technologies SF'
        }
    }

    AfterAll {
        if (Get-Variable -Name tempDir -ErrorAction SilentlyContinue) {
            Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'WrongSha256Throws' {
        $badLock = @{ unity = @{
            installerSha256 = ('f' * 64)
            signerSubject = $lockFixture.unity.signerSubject
        } }
        { Assert-UnityInstaller -Path $installerPath -Lock $badLock -ExpectedMd5 $fileMd5 } |
            Should -Throw '*SHA-256*'
    }

    It 'WrongMd5Throws' {
        { Assert-UnityInstaller -Path $installerPath -Lock $lockFixture -ExpectedMd5 'deadbeef' } |
            Should -Throw '*md5*'
    }

    It 'SubstringOfSubjectIsNotEnough' {
        # The observed subject starts with the locked subject. Only exact
        # equality is enough, so this must throw.
        $shortLock = @{ unity = @{
            installerSha256 = $fileSha256
            signerSubject = 'CN=Unity Technologies SF'
        } }
        Mock Get-AuthenticodeSignature {
            New-Signature 'Valid' 'CN=Unity Technologies SF, O=Unity Technologies SF'
        }
        { Assert-UnityInstaller -Path $installerPath -Lock $shortLock -ExpectedMd5 $fileMd5 } |
            Should -Throw '*signer subject*'
    }

    It 'InvalidSignatureThrows' {
        Mock Get-AuthenticodeSignature {
            New-Signature 'HashMismatch' 'CN=Unity Technologies SF, O=Unity Technologies SF'
        }
        { Assert-UnityInstaller -Path $installerPath -Lock $lockFixture -ExpectedMd5 $fileMd5 } |
            Should -Throw '*not valid*'
    }

    It 'ForeignHostUrlThrows' {
        { Select-UnityDownload (New-ReleaseResponse 'https://malicious.example.net/UnitySetup64.exe' $fixtureIntegrity) } |
            Should -Throw '*host*'
    }

    It 'SelectsWindowsExeDownloadAndParsesMd5' {
        $download = Select-UnityDownload (New-ReleaseResponse 'https://download.unity3d.com/UnitySetup64.exe' $fixtureIntegrity)
        $download.Url | Should -Be 'https://download.unity3d.com/UnitySetup64.exe'
        $download.Md5 | Should -Be $fixtureMd5
    }

    It 'FailedCheckNeverStartsTheInstaller' {
        Mock Get-UnityDownload {
            return @{ Url = 'https://download.unity3d.com/UnitySetup64.exe'; Md5 = $fileMd5 }
        }
        Mock Invoke-WebRequest {
            param($Uri, $OutFile)
            Set-Content -LiteralPath $OutFile -Value 'downloaded installer bytes'
        }
        Mock Assert-UnityInstaller { throw 'Installer SHA-256 does not match the lock.' }
        Mock Invoke-UnityInstaller { }
        { Install-Unity -Lock $lockFixture -TargetDir $tempDir -WorkDir $tempDir } |
            Should -Throw '*SHA-256*'
        Should -Invoke Invoke-UnityInstaller -Exactly -Times 0 -Scope It
    }

    It 'InstallsSilentlyDeletesTheInstallerAndChecksUnityExe' {
        Mock Get-UnityDownload {
            return @{ Url = 'https://download.unity3d.com/UnitySetup64.exe'; Md5 = $fileMd5 }
        }
        Mock Invoke-WebRequest {
            param($Uri, $OutFile)
            Set-Content -LiteralPath $OutFile -Value 'downloaded installer bytes'
        }
        Mock Assert-UnityInstaller { }
        # The real Invoke-UnityInstaller runs, so the process arguments reach
        # the Start-Process seam. The mock creates the Editor binary that
        # Install-Unity checks afterwards.
        Mock Start-Process {
            param($FilePath, $ArgumentList, $Wait)
            $target = [string](@($ArgumentList | Where-Object { $_ -like '/D=*' })[0]).Substring(3)
            New-Item -ItemType Directory -Force -Path (Join-Path $target 'Editor') | Out-Null
            Set-Content -LiteralPath (Join-Path $target 'Editor\Unity.exe') -Value 'unity exe'
        }
        $targetDir = Join-Path $tempDir 'target'
        Install-Unity -Lock $lockFixture -TargetDir $targetDir -WorkDir $tempDir
        Test-Path -LiteralPath (Join-Path $targetDir 'Editor\Unity.exe') | Should -BeTrue
        Test-Path -LiteralPath $installerPath | Should -BeFalse
        # The silent switch is part of the contract. A dropped /S fails here.
        Should -Invoke Start-Process -Exactly -Times 1 -Scope It -ParameterFilter {
            @($ArgumentList).Count -eq 2 -and @($ArgumentList)[0] -ceq '/S' -and @($ArgumentList)[1] -like '/D=*'
        }
    }
}
