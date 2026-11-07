# Tests for scripts/ci/check-lock.ps1. Run them with Pester 5.7.1:
# pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.7.1; Invoke-Pester scripts/ci/tests/Lock.Tests.ps1 -Output Detailed"
Set-StrictMode -Version Latest

Describe 'Lock check' {
    BeforeAll {
        Set-StrictMode -Version Latest
        . (Join-Path $PSScriptRoot '../check-lock.ps1')
        # The tests below must fail when the function is missing. They must not
        # pass because a throw test silently swallowed the error.
        Get-Command Test-LockMatchesManifest -ErrorAction Stop | Out-Null

        $tempDir = Join-Path ([IO.Path]::GetTempPath()) "amuse-lock-tests-$PID"
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

        function Write-TempJson([string]$name, [object]$value) {
            $path = Join-Path $tempDir $name
            Set-Content -LiteralPath $path -Value (ConvertTo-Json -InputObject $value -Depth 6)
            return $path
        }

        function Write-TempVersionFile([string]$name, [string]$version) {
            $path = Join-Path $tempDir $name
            Set-Content -LiteralPath $path -Value "m_EditorVersion: $version"
            return $path
        }

        function Copy-Lock([object]$value) {
            return (ConvertFrom-Json (ConvertTo-Json -InputObject $value -Depth 6 -Compress))
        }

        # One lock entry for a plain package and the one allowed unhashed package.
        $hashOne = 'aa11bb22cc33dd44ee55ff6600112233445566778899aabbccddeeff00112233'
        $baseLock = [ordered]@{
            unity = [ordered]@{ version = '2022.3.22f1'; installerSha256 = 'e42d3637666483009c16fee551ca083ba17f4f1b36055c4ca62c552f124dfb01'; signerSubject = 'CN=Unity Technologies SF' }
            licensingClientSubject = 'CN=Unity Technologies SF'
            packages = [ordered]@{
                'test.pkg.one' = [ordered]@{ version = '1.0.0'; sha256 = $hashOne }
                'com.vrcfury.vrcfury' = [ordered]@{ version = '1.0.0'; sha256 = $null; unhashedReason = 'no hash route' }
            }
        }
        $baseManifest = [ordered]@{
            dependencies = [ordered]@{ 'test.pkg.one' = [ordered]@{ version = '1.0.0' } }
            locked = [ordered]@{
                'test.pkg.one' = [ordered]@{ version = '1.0.0'; dependencies = [ordered]@{} }
                'com.vrcfury.vrcfury' = [ordered]@{ version = '1.0.0'; dependencies = [ordered]@{} }
            }
        }
        $baseSources = [ordered]@{
            sources = [ordered]@{ test = 'https://example.test/listing/index.json' }
            packages = [ordered]@{ 'test.pkg.one' = 'test'; 'com.vrcfury.vrcfury' = 'test' }
        }

        function Invoke-Check($lock, $manifest, $sources, [string]$unityVersion = '2022.3.22f1') {
            $lockPath = Write-TempJson 'lock.json' $lock
            $manifestPath = Write-TempJson 'vpm-manifest.json' $manifest
            $sourcesPath = Write-TempJson 'sources.json' $sources
            $versionPath = Write-TempVersionFile 'ProjectVersion.txt' $unityVersion
            return @(Test-LockMatchesManifest -LockPath $lockPath -ManifestPath $manifestPath -VersionFilePath $versionPath -SourcesPath $sourcesPath)
        }
    }

    AfterAll {
        if (Get-Variable -Name tempDir -ErrorAction SilentlyContinue) {
            Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Context 'Matching lock' {
        It 'LockMatchingManifestHasNoDifferences' {
            $differences = @(Invoke-Check $baseLock $baseManifest $baseSources)
            $differences | Should -BeNullOrEmpty
        }
    }

    Context 'Version drift' {
        It 'ManifestVersionBumpWithoutLockEditIsReported' {
            $manifest = Copy-Lock $baseManifest
            $manifest.locked.'test.pkg.one'.version = '2.0.0'
            $differences = @(Invoke-Check $baseLock $manifest $baseSources)
            $differences | Should -HaveCount 1
            $differences[0] | Should -Match 'test\.pkg\.one'
            $differences[0] | Should -Match '2\.0\.0'
        }
    }

    Context 'Name drift' {
        It 'PackageMissingFromLockIsReported' {
            $manifest = Copy-Lock $baseManifest
            $manifest.locked | Add-Member -MemberType NoteProperty -Name 'test.pkg.two' -Value ([pscustomobject]@{ version = '3.0.0'; dependencies = [pscustomobject]@{} })
            $differences = @(Invoke-Check $baseLock $manifest $baseSources)
            $differences | Should -HaveCount 1
            $differences[0] | Should -Match 'test\.pkg\.two'
        }

        It 'PackageMissingFromManifestIsReported' {
            $lock = Copy-Lock $baseLock
            $lock.packages | Add-Member -MemberType NoteProperty -Name 'test.pkg.three' -Value ([pscustomobject]@{ version = '4.0.0'; sha256 = $hashOne })
            $differences = @(Invoke-Check $lock $baseManifest $baseSources)
            $differences | Should -HaveCount 1
            $differences[0] | Should -Match 'test\.pkg\.three'
        }
    }

    Context 'Source mapping' {
        It 'PackageWithoutSourceKeyIsReported' {
            $sources = Copy-Lock $baseSources
            $sources.packages.PSObject.Properties.Remove('test.pkg.one')
            $differences = @(Invoke-Check $baseLock $baseManifest $sources)
            $differences | Should -HaveCount 1
            $differences[0] | Should -Match 'test\.pkg\.one'
        }
    }

    Context 'Null hash rule' {
        It 'NullHashWithoutReasonIsReported' {
            $lock = Copy-Lock $baseLock
            $lock.packages.'com.vrcfury.vrcfury'.PSObject.Properties.Remove('unhashedReason')
            $differences = @(Invoke-Check $lock $baseManifest $baseSources)
            $differences | Should -HaveCount 1
            $differences[0] | Should -Match 'com\.vrcfury\.vrcfury'
        }

        It 'NullHashOnAnotherPackageThanVrcfuryIsReported' {
            $lock = Copy-Lock $baseLock
            $entry = $lock.packages.'test.pkg.one'
            $entry.sha256 = $null
            $entry | Add-Member -MemberType NoteProperty -Name 'unhashedReason' -Value 'no hash route'
            $differences = @(Invoke-Check $lock $baseManifest $baseSources)
            $differences | Should -HaveCount 1
            $differences[0] | Should -Match 'test\.pkg\.one'
        }
    }

    Context 'Unity version' {
        It 'UnityVersionMismatchIsReported' {
            $differences = @(Invoke-Check $baseLock $baseManifest $baseSources -UnityVersion '2022.3.30f1')
            $differences | Should -HaveCount 1
            $differences[0] | Should -Match '2022\.3\.30f1'
        }
    }
}
