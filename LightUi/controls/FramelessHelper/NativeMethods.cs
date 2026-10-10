using System.Runtime.InteropServices;

namespace LightUi.Controls.Frameless;

/// <summary>
/// Win32 的常量、结构体和 P/Invoke，集中一处。
///
/// 对应 Chromium 里散落的 <c>ui/views/win/*</c> + <c>ui/base/win/hwnd_metrics.cc</c> 用到的那些定义；
/// 这里只是把用得到的部分收拢起来，值都用 <c>Winuser.h</c> / <c>Dwmapi.h</c> 的原始编号。
/// </summary>
internal static class NativeMethods
{
    #region 消息

    public const int WM_NCCALCSIZE = 0x0083;
    public const int WM_NCHITTEST = 0x0084;
    public const int WM_NCACTIVATE = 0x0086;
    public const int WM_NCPAINT = 0x0085;
    public const int WM_NCMOUSEMOVE = 0x00A0;
    public const int WM_NCLBUTTONDOWN = 0x00A1;
    public const int WM_NCLBUTTONUP = 0x00A2;
    public const int WM_NCLBUTTONDBLCLK = 0x00A3;
    public const int WM_NCRBUTTONUP = 0x00A5;
    public const int WM_NCMOUSELEAVE = 0x02A2;
    public const int WM_SIZE = 0x0005;
    public const int WM_GETMINMAXINFO = 0x0024;
    public const int WM_WINDOWPOSCHANGED = 0x0047;
    public const int WM_SYSCOMMAND = 0x0112;
    public const int WM_DPICHANGED = 0x02E0;
    public const int WM_SETTINGCHANGE = 0x001A;
    public const int WM_THEMECHANGED = 0x031A;
    public const int WM_DWMCOMPOSITIONCHANGED = 0x031E;

    // 下面两条是「系统偷偷画原生标题栏」的私人消息（没进公开头文件）。
    // Chromium 专门吃掉它们，否则窗口在激活/尺寸变化时会闪一下原生标题栏。
    public const int WM_NCUAHDRAWCAPTION = 0x00AE;
    public const int WM_NCUAHDRAWFRAME = 0x00AF;

    #endregion

    #region 命中测试返回值（MS 文档已核对）

    public const int HTCLIENT = 1;
    public const int HTCAPTION = 2;
    public const int HTMINBUTTON = 8;
    public const int HTMAXBUTTON = 9;
    public const int HTLEFT = 10;
    public const int HTRIGHT = 11;
    public const int HTTOP = 12;
    public const int HTTOPLEFT = 13;
    public const int HTTOPRIGHT = 14;
    public const int HTBOTTOM = 15;
    public const int HTBOTTOMLEFT = 16;
    public const int HTBOTTOMRIGHT = 17;
    public const int HTCLOSE = 20;

    /// <summary>本工程用 0 表示「不接管这条 WM_NCHITTEST，交回 WPF」。</summary>
    public const int HTNotHandled = 0;

    #endregion

    #region 系统度量

    /// <summary>可缩放窗口的边框厚度（水平方向，像素）。</summary>
    public const int SM_CXSIZEFRAME = 32;

    /// <summary>可缩放窗口的边框厚度（垂直方向，像素）。本工程未用到。</summary>
    public const int SM_CYSIZEFRAME = 33;

    /// <summary>边框额外追加的宽度（像素）；与 <c>SM_CXSIZEFRAME</c> 相加就是那圈「不可见边框」。</summary>
    public const int SM_CXPADDEDBORDER = 92;

    /// <summary>标题栏高度（像素）；<c>WinFrameView.TitlebarMaximizedVisualHeight()</c> 读它。</summary>
    public const int SM_CYCAPTION = 4;

    /// <summary><c>MonitorFromWindow</c> 的标志：取离窗口最近的那块显示器。本工程未用到。</summary>
    public const uint MONITOR_DEFAULTTONEAREST = 2;

    /// <summary>DWM 属性：窗口的<b>可见边框</b>；<c>GetWindowRect</c> 减掉它才是「不可见边框」。</summary>
    public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    /// <summary>DWM 属性：系统认为的标题栏按钮区（窗口坐标）。</summary>
    public const int DWMWA_CAPTION_BUTTON_BOUNDS = 5;

    /// <summary>GetWindowLong 的索引：窗口样式位（GWLP_ 系列之一）。</summary>
    public const int GWL_STYLE = -16;

    /// <summary>
    /// DWM 属性：把窗口「隐身」—— 用户看不见，但窗口照常被 DWM 合成。
    /// 文档原文："Cloaks the window such that it is not visible to the user. The window is still composed by DWM."
    /// ★ 只能由窗口自己的进程设（跨进程调用会返回 E_ACCESSDENIED）。
    /// </summary>
    public const int DWMWA_CLOAK = 13;

    /// <summary>DWM 属性（只读）：当前是否被隐身、以及是谁隐的（1 = 属主应用，DWM_CLOAKED_APP）。</summary>
    public const int DWMWA_CLOAKED = 14;

    #endregion

    #region SetWindowPos 标志

    /// <summary>按新样式重算非客户区 —— 即使尺寸没变，也会发一次 <c>WM_NCCALCSIZE</c>。</summary>
    public const uint SWP_FRAMECHANGED = 0x0020;

    /// <summary>不改尺寸。</summary>
    public const uint SWP_NOSIZE = 0x0001;

    /// <summary>不改位置。</summary>
    public const uint SWP_NOMOVE = 0x0002;

    /// <summary>不改 Z 序。</summary>
    public const uint SWP_NOZORDER = 0x0004;

    /// <summary>不激活窗口。</summary>
    public const uint SWP_NOACTIVATE = 0x0010;

    #endregion

    #region TrackPopupMenuEx 标志

    /// <summary>选中的命令当返回值给我（不给我发 WM_COMMAND）。</summary>
    public const uint TPM_RETURNCMD = 0x0100;

    /// <summary>左右键都响应。</summary>
    public const uint TPM_RIGHTBUTTON = 0x0002;

    #endregion

    #region 结构体

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>WM_NCCALCSIZE 且 wParam=TRUE 时 lParam 指向的结构。只关心 rgrc[0]（提议的客户区）。</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NCCALCSIZE_PARAMS
    {
        public RECT Rgrc0;
        public RECT Rgrc1;
        public RECT Rgrc2;
        public IntPtr WindowPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int CbSize;
        public RECT RcMonitor;
        public RECT RcWork;
        public uint DwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int LeftWidth;
        public int RightWidth;
        public int TopHeight;
        public int BottomHeight;
    }

    #endregion

    #region P/Invoke

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetricsForDpi(int index, uint dpi);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(
        IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);

    [DllImport("user32.dll")]
    public static extern IntPtr GetSystemMenu(IntPtr hwnd, bool revert);

    [DllImport("user32.dll")]
    public static extern int TrackPopupMenuEx(
        IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr parameters);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

    #endregion
}
