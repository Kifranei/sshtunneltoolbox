using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Ellipse = Microsoft.UI.Xaml.Shapes.Ellipse;
using Windows.ApplicationModel.DataTransfer;
using Color = Windows.UI.Color;
using Icon = System.Drawing.Icon;

namespace SshTunnelToolbox;

public sealed class MainWindow : Window
{
	private AppConfig _cfg;

	private string _loadWarn;

	private readonly Grid _root;

	private readonly TabView _tabs;

	private TrayIcon _tray;

	private readonly Dictionary<TunnelState, IntPtr> _icons = new Dictionary<TunnelState, IntPtr>();

	private readonly DispatcherQueueTimer _timer;

	private readonly bool _hideOnStart;

	private bool _dataInit;

	private bool _exiting;

	private bool _balloonShown;

	private bool _activated;

	private bool _hidden;

	private bool _closingAsk;

	private readonly List<RadioMenuFlyoutItem> _themeItems = new List<RadioMenuFlyoutItem>();

	public static readonly Color CGreen = Color.FromArgb(255, 0x10, 0xA0, 0x3C);

	public static readonly Color CYellow = Color.FromArgb(255, 0xE6, 0xAA, 0x00);

	public static readonly Color CGray = Color.FromArgb(255, 0x96, 0x96, 0x96);

	private readonly Dictionary<TabViewItem, ProfileView> _viewOf = new Dictionary<TabViewItem, ProfileView>();

	public IEnumerable<ProfileView> Views => _tabs.TabItems.OfType<TabViewItem>().Select(t => _viewOf[t]);

	private ProfileView CurrentView => _tabs.SelectedItem is TabViewItem t && _viewOf.TryGetValue(t, out ProfileView v) ? v : null;

	public bool IsShown => _activated && !_hidden && !IsMinimized;

	private bool IsMinimized => AppWindow.Presenter is OverlappedPresenter op && op.State == OverlappedPresenterState.Minimized;

	public MainWindow(bool trayArg)
	{
		Title = Program.AppTitle;
		_cfg = AppConfig.Load(out _loadWarn);
		_root = new Grid();
		_root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		_root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		_root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		Grid titleBar = WindowChrome.BuildTitleBar(Program.AppTitle, out _);
		_root.Children.Add(titleBar);

		CommandBar bar = BuildCommandBar();
		Grid.SetRow(bar, 1);
		_root.Children.Add(bar);

		_tabs = new TabView
		{
			IsAddTabButtonVisible = true,
			CanDragTabs = false,
			CanReorderTabs = false,
			TabWidthMode = TabViewWidthMode.SizeToContent,
			CloseButtonOverlayMode = TabViewCloseButtonOverlayMode.OnPointerOver,
			Margin = new Thickness(0, 4, 0, 0)
		};
		_tabs.AddTabButtonClick += (s, e) => NewProfile();
		_tabs.SelectionChanged += (s, e) => CurrentView?.UpdateStatus();
		Grid.SetRow(_tabs, 2);
		_root.Children.Add(_tabs);
		Content = _root;

		Dialogs.Root = _root;
		Dialogs.DefaultTitle = Program.AppTitle;
		Dialogs.EnsureVisible = () =>
		{
			if (!IsShown)
			{
				ShowFromTray();
			}
		};
		WindowChrome.Setup(this, _root, titleBar, _cfg.Theme, 900, 800, 760, 640);
		SyncThemeMenu();

		foreach (TunnelState st in Enum.GetValues(typeof(TunnelState)))
		{
			_icons[st] = MakeTrayIcon(ColorOf(st));
		}
		_tray = new TrayIcon("SshTunnelToolboxTray", _icons[TunnelState.Stopped], "SSH 隧道：未运行");
		_tray.DoubleClick += ToggleWindow;
		_tray.BuildMenu = BuildTrayMenu;

		foreach (Profile p in _cfg.Profiles)
		{
			AddView(p, select: false);
		}
		if (_tabs.TabItems.Count > 0)
		{
			_tabs.SelectedIndex = Math.Max(0, Math.Min(_cfg.SelectedProfile, _tabs.TabItems.Count - 1));
		}
		_hideOnStart = trayArg || _cfg.StartHidden;

		_timer = DispatcherQueue.CreateTimer();
		_timer.Interval = TimeSpan.FromSeconds(1);
		_timer.Tick += (s, e) =>
		{
			foreach (ProfileView v in Views)
			{
				v.UpdateStatus();
			}
			UpdateTray();
		};
		_timer.Start();

		AppWindow.Closing += OnClosing;
		AppWindow.Changed += (s, e) =>
		{
			if (e.DidPresenterChange || e.DidSizeChange || e.DidVisibilityChange)
			{
				if (IsMinimized && _cfg.MinimizeToTray && !_hidden && _activated)
				{
					HideToTray();
				}
			}
		};
		Closed += (s, e) => Cleanup();
	}

	/// <summary>启动：正常显示窗口，或（--tray / 启动时隐藏）只在托盘中运行。</summary>
	public void Start()
	{
		if (_hideOnStart)
		{
			_hidden = true;
			InitData(hidden: true);
		}
		else
		{
			_activated = true;
			Activate();
			InitData(hidden: false);
		}
	}

	// ---------------- 顶部命令栏 ----------------

	private CommandBar BuildCommandBar()
	{
		CommandBar bar = new CommandBar
		{
			DefaultLabelPosition = CommandBarDefaultLabelPosition.Right,
			HorizontalAlignment = HorizontalAlignment.Left,
			Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
			Margin = new Thickness(4, 0, 4, 0),
			IsDynamicOverflowEnabled = true
		};
		bar.PrimaryCommands.Add(Cmd("新建配置", "\uE710", NewProfile));
		bar.PrimaryCommands.Add(Cmd("复制当前", "\uE8C8", CopyCurrent));
		bar.PrimaryCommands.Add(Cmd("重命名", "\uE8AC", RenameCurrent));
		bar.PrimaryCommands.Add(Cmd("删除", "\uE74D", DeleteCurrent));
		bar.PrimaryCommands.Add(Cmd("左移", "\uE72B", () => MoveCurrent(-1), iconOnly: true));
		bar.PrimaryCommands.Add(Cmd("右移", "\uE72A", () => MoveCurrent(1), iconOnly: true));
		bar.PrimaryCommands.Add(new AppBarSeparator());
		bar.PrimaryCommands.Add(Cmd("全部启动", "\uE768", StartAll));
		bar.PrimaryCommands.Add(Cmd("全部停止", "\uE71A", StopAll));
		bar.PrimaryCommands.Add(Cmd("保存全部", "\uE74E", () => SaveAll(showMsg: true)));
		bar.PrimaryCommands.Add(new AppBarSeparator());
		AppBarButton theme = new AppBarButton { Label = "主题", Icon = new FontIcon { Glyph = "\uE790" }, LabelPosition = CommandBarLabelPosition.Collapsed };
		ToolTipService.SetToolTip(theme, "主题：跟随系统 / 浅色 / 深色");
		MenuFlyout tf = new MenuFlyout();
		foreach ((string key, string label) in new[] { ("system", "跟随系统"), ("light", "浅色"), ("dark", "深色") })
		{
			RadioMenuFlyoutItem it = new RadioMenuFlyoutItem { Text = label, GroupName = "theme", Tag = key };
			it.Click += (s, e) => SetTheme(key);
			tf.Items.Add(it);
			_themeItems.Add(it);
		}
		theme.Flyout = tf;
		bar.PrimaryCommands.Add(theme);
		bar.PrimaryCommands.Add(Cmd("设置…", "\uE713", () => _ = OpenSettings()));
		return bar;
	}

	private static AppBarButton Cmd(string label, string glyph, Action a, bool iconOnly = false)
	{
		AppBarButton b = new AppBarButton { Label = label, Icon = new FontIcon { Glyph = glyph } };
		if (iconOnly)
		{
			b.LabelPosition = CommandBarLabelPosition.Collapsed;
			ToolTipService.SetToolTip(b, label);
		}
		b.Click += (s, e) => a();
		return b;
	}

	public void SetTheme(string theme)
	{
		_cfg.Theme = WindowChrome.NormalizeTheme(theme);
		WindowChrome.ApplyTheme(_root, _cfg.Theme);
		SyncThemeMenu();
		try
		{
			_cfg.Save();
		}
		catch
		{
		}
	}

	private void SyncThemeMenu()
	{
		string t = WindowChrome.NormalizeTheme(_cfg.Theme);
		foreach (RadioMenuFlyoutItem it in _themeItems)
		{
			it.IsChecked = (string)it.Tag == t;
		}
	}

	// ---------------- 状态颜色 / 托盘图标 ----------------

	public static Color ColorOf(TunnelState s)
	{
		switch (s)
		{
		case TunnelState.Connected:
			return CGreen;
		case TunnelState.Connecting:
		case TunnelState.Reconnecting:
			return CYellow;
		default:
			return CGray;
		}
	}

	private static IntPtr MakeTrayIcon(Color c)
	{
		try
		{
			int sz = 32;
			using Stream s = WindowChrome.OpenIconStream();
			using Icon baseIcon = s != null ? new Icon(s, sz, sz) : (Icon)SystemIcons.Application.Clone();
			using Bitmap bmp = new Bitmap(sz, sz);
			using (Graphics g = Graphics.FromImage(bmp))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.InterpolationMode = InterpolationMode.HighQualityBicubic;
				g.Clear(System.Drawing.Color.Transparent);
				g.DrawIcon(baseIcon, new Rectangle(0, 0, sz, sz));
				float d = sz * 0.5f;
				RectangleF r = new RectangleF(sz - d - 0.5f, sz - d - 0.5f, d, d);
				using SolidBrush br = new SolidBrush(System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B));
				g.FillEllipse(br, r);
				using Pen pen = new Pen(System.Drawing.Color.White, Math.Max(1f, sz / 16f));
				g.DrawEllipse(pen, r);
			}
			return bmp.GetHicon();
		}
		catch
		{
			return IntPtr.Zero;
		}
	}

	// ---------------- 配置标签页 ----------------

	private TabViewItem PageOf(ProfileView v)
	{
		return _tabs.TabItems.OfType<TabViewItem>().FirstOrDefault(t => ReferenceEquals(_viewOf[t], v));
	}

	private ProfileView AddView(Profile p, bool select)
	{
		ProfileView v = new ProfileView(this, p);
		Ellipse dot = new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(CGray), VerticalAlignment = VerticalAlignment.Center };
		TextBlock name = new TextBlock { Text = p.Name, VerticalAlignment = VerticalAlignment.Center };
		StackPanel header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		header.Children.Add(dot);
		header.Children.Add(name);
		TabViewItem page = new TabViewItem { Header = header, IsClosable = false, Content = v.Element };
		_viewOf[page] = v;
		v.HeaderDot = dot;
		v.HeaderText = name;
		page.DoubleTapped += (s, e) =>
		{
			_tabs.SelectedItem = page;
			RenameCurrent();
		};
		MenuFlyout menu = new MenuFlyout();
		menu.Items.Add(MenuItem("新建配置", NewProfile));
		menu.Items.Add(MenuItem("复制当前", CopyCurrent));
		menu.Items.Add(MenuItem("重命名…", RenameCurrent));
		menu.Items.Add(MenuItem("删除", DeleteCurrent));
		menu.Items.Add(new MenuFlyoutSeparator());
		menu.Items.Add(MenuItem("左移", () => MoveCurrent(-1)));
		menu.Items.Add(MenuItem("右移", () => MoveCurrent(1)));
		menu.Opening += (s, e) => _tabs.SelectedItem = page;
		page.ContextFlyout = menu;
		v.StatusChanged += () => UpdateTab(page, v);
		_tabs.TabItems.Add(page);
		if (select)
		{
			_tabs.SelectedItem = page;
		}
		UpdateTab(page, v);
		return v;
	}

	private static MenuFlyoutItem MenuItem(string text, Action a)
	{
		MenuFlyoutItem it = new MenuFlyoutItem { Text = text };
		it.Click += (s, e) => a();
		return it;
	}

	private void UpdateTab(TabViewItem page, ProfileView v)
	{
		if (v.HeaderDot != null)
		{
			Color c = ColorOf(v.State);
			if (!(v.HeaderDot.Fill is SolidColorBrush b) || b.Color != c)
			{
				v.HeaderDot.Fill = new SolidColorBrush(c);
			}
		}
		if (v.HeaderText != null && v.HeaderText.Text != v.ProfileName)
		{
			v.HeaderText.Text = v.ProfileName;
		}
		string tip = v.ProfileName + "：" + v.ShortStatus;
		if (!(ToolTipService.GetToolTip(page) is string old) || old != tip)
		{
			ToolTipService.SetToolTip(page, tip);
		}
	}

	public void ShowProfile(ProfileView v)
	{
		ShowFromTray();
		TabViewItem page = PageOf(v);
		if (page != null)
		{
			_tabs.SelectedItem = page;
		}
	}

	public void LogGlobal(string msg, LogLevel lv)
	{
		(CurrentView ?? Views.FirstOrDefault())?.Log(msg, lv);
	}

	private string UniqueName(string baseName, ProfileView except)
	{
		HashSet<string> names = new HashSet<string>(Views.Where(v => v != except).Select(v => v.ProfileName), StringComparer.OrdinalIgnoreCase);
		if (!names.Contains(baseName))
		{
			return baseName;
		}
		int n = 2;
		string t;
		while (names.Contains(t = baseName + " (" + n + ")"))
		{
			n++;
		}
		return t;
	}

	private string ValidateName(string name, ProfileView except)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return "名称不能为空";
		}
		if (name.Length > 40)
		{
			return "名称太长（最多 40 个字）";
		}
		if (Views.Any(v => v != except && string.Equals(v.ProfileName, name, StringComparison.OrdinalIgnoreCase)))
		{
			return "已有同名配置：" + name;
		}
		return null;
	}

	private async void NewProfile()
	{
		string name = await Dialogs.Input("新建配置", "配置名称（例如「办公室」「机房 A」）：", UniqueName("新配置", null), n => ValidateName(n, null));
		if (name == null)
		{
			return;
		}
		Profile p = new Profile { Name = name, Port = 22, ReconnectDelaySeconds = 5 };
		ProfileView v = AddView(p, select: true);
		SaveAll(showMsg: false);
		v.Log("已新建配置「" + p.Name + "」：请填写主机、用户名、密码、主机指纹，并添加端口转发。", LogLevel.Info);
	}

	private async void CopyCurrent()
	{
		ProfileView cur = CurrentView;
		if (cur == null)
		{
			return;
		}
		cur.Commit();
		string name = await Dialogs.Input("复制配置", "新配置名称：", UniqueName(cur.ProfileName + " - 副本", null), n => ValidateName(n, null));
		if (name == null)
		{
			return;
		}
		Profile p = cur.Saved.Clone();
		p.Name = name;
		p.AutoConnect = false;
		ProfileView v = AddView(p, select: true);
		SaveAll(showMsg: false);
		v.Log("已从「" + cur.ProfileName + "」复制（含端口转发和密码）。注意：两个配置的本地端口相同时不能同时运行。", LogLevel.Info);
	}

	private async void RenameCurrent()
	{
		ProfileView cur = CurrentView;
		if (cur == null)
		{
			return;
		}
		string name = await Dialogs.Input("重命名配置", "配置名称：", cur.ProfileName, n => ValidateName(n, cur));
		if (name == null || name == cur.ProfileName)
		{
			return;
		}
		string old = cur.ProfileName;
		cur.Rename(name);
		TabViewItem page = PageOf(cur);
		if (page != null)
		{
			UpdateTab(page, cur);
		}
		SaveAll(showMsg: false);
		cur.Log("配置「" + old + "」已重命名为「" + name + "」", LogLevel.Ok);
	}

	private async void DeleteCurrent()
	{
		ProfileView cur = CurrentView;
		if (cur == null)
		{
			return;
		}
		if (cur.Running)
		{
			await Dialogs.Message("配置「" + cur.ProfileName + "」的隧道正在运行，请先停止再删除。");
			return;
		}
		if (_tabs.TabItems.Count <= 1)
		{
			await Dialogs.Message("至少要保留一个配置。");
			return;
		}
		if (!await Dialogs.Confirm("确定删除配置「" + cur.ProfileName + "」吗？\n（主机、密码、端口转发都会删除，无法恢复）", null, "删除", "取消", defaultNo: true))
		{
			return;
		}
		TabViewItem page = PageOf(cur);
		int idx = _tabs.TabItems.IndexOf(page);
		_tabs.TabItems.Remove(page);
		_viewOf.Remove(page);
		cur.DisposeTunnel();
		_tabs.SelectedIndex = Math.Max(0, Math.Min(idx, _tabs.TabItems.Count - 1));
		SaveAll(showMsg: false);
		LogGlobal("已删除配置「" + cur.ProfileName + "」", LogLevel.Warn);
	}

	private void MoveCurrent(int delta)
	{
		int i = _tabs.SelectedIndex;
		int j = i + delta;
		if (i < 0 || j < 0 || j >= _tabs.TabItems.Count)
		{
			return;
		}
		object page = _tabs.TabItems[i];
		_tabs.TabItems.RemoveAt(i);
		_tabs.TabItems.Insert(j, page);
		_tabs.SelectedIndex = j;
		SaveAll(showMsg: false);
	}

	private void StartAll()
	{
		foreach (ProfileView v in Views.ToList())
		{
			if (!v.Running)
			{
				v.Start();
			}
		}
	}

	private void StopAll()
	{
		foreach (ProfileView v in Views.ToList())
		{
			if (v.Running)
			{
				v.Stop();
			}
		}
	}

	public bool SaveAll(bool showMsg)
	{
		List<string> errors = new List<string>();
		foreach (ProfileView v in Views)
		{
			string e = v.Commit();
			if (e != null)
			{
				errors.Add("「" + v.ProfileName + "」" + e);
			}
		}
		_cfg.Profiles = Views.Select(v => v.Saved.Clone()).ToList();
		_cfg.SelectedProfile = Math.Max(0, _tabs.SelectedIndex);
		try
		{
			_cfg.Save();
			if (showMsg)
			{
				LogGlobal("配置已保存（" + _cfg.Profiles.Count + " 个配置，密码已用 Windows DPAPI 加密）：" + AppConfig.FilePath, LogLevel.Ok);
				if (errors.Count > 0)
				{
					_ = Dialogs.Message("以下配置有错误，这些配置的修改没有保存：\n\n" + string.Join("\n", errors));
				}
			}
			return errors.Count == 0;
		}
		catch (Exception ex)
		{
			LogGlobal("保存配置失败：" + ex.Message, LogLevel.Error);
			return false;
		}
	}

	public string EnsurePlink(ProfileView v)
	{
		string path = (_cfg.PlinkPath ?? "").Trim();
		if (File.Exists(path))
		{
			return path;
		}
		string found = Plink.Detect();
		if (found != null)
		{
			_cfg.PlinkPath = found;
			v.Log("找到 plink：" + found, LogLevel.Ok);
			try
			{
				_cfg.Save();
			}
			catch
			{
			}
			return found;
		}
		v.Log("没有找到 plink.exe（已查找 C:\\Program Files\\PuTTY、C:\\Program Files (x86)\\PuTTY 和 PATH）。请安装 PuTTY，或在「设置…」里选择 plink.exe。", LogLevel.Error);
		ShowProfile(v);
		_ = Dialogs.Message("找不到 plink.exe：\n" + path + "\n\n请安装 PuTTY，或点「设置…」选择 plink.exe。");
		return null;
	}

	private static bool AddrOverlap(string a, string b)
	{
		a = string.IsNullOrWhiteSpace(a) ? "127.0.0.1" : a.Trim();
		b = string.IsNullOrWhiteSpace(b) ? "127.0.0.1" : b.Trim();
		return a == b || a == "0.0.0.0" || b == "0.0.0.0";
	}

	public string FindPortConflict(ProfileView self, List<Forward> forwards)
	{
		foreach (ProfileView v in Views)
		{
			if (v == self)
			{
				continue;
			}
			List<Forward> running = v.RunningForwards;
			if (running == null)
			{
				continue;
			}
			foreach (Forward f in forwards)
			{
				Forward hit = running.FirstOrDefault(t => t.LocalPort == f.LocalPort && AddrOverlap(t.LocalAddress, f.LocalAddress));
				if (hit != null)
				{
					return string.Format("本地端口 {0} 已被配置「{1}」使用（「{1}」正在运行{2}）。请先停止「{1}」，或修改本配置的本地端口。", f.LocalPort, v.ProfileName, string.IsNullOrEmpty(hit.Note) ? "" : ("，用于「" + hit.Note + "」"));
				}
			}
		}
		return null;
	}

	// ---------------- 设置 ----------------

	private async Task OpenSettings()
	{
		SettingsDialog dlg = new SettingsDialog(this, _cfg, AutoStart.IsEnabled);
		if (await Dialogs.ShowAsync(dlg.Dialog) != ContentDialogResult.Primary)
		{
			SetTheme(dlg.OriginalTheme);
			return;
		}
		_cfg.PlinkPath = dlg.PlinkPath;
		_cfg.CloseAction = dlg.CloseAction;
		_cfg.MinimizeToTray = dlg.MinimizeToTray;
		_cfg.StartHidden = dlg.StartHidden;
		_cfg.Balloons = dlg.Balloons;
		_cfg.Theme = dlg.Theme;
		if (dlg.AutoStartOn != AutoStart.IsEnabled)
		{
			try
			{
				AutoStart.Set(dlg.AutoStartOn);
				LogGlobal(dlg.AutoStartOn ? ("已开启开机自启（登录后在托盘中启动）：" + AutoStart.Command) : "已关闭开机自启", LogLevel.Ok);
			}
			catch (Exception ex)
			{
				LogGlobal("设置开机自启失败：" + ex.Message, LogLevel.Error);
			}
		}
		try
		{
			_cfg.Save();
			LogGlobal("设置已保存", LogLevel.Ok);
		}
		catch (Exception ex2)
		{
			LogGlobal("保存设置失败：" + ex2.Message, LogLevel.Error);
		}
		SetTheme(_cfg.Theme);
		if (!File.Exists(_cfg.PlinkPath))
		{
			LogGlobal("注意：plink.exe 不存在：" + _cfg.PlinkPath, LogLevel.Warn);
		}
	}

	/// <summary>设置对话框里切换主题时实时预览。</summary>
	public void PreviewTheme(string theme)
	{
		WindowChrome.ApplyTheme(_root, theme);
	}

	public IntPtr Hwnd => WindowChrome.Hwnd(this);

	private void OpenConfigDir()
	{
		try
		{
			Directory.CreateDirectory(AppConfig.Dir);
			Process.Start("explorer.exe", "\"" + AppConfig.Dir + "\"");
		}
		catch (Exception ex)
		{
			LogGlobal("打开配置文件夹失败：" + ex.Message, LogLevel.Error);
		}
	}

	// ---------------- 启动初始化 ----------------

	private void InitData(bool hidden)
	{
		if (_dataInit)
		{
			return;
		}
		_dataInit = true;
		LogGlobal("配置文件：" + AppConfig.FilePath + "（共 " + _tabs.TabItems.Count + " 个配置）", LogLevel.Detail);
		if (_loadWarn != null)
		{
			LogGlobal(_loadWarn, LogLevel.Warn);
		}
		if (AutoStart.IsStale)
		{
			try
			{
				AutoStart.Set(enable: true);
				LogGlobal("开机自启路径已更新为当前程序：" + Environment.ProcessPath, LogLevel.Detail);
			}
			catch
			{
			}
		}
		if (!File.Exists(_cfg.PlinkPath))
		{
			string found = Plink.Detect();
			if (found != null)
			{
				_cfg.PlinkPath = found;
				LogGlobal("找到 plink：" + found, LogLevel.Ok);
				try
				{
					_cfg.Save();
				}
				catch
				{
				}
			}
			else
			{
				LogGlobal("没有找到 plink.exe，请安装 PuTTY，或在「设置…」里选择 plink.exe。", LogLevel.Error);
			}
		}
		foreach (ProfileView v in Views)
		{
			v.UpdateStatus();
		}
		UpdateTray();
		List<ProfileView> auto = Views.Where(v => v.Saved.AutoConnect).ToList();
		if (hidden)
		{
			LogGlobal("已在托盘中启动（双击托盘图标显示窗口）", LogLevel.Detail);
			if (auto.Count == 0)
			{
				Balloon(null, "已在托盘中运行", "双击图标显示窗口；右键可启动隧道。", Win32.NIIF_INFO);
			}
		}
		foreach (ProfileView v in auto)
		{
			v.Log("已勾选「程序启动时自动连接此配置」，正在连接…", LogLevel.Info);
			v.Start();
		}
	}

	// ---------------- 托盘 ----------------

	public void Balloon(string profile, string title, string text, int icon, bool force = false)
	{
		if (!force && !_cfg.Balloons)
		{
			return;
		}
		try
		{
			_tray?.ShowBalloon(((profile != null) ? ("「" + profile + "」") : "") + title, text, icon);
		}
		catch
		{
		}
	}

	private void UpdateTray()
	{
		if (_tray == null)
		{
			return;
		}
		List<ProfileView> all = Views.ToList();
		int connected = all.Count(v => v.State == TunnelState.Connected);
		int pending = all.Count(v => v.State == TunnelState.Connecting || v.State == TunnelState.Reconnecting);
		TunnelState key = pending > 0 ? TunnelState.Reconnecting : (connected > 0 ? TunnelState.Connected : TunnelState.Stopped);
		_tray.SetIcon(_icons[key]);
		string text;
		if (connected == 0 && pending == 0)
		{
			text = "SSH 隧道：未运行";
		}
		else
		{
			List<string> parts = new List<string>();
			if (connected > 0)
			{
				parts.Add(connected + " 个已连接");
			}
			if (pending > 0)
			{
				parts.Add(pending + " 个连接中/重连中");
			}
			text = "SSH 隧道：" + string.Join("，", parts);
			if (all.Count(v => v.Running) == 1)
			{
				ProfileView one = all.First(v => v.Running);
				string t2 = "SSH 隧道：" + one.ProfileName + " " + one.ShortStatus;
				if (t2.Length <= 127)
				{
					text = t2;
				}
			}
		}
		_tray.Tip = text;
	}

	private List<TrayMenuItem> BuildTrayMenu()
	{
		List<TrayMenuItem> items = new List<TrayMenuItem>();
		foreach (ProfileView v in Views.ToList())
		{
			ProfileView view = v;
			TunnelState st = v.State;
			TrayMenuItem sub = new TrayMenuItem(v.ProfileName + "    " + v.ShortStatus) { Children = new List<TrayMenuItem>() };
			sub.Children.Add(new TrayMenuItem("● " + v.ShortStatus, null, enabled: false));
			sub.Children.Add(TrayMenuItem.Separator);
			sub.Children.Add(new TrayMenuItem("启动隧道", () => view.Start(), !v.Running && !v.Restarting));
			sub.Children.Add(new TrayMenuItem("停止隧道", () => view.Stop(), v.Running && !v.Restarting));
			sub.Children.Add(new TrayMenuItem("重新连接", () => view.Reconnect(), v.Running && !v.Restarting));
			sub.Children.Add(TrayMenuItem.Separator);
			sub.Children.Add(new TrayMenuItem("测试端口", () => view.TestPorts(fromTray: true), !v.Testing));
			TrayMenuItem copy = new TrayMenuItem("复制地址") { Children = new List<TrayMenuItem>() };
			foreach (Forward f in v.TableForwards())
			{
				string addr = f.ClientAddress;
				copy.Children.Add(new TrayMenuItem((string.IsNullOrEmpty(f.Note) ? "" : (f.Note + "  ")) + addr, () => CopyText(addr)));
			}
			if (copy.Children.Count == 0)
			{
				copy.Children.Add(new TrayMenuItem("（没有端口转发）", null, enabled: false));
			}
			sub.Children.Add(copy);
			sub.Children.Add(TrayMenuItem.Separator);
			sub.Children.Add(new TrayMenuItem("显示此配置", () => ShowProfile(view)));
			items.Add(sub);
		}
		items.Add(TrayMenuItem.Separator);
		items.Add(new TrayMenuItem("全部启动", StartAll, Views.Any(v => !v.Running)));
		items.Add(new TrayMenuItem("全部停止", StopAll, Views.Any(v => v.Running)));
		items.Add(TrayMenuItem.Separator);
		items.Add(new TrayMenuItem("打开配置文件夹", OpenConfigDir));
		items.Add(TrayMenuItem.Separator);
		items.Add(new TrayMenuItem(IsShown ? "隐藏到托盘" : "显示主窗口", ToggleWindow) { Default = true });
		items.Add(new TrayMenuItem("退出", ExitApp));
		return items;
	}

	private void CopyText(string text)
	{
		for (int i = 0; i < 5; i++)
		{
			try
			{
				DataPackage dp = new DataPackage();
				dp.SetText(text);
				Clipboard.SetContent(dp);
				Clipboard.Flush();
				Balloon(null, "已复制", text, Win32.NIIF_INFO, force: true);
				return;
			}
			catch
			{
				Thread.Sleep(100);
			}
		}
		LogGlobal("复制到剪贴板失败（剪贴板被其它程序占用）", LogLevel.Warn);
	}

	private void HideToTray()
	{
		_hidden = true;
		AppWindow.Hide();
		if (!_balloonShown)
		{
			_balloonShown = true;
			Balloon(null, Program.AppTitle, "已最小化到托盘，双击图标可恢复；右键可按配置启动 / 停止 / 重连。", Win32.NIIF_INFO);
		}
	}

	public void ShowFromTray()
	{
		_hidden = false;
		if (!_activated)
		{
			_activated = true;
			Activate();
		}
		WindowChrome.BringToFront(this);
	}

	private void ToggleWindow()
	{
		if (IsShown)
		{
			HideToTray();
		}
		else
		{
			ShowFromTray();
		}
	}

	private void ExitApp()
	{
		_exiting = true;
		Cleanup();
		Close();
		Application.Current.Exit();
	}

	private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs e)
	{
		if (_exiting)
		{
			return;
		}
		e.Cancel = true;
		if (_closingAsk)
		{
			return;
		}
		string action = _cfg.CloseAction;
		if (action == "ask")
		{
			_closingAsk = true;
			try
			{
				bool running = Views.Any(v => v.Running);
				CheckBox remember = new CheckBox { Content = "记住我的选择（以后可在「设置…」里修改）" };
				StackPanel sp = new StackPanel { Spacing = 12 };
				sp.Children.Add(Dialogs.Text(running ? "有隧道正在运行。关闭窗口要怎么处理？\n\n「最小化到托盘」：隧道继续在后台运行\n「退出程序」：停止所有隧道并退出" : "要最小化到托盘，还是退出程序？"));
				sp.Children.Add(remember);
				ContentDialog d = new ContentDialog
				{
					Title = Program.AppTitle,
					Content = sp,
					PrimaryButtonText = "最小化到托盘",
					SecondaryButtonText = "退出程序",
					CloseButtonText = "取消",
					DefaultButton = ContentDialogButton.Primary
				};
				ContentDialogResult r = await Dialogs.ShowAsync(d);
				if (r == ContentDialogResult.None)
				{
					return;
				}
				action = r == ContentDialogResult.Primary ? "tray" : "exit";
				if (remember.IsChecked == true)
				{
					_cfg.CloseAction = action;
					try
					{
						_cfg.Save();
					}
					catch
					{
					}
				}
			}
			finally
			{
				_closingAsk = false;
			}
		}
		if (action == "tray")
		{
			HideToTray();
			return;
		}
		ExitApp();
	}

	private bool _cleaned;

	private void Cleanup()
	{
		if (_cleaned)
		{
			return;
		}
		_cleaned = true;
		try
		{
			SaveAll(showMsg: false);
		}
		catch
		{
		}
		_timer?.Stop();
		try
		{
			Task.WaitAll(Views.ToList().Select(v => Task.Run(() => v.StopNow())).ToArray(), 8000);
		}
		catch
		{
		}
		_tray?.Dispose();
		_tray = null;
	}
}
