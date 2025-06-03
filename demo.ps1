# Shadowverse Beyond 开发者工具演示脚本
# 展示所有开发者工具的使用方法

param(
    [switch]$SkipBuild,
    [switch]$AutoDemo
)

Write-Host "🎮 Shadowverse Beyond 开发者工具演示" -ForegroundColor Cyan
Write-Host "=" * 50 -ForegroundColor Gray

function Show-Step {
    param($StepNumber, $Description, $Action = $null)
    
    Write-Host ""
    Write-Host "📍 步骤 $StepNumber : $Description" -ForegroundColor Yellow
    Write-Host "-" * 40 -ForegroundColor Gray
    
    if ($Action) {
        Write-Host "操作: $Action" -ForegroundColor White
    }
    
    if (!$AutoDemo) {
        Write-Host "按任意键继续..." -ForegroundColor Green
        $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    } else {
        Start-Sleep -Seconds 2
    }
}

function Test-Prerequisites {
    Write-Host "🔍 检查环境..." -ForegroundColor Yellow
    
    $issues = @()
    
    try {
        $dotnetVersion = dotnet --version
        Write-Host "✅ .NET SDK: $dotnetVersion" -ForegroundColor Green
    } catch {
        $issues += ".NET SDK 未安装"
    }
    
    try {
        $nodeVersion = node --version
        Write-Host "✅ Node.js: $nodeVersion" -ForegroundColor Green
    } catch {
        $issues += "Node.js 未安装"
    }
    
    if (!(Test-Path "Server/ShadowverseBeyond.Server.csproj")) {
        $issues += "后端项目文件不存在"
    }
    
    if (!(Test-Path "package.json")) {
        $issues += "前端项目文件不存在"
    }
    
    if ($issues.Count -gt 0) {
        Write-Host "❌ 发现问题:" -ForegroundColor Red
        $issues | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
        return $false
    }
    
    Write-Host "✅ 环境检查通过" -ForegroundColor Green
    return $true
}

function Demo-DevTools {
    Write-Host "🛠️ 开始开发者工具演示..." -ForegroundColor Cyan
    
    Show-Step 1 "环境检查" "检查必要的开发工具和项目文件"
    
    if (!(Test-Prerequisites)) {
        Write-Host "❌ 环境检查失败，请先安装必要的工具" -ForegroundColor Red
        return
    }
    
    Show-Step 2 "编译项目" "编译后端项目以确保没有错误"
    
    if (!$SkipBuild) {
        Write-Host "🔨 编译后端项目..." -ForegroundColor Yellow
        Push-Location "Server"
        try {
            dotnet build --configuration Debug
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✅ 编译成功" -ForegroundColor Green
            } else {
                Write-Host "❌ 编译失败" -ForegroundColor Red
                Pop-Location
                return
            }
        } catch {
            Write-Host "❌ 编译出错: $_" -ForegroundColor Red
            Pop-Location
            return
        }
        Pop-Location
    } else {
        Write-Host "⏭️ 跳过编译" -ForegroundColor Yellow
    }
    
    Show-Step 3 "安装前端依赖" "确保前端依赖已安装"
    
    if (!(Test-Path "node_modules")) {
        Write-Host "📦 安装前端依赖..." -ForegroundColor Yellow
        npm install
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ 依赖安装成功" -ForegroundColor Green
        } else {
            Write-Host "❌ 依赖安装失败" -ForegroundColor Red
            return
        }
    } else {
        Write-Host "✅ 前端依赖已存在" -ForegroundColor Green
    }
    
    Show-Step 4 "展示开发者工具文件" "查看创建的开发者工具文件"
    
    $toolFiles = @(
        @{Name="游戏内控制台"; File="src/devConsole.js"; Description="集成在游戏界面中的控制台"},
        @{Name="独立工具面板"; File="dev-tools.html"; Description="独立的调试界面"},
        @{Name="自动化测试脚本"; File="test-automation.js"; Description="浏览器控制台自动化测试"},
        @{Name="一键启动脚本"; File="start-dev.ps1"; Description="自动启动开发环境"},
        @{Name="测试运行器"; File="run-tests.ps1"; Description="简化单元测试执行"}
    )
    
    foreach ($tool in $toolFiles) {
        if (Test-Path $tool.File) {
            Write-Host "✅ $($tool.Name): $($tool.File)" -ForegroundColor Green
            Write-Host "   $($tool.Description)" -ForegroundColor Gray
        } else {
            Write-Host "❌ $($tool.Name): $($tool.File) (文件不存在)" -ForegroundColor Red
        }
    }
    
    Show-Step 5 "演示一键启动脚本" "展示如何使用一键启动脚本"
    
    Write-Host "💡 一键启动脚本用法:" -ForegroundColor Yellow
    Write-Host "  .\start-dev.ps1                    # 基本启动" -ForegroundColor White
    Write-Host "  .\start-dev.ps1 -OpenBrowser       # 启动并打开浏览器" -ForegroundColor White
    Write-Host "  .\start-dev.ps1 -DevTools          # 启动并打开开发工具" -ForegroundColor White
    Write-Host "  .\start-dev.ps1 -OpenBrowser -DevTools  # 启动所有工具" -ForegroundColor White
    
    Show-Step 6 "演示测试运行器" "展示如何运行测试"
    
    Write-Host "🧪 测试运行器用法:" -ForegroundColor Yellow
    Write-Host "  .\run-tests.ps1                    # 运行所有测试" -ForegroundColor White
    Write-Host "  .\run-tests.ps1 -Watch             # 监视模式" -ForegroundColor White
    Write-Host "  .\run-tests.ps1 -Coverage          # 代码覆盖率" -ForegroundColor White
    Write-Host "  .\run-tests.ps1 -Filter 'Quick'    # 过滤测试" -ForegroundColor White
    
    Show-Step 7 "演示独立开发工具" "打开独立的开发者工具面板"
    
    if (Test-Path "dev-tools.html") {
        Write-Host "🌐 打开独立开发者工具面板..." -ForegroundColor Yellow
        Start-Process "dev-tools.html"
        Write-Host "✅ 开发者工具面板已打开" -ForegroundColor Green
        Write-Host "💡 在工具面板中可以:" -ForegroundColor Yellow
        Write-Host "  - 连接到游戏服务器" -ForegroundColor White
        Write-Host "  - 创建和管理游戏" -ForegroundColor White
        Write-Host "  - 测试各种游戏功能" -ForegroundColor White
        Write-Host "  - 查看实时日志" -ForegroundColor White
    } else {
        Write-Host "❌ 开发者工具面板文件不存在" -ForegroundColor Red
    }
    
    Show-Step 8 "游戏内控制台说明" "介绍游戏内控制台的使用方法"
    
    Write-Host "🎮 游戏内控制台使用方法:" -ForegroundColor Yellow
    Write-Host "  1. 启动游戏后，按 Ctrl + ` 打开控制台" -ForegroundColor White
    Write-Host "  2. 或点击游戏界面右上角的 🛠️ 按钮" -ForegroundColor White
    Write-Host "  3. 输入 'help' 查看所有可用命令" -ForegroundColor White
    Write-Host "  4. 使用 ↑↓ 键浏览命令历史" -ForegroundColor White
    Write-Host "" -ForegroundColor White
    Write-Host "📝 常用命令:" -ForegroundColor Yellow
    Write-Host "  gamestate                          # 查看游戏状态" -ForegroundColor White
    Write-Host "  play <cardId> [target]             # 出牌" -ForegroundColor White
    Write-Host "  attack <attacker> <target>         # 攻击" -ForegroundColor White
    Write-Host "  evolve <unitUid>                   # 进化" -ForegroundColor White
    Write-Host "  endturn                            # 结束回合" -ForegroundColor White
    
    Show-Step 9 "自动化测试演示" "展示浏览器控制台自动化测试"
    
    Write-Host "🤖 自动化测试使用方法:" -ForegroundColor Yellow
    Write-Host "  1. 在浏览器中打开游戏页面" -ForegroundColor White
    Write-Host "  2. 按 F12 打开开发者工具" -ForegroundColor White
    Write-Host "  3. 在控制台中输入: quickTest()" -ForegroundColor White
    Write-Host "  4. 或者使用自定义测试:" -ForegroundColor White
    Write-Host "     const tester = new GameTestAutomation();" -ForegroundColor Gray
    Write-Host "     await tester.runFullTestSuite();" -ForegroundColor Gray
    
    Show-Step 10 "完整演示" "启动完整的开发环境"
    
    Write-Host "🚀 现在启动完整的开发环境进行演示..." -ForegroundColor Yellow
    Write-Host "这将:" -ForegroundColor White
    Write-Host "  - 启动后端服务器" -ForegroundColor White
    Write-Host "  - 启动前端服务器" -ForegroundColor White
    Write-Host "  - 打开游戏页面" -ForegroundColor White
    Write-Host "  - 初始化游戏内控制台" -ForegroundColor White
    
    if (!$AutoDemo) {
        $response = Read-Host "是否启动完整演示? (y/N)"
        if ($response -eq 'y' -or $response -eq 'Y') {
            Write-Host "🎯 启动完整演示..." -ForegroundColor Green
            & ".\start-dev.ps1" -OpenBrowser
        } else {
            Write-Host "演示完成！您可以手动运行 .\start-dev.ps1 -OpenBrowser 来启动完整环境。" -ForegroundColor Green
        }
    } else {
        Write-Host "自动演示模式：跳过完整启动" -ForegroundColor Yellow
    }
}

# 主函数
function Main {
    try {
        Demo-DevTools
        
        Write-Host ""
        Write-Host "🎉 演示完成！" -ForegroundColor Green
        Write-Host "=" * 50 -ForegroundColor Gray
        Write-Host "📚 更多信息请查看: 文档\开发者工具使用指南.md" -ForegroundColor Cyan
        Write-Host "🛠️ 开始开发: .\start-dev.ps1 -OpenBrowser -DevTools" -ForegroundColor Cyan
        Write-Host "🧪 运行测试: .\run-tests.ps1 -Watch" -ForegroundColor Cyan
        
    } catch {
        Write-Host "❌ 演示过程中出现错误: $_" -ForegroundColor Red
        exit 1
    }
}

# 显示帮助信息
if ($args -contains "-h" -or $args -contains "--help") {
    Write-Host "Shadowverse Beyond 开发者工具演示" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "用法: .\demo.ps1 [选项]" -ForegroundColor White
    Write-Host ""
    Write-Host "选项:" -ForegroundColor Yellow
    Write-Host "  -SkipBuild     跳过编译步骤" -ForegroundColor White
    Write-Host "  -AutoDemo      自动演示模式（无需手动确认）" -ForegroundColor White
    Write-Host ""
    Write-Host "示例:" -ForegroundColor Yellow
    Write-Host "  .\demo.ps1                         # 完整演示" -ForegroundColor White
    Write-Host "  .\demo.ps1 -SkipBuild              # 跳过编译" -ForegroundColor White
    Write-Host "  .\demo.ps1 -AutoDemo               # 自动演示" -ForegroundColor White
    exit 0
}

# 运行主函数
Main
