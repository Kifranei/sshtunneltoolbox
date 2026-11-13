using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SshTunnelToolbox;

/// <summary>托盘菜单项（null 文本表示分隔线）。</summary>
internal sealed class TrayMenuItem
{
	public string Text;

	public bool Enabled = true;

	public bool Default;

	public Action Click;

	public List<TrayMenuItem> Children;

	public static TrayMenuItem Separator => new TrayMenuItem();

	public TrayMenuItem()
	{
	}

	public TrayMenuItem(string text, Action click = null, bool enabled = true)
	{
		Text = text;
		Click = click;
		Enabled = enabled;
	}
}

/// <summary>
/// 基于 Shell_NotifyIcon 的托盘图标。用一个隐藏的 Win32 窗口接收托盘消息（WinUI 的消息循环会分发它）。
/// 右键菜单用原生 Win32 弹出菜单，气泡通知用 NIF_INFO。
/// </summary>
internal sealed class TrayIcon : IDisposable
{
	private const uint WM_TRAY = 0x8000 + 0x21;
	private const uint WM_LBUTTONDBLCLK = 0x203, WM_RBUTTONUP = 0x205, WM_NULL = 0;

	private static readonly Win32.WndProc Proc = StaticWndProc;

	private static readonly Dictionary<IntPtr, TrayIcon> Instances = new Dictionary<IntPtr, TrayIcon>();

	private static bool _registered;

	private static uint _taskbarCreated;

	private readonly IntPtr _hwnd;

	private IntPtr _icon;

	private string _tip = "";

	private bool _added;

	private bool _disposed;

	public event Action DoubleClick;

	/// <summary>右键时调用，返回要显示的菜单。</summary>
	public Func<List<TrayMenuItem>> BuildMenu;

	public TrayIcon(string className, IntPtr icon, string tip)
	{
		IntPtr hInst = Win32.GetModuleHandleW(null);
		if (!_registered)
		{
			Win32.WNDCLASSEXW wc = new Win32.WNDCLASSEXW
			{
				cbSize = Marshal.SizeOf<Win32.WNDCLASSEXW>(),
				lpfnWndProc = Marshal.GetFunctionPointerForDelegate(Proc),
				hInstance = hInst,
				lpszClassName = className
			};
			Win32.RegisterClassExW(ref wc);
			_taskbarCreated = Win32.RegisterWindowMessageW("TaskbarCreated");
			_registered = true;
		}
		_hwnd = Win32.CreateWindowExW(0, className, className, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInst, IntPtr.Zero);
		Instances[_hwnd] = this;
		_icon = icon;
		_tip = tip ?? "";
		Add();
	}

	private Win32.NOTIFYICONDATAW Data(int flags)
	{
		return new Win32.NOTIFYICONDATAW
		{
			cbSize = Marshal.SizeOf<Win32.NOTIFYICONDATAW>(),
			hWnd = _hwnd,
			uID = 1,
			uFlags = flags,
			uCallbackMessage = (int)WM_TRAY,
			hIcon = _icon,
			szTip = _tip,
			szInfo = "",
			szInfoTitle = ""
		};
	}

	private void Add()
	{
		Win32.NOTIFYICONDATAW d = Data(Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP);
		_added = Win32.Shell_NotifyIconW(Win32.NIM_ADD, ref d);
	}

	public void SetIcon(IntPtr icon)
	{
		if (_disposed || icon == _icon)
		{
			return;
		}
		_icon = icon;
		Win32.NOTIFYICONDATAW d = Data(Win32.NIF_ICON);
		Win32.Shell_NotifyIconW(Win32.NIM_MODIFY, ref d);
	}

	public string Tip
	{
		get => _tip;
		set
		{
			value ??= "";
			if (value.Length > 127)
			{
				value = value.Substring(0, 127);
			}
			if (_disposed || value == _tip)
			{
				return;
			}
			_tip = value;
			Win32.NOTIFYICONDATAW d = Data(Win32.NIF_TIP);
			Win32.Shell_NotifyIconW(Win32.NIM_MODIFY, ref d);
		}
	}

	/// <param name="icon">Win32.NIIF_INFO / NIIF_WARNING / NIIF_ERROR</param>
	public void ShowBalloon(string title, string text, int icon)
	{
		if (_disposed)
		{
			return;
		}
		Win32.NOTIFYICONDATAW d = Data(Win32.NIF_INFO);
		d.szInfoTitle = title.Length > 63 ? title.Substring(0, 63) : title;
		d.szInfo = string.IsNullOrEmpty(text) ? " " : (text.Length > 255 ? text.Substring(0, 252) + "…" : text);
		d.dwInfoFlags = icon;
		d.uTimeoutOrVersion = 4000;
		Win32.Shell_NotifyIconW(Win32.NIM_MODIFY, ref d);
	}

	private static IntPtr StaticWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
	{
		if (Instances.TryGetValue(hWnd, out TrayIcon t))
		{
			try
			{
				if (msg == WM_TRAY)
				{
					uint m = (uint)(lParam.ToInt64() & 0xFFFF);
					if (m == WM_LBUTTONDBLCLK)
					{
						t.DoubleClick?.Invoke();
					}
					else if (m == WM_RBUTTONUP)
					{
						t.ShowMenu();
					}
					return IntPtr.Zero;
				}
				if (msg == _taskbarCreated && _taskbarCreated != 0)
				{
					t.Add();
					return IntPtr.Zero;
				}
			}
			catch
			{
			}
		}
		return Win32.DefWindowProcW(hWnd, msg, wParam, lParam);
	}

	private void ShowMenu()
	{
		List<TrayMenuItem> items = BuildMenu?.Invoke();
		if (items == null || items.Count == 0)
		{
			return;
		}
		Dictionary<int, Action> actions = new Dictionary<int, Action>();
		List<IntPtr> menus = new List<IntPtr>();
		int nextId = 1;
		IntPtr root = Build(items, actions, menus, ref nextId);
		try
		{
			Win32.GetCursorPos(out Win32.POINT pt);
			Win32.SetForegroundWindow(_hwnd);
			int cmd = Win32.TrackPopupMenuEx(root, Win32.TPM_RETURNCMD | Win32.TPM_RIGHTBUTTON | Win32.TPM_NONOTIFY | Win32.TPM_BOTTOMALIGN, pt.X, pt.Y, _hwnd, IntPtr.Zero);
			Win32.PostMessageW(_hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);
			if (cmd > 0 && actions.TryGetValue(cmd, out Action a))
			{
				a?.Invoke();
			}
		}
		finally
		{
			Win32.DestroyMenu(root);
		}
	}

	private static IntPtr Build(List<TrayMenuItem> items, Dictionary<int, Action> actions, List<IntPtr> menus, ref int nextId)
	{
		IntPtr m = Win32.CreatePopupMenu();
		menus.Add(m);
		foreach (TrayMenuItem it in items)
		{
			if (it.Text == null)
			{
				Win32.AppendMenuW(m, Win32.MF_SEPARATOR, IntPtr.Zero, null);
				continue;
			}
			uint flags = it.Enabled ? Win32.MF_STRING : (Win32.MF_STRING | Win32.MF_GRAYED);
			if (it.Children != null)
			{
				IntPtr sub = Build(it.Children, actions, menus, ref nextId);
				Win32.AppendMenuW(m, flags | Win32.MF_POPUP, sub, it.Text);
				continue;
			}
			int id = nextId++;
			actions[id] = it.Click;
			Win32.AppendMenuW(m, flags, (IntPtr)id, it.Text);
			if (it.Default)
			{
				Win32.SetMenuDefaultItem(m, (uint)id, 0);
			}
		}
		return m;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		if (_added)
		{
			Win32.NOTIFYICONDATAW d = Data(0);
			Win32.Shell_NotifyIconW(Win32.NIM_DELETE, ref d);
		}
		Instances.Remove(_hwnd);
		Win32.DestroyWindow(_hwnd);
	}
}
