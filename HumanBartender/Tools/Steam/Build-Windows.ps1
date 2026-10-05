param(
    [string]$UnityPath
)

$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$versionFile = Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt'
$version = ((Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion: ' }) -replace '^m_EditorVersion: ', '').Trim()
if (-not $UnityPath) {
    $UnityPath = Join-Path ${env:ProgramFiles} "Unity/Hub/Editor/$version/Editor/Unity.exe"
}
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity $version was not found. Pass -UnityPath with its executable path."
}
if ((Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion -notlike "$version*") {
    throw "Use Unity $version, matching ProjectVersion.txt."
}
$openEditor = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Contains($projectPath)
}
if ($openEditor) {
    throw 'This project is already open in Unity. Close it before running the batch build.'
}

$commit = & git -C $projectPath rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the source commit.' }
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$releaseRoot = Join-Path $projectPath "Builds/SteamDemo/$stamp-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
New-Item -ItemType Directory -Path $releaseRoot | Out-Null
$logPath = Join-Path $releaseRoot 'unity-build.log'
$sourceStatus = @(& git -C $projectPath status --short)
if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the working tree state.' }
$sourceStatus | Set-Content -LiteralPath (Join-Path $releaseRoot 'source-status.txt') -Encoding UTF8

# Quotes preserve paths with spaces; input quotes are invalid in Windows paths.
$arguments = @(
    '-batchmode', '-quit', '-buildTarget', 'Win64',
    '-projectPath', ('"' + $projectPath + '"'),
    '-executeMethod', 'SteamDemoBuild.BuildWindows',
    '-releaseOutput', ('"' + $releaseRoot + '"'),
    '-sourceCommit', $commit.Trim(),
    '-logFile', ('"' + $logPath + '"')
)
Write-Output "Building Windows64 with Unity $version"
Write-Output "Release folder: $releaseRoot"
Write-Output "Unity log: $logPath"
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
$process.WaitForExit()
if ($process.ExitCode -ne 0) {
    throw "Unity build failed (exit $($process.ExitCode)). Log: $logPath"
}
$infoPath = Join-Path $releaseRoot 'build-info.json'
if (-not (Test-Path -LiteralPath $infoPath -PathType Leaf)) {
    throw "Unity did not produce a successful build report. Log: $logPath"
}
Write-Output "Build and artifact checks passed. Upload content from: $(Join-Path $releaseRoot 'content')"
Write-Output "Build report: $infoPath"
