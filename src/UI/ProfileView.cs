using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace SshTunnelToolbox;

/// <summary>一个配置（标签页）的界面：连接设置、跳板机、端口转发表、状态 / 按钮、日志。</summary>
public sealed class ProfileView
{
	/// <summary>界面根元素（用组合而不是继承 UserControl，单文件发布时自定义 XAML 派生类型会导致崩溃）。</summary>
	public Grid Element { get; private set; }

	/// <summary>端口转发表的一行。</summary>
	private sealed class ForwardRow
	{
		public Grid Root;

		public TextBox Note, LAddr, LPort, THost, TPort;

		public TextBlock Test;

		public string Text(int i)
		{
			TextBox b = i switch { 0 => Note, 1 => LAddr, 2 => LPort, 3 => THost, _ => TPort };
			return (b.Text ?? "").Trim();
		}

		public void Set(string note, string laddr, string lport, string thost, string tport)
		{
			Note.Text = note ?? "";
			LAddr.Text = laddr ?? "";
			LPort.Text = lport ?? "";
			THost.Text = thost ?? "";
			TPort.Text = tport ?? "";
			Test.Text = "";
		}
	}

	private static readonly double[] ColWeights = { 24, 16, 11, 20, 11, 18 };

	private readonly MainWindow _main;

	private readonly DispatcherQueue _dq;

	private readonly TunnelManager _tunnel = new TunnelManager();

	private Profile _saved;

	private List<Forward> _runningForwards;

	private readonly TextBox _host, _user, _hostKey, _jumpHost, _jumpUser, _jumpHostKey;

	private readonly PasswordBox _pw, _jumpPw;

	private readonly NumberBox _port, _delay, _jumpPort;

	private readonly CheckBox _showPw, _autoConnect, _verbose, _useJump, _showJumpPw;

	private readonly TextBlock _lblHost;

	private readonly StackPanel _jumpPanel;

	private readonly Button _btnKey, _btnJumpKey, _btnStart, _btnStop, _btnReconnect, _btnTest;

	private readonly StackPanel _rowsPanel;

	private readonly List<ForwardRow> _rows = new List<ForwardRow>();

	private readonly Ellipse _dot;

	private readonly TextBlock _statusText;

	private readonly LogView _log;

	private bool _fetchingKey, _autoFetchDone, _testing, _restarting, _userStopping, _everConnected, _outageNotified, _disposed;

	private TunnelState _lastState;

	private DateTime _lastFailBalloon = DateTime.MinValue;

	public Ellipse HeaderDot;

	public TextBlock HeaderText;

	public string ProfileName => _saved.Name;

	public Profile Saved => _saved;

	public bool Running => _tunnel.Running;

	public bool Testing => _testing;

	public bool Restarting => _restarting;

	public TunnelState State => _tunnel.Running ? _tunnel.State : TunnelState.Stopped;

	public List<Forward> RunningForwards => _tunnel.Running ? _runningForwards : null;

	public string ShortStatus => State switch
	{
		TunnelState.Connected => "已连接 " + TunnelManager.Fmt(_tunnel.ConnectedSince.HasValue ? (DateTime.Now - _tunnel.ConnectedSince.Value) : TimeSpan.Zero),
		TunnelState.Connecting => "连接中…（第 " + _tunnel.AttemptCount + " 次）",
		TunnelState.Reconnecting => "重连中…",
		_ => "已停止"
	};

	public event Action StatusChanged;

	public ProfileView(MainWindow main, Profile p)
	{
		_main = main;
		_dq = DispatcherQueue.GetForCurrentThread();
		_saved = p.Clone();
		_tunnel.Name = p.Name;

		Grid root = new Grid { Padding = new Thickness(16, 12, 16, 14), RowSpacing = 10 };
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 150 });
		root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 120 });

		// ---------- 连接设置 ----------
		_host = new TextBox { PlaceholderText = "例如 10.0.0.1 或 ssh.example.com" };
		_user = new TextBox { PlaceholderText = "root" };
		_pw = new PasswordBox { PasswordRevealMode = PasswordRevealMode.Hidden };
		_hostKey = new TextBox { FontFamily = new FontFamily("Consolas"), PlaceholderText = "SHA256:…（留空则首次连接时自动获取）" };
		_port = Num(1, 65535, 22, 120);
		_delay = Num(1, 3600, 5, 120);
		_showPw = new CheckBox { Content = "显示", MinWidth = 0 };
		_showPw.Checked += (s, e) => _pw.PasswordRevealMode = PasswordRevealMode.Visible;
		_showPw.Unchecked += (s, e) => _pw.PasswordRevealMode = PasswordRevealMode.Hidden;
		_autoConnect = new CheckBox { Content = "程序启动时自动连接此配置" };
		_verbose = new CheckBox { Content = "详细日志（plink -v）" };
		_useJump = new CheckBox { Content = "通过跳板机连接上面的主机（相当于 ssh -J 跳板用户@跳板机）" };
		ToolTipService.SetToolTip(_useJump, "不勾选：plink -L 本地端口:目标主机:目标端口 用户@主机（主机本身就是跳板，目标主机可以是另一网段）\n勾选：目标主机在另一网段、只有跳板机能 SSH 到它时使用");
		_useJump.Checked += (s, e) => UpdateJumpVisibility();
		_useJump.Unchecked += (s, e) => UpdateJumpVisibility();
		_btnKey = X.Btn("获取指纹", async (s, e) => await FetchKeyAsync(jump: false, silent: false), "\uE928");
		ToolTipService.SetToolTip(_btnKey, "连接服务器读取主机密钥指纹并填入（不会登录）；勾选跳板机时经跳板机获取");

		_jumpHost = new TextBox();
		_jumpUser = new TextBox();
		_jumpPw = new PasswordBox { PasswordRevealMode = PasswordRevealMode.Hidden };
		_jumpHostKey = new TextBox { FontFamily = new FontFamily("Consolas"), PlaceholderText = "SHA256:…（留空则自动获取）" };
		ToolTipService.SetToolTip(_jumpHostKey, "跳板机的主机指纹，如 SHA256:xxxx；留空时启动隧道会先自动获取");
		_jumpPort = Num(1, 65535, 22, 120);
		_showJumpPw = new CheckBox { Content = "显示", MinWidth = 0 };
		_showJumpPw.Checked += (s, e) => _jumpPw.PasswordRevealMode = PasswordRevealMode.Visible;
		_showJumpPw.Unchecked += (s, e) => _jumpPw.PasswordRevealMode = PasswordRevealMode.Hidden;
		_btnJumpKey = X.Btn("获取指纹", async (s, e) => await FetchKeyAsync(jump: true, silent: false), "\uE928");
		ToolTipService.SetToolTip(_btnJumpKey, "连接跳板机读取主机密钥指纹并填入（不会登录）");

		_lblHost = X.Label("主机");
		StackPanel settings = new StackPanel { Spacing = 10 };
		settings.Children.Add(FieldRow(
			(_lblHost, _host, X.Star(3)),
			(X.Label("端口"), _port, X.Auto),
			(X.Label("用户名"), _user, X.Star(2))));
		settings.Children.Add(FieldRow(
			(X.Label("密码"), Pair(_pw, _showPw), X.Star(2)),
			(X.Label("主机指纹"), Pair(_hostKey, _btnKey), X.Star(3))));
		StackPanel opts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
		StackPanel delay = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		delay.Children.Add(Fixed(X.Label("重连间隔")));
		delay.Children.Add(_delay);
		delay.Children.Add(X.Label("秒"));
		opts.Children.Add(delay);
		opts.Children.Add(_autoConnect);
		opts.Children.Add(_verbose);
		settings.Children.Add(opts);
		settings.Children.Add(_useJump);

		_jumpPanel = new StackPanel { Spacing = 10 };
		_jumpPanel.Children.Add(X.Secondary("开启后：先 SSH 到跳板机，再由跳板机连到上面的“SSH 主机:端口”登录，端口转发从那台主机发出。", wrap: true));
		_jumpPanel.Children.Add(FieldRow(
			(X.Label("跳板机"), _jumpHost, X.Star(3)),
			(X.Label("端口"), _jumpPort, X.Auto),
			(X.Label("跳板用户"), _jumpUser, X.Star(2))));
		_jumpPanel.Children.Add(FieldRow(
			(X.Label("密码"), Pair(_jumpPw, _showJumpPw), X.Star(2)),
			(X.Label("跳板指纹"), Pair(_jumpHostKey, _btnJumpKey), X.Star(3))));
		settings.Children.Add(_jumpPanel);

		Border settingsCard = X.Card();
		settingsCard.Child = settings;
		root.Children.Add(settingsCard);

		// ---------- 端口转发 ----------
		Grid fwd = new Grid { RowSpacing = 8 };
		fwd.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		fwd.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
		fwd.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
		Grid fwdHead = new Grid { ColumnSpacing = 8 };
		fwdHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		fwdHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		fwdHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		StackPanel titleSp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
		titleSp.Children.Add(X.Header("端口转发（-L）"));
		TextBlock hint = X.Secondary("目标主机由 SSH 服务器去连接，可填其它网段地址");
		hint.TextTrimming = TextTrimming.CharacterEllipsis;
		Grid.SetColumn(hint, 1);
		fwdHead.Children.Add(titleSp);
		fwdHead.Children.Add(hint);
		StackPanel fwdBtns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		fwdBtns.Children.Add(X.Btn("添加一行", (s, e) =>
		{
			ForwardRow r = AddRow("", "127.0.0.1", "", "", "");
			r.Note.Focus(FocusState.Programmatic);
		}, "\uE710"));
		fwdBtns.Children.Add(X.Btn("从 ssh 命令导入…", (s, e) => ImportSshCommand(), "\uE8B5"));
		Grid.SetColumn(fwdBtns, 2);
		fwdHead.Children.Add(fwdBtns);
		fwd.Children.Add(fwdHead);

		Grid colHead = RowGrid();
		string[] heads = { "备注", "本地地址", "本地端口", "目标主机", "目标端口", "测试结果" };
		for (int i = 0; i < heads.Length; i++)
		{
			TextBlock h = X.Secondary(heads[i]);
			h.Margin = new Thickness(2, 0, 0, 0);
			Grid.SetColumn(h, i);
			colHead.Children.Add(h);
		}
		Grid.SetRow(colHead, 1);
		fwd.Children.Add(colHead);
		_rowsPanel = new StackPanel { Spacing = 6 };
		ScrollViewer rowsSv = new ScrollViewer { Content = _rowsPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 12, 0) };
		Grid.SetRow(rowsSv, 2);
		fwd.Children.Add(rowsSv);
		Border fwdCard = X.Card();
		fwdCard.Child = fwd;
		Grid.SetRow(fwdCard, 1);
		root.Children.Add(fwdCard);

		// ---------- 按钮 + 状态 ----------
		Grid actions = new Grid { ColumnSpacing = 12 };
		actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		StackPanel btns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		_btnStart = X.Btn("启动隧道", (s, e) => Start(), "\uE768", accent: true);
		_btnStop = X.Btn("停止隧道", (s, e) => Stop(), "\uE71A");
		_btnReconnect = X.Btn("重新连接", (s, e) => Reconnect(), "\uE72C");
		_btnTest = X.Btn("测试端口", (s, e) => TestPorts(fromTray: false), "\uE9D9");
		btns.Children.Add(_btnStart);
		btns.Children.Add(_btnStop);
		btns.Children.Add(_btnReconnect);
		btns.Children.Add(_btnTest);
		btns.Children.Add(X.Btn("保存配置", (s, e) => _main.SaveAll(showMsg: true), "\uE74E"));
		btns.Children.Add(X.Btn("清空日志", (s, e) => _log.Clear(), "\uE894"));
		actions.Children.Add(btns);
		StackPanel status = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
		_dot = new Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(MainWindow.CGray), VerticalAlignment = VerticalAlignment.Center };
		_statusText = new TextBlock { Text = "已停止", Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"], VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
		status.Children.Add(_dot);
		status.Children.Add(_statusText);
		Grid.SetColumn(status, 1);
		actions.Children.Add(status);
		Grid.SetRow(actions, 2);
		root.Children.Add(actions);

		// ---------- 日志 ----------
		_log = new LogView("Consolas, Microsoft YaHei UI", 12.5, "yyyy-MM-dd HH:mm:ss");
		Border logCard = X.Card(0, 0);
		logCard.Child = _log.Element;
		Grid.SetRow(logCard, 3);
		root.Children.Add(logCard);

		Element = root;

		_tunnel.Log += (m, lv) => Log(m, lv);
		_tunnel.StateChanged += () => _dq.TryEnqueue(UpdateStatus);
		ToUi(p);
	}

	// ---------- 布局小工具 ----------

	private static NumberBox Num(double min, double max, double val, double width)
	{
		return new NumberBox { Minimum = min, Maximum = max, Value = val, Width = width, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact, ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten };
	}

	private static int NumVal(NumberBox b, int def)
	{
		double v = b.Value;
		if (double.IsNaN(v))
		{
			return def;
		}
		return (int)Math.Round(Math.Max(b.Minimum, Math.Min(b.Maximum, v)));
	}

	private static FrameworkElement Fixed(TextBlock t)
	{
		t.MinWidth = 64;
		return t;
	}

	private static Grid Pair(FrameworkElement main, FrameworkElement side)
	{
		return X.Row((X.Star(), main), (X.Auto, side));
	}

	/// <summary>一行若干「标签 + 输入框」，第一个标签固定宽度以便上下对齐。</summary>
	private static Grid FieldRow(params (TextBlock label, FrameworkElement field, GridLength width)[] items)
	{
		Grid g = new Grid { ColumnSpacing = 10 };
		int col = 0;
		for (int i = 0; i < items.Length; i++)
		{
			(TextBlock label, FrameworkElement field, GridLength width) = items[i];
			if (i == 0)
			{
				label.MinWidth = 64;
			}
			g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			g.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
			Grid.SetColumn(label, col++);
			Grid.SetColumn(field, col++);
			if (i > 0)
			{
				label.Margin = new Thickness(8, 0, 0, 0);
			}
			g.Children.Add(label);
			g.Children.Add(field);
		}
		return g;
	}

	private static Grid RowGrid()
	{
		Grid g = new Grid { ColumnSpacing = 6 };
		foreach (double w in ColWeights)
		{
			g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star), MinWidth = 50 });
		}
		g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
		return g;
	}

	private ForwardRow AddRow(string note, string laddr, string lport, string thost, string tport)
	{
		ForwardRow r = new ForwardRow { Root = RowGrid() };
		r.Note = new TextBox();
		r.LAddr = new TextBox();
		r.LPort = new TextBox();
		r.THost = new TextBox();
		r.TPort = new TextBox();
		r.Test = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4, 0, 0, 0) };
		TextBox[] boxes = { r.Note, r.LAddr, r.LPort, r.THost, r.TPort };
		for (int i = 0; i < boxes.Length; i++)
		{
			boxes[i].MinWidth = 0;
			Grid.SetColumn(boxes[i], i);
			r.Root.Children.Add(boxes[i]);
		}
		Grid.SetColumn(r.Test, 5);
		r.Root.Children.Add(r.Test);
		Button del = new Button { Content = new FontIcon { Glyph = "\uE711", FontSize = 12 }, Padding = new Thickness(8, 6, 8, 6), VerticalAlignment = VerticalAlignment.Center };
		ToolTipService.SetToolTip(del, "删除此行");
		del.Click += (s, e) =>
		{
			_rows.Remove(r);
			_rowsPanel.Children.Remove(r.Root);
		};
		Grid.SetColumn(del, 6);
		r.Root.Children.Add(del);
		r.Set(note, laddr, lport, thost, tport);
		_rows.Add(r);
		_rowsPanel.Children.Add(r.Root);
		return r;
	}

	private void ClearRows()
	{
		_rows.Clear();
		_rowsPanel.Children.Clear();
	}

	private void SetTest(ForwardRow r, string text, string level)
	{
		r.Test.Text = text;
		r.Test.Foreground = level == null ? null : new SolidColorBrush(LogView.ColorOf(level, Element.ActualTheme == ElementTheme.Dark));
		ToolTipService.SetToolTip(r.Test, string.IsNullOrEmpty(text) ? null : text);
	}

	// ---------- 数据 ----------

	public void Rename(string name)
	{
		_saved.Name = name;
		_tunnel.Name = name;
	}

	public List<Forward> TableForwards()
	{
		List<Forward> list = new List<Forward>();
		foreach (ForwardRow r in _rows)
		{
			if (int.TryParse(r.Text(2), out int lp))
			{
				list.Add(new Forward { Note = r.Text(0), LocalAddress = r.Text(1), LocalPort = lp });
			}
		}
		return list;
	}

	public string Commit()
	{
		string err = FromUi(out Profile c);
		if (err == null)
		{
			_saved = c;
		}
		return err;
	}

	public void Log(string msg, LogLevel level)
	{
		if (_disposed)
		{
			return;
		}
		if (!_dq.HasThreadAccess)
		{
			_dq.TryEnqueue(() => Log(msg, level));
			return;
		}
		_log.Append(msg, level.ToString());
	}

	private void ToUi(Profile c)
	{
		_host.Text = c.Host ?? "";
		_port.Value = Math.Max(1, Math.Min(65535, c.Port));
		_user.Text = c.User ?? "";
		_pw.Password = c.GetPassword() ?? "";
		_hostKey.Text = c.HostKey ?? "";
		_delay.Value = Math.Max(1, Math.Min(3600, c.ReconnectDelaySeconds));
		_autoConnect.IsChecked = c.AutoConnect;
		_verbose.IsChecked = c.Verbose;
		_useJump.IsChecked = c.UseJump;
		_jumpHost.Text = c.JumpHost ?? "";
		_jumpPort.Value = Math.Max(1, Math.Min(65535, c.JumpPort));
		_jumpUser.Text = c.JumpUser ?? "";
		_jumpPw.Password = c.GetJumpPassword() ?? "";
		_jumpHostKey.Text = c.JumpHostKey ?? "";
		UpdateJumpVisibility();
		ClearRows();
		foreach (Forward f in c.Forwards)
		{
			AddRow(f.Note, f.LocalAddress, f.LocalPort.ToString(), f.TargetHost, f.TargetPort.ToString());
		}
	}

	private string FromUi(out Profile c)
	{
		c = new Profile
		{
			Name = _saved.Name,
			Host = _host.Text.Trim(),
			Port = NumVal(_port, 22),
			User = _user.Text.Trim(),
			HostKey = _hostKey.Text.Trim(),
			ReconnectDelaySeconds = NumVal(_delay, 5),
			AutoConnect = _autoConnect.IsChecked == true,
			Verbose = _verbose.IsChecked == true,
			Forwards = new List<Forward>(),
			UseJump = _useJump.IsChecked == true,
			JumpHost = _jumpHost.Text.Trim(),
			JumpPort = NumVal(_jumpPort, 22),
			JumpUser = _jumpUser.Text.Trim(),
			JumpHostKey = _jumpHostKey.Text.Trim()
		};
		if (_jumpPw.Password == _saved.GetJumpPassword() && !string.IsNullOrEmpty(_saved.JumpPasswordProtected))
		{
			c.JumpPasswordProtected = _saved.JumpPasswordProtected;
		}
		else
		{
			c.SetJumpPassword(_jumpPw.Password);
		}
		if (_pw.Password == _saved.GetPassword() && !string.IsNullOrEmpty(_saved.PasswordProtected))
		{
			c.PasswordProtected = _saved.PasswordProtected;
		}
		else
		{
			c.SetPassword(_pw.Password);
		}
		int num = 0;
		foreach (ForwardRow r in _rows)
		{
			num++;
			string note = r.Text(0), laddr = r.Text(1), lport = r.Text(2), thost = r.Text(3), tport = r.Text(4);
			if (note == "" && lport == "" && thost == "" && tport == "")
			{
				continue;
			}
			if (!int.TryParse(lport, out int lpi) || lpi < 1 || lpi > 65535)
			{
				return "第 " + num + " 行：本地端口无效";
			}
			if (!int.TryParse(tport, out int tpi) || tpi < 1 || tpi > 65535)
			{
				return "第 " + num + " 行：目标端口无效";
			}
			if (thost == "")
			{
				return "第 " + num + " 行：目标主机不能为空";
			}
			if (laddr == "")
			{
				laddr = "127.0.0.1";
			}
			if (!IPAddress.TryParse(laddr, out _))
			{
				return "第 " + num + " 行：本地地址必须是 IP（如 127.0.0.1）";
			}
			if (c.Forwards.Any(f => f.LocalPort == lpi))
			{
				return "第 " + num + " 行：本地端口 " + lpi + " 重复";
			}
			c.Forwards.Add(new Forward { Note = note, LocalAddress = laddr, LocalPort = lpi, TargetHost = thost, TargetPort = tpi });
		}
		return null;
	}

	// ---------- 指纹 ----------

	/// <summary>主机指纹为空时：首次连接自动获取并信任（TOFU），填入后继续启动。</summary>
	private async void AutoFetchThenStart()
	{
		if (_fetchingKey)
		{
			return;
		}
		_btnStart.IsEnabled = false;
		try
		{
			Log("主机指纹为空：先自动获取主机指纹（首次连接信任 TOFU），获取后自动保存到配置", LogLevel.Info);
			bool ok = true;
			if (_useJump.IsChecked == true && _jumpHostKey.Text.Trim() == "")
			{
				ok = await FetchKeyAsync(jump: true, silent: true);
			}
			if (ok && _hostKey.Text.Trim() == "")
			{
				ok = await FetchKeyAsync(jump: false, silent: true);
			}
			if (!ok)
			{
				Log("自动获取指纹失败，仍按原方式启动（未缓存的主机密钥会导致连接失败）", LogLevel.Warn);
			}
		}
		finally
		{
			_btnStart.IsEnabled = true;
		}
		_autoFetchDone = true;
		try
		{
			Start();
		}
		finally
		{
			_autoFetchDone = false;
		}
	}

	/// <summary>jump=true：获取跳板机的指纹；jump=false：获取“主机”的指纹（勾选跳板机时经跳板获取）。</summary>
	public async Task<bool> FetchKeyAsync(bool jump, bool silent)
	{
		if (_fetchingKey)
		{
			return false;
		}
		string plink = _main.EnsurePlink(this);
		if (plink == null)
		{
			return false;
		}
		string host = (jump ? _jumpHost.Text : _host.Text).Trim();
		int port = jump ? NumVal(_jumpPort, 22) : NumVal(_port, 22);
		string label = jump ? "跳板机" : "主机";
		if (host == "")
		{
			if (!silent)
			{
				ShowError(label + "地址为空，无法获取指纹。");
			}
			return false;
		}
		bool viaJump = !jump && _useJump.IsChecked == true;
		Profile jcfg = null;
		string jpw = null;
		if (viaJump)
		{
			if (_jumpHost.Text.Trim() == "" || _jumpUser.Text.Trim() == "")
			{
				if (!silent)
				{
					ShowError("已勾选“通过跳板机连接”：要经跳板机获取指纹，请先填写跳板机和跳板用户。");
				}
				return false;
			}
			if (_jumpHostKey.Text.Trim() == "")
			{
				Log("跳板指纹为空，先获取跳板机指纹", LogLevel.Info);
				_fetchingKey = false;
				if (!await FetchKeyAsync(jump: true, silent: silent))
				{
					return false;
				}
			}
			jcfg = new Profile
			{
				UseJump = true,
				JumpHost = _jumpHost.Text.Trim(),
				JumpPort = NumVal(_jumpPort, 22),
				JumpUser = _jumpUser.Text.Trim(),
				JumpHostKey = _jumpHostKey.Text.Trim()
			};
			jpw = _jumpPw.Password;
		}
		_fetchingKey = true;
		Button btn = jump ? _btnJumpKey : _btnKey;
		btn.IsEnabled = false;
		X.SetButtonText(btn, "获取中…");
		try
		{
			Log("获取" + label + "指纹：" + host + ":" + port + (viaJump ? ("（经跳板机 " + jcfg.JumpUser + "@" + jcfg.JumpHost + ":" + jcfg.JumpPort + "）") : "") + "…", LogLevel.Info);
			HostKeyResult r = await Task.Run(() => HostKeyFetcher.Fetch(plink, host, port, jcfg, jpw));
			if (_disposed)
			{
				return false;
			}
			if (!r.Ok)
			{
				if (viaJump && r.Error.IndexOf("超时", StringComparison.Ordinal) < 0)
				{
					r.Error += "（经跳板机获取：请检查跳板用户/密码/跳板指纹，以及跳板机能否访问 " + host + ":" + port + "）";
				}
				Log("获取" + label + "指纹失败：" + r.Error, LogLevel.Error);
				foreach (string line in (r.Output ?? "").Replace("\r", "").Split('\n'))
				{
					if (line.Trim() != "")
					{
						Log("plink> " + line.Trim(), LogLevel.Plink);
					}
				}
				if (!silent)
				{
					ShowError("获取" + label + "指纹失败：" + r.Error);
				}
				return false;
			}
			TextBox box = jump ? _jumpHostKey : _hostKey;
			string old = box.Text.Trim();
			box.Text = r.Fingerprint;
			Log("已获取" + label + " " + host + ":" + port + " 的指纹：" + ((r.KeyType != "") ? (r.KeyType + " ") : "") + r.Fingerprint, LogLevel.Ok);
			if (old != "" && !string.Equals(old, r.Fingerprint, StringComparison.OrdinalIgnoreCase))
			{
				Log("注意：新指纹与原来填写的不同（原：" + old + "）。如果服务器没有重装/换密钥，可能存在中间人，请确认后再连接。", LogLevel.Warn);
			}
			if (!silent)
			{
				Log("指纹已填入，点“保存配置”或启动隧道时会一起保存", LogLevel.Detail);
			}
			return true;
		}
		finally
		{
			_fetchingKey = false;
			X.SetButtonText(btn, "获取指纹");
			btn.IsEnabled = true;
		}
	}

	private void UpdateJumpVisibility()
	{
		bool on = _useJump.IsChecked == true;
		_jumpPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
		_lblHost.Text = on ? "SSH 主机" : "主机";
	}

	// ---------- 从 ssh 命令导入 ----------

	private async void ImportSshCommand()
	{
		SshCommand parsed = null;
		string text = await Dialogs.Input("从 ssh 命令导入", "粘贴 ssh 命令，例如：\nssh -N -L 2222:192.0.2.20:22 user@10.0.0.1\n（支持多个 -L、-p 端口、-J 跳板用户@跳板机[:端口]）", "", t =>
		{
			parsed = SshCommand.Parse(t, out string error);
			return parsed == null ? (error ?? "无法解析") : null;
		}, 560);
		if (text == null || parsed == null)
		{
			return;
		}
		bool replace = true;
		if (_rows.Count > 0)
		{
			ContentDialogResult r = await Dialogs.Ask("当前已有端口转发。\n\n替换：用命令里的转发替换现有的\n追加：追加到现有转发后面（本地端口相同的会被更新）", "从 ssh 命令导入", "替换", "追加");
			if (r == ContentDialogResult.None)
			{
				return;
			}
			replace = r == ContentDialogResult.Primary;
		}
		_host.Text = parsed.Host;
		_port.Value = parsed.Port;
		_user.Text = parsed.User;
		if (parsed.HostKey != "")
		{
			_hostKey.Text = parsed.HostKey;
		}
		if (parsed.JumpHost != "")
		{
			_useJump.IsChecked = true;
			_jumpHost.Text = parsed.JumpHost;
			_jumpPort.Value = parsed.JumpPort;
			_jumpUser.Text = parsed.JumpUser;
		}
		else
		{
			_useJump.IsChecked = false;
		}
		if (replace)
		{
			ClearRows();
		}
		foreach (Forward f in parsed.Forwards)
		{
			ForwardRow existing = _rows.FirstOrDefault(x => x.Text(2) == f.LocalPort.ToString());
			if (existing != null)
			{
				existing.Set(f.Note, f.LocalAddress, f.LocalPort.ToString(), f.TargetHost, f.TargetPort.ToString());
			}
			else
			{
				AddRow(f.Note, f.LocalAddress, f.LocalPort.ToString(), f.TargetHost, f.TargetPort.ToString());
			}
		}
		UpdateJumpVisibility();
		Log("已从 ssh 命令导入：" + parsed.User + "@" + parsed.Host + ":" + parsed.Port + ((parsed.JumpHost != "") ? ("，跳板机 " + parsed.JumpUser + "@" + parsed.JumpHost + ":" + parsed.JumpPort) : "") + "，" + parsed.Forwards.Count + " 条转发。请检查密码/指纹后点“保存配置”或“启动隧道”。", LogLevel.Info);
		foreach (string note in parsed.Notes)
		{
			Log(note, LogLevel.Warn);
		}
	}

	private void ShowError(string msg)
	{
		_main.ShowProfile(this);
		_ = Dialogs.Message("配置「" + ProfileName + "」：" + msg);
	}

	// ---------- 启动 / 停止 / 重连 / 测试 ----------

	public void Start()
	{
		if (_tunnel.Running)
		{
			Log("隧道已在运行", LogLevel.Detail);
			return;
		}
		string err = FromUi(out Profile c);
		if (err != null)
		{
			ShowError(err);
			return;
		}
		string plink = _main.EnsurePlink(this);
		if (plink == null)
		{
			return;
		}
		if (c.Host == "" || c.User == "")
		{
			ShowError("主机和用户名不能为空。");
			return;
		}
		if (c.UseJump && (c.JumpHost == "" || c.JumpUser == ""))
		{
			ShowError("已勾选“通过跳板机连接”，跳板机和跳板用户不能为空。");
			return;
		}
		if (c.UseJump && (c.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || c.Host.StartsWith("127.") || c.Host == "::1"))
		{
			ShowError("跳板模式下“SSH 主机”不能填 localhost / 127.x（PuTTY 对本机地址不走跳板代理）。\n如果要登录的就是跳板机本身，请取消勾选“通过跳板机连接”，直接把跳板机填在“主机”里。");
			return;
		}
		if (c.Forwards.Count == 0)
		{
			ShowError("请至少添加一条端口转发。");
			return;
		}
		string conflict = _main.FindPortConflict(this, c.Forwards);
		if (conflict != null)
		{
			Log("无法启动：" + conflict, LogLevel.Error);
			_main.Balloon(ProfileName, "无法启动", conflict, Win32.NIIF_ERROR, force: true);
			return;
		}
		if (!_autoFetchDone && (c.HostKey == "" || (c.JumpActive && c.JumpHostKey == "")))
		{
			AutoFetchThenStart();
			return;
		}
		_saved = c;
		_main.SaveAll(showMsg: false);
		foreach (ForwardRow r in _rows)
		{
			SetTest(r, "", null);
		}
		_runningForwards = c.Forwards.Select(f => f.Clone()).ToList();
		Log("———— 启动隧道「" + c.Name + "」 " + c.Describe() + " ————", LogLevel.Info);
		_tunnel.Start(c, plink);
		UpdateStatus();
	}

	public void Stop()
	{
		if (!_tunnel.Running)
		{
			return;
		}
		Log("正在停止隧道…", LogLevel.Info);
		_userStopping = true;
		Task.Run(() => _tunnel.Stop()).ContinueWith(t => _dq.TryEnqueue(UpdateStatus));
	}

	public void StopNow()
	{
		_userStopping = true;
		try
		{
			_tunnel.Dispose();
		}
		catch
		{
		}
	}

	public void DisposeTunnel()
	{
		_disposed = true;
		try
		{
			_tunnel.Dispose();
		}
		catch
		{
		}
	}

	public void Reconnect()
	{
		if (!_tunnel.Running)
		{
			Start();
			return;
		}
		if (_restarting)
		{
			return;
		}
		_restarting = true;
		Log("———— 重新连接 ————", LogLevel.Info);
		_userStopping = true;
		UpdateStatus();
		Task.Run(() => _tunnel.Stop()).ContinueWith(t => _dq.TryEnqueue(() =>
		{
			_restarting = false;
			UpdateStatus();
			Start();
		}));
	}

	public async void TestPorts(bool fromTray)
	{
		if (_testing)
		{
			return;
		}
		string err = FromUi(out Profile c);
		if (err != null)
		{
			ShowError(err);
			return;
		}
		_testing = true;
		_btnTest.IsEnabled = false;
		int okCount = 0;
		int total = 0;
		List<string> fails = new List<string>();
		Log("测试本地端口…", LogLevel.Info);
		foreach (ForwardRow r in _rows.ToList())
		{
			if (!int.TryParse(r.Text(2), out int lp))
			{
				continue;
			}
			string la = r.Text(1);
			string note = r.Text(0);
			SetTest(r, "测试中…", "Detail");
			string e2 = null;
			bool ok = await Task.Run(() =>
			{
				bool res = PortUtil.TryConnect(la, lp, 2000, out string error);
				e2 = error;
				return res;
			});
			total++;
			if (ok)
			{
				okCount++;
			}
			else
			{
				fails.Add(((note != "") ? note : lp.ToString()) + "：" + e2);
			}
			SetTest(r, ok ? "✔ OK" : ("✘ " + e2), ok ? "Ok" : "Error");
			Log(string.Format("端口 {0}:{1}（{2}）：{3}", string.IsNullOrEmpty(la) ? "127.0.0.1" : la, lp, note, ok ? "OK，可连接" : ("失败 - " + e2)), ok ? LogLevel.Ok : LogLevel.Error);
		}
		Log("提示：本地端口 OK 只说明隧道在监听；目标数据库是否可用要以客户端实际连接为准。", LogLevel.Detail);
		_btnTest.IsEnabled = true;
		_testing = false;
		if (fromTray || !_main.IsShown)
		{
			string text = total == 0 ? "没有可测试的端口" : $"{okCount}/{total} 个本地端口可连接";
			if (fails.Count > 0)
			{
				text = text + "\n失败：" + string.Join("；", fails);
			}
			_main.Balloon(ProfileName, "测试端口", text, (total > 0 && fails.Count == 0) ? Win32.NIIF_INFO : Win32.NIIF_WARNING, force: true);
		}
	}

	public void UpdateStatus()
	{
		if (_disposed)
		{
			return;
		}
		TunnelState state = State;
		string text;
		switch (state)
		{
		case TunnelState.Connected:
		{
			TimeSpan t = _tunnel.ConnectedSince.HasValue ? (DateTime.Now - _tunnel.ConnectedSince.Value) : TimeSpan.Zero;
			text = $"已连接\u3000\u3000连接次数：{_tunnel.ConnectCount}\u3000\u3000本次已运行：{TunnelManager.Fmt(t)}";
			break;
		}
		case TunnelState.Connecting:
			text = $"连接中…（第 {_tunnel.AttemptCount} 次尝试，已成功连接 {_tunnel.ConnectCount} 次）";
			break;
		case TunnelState.Reconnecting:
			text = $"重连中…（已成功连接 {_tunnel.ConnectCount} 次）";
			break;
		default:
			text = "已停止";
			break;
		}
		if (_restarting)
		{
			text = "正在重新连接…";
		}
		Color c = MainWindow.ColorOf(state);
		if (!(_dot.Fill is SolidColorBrush b) || b.Color != c)
		{
			_dot.Fill = new SolidColorBrush(c);
		}
		if (_statusText.Text != text)
		{
			_statusText.Text = text;
		}
		bool running = _tunnel.Running;
		_btnStart.IsEnabled = !running && !_restarting && !_fetchingKey;
		_btnStop.IsEnabled = running && !_restarting;
		_btnReconnect.IsEnabled = running && !_restarting;
		NotifyTransition(state);
		StatusChanged?.Invoke();
	}

	private void NotifyTransition(TunnelState s)
	{
		TunnelState last = _lastState;
		if (s == last)
		{
			return;
		}
		_lastState = s;
		switch (s)
		{
		case TunnelState.Connected:
		{
			string addrs = (_runningForwards != null) ? string.Join("、", _runningForwards.Select(f => f.ClientAddress)) : "";
			_main.Balloon(ProfileName, _everConnected ? "隧道已重新连接" : "隧道已连接", addrs, Win32.NIIF_INFO);
			_everConnected = true;
			_outageNotified = false;
			break;
		}
		case TunnelState.Stopped:
		{
			bool userStopping = _userStopping;
			_userStopping = false;
			if (!_restarting)
			{
				if (!userStopping)
				{
					_main.Balloon(ProfileName, "隧道已停止", "隧道意外停止，详见日志。", Win32.NIIF_WARNING);
				}
				else if (!_main.IsShown)
				{
					_main.Balloon(ProfileName, "隧道已停止", "", Win32.NIIF_INFO);
				}
				_everConnected = false;
				_outageNotified = false;
			}
			break;
		}
		case TunnelState.Reconnecting:
			if (!_outageNotified)
			{
				_outageNotified = true;
				_lastFailBalloon = DateTime.Now;
				if (last == TunnelState.Connected || _everConnected)
				{
					_main.Balloon(ProfileName, "隧道连接断开", "正在自动重连…", Win32.NIIF_WARNING);
				}
				else
				{
					_main.Balloon(ProfileName, "隧道连接失败", "正在自动重试…（详见日志）", Win32.NIIF_WARNING);
				}
			}
			else if ((DateTime.Now - _lastFailBalloon).TotalMinutes >= 10.0)
			{
				_lastFailBalloon = DateTime.Now;
				_main.Balloon(ProfileName, "隧道仍未连上", "已尝试 " + _tunnel.AttemptCount + " 次，仍在自动重连…", Win32.NIIF_WARNING);
			}
			break;
		}
	}
}
