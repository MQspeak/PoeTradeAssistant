[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$RuntimeIdentifier = 'win-x64',
    [switch]$KeepExistingPublishOutput,
    [switch]$SkipLaunch
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $projectRoot 'PoeTradeAssistant.sln'
$applicationProjectPath = Join-Path $projectRoot 'src\Poe2MarketScanner.App\Poe2MarketScanner.App.csproj'
$artifactsRoot = Join-Path $projectRoot 'artifacts'
$publishDirectory = Join-Path $artifactsRoot (Join-Path 'publish' $RuntimeIdentifier)
$executableName = 'PoeTradeAssistant.exe'

function Find-CompatibleDotNet {
    $candidates = @()
    if ($env:DOTNET_ROOT) {
        $candidates += Join-Path $env:DOTNET_ROOT 'dotnet.exe'
    }
    $candidates += @(Get-Command dotnet.exe -All -ErrorAction SilentlyContinue | ForEach-Object { $_.Source })
    $candidates += Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'
    $candidates += Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
    $candidates += Join-Path $env:TEMP 'codex-dotnet10-sdk\dotnet.exe'

    # Resolve each SDK from the project directory so global.json is enforced.
    Push-Location $projectRoot
    try {
        foreach ($candidate in ($candidates | Select-Object -Unique)) {
            if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { continue }
            $previousErrorActionPreference = $ErrorActionPreference
            try {
                $ErrorActionPreference = 'Continue'
                $sdkVersion = & $candidate --version 2>$null
                $sdkExitCode = $LASTEXITCODE
            }
            finally {
                $ErrorActionPreference = $previousErrorActionPreference
            }
            if ($sdkExitCode -eq 0) {
                Write-Host "Using .NET SDK $sdkVersion ($candidate)" -ForegroundColor Cyan
                return $candidate
            }
        }
    }
    finally {
        Pop-Location
    }

    $requiredVersion = (Get-Content -LiteralPath (Join-Path $projectRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
    throw "No compatible .NET SDK was found. Install .NET SDK $requiredVersion (or a version allowed by global.json), or set DOTNET_ROOT to its directory. The .NET runtime alone is not sufficient."
}

function Invoke-DotNet {
    param([string[]]$Arguments)

    Push-Location $projectRoot
    try {
        & $dotnetPath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

if (-not (Test-Path -LiteralPath $solutionPath -PathType Leaf)) {
    throw "Solution file was not found: $solutionPath"
}

if (-not (Test-Path -LiteralPath $applicationProjectPath -PathType Leaf)) {
    throw "Application project was not found: $applicationProjectPath"
}

$dotnetPath = Find-CompatibleDotNet

New-Item -ItemType Directory -Force -Path $artifactsRoot | Out-Null

$runningProcess = Get-Process -Name ([IO.Path]::GetFileNameWithoutExtension($executableName)) -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq (Join-Path $publishDirectory $executableName) }
if ($runningProcess) {
    throw "Close the running ${executableName} process (PID: $($runningProcess.Id -join ', ')) before publishing."
}

if ((Test-Path -LiteralPath $publishDirectory) -and -not $KeepExistingPublishOutput) {
    $resolvedPublishDirectory = [IO.Path]::GetFullPath($publishDirectory)
    $expectedPublishDirectory = [IO.Path]::GetFullPath((Join-Path $projectRoot "artifacts\publish\$RuntimeIdentifier"))
    if ($resolvedPublishDirectory -ne $expectedPublishDirectory -or
        (Get-Item -LiteralPath $publishDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Refusing to clean an unexpected publish directory: $publishDirectory"
    }
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
    '-p:PlaywrightPlatform=win',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
)

$executablePath = Join-Path $publishDirectory $executableName
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Publish completed but the executable was not found: $executablePath"
}

Write-Host 'Verifying the Windows Playwright driver...'
$driverNode = Join-Path $publishDirectory '.playwright/node/win32_x64/node.exe'
if (-not (Test-Path -LiteralPath $driverNode)) {
    throw 'The Playwright driver was not published. Publish is incomplete.'
}

Write-Host ''
Write-Host 'Publish completed successfully.' -ForegroundColor Green
Write-Host "Executable: $executablePath"

if ($SkipLaunch) { return }

Write-Host 'Starting the published application...' -ForegroundColor Cyan
try {
    Start-Process -FilePath $executablePath -WorkingDirectory $publishDirectory -ErrorAction Stop | Out-Null
}
catch {
    Write-Warning "Publish completed, but the application could not be started: $($_.Exception.Message). Start it manually: $executablePath"
}
