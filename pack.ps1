$output = "artifacts"

Write-Host "Building solution..." -ForegroundColor Cyan
dotnet build CoreDesign.slnx --configuration Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed." -ForegroundColor Red
    exit 1
}

Write-Host "Packing packages to $output..." -ForegroundColor Cyan
dotnet pack CoreDesign.slnx --configuration Release --no-build --output $output
if ($LASTEXITCODE -ne 0) {
    Write-Host "Pack failed." -ForegroundColor Red
    exit 1
}

Write-Host "Done. Packages written to $output." -ForegroundColor Green
Get-ChildItem $output -Filter "*.nupkg" | Sort-Object Name | ForEach-Object { Write-Host "  $($_.Name)" }
