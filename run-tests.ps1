# 简化的测试运行脚本
param(
    [switch]$Watch,        # 监视模式
    [switch]$Coverage,     # 代码覆盖率
    [switch]$Verbose,      # 详细输出
    [string]$Filter = ""   # 测试过滤器
)

Write-Host "🧪 Shadowverse Beyond 测试运行器" -ForegroundColor Cyan
Write-Host "=" * 40 -ForegroundColor Gray

# 检查测试项目
if (!(Test-Path "Server.Tests/Server.Tests.csproj")) {
    Write-Host "❌ 未找到测试项目" -ForegroundColor Red
    exit 1
}

# 构建测试参数
$testArgs = @()

if ($Watch) {
    $testArgs += "--watch"
    Write-Host "👀 启用监视模式" -ForegroundColor Yellow
}

if ($Coverage) {
    $testArgs += "--collect:`"XPlat Code Coverage`""
    Write-Host "📊 启用代码覆盖率" -ForegroundColor Yellow
}

if ($Verbose) {
    $testArgs += "--logger", "console;verbosity=detailed"
    Write-Host "📝 启用详细输出" -ForegroundColor Yellow
}

if ($Filter) {
    $testArgs += "--filter", $Filter
    Write-Host "🔍 应用过滤器: $Filter" -ForegroundColor Yellow
}

# 运行测试
Write-Host "🚀 开始运行测试..." -ForegroundColor Green
Push-Location "Server.Tests"

try {
    if ($testArgs.Count -gt 0) {
        dotnet test @testArgs
    } else {
        dotnet test
    }
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ 所有测试通过!" -ForegroundColor Green
    } else {
        Write-Host "❌ 部分测试失败" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ 测试运行出错: $_" -ForegroundColor Red
} finally {
    Pop-Location
}

# 显示使用提示
if (!$Watch) {
    Write-Host ""
    Write-Host "💡 使用提示:" -ForegroundColor Yellow
    Write-Host "  .\run-tests.ps1 -Watch          # 监视模式" -ForegroundColor White
    Write-Host "  .\run-tests.ps1 -Coverage       # 代码覆盖率" -ForegroundColor White
    Write-Host "  .\run-tests.ps1 -Filter 'Quick' # 只运行包含'Quick'的测试" -ForegroundColor White
    Write-Host "  .\run-tests.ps1 -Verbose        # 详细输出" -ForegroundColor White
}
