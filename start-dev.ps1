# Shadowverse Beyond 开发环境一键启动脚本
# 使用方法: 在项目根目录运行 .\start-dev.ps1

param(
    [switch]$SkipBuild,      # 跳过编译
    [switch]$OpenBrowser,    # 自动打开浏览器
    [switch]$DevTools,       # 打开开发者工具
    [switch]$RunTests,       # 运行测试
    [string]$Port = "5173"   # 前端端口
)

Write-Host "🚀 Shadowverse Beyond 开发环境启动器" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Gray

# 检查必要的工具
function Test-Prerequisites {
    Write-Host "🔍 检查开发环境..." -ForegroundColor Yellow
    
    # 检查 .NET
    try {
        $dotnetVersion = dotnet --version
        Write-Host "✅ .NET SDK: $dotnetVersion" -ForegroundColor Green
    } catch {
        Write-Host "❌ 未找到 .NET SDK，请先安装" -ForegroundColor Red
        exit 1
    }
    
    # 检查 Node.js
    try {
        $nodeVersion = node --version
        Write-Host "✅ Node.js: $nodeVersion" -ForegroundColor Green
    } catch {
        Write-Host "❌ 未找到 Node.js，请先安装" -ForegroundColor Red
        exit 1
    }
    
    # 检查 npm
    try {
        $npmVersion = npm --version
        Write-Host "✅ npm: $npmVersion" -ForegroundColor Green
    } catch {
        Write-Host "❌ 未找到 npm" -ForegroundColor Red
        exit 1
    }
}

# 编译后端
function Build-Backend {
    if ($SkipBuild) {
        Write-Host "⏭️ 跳过后端编译" -ForegroundColor Yellow
        return
    }
    
    Write-Host "🔨 编译后端项目..." -ForegroundColor Yellow
    Push-Location "Server"
    
    try {
        dotnet build --configuration Debug
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ 后端编译成功" -ForegroundColor Green
        } else {
            Write-Host "❌ 后端编译失败" -ForegroundColor Red
            Pop-Location
            exit 1
        }
    } catch {
        Write-Host "❌ 后端编译出错: $_" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    
    Pop-Location
}

# 安装前端依赖
function Install-FrontendDependencies {
    Write-Host "📦 检查前端依赖..." -ForegroundColor Yellow
    
    if (!(Test-Path "node_modules")) {
        Write-Host "📥 安装前端依赖..." -ForegroundColor Yellow
        npm install
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ 前端依赖安装成功" -ForegroundColor Green
        } else {
            Write-Host "❌ 前端依赖安装失败" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "✅ 前端依赖已存在" -ForegroundColor Green
    }
}

# 运行测试
function Run-Tests {
    if (!$RunTests) {
        return
    }
    
    Write-Host "🧪 运行测试..." -ForegroundColor Yellow
    Push-Location "Server.Tests"
    
    try {
        dotnet test --logger "console;verbosity=normal"
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ 测试通过" -ForegroundColor Green
        } else {
            Write-Host "⚠️ 部分测试失败" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "❌ 测试运行出错: $_" -ForegroundColor Red
    }
    
    Pop-Location
}

# 启动后端服务器
function Start-Backend {
    Write-Host "🖥️ 启动后端服务器..." -ForegroundColor Yellow
    
    $backendJob = Start-Job -ScriptBlock {
        Set-Location $using:PWD
        Push-Location "Server"
        dotnet run --urls "http://localhost:5000"
    }
    
    # 等待后端启动
    Write-Host "⏳ 等待后端服务器启动..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    
    # 检查后端是否启动成功
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:5000/health" -TimeoutSec 5 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-Host "✅ 后端服务器启动成功 (http://localhost:5000)" -ForegroundColor Green
        } else {
            Write-Host "⚠️ 后端服务器可能未完全启动" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "⚠️ 无法验证后端状态，但进程已启动" -ForegroundColor Yellow
    }
    
    return $backendJob
}

# 启动前端服务器
function Start-Frontend {
    Write-Host "🌐 启动前端服务器..." -ForegroundColor Yellow
    
    $frontendJob = Start-Job -ScriptBlock {
        Set-Location $using:PWD
        npm run dev -- --port $using:Port --host
    }
    
    # 等待前端启动
    Write-Host "⏳ 等待前端服务器启动..." -ForegroundColor Yellow
    Start-Sleep -Seconds 3
    
    Write-Host "✅ 前端服务器启动成功 (http://localhost:$Port)" -ForegroundColor Green
    
    return $frontendJob
}

# 打开浏览器
function Open-Browser {
    if ($OpenBrowser) {
        Write-Host "🌐 打开浏览器..." -ForegroundColor Yellow
        Start-Process "http://localhost:$Port"
    }
    
    if ($DevTools) {
        Write-Host "🛠️ 打开开发者工具..." -ForegroundColor Yellow
        Start-Process "dev-tools.html"
    }
}

# 显示使用说明
function Show-Instructions {
    Write-Host ""
    Write-Host "🎮 开发环境已启动!" -ForegroundColor Green
    Write-Host "=" * 50 -ForegroundColor Gray
    Write-Host "前端地址: http://localhost:$Port" -ForegroundColor Cyan
    Write-Host "后端地址: http://localhost:5000" -ForegroundColor Cyan
    Write-Host "API文档: http://localhost:5000/swagger" -ForegroundColor Cyan
    if (Test-Path "dev-tools.html") {
        Write-Host "开发工具: dev-tools.html" -ForegroundColor Cyan
    }
    Write-Host ""
    Write-Host "💡 使用提示:" -ForegroundColor Yellow
    Write-Host "- 按 Ctrl+C 停止服务器" -ForegroundColor White
    Write-Host "- 修改代码后会自动重新加载" -ForegroundColor White
    Write-Host "- 查看浏览器控制台获取调试信息" -ForegroundColor White
    Write-Host ""
    Write-Host "🧪 快速测试:" -ForegroundColor Yellow
    Write-Host "- 在浏览器控制台运行: quickTest()" -ForegroundColor White
    Write-Host "- 或使用开发者工具面板进行测试" -ForegroundColor White
    Write-Host ""
}

# 主函数
function Main {
    try {
        Test-Prerequisites
        Build-Backend
        Install-FrontendDependencies
        Run-Tests
        
        $backendJob = Start-Backend
        $frontendJob = Start-Frontend
        
        Open-Browser
        Show-Instructions
        
        # 等待用户中断
        Write-Host "按任意键停止服务器..." -ForegroundColor Yellow
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
        
        # 清理
        Write-Host "🛑 正在停止服务器..." -ForegroundColor Yellow
        Stop-Job $backendJob -ErrorAction SilentlyContinue
        Stop-Job $frontendJob -ErrorAction SilentlyContinue
        Remove-Job $backendJob -ErrorAction SilentlyContinue
        Remove-Job $frontendJob -ErrorAction SilentlyContinue
        
        Write-Host "✅ 服务器已停止" -ForegroundColor Green
        
    } catch {
        Write-Host "❌ 启动过程中出现错误: $_" -ForegroundColor Red
        exit 1
    }
}

# 显示帮助信息
if ($args -contains "-h" -or $args -contains "--help") {
    Write-Host "Shadowverse Beyond 开发环境启动器" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "用法: .\start-dev.ps1 [选项]" -ForegroundColor White
    Write-Host ""
    Write-Host "选项:" -ForegroundColor Yellow
    Write-Host "  -SkipBuild     跳过后端编译" -ForegroundColor White
    Write-Host "  -OpenBrowser   自动打开浏览器" -ForegroundColor White
    Write-Host "  -DevTools      打开开发者工具" -ForegroundColor White
    Write-Host "  -RunTests      运行测试" -ForegroundColor White
    Write-Host "  -Port <端口>   指定前端端口 (默认: 5173)" -ForegroundColor White
    Write-Host ""
    Write-Host "示例:" -ForegroundColor Yellow
    Write-Host "  .\start-dev.ps1 -OpenBrowser -DevTools" -ForegroundColor White
    Write-Host "  .\start-dev.ps1 -SkipBuild -Port 3000" -ForegroundColor White
    exit 0
}

# 运行主函数
Main
