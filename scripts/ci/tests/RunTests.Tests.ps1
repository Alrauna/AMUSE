# Tests for scripts/ci/run-tests.ps1 and for the run helpers that live in
# scripts/ci/lib/Common.ps1. Run them with Pester 5.7.1:
# pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.7.1; Invoke-Pester scripts/ci/tests/RunTests.Tests.ps1 -Output Detailed"
Set-StrictMode -Version Latest

Describe 'Test runner' {
    BeforeAll {
        Set-StrictMode -Version Latest
        . (Join-Path $PSScriptRoot '../lib/Common.ps1')
        . (Join-Path $PSScriptRoot '../run-tests.ps1')

        # The tests below must fail when a function is missing. They must not
        # pass because a throw test silently swallowed the error.
        foreach ($name in 'Get-LogFacts', 'Complete-TestRun', 'Get-UnityTestArguments') {
            Get-Command $name -ErrorAction Stop | Out-Null
        }

        $tempDir = Join-Path ([IO.Path]::GetTempPath()) "amuse-runtests-tests-$PID"
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

        # Writes one small fixture file and returns its path.
        function Write-FixtureFile([string]$name, [string[]]$lines) {
            $path = Join-Path $tempDir $name
            Set-Content -LiteralPath $path -Value $lines
            return $path
        }

        # Reads the value that follows one flag in an argument list. Returns
        # nothing when the flag is absent.
        function Get-ArgumentValue([string[]]$arguments, [string]$flag) {
            $index = [array]::IndexOf($arguments, $flag)
            if ($index -lt 0) { return $null }
            return $arguments[$index + 1]
        }

        $assembly = 'Alrauna.Amuse.Tests.Editor'

        # Two NUnit result fixtures. The failed one carries a test name with
        # an email shape and a double colon, so the test can prove that the
        # printed line went through Redact.
        $xmlPassingRun = @'
<test-run total="3" passed="3" failed="0" skipped="0" inconclusive="0">
  <test-suite type="Assembly" name="Alrauna.Amuse.Tests.Editor.dll" fullname="Alrauna.Amuse.Tests.Editor.dll" />
  <test-case fullname="Alrauna.Amuse.Tests.Editor.A passing test" result="Passed" />
</test-run>
'@
        $xmlFailedRun = @'
<test-run total="3" passed="2" failed="1" skipped="0" inconclusive="0">
  <test-suite type="Assembly" name="Alrauna.Amuse.Tests.Editor.dll" fullname="Alrauna.Amuse.Tests.Editor.dll" />
  <test-case fullname="Namespace.A test with user@example.com and a::colon fails" result="Failed" />
</test-run>
'@
        $passingXmlPath = Write-FixtureFile 'passing-results.xml' $xmlPassingRun
        $failedXmlPath = Write-FixtureFile 'failed-results.xml' $xmlFailedRun
        $greenFacts = @{ Shader = 0; Compile = 0; Licensing = 0 }
    }

    AfterAll {
        Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Context 'Unity test arguments' {
        It 'PassesForceD3D11AndNeverNographics' {
            $arguments = Get-UnityTestArguments '<project-path>' '<log-path>' $assembly '<results-path>'
            $arguments | Should -Contain '-batchmode'
            $arguments | Should -Contain '-force-d3d11'
            $arguments | Should -Not -Contain '-nographics'
        }

        It 'RunsTheNamedAssemblyInEditMode' {
            $arguments = Get-UnityTestArguments '<project-path>' '<log-path>' $assembly '<results-path>'
            $arguments | Should -Contain '-runTests'
            (Get-ArgumentValue $arguments '-testPlatform') | Should -Be 'EditMode'
            # The value after -assemblyNames must be the assembly and nothing
            # else, so a stray name can never widen the run.
            (Get-ArgumentValue $arguments '-assemblyNames') | Should -Be $assembly
        }

        It 'PointsProjectLogAndResultsAtTheGivenPaths' {
            $arguments = Get-UnityTestArguments '<project-path>' '<log-path>' $assembly '<results-path>'
            (Get-ArgumentValue $arguments '-projectPath') | Should -Be '<project-path>'
            (Get-ArgumentValue $arguments '-logFile') | Should -Be '<log-path>'
            (Get-ArgumentValue $arguments '-testResults') | Should -Be '<results-path>'
        }
    }

    Context 'Log facts' {
        It 'CountsShaderErrorLines' {
            $logPath = Write-FixtureFile 'shader.log' @(
                'Initialize engine version: 2022.3.22f1'
                'Shader error in Poiyomi/Toon: parse error'
                'Shader error in lilToon: another parse error'
            )
            $facts = Get-LogFacts $logPath
            $facts.Shader | Should -Be 2
            $facts.Compile | Should -Be 0
        }

        It 'CountsCompileErrorLines' {
            $logPath = Write-FixtureFile 'compile.log' @(
                'Assets\Thing.cs(3,7): error CS0246: The type or namespace name could not be found'
                'Scripts have compiler errors'
                'Compilation failed'
            )
            $facts = Get-LogFacts $logPath
            $facts.Compile | Should -Be 3
            $facts.Shader | Should -Be 0
        }

        It 'IgnoresBenignLicensingClientSignatureLine' {
            # The benign Code 10 line of the licensing client counts as a
            # line about licensing and never as a shader or compile error.
            $logPath = Write-FixtureFile 'benign.log' @(
                'Licensing: Code 10 while verifying Licensing Client signature: BAD, ignoring'
            )
            $facts = Get-LogFacts $logPath
            $facts.Shader | Should -Be 0
            $facts.Compile | Should -Be 0
            $facts.Licensing | Should -Be 1
        }

        It 'MissingLogThrows' {
            { Get-LogFacts (Join-Path $tempDir 'no-such-log.log') } | Should -Throw '*log is missing*'
        }
    }

    Context 'Test run verdict' {
        It 'CompleteTestRunThrowsWhenTheResultFileIsMissing' {
            { Complete-TestRun (Join-Path $tempDir 'missing-results.xml') $assembly 0 $greenFacts } |
                Should -Throw '*result file is missing*'
        }

        It 'CompleteTestRunThrowsOnShaderErrorLinesWithAllTestsPassing' {
            { Complete-TestRun $passingXmlPath $assembly 0 @{ Shader = 1; Compile = 0; Licensing = 0 } } |
                Should -Throw '*shader or compile errors*'
        }

        It 'CompleteTestRunThrowsOnCompileErrorLinesWithAllTestsPassing' {
            { Complete-TestRun $passingXmlPath $assembly 0 @{ Shader = 0; Compile = 2; Licensing = 0 } } |
                Should -Throw '*shader or compile errors*'
        }

        It 'CompleteTestRunPrintsFailedNamesThroughRedact' {
            $printed = [System.Collections.Generic.List[string]]::new()
            Mock Say { param($text) $printed.Add([string]$text) }
            { Complete-TestRun $failedXmlPath $assembly 0 $greenFacts } | Should -Throw '*failed tests*'
            $failedLines = @($printed | Where-Object { $_ -like '  - FAILED *' })
            $failedLines | Should -HaveCount 1
            $failedLines[0] | Should -Match '<email>'
            $failedLines[0] | Should -Not -Match 'user@example\.com'
            $failedLines[0] | Should -Match 'a: :colon'
            $failedLines[0] | Should -Not -Match '::'
            # The counters print before the verdict, so a failed run still
            # reports its numbers.
            $printed | Should -Contain '- tests: total 3, passed 2, failed 1, skipped 0, inconclusive 0'
        }

        It 'CompleteTestRunIsGreenWhenEverythingPasses' {
            $printed = [System.Collections.Generic.List[string]]::new()
            Mock Say { param($text) $printed.Add([string]$text) }
            { Complete-TestRun $passingXmlPath $assembly 0 $greenFacts } | Should -Not -Throw
            $printed | Should -Contain '- tests: total 3, passed 3, failed 0, skipped 0, inconclusive 0'
            @($printed | Where-Object { $_ -like '  - FAILED *' }) | Should -HaveCount 0
        }
    }
}
