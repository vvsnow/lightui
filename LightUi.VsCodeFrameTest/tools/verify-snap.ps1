<#
    验收脚本（可复现，不靠肉眼）。

    ★ 本文件必须保存为「UTF-8 with BOM」。Windows PowerShell 5.1 在没有 BOM 时会按 ANSI 读文件，
      中文注释会被解成乱码并把脚本解析崩掉（报的错会是 using/here-string 之类的莫名错误）。
      改完这个文件后请确认它是带 BOM 的。

    检查 A —— 有没有「不可见边框」
        GetWindowRect 与 DWMWA_EXTENDED_FRAME_BOUNDS 的差值。有边框 = 自绘标题栏按钮能贴上
        「可见边框」，也就是系统计算 Snap Layouts 热区的锚点。
        对照基线：WindowChrome 窗口这项全是 0；原生窗口（记事本/终端/VS/VS Code）是 5.6~7.2 DIP。

    检查 B —— 最大化按钮的 Snap 热区有没有死区
        光标沿最大化按钮从右缘往左缘逐点停靠，每次比对「按钮正下方那块」的截图。
        真浮窗会变化上万个采样点；先跑一次静止噪声基线确认这个区域是干净的。

    用法：
        .\verify-snap.ps1                                    # 跑本工程
        .\verify-snap.ps1 -Exe <另一个 exe 的路径>            # 跑对照组（例如 LightUi.Demo）
#>

param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\bin\Debug\net10.0-windows\LightUi.VsCodeFrameTest.exe'),
    [int]$StepPx = 3,
    [int]$DwellMs = 1700,
    # 兜底几何推算要用到当前按钮常量（WindowFrameUtil），改了常量记得一起传
    [int]$CaptionButtonWidth = 60,
    [int]$CaptionButtonSpacing = 1
)

$ErrorActionPreference = 'Stop'

# ★ 不设 DPI-aware 的话，本进程拿到的是「虚拟化」坐标（本机 125% 下会差 1.25 倍），
#   而 UIAutomation 和 SetCursorPos 用的是物理坐标 —— 两套混用会让点击和截图全部打偏，且不报错。
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class VerifySnapDpi { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware(); }
"@
[VerifySnapDpi]::SetProcessDPIAware() | Out-Null

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class VerifySnap {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetSystemMetricsForDpi(int i, uint dpi);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint f, IntPtr extra);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT val, int size);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public static readonly IntPtr TOPMOST = new IntPtr(-1);
    public static readonly IntPtr NOTOPMOST = new IntPtr(-2);
    public const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001;
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    public const int SM_CXSIZEFRAME = 32, SM_CXPADDEDBORDER = 92, SM_CXSIZE = 46;

    public static void ForceFocus(IntPtr h) {
        // 只做 SetForegroundWindow —— 不要用「敲一下 ALT」那种抢前台的老花招：
        // 单独一个 ALT 可能给窗口拉出系统菜单并进入模态循环，消息循环不转，
        // 窗口虽然还在（截图、DWM 查询都正常），但定时器不跑、WM_NCHITTEST 也没人处理。
        for (int i = 0; i < 10; i++) {
            SetForegroundWindow(h);
            SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
            System.Threading.Thread.Sleep(150);
            if (GetForegroundWindow() == h) return;
        }
    }
}
"@

function Grab([int]$x, [int]$y, [int]$w, [int]$h) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($x, $y, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $g.Dispose()
    return $bmp
}

# 采样比对（每 4px 取一点）。真浮窗会变上万点，光标闪烁之类只变个位数。
function DiffCount($a, $b) {
    $n = 0
    for ($y = 0; $y -lt $a.Height; $y += 4) {
        for ($x = 0; $x -lt $a.Width; $x += 4) {
            if ($a.GetPixel($x, $y).ToArgb() -ne $b.GetPixel($x, $y).ToArgb()) { $n++ }
        }
    }
    return $n
}

$Exe = [System.IO.Path]::GetFullPath($Exe)
if (-not (Test-Path $Exe)) { throw "找不到 exe：$Exe" }

Write-Output "================ 验收 ================"
Write-Output "目标：$Exe"

$proc = Start-Process -FilePath $Exe -PassThru
for ($i = 0; $i -lt 200; $i++) {
    Start-Sleep -Milliseconds 250
    $proc.Refresh()
    if ($proc.HasExited) { throw "进程提前退出，exit code $($proc.ExitCode)" }
    if ($proc.MainWindowHandle -ne 0) { break }
}
if ($proc.MainWindowHandle -eq 0) { throw '没等到主窗口' }
$h = $proc.MainWindowHandle
Start-Sleep -Milliseconds 2500

# 检查 A 要在「非最大化」下做：那时顶部内缩 0、左右下应为边框厚度
if ([VerifySnap]::IsZoomed($h)) { [VerifySnap]::ShowWindow($h, 9) | Out-Null; Start-Sleep -Milliseconds 1500 }
[VerifySnap]::SetWindowPos($h, [VerifySnap]::TOPMOST, 0, 0, 0, 0, ([VerifySnap]::SWP_NOMOVE -bor [VerifySnap]::SWP_NOSIZE)) | Out-Null
[VerifySnap]::ForceFocus($h)
Start-Sleep -Milliseconds 1000

# ---------------- 检查 A ----------------
$dpi = [VerifySnap]::GetDpiForWindow($h)
$scale = $dpi / 96.0
$r = New-Object VerifySnap+RECT; [VerifySnap]::GetWindowRect($h, [ref]$r) | Out-Null
$ef = New-Object VerifySnap+RECT; $hr = [VerifySnap]::DwmGetWindowAttribute($h, [VerifySnap]::DWMWA_EXTENDED_FRAME_BOUNDS, [ref]$ef, 16)

Write-Output ""
Write-Output "---- 检查 A：不可见边框 ----"
Write-Output ("dpi={0}（scale {1}）   SM_CXSIZEFRAME={2}  SM_CXPADDEDBORDER={3}  和为 {4}px = {5:F1} DIP" -f `
    $dpi, $scale, `
    [VerifySnap]::GetSystemMetricsForDpi([VerifySnap]::SM_CXSIZEFRAME, $dpi), `
    [VerifySnap]::GetSystemMetricsForDpi([VerifySnap]::SM_CXPADDEDBORDER, $dpi), `
    ([VerifySnap]::GetSystemMetricsForDpi([VerifySnap]::SM_CXSIZEFRAME, $dpi) + [VerifySnap]::GetSystemMetricsForDpi([VerifySnap]::SM_CXPADDEDBORDER, $dpi)), `
    (([VerifySnap]::GetSystemMetricsForDpi([VerifySnap]::SM_CXSIZEFRAME, $dpi) + [VerifySnap]::GetSystemMetricsForDpi([VerifySnap]::SM_CXPADDEDBORDER, $dpi)) / $scale))
Write-Output ("窗口矩形    {0},{1} - {2},{3}" -f $r.Left, $r.Top, $r.Right, $r.Bottom)
if ($hr -eq 0) {
    Write-Output ("可见边框    {0},{1} - {2},{3}" -f $ef.Left, $ef.Top, $ef.Right, $ef.Bottom)
    $l = $ef.Left - $r.Left; $t = $ef.Top - $r.Top; $rr = $r.Right - $ef.Right; $b = $r.Bottom - $ef.Bottom
    Write-Output ("不可见边框  左 {0}px / 上 {1}px / 右 {2}px / 下 {3}px   = {4:F1} / {5:F1} / {6:F1} / {7:F1} DIP" -f $l, $t, $rr, $b, ($l/$scale), ($t/$scale), ($rr/$scale), ($b/$scale))
    $verdict = if ($l -gt 0 -or $rr -gt 0 -or $b -gt 0) { '有不可见边框 ✓' } else { '没有不可见边框 ✗' }
    Write-Output ("A 结论：$verdict")
} else {
    Write-Output "A 结论：拿不到 DWMWA_EXTENDED_FRAME_BOUNDS（hr=$hr）"
}

# ---------------- 检查 B ----------------
Write-Output ""
Write-Output "---- 检查 B：最大化按钮 Snap 热区 ----"

# 标题栏按钮是按 Electron 的做法自绘的（Canvas + Path），不是 WPF Button，
# UIAutomation 看不见它们 —— 改成读窗口自己写出来的诊断旁路（%TEMP%\lightui-vscode-frametest.txt）。
$dump = Join-Path $env:TEMP 'lightui-vscode-frametest.txt'
Remove-Item $dump -ErrorAction SilentlyContinue

# 中途存一张窗口截图：如果诊断面板是空的，说明应用的消息循环没在转，后面的结论都不可信
$shotDir = Join-Path $env:TEMP 'lightui-vscode-frametest-shots'
New-Item -ItemType Directory -Force $shotDir | Out-Null
$shotBmp = New-Object System.Drawing.Bitmap ($r.Right - $r.Left), ($r.Bottom - $r.Top)
$shotG = [System.Drawing.Graphics]::FromImage($shotBmp)
$shotG.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size(($r.Right - $r.Left), ($r.Bottom - $r.Top))))
$shotG.Dispose()
$shotBmp.Save((Join-Path $shotDir 'at-check-b.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$shotBmp.Dispose()
Write-Output "已存窗口截图：$shotDir\at-check-b.png"

$maxL = 0; $maxT = 0; $maxR = 0; $maxH = 0; $maxName = ''
for ($i = 0; $i -lt 40 -and $maxR -eq 0; $i++) {
    if (Test-Path $dump) {
        foreach ($line in Get-Content $dump) {
            if ($line -match '^button=(Maximize|Restore),(-?\d+),(-?\d+),(-?\d+),(-?\d+)') {
                $maxName = $matches[1]
                $maxL = [int]$matches[2]; $maxT = [int]$matches[3]; $maxR = [int]$matches[4]
                $maxH = [int]$matches[5] - $maxT
                break
            }
        }
    }

    if ($maxR -eq 0) { Start-Sleep -Milliseconds 250 }
}

if ($maxR -eq 0) {
    # 兜底：脚本自己按几何算（和窗口无关的独立来源）。
    #   客户区宽度 = 可见边框宽度（左右各内缩一个 frame）
    #   容器右缘 = 客户区宽度 − 1     （WinFrameView::LayoutCaptionButtons 里那句 - 1）
    #   从右往左：close、maximize 各占 (基准宽 + 间距)，都贴客户区右缘
    $clientWidthDip = ($ef.Right - $ef.Left) / $scale
    $containerRightDip = $clientWidthDip - 1
    $step = $CaptionButtonWidth + $CaptionButtonSpacing
    $maxRightDip = $containerRightDip - $step
    $maxLeftDip = $maxRightDip - $step
    $maxL = [int]($ef.Left + $maxLeftDip * $scale)
    $maxR = [int]($ef.Left + $maxRightDip * $scale)
    $maxT = $r.Top
    $maxH = [int](29 * $scale)
    $maxName = 'Maximize(脚本按几何推算)'
    Write-Output '（诊断旁路不可用，改用脚本按几何推算的按钮矩形）'
}

Write-Output "诊断旁路（窗口自己报的）："
if (Test-Path $dump) {
    Get-Content $dump | ForEach-Object { Write-Output "  $_" }
} else {
    Write-Output "  （没有这个文件）"
}
Write-Output ("最大化按钮（{0}）  {1}..{2} y {3}..{4}（宽 {5}px = {6:F1} DIP），距窗口右边缘 {7:F1}..{8:F1} DIP" -f `
    $maxName, $maxL, $maxR, $maxT, ($maxT + $maxH), ($maxR - $maxL), (($maxR - $maxL) / $scale), `
    (($r.Right - $maxR) / $scale), (($r.Right - $maxL) / $scale))

$watchX = $maxL - [int](280 * $scale); if ($watchX -lt 0) { $watchX = 0 }
$watchY = $maxT + $maxH + 3
$watchW = [int](440 * $scale)
$watchH = [int](260 * $scale)
$vd = [System.Windows.Forms.SystemInformation]::VirtualScreen
if ($watchX + $watchW -gt $vd.Right) { $watchW = $vd.Right - $watchX }
if ($watchY + $watchH -gt $vd.Bottom) { $watchH = $vd.Bottom - $watchY }
Write-Output "监看区域    $watchX,$watchY ${watchW}x${watchH}"

# 光标停到窗口外，取基线
[VerifySnap]::SetCursorPos(20, ($vd.Bottom - 40)) | Out-Null
Start-Sleep -Milliseconds 1200
$base = Grab $watchX $watchY $watchW $watchH

# 静止噪声基线：同一状态连拍两次应当为 0，否则这个区域本身在动，检查 B 不可信
Start-Sleep -Milliseconds 1200
$base2 = Grab $watchX $watchY $watchW $watchH
$noise = DiffCount $base $base2
$base2.Dispose()
Write-Output "静止噪声    $noise 个采样点（应当为 0）"
if ($noise -gt 20) { Write-Output '警告：监看区域自身在变化，下面的判定可能不可信' }

$cy = $maxT + [int]($maxH / 2)
$dead = @()
$alive = 0
Write-Output "扫描（右 → 左，步长 ${StepPx}px）："
for ($x = $maxR - 2; $x -ge $maxL + 2; $x -= $StepPx) {
    [VerifySnap]::SetCursorPos(20, ($vd.Bottom - 40)) | Out-Null
    Start-Sleep -Milliseconds 450
    [VerifySnap]::SetCursorPos($x, $cy) | Out-Null
    Start-Sleep -Milliseconds $DwellMs
    $shot = Grab $watchX $watchY $watchW $watchH
    $n = DiffCount $base $shot
    $shot.Dispose()

    $ok = $n -gt 500
    if ($ok) { $alive++ } else { $dead += $x }
    Write-Output ("  x={0,5}  距右 {1,5:F1} DIP  diff={2,6}  {3}" -f $x, (($r.Right - $x) / $scale), $n, $(if ($ok) { 'YES' } else { 'no' }))
}

Write-Output ""
if ($dead.Count -eq 0) {
    Write-Output ("B 结论：整条最大化按钮都弹出 ✓（{0} 个位置全部 YES）" -f $alive)
} else {
    Write-Output ("B 结论：有死区 ✗ —— {0} 个位置不弹，最靠右那个在距窗口右边缘 {1:F1} DIP 处" -f `
        $dead.Count, (($r.Right - ($dead | Measure-Object -Maximum).Maximum) / $scale))
}

$base.Dispose()
[VerifySnap]::SetWindowPos($h, [VerifySnap]::NOTOPMOST, 0, 0, 0, 0, ([VerifySnap]::SWP_NOMOVE -bor [VerifySnap]::SWP_NOSIZE)) | Out-Null
Stop-Process -Id $proc.Id -Force
Write-Output '完成（进程已关闭）'








