param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    dotnet run --project tests/SuperCalc.Tests -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
    if ($Publish) {
        dotnet publish src/SuperCalc.App/SuperCalc.App.csproj -c Release -p:Platform=x64 -o artifacts/SuperCalc-win-x64
    } else {
        dotnet build src/SuperCalc.App/SuperCalc.App.csproj -c Release -p:Platform=x64
    }
    if ($LASTEXITCODE -ne 0) { throw 'App build failed.' }
} finally { Pop-Location }
