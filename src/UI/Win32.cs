using System;
using System.Runtime.InteropServices;

namespace SshTunnelToolbox;

/// <summary>少量 Win32 P/Invoke：消息框、窗口图标、托盘、菜单、文件对话框。</summary>
internal static class Win32
{
	public const uint MB_OK = 0x0, MB_YESNO = 0x4, MB_ICONERROR = 0x10, MB_ICONWARNING = 0x30, MB_ICONINFORMATION = 0x40;
	public const int IDYES = 6;

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	public static extern uint GetDpiForWindow(IntPtr hWnd);

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	public static extern uint ExtractIconExW(string file, int index, out IntPtr large, out IntPtr small, uint count);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DestroyIcon(IntPtr hIcon);

	// ---------- 隐藏窗口（托盘消息） ----------
	public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct WNDCLASSEXW
	{
		public int cbSize;
		public uint style;
		public IntPtr lpfnWndProc;
		public int cbClsExtra;
		public int cbWndExtra;
		public IntPtr hInstance;
		public IntPtr hIcon;
		public IntPtr hCursor;
		public IntPtr hbrBackground;
		public string lpszMenuName;
		public string lpszClassName;
		public IntPtr hIconSm;
	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern ushort RegisterClassExW(ref WNDCLASSEXW wc);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr CreateWindowExW(int exStyle, string className, string windowName, int style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DestroyWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern uint RegisterWindowMessageW(string name);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr GetModuleHandleW(string name);

	// ---------- 托盘 ----------
	public const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
	public const int NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4, NIF_INFO = 0x10;
	public const int NIIF_NONE = 0, NIIF_INFO = 1, NIIF_WARNING = 2, NIIF_ERROR = 3;

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct NOTIFYICONDATAW
	{
		public int cbSize;
		public IntPtr hWnd;
		public int uID;
		public int uFlags;
		public int uCallbackMessage;
		public IntPtr hIcon;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string szTip;
		public int dwState;
		public int dwStateMask;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string szInfo;
		public int uTimeoutOrVersion;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
		public string szInfoTitle;
		public int dwInfoFlags;
		public Guid guidItem;
		public IntPtr hBalloonIcon;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool Shell_NotifyIconW(int message, ref NOTIFYICONDATAW data);

	// ---------- 菜单 ----------
	public const uint MF_STRING = 0x0, MF_GRAYED = 0x1, MF_POPUP = 0x10, MF_SEPARATOR = 0x800;
	public const uint TPM_RIGHTBUTTON = 0x2, TPM_RETURNCMD = 0x100, TPM_NONOTIFY = 0x80, TPM_BOTTOMALIGN = 0x20;

	[DllImport("user32.dll")]
	public static extern IntPtr CreatePopupMenu();

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool AppendMenuW(IntPtr hMenu, uint flags, IntPtr idOrSub, string text);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool SetMenuDefaultItem(IntPtr hMenu, uint item, uint byPos);

	[DllImport("user32.dll")]
	public static extern int TrackPopupMenuEx(IntPtr hMenu, uint flags, int x, int y, IntPtr hWnd, IntPtr tpm);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool DestroyMenu(IntPtr hMenu);

	[StructLayout(LayoutKind.Sequential)]
	public struct POINT
	{
		public int X;
		public int Y;
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetCursorPos(out POINT pt);

	// ---------- 文件对话框（comdlg32，管理员进程中也能用） ----------
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct OPENFILENAMEW
	{
		public int lStructSize;
		public IntPtr hwndOwner;
		public IntPtr hInstance;
		public string lpstrFilter;
		public IntPtr lpstrCustomFilter;
		public int nMaxCustFilter;
		public int nFilterIndex;
		public IntPtr lpstrFile;
		public int nMaxFile;
		public IntPtr lpstrFileTitle;
		public int nMaxFileTitle;
		public string lpstrInitialDir;
		public string lpstrTitle;
		public int Flags;
		public short nFileOffset;
		public short nFileExtension;
		public string lpstrDefExt;
		public IntPtr lCustData;
		public IntPtr lpfnHook;
		public IntPtr lpTemplateName;
		public IntPtr pvReserved;
		public int dwReserved;
		public int FlagsEx;
	}

	[DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetOpenFileNameW(ref OPENFILENAMEW ofn);

	[DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool GetSaveFileNameW(ref OPENFILENAMEW ofn);
}
