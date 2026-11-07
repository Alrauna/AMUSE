# Tests for scripts/ci/check-release-exclusion.ps1. Run them with Pester 5.7.1:
# pwsh -NoProfile -Command "Import-Module Pester -RequiredVersion 5.7.1; Invoke-Pester scripts/ci/tests/ReleaseExclusion.Tests.ps1 -Output Detailed"
Set-StrictMode -Version Latest

Describe 'Release exclusion check' {
    BeforeAll {
        Set-StrictMode -Version Latest
        . (Join-Path $PSScriptRoot '../check-release-exclusion.ps1')
        # The tests below must fail when a function is missing. They must not
        # pass because a throw test silently swallowed the error.
        foreach ($name in 'Test-ReleaseZip', 'New-ReleaseZip') {
            Get-Command $name -ErrorAction Stop | Out-Null
        }
        $tempRoot = Join-Path ([IO.Path]::GetTempPath()) "amuse-release-check-tests-$PID"
        New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
    }

    AfterAll {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
    }

    It 'TestsFolderEntryIsAViolation' {
        $violations = @(Test-ReleaseZip @('Tests/x.cs', 'Editor/Alrauna.Amuse.Editor.asmdef'))
        $violations.Count | Should -Be 1
        $violations[0] | Should -BeLike '*Tests/x.cs*'
    }

    It 'TestsMetaEntryIsAViolation' {
        $violations = @(Test-ReleaseZip @('Tests.meta', 'Editor/Alrauna.Amuse.Editor.asmdef'))
        $violations.Count | Should -Be 1
        $violations[0] | Should -BeLike '*Tests.meta*'
    }

    It 'CleanListPasses' {
        $violations = @(Test-ReleaseZip @(
            'Editor/Alrauna.Amuse.Editor.asmdef', 'Editor/Foo.cs', 'Editor/Foo.cs.meta',
            'Runtime/x.cs', 'Runtime/x.cs.meta', 'package.json', 'package.json.meta'))
        $violations.Count | Should -Be 0
    }

    It 'MissingEditorAsmdefIsAViolation' {
        $violations = @(Test-ReleaseZip @('Runtime/x.cs', 'package.json'))
        $violations.Count | Should -Be 1
        $violations[0] | Should -BeLike '*assembly*'
    }

    It 'NestedTestsFolderIsAViolation' {
        # The release rule matches a Tests folder at any depth, not only at
        # the top. A zip that holds Runtime/Tests/x.cs must fail.
        $violations = @(Test-ReleaseZip @('Runtime/Tests/x.cs', 'Editor/Alrauna.Amuse.Editor.asmdef'))
        $violations.Count | Should -Be 1
        $violations[0] | Should -BeLike '*Runtime/Tests/x.cs*'
    }

    It 'BackslashSeparatedTestsEntryIsAViolation' {
        # The check normalizes backslashes first, so a zip built on Windows
        # behaves like the zip of the release step.
        $violations = @(Test-ReleaseZip @('Tests\x.cs', 'Editor/Alrauna.Amuse.Editor.asmdef'))
        $violations.Count | Should -Be 1
        $violations[0] | Should -BeLike '*Tests/x.cs*'
    }

    It 'EmptyEntryListIsAViolation' {
        # A zip that holds nothing proves nothing, so it must fail.
        $violations = @(Test-ReleaseZip @())
        $violations.Count | Should -Be 1
        $violations[0] | Should -BeLike '*no entries*'
        $fromNull = @(Test-ReleaseZip $null)
        $fromNull.Count | Should -Be 1
    }

    It 'BuilderLeavesOutTestsAndKeepsTheAsmdef' {
        # A small fake package with the same shapes as the real one. The zip
        # builder must leave out the top-level Tests folder and Tests.meta,
        # and it must keep the production editor assembly.
        $package = Join-Path $tempRoot 'fake-package'
        New-Item -ItemType Directory -Path (Join-Path $package 'Editor') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $package 'Runtime') -Force | Out-Null
        New-Item -ItemType Directory -Path (Join-Path $package 'Tests') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $package 'Editor/Alrauna.Amuse.Editor.asmdef') -Value '{ "name": "x" }'
        Set-Content -LiteralPath (Join-Path $package 'Runtime/x.cs') -Value 'class X {}'
        Set-Content -LiteralPath (Join-Path $package 'Tests/t.cs') -Value 'class T {}'
        Set-Content -LiteralPath (Join-Path $package 'Tests/t.cs.meta') -Value 'meta'
        Set-Content -LiteralPath (Join-Path $package 'Tests.meta') -Value 'meta'

        $zipPath = Join-Path $tempRoot 'fake-package.zip'
        New-ReleaseZip -PackagePath $package -ZipPath $zipPath
        $zipPath | Should -Exist

        $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
        try {
            $names = @($zip.Entries | ForEach-Object { $_.FullName })
        } finally {
            $zip.Dispose()
        }
        ($names | Where-Object { $_ -match '(^|/)Tests(/|\.meta$)' }) | Should -BeNullOrEmpty
        $names | Should -Contain 'Editor/Alrauna.Amuse.Editor.asmdef'
        $names | Should -Contain 'Runtime/x.cs'
        @(Test-ReleaseZip $names).Count | Should -Be 0
    }
}
