param(
    [Parameter(Mandatory = $true)][string]$UnityEditor,
    [Parameter(Mandatory = $true)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
$source = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) { throw 'Unity Editor executable not found.' }
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory. Existing files are never overwritten.' }
$tarCommand = Get-Command tar -ErrorAction Stop
$version = ((Get-Item -LiteralPath $UnityEditor).VersionInfo.ProductVersion -split '_')[0]
if ($version -notmatch '^\d+\.\d+\.\d+[abfp]\d+$') { throw 'Cannot determine Unity version.' }
$manifest = Get-Content -LiteralPath (Join-Path $source 'package.json') -Raw | ConvertFrom-Json
$project = Join-Path $output 'Project'
$content = Join-Path $project 'Assets/AssetDeleteGuard'
$helper = Join-Path $project 'Assets/PreviewBuilder/Editor'
$upmParent = Join-Path $output 'UPM'
$upm = Join-Path $upmParent 'package'
New-Item -ItemType Directory -Path $content, $helper, $upm, (Join-Path $project 'Packages'), (Join-Path $project 'ProjectSettings') | Out-Null
Set-Content -LiteralPath (Join-Path $project '.asset-delete-guard-preview') -Value 'Isolated preview project' -Encoding UTF8
Set-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Value ("m_EditorVersion: " + $version) -Encoding UTF8
@{ dependencies = @{ 'com.unity.modules.imgui' = '1.0.0'; 'com.unity.modules.jsonserialize' = '1.0.0' } } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'Packages/manifest.json') -Encoding UTF8

# Explicit allowlists keep test projects, game assets, developer tools and publisher drafts out of previews.
foreach ($name in @('Editor', 'README.md', 'CHANGELOG.md', 'LICENSE.md', 'Third-Party Notices.txt')) {
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination $content -Recurse
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination $upm -Recurse
    $meta = Join-Path $source ($name + '.meta')
    if (Test-Path -LiteralPath $meta) {
        Copy-Item -LiteralPath $meta -Destination $content
        Copy-Item -LiteralPath $meta -Destination $upm
    }
}
Copy-Item -LiteralPath (Join-Path $source 'Documentation~') -Destination (Join-Path $content 'Documentation') -Recurse
Copy-Item -LiteralPath (Join-Path $source 'Samples~') -Destination (Join-Path $content 'Samples') -Recurse
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ExportMetadata/AssetDeleteGuard.meta') -Destination ($content + '.meta')
foreach ($name in @('Documentation', 'Samples')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('ExportMetadata/' + $name + '.meta')) -Destination (Join-Path $content ($name + '.meta'))
}
foreach ($name in @('Documentation~', 'Samples~', 'package.json', 'package.json.meta')) {
    Copy-Item -LiteralPath (Join-Path $source $name) -Destination $upm -Recurse
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PreviewBuilder.cs') -Destination $helper
@{ name = 'AssetDeleteGuard.Development.Editor'; references = @('AssetDeleteGuard.Core', 'AssetDeleteGuard.Editor'); includePlatforms = @('Editor'); autoReferenced = $true } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $helper 'AssetDeleteGuard.Development.Editor.asmdef') -Encoding UTF8

$archive = Join-Path $output ('AssetDeleteGuard-' + $manifest.version + '.unitypackage')
$tarball = Join-Path $output ($manifest.name + '-' + $manifest.version + '.tgz')
$logPath = Join-Path $project 'build.log'
$unityArguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $project + '"'),
    '-executeMethod', 'AssetDeleteGuard.Development.PreviewBuilder.VerifyAndExport',
    '-adgExportPath', ('"' + $archive + '"'), '-logFile', ('"' + $logPath + '"'))
Write-Output "Staging project: $project"
$process = Start-Process -FilePath $UnityEditor -ArgumentList $unityArguments -WindowStyle Hidden -PassThru -Wait
if ($process.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $archive) -or
    -not (Test-Path -LiteralPath (Join-Path $project 'preview-verification.json'))) {
    throw "Unity preview export failed. Inspect $logPath"
}
if (Select-String -LiteralPath $logPath -Pattern '(error|warning) CS\d+' -Quiet) {
    throw "Preview has C# compiler diagnostics. Inspect $logPath"
}
& $tarCommand.Source -czf $tarball -C $upmParent package
if ($LASTEXITCODE -ne 0) { throw 'UPM tarball creation failed.' }
Get-FileHash -LiteralPath $archive, $tarball -Algorithm SHA256 |
    Select-Object @{Name='File';Expression={[System.IO.Path]::GetFileName($_.Path)}}, Hash |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'SHA256.json') -Encoding UTF8
Write-Output "Preview archive: $archive"
Write-Output "Local UPM tarball: $tarball"
Write-Output 'No upload or marketplace submission was performed.'
