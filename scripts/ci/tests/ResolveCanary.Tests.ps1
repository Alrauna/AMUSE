# Tests for scripts/ci/resolve-canary.ps1. Run them with Pester 5.7.1:
# pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.7.1; Invoke-Pester scripts/ci/tests/ResolveCanary.Tests.ps1 -Output Detailed"
# The tests use fixtures only. No test touches the network.
Set-StrictMode -Version Latest

Describe 'Canary resolver' {
    BeforeAll {
        Set-StrictMode -Version Latest
        . (Join-Path $PSScriptRoot '../resolve-canary.ps1')
        # The tests below must fail when a function is missing. They must not
        # pass because a throw test silently swallowed the error.
        foreach ($name in 'Get-StableVersions', 'Compare-StableVersion', 'Select-CanaryVersion',
            'Get-ReleaseDate', 'Assert-PlanDependencies', 'Update-LockedVersions',
            'Update-LockedManifest') {
            Get-Command $name -ErrorAction Stop | Out-Null
        }

        $tempDir = Join-Path ([IO.Path]::GetTempPath()) "amuse-resolve-canary-tests-$PID"
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
        $now = [datetimeoffset]'2026-11-07T12:00:00+00:00'

        # Two listings that hold the same three versions in a different key
        # order. A choice that depends on the listing order would pick a
        # different version here.
        $listingHighestFirst = (@'
{
  "versions": {
    "3.0.0": { "url": "https://vendor.example/3.0.0.zip" },
    "1.0.0": { "url": "https://vendor.example/1.0.0.zip" },
    "2.0.0": { "url": "https://vendor.example/2.0.0.zip" }
  }
}
'@ | ConvertFrom-Json)
        $listingLowestFirst = (@'
{
  "versions": {
    "1.0.0": { "url": "https://vendor.example/1.0.0.zip" },
    "2.0.0": { "url": "https://vendor.example/2.0.0.zip" },
    "3.0.0": { "url": "https://vendor.example/3.0.0.zip" }
  }
}
'@ | ConvertFrom-Json)

        # A manifest with the real shape of a checkout: a dependencies
        # section, a locked block, one entry with a dependencies field, and
        # a locked entry that stays outside every rewrite.
        $manifestFixture = @'
{
  "dependencies": {
    "com.example.alpha": {
      "version": "1.2.0"
    }
  },
  "locked": {
    "com.example.alpha": {
      "version": "1.2.0",
      "dependencies": {}
    },
    "com.example.beta": {
      "version": "2.0.0",
      "dependencies": {
        "com.example.alpha": ">=1.2.0 <2.0.0-a"
      }
    }
  }
}
'@
        # The same text with only the version of com.example.alpha changed.
        $manifestExpected = @'
{
  "dependencies": {
    "com.example.alpha": {
      "version": "1.2.0"
    }
  },
  "locked": {
    "com.example.alpha": {
      "version": "9.9.9",
      "dependencies": {}
    },
    "com.example.beta": {
      "version": "2.0.0",
      "dependencies": {
        "com.example.alpha": ">=1.2.0 <2.0.0-a"
      }
    }
  }
}
'@
    }

    It 'PicksHighestStableVersion' {
        $result = Select-CanaryVersion @('1.2.3', '2.0.0', '1.9.9') $now
        $result.Version | Should -Be '2.0.0'
        @($result.Skipped).Count | Should -Be 0
        $result.DateSource | Should -Be 'none'
    }

    It 'TenDotZeroDotTwentyNineBeatsTwoDotFourDotOne' {
        $result = Select-CanaryVersion @('2.4.1', '10.0.29') $now
        $result.Version | Should -Be '10.0.29'
    }

    It 'OneDot1442BeatsOneDot999' {
        $result = Select-CanaryVersion @('1.999.0', '1.1442.0') $now
        $result.Version | Should -Be '1.1442.0'
    }

    It 'PrereleaseIsIgnored' {
        $result = Select-CanaryVersion @('10.0.29', '10.0.30-beta') $now
        $result.Version | Should -Be '10.0.29'
    }

    It 'VersionYoungerThan48HoursIsSkippedAndNamed' {
        # Review focus 6: a release that is younger than the cooldown is
        # skipped and named, and the next lower stable version wins. A
        # release of exactly 48 hours is not younger, so it stays.
        $dates = @{
            '10.0.29' = $now.AddHours(-1)
            '10.0.28' = $now.AddHours(-47)
            '10.0.27' = $now.AddHours(-48)
        }
        $result = Select-CanaryVersion @('10.0.29', '10.0.28', '10.0.27') $now 48 $dates
        $result.Version | Should -Be '10.0.27'
        @($result.Skipped) | Should -Be @('10.0.29', '10.0.28')
        $result.DateSource | Should -Be 'known'
    }

    It 'VersionWithoutReleaseDateIsKeptAndFlagged' {
        # A missing key means an unknown date. The cooldown cannot apply,
        # so the version is kept and the summary must say so.
        $dates = @{ '1.0.0' = $now.AddDays(-30) }
        $result = Select-CanaryVersion @('2.0.0', '1.0.0') $now 48 $dates
        $result.Version | Should -Be '2.0.0'
        @($result.Skipped).Count | Should -Be 0
        $result.DateSource | Should -Be 'none'
    }

    It 'ListingKeyOrderDoesNotChangeTheChoice' {
        $first = Select-CanaryVersion @($listingHighestFirst.versions.PSObject.Properties.Name) $now
        $second = Select-CanaryVersion @($listingLowestFirst.versions.PSObject.Properties.Name) $now
        $first.Version | Should -Be '3.0.0'
        $second.Version | Should -Be '3.0.0'
    }

    It 'NewTransitiveDependencyOutsideThePlanFailsClosed' {
        $planNames = @('com.example.alpha', 'com.example.beta')
        $unknownDependency = (@'
{ "vpmDependencies": { "com.unknown.thing": ">=1.0.0" } }
'@ | ConvertFrom-Json)
        $knownDependency = (@'
{ "vpmDependencies": { "com.example.beta": ">=1.0.0" } }
'@ | ConvertFrom-Json)
        $noDependencies = '{}' | ConvertFrom-Json

        { Assert-PlanDependencies @(@{ Name = 'com.example.alpha'; ListingEntry = $unknownDependency }) $planNames } |
            Should -Throw '*com.example.alpha*com.unknown.thing*'

        # A dependency inside the plan and a missing dependencies field are
        # both fine.
        Assert-PlanDependencies @(
            @{ Name = 'com.example.alpha'; ListingEntry = $knownDependency }
            @{ Name = 'com.example.beta'; ListingEntry = $noDependencies }
        ) $planNames
    }

    It 'LockedBlockRewriteMatchesInstalledVersions' {
        $path = Join-Path $tempDir 'vpm-manifest.json'
        Set-Content -LiteralPath $path -Value $manifestFixture -NoNewline -Encoding utf8

        Update-LockedManifest -Path $path -Versions @{ 'com.example.alpha' = '9.9.9' }

        # The exact match pins every byte: the dependencies section, the key
        # order, and the untouched locked entry of com.example.beta.
        $after = Get-Content -LiteralPath $path -Raw
        $after | Should -BeExactly $manifestExpected

        # A name outside the locked block stops the rewrite. The file keeps
        # its content.
        { Update-LockedManifest -Path $path -Versions @{ 'com.example.missing' = '1.0.0' } } |
            Should -Throw '*not in the locked block*'
        Get-Content -LiteralPath $path -Raw | Should -BeExactly $manifestExpected
    }
}
