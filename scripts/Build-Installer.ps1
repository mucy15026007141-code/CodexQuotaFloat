[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $projectRoot 'CodexQuotaFloat.slnx'
$project = Join-Path $projectRoot 'src\CodexQuotaFloat\CodexQuotaFloat.csproj'
$testProject = Join-Path $projectRoot 'tests\CodexQuotaFloat.Tests\CodexQuotaFloat.Tests.csproj'
$release = Join-Path $projectRoot 'release\CodexQuotaFloat-1.3.0-win-x64'
$dist = Join-Path $projectRoot 'dist'
$installer = Join-Path $projectRoot 'installer\CodexQuotaFloat.iss'
$package = Join-Path $dist 'CodexQuotaFloat-Setup-1.3.0-win-x64.exe'

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { throw '未找到 Git。' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '未找到 .NET SDK。' }
$status = git -C $projectRoot status --short
if ($status) { Write-Warning "Git 工作区包含未提交的变更：`n$status" }

& dotnet test $testProject -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Release 测试失败。' }

if (Test-Path $release) { Remove-Item -LiteralPath $release -Recurse -Force }
& dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $release
if ($LASTEXITCODE -ne 0) { throw '自包含发布失败。' }
if (-not (Test-Path (Join-Path $release 'CodexQuotaFloat.exe'))) { throw '未找到正式 EXE。' }

$iscc = @('C:\Program Files (x86)\Inno Setup 6\ISCC.exe', 'C:\Program Files\Inno Setup 6\ISCC.exe', (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw '未找到 Inno Setup 6 的 ISCC.exe。请安装 JRSoftware.InnoSetup 后重试。' }
New-Item -ItemType Directory -Force -Path $dist | Out-Null
& $iscc $installer
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup 编译失败。' }
if (-not (Test-Path $package)) { throw "未找到安装包：$package" }

$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
"$hash  $(Split-Path -Leaf $package)" | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ascii
Write-Output "安装包: $package"
Write-Output "SHA-256: $hash"
