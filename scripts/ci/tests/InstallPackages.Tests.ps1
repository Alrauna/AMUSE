# Tests for scripts/ci/install-packages.ps1. Run them with Pester 5.7.1:
# pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.7.1; Invoke-Pester scripts/ci/tests/InstallPackages.Tests.ps1 -Output Detailed"
#
# The tests dot-source the script and test its functions with fixtures in a
# temp folder. They never touch the Packages folder of the repository and
# they never use the network.
Set-StrictMode -Version Latest

Describe 'Install packages' {
    BeforeAll {
        Set-StrictMode -Version Latest
        . (Join-Path $PSScriptRoot '../install-packages.ps1')
        # The tests below must fail when a function is missing. They must not
        # pass because a throw test silently swallowed the error.
        Get-Command Resolve-ListingEntry -ErrorAction Stop | Out-Null
        Get-Command Get-ListingEntryOrThrow -ErrorAction Stop | Out-Null
        Get-Command Get-PublisherHash -ErrorAction Stop | Out-Null
        Get-Command Get-GatePackages -ErrorAction Stop | Out-Null
        Get-Command Assert-ZipHash -ErrorAction Stop | Out-Null
        Get-Command Get-VerifiedCell -ErrorAction Stop | Out-Null
        Get-Command Find-MissingDependencies -ErrorAction Stop | Out-Null
        Get-Command Get-DependencyFailures -ErrorAction Stop | Out-Null
        Get-Command New-PackageRow -ErrorAction Stop | Out-Null
        Get-Command Edit-UnityManifest -ErrorAction Stop | Out-Null

        $tempDir = Join-Path ([IO.Path]::GetTempPath()) "amuse-install-packages-tests-$PID"
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

        # A small zip and its real SHA-256. The hash tests use it.
        $zipSource = Join-Path $tempDir 'zip-source'
        New-Item -ItemType Directory -Path $zipSource -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $zipSource 'entry.txt') -Value 'fixture'
        $zipPath = Join-Path $tempDir 'fixture.zip'
        Compress-Archive -Path (Join-Path $zipSource '*') -DestinationPath $zipPath -Force
        $zipHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $zipPath).Hash.ToLowerInvariant()

        # A listing in the shape that the vendor listings use.
        $listingHash = 'AA11BB22CC33DD44EE55FF6600112233445566778899AABBCCDDEEFF00112233'
        $listingJson = @'
{
  "packages": {
    "alpha.dev.pack": {
      "versions": {
        "1.0.0": {
          "url": "https://listing.example.invalid/alpha-1.0.0.zip",
          "zipSHA256": "AA11BB22CC33DD44EE55FF6600112233445566778899AABBCCDDEEFF00112233"
        },
        "2.0.0": {
          "url": "https://listing.example.invalid/alpha-2.0.0.zip"
        }
      }
    }
  }
}
'@
        $listing = $listingJson | ConvertFrom-Json

        # Installed packages with one dependency inside and one outside the
        # allowed set.
        $packagesDir = Join-Path $tempDir 'installed-packages'
        New-Item -ItemType Directory -Path (Join-Path $packagesDir 'alpha') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $packagesDir 'beta') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $packagesDir 'alpha/package.json') -Value @'
{
  "name": "alpha",
  "vpmDependencies": {
    "com.allowed.thing": "1.0.0",
    "com.outside.dep": "2.0.0"
  }
}
'@
        Set-Content -LiteralPath (Join-Path $packagesDir 'beta/package.json') -Value @'
{
  "name": "beta"
}
'@
    }

    AfterAll {
        if (Get-Variable -Name tempDir -ErrorAction SilentlyContinue) {
            Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Context 'Zip hash assertion' {
        It 'HashMismatchThrowsAndNamesThePackage' {
            { Assert-ZipHash -Path $zipPath -Expected ('f' * 64) -Name 'alpha.dev.pack' } |
                Should -Throw '*alpha.dev.pack*'
        }

        It 'ZipHashMatchingExpectedReturnsObservedHash' {
            Assert-ZipHash -Path $zipPath -Expected $zipHash -Name 'alpha.dev.pack' |
                Should -Be $zipHash
        }

        It 'HashMismatchMessageKeepsBothHashesAndSplitsTheName' {
            # The failure line of the install loop redacts everything except
            # a plain SHA-256 shape. A mismatch must then keep both hashes
            # readable for the maintainer, and a name with a double colon
            # must still lose it.
            $notTheZipHash = 'a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2'
            $caught = $null
            try { Assert-ZipHash -Path $zipPath -Expected $notTheZipHash -Name 'com::evil.pkg' }
            catch { $caught = $_ }
            $caught | Should -Not -BeNullOrEmpty
            $line = Redact-KeepingHash "com::evil.pkg 1.0.0 failed: $($caught.Exception.Message)"
            $line | Should -Match $notTheZipHash
            $line | Should -Match ([regex]::Escape($zipHash))
            $line | Should -Not -Match '::'
            $line | Should -BeLike '*com: :evil.pkg*'
        }

        It 'RedactKeepingHashStillRedactsOtherLongHex' {
            # Only a run of exactly 64 lowercase hex digits passes through.
            # A longer run stays redacted.
            $line = "com.pkg 1.0.0 failed: token $('z9' * 40)"
            $out = Redact-KeepingHash $line
            $out | Should -Match '<token>'
            $out | Should -Not -Match 'z9z9'
        }
    }

    Context 'Listing resolution' {
        It 'PackageNotInItsOwnNamedListingStops' {
            Resolve-ListingEntry -Listing $listing -Name 'missing.pkg' -Version '1.0.0' |
                Should -BeNullOrEmpty
            { Get-ListingEntryOrThrow -Listing $listing -Name 'missing.pkg' -Version '1.0.0' } |
                Should -Throw '*missing.pkg*'
        }

        It 'ListingEntryThrowSplitsDoubleColonsInTheName' {
            # The thrown message can hold vendor text, so it passes through
            # Redact. A name that holds a double colon can then never act as
            # a workflow command, and the dotted name stays readable.
            { Get-ListingEntryOrThrow -Listing $listing -Name 'com::evil.pkg' -Version '1.0.0' } |
                Should -Throw '*com: :evil.pkg*'
        }

        It 'ResolveListingEntryFindsVersionEntry' {
            $entry = Resolve-ListingEntry -Listing $listing -Name 'alpha.dev.pack' -Version '1.0.0'
            $entry.url | Should -Be 'https://listing.example.invalid/alpha-1.0.0.zip'
            $found = Resolve-ListingEntry -Listing $listing -Name 'alpha.dev.pack' -Version '2.0.0'
            @($found.PSObject.Properties.Name) -contains 'zipSHA256' | Should -BeFalse
        }
    }

    Context 'Publisher hash routes' {
        It 'PublisherHashPrefersListingOverGithubDigest' {
            # The entry holds both routes. The listing route must win without
            # any network call to the release API.
            $entry = [pscustomobject]@{
                url = 'https://github.com/one/two/releases/download/v1/pack.zip'
                zipSHA256 = $listingHash
            }
            $route = Get-PublisherHash -Entry $entry
            $route.Hash | Should -Be $listingHash.ToLowerInvariant()
            $route.Route | Should -Be 'listing zipSHA256'
        }

        It 'PublisherHashReturnsNoneWithoutNetworkRoute' {
            $entry = [pscustomobject]@{ url = 'https://listing.example.invalid/pack.zip' }
            $route = Get-PublisherHash -Entry $entry
            $route.Hash | Should -BeNullOrEmpty
            $route.Route | Should -Be 'none'
        }
    }

    Context 'Download with release API fallback' {
        BeforeAll {
            function New-HttpFault([int]$code) {
                return [Microsoft.PowerShell.Commands.HttpResponseException]::new("raw $code", [System.Net.Http.HttpResponseMessage]::new([System.Net.HttpStatusCode]$code))
            }
            $script:ghUrl = 'https://github.com/one/two/releases/download/v1/pack.zip?'
            $script:assetApiUrl = 'https://api.github.com/repos/one/two/releases/assets/7'
        }

        BeforeEach {
            Mock Start-Sleep { }
            Mock Say { }
            Mock Invoke-RestMethod { [pscustomobject]@{ assets = @([pscustomobject]@{ name = 'pack.zip'; url = $script:assetApiUrl }) } }
        }

        It 'ADirectDownloadThatWorksNeverCallsTheApi' {
            Mock Invoke-WebRequest { }
            Save-Download $script:ghUrl 'out.zip'
            Should -Invoke Invoke-WebRequest -Times 1
            Should -Invoke Invoke-RestMethod -Times 0
        }

        It 'ADirectLinkThatKeepsAnswering504TakesTheAssetFromTheApi' {
            Mock Invoke-WebRequest { throw (New-HttpFault 504) } -ParameterFilter { $Uri -eq $script:ghUrl }
            Mock Invoke-WebRequest { } -ParameterFilter { $Uri -eq $script:assetApiUrl }
            Save-Download $script:ghUrl 'out.zip'
            Should -Invoke Invoke-WebRequest -Times 2 -ParameterFilter { $Uri -eq $script:ghUrl }
            Should -Invoke Invoke-WebRequest -Times 1 -ParameterFilter { $Uri -eq $script:assetApiUrl -and $Headers['Accept'] -eq 'application/octet-stream' }
        }

        It 'AnHttp404OnTheDirectLinkStopsWithoutTheApi' {
            # Falsifier: a missing file is not a transient fault.
            Mock Invoke-WebRequest { throw (New-HttpFault 404) }
            { Save-Download $script:ghUrl 'out.zip' } | Should -Throw
            Should -Invoke Invoke-RestMethod -Times 0
        }

        It 'ALinkOutsideGithubThatRedirectsNowhereKeepsTheDirectError' {
            Mock Invoke-WebRequest { throw (New-HttpFault 504) }
            Mock Get-RedirectLocation { $null }
            { Save-Download 'https://listing.example.invalid/pack.zip' 'out.zip' } | Should -Throw
            Should -Invoke Invoke-RestMethod -Times 0
        }

        It 'ALinkThatRedirectsToAGithubReleaseUsesTheApi' {
            Mock Invoke-WebRequest { throw (New-HttpFault 504) } -ParameterFilter { $Uri -like 'https://listing.example.invalid/*' }
            Mock Invoke-WebRequest { } -ParameterFilter { $Uri -eq $script:assetApiUrl }
            Mock Get-RedirectLocation { 'https://github.com/one/two/releases/download/v1/pack.zip' }
            Save-Download 'https://listing.example.invalid/pack.zip' 'out.zip'
            Should -Invoke Invoke-WebRequest -Times 1 -ParameterFilter { $Uri -eq $script:assetApiUrl }
        }

        It 'ARedirectToAnotherHostDoesNotUseTheApi' {
            Mock Invoke-WebRequest { throw (New-HttpFault 504) }
            Mock Get-RedirectLocation { 'https://other.example.invalid/pack.zip' }
            { Save-Download 'https://listing.example.invalid/pack.zip' 'out.zip' } | Should -Throw
            Should -Invoke Invoke-RestMethod -Times 0
        }

        It 'AReleaseWithoutTheNamedAssetStops' {
            Mock Invoke-WebRequest { throw (New-HttpFault 504) }
            Mock Invoke-RestMethod { [pscustomobject]@{ assets = @([pscustomobject]@{ name = 'other.zip'; url = $script:assetApiUrl }) } }
            { Save-Download $script:ghUrl 'out.zip' } | Should -Throw '*no asset*'
        }
    }

    Context 'Lock package rules' {
        It 'UnhashedPackageWithoutReasonStops' {
            $lockJson = @'
{
  "packages": {
    "bad.pkg": { "version": "1.0.0", "sha256": null },
    "good.pkg": { "version": "2.0.0", "sha256": "aa11bb22cc33dd44ee55ff6600112233445566778899aabbccddeeff00112233" }
  }
}
'@
            $lock = $lockJson | ConvertFrom-Json
            { Get-GatePackages -Lock $lock } | Should -Throw '*bad.pkg*'
        }

        It 'UnhashedPackageWithReasonReturnsNullHash' {
            $lockJson = @'
{
  "packages": {
    "reason.pkg": { "version": "1.2.3", "sha256": null, "unhashedReason": "no hash route" }
  }
}
'@
            $lock = $lockJson | ConvertFrom-Json
            $packages = @(Get-GatePackages -Lock $lock)
            $packages.Count | Should -Be 1
            $packages[0].Name | Should -Be 'reason.pkg'
            $packages[0].Version | Should -Be '1.2.3'
            $packages[0].ExpectedHash | Should -BeNullOrEmpty
        }
    }

    Context 'Verified cell' {
        It 'NullHashPackagePrintsObservedHash' {
            $observed = 'c' * 64
            Get-VerifiedCell -Observed $observed -Expected '' |
                Should -Be "no hash available, observed $observed"
        }

        It 'VerifiedCellSaysYesWhenHashMatches' {
            Get-VerifiedCell -Observed $zipHash -Expected $zipHash | Should -Be 'yes'
        }
    }

    Context 'Dependency checks' {
        It 'GateModeRejectsDependencyOutsideTheLock' {
            $missing = @(Find-MissingDependencies -PackagesDir $packagesDir -AllowedNames @('com.allowed.thing'))
            $missing.Count | Should -Be 1
            $missing[0] | Should -Be 'alpha needs com.outside.dep'
        }

        It 'CanaryModeAllowsDependencyThatIsInThePlan' {
            $allowed = @('com.allowed.thing', 'com.outside.dep')
            $missing = @(Find-MissingDependencies -PackagesDir $packagesDir -AllowedNames $allowed)
            $missing.Count | Should -Be 0
        }

        It 'DependencyFailureLinesPassRedact' {
            # The dependency names come from package.json files inside vendor
            # zips. A name with a double colon must never reach the log raw.
            $vendorDir = Join-Path $tempDir 'vendor-packages'
            New-Item -ItemType Directory -Path (Join-Path $vendorDir 'vendor') -Force | Out-Null
            Set-Content -LiteralPath (Join-Path $vendorDir 'vendor/package.json') -Value @'
{
  "name": "vendor.pkg",
  "vpmDependencies": {
    "com.bad::thing": "1.0.0"
  }
}
'@
            $failures = @(Get-DependencyFailures -PackagesDir $vendorDir -AllowedNames @())
            $failures.Count | Should -Be 1
            $failures[0] | Should -Not -Match '::'
            $failures[0] | Should -Be 'vendor.pkg needs com.bad: :thing'
        }
    }

    Context 'Result table rows' {
        It 'PackageRowKeepsObservedHashAfterRedact' {
            # The observed hash of a null-hash package must stay readable in
            # the log, so Redact runs on the identifier columns only.
            $observed = 'c' * 64
            $row = New-PackageRow -Name 'some.pkg' -Version '1.0.0' -SourceKey 'src' `
                -Route 'lock' -Verified "no hash available, observed $observed"
            $row | Should -Match "observed $observed \|"
        }

        It 'PackageRowSplitsDoubleColonsInIdentifiers' {
            $row = New-PackageRow -Name 'bad::pkg' -Version '1.0.0' -SourceKey 'src' `
                -Route 'lock' -Verified 'yes'
            $row | Should -Not -Match '::'
            $row | Should -Match '^\| bad: :pkg \|'
        }
    }

    Context 'Unity manifest edit' {
        It 'ManifestEditRemovesMcpAndToolchainOnly' {
            $manifestPath = Join-Path $tempDir 'manifest.json'
            Set-Content -LiteralPath $manifestPath -Value @'
{
  "dependencies": {
    "com.unity.textmeshpro": "3.0.6",
    "com.coplaydev.unity-mcp": "1.0.0",
    "com.unity.toolchain.win-x64": "2.0.4"
  },
  "locked": {
    "com.unity.textmeshpro": { "version": "3.0.6", "hash": "abc" }
  }
}
'@
            Edit-UnityManifest -Path $manifestPath
            $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
            $names = @($manifest.dependencies.PSObject.Properties | ForEach-Object { $_.Name })
            $names | Should -Be @('com.unity.textmeshpro')
            $manifest.locked.'com.unity.textmeshpro'.hash | Should -Be 'abc'
        }
    }
}
