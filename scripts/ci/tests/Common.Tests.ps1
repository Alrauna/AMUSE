# Tests for scripts/ci/lib/Common.ps1. Run them with Pester 5.7.1:
# pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.7.1; Invoke-Pester scripts/ci/tests/Common.Tests.ps1 -Output Detailed"
Set-StrictMode -Version Latest

Describe 'Common library' {
    BeforeAll {
        Set-StrictMode -Version Latest
        . (Join-Path $PSScriptRoot '../lib/Common.ps1')

        $resultsDir = Join-Path ([IO.Path]::GetTempPath()) "amuse-common-tests-$PID"
        New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null

        function Write-ResultFile([string]$name, [string]$xml) {
            $path = Join-Path $resultsDir $name
            Set-Content -LiteralPath $path -Value $xml
            return $path
        }

        function New-RunResult([int]$total, [int]$passed, [int]$failed, [int]$skipped, [int]$inconclusive, [string[]]$assemblies = @('AMUSE.Product.Tests')) {
            return [pscustomobject]@{
                Total = $total
                Passed = $passed
                Failed = $failed
                Skipped = $skipped
                Inconclusive = $inconclusive
                FailedNames = @()
                Assemblies = $assemblies
            }
        }

        $xmlFullRun = @'
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<test-run total="4" passed="2" failed="1" skipped="1" inconclusive="0">
  <test-suite type="Assembly" name="AMUSE.Product.Tests.dll" fullname="AMUSE.Product.Tests.dll">
    <test-case fullname="AMUSE.Product.Tests.OrderTests.Sends_Order" result="Passed" />
    <test-case fullname="AMUSE.Product.Tests.OrderTests.Keeps_Ledger" result="Failed" />
    <test-case fullname="AMUSE.Product.Tests.OrderTests.Skips_One" result="Skipped" />
    <test-case fullname="AMUSE.Product.Tests.OrderTests.Passes_Two" result="Passed" />
  </test-suite>
  <test-suite type="Assembly" name="AMUSE.Research.Tests.dll" fullname="AMUSE.Research.Tests.dll" />
  <test-suite type="SetUpFixture" name="AMUSE.Product.Tests" />
</test-run>
'@

        # Unity can write the assembly suite name as a full path. The two
        # suites here use a forward-slash path and a backslash path. Neither
        # fixture name holds a machine identity.
        $xmlPathStyleRun = @'
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<test-run total="2" passed="2" failed="0" skipped="0" inconclusive="0">
  <test-suite type="Assembly" name="/agents/work/job/AMUSE.Product.Tests.dll" fullname="/agents/work/job/AMUSE.Product.Tests.dll">
    <test-case fullname="AMUSE.Product.Tests.OrderTests.Sends_Order" result="Passed" />
  </test-suite>
  <test-suite type="Assembly" name="job\AMUSE.Research.Tests.dll" fullname="job\AMUSE.Research.Tests.dll">
    <test-case fullname="AMUSE.Research.Tests.SampleTests.Samples_One" result="Passed" />
  </test-suite>
</test-run>
'@
    }

    AfterAll {
        Remove-Item -LiteralPath $resultsDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Context 'Redact' {
        It 'RedactMasksEmailSerialGuidAndLongTokens' {
            $token = 'x9z-' * 12
            $line = 'ci.user+tag@example.com AB-CD12-EF34-GH56-IJ78-KL90 01234567-89ab-cdef-0123-456789abcdef 0123456789abcdef012345678a ' + $token
            $out = Redact $line
            $out | Should -Match '<email>'
            $out | Should -Match '<serial>'
            $out | Should -Match '<guid>'
            $out | Should -Match '<hex>'
            $out | Should -Match '<token>'
            $out | Should -Not -BeLike '*@*'
            $out | Should -Not -BeLike '*example.com*'
            $out | Should -Not -BeLike '*CD12*'
        }

        It 'RedactSplitsDoubleColons' {
            Redact 'step::error::Build failed' | Should -Be 'step: :error: :Build failed'
            Redact 'no colons here' | Should -Not -Match '::'
        }

        It 'FailedTestNameWithDoubleColonIsNeverPrintedRaw' {
            $line = Format-FailedTestLine 'AMUSE.Product.Tests.Queue::Worker.Send_Order user@example.com'
            $line | Should -BeLike '  - FAILED *'
            $line | Should -Match ': :'
            $line | Should -Not -Match '::'
            $line | Should -Not -BeLike '*user@example.com*'
            $line | Should -Match '<email>'
        }
    }

    Context 'Credentials and licensing arguments' {
        It 'CredentialRejectsQuoteAndWhitespaceInPassword' {
            Get-Command Test-UnityCredential -ErrorAction Stop | Out-Null
            { Test-UnityCredential 'ci.user@example.com' 'has"quote' } | Should -Throw
            { Test-UnityCredential 'ci.user@example.com' 'has space' } | Should -Throw
        }

        It 'CredentialRejectsEmailWithoutDomain' {
            Get-Command Test-UnityCredential -ErrorAction Stop | Out-Null
            { Test-UnityCredential 'ci-user-at-host' 'pass' } | Should -Throw
        }

        It 'CredentialTrimsBothValues' {
            $pair = Test-UnityCredential '  ci.user@example.com ' " `tsecret`t "
            $pair.Email | Should -Be 'ci.user@example.com'
            $pair.Password | Should -Be 'secret'
        }

        It 'ArgumentsDoubleTrailingBackslash' {
            $arguments = ConvertTo-LicensingArguments 'ci.user@example.com' 'abc\'
            $arguments | Should -Be '--activate-ulf --username "ci.user@example.com" --password "abc\\" --include-personal'
        }

        It 'ArgumentsKeepInnerBackslashes' {
            $arguments = ConvertTo-LicensingArguments 'ci.user@example.com' 'a\b'
            $arguments | Should -Be '--activate-ulf --username "ci.user@example.com" --password "a\b" --include-personal'
        }
    }

    Context 'NUnit result reader' {
        It 'ReadNUnitResultExtractsAssemblyNames' {
            $path = Write-ResultFile 'full-run.xml' $xmlFullRun
            $result = Read-NUnitResult $path
            @($result.Assemblies) | Should -Be @('AMUSE.Product.Tests', 'AMUSE.Research.Tests')
            $result.Total | Should -Be 4
            $result.Passed | Should -Be 2
            $result.Failed | Should -Be 1
            $result.Skipped | Should -Be 1
            $result.Inconclusive | Should -Be 0
            @($result.FailedNames) | Should -Be @('AMUSE.Product.Tests.OrderTests.Keeps_Ledger')
        }

        It 'ReadNUnitResultThrowsOnMissingFile' {
            Get-Command Read-NUnitResult -ErrorAction Stop | Out-Null
            { Read-NUnitResult (Join-Path $resultsDir 'missing-run.xml') } | Should -Throw
        }

        It 'ReadNUnitResultReducesAPathStyleAssemblyNameToItsLeaf' {
            # Unity can write the assembly suite name as a full path with
            # forward or back separators. The reader must keep the leaf name
            # without the .dll suffix.
            $path = Write-ResultFile 'path-style-run.xml' $xmlPathStyleRun
            $result = Read-NUnitResult $path
            @($result.Assemblies) | Should -Be @('AMUSE.Product.Tests', 'AMUSE.Research.Tests')
        }

        It 'AssertTestRunAcceptsAPathStyleAssemblyName' {
            $path = Write-ResultFile 'path-style-run.xml' $xmlPathStyleRun
            $result = Read-NUnitResult $path
            Assert-TestRun $result 'AMUSE.Product.Tests' 0
            Assert-TestRun $result 'amuse.research.tests' 0
        }
    }

    Context 'Test run assertions' {
        It 'AssertTestRunRejectsZeroTests' {
            Get-Command Assert-TestRun -ErrorAction Stop | Out-Null
            { Assert-TestRun (New-RunResult 0 0 0 0 0) 'AMUSE.Product.Tests' 0 } | Should -Throw
        }

        It 'AssertTestRunRejectsMissingAssembly' {
            Get-Command Assert-TestRun -ErrorAction Stop | Out-Null
            { Assert-TestRun (New-RunResult 4 4 0 0 0 @('AMUSE.Research.Tests')) 'AMUSE.Product.Tests' 0 } | Should -Throw
        }

        It 'AssertTestRunRejectsOneFailure' {
            Get-Command Assert-TestRun -ErrorAction Stop | Out-Null
            { Assert-TestRun (New-RunResult 4 3 1 0 0) 'AMUSE.Product.Tests' 0 } | Should -Throw
        }

        It 'AssertTestRunRejectsInconclusiveTest' {
            Get-Command Assert-TestRun -ErrorAction Stop | Out-Null
            { Assert-TestRun (New-RunResult 4 3 0 0 1) 'AMUSE.Product.Tests' 0 } | Should -Throw
        }

        It 'AssertTestRunRejectsAllSkipped' {
            Get-Command Assert-TestRun -ErrorAction Stop | Out-Null
            { Assert-TestRun (New-RunResult 4 0 0 4 0) 'AMUSE.Product.Tests' 0 } | Should -Throw
        }

        It 'AssertTestRunRejectsNonZeroExitWithAllPassing' {
            Get-Command Assert-TestRun -ErrorAction Stop | Out-Null
            { Assert-TestRun (New-RunResult 4 4 0 0 0) 'AMUSE.Product.Tests' 1 } | Should -Throw
        }

        It 'AssertTestRunAcceptsHealthyRun' {
            { Assert-TestRun (New-RunResult 4 4 0 0 0) 'AMUSE.Product.Tests' 0 } | Should -Not -Throw
        }
    }
}
