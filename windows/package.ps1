#requires -Version 7.0
param([switch]$SkipBuild, [switch]$RunIntegrationTests)
$ErrorActionPreference = 'Stop'
$repository = Split-Path $PSScriptRoot -Parent
$configuration = 'Release'
$project = Join-Path $PSScriptRoot 'RimeAutomation/RimeAutomation.csproj'
[xml]$metadata = Get-Content $project -Raw
$version = [string]$metadata.Project.PropertyGroup.Version
[xml]$properties = Get-Content (Join-Path $PSScriptRoot 'Directory.Build.props') -Raw
$framework = [string]$properties.Project.PropertyGroup.TargetFramework
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw '项目版本必须是三段数字。' }
if (-not $SkipBuild) {
    dotnet build (Join-Path $PSScriptRoot 'RimeAutomation.sln') -c $configuration --nologo
    if ($LASTEXITCODE) { throw '构建失败。' }
}
if ($RunIntegrationTests) {
    & (Join-Path $PSScriptRoot "RimeAutomation.Tests/bin/$configuration/$framework/RimeAutomation.Tests.exe")
    if ($LASTEXITCODE) { throw '集成验证失败。' }
} else {
    Write-Warning '本次打包不执行集成验证。发布前请在已登录的 Windows 桌面会话中运行测试。'
}
$executable = Join-Path $PSScriptRoot "RimeAutomation/bin/$configuration/$framework/RimeAutomation.exe"
if ((Get-Item $executable).VersionInfo.FileVersion -ne "$version.0") { throw '程序版本与源码不一致，请重新构建。' }
$output = Join-Path $repository 'artifacts'
$staging = Join-Path $output 'package'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
$null = New-Item $staging -ItemType Directory -Force
try {
    Copy-Item $executable (Join-Path $staging 'RimeAutomation.exe')
    Copy-Item (Join-Path $repository 'LICENSE') $staging
    Copy-Item (Join-Path $PSScriptRoot 'README.md') $staging
    $archive = Join-Path $output "RimeAutomation-$version-windows-x64.zip"
    Compress-Archive -LiteralPath @((Join-Path $staging 'RimeAutomation.exe'), (Join-Path $staging 'LICENSE'), (Join-Path $staging 'README.md')) -DestinationPath $archive -Force
    $hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText("$archive.sha256", "$hash  $([IO.Path]::GetFileName($archive))`n", [Text.UTF8Encoding]::new($false))
    Write-Host "Package: $archive"
} finally { Remove-Item $staging -Recurse -Force }
