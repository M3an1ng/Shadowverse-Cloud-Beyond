# Quick start script for Shadowverse Beyond
param(
    [switch]$OpenBrowser,
    [switch]$DevTools
)

Write-Host "Starting Shadowverse Beyond Development Environment" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Gray

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET SDK not found" -ForegroundColor Red
    exit 1
}

try {
    $nodeVersion = node --version  
    Write-Host "✓ Node.js: $nodeVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ Node.js not found" -ForegroundColor Red
    exit 1
}

# Install frontend dependencies if needed
if (!(Test-Path "node_modules")) {
    Write-Host "Installing frontend dependencies..." -ForegroundColor Yellow
    npm install
    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Failed to install dependencies" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Dependencies installed" -ForegroundColor Green
} else {
    Write-Host "✓ Dependencies already installed" -ForegroundColor Green
}

# Build backend
Write-Host "Building backend..." -ForegroundColor Yellow
Push-Location "Server"
dotnet build --configuration Debug
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Backend build failed" -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host "✓ Backend built successfully" -ForegroundColor Green

# Start backend server
Write-Host "Starting backend server..." -ForegroundColor Yellow
$backendJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    Push-Location "Server"
    dotnet run --launch-profile http
}

# Wait for backend to start
Start-Sleep -Seconds 5
Write-Host "✓ Backend server started (http://localhost:5100)" -ForegroundColor Green

# Start frontend server  
Write-Host "Starting frontend server..." -ForegroundColor Yellow
$frontendJob = Start-Job -ScriptBlock {
    Set-Location $using:PWD
    npm run dev -- --port 5173 --host
}

# Wait for frontend to start
Start-Sleep -Seconds 3
Write-Host "✓ Frontend server started (http://localhost:5173)" -ForegroundColor Green

# Open browser if requested
if ($OpenBrowser) {
    Write-Host "Opening browser..." -ForegroundColor Yellow
    Start-Process "http://localhost:5173"
}

# Open dev tools if requested
if ($DevTools) {
    Write-Host "Opening developer tools..." -ForegroundColor Yellow
    Start-Process "dev-tools.html"
}

# Show instructions
Write-Host ""
Write-Host "🎮 Development environment is ready!" -ForegroundColor Green
Write-Host "=" * 50 -ForegroundColor Gray
Write-Host "Frontend: http://localhost:5173" -ForegroundColor Cyan
Write-Host "Backend:  http://localhost:5100" -ForegroundColor Cyan
Write-Host "API Docs: http://localhost:5100/swagger" -ForegroundColor Cyan
Write-Host ""
Write-Host "💡 Tips:" -ForegroundColor Yellow
Write-Host "- Press Ctrl+C to stop servers" -ForegroundColor White
Write-Host "- In game, press Ctrl + ` to open developer console" -ForegroundColor White
Write-Host "- Type 'help' in console to see available commands" -ForegroundColor White
Write-Host "- Use dev-tools.html for standalone debugging" -ForegroundColor White
Write-Host ""
Write-Host "🧪 Quick test:" -ForegroundColor Yellow
Write-Host "- In browser console: quickTest()" -ForegroundColor White
Write-Host ""

# Wait for user to stop
Write-Host "Press any key to stop servers..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Cleanup
Write-Host "Stopping servers..." -ForegroundColor Yellow
Stop-Job $backendJob -ErrorAction SilentlyContinue
Stop-Job $frontendJob -ErrorAction SilentlyContinue
Remove-Job $backendJob -ErrorAction SilentlyContinue
Remove-Job $frontendJob -ErrorAction SilentlyContinue

Write-Host "✓ Servers stopped" -ForegroundColor Green
