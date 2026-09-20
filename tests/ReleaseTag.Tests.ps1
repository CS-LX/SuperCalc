$ErrorActionPreference = 'Stop'
$validator = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../scripts/validate-release-tag.ps1'))
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('SuperCalc-tag-tests-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $testRoot | Out-Null
Push-Location $testRoot
function GitCommand([string[]]$Arguments) {
    git @Arguments 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Git fixture failed: $Arguments" }
}
function RejectTag([string]$Tag) {
    $rejected = $false
    try { & $validator -Tag $Tag -MainRef refs/heads/main } catch { $rejected = $true }
    if (!$rejected) { throw "Incorrectly accepted $Tag" }
    Write-Host "PASS rejects $Tag"
}
try {
    GitCommand @('init', '--initial-branch=main')
    GitCommand @('config', 'user.name', 'Release guard test')
    GitCommand @('config', 'user.email', 'test@example.invalid')
    GitCommand @('commit', '--allow-empty', '-m', 'main commit')
    GitCommand @('tag', '-a', 'v1.2.3.4', '-m', 'annotated version')
    & $validator -Tag v1.2.3.4 -MainRef refs/heads/main
    Write-Host 'PASS accepts annotated main tag'
    RejectTag v1.2.3
    RejectTag v1.2.3.4-preview
    RejectTag v01.2.3.4
    RejectTag v1.2.3.65535
    GitCommand @('switch', '-c', 'feature/test')
    GitCommand @('commit', '--allow-empty', '-m', 'unmerged change')
    GitCommand @('tag', 'v1.2.3.5')
    RejectTag v1.2.3.5
    RejectTag v1.2.3.4
    GitCommand @('switch', 'main')
    GitCommand @('commit', '--allow-empty', '-m', 'later main commit')
    GitCommand @('checkout', 'v1.2.3.4')
    & $validator -Tag v1.2.3.4 -MainRef refs/heads/main
    Write-Host 'PASS accepts a tagged ancestor of main'
} finally {
    Pop-Location
    $resolvedTest = [IO.Path]::GetFullPath($testRoot)
    $allowedRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    if (!$resolvedTest.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolvedTest -Leaf) -notlike 'SuperCalc-tag-tests-*') { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolvedTest -Recurse -Force
}
