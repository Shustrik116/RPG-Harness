param(
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0',
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot 'RPG_Harness.csproj'
$publishDir = Join-Path $projectRoot 'artifacts\win-x64'
$installerDir = Join-Path $projectRoot 'artifacts\installer'
$definition = Join-Path $PSScriptRoot 'RPG_Harness.iss'

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $publishDir `
    -p:PublishProfile=win-x64 `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish завершился с кодом $LASTEXITCODE."
}

Write-Host "Переносимая сборка готова: $publishDir"

if ($SkipInstaller) {
    return
}

$isccCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    throw 'Inno Setup 6 не найден. Установите его или запустите скрипт с -SkipInstaller; GitHub Actions собирает установщик автоматически.'
}

New-Item -ItemType Directory -Path $installerDir -Force | Out-Null
& $iscc "/DMyAppVersion=$Version" $definition
if ($LASTEXITCODE -ne 0) {
    throw "ISCC завершился с кодом $LASTEXITCODE."
}

Write-Host "Установщик готов: $(Join-Path $installerDir 'RPG-Harness-Setup-win-x64.exe')"
