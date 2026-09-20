param([Parameter(Mandatory)][string]$Tag, [string]$MainRef = 'refs/remotes/origin/main')
$ErrorActionPreference = 'Stop'
if ($Tag -cnotmatch '^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw 'Expected vX.X.X.X with four numeric components and no leading zeroes.'
}
foreach ($part in $Tag.Substring(1).Split('.')) {
    if ([decimal]$part -gt 65534) { throw 'Version components must be between 0 and 65534.' }
}
$tagCommit = git rev-parse --verify "$Tag^{commit}"
if ($LASTEXITCODE -ne 0) { throw 'The release tag must already exist.' }
$headCommit = git rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $headCommit -ne $tagCommit) { throw 'Checkout must match the tagged commit.' }
git merge-base --is-ancestor $tagCommit $MainRef
if ($LASTEXITCODE -ne 0) { throw 'The tagged commit must belong to main history.' }
Write-Host "Validated $Tag at $tagCommit on main."
