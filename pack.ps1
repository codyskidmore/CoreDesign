param(
    # SemVer2 prerelease label, e.g. "net11-preview.1" -> packages version as 1.2.4-net11-preview.1.
    # Leave unset to pack the stable version (today's default behavior).
    [string]$VersionSuffix = ""
)

$output = "artifacts"

$buildArgs = @("CoreDesign.slnx", "--configuration", "Release")
if ($VersionSuffix) {
    $buildArgs += "-p:VersionSuffix=$VersionSuffix"
    Write-Host "Packing as prerelease: -$VersionSuffix" -ForegroundColor Yellow
}

Write-Host "Building solution..." -ForegroundColor Cyan
dotnet build @buildArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

$packArgs = @("CoreDesign.slnx", "--configuration", "Release", "--no-build", "--output", $output)
if ($VersionSuffix) { $packArgs += "-p:VersionSuffix=$VersionSuffix" }

Write-Host "Packing packages to $output..." -ForegroundColor Cyan
dotnet pack @packArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Pack failed." -ForegroundColor Red
    exit 1
}

Write-Host "Done. Packages written to $output." -ForegroundColor Green
Get-ChildItem $output -Filter "*.nupkg" | Sort-Object Name | ForEach-Object { Write-Host "  $($_.Name)" }
