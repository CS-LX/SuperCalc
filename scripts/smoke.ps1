param([string]$OutputDirectory = '', [string]$ApplicationPath = '')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'artifacts/qa' }
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$appPath = if ($ApplicationPath) { [IO.Path]::GetFullPath($ApplicationPath) } else { Join-Path $projectRoot 'artifacts/SuperCalc-win-x64/SuperCalc.App.exe' }
if (!(Test-Path -LiteralPath $appPath)) { throw 'Run ./scripts/build.ps1 -Publish first.' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$reportPath = Join-Path $OutputDirectory 'results.json'
if (Test-Path -LiteralPath $reportPath) { Remove-Item -LiteralPath $reportPath }
# This is an explicitly requested interactive test window, not a background helper.
$testProcess = Start-Process -FilePath $appPath -ArgumentList ('--smoke-test "' + $OutputDirectory + '"') -PassThru
if (!$testProcess.WaitForExit(45000)) { throw 'Smoke test timed out; inspect the test window.' }
if ($testProcess.ExitCode -ne 0) { throw "Smoke process failed with exit code $($testProcess.ExitCode)." }
if (!(Test-Path -LiteralPath $reportPath)) { throw 'App exited without a smoke-test report.' }
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$report.checks | ForEach-Object { Write-Host $_ }
if (!$report.success) { throw $report.error }
Write-Host "All app integration checks passed. Screenshots: $OutputDirectory"
