# ===== 存档文件查找助手（全中文，不需要 Procmon）=====
# 原理：先记下时间 -> 你去游戏存档 -> 扫描这段时间内被修改的文件。
# 这就是逆向里"从零观测"的第一步：不看代码，先看它碰了什么文件。

$ErrorActionPreference = 'SilentlyContinue'

Write-Host "===== 存档文件查找助手 ====="
Write-Host ""
Write-Host "步骤："
Write-Host "  1. 按回车，脚本记下当前时间"
Write-Host "  2. 切到游戏，存一次档"
Write-Host "  3. 回到本窗口，再按回车"
Write-Host "  4. 脚本告诉你刚才哪些文件被写入了"
Write-Host ""
Read-Host "准备好了就按回车"

$start = Get-Date
Write-Host ""
Write-Host ("已记下时间：" + $start.ToString('HH:mm:ss'))
Write-Host ""
Read-Host ">> 现在去游戏存档，存完回到这里按回车 <<"

$end = Get-Date
Write-Host ""
Write-Host "正在扫描文件，请稍等十几秒..."

$places = @(
    (Join-Path $env:USERPROFILE 'AppData\Local')
    (Join-Path $env:USERPROFILE 'AppData\Roaming')
    (Join-Path $env:USERPROFILE 'Documents')
    (Join-Path $env:USERPROFILE 'Saved Games')
    (Join-Path $env:USERPROFILE 'Desktop')
)

$hits = foreach ($p in $places) {
    Get-ChildItem -LiteralPath $p -Recurse -File |
        Where-Object { $_.LastWriteTime -ge $start -and $_.LastWriteTime -le $end }
}

Write-Host ""
if (-not $hits) {
    Write-Host "没有发现变化的文件。"
    Write-Host "可能原因：存档写到了别的盘（比如游戏目录 E:\game\ASTLIBRA），"
    Write-Host "          或者游戏先缓存、过一会儿才写盘。多存几次档再试。"
} else {
    Write-Host "===== 刚才被修改的文件（最新的排在最上面）====="
    $hits | Sort-Object LastWriteTime -Descending | Select-Object -First 40 |
        Format-Table @{L='时间';E={$_.LastWriteTime.ToString('HH:mm:ss')}}, @{L='字节';E={$_.Length}}, @{L='完整路径';E={$_.FullName}} -AutoSize
    Write-Host ""
    Write-Host "提示：游戏存档通常 名字里带 save / dat / slot，在游戏名同名的文件夹里。"
}
