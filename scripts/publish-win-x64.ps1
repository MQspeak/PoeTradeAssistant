[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$KeepExistingPublishOutput
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $projectRoot 'PoeTradeAssistant.sln'
$applicationProjectPath = Join-Path $projectRoot 'src\Poe2MarketScanner.App\Poe2MarketScanner.App.csproj'
$artifactsRoot = Join-Path $projectRoot 'artifacts'
$publishDirectory = Join-Path $artifactsRoot (Join-Path 'publish' $RuntimeIdentifier)
$zipPath = Join-Path $artifactsRoot "PoeTradeAssistant-$RuntimeIdentifier.zip"
$executableName = 'PoeTradeAssistant.exe'

function Invoke-DotNet {
    param([string[]]$Arguments)

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    throw "Solution file was not found: $solutionPath"
}

if (-not (Test-Path -LiteralPath $applicationProjectPath -PathType Leaf)) {
    throw "Application project was not found: $applicationProjectPath"
}

New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null

$runningProcess = Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($executableName)) -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq (Join-Path $publishDirectory $executableName) }
if ($runningProcess) {
    throw "Close the running ${executableName} process (PID: $($runningProcess.Id -join ', ')) before packaging."
}

if ((Test-Path -LiteralPath $publishDirectory) -and -not $KeepExistingPublishOutput) {
    Remove-Item -LiteralPath $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null

Write-Host "Restoring $RuntimeIdentifier packages..." -ForegroundColor Cyan
Invoke-DotNet @('restore', $solutionPath, '--runtime', $RuntimeIdentifier)

Write-Host "Publishing self-contained desktop application..." -ForegroundColor Cyan
Invoke-DotNet @(
    'publish',
    $applicationProjectPath,
    '--configuration', 'Release',
    '--runtime', $RuntimeIdentifier,
    '--self-contained', 'true',
    '--no-restore',
    '--output', $publishDirectory,
    '-p:PublishSingleFile=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

$executablePath = Join-Path $publishDirectory $executableName
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Publish completed but the executable was not found: $executablePath"
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

$archiveCompleted = $false
for ($attempt = 1; $attempt -le 15; $attempt++) {
    try {
        Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -Force -ErrorAction Stop
        $archiveCompleted = $true
        break
    }
    catch {
        if ($attempt -eq 15) {
            throw
        }

        Start-Sleep -Seconds 2
    }
}

if (-not $archiveCompleted) {
    throw 'Distribution ZIP could not be created.'
}

Write-Host ''
Write-Host 'Package created successfully.' -ForegroundColor Green
Write-Host "Executable: $executablePath"
Write-Host "Distribution ZIP: $zipPath"
Write-Host 'Extract the ZIP completely, then start PoeTradeAssistant.exe from the extracted folder.' -ForegroundColor Yellow
