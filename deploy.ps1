param(
    [string]$NugetApiKey = "",
    [string]$Project = "",
    [string]$SecretsKey = "NuGet:ApiKey"
)

$output = "artifacts"
$source = "https://api.nuget.org/v3/index.json"

# ---- User-secrets resolution (mirrors CoreDesign.SeedTool) ----

function Get-ApiKeyFromUserSecrets {
    param([string]$projectPath, [string]$key)

    $projectDir = if (Test-Path $projectPath -PathType Container) {
        $projectPath
    } else {
        Split-Path $projectPath -Parent
    }

    $csproj = Get-ChildItem $projectDir -Filter "*.csproj" -ErrorAction SilentlyContinue |
              Select-Object -First 1
    if (-not $csproj) {
        Write-Host "  No .csproj found in: $projectDir" -ForegroundColor Red
        return $null
    }

    [xml]$xml   = Get-Content $csproj.FullName
    $node       = $xml.SelectSingleNode("//UserSecretsId")
    $secretsId  = if ($node) { $node.InnerText } else { $null }

    if (-not $secretsId) {
        Write-Host "  No <UserSecretsId> in $($csproj.Name)." -ForegroundColor Red
        Write-Host "  Run: dotnet user-secrets init --project `"$projectDir`"" -ForegroundColor Yellow
        return $null
    }

    $secretsFile = Join-Path $env:APPDATA "Microsoft\UserSecrets\$secretsId\secrets.json"
    if (-not (Test-Path $secretsFile)) {
        Write-Host "  No user secrets file found for $($csproj.Name)." -ForegroundColor Red
        Write-Host "  Run: dotnet user-secrets set `"$key`" `"<your-api-key>`" --project `"$projectDir`"" -ForegroundColor Yellow
        return $null
    }

    $json    = Get-Content $secretsFile -Raw | ConvertFrom-Json
    $current = $json
    foreach ($part in ($key -split ':')) {
        if ($null -eq $current) { break }
        $current = $current.$part
    }

    if (-not $current) {
        Write-Host "  Key '$key' not found in user secrets for $($csproj.Name)." -ForegroundColor Red
        Write-Host "  Run: dotnet user-secrets set `"$key`" `"<your-api-key>`" --project `"$projectDir`"" -ForegroundColor Yellow
        return $null
    }

    return $current
}

# ---- Resolve API key ----

$apiKey = $null

if ($NugetApiKey) {
    $apiKey = $NugetApiKey
} elseif ($env:NUGET_API_KEY) {
    $apiKey = $env:NUGET_API_KEY
    Write-Host "Using NUGET_API_KEY from environment." -ForegroundColor DarkGray
} elseif ($Project) {
    Write-Host "Reading NuGet API key from user secrets ($Project, key: $SecretsKey)..." -ForegroundColor DarkGray
    $apiKey = Get-ApiKeyFromUserSecrets -projectPath $Project -key $SecretsKey
} else {
    Write-Host "No API key found. Set NUGET_API_KEY or pass -Project." -ForegroundColor Red
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  Environment variable (session):" -ForegroundColor Yellow
    Write-Host "    `$env:NUGET_API_KEY = `"<your-api-key>`"" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "  User secrets (persistent, per project):" -ForegroundColor Yellow
    Write-Host "    dotnet user-secrets set `"NuGet:ApiKey`" `"<your-api-key>`" --project <project-path>" -ForegroundColor Yellow
    Write-Host "    .\deploy.ps1 -Project <project-path>" -ForegroundColor Yellow
    exit 1
}

if (-not $apiKey) {
    exit 1
}

# ---- Discover and push packages ----

$packages = Get-ChildItem $output -Filter "*.nupkg" -ErrorAction SilentlyContinue | Sort-Object Name

if ($packages.Count -eq 0) {
    Write-Host "No packages found in '$output'. Run pack.ps1 first." -ForegroundColor Yellow
    exit 0
}

Write-Host "Pushing $($packages.Count) package(s) to NuGet..." -ForegroundColor Cyan

$failed = @()

foreach ($pkg in $packages) {
    Write-Host "  Pushing $($pkg.Name)..." -ForegroundColor Cyan
    dotnet nuget push $pkg.FullName --api-key $apiKey --source $source --skip-duplicate
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  Failed: $($pkg.Name)" -ForegroundColor Red
        $failed += $pkg.Name
    } else {
        Write-Host "  OK: $($pkg.Name)" -ForegroundColor Green
    }
}

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "$($failed.Count) package(s) failed to push:" -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  $_" }
    exit 1
}

Write-Host ""
Write-Host "All packages pushed successfully." -ForegroundColor Green
