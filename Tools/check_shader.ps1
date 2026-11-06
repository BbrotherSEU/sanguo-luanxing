$ErrorActionPreference = "Stop"
$unity = "D:\Games\UnityData\2022.3.62f3c1\Editor\Unity.exe"
$proj = Join-Path (Split-Path $PSScriptRoot -Parent) "SanguoLuanxing"
$tmp = Join-Path $env:TEMP "sanguo_shadercheck"

# 造一个最小工程：只放要验证的着色器 + 一个脚本强制重新导入并检查编译错误
if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
New-Item -ItemType Directory -Force -Path "$tmp\Assets" | Out-Null
New-Item -ItemType Directory -Force -Path "$tmp\ProjectSettings" | Out-Null

$src = Join-Path $proj "Assets\Resources\Shaders"
Copy-Item "$src\*.shader" "$tmp\Assets" -Force

@'
using UnityEditor;
using UnityEngine;

public class ShaderCheck
{
    public static void Run()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        int bad = 0, ok = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Shader", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var sh = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (sh == null) { Debug.LogError("SHADER LOAD FAIL: " + path); bad++; continue; }
            if (ShaderUtil.ShaderHasError(sh))
            {
                foreach (var m in ShaderUtil.GetShaderMessages(sh))
                    Debug.LogError("SHADER ERROR [" + path + "] line " + m.line + ": " + m.message + " | " + m.messageDetails);
                bad++;
            }
            else { Debug.Log("SHADER OK: " + path); ok++; }
        }
        Debug.Log("SHADERCHECK RESULT ok=" + ok + " bad=" + bad);
        EditorApplication.Exit(bad > 0 ? 2 : (ok > 0 ? 0 : 3));
    }
}
'@ | Set-Content "$tmp\Assets\ShaderCheck.cs" -Encoding UTF8

@'
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!129 &1
PlayerSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 23
  productGUID: 00000000000000000000000000000000
  companyName: Test
  productName: ShaderCheck
  defaultScreenWidth: 640
  defaultScreenHeight: 480
'@ | Set-Content "$tmp\ProjectSettings\ProjectSettings.asset" -Encoding UTF8

$log = Join-Path $tmp "shadercheck.log"
if (Test-Path $log) { Remove-Item $log -Force }
# 沙箱内许可客户端握手会报错（不影响执行），清掉代理避免它去连外网
$env:no_proxy = "*"; $env:NO_PROXY = "*"
$env:HTTP_PROXY = ""; $env:HTTPS_PROXY = ""
$p = Start-Process -FilePath $unity -ArgumentList @(
    "-batchmode", "-nographics", "-quit", "-projectPath", $tmp,
    "-logFile", $log, "-executeMethod", "ShaderCheck.Run"
) -PassThru -Wait -NoNewWindow

$out = if (Test-Path $log) { Get-Content $log -Raw } else { "" }
$okLines = ([regex]::Matches($out, "SHADER OK:")).Count
$errLines = ([regex]::Matches($out, "SHADER ERROR|SHADER LOAD FAIL")).Count
Write-Host "--- shaders ok: $okLines, errors: $errLines ---"
if ($errLines -gt 0) {
    Write-Host "SHADER COMPILE FAILED"
    ($out -split "`n") | Select-String -Pattern "SHADER ERROR|SHADER LOAD FAIL" | Select-Object -First 20 | ForEach-Object { Write-Host $_.ToString() }
    exit 1
}
if ($okLines -eq 0) {
    Write-Host "NO SHADER COMPILED (see log below)"
    ($out -split "`n") | Select-String -Pattern "Exception|error CS|Aborting|licen" | Select-Object -First 20 | ForEach-Object { Write-Host $_.ToString() }
    exit 1
}
Write-Host "SHADER COMPILE OK"
