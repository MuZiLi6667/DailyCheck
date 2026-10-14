# -*- coding: utf-8 -*-
"""在桌面创建「每日打卡」快捷方式，并画一个图标"""
import base64
import os
import subprocess
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

APP_DIR = r"D:\打卡程序\app"
ICON = APP_DIR + r"\app.ico"

# 整段 PowerShell 用 UTF-16LE + Base64 传进去，避免中文和引号在命令行里出问题
script = r'''
Add-Type -AssemblyName System.Drawing

$S = 256
$bmp = New-Object System.Drawing.Bitmap($S, $S)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# 圆角蓝色方块
$p = New-Object System.Drawing.Drawing2D.GraphicsPath
$r = 56
$p.AddArc(6, 6, $r, $r, 180, 90)
$p.AddArc(($S - 6 - $r), 6, $r, $r, 270, 90)
$p.AddArc(($S - 6 - $r), ($S - 6 - $r), $r, $r, 0, 90)
$p.AddArc(6, ($S - 6 - $r), $r, $r, 90, 90)
$p.CloseFigure()
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 79, 142, 247))
$g.FillPath($brush, $p)

# 白色对勾
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 24)
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$pts = @(
  (New-Object System.Drawing.Point(68, 134)),
  (New-Object System.Drawing.Point(112, 180)),
  (New-Object System.Drawing.Point(190, 84))
)
$g.DrawLines($pen, $pts)
$g.Dispose()

$hicon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hicon)
$fs = [System.IO.File]::Create("D:\打卡程序\app\app.ico")
$icon.Save($fs)
$fs.Close()
$bmp.Dispose()
Write-Output "ICON_DONE"

# ---------- 建快捷方式 ----------
$desktop = [Environment]::GetFolderPath("Desktop")
$lnk = Join-Path $desktop "每日打卡.lnk"
$ws = New-Object -ComObject WScript.Shell
$sc = $ws.CreateShortcut($lnk)
$sc.TargetPath = "D:\打卡程序\app\DailyCheck.exe"
$sc.WorkingDirectory = "D:\打卡程序\app"
$sc.Description = "每日打卡 - 四级 + 行测备考"
$sc.IconLocation = "D:\打卡程序\app\app.ico,0"
$sc.Save()
Write-Output "LNK_DONE"
Write-Output $lnk
'''

encoded = base64.b64encode(script.encode("utf-16-le")).decode()
r = subprocess.run(["powershell", "-NoProfile", "-EncodedCommand", encoded],
                   capture_output=True)

print(r.stdout.decode("utf-8", "replace").strip())
err = r.stderr.decode("gbk", "replace").strip()
if err:
    print("STDERR:", err[:800])

if os.path.exists(ICON):
    print(f"图标文件: {os.path.getsize(ICON)} 字节")
else:
    print("图标文件没生成，快捷方式会退回用默认图标")
