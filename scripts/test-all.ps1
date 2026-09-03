$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    dotnet test .\PoeTradeAssistant.sln
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build .\PoeTradeAssistant.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
