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
        foreach ($name in 'Get-LogFacts', 'Complete-TestRun', 'Get-UnityTestArguments',
            'Save-OptInGateVariables', 'Restore-OptInGateVariables') {
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
  <test-case fullname="Namespace.A test with user@example.com and a::colon fails" result="Failed">
    <failure><message><![CDATA[Real lock isolation preflight refused: DirtyAsset for owner@example.com
second line with a::colon
third line is dropped]]></message></failure>
  </test-case>
</test-run>
'@
        $passingXmlPath = Write-FixtureFile 'passing-results.xml' $xmlPassingRun
        $failedXmlPath = Write-FixtureFile 'failed-results.xml' $xmlFailedRun
        # One passing test and two skipped tests. One skip names the LAC gate.
        # The other skip has a vendor-absent reason.
        $xmlSkippedRun = @'
<test-run total="3" passed="1" failed="0" skipped="2" inconclusive="0">
  <test-suite type="Assembly" name="Alrauna.Amuse.Tests.Editor.dll" fullname="Alrauna.Amuse.Tests.Editor.dll" />
  <test-case fullname="Alrauna.Amuse.Tests.Editor.A passing test" result="Passed" />
  <test-case fullname="Alrauna.Amuse.Tests.Editor.Gated test" result="Skipped" label="Ignored">
    <reason><message><![CDATA[The opt-in variable AMUSE_REAL_LAC_PASS is not set to 1. It changes vendor state.]]></message></reason>
  </test-case>
  <test-case fullname="Alrauna.Amuse.Tests.Editor.Absent vendor test" result="Skipped" label="Ignored">
    <reason><message><![CDATA[The vendor package is not installed in this project.]]></message></reason>
  </test-case>
</test-run>
'@
        $skippedXmlPath = Write-FixtureFile 'skipped-results.xml' $xmlSkippedRun
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

        It 'ASkipForAGateThatTheRunSetFailsTheRun' {
            Mock Say { }
            { Complete-TestRun $skippedXmlPath $assembly 0 $greenFacts @('AMUSE_REAL_LAC_PASS') } |
                Should -Throw '*skipped for a gate that the run set: AMUSE_REAL_LAC_PASS*'
        }

        It 'ASkipForAGateThatTheRunDidNotSetPasses' {
            Mock Say { }
            { Complete-TestRun $skippedXmlPath $assembly 0 $greenFacts @('AMUSE_REAL_THRY_LOCK') } | Should -Not -Throw
            { Complete-TestRun $skippedXmlPath $assembly 0 $greenFacts } | Should -Not -Throw
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

        It 'CompleteTestRunPrintsTheFirstTwoMessageLinesThroughRedact' {
            $printed = [System.Collections.Generic.List[string]]::new()
            Mock Say { param($text) $printed.Add([string]$text) }
            { Complete-TestRun $failedXmlPath $assembly 0 $greenFacts } | Should -Throw '*failed tests*'
            $messageLines = @($printed | Where-Object { $_ -like '      message: *' })
            $messageLines | Should -HaveCount 1
            $messageLines[0] | Should -Match 'DirtyAsset'
            $messageLines[0] | Should -Match 'second line'
            $messageLines[0] | Should -Not -Match 'third line'
            $messageLines[0] | Should -Match '<email>'
            $messageLines[0] | Should -Not -Match 'owner@example\.com'
            $messageLines[0] | Should -Not -Match '::'
        }

        It 'FailedMessageLineIsCutToThreeHundredCharacters' {
            (Format-FailedMessageLine ('word ' * 200)).Length | Should -BeLessThan 330
        }

        It 'CompleteTestRunIsGreenWhenEverythingPasses' {
            $printed = [System.Collections.Generic.List[string]]::new()
            Mock Say { param($text) $printed.Add([string]$text) }
            { Complete-TestRun $passingXmlPath $assembly 0 $greenFacts } | Should -Not -Throw
            $printed | Should -Contain '- tests: total 3, passed 3, failed 0, skipped 0, inconclusive 0'
            @($printed | Where-Object { $_ -like '  - FAILED *' }) | Should -HaveCount 0
        }
    }

    Context 'Opt-in gate variables' {
        BeforeAll {
            # The four gate names of the repo, so a test can strip them
            # without a reference into the script under test.
            $gateNames = @('AMUSE_DAO_INTEGRATION', 'AMUSE_REAL_THRY_LOCK', 'AMUSE_PRODUCTION_ORDER_DISPATCH', 'AMUSE_REAL_LAC_PASS')
        }

        It 'RejectsANameOutsideTheAllowListWithANamedError' {
            { Save-OptInGateVariables @('AMUSE_NOT_A_GATE') } |
                Should -Throw "*Unknown opt-in name 'AMUSE_NOT_A_GATE'*"
        }

        It 'SetsTheListedGateToOneAndLeavesTheOthersUnset' {
            foreach ($gate in $gateNames) {
                [Environment]::SetEnvironmentVariable($gate, [NullString]::Value)
            }
            $saved = Save-OptInGateVariables @('AMUSE_REAL_THRY_LOCK')
            try {
                Test-Path 'Env:\AMUSE_REAL_THRY_LOCK' | Should -BeTrue
                [Environment]::GetEnvironmentVariable('AMUSE_REAL_THRY_LOCK') | Should -Be '1'
                Test-Path 'Env:\AMUSE_DAO_INTEGRATION' | Should -BeFalse
                Test-Path 'Env:\AMUSE_PRODUCTION_ORDER_DISPATCH' | Should -BeFalse
                Test-Path 'Env:\AMUSE_REAL_LAC_PASS' | Should -BeFalse
            }
            finally {
                Restore-OptInGateVariables $saved
            }
            # The caller had the gate unset, so the restore must remove the
            # name again. A plain emptiness check would accept a leftover
            # value of 1.
            Test-Path 'Env:\AMUSE_REAL_THRY_LOCK' | Should -BeFalse
        }

        It 'SavesTheCallerValuesAndRestoresThemAfterTheRun' {
            [Environment]::SetEnvironmentVariable('AMUSE_DAO_INTEGRATION', 'keep-me')
            try {
                $saved = Save-OptInGateVariables @()
                $saved['AMUSE_DAO_INTEGRATION'] | Should -Be 'keep-me'
                [Environment]::GetEnvironmentVariable('AMUSE_DAO_INTEGRATION') | Should -BeNullOrEmpty
                Restore-OptInGateVariables $saved
                [Environment]::GetEnvironmentVariable('AMUSE_DAO_INTEGRATION') | Should -Be 'keep-me'
            }
            finally {
                # The test set this value, so the test unsets it again. The
                # runner and the dev machine carry no gate names.
                [Environment]::SetEnvironmentVariable('AMUSE_DAO_INTEGRATION', $null)
            }
        }

        It 'RemovesAnOptInGateThatTheCallerNeverHadAndKeepsTheSavedValue' {
            [Environment]::SetEnvironmentVariable('AMUSE_DAO_INTEGRATION', 'keep-me')
            try {
                $saved = Save-OptInGateVariables @('AMUSE_REAL_THRY_LOCK')
                Restore-OptInGateVariables $saved
                # The caller never had the opt-in gate, so the restore must
                # remove the name. The caller value must come back.
                Test-Path 'Env:\AMUSE_REAL_THRY_LOCK' | Should -BeFalse
                Test-Path 'Env:\AMUSE_DAO_INTEGRATION' | Should -BeTrue
                [Environment]::GetEnvironmentVariable('AMUSE_DAO_INTEGRATION') | Should -Be 'keep-me'
            }
            finally {
                [Environment]::SetEnvironmentVariable('AMUSE_DAO_INTEGRATION', [NullString]::Value)
            }
        }
    }

    Context 'Workflow opt-in gates' {
        BeforeAll {
            # The workflows call the admin copy of run-tests.ps1. A silent
            # removal of a gate from a product step must turn these tests
            # red. The precedent is the ChangeScope suite, which cuts text
            # out of pr.yml.
            $prWorkflow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../../.github/workflows/pr.yml') -Raw
            $canaryWorkflow = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../../.github/workflows/canary.yml') -Raw

            # Returns the one run-tests.ps1 step line of the workflow that
            # names the assembly pattern. Zero or two lines are a defect.
            function Get-RunTestsLine([string]$workflowText, [string]$assemblyPattern) {
                $lines = @(
                    $workflowText -split "`n" |
                        Where-Object { $_ -match ('run-tests\.ps1.*' + $assemblyPattern) }
                )
                if ($lines.Count -ne 1) {
                    throw "Expected one run-tests.ps1 step line for '$assemblyPattern', found $($lines.Count)."
                }
                return $lines[0]
            }
        }

        It 'ThePrProductStepSetsOnlyTheRealLacPassGate' {
            $line = Get-RunTestsLine $prWorkflow 'Alrauna\.Amuse\.Tests\.Editor'
            $line | Should -Match '-OptIn'
            $line | Should -Match 'AMUSE_REAL_LAC_PASS'
            $line | Should -Not -Match 'AMUSE_REAL_THRY_LOCK'
            $line | Should -Not -Match 'AMUSE_DAO_INTEGRATION'
            $line | Should -Not -Match 'AMUSE_PRODUCTION_ORDER_DISPATCH'
        }

        It 'TheCanaryProductStepSetsAllFourGates' {
            $line = Get-RunTestsLine $canaryWorkflow 'Alrauna\.Amuse\.Tests\.Editor'
            $line | Should -Match '-OptIn'
            $line | Should -Match 'AMUSE_DAO_INTEGRATION'
            $line | Should -Match 'AMUSE_REAL_THRY_LOCK'
            $line | Should -Match 'AMUSE_PRODUCTION_ORDER_DISPATCH'
            $line | Should -Match 'AMUSE_REAL_LAC_PASS'
        }

        It 'TheResearchStepsSetNoGate' {
            Get-RunTestsLine $prWorkflow 'Alrauna\.Amuse\.Research\.Tests\.Editor' | Should -Not -Match '-OptIn'
            Get-RunTestsLine $canaryWorkflow 'Alrauna\.Amuse\.Research\.Tests\.Editor' | Should -Not -Match '-OptIn'
        }
    }
}
