using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace SshTunnelToolbox;

/// <summary>
/// 窗口外观：自定义标题栏、背景材质（Win11 云母 Mica / Win10 亚克力 Acrylic / 都不支持时纯色）、
/// 浅色 / 深色 / 跟随系统主题，以及标题栏按钮颜色。
/// </summary>
internal static class WindowChrome
{
	/// <summary>当前使用的背景材质：Mica / Acrylic / Solid。</summary>
	public static string BackdropKind { get; private set; } = "";

	private static IntPtr _iconLarge;

	public static IntPtr Hwnd(Window w) => WindowNative.GetWindowHandle(w);

	public static double Scale(Window w)
	{
		uint dpi = Win32.GetDpiForWindow(Hwnd(w));
		return dpi == 0 ? 1.0 : dpi / 96.0;
	}

	/// <summary>标题栏：图标 + 标题文字。返回整个标题栏元素（用于 SetTitleBar）。</summary>
	public static Grid BuildTitleBar(string title, out TextBlock titleText)
	{
		Grid bar = new Grid { Height = 32, Background = new SolidColorBrush(Colors.Transparent) };
		StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0), Spacing = 10 };
		Image img = new Image { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
		LoadIconInto(img);
		titleText = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Center, Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"] };
		sp.Children.Add(img);
		sp.Children.Add(titleText);
		bar.Children.Add(sp);
		return bar;
	}

	/// <summary>窗口初始化：标题栏、材质、主题、图标、初始大小和最小大小（单位：DIP）。</summary>
	public static void Setup(Window w, FrameworkElement root, UIElement titleBar, string theme, double width, double height, double minWidth, double minHeight)
	{
		w.ExtendsContentIntoTitleBar = true;
		if (titleBar != null)
		{
			w.SetTitleBar(titleBar);
		}
		ApplyBackdrop(w, root);
		ApplyTheme(root, theme);
		root.ActualThemeChanged += (s, e) => OnThemeChanged(w, root);
		OnThemeChanged(w, root);
		SetWindowIcon(w);
		double scale = Scale(w);
		try
		{
			DisplayArea area = DisplayArea.GetFromWindowId(w.AppWindow.Id, DisplayAreaFallback.Primary);
			RectInt32 wa = area.WorkArea;
			int pw = Math.Min((int)(width * scale), wa.Width);
			int ph = Math.Min((int)(height * scale), wa.Height);
			w.AppWindow.MoveAndResize(new RectInt32(wa.X + (wa.Width - pw) / 2, wa.Y + (wa.Height - ph) / 2, pw, ph));
			if (w.AppWindow.Presenter is OverlappedPresenter op)
			{
				op.PreferredMinimumWidth = Math.Min((int)(minWidth * scale), wa.Width);
				op.PreferredMinimumHeight = Math.Min((int)(minHeight * scale), wa.Height);
			}
		}
		catch
		{
		}
	}

	/// <summary>Win11：MicaBackdrop；Win10（不支持 Mica）：DesktopAcrylicBackdrop；都不支持：纯色背景。</summary>
	public static void ApplyBackdrop(Window w, FrameworkElement root)
	{
		try
		{
			if (MicaController.IsSupported())
			{
				w.SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
				BackdropKind = "Mica";
				return;
			}
			if (DesktopAcrylicController.IsSupported())
			{
				w.SystemBackdrop = new DesktopAcrylicBackdrop();
				BackdropKind = "Acrylic";
				return;
			}
		}
		catch
		{
		}
		BackdropKind = "Solid";
		UpdateSolidBackground(root);
	}

	public static ElementTheme ToElementTheme(string theme)
	{
		return theme switch
		{
			"light" => ElementTheme.Light,
			"dark" => ElementTheme.Dark,
			_ => ElementTheme.Default
		};
	}

	public static string NormalizeTheme(string theme)
	{
		return theme == "light" || theme == "dark" ? theme : "system";
	}

	public static string ThemeLabel(string theme)
	{
		return NormalizeTheme(theme) switch
		{
			"light" => "浅色",
			"dark" => "深色",
			_ => "跟随系统"
		};
	}

	/// <summary>通过根元素的 RequestedTheme 切换主题；材质（Mica/Acrylic）会跟着根元素的实际主题变化。</summary>
	public static void ApplyTheme(FrameworkElement root, string theme)
	{
		root.RequestedTheme = ToElementTheme(theme);
	}

	private static void OnThemeChanged(Window w, FrameworkElement root)
	{
		bool dark = root.ActualTheme == ElementTheme.Dark;
		UpdateCaptionButtons(w, dark);
		SetDarkFrame(w, dark);
		if (BackdropKind == "Solid")
		{
			UpdateSolidBackground(root);
		}
	}

	private static void UpdateSolidBackground(FrameworkElement root)
	{
		if (root is Panel p)
		{
			bool dark = root.ActualTheme == ElementTheme.Dark;
			p.Background = new SolidColorBrush(dark ? Color.FromArgb(255, 0x20, 0x20, 0x20) : Color.FromArgb(255, 0xF3, 0xF3, 0xF3));
		}
	}

	/// <summary>标题栏右上角最小化 / 最大化 / 关闭按钮的颜色跟随主题。</summary>
	public static void UpdateCaptionButtons(Window w, bool dark)
	{
		try
		{
			AppWindowTitleBar tb = w.AppWindow.TitleBar;
			Color fg = dark ? Colors.White : Colors.Black;
			tb.ButtonBackgroundColor = Colors.Transparent;
			tb.ButtonInactiveBackgroundColor = Colors.Transparent;
			tb.ButtonForegroundColor = fg;
			tb.ButtonInactiveForegroundColor = dark ? Color.FromArgb(255, 0x8A, 0x8A, 0x8A) : Color.FromArgb(255, 0x8A, 0x8A, 0x8A);
			tb.ButtonHoverForegroundColor = fg;
			tb.ButtonHoverBackgroundColor = dark ? Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x12, 0x00, 0x00, 0x00);
			tb.ButtonPressedForegroundColor = fg;
			tb.ButtonPressedBackgroundColor = dark ? Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0x0A, 0x00, 0x00, 0x00);
		}
		catch
		{
		}
	}

	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

	private static void SetDarkFrame(Window w, bool dark)
	{
		try
		{
			int v = dark ? 1 : 0;
			DwmSetWindowAttribute(Hwnd(w), 20, ref v, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
		}
		catch
		{
		}
	}

	public static void SetWindowIcon(Window w)
	{
		try
		{
			if (_iconLarge == IntPtr.Zero)
			{
				Win32.ExtractIconExW(Environment.ProcessPath, 0, out _iconLarge, out IntPtr small, 1);
				if (small != IntPtr.Zero)
				{
					Win32.DestroyIcon(small);
				}
			}
			if (_iconLarge != IntPtr.Zero)
			{
				w.AppWindow.SetIcon(Win32Interop.GetIconIdFromIcon(_iconLarge));
			}
		}
		catch
		{
		}
	}

	public static Stream OpenIconStream()
	{
		Assembly asm = Assembly.GetExecutingAssembly();
		string name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase));
		return name == null ? null : asm.GetManifestResourceStream(name);
	}

	private static async void LoadIconInto(Image img)
	{
		try
		{
			using Stream s = OpenIconStream();
			if (s == null)
			{
				return;
			}
			MemoryStream ms = new MemoryStream();
			await s.CopyToAsync(ms);
			ms.Position = 0;
			BitmapImage bmp = new BitmapImage { DecodePixelWidth = 32, DecodePixelType = DecodePixelType.Physical };
			await bmp.SetSourceAsync(ms.AsRandomAccessStream());
			img.Source = bmp;
		}
		catch
		{
		}
	}

	/// <summary>把窗口带到前台。</summary>
	public static void BringToFront(Window w)
	{
		try
		{
			w.AppWindow.Show(true);
			if (w.AppWindow.Presenter is OverlappedPresenter op && op.State == OverlappedPresenterState.Minimized)
			{
				op.Restore(true);
			}
			w.Activate();
			Win32.SetForegroundWindow(Hwnd(w));
		}
		catch
		{
		}
	}
}
