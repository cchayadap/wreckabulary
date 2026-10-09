#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$UnityEditorPath = $env:UNITY_EDITOR_PATH,
    [string]$ResultsRoot = $env:WRECK_VERIFY_RESULTS,
    [switch]$Build
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:FailureExitCode = 1
$editorVersion = '6000.6.3f1'
$repoRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path

function Invoke-Unity {
    param([string]$LogName, [string[]]$UnityArguments)
    $logPath = Join-Path $results $LogName
    $arguments = @('-batchmode', '-nographics', '-projectPath', $repoRoot) +
        $UnityArguments + @('-logFile', $logPath)
    $commandLine = ($arguments | ForEach-Object {
        '"' + ($_ -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
    }) -join ' '
    $process = Start-Process -FilePath $UnityEditorPath -ArgumentList $commandLine `
        -WorkingDirectory $repoRoot -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        $script:FailureExitCode = $process.ExitCode
        $hint = if ($process.ExitCode -eq 198) {
            ' Activate Unity Personal through Unity Hub on this machine, then retry.'
        } else { '' }
        throw "Unity exited $($process.ExitCode). See $logPath.$hint"
    }
    if (-not (Test-Path -LiteralPath $logPath -PathType Leaf) -or
        (Get-Item -LiteralPath $logPath).Length -eq 0) {
        throw "Unity did not create a current, nonempty log: $logPath"
    }
}

function Assert-TestReport {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Unity did not create current test results: $Path"
    }
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [System.Xml.XmlReader]::Create($Path, $settings)
    $document = New-Object System.Xml.XmlDocument
    try { $document.Load($reader) } finally { $reader.Dispose() }
    $root = $document.DocumentElement
    if ($null -eq $root -or $root.Name -ne 'test-run') {
        throw "Expected a Unity NUnit test-run report: $Path"
    }
    $counts = @{}
    foreach ($name in @('total', 'passed', 'failed')) {
        $value = 0
        if (-not [int]::TryParse($root.GetAttribute($name), [ref]$value) -or $value -lt 0) {
            throw "Invalid '$name' test count: $Path"
        }
        $counts[$name] = $value
    }
    $cases = @($root.SelectNodes('.//test-case'))
    $passedCases = @($cases | Where-Object { $_.GetAttribute('result') -in @('Passed', 'Success') }).Count
    $failedCases = @($cases | Where-Object { $_.GetAttribute('result') -eq 'Failed' }).Count
    if ($counts.total -le 0 -or $counts.passed -le 0 -or $counts.failed -ne 0 -or
        $root.GetAttribute('result') -notin @('Passed', 'Success') -or
        $cases.Count -ne $counts.total -or $passedCases -ne $counts.passed -or
        $failedCases -ne $counts.failed) {
        throw "Tests did not pass with nonzero executed cases: $Path (total=$($counts.total), passed=$($counts.passed), failed=$($counts.failed), result=$($root.GetAttribute('result')))"
    }
    Write-Host "$([IO.Path]::GetFileName($Path)): total=$($counts.total), passed=$($counts.passed), failed=$($counts.failed), other=$($counts.total - $counts.passed - $counts.failed)"
}

try {
    $projectVersion = Get-Content -LiteralPath (Join-Path $repoRoot 'ProjectSettings/ProjectVersion.txt') -Raw
    if ($projectVersion -notmatch "(?m)^m_EditorVersion: $([regex]::Escape($editorVersion))\s*$") {
        throw "The project must be pinned to Unity $editorVersion."
    }
    if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) {
        if ([string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
            throw 'Set -UnityEditorPath or UNITY_EDITOR_PATH to the activated Windows Unity.exe.'
        }
        $UnityEditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$editorVersion/Editor/Unity.exe"
    }
    if (-not (Test-Path -LiteralPath $UnityEditorPath -PathType Leaf)) {
        throw "Unity $editorVersion is required. Set -UnityEditorPath or UNITY_EDITOR_PATH to its Unity.exe."
    }
    $UnityEditorPath = (Resolve-Path -LiteralPath $UnityEditorPath).Path
    $productVersion = (Get-Item -LiteralPath $UnityEditorPath).VersionInfo.ProductVersion
    if ($productVersion -notmatch "^$([regex]::Escape($editorVersion))(?=$|[^0-9A-Za-z])") {
        throw "Expected Unity $editorVersion; executable product version is '$productVersion'."
    }
    if ([string]::IsNullOrWhiteSpace($ResultsRoot)) {
        $ResultsRoot = Join-Path $repoRoot 'Logs/Verification'
    }
    $ResultsRoot = [IO.Path]::GetFullPath($ResultsRoot)
    $results = Join-Path $ResultsRoot ('run.' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $results | Out-Null
    Write-Host "Current Unity verification results: $results"

    Invoke-Unity 'setup.log' @('-quit', '-executeMethod', 'Wreckabulary.EditorTools.ProjectSetup.Run')
    foreach ($platform in @('EditMode', 'PlayMode')) {
        $reportPath = Join-Path $results "$platform.xml"
        Invoke-Unity "$platform.log" @('-runTests', '-testPlatform', $platform, '-testResults', $reportPath)
        Assert-TestReport $reportPath
    }
    if ($Build) {
        foreach ($target in @('Windows', 'Web')) {
            Invoke-Unity "build-$target.log" @('-quit', '-executeMethod', "Wreckabulary.EditorTools.ProductionBuild.$target")
            $buildTarget = if ($target -eq 'Windows') { 'StandaloneWindows64' } else { 'WebGL' }
            $buildLog = Get-Content -LiteralPath (Join-Path $results "build-$target.log") -Raw
            if ($buildLog -notmatch "PRODUCTION_BUILD target=$buildTarget result=Succeeded errors=0 bytes=[1-9][0-9]*(?=\s|$)") {
                throw "No successful current $target build report. See $(Join-Path $results "build-$target.log")"
            }
            $artifact = if ($target -eq 'Windows') { 'Builds/Windows/Wreckabulary.exe' } else { 'Builds/UnityWeb/index.html' }
            $artifactPath = Join-Path $repoRoot $artifact
            if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf) -or
                (Get-Item -LiteralPath $artifactPath).Length -eq 0) {
                throw "Build output is missing or empty: $artifactPath"
            }
        }
    }
    Write-Host "Unity verification passed. Results: $results"
    exit 0
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit $script:FailureExitCode
}
