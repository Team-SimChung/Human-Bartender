param(
    [Parameter(Mandatory = $true)] [string]$ReleasePath,
    [string]$ConfigPath,
    [switch]$ForUpload
)

$ErrorActionPreference = 'Stop'
if (-not $ConfigPath) { $ConfigPath = Join-Path $PSScriptRoot 'steam-demo.json' }
$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
[uint32]$DemoAppId = 0
[uint32]$WindowsDepotId = 0
if (-not [uint32]::TryParse([string]$config.demoAppId, [ref]$DemoAppId) -or $DemoAppId -eq 0) {
    throw 'Set the actual demoAppId in steam-demo.json.'
}
if (-not [uint32]::TryParse([string]$config.windowsDepotId, [ref]$WindowsDepotId) -or $WindowsDepotId -eq 0) {
    throw 'Set the actual Windows Depot ID in steam-demo.json. Find it in Steamworks -> SteamPipe -> Depots.'
}
$releaseRoot = (Resolve-Path -LiteralPath $ReleasePath).Path
$infoPath = Join-Path $releaseRoot 'build-info.json'
if (-not (Test-Path -LiteralPath $infoPath -PathType Leaf)) {
    throw 'A successful build-info.json is required. Run Build-Windows.ps1 first.'
}
$info = Get-Content -LiteralPath $infoPath -Raw | ConvertFrom-Json
$content = Join-Path $releaseRoot 'content'
if ($info.target -ne 'StandaloneWindows64' -or $info.options -ne 'None' -or $info.errors -ne 0) {
    throw 'Expected a successful Windows64 release build.'
}
if (-not (Test-Path -LiteralPath (Join-Path $content $info.executable) -PathType Leaf)) {
    throw 'The executable recorded in build-info.json is missing.'
}
$forbidden = Get-ChildItem -LiteralPath $content -Recurse -File -Filter 'steam_appid.txt'
if ($forbidden) { throw 'Remove development-only steam_appid.txt before upload.' }

$uploadRoot = Join-Path $releaseRoot 'steam-upload'
New-Item -ItemType Directory -Path $uploadRoot -Force | Out-Null
$cache = Join-Path $uploadRoot 'cache'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
$contentVdf = $content.Replace('\', '/')
$cacheVdf = $cache.Replace('\', '/')
$preview = if ($ForUpload) { '0' } else { '1' }
$description = "Windows demo $($info.sourceCommit.Substring(0, 12)) $($info.createdUtc)"
$vdf = @"
"AppBuild"
{
    "AppID" "$DemoAppId"
    "Desc" "$description"
    "ContentRoot" "$contentVdf"
    "BuildOutput" "$cacheVdf"
    "Preview" "$preview"
    "SetLive" ""
    "Depots"
    {
        "$WindowsDepotId"
        {
            "FileMapping"
            {
                "LocalPath" "*"
                "DepotPath" "."
                "Recursive" "1"
            }
            "FileExclusion" "steam_appid.txt"
            "FileExclusion" "*.pdb"
            "FileExclusion" "*/.idea/*"
            "FileExclusion" "*/.vscode/*"
        }
    }
}
"@
$vdfName = if ($ForUpload) { 'app_build_upload.vdf' } else { 'app_build_preview.vdf' }
$vdfPath = Join-Path $uploadRoot $vdfName
[IO.File]::WriteAllText($vdfPath, $vdf, (New-Object Text.UTF8Encoding($false)))
Write-Output "SteamPipe configuration: $vdfPath"
Write-Output "Demo App ID: $DemoAppId; Windows Depot ID: $WindowsDepotId; Preview: $preview"
Write-Output "Launch Option executable: $($info.executable)"
Write-Output 'SetLive is empty. Upload does not select a live branch.'
Write-Output 'Run SteamCMD interactively, log in with your build account, then run:'
Write-Output ('run_app_build "' + $vdfPath + '"')
