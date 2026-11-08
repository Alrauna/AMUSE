# Common helpers for the CI scripts. Dot-source this file first. The file has
# no parameters and runs no code on load.
#
# Print rules of the gate: a script prints counters and redacted allowlisted
# lines only. It never prints a raw log, an email, a password or a serial.
# Every printed string that can hold text from a test, a vendor or the Editor
# passes through Redact first.

# Writes a line to the console. When the step summary file is set, the line
# goes there too. Nothing is written when the variable is unset.
function Say([string]$text) {
    Write-Host $text
    if ($env:GITHUB_STEP_SUMMARY) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $text }
}

# Last line of defense for the few lines that are printed. Two colons are split
# so that no printed line can act as a workflow command. Serial shapes, email
# addresses, GUID shapes, long hex runs and long token shapes are replaced.
function Redact([string]$line) {
    $line = $line.Replace('::', ': :')
    $line = [regex]::Replace($line, '\b[A-Z0-9]{2}(-[A-Z0-9]{4}){4,5}\b', '<serial>')
    $line = [regex]::Replace($line, '[\w.+-]+@[\w-]+(\.[\w-]+)+', '<email>')
    $line = [regex]::Replace($line, '[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}', '<guid>')
    $line = [regex]::Replace($line, '[0-9a-fA-F]{24,}', '<hex>')
    $line = [regex]::Replace($line, '[A-Za-z0-9+/=_-]{40,}', '<token>')
    return $line
}

# Checks the Unity account values before any process starts. Both values are
# trimmed first. An empty value, an email outside the plain shape, or a
# password with a quote or whitespace stops the run. Returns the trimmed pair.
function Test-UnityCredential([string]$email, [string]$password) {
    $email = ([string]$email).Trim()
    $password = ([string]$password).Trim()
    if ([string]::IsNullOrWhiteSpace($email) -or [string]::IsNullOrWhiteSpace($password)) {
        throw 'The Unity account email or password is empty.'
    }
    if ($email -notmatch '^[A-Za-z0-9._+-]+@[A-Za-z0-9.-]+$') {
        throw 'The Unity account email has an unexpected shape.'
    }
    # A quote or a space would end the quoted argument early, so stop on those.
    if ($password -match '["\s]') {
        throw 'The Unity account password must not hold a quote or a space.'
    }
    return @{
        Email = $email
        Password = $password
    }
}

# Builds the argument string for the licensing client. The email and the
# password sit in double quotes. A backslash at the end of the password would
# escape the closing quote, so every trailing backslash is doubled.
function ConvertTo-LicensingArguments([string]$email, [string]$password) {
    $quotedPassword = [regex]::Replace($password, '(\\+)$', '$1$1')
    return "--activate-ulf --username `"$email`" --password `"$quotedPassword`" --include-personal"
}

# Reads a NUnit 3 result file. The root element is test-run. Returns the
# counters, the full names of the failed tests, and the leaf names of the
# assembly suites without the .dll suffix. Unity can write the assembly
# suite name as a full path with forward or back separators, so only the
# part after the last separator stays. Throws when the file is missing.
function Read-NUnitResult([string]$xmlPath) {
    if (-not (Test-Path $xmlPath)) { throw 'The result file is missing.' }
    [xml]$xml = Get-Content -Path $xmlPath -Raw
    $run = $xml.'test-run'
    $failed = @($xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object { $_.fullname })
    $assemblies = @($xml.SelectNodes("//test-suite[@type='Assembly']") | ForEach-Object {
        (([string]$_.name) -replace '^.*[/\\]', '') -replace '\.dll$', ''
    })
    return [pscustomobject]@{
        Total = [int]$run.total
        Passed = [int]$run.passed
        Failed = [int]$run.failed
        Skipped = [int]$run.skipped
        Inconclusive = [int]$run.inconclusive
        FailedNames = $failed
        Assemblies = $assemblies
    }
}

# Builds the printed line for one failed test. The full name passes through
# Redact, so a name with a double colon can never act as a workflow command.
function Format-FailedTestLine([string]$fullName) {
    return Redact "  - FAILED $fullName"
}

# Fails closed on a run that proves nothing. Zero tests, an assembly whose
# leaf name is not in the result file, any failed test, any inconclusive
# test, zero passed tests, or a non-zero Editor exit code stops the run.
function Assert-TestRun($result, [string]$assembly, [int]$unityExit) {
    if ($result.Total -lt 1) { throw 'The test run has zero tests.' }
    if (@($result.Assemblies) -notcontains $assembly) { throw "The assembly is not in the result file: $assembly" }
    if ($result.Failed -gt 0) { throw "The test run has failed tests: $($result.Failed)" }
    if ($result.Inconclusive -gt 0) { throw "The test run has inconclusive tests: $($result.Inconclusive)" }
    if ($result.Passed -lt 1) { throw 'The test run has no passed test.' }
    if ($unityExit -ne 0) { throw "The Unity exit code was not zero: $unityExit" }
}

# Counts the allowlisted shapes of one Unity log without printing the log.
# The regexes are the exact regexes of the spike runs. The lines about
# licensing count on their own. A licensing client signature line of the
# benign kind holds none of the shader or compile shapes, so it never counts
# there. The function throws when the log is missing, because a run without
# a log proves nothing.
function Get-LogFacts([string]$logPath) {
    if (-not (Test-Path -LiteralPath $logPath)) { throw 'The Unity log is missing.' }
    $lines = @(Get-Content -LiteralPath $logPath)
    $shader = @($lines | Where-Object { $_ -match 'Shader error in' })
    $compile = @($lines | Where-Object { $_ -match 'error CS\d+|Scripts have compiler errors|Compilation failed' })
    $licensing = @($lines | Where-Object { $_ -match 'icens' })
    return @{
        Shader = $shader.Count
        Compile = $compile.Count
        Licensing = $licensing.Count
    }
}

# Prints the counters and the failed test names of one run and then fails
# closed. The counters print before every check, so a failed run still
# reports its numbers. The log check runs after the run checks, so shader
# and compile errors fail the run even when every test passed. A missing
# result file, zero tests, a missing assembly, a failed test, an
# inconclusive test or a non-zero Editor exit code stops the run through
# Assert-TestRun.
function Complete-TestRun([string]$xmlPath, [string]$assembly, [int]$unityExit, $logFacts) {
    $result = Read-NUnitResult $xmlPath
    Say "- tests: total $($result.Total), passed $($result.Passed), failed $($result.Failed), skipped $($result.Skipped), inconclusive $($result.Inconclusive)"
    @($result.FailedNames) | Select-Object -First 40 | ForEach-Object { Say (Format-FailedTestLine $_) }
    Assert-TestRun $result $assembly $unityExit
    if ($logFacts.Shader -gt 0 -or $logFacts.Compile -gt 0) { throw 'The log holds shader or compile errors.' }
}
