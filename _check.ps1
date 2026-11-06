$ed = "D:\Games\UnityData\2022.3.62f3c1\Editor\Data"
$api = "$ed\MonoBleedingEdge\lib\mono\4.7.1-api"
$proj = Join-Path $PSScriptRoot "SanguoLuanxing\Assets"
$out = Join-Path $env:TEMP "sanguo_check"
New-Item -ItemType Directory -Force -Path $out | Out-Null

$refs = @()
$refs += Get-ChildItem "$ed\Managed\UnityEngine" -Filter *.dll | ForEach-Object { $_.FullName }
$refs += "$api\mscorlib.dll", "$api\System.dll", "$api\System.Core.dll", "$api\Facades\netstandard.dll"
$refArgs = $refs | ForEach-Object { "-r:`"$_`"" }

$game = Get-ChildItem "$proj\Scripts" -Recurse -Filter *.cs | ForEach-Object { "`"$($_.FullName)`"" }
$rsp = Join-Path $out "game.rsp"
@("-nologo", "-noconfig", "-nostdlib+", "-target:library", "-langversion:9.0", "-nowarn:0414,0649,0169,0219", "-out:`"$out\Game.dll`"") + $refArgs + $game | Set-Content $rsp -Encoding UTF8
& "$ed\NetCoreRuntime\dotnet.exe" "$ed\DotNetSdkRoslyn\csc.dll" "@$rsp"
if ($LASTEXITCODE -ne 0) { Write-Host "GAME COMPILE FAILED"; exit 1 }

$editor = Get-ChildItem "$proj\Editor" -Recurse -Filter *.cs | ForEach-Object { "`"$($_.FullName)`"" }
$rsp2 = Join-Path $out "editor.rsp"
@("-nologo", "-noconfig", "-nostdlib+", "-target:library", "-langversion:9.0", "-nowarn:0414,0649,0169,0219", "-out:`"$out\GameEditor.dll`"", "-r:`"$out\Game.dll`"", "-r:`"$ed\Managed\UnityEditor.dll`"") + $refArgs + $editor | Set-Content $rsp2 -Encoding UTF8
& "$ed\NetCoreRuntime\dotnet.exe" "$ed\DotNetSdkRoslyn\csc.dll" "@$rsp2"
if ($LASTEXITCODE -ne 0) { Write-Host "EDITOR COMPILE FAILED"; exit 1 }
Write-Host "COMPILE OK"
