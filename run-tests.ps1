# 后端测试入口；项目路径相对于脚本定位，不依赖当前终端目录。
param(
    [switch]$Watch,
    [switch]$Coverage,
    [switch]$Verbose,
    [string]$Filter = ""
)

$testProject = Join-Path $PSScriptRoot "Server.Tests/ShadowverseBeyond.Server.Tests.csproj"
$testArgs = @()

if ($Coverage) {
    $testArgs += '--collect:XPlat Code Coverage'
}

if ($Verbose) {
    $testArgs += "--logger", "console;verbosity=detailed"
}

if ($Filter) {
    $testArgs += "--filter", $Filter
}

# SDK 解析也依赖工作目录，因此统一在项目根目录运行。
Push-Location -LiteralPath $PSScriptRoot
try {
    # watch 是独立命令，不能作为 dotnet test 的参数。
    if ($Watch) {
        dotnet watch --project "$testProject" test @testArgs
    } else {
        dotnet test "$testProject" @testArgs
    }
    $testExitCode = $LASTEXITCODE
} finally {
    Pop-Location
}

# 让调用方接收到真实测试状态，不把失败包装成脚本成功。
exit $testExitCode
