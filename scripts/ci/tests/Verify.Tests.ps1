# Tests for scripts/verify.ps1, the local gate. Run them with Pester 5.7.1:
# pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.7.1; Invoke-Pester scripts/ci/tests/Verify.Tests.ps1 -Output Detailed"
Set-StrictMode -Version Latest

Describe 'Local gate Unity lookup' {
    BeforeAll {
        Set-StrictMode -Version Latest
        . (Join-Path $PSScriptRoot '../../verify.ps1')
        # The tests below must fail when the function is missing. They must
        # not pass because a throw test silently swallowed the error.
        Get-Command Find-LocalUnity -ErrorAction Stop | Out-Null

        $tempDir = Join-Path ([IO.Path]::GetTempPath()) "amuse-verify-tests-$PID"
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

        # Builds one fake Editor install under a search root and returns the
        # path of the empty Unity.exe stand-in.
        function New-FakeEditor([string]$searchRoot, [string]$version) {
            $folder = Join-Path (Join-Path $searchRoot $version) 'Editor'
            New-Item -ItemType Directory -Path $folder -Force | Out-Null
            $exe = Join-Path $folder 'Unity.exe'
            Set-Content -LiteralPath $exe -Value ''
            return $exe
        }
    }

    AfterAll {
        if (Get-Variable -Name tempDir -ErrorAction SilentlyContinue) {
            Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    It 'FindLocalUnityPrefersTheExplicitPath' {
        $searchRoot = Join-Path $tempDir 'hub-root'
        $hubExe = New-FakeEditor $searchRoot '2022.3.22f1'
        $explicit = Join-Path $tempDir 'explicit/Unity.exe'
        New-Item -ItemType Directory -Path (Split-Path $explicit -Parent) -Force | Out-Null
        Set-Content -LiteralPath $explicit -Value ''

        $found = Find-LocalUnity '2022.3.22f1' $explicit @($searchRoot)
        $found | Should -Be $explicit
        $found | Should -Not -Be $hubExe
    }

    It 'FindLocalUnityFindsTheVersionFolderUnderASearchRoot' {
        $searchRoot = Join-Path $tempDir 'other-hub-root'
        $expected = New-FakeEditor $searchRoot '2022.3.22f1'

        $found = Find-LocalUnity '2022.3.22f1' '' @($searchRoot)
        $found | Should -Be $expected
    }

    It 'FindLocalUnityThrowsAndNamesTheVersionWhenNothingIsFound' {
        $thrown = $null
        try {
            Find-LocalUnity '2099.1.1f1' '' @() | Out-Null
        }
        catch {
            $thrown = $_
        }
        $thrown | Should -Not -BeNullOrEmpty
        $thrown.Exception.Message | Should -Match '2099\.1\.1f1'
        $thrown.Exception.Message | Should -Match 'UnityPath'
    }

    It 'FindLocalUnityIgnoresAMissingExplicitPathAndFallsBack' {
        $searchRoot = Join-Path $tempDir 'fallback-hub-root'
        $expected = New-FakeEditor $searchRoot '2022.3.22f1'
        $missing = Join-Path $tempDir 'missing-editor/Unity.exe'

        $found = Find-LocalUnity '2022.3.22f1' $missing @($searchRoot)
        $found | Should -Be $expected
    }
}
