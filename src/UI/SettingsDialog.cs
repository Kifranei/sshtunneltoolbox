using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SshTunnelToolbox;

/// <summary>设置（所有配置共用）：plink 路径、托盘行为、开机自启、气泡通知、主题。</summary>
public sealed class SettingsDialog
{
	/// <summary>实际显示的对话框（用组合而不是继承 ContentDialog，单文件发布时自定义 XAML 派生类型会导致崩溃）。</summary>
	public ContentDialog Dialog { get; } = new ContentDialog();

	public static readonly string[] CloseKeys = { "ask", "tray", "exit" };

	public static readonly string[] CloseTexts = { "每次询问", "最小化到托盘", "直接退出" };

	public static readonly string[] ThemeKeys = { "system", "light", "dark" };

	public static readonly string[] ThemeTexts = { "跟随系统", "浅色", "深色" };

	private readonly TextBox _plink;

	private readonly ComboBox _close, _theme;

	private readonly CheckBox _minToTray, _startHidden, _autoStart, _balloons;

	private readonly TextBlock _plinkMsg;

	public string OriginalTheme { get; }

	public string PlinkPath => _plink.Text.Trim();

	public string CloseAction => CloseKeys[Math.Max(0, _close.SelectedIndex)];

	public string Theme => ThemeKeys[Math.Max(0, _theme.SelectedIndex)];

	public bool MinimizeToTray => _minToTray.IsChecked == true;

	public bool StartHidden => _startHidden.IsChecked == true;

	public bool AutoStartOn => _autoStart.IsChecked == true;

	public bool Balloons => _balloons.IsChecked == true;

	public SettingsDialog(MainWindow main, AppConfig cfg, bool autoStart)
	{
		Dialog.Title = "设置（所有配置共用）";
		Dialog.PrimaryButtonText = "确定";
		Dialog.CloseButtonText = "取消";
		Dialog.DefaultButton = ContentDialogButton.Primary;
		OriginalTheme = WindowChrome.NormalizeTheme(cfg.Theme);

		StackPanel sp = new StackPanel { Spacing = 10, MinWidth = 480 };
		sp.Children.Add(X.Header("plink.exe（PuTTY）"));
		_plink = new TextBox { Text = cfg.PlinkPath ?? "" };
		Button browse = X.Btn("浏览…", (s, e) =>
		{
			string f = FileDialogs.Open(main.Hwnd, "选择 plink.exe", "plink.exe|plink.exe|程序 (*.exe)|*.exe", PlinkPath);
			if (f != null)
			{
				_plink.Text = f;
			}
		});
		_plinkMsg = X.Secondary("", wrap: true);
		_plinkMsg.Visibility = Visibility.Collapsed;
		Button detect = X.Btn("自动查找", (s, e) =>
		{
			string f = Plink.Detect();
			if (f != null)
			{
				_plink.Text = f;
				_plinkMsg.Text = "已找到：" + f;
			}
			else
			{
				_plinkMsg.Text = "没有找到 plink.exe（已查找 C:\\Program Files\\PuTTY、C:\\Program Files (x86)\\PuTTY 和 PATH）。";
			}
			_plinkMsg.Visibility = Visibility.Visible;
		});
		sp.Children.Add(X.Row((X.Star(), _plink), (X.Auto, browse), (X.Auto, detect)));
		sp.Children.Add(_plinkMsg);

		sp.Children.Add(Spacer());
		sp.Children.Add(X.Header("托盘"));
		_close = new ComboBox { MinWidth = 180 };
		foreach (string t in CloseTexts)
		{
			_close.Items.Add(t);
		}
		_close.SelectedIndex = Math.Max(0, Array.IndexOf(CloseKeys, cfg.CloseAction));
		sp.Children.Add(X.Row((X.Auto, X.Label("关闭窗口时：")), (X.Auto, _close)));
		_minToTray = Chk("最小化到托盘（不勾选：普通最小化到任务栏）", cfg.MinimizeToTray);
		_startHidden = Chk("启动时隐藏到托盘", cfg.StartHidden);
		_autoStart = Chk("开机自启（当前用户，无需管理员；启动后直接在托盘运行）", autoStart);
		_balloons = Chk("显示气泡通知（连接 / 断开 / 重连；连续失败不会反复弹出）", cfg.Balloons);
		sp.Children.Add(_minToTray);
		sp.Children.Add(_startHidden);
		sp.Children.Add(_autoStart);
		sp.Children.Add(_balloons);

		sp.Children.Add(Spacer());
		sp.Children.Add(X.Header("外观"));
		_theme = new ComboBox { MinWidth = 180 };
		foreach (string t in ThemeTexts)
		{
			_theme.Items.Add(t);
		}
		_theme.SelectedIndex = Math.Max(0, Array.IndexOf(ThemeKeys, OriginalTheme));
		_theme.SelectionChanged += (s, e) =>
		{
			main.PreviewTheme(Theme);
			Dialog.RequestedTheme = WindowChrome.ToElementTheme(Theme) == ElementTheme.Default ? ((FrameworkElement)Dialogs.Root).ActualTheme : WindowChrome.ToElementTheme(Theme);
		};
		sp.Children.Add(X.Row((X.Auto, X.Label("主题：")), (X.Auto, _theme)));
		sp.Children.Add(X.Secondary("背景材质：" + (WindowChrome.BackdropKind switch { "Mica" => "云母 Mica（Windows 11）", "Acrylic" => "亚克力 Acrylic（Windows 10）", _ => "纯色（系统不支持 Mica / Acrylic）" }), wrap: true));

		Dialog.Content = new ScrollViewer { Content = sp, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
	}

	private static CheckBox Chk(string text, bool value)
	{
		return new CheckBox { Content = text, IsChecked = value };
	}

	private static FrameworkElement Spacer()
	{
		return new Border { Height = 4 };
	}
}
