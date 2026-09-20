param([string]$Version = '', [ValidateSet('all', 'self-contained', 'lightweight')][string]$Variant = 'all', [switch]$ArtifactUpload)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$stagingRoot = $null
Push-Location $projectRoot
try {
    if ($ArtifactUpload -and ($Variant -eq 'all' -or !$env:GITHUB_OUTPUT)) { throw 'Artifact upload requires one variant and GITHUB_OUTPUT.' }
    if ($Version -and $Version -cnotmatch '^(0|[1-9][0-9]*)(\.(0|[1-9][0-9]*)){3}$') { throw 'Expected a four-part numeric version.' }
    if ($Version) {
        foreach ($part in $Version.Split('.')) {
            if ([decimal]$part -gt 65534) { throw 'Version components must be between 0 and 65534.' }
        }
    }
    $commit = git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine package commit.' }
    $shortSha = $commit.Substring(0, 12)
    $outputRoot = Join-Path $projectRoot 'artifacts/packages'
    $stagingRoot = Join-Path $projectRoot ('artifacts/package-staging/' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $outputRoot, $stagingRoot -Force | Out-Null
    $checksums = @()
    $variants = if ($Variant -eq 'all') { @('self-contained', 'lightweight') } else { @($Variant) }
    foreach ($flavor in $variants) {
        $selfContained = ($flavor -eq 'self-contained').ToString().ToLowerInvariant()
        $bootstrap = ($flavor -eq 'lightweight').ToString().ToLowerInvariant()
        $publishDirectory = Join-Path $stagingRoot $flavor
        $publishArgs = @('publish', 'src/SuperCalc.App/SuperCalc.App.csproj', '-c', 'Release', '-p:Platform=x64',
            "-p:SelfContained=$selfContained", "-p:WindowsAppSDKSelfContained=$selfContained", "-p:WindowsAppSdkBootstrapInitialize=$bootstrap",
            '-o', $publishDirectory)
        if ($Version) { $publishArgs += @("-p:Version=$Version", "-p:AssemblyVersion=$Version", "-p:FileVersion=$Version") }
        dotnet @publishArgs
        if ($LASTEXITCODE -ne 0) { throw "Failed to publish $flavor." }
        foreach ($file in @('SuperCalc.App.exe', 'SuperCalc.App.dll', 'SuperCalc.App.pri', 'Assets/SuperCalc.ico')) {
            if (!(Test-Path -LiteralPath (Join-Path $publishDirectory $file))) { throw "Missing $flavor asset: $file" }
        }
        $runtime = Get-Content (Join-Path $publishDirectory 'SuperCalc.App.runtimeconfig.json') -Raw | ConvertFrom-Json
        if ($flavor -eq 'self-contained') {
            if (!$runtime.runtimeOptions.includedFrameworks) { throw 'Self-contained .NET runtime configuration is missing.' }
            foreach ($file in @('coreclr.dll', 'Microsoft.UI.Xaml.dll')) {
                if (!(Test-Path (Join-Path $publishDirectory $file))) { throw "Missing bundled runtime: $file" }
            }
        } else {
            if (!$runtime.runtimeOptions.framework -and !$runtime.runtimeOptions.frameworks) { throw 'Lightweight runtime dependency is missing.' }
            foreach ($file in @('coreclr.dll', 'Microsoft.UI.Xaml.dll')) {
                if (Test-Path (Join-Path $publishDirectory $file)) { throw "Lightweight package unexpectedly includes $file." }
            }
            if (!(Test-Path (Join-Path $publishDirectory 'Microsoft.WindowsAppRuntime.Bootstrap.dll'))) { throw 'Lightweight bootstrapper is missing.' }
        }
        Copy-Item LICENSE (Join-Path $publishDirectory 'LICENSE.txt')
        Copy-Item docs/release.md (Join-Path $publishDirectory 'README.md')
        "Commit: $commit`nVariant: $flavor`nVersion: $Version" | Set-Content (Join-Path $publishDirectory 'BUILD.txt') -Encoding utf8
        $versionPart = if ($Version) { "$Version-" } else { '' }
        $artifactName = "SuperCalc-${versionPart}win-x64-$flavor-$shortSha"
        if ($ArtifactUpload) {
            # upload-artifact creates the downloadable ZIP directly from these files.
            # Keep the staging directory until that step completes; do not nest a ZIP in it.
            "artifact-name=$artifactName" >> $env:GITHUB_OUTPUT
            "artifact-path=$publishDirectory" >> $env:GITHUB_OUTPUT
            Write-Host "Prepared independent artifact $artifactName"
            continue
        }
        $archiveName = "$artifactName.zip"
        $archivePath = Join-Path $outputRoot $archiveName
        Compress-Archive -Path "$publishDirectory/*" -DestinationPath $archivePath -CompressionLevel Optimal -Force
        $digest = (Get-FileHash $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        $checksums += "$digest  $archiveName"
        Write-Host "Packaged $archiveName ($([math]::Round((Get-Item $archivePath).Length / 1MB, 1)) MiB)"
    }
    if (!$ArtifactUpload) {
        $checksums | Set-Content (Join-Path $outputRoot 'SHA256SUMS.txt') -Encoding utf8
        $notes = Get-Content docs/release.md -Raw
        "$notes`n`nBuild commit: $commit" | Set-Content (Join-Path $outputRoot 'RELEASE-NOTES.md') -Encoding utf8
    }
} finally {
    # Delete only this invocation's verified staging directory, never the package output.
    if (!$ArtifactUpload -and $stagingRoot -and (Test-Path -LiteralPath $stagingRoot)) {
        $resolvedStaging = [IO.Path]::GetFullPath($stagingRoot)
        $allowedRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts/package-staging')) + [IO.Path]::DirectorySeparatorChar
        if (!$resolvedStaging.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe staging cleanup path.' }
        Remove-Item -LiteralPath $resolvedStaging -Recurse -Force
    }
    Pop-Location
}
