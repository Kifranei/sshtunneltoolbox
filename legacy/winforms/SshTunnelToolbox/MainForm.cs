using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using System.Windows.Forms.Layout;

namespace SshTunnelToolbox;

public class MainForm : Form
{
	private AppConfig _cfg;

	private string _loadWarn;

	private TabControl _tabs;

	private ImageList _dots;

	private NotifyIcon _tray;

	private ContextMenuStrip _trayMenu;

	private ContextMenuStrip _tabMenu;

	private readonly List<ToolStripItem> _trayDynamic = new List<ToolStripItem>();

	private ToolStripMenuItem _miAllStart;

	private ToolStripMenuItem _miAllStop;

	private ToolStripMenuItem _miShow;

	private readonly Dictionary<TunnelState, Icon> _icons = new Dictionary<TunnelState, Icon>();

	private readonly Dictionary<TunnelState, Bitmap> _dotBmps = new Dictionary<TunnelState, Bitmap>();

	private Timer _timer;

	private bool _hideOnStart;

	private bool _dataInit;

	private bool _layoutInit;

	private bool _exiting;

	private bool _balloonShown;

	public static readonly Color CGreen = Color.FromArgb(0, 160, 60);

	public static readonly Color CYellow = Color.FromArgb(230, 170, 0);

	public static readonly Color CGray = Color.FromArgb(150, 150, 150);

	private static Icon _appIcon;

	public static Icon AppIcon
	{
		get
		{
			if (_appIcon == null)
			{
				try
				{
					Assembly executingAssembly = Assembly.GetExecutingAssembly();
					string text = executingAssembly.GetManifestResourceNames().FirstOrDefault((string n) => n.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase));
					if (text != null)
					{
						using Stream stream = executingAssembly.GetManifestResourceStream(text);
						_appIcon = new Icon(stream);
					}
				}
				catch
				{
				}
				if (_appIcon == null)
				{
					_appIcon = SystemIcons.Application;
				}
			}
			return _appIcon;
		}
	}

	public IEnumerable<ProfileView> Views => from TabPage t in (IEnumerable)_tabs.TabPages
		select (ProfileView)((Control)t).Tag;

	private ProfileView CurrentView
	{
		get
		{
			TabPage selectedTab = _tabs.SelectedTab;
			if (selectedTab != null)
			{
				return (ProfileView)((Control)selectedTab).Tag;
			}
			return null;
		}
	}

	public MainForm()
		: this(trayArg: false)
	{
	}

	public MainForm(bool trayArg)
	{
		((Control)this).SuspendLayout();
		((ContainerControl)this).AutoScaleDimensions = new SizeF(96f, 96f);
		((ContainerControl)this).AutoScaleMode = (AutoScaleMode)2;
		((Control)this).Text = "SSH 隧道工具箱";
		((Control)this).Font = new Font("Microsoft YaHei UI", 9f);
		((Form)this).ClientSize = new Size(900, 780);
		((Control)this).MinimumSize = new Size(760, 640);
		((Form)this).StartPosition = (FormStartPosition)1;
		((Form)this).Icon = AppIcon;
		foreach (TunnelState value in Enum.GetValues(typeof(TunnelState)))
		{
			_icons[value] = MakeIcon(ColorOf(value));
		}
		Graphics val = Graphics.FromHwnd(IntPtr.Zero);
		float dpiX;
		try
		{
			dpiX = val.DpiX;
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		int num = Math.Max(12, (int)Math.Round(12f * dpiX / 96f));
		_dots = new ImageList
		{
			ImageSize = new Size(num, num),
			ColorDepth = (ColorDepth)32
		};
		foreach (TunnelState value2 in Enum.GetValues(typeof(TunnelState)))
		{
			Bitmap val2 = DotBitmap(ColorOf(value2), num);
			_dots.Images.Add(value2.ToString(), (Image)(object)val2);
			_dotBmps[value2] = DotBitmap(ColorOf(value2), 16);
		}
		_tabs = new TabControl
		{
			Dock = (DockStyle)5,
			ImageList = _dots,
			Padding = new Point(10, 4),
			HotTrack = true
		};
		((Control)_tabs).MouseUp += new MouseEventHandler(TabsMouseUp);
		((Control)_tabs).MouseDoubleClick += (MouseEventHandler)delegate(object s, MouseEventArgs e)
		{
			TabPage val5 = TabAt(e.Location);
			if (val5 != null)
			{
				_tabs.SelectedTab = val5;
				RenameCurrent();
			}
		};
		_tabs.SelectedIndexChanged += delegate
		{
			CurrentView?.UpdateStatus();
		};
		Panel val3 = new Panel
		{
			Dock = (DockStyle)5,
			Padding = new Padding(6, 2, 6, 6)
		};
		((Control)val3).Controls.Add((Control)(object)_tabs);
		((Control)this).Controls.Add((Control)(object)val3);
		FlowLayoutPanel val4 = new FlowLayoutPanel
		{
			Dock = (DockStyle)1,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			WrapContents = true,
			Padding = new Padding(6, 6, 6, 2)
		};
		((Control)val4).Controls.Add((Control)(object)SmallBtn("＋ 新建配置", delegate
		{
			NewProfile();
		}));
		((Control)val4).Controls.Add((Control)(object)SmallBtn("复制当前", delegate
		{
			CopyCurrent();
		}));
		((Control)val4).Controls.Add((Control)(object)SmallBtn("重命名", delegate
		{
			RenameCurrent();
		}));
		((Control)val4).Controls.Add((Control)(object)SmallBtn("删除", delegate
		{
			DeleteCurrent();
		}));
		((Control)val4).Controls.Add((Control)(object)SmallBtn("◀ 左移", delegate
		{
			MoveCurrent(-1);
		}));
		((Control)val4).Controls.Add((Control)(object)SmallBtn("右移 ▶", delegate
		{
			MoveCurrent(1);
		}));
		((Control)val4).Controls.Add((Control)new Label
		{
			Text = "|",
			AutoSize = true,
			ForeColor = Color.Silver,
			Margin = new Padding(6, 7, 6, 0)
		});
		((Control)val4).Controls.Add((Control)(object)SmallBtn("全部启动", delegate
		{
			StartAll();
		}));
		((Control)val4).Controls.Add((Control)(object)SmallBtn("全部停止", delegate
		{
			StopAll();
		}));
		((Control)val4).Controls.Add((Control)(object)SmallBtn("保存全部", delegate
		{
			SaveAll(showMsg: true);
		}));
		((Control)val4).Controls.Add((Control)(object)SmallBtn("⚙ 设置…", delegate
		{
			OpenSettings();
		}));
		((Control)this).Controls.Add((Control)(object)val4);
		_tabMenu = new ContextMenuStrip();
		((ToolStrip)_tabMenu).Items.Add("新建配置", (Image)null, (EventHandler)delegate
		{
			NewProfile();
		});
		((ToolStrip)_tabMenu).Items.Add("复制当前", (Image)null, (EventHandler)delegate
		{
			CopyCurrent();
		});
		((ToolStrip)_tabMenu).Items.Add("重命名…", (Image)null, (EventHandler)delegate
		{
			RenameCurrent();
		});
		((ToolStrip)_tabMenu).Items.Add("删除", (Image)null, (EventHandler)delegate
		{
			DeleteCurrent();
		});
		((ToolStrip)_tabMenu).Items.Add((ToolStripItem)new ToolStripSeparator());
		((ToolStrip)_tabMenu).Items.Add("左移", (Image)null, (EventHandler)delegate
		{
			MoveCurrent(-1);
		});
		((ToolStrip)_tabMenu).Items.Add("右移", (Image)null, (EventHandler)delegate
		{
			MoveCurrent(1);
		});
		_trayMenu = new ContextMenuStrip();
		_miAllStart = new ToolStripMenuItem("全部启动", (Image)null, (EventHandler)delegate
		{
			StartAll();
		});
		_miAllStop = new ToolStripMenuItem("全部停止", (Image)null, (EventHandler)delegate
		{
			StopAll();
		});
		_miShow = new ToolStripMenuItem("显示主窗口", (Image)null, (EventHandler)delegate
		{
			ToggleWindow();
		})
		{
			Font = new Font(((Control)_trayMenu).Font, (FontStyle)1)
		};
		((ToolStrip)_trayMenu).Items.AddRange((ToolStripItem[])(object)new ToolStripItem[7]
		{
			(ToolStripItem)_miAllStart,
			(ToolStripItem)_miAllStop,
			(ToolStripItem)new ToolStripSeparator(),
			(ToolStripItem)new ToolStripMenuItem("打开配置文件夹", (Image)null, (EventHandler)delegate
			{
				OpenConfigDir();
			}),
			(ToolStripItem)new ToolStripSeparator(),
			(ToolStripItem)_miShow,
			(ToolStripItem)new ToolStripMenuItem("退出", (Image)null, (EventHandler)delegate
			{
				ExitApp();
			})
		});
		((ToolStripDropDown)_trayMenu).Opening += delegate
		{
			BuildTrayMenu();
		};
		_tray = new NotifyIcon
		{
			Icon = _icons[TunnelState.Stopped],
			Text = "SSH 隧道：未运行",
			Visible = true,
			ContextMenuStrip = _trayMenu
		};
		_tray.MouseDoubleClick += (MouseEventHandler)delegate(object s, MouseEventArgs e)
		{
			if ((int)e.Button == 1048576)
			{
				ToggleWindow();
			}
		};
		_cfg = AppConfig.Load(out _loadWarn);
		foreach (Profile profile in _cfg.Profiles)
		{
			AddView(profile, select: false);
		}
		if (_tabs.TabCount > 0)
		{
			_tabs.SelectedIndex = Math.Max(0, Math.Min(_cfg.SelectedProfile, _tabs.TabCount - 1));
		}
		_hideOnStart = trayArg || _cfg.StartHidden;
		((Control)this).ResumeLayout(false);
		((Control)this).PerformLayout();
		_timer = new Timer
		{
			Interval = 1000
		};
		_timer.Tick += delegate
		{
			foreach (ProfileView view in Views)
			{
				view.UpdateStatus();
			}
			UpdateTray();
		};
		_timer.Start();
		((Form)this).Load += delegate
		{
			OnLoaded();
		};
		((Control)this).Resize += delegate
		{
			if ((int)((Form)this).WindowState == 1 && _cfg.MinimizeToTray)
			{
				HideToTray();
			}
		};
		((Form)this).FormClosing += new FormClosingEventHandler(OnClosing);
	}

	public static Button SmallBtn(string text, EventHandler h)
	{
		Button val = new Button
		{
			Text = text,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			Padding = new Padding(6, 0, 6, 0),
			Margin = new Padding(3),
			UseVisualStyleBackColor = true
		};
		((Control)val).Click += h;
		return val;
	}

	public static Button BigBtn(string text, Color fore, EventHandler h)
	{
		Button val = new Button
		{
			Text = text,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			Padding = new Padding(12, 6, 12, 6),
			Margin = new Padding(3),
			ForeColor = fore,
			Font = new Font("Microsoft YaHei UI", 10f, (FontStyle)1),
			UseVisualStyleBackColor = true
		};
		((Control)val).Click += h;
		return val;
	}

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

	public static int FlowPlace(List<Control[]> units, int startX, int wrapX, int W, int y, int h, int gy, int gapUnit, int gapIn)
	{
		int num = startX;
		bool flag = true;
		foreach (Control[] unit in units)
		{
			Control[] array = unit;
			foreach (Control val in array)
			{
				if (!(val is ComboBox))
				{
					val.Size = val.PreferredSize;
				}
			}
			int num2 = unit.Sum((Control c) => c.Width) + gapIn * (unit.Length - 1);
			if (!flag && num + num2 > W)
			{
				num = wrapX;
				y += h + gy;
			}
			array = unit;
			foreach (Control val2 in array)
			{
				int num3 = ((val2 is ComboBox) ? val2.Height : val2.PreferredSize.Height);
				val2.SetBounds(num, y + Math.Max(0, (h - num3) / 2), val2.Width, num3);
				num += val2.Width + gapIn;
			}
			num += gapUnit - gapIn;
			flag = false;
		}
		return y;
	}

	private static Bitmap DotBitmap(Color c, int sz)
	{
		Bitmap val = new Bitmap(sz, sz);
		Graphics val2 = Graphics.FromImage((Image)(object)val);
		try
		{
			val2.SmoothingMode = (SmoothingMode)4;
			val2.Clear(Color.Transparent);
			float num = (float)sz * 0.12f;
			SolidBrush val3 = new SolidBrush(c);
			try
			{
				val2.FillEllipse((Brush)(object)val3, num, num, (float)sz - 2f * num, (float)sz - 2f * num);
				return val;
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private static Icon MakeIcon(Color c)
	{
		Size size = SystemInformation.SmallIconSize;
		if (size.Width < 16)
		{
			size = new Size(16, 16);
		}
		Icon val;
		try
		{
			val = new Icon(AppIcon, size);
		}
		catch
		{
			val = AppIcon;
		}
		Bitmap val2 = new Bitmap(size.Width, size.Height);
		try
		{
			Graphics val3 = Graphics.FromImage((Image)(object)val2);
			try
			{
				val3.SmoothingMode = (SmoothingMode)4;
				val3.InterpolationMode = (InterpolationMode)7;
				val3.Clear(Color.Transparent);
				val3.DrawIcon(val, new Rectangle(0, 0, size.Width, size.Height));
				float num = (float)size.Width * 0.5f;
				RectangleF rectangleF = new RectangleF((float)size.Width - num - 0.5f, (float)size.Height - num - 0.5f, num, num);
				SolidBrush val4 = new SolidBrush(c);
				try
				{
					val3.FillEllipse((Brush)(object)val4, rectangleF);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
				Pen val5 = new Pen(Color.White, Math.Max(1f, (float)size.Width / 16f));
				try
				{
					val3.DrawEllipse(val5, rectangleF);
				}
				finally
				{
					((IDisposable)val5)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			return Icon.FromHandle(val2.GetHicon());
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private int S(int v)
	{
		return (int)Math.Round((double)(v * ((Control)this).DeviceDpi) / 96.0);
	}

	private TabPage PageOf(ProfileView v)
	{
		return ((IEnumerable)_tabs.TabPages).Cast<TabPage>().FirstOrDefault((Func<TabPage, bool>)((TabPage t) => ((Control)t).Tag == v));
	}

	private ProfileView AddView(Profile p, bool select)
	{
		TabPage page = new TabPage(p.Name)
		{
			ImageKey = TunnelState.Stopped.ToString(),
			UseVisualStyleBackColor = true,
			Padding = new Padding(0)
		};
		ProfileView v = new ProfileView(this, p);
		((Control)page).Tag = v;
		((Control)page).Controls.Add((Control)(object)v);
		v.StatusChanged += delegate
		{
			UpdateTab(page, v);
		};
		_tabs.TabPages.Add(page);
		if (select)
		{
			_tabs.SelectedTab = page;
		}
		return v;
	}

	private void UpdateTab(TabPage page, ProfileView v)
	{
		string text = v.State.ToString();
		if (page.ImageKey != text)
		{
			page.ImageKey = text;
		}
		string profileName = v.ProfileName;
		if (((Control)page).Text != profileName)
		{
			((Control)page).Text = profileName;
		}
		string text2 = v.ProfileName + "：" + v.ShortStatus;
		if (page.ToolTipText != text2)
		{
			page.ToolTipText = text2;
		}
	}

	private TabPage TabAt(Point pt)
	{
		for (int i = 0; i < _tabs.TabCount; i++)
		{
			if (_tabs.GetTabRect(i).Contains(pt))
			{
				return _tabs.TabPages[i];
			}
		}
		return null;
	}

	private void TabsMouseUp(object sender, MouseEventArgs e)
	{
		if ((int)e.Button == 2097152)
		{
			TabPage val = TabAt(e.Location);
			if (val != null)
			{
				_tabs.SelectedTab = val;
				((ToolStripDropDown)_tabMenu).Show((Control)(object)_tabs, e.Location);
			}
		}
	}

	public void ShowProfile(ProfileView v)
	{
		ShowFromTray();
		TabPage val = PageOf(v);
		if (val != null)
		{
			_tabs.SelectedTab = val;
		}
	}

	private void LogGlobal(string msg, LogLevel lv)
	{
		(CurrentView ?? Views.FirstOrDefault())?.Log(msg, lv);
	}

	private string UniqueName(string baseName, ProfileView except)
	{
		HashSet<string> hashSet = new HashSet<string>(from v in Views
			where v != except
			select v.ProfileName, StringComparer.OrdinalIgnoreCase);
		if (!hashSet.Contains(baseName))
		{
			return baseName;
		}
		int num = 2;
		string text;
		while (true)
		{
			text = baseName + " (" + num + ")";
			if (!hashSet.Contains(text))
			{
				break;
			}
			num++;
		}
		return text;
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
		if (Views.Any((ProfileView v) => v != except && string.Equals(v.ProfileName, name, StringComparison.OrdinalIgnoreCase)))
		{
			return "已有同名配置：" + name;
		}
		return null;
	}

	private void NewProfile()
	{
		InputForm inputForm = new InputForm("新建配置", "配置名称（例如「办公室」「机房 A」）：", UniqueName("新配置", null), (string n) => ValidateName(n, null));
		try
		{
			if ((int)((Form)inputForm).ShowDialog((IWin32Window)(object)this) == 1)
			{
				Profile profile = new Profile
				{
					Name = inputForm.Value,
					Port = 22,
					ReconnectDelaySeconds = 5
				};
				ProfileView profileView = AddView(profile, select: true);
				SaveAll(showMsg: false);
				profileView.Log("已新建配置「" + profile.Name + "」：请填写主机、用户名、密码、主机指纹，并添加端口转发。", LogLevel.Info);
			}
		}
		finally
		{
			((IDisposable)inputForm)?.Dispose();
		}
	}

	private void CopyCurrent()
	{
		ProfileView currentView = CurrentView;
		if (currentView == null)
		{
			return;
		}
		currentView.Commit();
		InputForm inputForm = new InputForm("复制配置", "新配置名称：", UniqueName(currentView.ProfileName + " - 副本", null), (string n) => ValidateName(n, null));
		try
		{
			if ((int)((Form)inputForm).ShowDialog((IWin32Window)(object)this) == 1)
			{
				Profile profile = currentView.Saved.Clone();
				profile.Name = inputForm.Value;
				profile.AutoConnect = false;
				ProfileView profileView = AddView(profile, select: true);
				SaveAll(showMsg: false);
				profileView.Log("已从「" + currentView.ProfileName + "」复制（含端口转发和密码）。注意：两个配置的本地端口相同时不能同时运行。", LogLevel.Info);
			}
		}
		finally
		{
			((IDisposable)inputForm)?.Dispose();
		}
	}

	private void RenameCurrent()
	{
		ProfileView cur = CurrentView;
		if (cur == null)
		{
			return;
		}
		InputForm inputForm = new InputForm("重命名配置", "配置名称：", cur.ProfileName, (string n) => ValidateName(n, cur));
		try
		{
			if ((int)((Form)inputForm).ShowDialog((IWin32Window)(object)this) == 1 && !(inputForm.Value == cur.ProfileName))
			{
				string profileName = cur.ProfileName;
				cur.Rename(inputForm.Value);
				UpdateTab(_tabs.SelectedTab, cur);
				SaveAll(showMsg: false);
				cur.Log("配置「" + profileName + "」已重命名为「" + inputForm.Value + "」", LogLevel.Ok);
			}
		}
		finally
		{
			((IDisposable)inputForm)?.Dispose();
		}
	}

	private void DeleteCurrent()
	{
		ProfileView currentView = CurrentView;
		if (currentView != null)
		{
			if (currentView.Running)
			{
				MessageBox.Show((IWin32Window)(object)this, "配置「" + currentView.ProfileName + "」的隧道正在运行，请先停止再删除。", "SSH 隧道工具箱", (MessageBoxButtons)0, (MessageBoxIcon)64);
			}
			else if (_tabs.TabCount <= 1)
			{
				MessageBox.Show((IWin32Window)(object)this, "至少要保留一个配置。", "SSH 隧道工具箱");
			}
			else if ((int)MessageBox.Show((IWin32Window)(object)this, "确定删除配置「" + currentView.ProfileName + "」吗？\n（主机、密码、端口转发都会删除，无法恢复）", "SSH 隧道工具箱", (MessageBoxButtons)4, (MessageBoxIcon)48, (MessageBoxDefaultButton)256) == 6)
			{
				TabPage selectedTab = _tabs.SelectedTab;
				int selectedIndex = _tabs.SelectedIndex;
				_tabs.TabPages.Remove(selectedTab);
				((Component)(object)currentView).Dispose();
				((Component)(object)selectedTab).Dispose();
				_tabs.SelectedIndex = Math.Max(0, Math.Min(selectedIndex, _tabs.TabCount - 1));
				SaveAll(showMsg: false);
				LogGlobal("已删除配置「" + currentView.ProfileName + "」", LogLevel.Warn);
			}
		}
	}

	private void MoveCurrent(int delta)
	{
		int selectedIndex = _tabs.SelectedIndex;
		int num = selectedIndex + delta;
		if (selectedIndex >= 0 && num >= 0 && num < _tabs.TabCount)
		{
			TabPage selectedTab = _tabs.SelectedTab;
			((Control)_tabs).SuspendLayout();
			_tabs.TabPages.Remove(selectedTab);
			_tabs.TabPages.Insert(num, selectedTab);
			_tabs.SelectedTab = selectedTab;
			((Control)_tabs).ResumeLayout();
			SaveAll(showMsg: false);
		}
	}

	private void StartAll()
	{
		foreach (ProfileView item in Views.ToList())
		{
			if (!item.Running)
			{
				item.Start();
			}
		}
	}

	private void StopAll()
	{
		foreach (ProfileView item in Views.ToList())
		{
			if (item.Running)
			{
				item.Stop();
			}
		}
	}

	public bool SaveAll(bool showMsg)
	{
		List<string> list = new List<string>();
		foreach (ProfileView view in Views)
		{
			string text = view.Commit();
			if (text != null)
			{
				list.Add("「" + view.ProfileName + "」" + text);
			}
		}
		_cfg.Profiles = Views.Select((ProfileView v) => v.Saved.Clone()).ToList();
		_cfg.SelectedProfile = Math.Max(0, _tabs.SelectedIndex);
		try
		{
			_cfg.Save();
			if (showMsg)
			{
				LogGlobal("配置已保存（" + _cfg.Profiles.Count + " 个配置，密码已用 Windows DPAPI 加密）：" + AppConfig.FilePath, LogLevel.Ok);
				if (list.Count > 0)
				{
					MessageBox.Show((IWin32Window)(object)this, "以下配置有错误，这些配置的修改没有保存：\n\n" + string.Join("\n", list), "SSH 隧道工具箱", (MessageBoxButtons)0, (MessageBoxIcon)48);
				}
			}
			return list.Count == 0;
		}
		catch (Exception ex)
		{
			LogGlobal("保存配置失败：" + ex.Message, LogLevel.Error);
			return false;
		}
	}

	public string EnsurePlink(ProfileView v)
	{
		string text = (_cfg.PlinkPath ?? "").Trim();
		if (File.Exists(text))
		{
			return text;
		}
		string text2 = Plink.Detect();
		if (text2 != null)
		{
			_cfg.PlinkPath = text2;
			v.Log("找到 plink：" + text2, LogLevel.Ok);
			try
			{
				_cfg.Save();
			}
			catch
			{
			}
			return text2;
		}
		v.Log("没有找到 plink.exe（已查找 C:\\Program Files\\PuTTY、C:\\Program Files (x86)\\PuTTY 和 PATH）。请安装 PuTTY，或在「⚙ 设置…」里选择 plink.exe。", LogLevel.Error);
		ShowProfile(v);
		MessageBox.Show((IWin32Window)(object)this, "找不到 plink.exe：\n" + text + "\n\n请安装 PuTTY，或点「⚙ 设置…」选择 plink.exe。", "SSH 隧道工具箱", (MessageBoxButtons)0, (MessageBoxIcon)48);
		return null;
	}

	private static bool AddrOverlap(string a, string b)
	{
		a = (string.IsNullOrWhiteSpace(a) ? "127.0.0.1" : a.Trim());
		b = (string.IsNullOrWhiteSpace(b) ? "127.0.0.1" : b.Trim());
		if (!(a == b) && !(a == "0.0.0.0"))
		{
			return b == "0.0.0.0";
		}
		return true;
	}

	public string FindPortConflict(ProfileView self, List<Forward> forwards)
	{
		foreach (ProfileView view in Views)
		{
			if (view == self)
			{
				continue;
			}
			List<Forward> runningForwards = view.RunningForwards;
			if (runningForwards == null)
			{
				continue;
			}
			foreach (Forward f in forwards)
			{
				Forward forward = runningForwards.FirstOrDefault((Forward t) => t.LocalPort == f.LocalPort && AddrOverlap(t.LocalAddress, f.LocalAddress));
				if (forward != null)
				{
					return string.Format("本地端口 {0} 已被配置「{1}」使用（「{1}」正在运行{2}）。请先停止「{1}」，或修改本配置的本地端口。", f.LocalPort, view.ProfileName, string.IsNullOrEmpty(forward.Note) ? "" : ("，用于「" + forward.Note + "」"));
				}
			}
		}
		return null;
	}

	private void OpenSettings()
	{
		SettingsForm settingsForm = new SettingsForm(_cfg, AutoStart.IsEnabled);
		try
		{
			if ((int)((Form)settingsForm).ShowDialog((IWin32Window)(object)this) != 1)
			{
				return;
			}
			_cfg.PlinkPath = settingsForm.PlinkPath;
			_cfg.CloseAction = settingsForm.CloseAction;
			_cfg.MinimizeToTray = settingsForm.MinimizeToTray;
			_cfg.StartHidden = settingsForm.StartHidden;
			_cfg.Balloons = settingsForm.Balloons;
			if (settingsForm.AutoStartOn != AutoStart.IsEnabled)
			{
				try
				{
					AutoStart.Set(settingsForm.AutoStartOn);
					LogGlobal(settingsForm.AutoStartOn ? ("已开启开机自启（登录后在托盘中启动）：" + AutoStart.Command) : "已关闭开机自启", LogLevel.Ok);
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
			if (!File.Exists(_cfg.PlinkPath))
			{
				LogGlobal("注意：plink.exe 不存在：" + _cfg.PlinkPath, LogLevel.Warn);
			}
		}
		finally
		{
			((IDisposable)settingsForm)?.Dispose();
		}
	}

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

	protected override void SetVisibleCore(bool value)
	{
		if (_hideOnStart && value)
		{
			_hideOnStart = false;
			if (!((Control)this).IsHandleCreated)
			{
				CreateHandle();
			}
			base.SetVisibleCore(false);
			InitData(hidden: true);
		}
		else
		{
			base.SetVisibleCore(value);
		}
	}

	private void OnLoaded()
	{
		if (!_layoutInit)
		{
			_layoutInit = true;
			Rectangle workingArea = Screen.FromControl((Control)(object)this).WorkingArea;
			((Control)this).MinimumSize = new Size(Math.Min(S(760), workingArea.Width), Math.Min(S(640), workingArea.Height));
			if (((Control)this).Height > workingArea.Height)
			{
				((Control)this).Top = workingArea.Top;
				((Control)this).Height = workingArea.Height;
			}
			if (((Control)this).Width > workingArea.Width)
			{
				((Control)this).Left = workingArea.Left;
				((Control)this).Width = workingArea.Width;
			}
		}
		InitData(hidden: false);
	}

	private void InitData(bool hidden)
	{
		if (_dataInit)
		{
			return;
		}
		_dataInit = true;
		LogGlobal("配置文件：" + AppConfig.FilePath + "（共 " + _tabs.TabCount + " 个配置）", LogLevel.Detail);
		if (_loadWarn != null)
		{
			LogGlobal(_loadWarn, LogLevel.Warn);
		}
		if (AutoStart.IsStale)
		{
			try
			{
				AutoStart.Set(enable: true);
				LogGlobal("开机自启路径已更新为当前程序：" + Application.ExecutablePath, LogLevel.Detail);
			}
			catch
			{
			}
		}
		if (!File.Exists(_cfg.PlinkPath))
		{
			string text = Plink.Detect();
			if (text != null)
			{
				_cfg.PlinkPath = text;
				LogGlobal("找到 plink：" + text, LogLevel.Ok);
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
				LogGlobal("没有找到 plink.exe，请安装 PuTTY，或在「⚙ 设置…」里选择 plink.exe。", LogLevel.Error);
			}
		}
		foreach (ProfileView view in Views)
		{
			view.UpdateStatus();
		}
		UpdateTray();
		List<ProfileView> list = Views.Where((ProfileView v) => v.Saved.AutoConnect).ToList();
		if (hidden)
		{
			LogGlobal("已在托盘中启动（双击托盘图标显示窗口）", LogLevel.Detail);
			if (list.Count == 0)
			{
				Balloon(null, "已在托盘中运行", "双击图标显示窗口；右键可启动隧道。", (ToolTipIcon)1);
			}
		}
		foreach (ProfileView item in list)
		{
			item.Log("已勾选「程序启动时自动连接此配置」，正在连接…", LogLevel.Info);
			item.Start();
		}
	}

	public void Balloon(string profile, string title, string text, ToolTipIcon icon, bool force = false, int ms = 4000)
	{
		if (!force && !_cfg.Balloons)
		{
			return;
		}
		string text2 = ((profile != null) ? ("「" + profile + "」") : "") + title;
		if (text2.Length > 63)
		{
			text2 = text2.Substring(0, 63);
		}
		string text3 = (string.IsNullOrEmpty(text) ? " " : text);
		if (text3.Length > 255)
		{
			text3 = text3.Substring(0, 252) + "…";
		}
		try
		{
			_tray.ShowBalloonTip(ms, text2, text3, icon);
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
		List<ProfileView> source = Views.ToList();
		int num = source.Count((ProfileView v) => v.State == TunnelState.Connected);
		int num2 = source.Count((ProfileView v) => v.State == TunnelState.Connecting || v.State == TunnelState.Reconnecting);
		TunnelState key = ((num2 > 0) ? TunnelState.Reconnecting : ((num > 0) ? TunnelState.Connected : TunnelState.Stopped));
		if (_tray.Icon != _icons[key])
		{
			_tray.Icon = _icons[key];
		}
		string text;
		if (num == 0 && num2 == 0)
		{
			text = "SSH 隧道：未运行";
		}
		else
		{
			List<string> list = new List<string>();
			if (num > 0)
			{
				list.Add(num + " 个已连接");
			}
			if (num2 > 0)
			{
				list.Add(num2 + " 个连接中/重连中");
			}
			text = "SSH 隧道：" + string.Join("，", list);
			if (source.Count((ProfileView v) => v.Running) == 1)
			{
				ProfileView profileView = source.First((ProfileView v) => v.Running);
				string text2 = "SSH 隧道：" + profileView.ProfileName + " " + profileView.ShortStatus;
				if (text2.Length <= 63)
				{
					text = text2;
				}
			}
		}
		if (text.Length > 63)
		{
			text = text.Substring(0, 63);
		}
		if (_tray.Text != text)
		{
			_tray.Text = text;
		}
	}

	private void BuildTrayMenu()
	{
		foreach (ToolStripItem item in _trayDynamic)
		{
			((ToolStrip)_trayMenu).Items.Remove(item);
			((Component)(object)item).Dispose();
		}
		_trayDynamic.Clear();
		int num = 0;
		foreach (ProfileView item2 in Views.ToList())
		{
			ProfileView view = item2;
			TunnelState state = item2.State;
			ToolStripMenuItem val = new ToolStripMenuItem(item2.ProfileName + "    " + item2.ShortStatus)
			{
				Image = (Image)(object)_dotBmps[state]
			};
			((ToolStripDropDownItem)val).DropDownItems.Add((ToolStripItem)new ToolStripMenuItem("● " + item2.ShortStatus)
			{
				Enabled = false,
				ForeColor = ColorOf(state)
			});
			((ToolStripDropDownItem)val).DropDownItems.Add((ToolStripItem)new ToolStripSeparator());
			((ToolStripDropDownItem)val).DropDownItems.Add((ToolStripItem)new ToolStripMenuItem("启动隧道", (Image)null, (EventHandler)delegate
			{
				view.Start();
			})
			{
				Enabled = (!item2.Running && !item2.Restarting)
			});
			((ToolStripDropDownItem)val).DropDownItems.Add((ToolStripItem)new ToolStripMenuItem("停止隧道", (Image)null, (EventHandler)delegate
			{
				view.Stop();
			})
			{
				Enabled = (item2.Running && !item2.Restarting)
			});
			((ToolStripDropDownItem)val).DropDownItems.Add((ToolStripItem)new ToolStripMenuItem("重新连接", (Image)null, (EventHandler)delegate
			{
				view.Reconnect();
			})
			{
				Enabled = (item2.Running && !item2.Restarting)
			});
			((ToolStripDropDownItem)val).DropDownItems.Add((ToolStripItem)new ToolStripSeparator());
			((ToolStripDropDownItem)val).DropDownItems.Add((ToolStripItem)new ToolStripMenuItem("测试端口", (Image)null, (EventHandler)delegate
			{
				view.TestPorts(fromTray: true);
			})
			{
				Enabled = !item2.Testing
			});
			ToolStripMenuItem val2 = new ToolStripMenuItem("复制地址");
			foreach (Forward item3 in item2.TableForwards())
			{
				string addr = item3.ClientAddress;
				((ToolStripDropDownItem)val2).DropDownItems.Add((ToolStripItem)new ToolStripMenuItem((string.IsNullOrEmpty(item3.Note) ? "" : (item3.Note + "  ")) + addr, (Image)null, (EventHandler)delegate
				{
					CopyText(addr);
				}));
			}
			if (((ArrangedElementCollection)((ToolStripDropDownItem)val2).DropDownItems).Count == 0)
			{
				((ToolStripDropDownItem)val2).DropDownItems.Add((ToolStripItem)new ToolStripMenuItem("（没有端口转发）")
				{
					Enabled = false
				});
			}
			((ToolStripDropDownItem)val).DropDownItems.Add((ToolStripItem)(object)val2);
			((ToolStripDropDownItem)val).DropDownItems.Add((ToolStripItem)new ToolStripSeparator());
			((ToolStripDropDownItem)val).DropDownItems.Add((ToolStripItem)new ToolStripMenuItem("显示此配置", (Image)null, (EventHandler)delegate
			{
				ShowProfile(view);
			}));
			((ToolStrip)_trayMenu).Items.Insert(num++, (ToolStripItem)(object)val);
			_trayDynamic.Add((ToolStripItem)(object)val);
		}
		ToolStripSeparator val3 = new ToolStripSeparator();
		((ToolStrip)_trayMenu).Items.Insert(num, (ToolStripItem)(object)val3);
		_trayDynamic.Add((ToolStripItem)(object)val3);
		((ToolStripItem)_miAllStart).Enabled = Views.Any((ProfileView v) => !v.Running);
		((ToolStripItem)_miAllStop).Enabled = Views.Any((ProfileView v) => v.Running);
		((ToolStripItem)_miShow).Text = ((((Control)this).Visible && (int)((Form)this).WindowState != 1) ? "隐藏到托盘" : "显示主窗口");
	}

	private void CopyText(string text)
	{
		for (int i = 0; i < 5; i++)
		{
			try
			{
				Clipboard.SetText(text);
				Balloon(null, "已复制", text, (ToolTipIcon)1, force: true, 1500);
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
		((Control)this).Hide();
		if (!_balloonShown)
		{
			_balloonShown = true;
			Balloon(null, "SSH 隧道工具箱", "已最小化到托盘，双击图标可恢复；右键可按配置启动 / 停止 / 重连。", (ToolTipIcon)1);
		}
	}

	private void ShowFromTray()
	{
		((Control)this).Show();
		if ((int)((Form)this).WindowState == 1)
		{
			((Form)this).WindowState = (FormWindowState)0;
		}
		((Form)this).Activate();
	}

	private void ToggleWindow()
	{
		if (((Control)this).Visible && (int)((Form)this).WindowState != 1)
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
		((Form)this).Close();
	}

	private void OnClosing(object sender, FormClosingEventArgs e)
	{
		if (!_exiting && (int)e.CloseReason == 3)
		{
			string text = _cfg.CloseAction;
			if (text == "ask")
			{
				CloseAskForm closeAskForm = new CloseAskForm(Views.Any((ProfileView v) => v.Running));
				try
				{
					if ((int)((Form)closeAskForm).ShowDialog((IWin32Window)(object)this) != 1 || closeAskForm.Choice == null)
					{
						((CancelEventArgs)(object)e).Cancel = true;
						return;
					}
					text = closeAskForm.Choice;
					if (closeAskForm.Remember)
					{
						_cfg.CloseAction = text;
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
					((IDisposable)closeAskForm)?.Dispose();
				}
			}
			if (text == "tray")
			{
				((CancelEventArgs)(object)e).Cancel = true;
				HideToTray();
				return;
			}
		}
		try
		{
			SaveAll(showMsg: false);
		}
		catch
		{
		}
		_timer.Stop();
		try
		{
			Task.WaitAll((from v in Views.ToList()
				select Task.Run(delegate
				{
					v.StopNow();
				})).ToArray(), 8000);
		}
		catch
		{
		}
		_tray.Visible = false;
		((Component)(object)_tray).Dispose();
	}
}
