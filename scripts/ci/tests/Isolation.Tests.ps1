# Tests for scripts/ci/lib/Isolation.ps1. The tests cover the pure parts only.
# They never create a user, never set an access rule and never start a process.
# A rule object is built in memory and then thrown away.
# Run them with Pester 5.7.1:
# pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.7.1; Invoke-Pester scripts/ci/tests/Isolation.Tests.ps1 -Output Detailed"
Set-StrictMode -Version Latest

Describe 'Isolation library' {
    BeforeAll {
        Set-StrictMode -Version Latest
        . (Join-Path $PSScriptRoot '../lib/Isolation.ps1')

        # The tests below must fail when a function is missing. They must not
        # pass because a throw test silently swallowed the error.
        foreach ($name in 'Get-RunPaths', 'New-DenyWriteRule', 'Add-DenyWrite', 'Get-LowCredential',
            'New-LowUser', 'Grant-LowAccess', 'Get-LowEnvironment', 'Start-AsLowUser', 'Stop-Tree',
            'Stop-LowUserProcesses', 'Remove-LowUser', 'Copy-AdminScripts', 'Find-LicensingClient',
            'Assert-LicensingClientSubject', 'Get-ProbeTargets', 'Get-IsolationVerdict') {
            Get-Command $name -ErrorAction Stop | Out-Null
        }

        $tempDir = Join-Path ([IO.Path]::GetTempPath()) "amuse-isolation-tests-$PID"
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }

    It 'RunPathsDeriveFromTheEnvironment' {
        $oldWorkspace = $env:GITHUB_WORKSPACE
        $oldTemp = $env:RUNNER_TEMP
        try {
            $fakeRoot = Join-Path $tempDir 'workspace'
            $fakeWork = Join-Path $tempDir 'work'
            $env:GITHUB_WORKSPACE = $fakeRoot
            $env:RUNNER_TEMP = $fakeWork
            $runPaths = Get-RunPaths
            ($runPaths.Keys | Sort-Object) -join ',' | Should -Be 'AdminScripts,LicenseDir,LicensePath,LowRoot,LowSecretFile,LowUser,Out,ProjectPath,Root,UnityExe,UnityHome,Work'
            $runPaths.Root | Should -Be $fakeRoot
            $runPaths.Work | Should -Be $fakeWork
            $runPaths.UnityHome | Should -Be (Join-Path $fakeWork 'UnityEditor')
            $runPaths.UnityExe | Should -Be (Join-Path $fakeWork 'UnityEditor\Editor\Unity.exe')
            $runPaths.LicenseDir | Should -Be (Join-Path $env:ProgramData 'Unity')
            $runPaths.LicensePath | Should -Be (Join-Path $env:ProgramData 'Unity\Unity_lic.ulf')
            $runPaths.LowUser | Should -Be 'amuseci'
            $runPaths.LowRoot | Should -Be "$($env:SystemDrive)\amuse-low"
            $runPaths.Out | Should -Be (Join-Path $runPaths.LowRoot 'out')
            $runPaths.ProjectPath | Should -Be (Join-Path $runPaths.LowRoot 'project')
            $runPaths.LowSecretFile | Should -Be (Join-Path $fakeWork 'lowuser.cred')
            $runPaths.AdminScripts | Should -Be (Join-Path $fakeWork 'admin-scripts')

            # Without a workspace variable the root falls back to the repository
            # root, two levels above the scripts folder. That fallback is for
            # local use only.
            $env:GITHUB_WORKSPACE = $null
            $fallback = Get-RunPaths
            $fallback.Root | Should -Be (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
        }
        finally {
            $env:GITHUB_WORKSPACE = $oldWorkspace
            $env:RUNNER_TEMP = $oldTemp
        }
    }

    It 'LowEnvironmentBlanksGithubAndRunnerVariables' {
        $base = @{
            PATH = 'tools-first'
            GITHUB_ENV = 'env-file'
            GITHUB_WORKSPACE = 'workspace-dir'
            RUNNER_TEMP = 'temp-dir'
            RUNNER_TRACKING_ID = 'tracking-id'
            ACTIONS_RUNTIME_TOKEN = 'token'
            ACTIONS_CACHE_URL = 'cache-url'
            ImageOS = 'win22'
            ImageVersion = '2022.11.1'
        }
        $low = Get-LowEnvironment $base 'profile-dir'
        $low['GITHUB_ENV'] | Should -Be ''
        $low['GITHUB_WORKSPACE'] | Should -Be ''
        $low['RUNNER_TEMP'] | Should -Be ''
        $low['RUNNER_TRACKING_ID'] | Should -Be ''
        $low['ACTIONS_RUNTIME_TOKEN'] | Should -Be ''
        $low['ACTIONS_CACHE_URL'] | Should -Be ''
        $low['ImageOS'] | Should -Be ''
        $low['ImageVersion'] | Should -Be ''
        $low['PATH'] | Should -Be 'tools-first'
    }

    It 'LowEnvironmentRepointsProfileVariablesToTheLowProfile' {
        $base = @{ USERNAME = 'other-account'; USERPROFILE = 'old-profile'; HOME = 'old-home' }
        $low = Get-LowEnvironment $base 'new-profile'
        $low['USERNAME'] | Should -Be 'amuseci'
        $low['USERPROFILE'] | Should -Be 'new-profile'
        $low['HOME'] | Should -Be 'new-profile'
        $low['HOMEPATH'] | Should -Be '\Users\amuseci'
        $low['LOCALAPPDATA'] | Should -Be (Join-Path 'new-profile' 'AppData\Local')
        $low['APPDATA'] | Should -Be (Join-Path 'new-profile' 'AppData\Roaming')
        $low['TEMP'] | Should -Be (Join-Path 'new-profile' 'AppData\Local\Temp')
        $low['TMP'] | Should -Be (Join-Path 'new-profile' 'AppData\Local\Temp')
    }

    It 'DenyRuleNamesNoSynchronizeRight' {
        $rule = New-DenyWriteRule 'Guest'
        $rule.AccessControlType | Should -Be ([Security.AccessControl.AccessControlType]::Deny)
        ([string]$rule.IdentityReference) | Should -BeLike '*Guest'
        foreach ($name in 'WriteData', 'AppendData', 'WriteExtendedAttributes', 'WriteAttributes',
            'DeleteSubdirectoriesAndFiles', 'Delete') {
            ($rule.FileSystemRights -band [Security.AccessControl.FileSystemRights]::$name) |
                Should -Be ([Security.AccessControl.FileSystemRights]::$name)
        }
        # A deny on the right to synchronize makes every open fail, even a read.
        # The rule must never name it.
        ($rule.FileSystemRights -band [Security.AccessControl.FileSystemRights]::Synchronize) | Should -Be 0
        $rule.InheritanceFlags | Should -Be ([Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit')
        $rule.PropagationFlags | Should -Be ([Security.AccessControl.PropagationFlags]::None)
    }

    It 'ProbeTargetListHoldsTheLicenseFolderAsAnAllowedWrite' {
        $targets = @(Get-ProbeTargets)
        $targets.Count | Should -BeGreaterThan 0
        foreach ($target in $targets) {
            $target.Label | Should -Not -BeNullOrEmpty
            $target.Path | Should -Not -BeNullOrEmpty
        }
        $license = @($targets | Where-Object { $_.Label -eq 'license folder' })
        $license.Count | Should -Be 1
        $license[0].AllowWrite | Should -BeTrue
        foreach ($target in @($targets | Where-Object { $_.Label -ne 'license folder' })) {
            $target.AllowWrite | Should -BeFalse
        }
    }

    It 'ProbeVerdictFailsWhenAnyNonAllowedPathIsWritable' {
        $facts = @{
            RunnerProcesses = @(@{ Name = 'Runner.Worker'; Readable = $false })
            Writable = @(
                @{ Label = 'workspace'; AllowWrite = $false; Writable = $false }
                @{ Label = 'runner temp folder'; AllowWrite = $false; Writable = $true }
            )
        }
        $violations = @(Get-IsolationVerdict $facts)
        $violations.Count | Should -BeGreaterThan 0
        ($violations -join ' ') | Should -BeLike '*runner temp folder*'
    }

    It 'ProbeVerdictFailsWhenAnyRunnerProcessIsReadable' {
        $facts = @{
            RunnerProcesses = @(
                @{ Name = 'Runner.Worker'; Readable = $false }
                @{ Name = 'Runner.Listener'; Readable = $true }
            )
            Writable = @(@{ Label = 'license folder'; AllowWrite = $true; Writable = $true })
        }
        $violations = @(Get-IsolationVerdict $facts)
        $violations.Count | Should -BeGreaterThan 0
        ($violations -join ' ') | Should -BeLike '*Runner.Listener*'
    }

    It 'ProbeVerdictFailsWhenNoRunnerProcessWasFound' {
        # An empty list proves nothing, so the verdict must fail closed.
        $facts = @{
            RunnerProcesses = @()
            Writable = @(@{ Label = 'license folder'; AllowWrite = $true; Writable = $true })
        }
        $violations = @(Get-IsolationVerdict $facts)
        $violations.Count | Should -BeGreaterThan 0
    }

    It 'ProbeVerdictPassesWhenOnlyTheLicenseFolderIsWritable' {
        $facts = @{
            RunnerProcesses = @(
                @{ Name = 'Runner.Worker'; Readable = $false }
                @{ Name = 'Runner.Listener'; Readable = $false }
            )
            Writable = @(
                @{ Label = 'admin scripts folder'; AllowWrite = $false; Writable = $false }
                @{ Label = 'workspace'; AllowWrite = $false; Writable = $false }
                @{ Label = 'license folder'; AllowWrite = $true; Writable = $true }
            )
        }
        $violations = @(Get-IsolationVerdict $facts)
        $violations.Count | Should -Be 0
    }

    It 'LicensingClientSubjectMismatchThrowsAndNamesTheObservedSubject' {
        # The observed signer subject is a public value, so the message may name it.
        $signature = [pscustomobject]@{
            Status = 'Valid'
            SignerCertificate = [pscustomobject]@{ Subject = 'CN=Some Other Company, O=Some Other Company' }
        }
        { Assert-LicensingClientSubject $signature 'CN=Unity Technologies SF, O=Unity Technologies SF' } |
            Should -Throw '*Some Other Company*'
    }
}
