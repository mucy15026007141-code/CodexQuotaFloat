[CmdletBinding()]
param([switch]$ForceStopRunning, [switch]$NoLaunch)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $projectRoot 'CodexQuotaFloat.slnx'
$project = Join-Path $projectRoot 'src\CodexQuotaFloat\CodexQuotaFloat.csproj'
$release = Join-Path $projectRoot 'release\CodexQuotaFloat-1.3.0-win-x64'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\CodexQuotaFloat'
$exeName = 'CodexQuotaFloat.exe'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '未找到 .NET SDK。' }
$running = @(Get-Process -Name 'CodexQuotaFloat' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    if (-not $ForceStopRunning) { throw '检测到 CodexQuotaFloat 正在运行。请从托盘退出后重试，或使用 -ForceStopRunning（仅结束本应用）。' }
    $running | Stop-Process -Force
}

& dotnet test $solution -c Release
if ($LASTEXITCODE -ne 0) { throw '测试失败，已取消发布。' }
if (Test-Path $release) { Remove-Item -LiteralPath $release -Recurse -Force }
& dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $release
if ($LASTEXITCODE -ne 0) { throw '发布失败。' }
$publishedExe = Join-Path $release $exeName
if (-not (Test-Path $publishedExe)) { throw "未找到发布产物：$publishedExe" }

New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Get-ChildItem -LiteralPath $release -Force | Copy-Item -Destination $installDir -Recurse -Force
$installedExe = Join-Path $installDir $exeName
$shell = New-Object -ComObject WScript.Shell
function New-AppShortcut([string]$path) { $shortcut = $shell.CreateShortcut($path); $shortcut.TargetPath = $installedExe; $shortcut.WorkingDirectory = $installDir; $shortcut.IconLocation = "$installedExe,0"; $shortcut.Description = 'Codex 额度悬浮窗'; $shortcut.Save() }
New-AppShortcut (Join-Path ([Environment]::GetFolderPath('Desktop')) 'Codex 额度悬浮窗.lnk')
$startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'; New-Item -ItemType Directory -Force -Path $startMenu | Out-Null; New-AppShortcut (Join-Path $startMenu 'Codex 额度悬浮窗.lnk')
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'; New-Item -Path $runKey -Force | Out-Null; Set-ItemProperty -Path $runKey -Name 'CodexQuotaFloat' -Value "`"$installedExe`" --startup"

Write-Output "Release: $release"
Write-Output "Installed: $installedExe"
Write-Output "Desktop shortcut: $([Environment]::GetFolderPath('Desktop'))\Codex 额度悬浮窗.lnk"
Write-Output "Startup: HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CodexQuotaFloat"
if (-not $NoLaunch) { Start-Process -FilePath $installedExe }
