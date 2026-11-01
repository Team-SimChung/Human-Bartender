param(
    [Parameter(Mandatory = $true)][string]$UnityEditor,
    [string]$OutputDirectory,
    [string]$TestFrameworkVersion = '1.6.0'
)
$ErrorActionPreference = 'Stop'
$packageRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw 'Unity Editor executable not found.' }
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('AssetDeleteGuard-' + [guid]::NewGuid().ToString('N'))
}
$fixtureRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $fixtureRoot) { throw 'Use a new output directory. Existing projects are never overwritten.' }
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
New-Item -ItemType Directory -Path (Join-Path $fixtureRoot 'Assets'), (Join-Path $fixtureRoot 'Packages'), (Join-Path $fixtureRoot 'ProjectSettings') | Out-Null
$editorVersion = ((Get-Item -LiteralPath $UnityEditor).VersionInfo.ProductVersion -split '_')[0]
if ($editorVersion -notmatch '^\d+\.\d+\.\d+[abfp]\d+$') { throw 'Cannot determine Unity version from executable.' }
Set-Content -LiteralPath (Join-Path $fixtureRoot 'ProjectSettings/ProjectVersion.txt') -Value ("m_EditorVersion: " + $editorVersion) -Encoding UTF8
Set-Content -LiteralPath (Join-Path $fixtureRoot '.asset-delete-guard-fixture') -Value 'Disposable test project' -Encoding UTF8
$manifest = @{
    dependencies = @{
        'com.assetdeleteguard.editor' = 'file:' + $packageRoot.Replace('\', '/')
        'com.unity.test-framework' = $TestFrameworkVersion
        'com.unity.modules.imgui' = '1.0.0'
        'com.unity.modules.jsonserialize' = '1.0.0'
    }
    testables = @('com.assetdeleteguard.editor')
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $fixtureRoot 'Packages/manifest.json') -Encoding UTF8
$logPath = Join-Path $fixtureRoot 'editor.log'
$resultsPath = Join-Path $fixtureRoot 'results.xml'
Write-Output "Fixture: $fixtureRoot"
$unityArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $fixtureRoot + '"'),
    '-runTests', '-testPlatform', 'EditMode', '-assemblyNames', 'AssetDeleteGuard.Editor.Tests',
    '-testResults', ('"' + $resultsPath + '"'), '-logFile', ('"' + $logPath + '"'))
$unityProcess = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -PassThru -Wait
$unityExit = $unityProcess.ExitCode
if (-not (Test-Path -LiteralPath $resultsPath)) { throw "No test results. Unity exit: $unityExit. Inspect $logPath" }
[xml]$results = Get-Content -LiteralPath $resultsPath -Raw
Write-Output ("Result: " + $results.'test-run'.result + "; total=" + $results.'test-run'.total + "; passed=" + $results.'test-run'.passed + "; failed=" + $results.'test-run'.failed)
if ($unityExit -ne 0 -or $results.'test-run'.result -ne 'Passed') { throw "Unity tests failed. Inspect $resultsPath and $logPath" }
