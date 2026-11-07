$ErrorActionPreference = "Stop"
$unity = "D:\Games\UnityData\2022.3.62f3c1\Editor\Unity.exe"
$proj = Join-Path (Split-Path $PSScriptRoot -Parent) "SanguoLuanxing"
$tmp = Join-Path $env:TEMP "sanguo_shadercheck"

# 校验思路：**直接用主工程**（它有现成的 Library，C# 不会重编译，
# 于是不会触发 Unity 的 IL Post Processor —— ILPP 后端要握手本地 dotnet 宿主，
# 沙箱里连不上，会一直卡在
# "Connectivity with IL Post Processor runner cannot be established yet"）。
# 批处理打开工程会导入新加的着色器，编译错误直接写进日志。
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

$log = Join-Path $tmp "shadercheck.log"
# 沙箱内许可客户端握手会报错（不影响执行），清掉代理避免它去连外网
$env:no_proxy = "*"; $env:NO_PROXY = "*"
$env:HTTP_PROXY = ""; $env:HTTPS_PROXY = ""
$p = Start-Process -FilePath $unity -ArgumentList @(
    "-batchmode", "-nographics", "-quit", "-projectPath", $proj,
    "-logFile", $log
) -PassThru -Wait -NoNewWindow

$out = if (Test-Path $log) { Get-Content $log -Raw } else { "" }
# 着色器编译错误在批处理日志里的形态
$errLines = ([regex]::Matches($out, "Shader error in|Shader warning in")).Count
$impLines = ([regex]::Matches($out, "GroundShadow")).Count
Write-Host "--- shader errors: $errLines, GroundShadow mentions: $impLines ---"
if ($errLines -gt 0) {
    Write-Host "SHADER COMPILE FAILED"
    ($out -split "`n") | Select-String -Pattern "Shader error in|Shader warning in" | Select-Object -First 20 | ForEach-Object { Write-Host $_.ToString() }
    exit 1
}
if ($impLines -eq 0) {
    Write-Host "SHADERS NOT IMPORTED (see log below)"
    ($out -split "`n") | Select-String -Pattern "Exception|error CS|Aborting" | Select-Object -First 20 | ForEach-Object { Write-Host $_.ToString() }
    exit 1
}
Write-Host "SHADER COMPILE OK"
