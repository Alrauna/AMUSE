BeforeAll {
    . (Join-Path $PSScriptRoot '../get-change-scope.ps1')
}

Describe 'Change scope' {
    Context 'Test-UnityNeeded' {
        It 'DocumentationOnlyChangeNeedsNoUnity' {
            Test-UnityNeeded @('docs/superpowers/specs/a.md', 'README.md', 'docs/img/b.png') | Should -BeFalse
        }

        It 'DependabotAndTheTwoUnrunWorkflowsNeedNoUnity' {
            Test-UnityNeeded @('.github/dependabot.yml', '.github/workflows/build-listing.yml', '.github/workflows/release.yml') |
                Should -BeFalse
        }

        It 'EveryPathOutsideTheAllowListNeedsUnity' {
            foreach ($path in 'Packages/com.alrauna.amuse/Editor/A.cs',
                'Packages/com.alrauna.amuse/README.md',
                'Packages/vpm-manifest.json',
                'Assets/Scene.unity',
                'ProjectSettings/ProjectSettings.asset',
                'scripts/ci/lock.json',
                'scripts/ci/get-change-scope.ps1',
                '.github/workflows/pr.yml',
                '.github/workflows/canary.yml',
                '.github/CODEOWNERS',
                'AGENTS.md',
                'CLAUDE.md',
                'NOTES.md') {
                Test-UnityNeeded @($path) | Should -BeTrue -Because $path
            }
        }

        It 'OneNeededPathAmongSafePathsNeedsUnity' {
            Test-UnityNeeded @('docs/a.md', 'README.md', 'Packages/com.alrauna.amuse/Editor/A.cs') | Should -BeTrue
        }

        It 'NoChangedPathsFailsClosed' {
            Test-UnityNeeded @() | Should -BeTrue
            Test-UnityNeeded $null | Should -BeTrue
        }

        It 'LookAlikePathsNeedUnity' {
            # Falsifier: a loose match would let each of these through.
            foreach ($path in 'Docs/a.md', 'docs/a.MD', 'docs/a.cs', 'docs/a.ps1', 'docs/run.sh',
                'README.md.cs', 'sub/README.md', 'x/docs/a.md',
                '.github/workflows/release.yml.bak', '.github/workflows/sub/release.yml',
                '.github/workflows/Release.yml', '.github/dependabot.yml/x', '.github/dependabot.yaml') {
                Test-UnityNeeded @($path) | Should -BeTrue -Because $path
            }
        }

        It 'PathsWithTraversalOrOddCharactersNeedUnity' {
            foreach ($path in 'docs/../Packages/a.cs', 'docs\a.md', '/docs/a.md', "docs/a`n.md", "docs/a.md`n", "README.md`n", "README.md`r", 'docs/a.md ', '', ' ') {
                Test-UnityNeeded @($path) | Should -BeTrue -Because "[$path]"
            }
        }
    }

    Context 'Get-ChangedPath' {
        BeforeAll {
            $script:repo = Join-Path ([IO.Path]::GetTempPath()) "amuse-scope-tests-$PID"
            New-Item -ItemType Directory -Path $script:repo | Out-Null
            git -C $script:repo init -q
            git -C $script:repo config user.email 'test@example.invalid'
            git -C $script:repo config user.name 'test'
            git -C $script:repo config core.autocrlf false
            New-Item -ItemType Directory -Path (Join-Path $script:repo 'Packages') | Out-Null
            Set-Content -LiteralPath (Join-Path $script:repo 'Packages/a.cs') -Value 'class A {}'
            Set-Content -LiteralPath (Join-Path $script:repo 'keep.txt') -Value 'keep'
            git -C $script:repo add -A
            git -C $script:repo commit -q -m base
            $script:base = (git -C $script:repo rev-parse HEAD).Trim()
        }

        AfterAll {
            if (Test-Path -LiteralPath $script:repo) { Remove-Item -LiteralPath $script:repo -Recurse -Force }
        }

        It 'ARenameFromCodeToDocsListsBothPathsSoTheOldPathStillNeedsUnity' {
            # Falsifier: git rename detection would list only the new path,
            # and a move of code into docs would look safe.
            New-Item -ItemType Directory -Path (Join-Path $script:repo 'docs') | Out-Null
            git -C $script:repo mv Packages/a.cs docs/a.md
            git -C $script:repo commit -q -m move
            $paths = @(Get-ChangedPath -Base $script:base -RepoPath $script:repo)
            $paths | Should -Contain 'Packages/a.cs'
            $paths | Should -Contain 'docs/a.md'
            Test-UnityNeeded $paths | Should -BeTrue
        }

        It 'ANameWithSpacesAndNonAsciiComesBackUnquoted' {
            # Falsifier: without the -z flag git quotes such a name in octal
            # escapes, and the name would not match the allow list.
            $name = 'docs/' + [char]0x00FC + ' b.md'
            Set-Content -LiteralPath (Join-Path $script:repo $name) -Value 'x'
            git -C $script:repo add -A
            git -C $script:repo commit -q -m spaces
            $head = (git -C $script:repo rev-parse HEAD~1).Trim()
            $oldEncoding = [Console]::OutputEncoding
            try {
                [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
                $paths = @(Get-ChangedPath -Base $head -RepoPath $script:repo)
            }
            finally { [Console]::OutputEncoding = $oldEncoding }
            $paths | Should -Be @($name)
            $paths[0] | Should -Not -Match '"'
        }

        It 'AFailingGitCommandThrows' {
            { Get-ChangedPath -Base 'no-such-ref' -RepoPath $script:repo } | Should -Throw
        }
    }

    Context 'Get-UnityNeededForEvent' {
        It 'APushToMainAlwaysNeedsUnity' {
            Get-UnityNeededForEvent -EventName 'push' -Paths @('docs/a.md') | Should -BeTrue
        }

        It 'AnUnknownEventFailsClosed' {
            Get-UnityNeededForEvent -EventName 'workflow_dispatch' -Paths @('docs/a.md') | Should -BeTrue
            Get-UnityNeededForEvent -EventName '' -Paths @('docs/a.md') | Should -BeTrue
        }

        It 'APullRequestAgainstTheDefaultBranchFollowsThePaths' {
            Get-UnityNeededForEvent -EventName 'pull_request' -Paths @('docs/a.md') -BaseBranch 'main' -DefaultBranch 'main' | Should -BeFalse
            Get-UnityNeededForEvent -EventName 'pull_request' -Paths @('scripts/ci/lock.json') -BaseBranch 'main' -DefaultBranch 'main' | Should -BeTrue
        }

        It 'APullRequestAgainstAnotherBranchAlwaysNeedsUnity' {
            # Falsifier: a docs-only diff against a feature branch can sit on
            # top of unreviewed work. A retarget of the base starts no run.
            Get-UnityNeededForEvent -EventName 'pull_request' -Paths @('docs/a.md') -BaseBranch 'feature/x' -DefaultBranch 'main' | Should -BeTrue
            Get-UnityNeededForEvent -EventName 'pull_request' -Paths @('docs/a.md') -BaseBranch 'Main' -DefaultBranch 'main' | Should -BeTrue
        }

        It 'AMissingBranchNameFailsClosed' {
            Get-UnityNeededForEvent -EventName 'pull_request' -Paths @('docs/a.md') | Should -BeTrue
            Get-UnityNeededForEvent -EventName 'pull_request' -Paths @('docs/a.md') -BaseBranch 'main' | Should -BeTrue
            Get-UnityNeededForEvent -EventName 'pull_request' -Paths @('docs/a.md') -BaseBranch '' -DefaultBranch 'main' | Should -BeTrue
        }
    }
}

Describe 'Validate verdict in pr.yml' {
    BeforeAll {
        # The verdict function lives inline in the workflow, because nothing
        # from a pull request may run in the validate job. The test cuts the
        # function text out of the workflow file and runs it.
        $text = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../../.github/workflows/pr.yml') -Raw
        $start = $text.IndexOf('function Get-ValidateVerdict')
        $end = $text.IndexOf('$needs = $env:NEEDS_JSON')
        $start | Should -BeGreaterThan -1
        $end | Should -BeGreaterThan $start
        Invoke-Expression ($text.Substring($start, $end - $start))

        function New-Needs([string]$scope, [string]$needed, [string]$unity, [string]$fork, [string]$scripts = 'success', [string]$cheap = 'success') {
            $json = @{
                scripts       = @{ result = $scripts; outputs = @{} }
                cheap         = @{ result = $cheap; outputs = @{} }
                scope         = @{ result = $scope; outputs = @{ unity_needed = $needed } }
                unity         = @{ result = $unity; outputs = @{} }
                'fork-notice' = @{ result = $fork; outputs = @{} }
            } | ConvertTo-Json -Depth 5
            return ($json | ConvertFrom-Json)
        }
    }

    It 'ANeededRunPassesOnlyWhenUnitySucceeds' {
        @(Get-ValidateVerdict (New-Needs 'success' 'true' 'success' 'skipped')).Count | Should -Be 0
        @(Get-ValidateVerdict (New-Needs 'success' 'true' 'failure' 'skipped')).Count | Should -BeGreaterThan 0
        @(Get-ValidateVerdict (New-Needs 'success' 'true' 'skipped' 'skipped')).Count | Should -BeGreaterThan 0
        @(Get-ValidateVerdict (New-Needs 'success' 'true' 'skipped' 'failure')).Count | Should -BeGreaterThan 0
    }

    It 'ASkipIsLegalOnlyWhenTheScopeSaysNoUnityIsNeeded' {
        @(Get-ValidateVerdict (New-Needs 'success' 'false' 'skipped' 'skipped')).Count | Should -Be 0
    }

    It 'AFailedOrMissingScopeNeverPasses' {
        @(Get-ValidateVerdict (New-Needs 'failure' 'false' 'skipped' 'skipped')).Count | Should -BeGreaterThan 0
        @(Get-ValidateVerdict (New-Needs 'skipped' 'false' 'skipped' 'skipped')).Count | Should -BeGreaterThan 0
        @(Get-ValidateVerdict (New-Needs 'success' '' 'skipped' 'skipped')).Count | Should -BeGreaterThan 0
        @(Get-ValidateVerdict (New-Needs 'success' 'maybe' 'skipped' 'skipped')).Count | Should -BeGreaterThan 0
        @(Get-ValidateVerdict (New-Needs 'success' 'True' 'skipped' 'skipped')).Count | Should -BeGreaterThan 0
    }

    It 'AScopeJobMissingFromNeedsNeverPasses' {
        $needs = New-Needs 'success' 'false' 'skipped' 'skipped'
        $needs.PSObject.Properties.Remove('scope')
        @(Get-ValidateVerdict $needs).Count | Should -BeGreaterThan 0
    }

    It 'ASkippedOrFailedScriptsOrCheapStaysRedEvenWhenNoUnityIsNeeded' {
        # Falsifier: scripts or cheap skipped must stay red even when the
        # scope says no Unity run is needed.
        @(Get-ValidateVerdict (New-Needs 'success' 'false' 'skipped' 'skipped' 'skipped')).Count | Should -BeGreaterThan 0
        @(Get-ValidateVerdict (New-Needs 'success' 'false' 'skipped' 'skipped' 'success' 'failure')).Count | Should -BeGreaterThan 0
    }

    It 'ASkippedUnityWhileScopeNeedsItIsRedEvenWhenTheForkNoticeFailed' {
        @(Get-ValidateVerdict (New-Needs 'success' 'true' 'skipped' 'failure')).Count | Should -BeGreaterThan 0
    }

    It 'ARanUnityWhileScopeSaysNoneIsNeededIsRed' {
        @(Get-ValidateVerdict (New-Needs 'success' 'false' 'success' 'skipped')).Count | Should -BeGreaterThan 0
    }
}
