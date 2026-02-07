param(
    [switch]$SkipFrontend,
    [switch]$SkipBackend,
    [switch]$SkipNpmInstall,
    [switch]$NoBackup,
    [switch]$StartServer,
    [switch]$SetProductionEnv,
    [switch]$CreateProdConfig,
    [string]$Urls = "http://0.0.0.0:5100",
    [string]$PublishDir = "publish_release"
)

$ErrorActionPreference = "Stop"

function Require-Command {
    param([string]$Name)
    try {
        Get-Command $Name -ErrorAction Stop | Out-Null
    } catch {
        Write-Host "Missing required command: $Name" -ForegroundColor Red
        exit 1
    }
}


$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$stamp = Get-Date -Format "yyyyMMddHHmmss"

Write-Host "Shadowverse Beyond one-click deploy prep" -ForegroundColor Cyan
Write-Host ("=" * 48) -ForegroundColor Gray

# Required data files for seeding
$cardDataPath = Join-Path $root "data\card_data.json"
$abilitySeedPath = Join-Path $root "Server\Infrastructure\Data\SeedData\CardAbilitySeedData.sql"

if (!(Test-Path $cardDataPath)) {
    Write-Host "Missing seed data: $cardDataPath" -ForegroundColor Red
    exit 1
}
if (!(Test-Path $abilitySeedPath)) {
    Write-Host "Missing seed SQL: $abilitySeedPath" -ForegroundColor Red
    exit 1
}

if ($CreateProdConfig) {
    $prodConfigPath = Join-Path $root "Server\appsettings.Production.json"
    if (!(Test-Path $prodConfigPath)) {
        @'
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "ShadowverseBeyond": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=Data/shadowverse.db"
  }
}
'@ | Set-Content -Path $prodConfigPath -Encoding UTF8
        Write-Host "Created Server/appsettings.Production.json" -ForegroundColor Green
    } else {
        Write-Host "Server/appsettings.Production.json already exists" -ForegroundColor Yellow
    }
}

if ($SetProductionEnv) {
    $env:ASPNETCORE_ENVIRONMENT = "Production"
    Write-Host "ASPNETCORE_ENVIRONMENT=Production set for this session" -ForegroundColor Green
}

if (-not $SkipFrontend) {
    Require-Command "node"
    Require-Command "npm"

    if (-not $SkipNpmInstall -and !(Test-Path "node_modules")) {
        Write-Host "Installing frontend dependencies..." -ForegroundColor Yellow
        npm install
        if ($LASTEXITCODE -ne 0) {
            Write-Host "npm install failed" -ForegroundColor Red
            exit 1
        }
    }

    Write-Host "Building frontend..." -ForegroundColor Yellow
    npm run build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "npm run build failed" -ForegroundColor Red
        exit 1
    }

    if (!(Test-Path "dist\index.html")) {
        Write-Host "dist/index.html not found after build" -ForegroundColor Red
        exit 1
    }

    if ((Test-Path "index.prod.html") -and -not $NoBackup) {
        Copy-Item "index.prod.html" ("index.prod.html.bak-" + $stamp) -Force
    }
    Copy-Item "dist\index.html" "index.prod.html" -Force

    if (Test-Path "assets") {
        if ($NoBackup) {
            Remove-Item "assets" -Recurse -Force
        } else {
            Rename-Item "assets" ("assets.bak-" + $stamp)
        }
    }
    Copy-Item "dist\assets" "assets" -Recurse -Force

    Write-Host "Frontend prepared at root index.html + assets/" -ForegroundColor Green
}

if (-not $SkipBackend) {
    Require-Command "dotnet"
    Write-Host "Publishing backend..." -ForegroundColor Yellow
    Push-Location "Server"
    dotnet publish "ShadowverseBeyond.Server.csproj" -c Release -o $PublishDir
    if ($LASTEXITCODE -ne 0) {
        Pop-Location
        Write-Host "dotnet publish failed" -ForegroundColor Red
        exit 1
    }
    Pop-Location

    $logDir = Join-Path $root ("Server\" + $PublishDir + "\Logs")
    if (!(Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir | Out-Null
    }

    Write-Host ("Backend published to Server/" + $PublishDir) -ForegroundColor Green
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host ("- Upload the project root (or Server/" + $PublishDir + " + data + index.html/assets) to your server.") -ForegroundColor White
Write-Host ("- Start server: dotnet Server/" + $PublishDir + "/ShadowverseBeyond.Server.dll --urls " + $Urls) -ForegroundColor White
Write-Host "- Ensure port 5100 is open and WebSocket is enabled." -ForegroundColor White

if ($StartServer) {
    $dllPath = Join-Path $root ("Server\" + $PublishDir + "\ShadowverseBeyond.Server.dll")
    if (!(Test-Path $dllPath)) {
        Write-Host "Cannot start server; publish output not found." -ForegroundColor Red
        exit 1
    }
    Write-Host ""
    Write-Host "Starting server..." -ForegroundColor Yellow
    Push-Location ("Server\" + $PublishDir)
    dotnet "ShadowverseBeyond.Server.dll" --urls $Urls
    Pop-Location
}
