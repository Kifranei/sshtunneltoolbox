using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SshTunnelToolbox;

public class ProfileView : UserControl
{
	private readonly MainForm _main;

	private readonly TunnelManager _tunnel = new TunnelManager();

	private Profile _saved;

	private List<Forward> _runningForwards;

	private TextBox _host;

	private TextBox _user;

	private TextBox _pw;

	private TextBox _hostKey;

	private NumericUpDown _port;

	private NumericUpDown _delay;

	private CheckBox _showPw;

	private CheckBox _autoConnect;

	private CheckBox _verbose;

	private DataGridView _grid;

	private Button _btnStart;

	private Button _btnStop;

	private Button _btnReconnect;

	private Button _btnTest;

	private Label _dot;

	private Label _statusText;

	private RichTextBox _log;

	private SplitContainer _split;

	private Panel _settings;

	private Label _lblHost;

	private Label _lblPort;

	private Label _lblUser;

	private Label _lblPw;

	private Label _lblKey;

	private Label _lblDelay;

	private Label _lblSec;

	private CheckBox _useJump;

	private TextBox _jumpHost;

	private NumericUpDown _jumpPort;

	private TextBox _jumpUser;

	private TextBox _jumpPw;

	private CheckBox _showJumpPw;

	private TextBox _jumpHostKey;

	private Label _lblJHost;

	private Label _lblJPort;

	private Label _lblJUser;

	private Label _lblJPw;

	private Label _lblJKey;

	private Label _jumpHint;

	private ToolTip _tips;

	private Button _btnKey;

	private Button _btnJumpKey;

	private bool _fetchingKey;

	private bool _autoFetchDone;

	private bool _inLayout;

	private bool _splitInit;

	private bool _testing;

	private bool _restarting;

	private bool _userStopping;

	private TunnelState _lastState;

	private bool _everConnected;

	private bool _outageNotified;

	private DateTime _lastFailBalloon = DateTime.MinValue;

	public string ProfileName => _saved.Name;

	public Profile Saved => _saved;

	public bool Running => _tunnel.Running;

	public bool Testing => _testing;

	public bool Restarting => _restarting;

	public TunnelState State
	{
		get
		{
			if (!_tunnel.Running)
			{
				return TunnelState.Stopped;
			}
			return _tunnel.State;
		}
	}

	public List<Forward> RunningForwards
	{
		get
		{
			if (!_tunnel.Running)
			{
				return null;
			}
			return _runningForwards;
		}
	}

	public string ShortStatus => State switch
	{
		TunnelState.Connected => "已连接 " + TunnelManager.Fmt(_tunnel.ConnectedSince.HasValue ? (DateTime.Now - _tunnel.ConnectedSince.Value) : TimeSpan.Zero), 
		TunnelState.Connecting => "连接中…（第 " + _tunnel.AttemptCount + " 次）", 
		TunnelState.Reconnecting => "重连中…", 
		_ => "已停止", 
	};

	public event Action StatusChanged;

	public ProfileView(MainForm main, Profile p)
	{
		_main = main;
		_saved = p.Clone();
		_tunnel.Name = p.Name;
		((Control)this).SuspendLayout();
		((Control)this).Dock = (DockStyle)5;
		((Control)this).Font = ((Control)main).Font;
		_split = new SplitContainer
		{
			Dock = (DockStyle)5,
			Orientation = (Orientation)0,
			SplitterWidth = 6
		};
		((Control)this).Controls.Add((Control)(object)_split);
		GroupBox val = new GroupBox
		{
			Text = "端口转发（-L）",
			Dock = (DockStyle)5,
			Padding = new Padding(8, 4, 8, 6)
		};
		_grid = new DataGridView
		{
			Dock = (DockStyle)5,
			AllowUserToAddRows = false,
			AllowUserToDeleteRows = true,
			RowHeadersVisible = false,
			AutoSizeColumnsMode = (DataGridViewAutoSizeColumnsMode)16,
			ColumnHeadersHeightSizeMode = (DataGridViewColumnHeadersHeightSizeMode)2,
			AutoSizeRowsMode = (DataGridViewAutoSizeRowsMode)7,
			SelectionMode = (DataGridViewSelectionMode)1,
			MultiSelect = true,
			BackgroundColor = SystemColors.Window,
			BorderStyle = (BorderStyle)1
		};
		_grid.Columns.Add((DataGridViewColumn)new DataGridViewTextBoxColumn
		{
			Name = "Note",
			HeaderText = "备注",
			FillWeight = 24f,
			MinimumWidth = 60
		});
		_grid.Columns.Add((DataGridViewColumn)new DataGridViewTextBoxColumn
		{
			Name = "LAddr",
			HeaderText = "本地地址",
			FillWeight = 16f,
			MinimumWidth = 60
		});
		_grid.Columns.Add((DataGridViewColumn)new DataGridViewTextBoxColumn
		{
			Name = "LPort",
			HeaderText = "本地端口",
			FillWeight = 11f,
			MinimumWidth = 50
		});
		_grid.Columns.Add((DataGridViewColumn)new DataGridViewTextBoxColumn
		{
			Name = "THost",
			HeaderText = "目标主机",
			FillWeight = 20f,
			MinimumWidth = 60
		});
		_grid.Columns.Add((DataGridViewColumn)new DataGridViewTextBoxColumn
		{
			Name = "TPort",
			HeaderText = "目标端口",
			FillWeight = 11f,
			MinimumWidth = 50
		});
		_grid.Columns.Add((DataGridViewColumn)new DataGridViewTextBoxColumn
		{
			Name = "Test",
			HeaderText = "测试结果",
			FillWeight = 18f,
			MinimumWidth = 60,
			ReadOnly = true
		});
		((Control)val).Controls.Add((Control)(object)_grid);
		FlowLayoutPanel val2 = new FlowLayoutPanel
		{
			Dock = (DockStyle)2,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			WrapContents = false,
			Padding = new Padding(0, 4, 0, 0)
		};
		((Control)val2).Controls.Add((Control)(object)MainForm.SmallBtn("添加一行", delegate
		{
			int num = _grid.Rows.Add(new object[6] { "", "127.0.0.1", "", "", "", "" });
			_grid.CurrentCell = _grid.Rows[num].Cells[0];
			_grid.BeginEdit(true);
		}));
		((Control)val2).Controls.Add((Control)(object)MainForm.SmallBtn("删除选中行", delegate
		{
			DeleteRows();
		}));
		((Control)val2).Controls.Add((Control)(object)MainForm.SmallBtn("从 ssh 命令导入…", delegate
		{
			ImportSshCommand();
		}));
		((Control)val2).Controls.Add((Control)new Label
		{
			Text = "提示：目标主机由 SSH 服务器去连接，可填其它网段地址",
			AutoSize = true,
			ForeColor = Color.Gray,
			Margin = new Padding(8, 7, 3, 0)
		});
		((Control)val).Controls.Add((Control)(object)val2);
		((Control)_split.Panel1).Controls.Add((Control)(object)val);
		((Control)_split.Panel1).Padding = new Padding(6, 0, 6, 0);
		((Control)_split.Panel2).Padding = new Padding(6, 0, 6, 6);
		_log = new RichTextBox
		{
			Dock = (DockStyle)5,
			ReadOnly = true,
			BackColor = Color.White,
			DetectUrls = false,
			Font = new Font("Consolas", 9f),
			WordWrap = true,
			HideSelection = false
		};
		((Control)_split.Panel2).Controls.Add((Control)(object)_log);
		FlowLayoutPanel val3 = new FlowLayoutPanel
		{
			Dock = (DockStyle)1,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			WrapContents = false,
			Padding = new Padding(0, 2, 0, 4)
		};
		_dot = new Label
		{
			Text = "●",
			AutoSize = true,
			Font = new Font("Microsoft YaHei UI", 15f),
			ForeColor = MainForm.CGray,
			Margin = new Padding(3, 0, 0, 0)
		};
		_statusText = new Label
		{
			Text = "已停止",
			AutoSize = true,
			Font = new Font("Microsoft YaHei UI", 11f, (FontStyle)1),
			Margin = new Padding(2, 6, 3, 0)
		};
		((Control)val3).Controls.Add((Control)(object)_dot);
		((Control)val3).Controls.Add((Control)(object)_statusText);
		((Control)_split.Panel2).Controls.Add((Control)(object)val3);
		FlowLayoutPanel val4 = new FlowLayoutPanel
		{
			Dock = (DockStyle)1,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			WrapContents = true,
			Padding = new Padding(0, 4, 0, 0)
		};
		_btnStart = MainForm.BigBtn("启动隧道", MainForm.CGreen, delegate
		{
			Start();
		});
		_btnStop = MainForm.BigBtn("停止隧道", Color.FromArgb(180, 40, 40), delegate
		{
			Stop();
		});
		_btnReconnect = MainForm.BigBtn("重新连接", Color.FromArgb(160, 110, 0), delegate
		{
			Reconnect();
		});
		_btnTest = MainForm.BigBtn("测试端口", Color.FromArgb(0, 90, 170), delegate
		{
			TestPorts(fromTray: false);
		});
		((Control)val4).Controls.Add((Control)(object)_btnStart);
		((Control)val4).Controls.Add((Control)(object)_btnStop);
		((Control)val4).Controls.Add((Control)(object)_btnReconnect);
		((Control)val4).Controls.Add((Control)(object)_btnTest);
		((Control)val4).Controls.Add((Control)(object)MainForm.BigBtn("保存配置", Color.Black, delegate
		{
			_main.SaveAll(showMsg: true);
		}));
		((Control)val4).Controls.Add((Control)(object)MainForm.BigBtn("清空日志", Color.Black, delegate
		{
			((TextBoxBase)_log).Clear();
		}));
		((Control)_split.Panel2).Controls.Add((Control)(object)val4);
		_settings = new Panel
		{
			Dock = (DockStyle)1,
			Height = 160
		};
		_lblHost = Lbl("主机：");
		_lblPort = Lbl("端口：");
		_lblUser = Lbl("用户名：");
		_lblPw = Lbl("密码：");
		_lblKey = Lbl("主机指纹：");
		_lblDelay = Lbl("重连间隔：");
		_lblSec = Lbl("秒");
		_host = new TextBox();
		_user = new TextBox();
		_pw = new TextBox
		{
			UseSystemPasswordChar = true
		};
		_hostKey = new TextBox
		{
			Font = new Font("Consolas", 9f)
		};
		_port = new NumericUpDown
		{
			Minimum = 1m,
			Maximum = 65535m,
			Value = 22m
		};
		_delay = new NumericUpDown
		{
			Minimum = 1m,
			Maximum = 3600m,
			Value = 5m
		};
		_showPw = new CheckBox
		{
			Text = "显示",
			AutoSize = true
		};
		_showPw.CheckedChanged += delegate
		{
			_pw.UseSystemPasswordChar = !_showPw.Checked;
		};
		_autoConnect = new CheckBox
		{
			Text = "程序启动时自动连接此配置",
			AutoSize = true
		};
		_verbose = new CheckBox
		{
			Text = "详细日志（plink -v）",
			AutoSize = true
		};
		_useJump = new CheckBox
		{
			Text = "通过跳板机连接上面的主机（相当于 ssh -J 跳板用户@跳板机）",
			AutoSize = true
		};
		_lblJHost = Lbl("跳板机：");
		_lblJPort = Lbl("端口：");
		_lblJUser = Lbl("跳板用户：");
		_lblJPw = Lbl("密码：");
		_lblJKey = Lbl("跳板指纹：");
		_jumpHint = new Label
		{
			Text = "开启后：先 SSH 到跳板机，再由跳板机连到上面的“SSH 主机:端口”登录，端口转发从那台主机发出。",
			AutoSize = true,
			ForeColor = Color.Gray
		};
		_jumpHost = new TextBox();
		_jumpUser = new TextBox();
		_jumpPw = new TextBox
		{
			UseSystemPasswordChar = true
		};
		_jumpHostKey = new TextBox
		{
			Font = new Font("Consolas", 9f)
		};
		_jumpPort = new NumericUpDown
		{
			Minimum = 1m,
			Maximum = 65535m,
			Value = 22m
		};
		_showJumpPw = new CheckBox
		{
			Text = "显示",
			AutoSize = true
		};
		_showJumpPw.CheckedChanged += delegate
		{
			_jumpPw.UseSystemPasswordChar = !_showJumpPw.Checked;
		};
		_useJump.CheckedChanged += delegate
		{
			UpdateJumpVisibility();
			LayoutSettings();
		};
		_btnKey = MainForm.SmallBtn("获取指纹", async delegate
		{
			await FetchKeyAsync(jump: false, silent: false);
		});
		_btnJumpKey = MainForm.SmallBtn("获取指纹", async delegate
		{
			await FetchKeyAsync(jump: true, silent: false);
		});
		_tips = new ToolTip();
		_tips.SetToolTip((Control)(object)_btnKey, "连接服务器读取主机密钥指纹并填入（不会登录）；勾选跳板机时经跳板机获取");
		_tips.SetToolTip((Control)(object)_btnJumpKey, "连接跳板机读取主机密钥指纹并填入（不会登录）");
		_tips.SetToolTip((Control)(object)_useJump, "不勾选：plink -L 本地端口:目标主机:目标端口 用户@主机（主机本身就是跳板，目标主机可以是另一网段）\n勾选：目标主机在另一网段、只有跳板机能 SSH 到它时使用");
		_tips.SetToolTip((Control)(object)_jumpHostKey, "跳板机的主机指纹，如 SHA256:xxxx；留空需先用 PuTTY 手动连接一次跳板机并接受密钥");
		((Control)_settings).Controls.AddRange((Control[])(object)new Control[16]
		{
			(Control)_lblHost,
			(Control)_host,
			(Control)_lblPort,
			(Control)_port,
			(Control)_lblUser,
			(Control)_user,
			(Control)_lblPw,
			(Control)_pw,
			(Control)_showPw,
			(Control)_lblKey,
			(Control)_hostKey,
			(Control)_lblDelay,
			(Control)_delay,
			(Control)_lblSec,
			(Control)_autoConnect,
			(Control)_verbose
		});
		((Control)_settings).Controls.AddRange((Control[])(object)new Control[13]
		{
			(Control)_useJump,
			(Control)_jumpHint,
			(Control)_lblJHost,
			(Control)_jumpHost,
			(Control)_lblJPort,
			(Control)_jumpPort,
			(Control)_lblJUser,
			(Control)_jumpUser,
			(Control)_lblJPw,
			(Control)_jumpPw,
			(Control)_showJumpPw,
			(Control)_lblJKey,
			(Control)_jumpHostKey
		});
		((Control)_settings).Controls.Add((Control)(object)_btnKey);
		((Control)_settings).Controls.Add((Control)(object)_btnJumpKey);
		UpdateJumpVisibility();
		((Control)_settings).Layout += (LayoutEventHandler)delegate
		{
			LayoutSettings();
		};
		((Control)this).Controls.Add((Control)(object)_settings);
		((Control)this).ResumeLayout(false);
		_tunnel.Log += delegate(string m, LogLevel lv)
		{
			Log(m, lv);
		};
		_tunnel.StateChanged += delegate
		{
			try
			{
				if (((Control)_main).IsHandleCreated)
				{
					((Control)_main).BeginInvoke((Delegate)new Action(UpdateStatus));
				}
			}
			catch
			{
			}
		};
		((Control)_split).SizeChanged += delegate
		{
			InitSplit();
		};
		ToUi(p);
	}

	private static Label Lbl(string t)
	{
		return new Label
		{
			Text = t,
			AutoSize = true
		};
	}

	private int S(int v)
	{
		return (int)Math.Round((double)(v * ((Control)this).DeviceDpi) / 96.0);
	}

	public void Rename(string name)
	{
		_saved.Name = name;
		_tunnel.Name = name;
	}

	public List<Forward> TableForwards()
	{
		_grid.EndEdit();
		List<Forward> list = new List<Forward>();
		foreach (DataGridViewRow item in (IEnumerable)_grid.Rows)
		{
			DataGridViewRow val = item;
			if (!val.IsNewRow && int.TryParse(Cell(val, 2), out var result))
			{
				list.Add(new Forward
				{
					Note = Cell(val, 0),
					LocalAddress = Cell(val, 1),
					LocalPort = result
				});
			}
		}
		return list;
	}

	public string Commit()
	{
		Profile c;
		string text = FromUi(out c);
		if (text == null)
		{
			_saved = c;
		}
		return text;
	}

	public void Log(string msg, LogLevel level)
	{
		if (((Control)this).IsDisposed)
		{
			return;
		}
		if (((Control)_main).InvokeRequired)
		{
			try
			{
				((Control)_main).BeginInvoke((Delegate)(Action)delegate
				{
					Log(msg, level);
				});
				return;
			}
			catch
			{
				return;
			}
		}
		Color selectionColor = level switch
		{
			LogLevel.Ok => Color.FromArgb(0, 128, 0), 
			LogLevel.Warn => Color.FromArgb(200, 110, 0), 
			LogLevel.Error => Color.Red, 
			LogLevel.Detail => Color.Gray, 
			LogLevel.Plink => Color.FromArgb(60, 60, 140), 
			_ => Color.Black, 
		};
		if (((TextBoxBase)_log).TextLength > 400000)
		{
			((TextBoxBase)_log).Select(0, 200000);
			((TextBoxBase)_log).SelectedText = "";
		}
		string[] array = (msg ?? "").Replace("\r\n", "\n").Split(new char[1] { '\n' });
		foreach (string text in array)
		{
			((TextBoxBase)_log).SelectionStart = ((TextBoxBase)_log).TextLength;
			((TextBoxBase)_log).SelectionLength = 0;
			_log.SelectionColor = Color.DarkGray;
			((TextBoxBase)_log).AppendText("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] ");
			((TextBoxBase)_log).SelectionStart = ((TextBoxBase)_log).TextLength;
			_log.SelectionColor = selectionColor;
			((TextBoxBase)_log).AppendText(text + "\n");
		}
		((TextBoxBase)_log).SelectionStart = ((TextBoxBase)_log).TextLength;
		((TextBoxBase)_log).ScrollToCaret();
	}

	private void LayoutSettings()
	{
		if (_inLayout || _settings == null)
		{
			return;
		}
		_inLayout = true;
		try
		{
			int num = S(8);
			int num2 = S(6);
			int num3 = S(6);
			int num4 = ((Control)_settings).ClientSize.Width - num;
			int num5 = ((IEnumerable<Label>)(object)new Label[7] { _lblHost, _lblUser, _lblKey, _lblDelay, _lblJHost, _lblJUser, _lblJKey }).Max((Label l) => ((Control)l).PreferredSize.Width);
			int num6 = new int[4]
			{
				((Control)_lblPort).PreferredSize.Width,
				((Control)_lblPw).PreferredSize.Width,
				((Control)_lblJPort).PreferredSize.Width,
				((Control)_lblJPw).PreferredSize.Width
			}.Max();
			int num7 = num + num5 + num2;
			int num8 = S(8);
			Action<Control, int, int, int, int> action = delegate(Control c, int x, int w, int rowY, int rowH)
			{
				c.SetBounds(x, rowY + Math.Max(0, (rowH - c.Height) / 2), Math.Max(S(20), w), c.Height);
			};
			Action<Control> action2 = delegate(Control c)
			{
				c.Size = c.PreferredSize;
			};
			Control[] array = (Control[])(object)new Control[11]
			{
				(Control)_host,
				(Control)_user,
				(Control)_pw,
				(Control)_hostKey,
				(Control)_port,
				(Control)_delay,
				(Control)_jumpHost,
				(Control)_jumpUser,
				(Control)_jumpPw,
				(Control)_jumpHostKey,
				(Control)_jumpPort
			};
			foreach (Control obj in array)
			{
				obj.Height = obj.PreferredSize.Height;
			}
			action2((Control)(object)_showPw);
			int num10 = num7 + (int)((double)(num4 - num7) * 0.55);
			int num11 = num10 + num6 + num2;
			int arg = num10 - num2 - num7;
			action2((Control)(object)_lblHost);
			action2((Control)(object)_lblPort);
			int num12 = new int[4]
			{
				((Control)_lblHost).Height,
				((Control)_host).Height,
				((Control)_lblPort).Height,
				((Control)_port).Height
			}.Max();
			action((Control)(object)_lblHost, num, ((Control)_lblHost).Width, num8, num12);
			action((Control)(object)_host, num7, arg, num8, num12);
			action((Control)(object)_lblPort, num10, ((Control)_lblPort).Width, num8, num12);
			action((Control)(object)_port, num11, S(90), num8, num12);
			num8 += num12 + num3;
			action2((Control)(object)_lblUser);
			action2((Control)(object)_lblPw);
			num12 = new int[5]
			{
				((Control)_lblUser).Height,
				((Control)_user).Height,
				((Control)_lblPw).Height,
				((Control)_pw).Height,
				((Control)_showPw).Height
			}.Max();
			action((Control)(object)_lblUser, num, ((Control)_lblUser).Width, num8, num12);
			action((Control)(object)_user, num7, arg, num8, num12);
			action((Control)(object)_lblPw, num10, ((Control)_lblPw).Width, num8, num12);
			action((Control)(object)_showPw, num4 - ((Control)_showPw).Width, ((Control)_showPw).Width, num8, num12);
			action((Control)(object)_pw, num11, ((Control)_showPw).Left - num2 - num11, num8, num12);
			num8 += num12 + num3;
			action2((Control)(object)_lblKey);
			num12 = Math.Max(((Control)_lblKey).Height, ((Control)_hostKey).Height);
			action((Control)(object)_lblKey, num, ((Control)_lblKey).Width, num8, num12);
			action2((Control)(object)_btnKey);
			num12 = Math.Max(num12, ((Control)_btnKey).Height);
			action((Control)(object)_btnKey, num4 - ((Control)_btnKey).Width, ((Control)_btnKey).Width, num8, num12);
			action((Control)(object)_hostKey, num7, ((Control)_btnKey).Left - num2 - num7, num8, num12);
			num8 += num12 + num3;
			action2((Control)(object)_lblDelay);
			action2((Control)(object)_lblSec);
			num12 = new int[5]
			{
				((Control)_lblDelay).Height,
				((Control)_delay).Height,
				((Control)_lblSec).Height,
				((Control)_autoConnect).PreferredSize.Height,
				((Control)_verbose).PreferredSize.Height
			}.Max();
			action((Control)(object)_lblDelay, num, ((Control)_lblDelay).Width, num8, num12);
			action((Control)(object)_delay, num7, S(70), num8, num12);
			action((Control)(object)_lblSec, ((Control)_delay).Right + S(4), ((Control)_lblSec).Width, num8, num12);
			List<Control[]> list = new List<Control[]>();
			list.Add((Control[])(object)new Control[1] { (Control)_autoConnect });
			list.Add((Control[])(object)new Control[1] { (Control)_verbose });
			num8 = MainForm.FlowPlace(list, ((Control)_lblSec).Right + S(20), num7, num4, num8, num12, num3, S(16), S(4));
			num8 += num12 + num3;
			// ---- 跳板机 ----
			action2((Control)(object)_useJump);
			action((Control)(object)_useJump, num7, ((Control)_useJump).Width, num8, ((Control)_useJump).Height);
			num8 += ((Control)_useJump).Height + num3;
			if (_useJump.Checked)
			{
				action2((Control)(object)_lblJHost);
				action2((Control)(object)_lblJPort);
				num12 = new int[4]
				{
					((Control)_lblJHost).Height,
					((Control)_jumpHost).Height,
					((Control)_lblJPort).Height,
					((Control)_jumpPort).Height
				}.Max();
				action((Control)(object)_lblJHost, num, ((Control)_lblJHost).Width, num8, num12);
				action((Control)(object)_jumpHost, num7, arg, num8, num12);
				action((Control)(object)_lblJPort, num10, ((Control)_lblJPort).Width, num8, num12);
				action((Control)(object)_jumpPort, num11, S(90), num8, num12);
				num8 += num12 + num3;
				action2((Control)(object)_lblJUser);
				action2((Control)(object)_lblJPw);
				action2((Control)(object)_showJumpPw);
				num12 = new int[5]
				{
					((Control)_lblJUser).Height,
					((Control)_jumpUser).Height,
					((Control)_lblJPw).Height,
					((Control)_jumpPw).Height,
					((Control)_showJumpPw).Height
				}.Max();
				action((Control)(object)_lblJUser, num, ((Control)_lblJUser).Width, num8, num12);
				action((Control)(object)_jumpUser, num7, arg, num8, num12);
				action((Control)(object)_lblJPw, num10, ((Control)_lblJPw).Width, num8, num12);
				action((Control)(object)_showJumpPw, num4 - ((Control)_showJumpPw).Width, ((Control)_showJumpPw).Width, num8, num12);
				action((Control)(object)_jumpPw, num11, ((Control)_showJumpPw).Left - num2 - num11, num8, num12);
				num8 += num12 + num3;
				action2((Control)(object)_lblJKey);
				num12 = Math.Max(((Control)_lblJKey).Height, ((Control)_jumpHostKey).Height);
				action((Control)(object)_lblJKey, num, ((Control)_lblJKey).Width, num8, num12);
				action2((Control)(object)_btnJumpKey);
				num12 = Math.Max(num12, ((Control)_btnJumpKey).Height);
				action((Control)(object)_btnJumpKey, num4 - ((Control)_btnJumpKey).Width, ((Control)_btnJumpKey).Width, num8, num12);
				action((Control)(object)_jumpHostKey, num7, ((Control)_btnJumpKey).Left - num2 - num7, num8, num12);
				num8 += num12 + num3;
				((Control)_jumpHint).MaximumSize = new Size(Math.Max(S(100), num4 - num7), 0);
				action2((Control)(object)_jumpHint);
				action((Control)(object)_jumpHint, num7, ((Control)_jumpHint).Width, num8, ((Control)_jumpHint).Height);
				num8 += ((Control)_jumpHint).Height + num3;
			}
			num8 += S(6) - num3;
			if (((Control)_settings).Height != num8)
			{
				((Control)_settings).Height = num8;
			}
		}
		finally
		{
			_inLayout = false;
		}
	}

	private void InitSplit()
	{
		if (_splitInit)
		{
			return;
		}
		try
		{
			int height = ((Control)_split).Height;
			int num = S(120);
			int num2 = S(150);
			if (height > num + num2 + _split.SplitterWidth)
			{
				_split.Panel1MinSize = num;
				_split.Panel2MinSize = num2;
				_split.SplitterDistance = Math.Max(num, Math.Min(height - num2 - _split.SplitterWidth, (int)((double)height * 0.42)));
				_splitInit = true;
			}
		}
		catch
		{
		}
	}

	private void ToUi(Profile c)
	{
		((Control)_host).Text = c.Host;
		_port.Value = Math.Max(1, Math.Min(65535, c.Port));
		((Control)_user).Text = c.User;
		((Control)_pw).Text = c.GetPassword();
		((Control)_hostKey).Text = c.HostKey;
		_delay.Value = Math.Max(1, Math.Min(3600, c.ReconnectDelaySeconds));
		_autoConnect.Checked = c.AutoConnect;
		_verbose.Checked = c.Verbose;
		_useJump.Checked = c.UseJump;
		((Control)_jumpHost).Text = c.JumpHost;
		_jumpPort.Value = Math.Max(1, Math.Min(65535, c.JumpPort));
		((Control)_jumpUser).Text = c.JumpUser;
		((Control)_jumpPw).Text = c.GetJumpPassword();
		((Control)_jumpHostKey).Text = c.JumpHostKey;
		UpdateJumpVisibility();
		_grid.Rows.Clear();
		foreach (Forward forward in c.Forwards)
		{
			_grid.Rows.Add(new object[6]
			{
				forward.Note,
				forward.LocalAddress,
				forward.LocalPort.ToString(),
				forward.TargetHost,
				forward.TargetPort.ToString(),
				""
			});
		}
	}

	private static string Cell(DataGridViewRow r, int i)
	{
		return (Convert.ToString(r.Cells[i].Value) ?? "").Trim();
	}

	private string FromUi(out Profile c)
	{
		_grid.EndEdit();
		c = new Profile
		{
			Name = _saved.Name,
			Host = ((Control)_host).Text.Trim(),
			Port = (int)_port.Value,
			User = ((Control)_user).Text.Trim(),
			HostKey = ((Control)_hostKey).Text.Trim(),
			ReconnectDelaySeconds = (int)_delay.Value,
			AutoConnect = _autoConnect.Checked,
			Verbose = _verbose.Checked,
			Forwards = new List<Forward>(),
			UseJump = _useJump.Checked,
			JumpHost = ((Control)_jumpHost).Text.Trim(),
			JumpPort = (int)_jumpPort.Value,
			JumpUser = ((Control)_jumpUser).Text.Trim(),
			JumpHostKey = ((Control)_jumpHostKey).Text.Trim()
		};
		if (((Control)_jumpPw).Text == _saved.GetJumpPassword() && !string.IsNullOrEmpty(_saved.JumpPasswordProtected))
		{
			c.JumpPasswordProtected = _saved.JumpPasswordProtected;
		}
		else
		{
			c.SetJumpPassword(((Control)_jumpPw).Text);
		}
		if (((Control)_pw).Text == _saved.GetPassword() && !string.IsNullOrEmpty(_saved.PasswordProtected))
		{
			c.PasswordProtected = _saved.PasswordProtected;
		}
		else
		{
			c.SetPassword(((Control)_pw).Text);
		}
		int num = 0;
		foreach (DataGridViewRow item in (IEnumerable)_grid.Rows)
		{
			DataGridViewRow val = item;
			num++;
			if (val.IsNewRow)
			{
				continue;
			}
			string text = Cell(val, 0);
			string text2 = Cell(val, 1);
			string text3 = Cell(val, 2);
			string text4 = Cell(val, 3);
			string text5 = Cell(val, 4);
			if (!(text == "") || !(text3 == "") || !(text4 == "") || !(text5 == ""))
			{
				if (!int.TryParse(text3, out var lpi) || lpi < 1 || lpi > 65535)
				{
					return "第 " + num + " 行：本地端口无效";
				}
				if (!int.TryParse(text5, out var result) || result < 1 || result > 65535)
				{
					return "第 " + num + " 行：目标端口无效";
				}
				if (text4 == "")
				{
					return "第 " + num + " 行：目标主机不能为空";
				}
				if (text2 == "")
				{
					text2 = "127.0.0.1";
				}
				if (!IPAddress.TryParse(text2, out IPAddress _))
				{
					return "第 " + num + " 行：本地地址必须是 IP（如 127.0.0.1）";
				}
				if (c.Forwards.Any((Forward f) => f.LocalPort == lpi))
				{
					return "第 " + num + " 行：本地端口 " + lpi + " 重复";
				}
				c.Forwards.Add(new Forward
				{
					Note = text,
					LocalAddress = text2,
					LocalPort = lpi,
					TargetHost = text4,
					TargetPort = result
				});
			}
		}
		return null;
	}

	// 主机指纹为空时：首次连接自动获取并信任（TOFU），填入后继续启动
	private async void AutoFetchThenStart()
	{
		if (_fetchingKey)
		{
			return;
		}
		((Control)_btnStart).Enabled = false;
		try
		{
			Log("主机指纹为空：先自动获取主机指纹（首次连接信任 TOFU），获取后自动保存到配置", LogLevel.Info);
			bool ok = true;
			if (_useJump.Checked && ((Control)_jumpHostKey).Text.Trim() == "")
			{
				ok = await FetchKeyAsync(jump: true, silent: true);
			}
			if (ok && ((Control)_hostKey).Text.Trim() == "")
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
			((Control)_btnStart).Enabled = true;
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

	// jump=true：获取跳板机的指纹；jump=false：获取“主机”的指纹（勾选跳板机时经跳板获取）
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
		string host = (jump ? ((Control)_jumpHost).Text : ((Control)_host).Text).Trim();
		int port = (int)(jump ? _jumpPort.Value : _port.Value);
		string label = jump ? "跳板机" : "主机";
		if (host == "")
		{
			if (!silent)
			{
				ShowError(label + "地址为空，无法获取指纹。");
			}
			return false;
		}
		bool viaJump = !jump && _useJump.Checked;
		Profile jcfg = null;
		string jpw = null;
		if (viaJump)
		{
			if (((Control)_jumpHost).Text.Trim() == "" || ((Control)_jumpUser).Text.Trim() == "")
			{
				if (!silent)
				{
					ShowError("已勾选“通过跳板机连接”：要经跳板机获取指纹，请先填写跳板机和跳板用户。");
				}
				return false;
			}
			if (((Control)_jumpHostKey).Text.Trim() == "")
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
				JumpHost = ((Control)_jumpHost).Text.Trim(),
				JumpPort = (int)_jumpPort.Value,
				JumpUser = ((Control)_jumpUser).Text.Trim(),
				JumpHostKey = ((Control)_jumpHostKey).Text.Trim()
			};
			jpw = ((Control)_jumpPw).Text;
		}
		_fetchingKey = true;
		Button btn = jump ? _btnJumpKey : _btnKey;
		((Control)btn).Enabled = false;
		string oldText = ((Control)btn).Text;
		((Control)btn).Text = "获取中…";
		try
		{
			Log("获取" + label + "指纹：" + host + ":" + port + (viaJump ? ("（经跳板机 " + jcfg.JumpUser + "@" + jcfg.JumpHost + ":" + jcfg.JumpPort + "）") : "") + "…", LogLevel.Info);
			HostKeyResult r = await Task.Run(() => HostKeyFetcher.Fetch(plink, host, port, jcfg, jpw));
			if (((Control)this).IsDisposed)
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
				foreach (string line in r.Output.Replace("\r", "").Split('\n'))
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
			string old = ((Control)box).Text.Trim();
			((Control)box).Text = r.Fingerprint;
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
			((Control)btn).Text = oldText;
			((Control)btn).Enabled = true;
		}
	}

	private void UpdateJumpVisibility()
	{
		bool on = _useJump.Checked;
		Control[] array = (Control[])(object)new Control[13]
		{
			(Control)_lblJHost,
			(Control)_jumpHost,
			(Control)_lblJPort,
			(Control)_jumpPort,
			(Control)_lblJUser,
			(Control)_jumpUser,
			(Control)_lblJPw,
			(Control)_jumpPw,
			(Control)_showJumpPw,
			(Control)_lblJKey,
			(Control)_jumpHostKey,
			(Control)_jumpHint,
			(Control)_btnJumpKey
		};
		foreach (Control obj in array)
		{
			obj.Visible = on;
		}
		((Control)_lblHost).Text = (on ? "SSH 主机：" : "主机：");
	}

	private void ImportSshCommand()
	{
		SshCommand parsed = null;
		InputForm inputForm = new InputForm("从 ssh 命令导入", "粘贴 ssh 命令，例如：\nssh -N -L 2222:192.0.2.20:22 user@10.0.0.1\n（支持多个 -L、-p 端口、-J 跳板用户@跳板机[:端口]）", "", delegate(string t)
		{
			string error;
			parsed = SshCommand.Parse(t, out error);
			return (parsed == null) ? (error ?? "无法解析") : null;
		}, 560);
		try
		{
			if ((int)((Form)inputForm).ShowDialog((IWin32Window)(object)_main) != 1 || parsed == null)
			{
				return;
			}
		}
		finally
		{
			((Component)(object)inputForm).Dispose();
		}
		bool replace = true;
		if (((IEnumerable)_grid.Rows).Cast<DataGridViewRow>().Any((DataGridViewRow r) => !r.IsNewRow))
		{
			DialogResult dialogResult = MessageBox.Show((IWin32Window)(object)_main, "当前已有端口转发。\n\n是：用命令里的转发替换现有的\n否：追加到现有转发后面（本地端口相同的会被更新）\n取消：不导入", "从 ssh 命令导入", (MessageBoxButtons)3, (MessageBoxIcon)32);
			if ((int)dialogResult == 2)
			{
				return;
			}
			replace = (int)dialogResult == 6;
		}
		((Control)_host).Text = parsed.Host;
		_port.Value = parsed.Port;
		((Control)_user).Text = parsed.User;
		if (parsed.HostKey != "")
		{
			((Control)_hostKey).Text = parsed.HostKey;
		}
		if (parsed.JumpHost != "")
		{
			_useJump.Checked = true;
			((Control)_jumpHost).Text = parsed.JumpHost;
			_jumpPort.Value = parsed.JumpPort;
			((Control)_jumpUser).Text = parsed.JumpUser;
		}
		else
		{
			_useJump.Checked = false;
		}
		if (replace)
		{
			_grid.Rows.Clear();
		}
		foreach (Forward f in parsed.Forwards)
		{
			DataGridViewRow existing = ((IEnumerable)_grid.Rows).Cast<DataGridViewRow>().FirstOrDefault((DataGridViewRow r) => !r.IsNewRow && Cell(r, 2) == f.LocalPort.ToString());
			object[] values = new object[6]
			{
				f.Note,
				f.LocalAddress,
				f.LocalPort.ToString(),
				f.TargetHost,
				f.TargetPort.ToString(),
				""
			};
			if (existing != null)
			{
				existing.SetValues(values);
			}
			else
			{
				_grid.Rows.Add(values);
			}
		}
		UpdateJumpVisibility();
		LayoutSettings();
		Log("已从 ssh 命令导入：" + parsed.User + "@" + parsed.Host + ":" + parsed.Port + ((parsed.JumpHost != "") ? ("，跳板机 " + parsed.JumpUser + "@" + parsed.JumpHost + ":" + parsed.JumpPort) : "") + "，" + parsed.Forwards.Count + " 条转发。请检查密码/指纹后点“保存配置”或“启动隧道”。", LogLevel.Info);
		foreach (string note in parsed.Notes)
		{
			Log(note, LogLevel.Warn);
		}
	}

	private void DeleteRows()
	{
		List<DataGridViewRow> list = ((IEnumerable)_grid.SelectedRows).Cast<DataGridViewRow>().ToList();
		if (list.Count == 0 && _grid.CurrentRow != null)
		{
			list.Add(_grid.CurrentRow);
		}
		foreach (DataGridViewRow item in list)
		{
			if (!item.IsNewRow)
			{
				_grid.Rows.Remove(item);
			}
		}
	}

	private void ShowError(string msg)
	{
		_main.ShowProfile(this);
		MessageBox.Show((IWin32Window)(object)_main, "配置「" + ProfileName + "」：" + msg, "SSH 隧道工具箱", (MessageBoxButtons)0, (MessageBoxIcon)48);
	}

	public void Start()
	{
		if (_tunnel.Running)
		{
			Log("隧道已在运行", LogLevel.Detail);
			return;
		}
		Profile c;
		string text = FromUi(out c);
		if (text != null)
		{
			ShowError(text);
			return;
		}
		string text2 = _main.EnsurePlink(this);
		if (text2 == null)
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
		string text3 = _main.FindPortConflict(this, c.Forwards);
		if (text3 != null)
		{
			Log("无法启动：" + text3, LogLevel.Error);
			_main.Balloon(ProfileName, "无法启动", text3, (ToolTipIcon)3, force: true);
			return;
		}
		if (!_autoFetchDone && (c.HostKey == "" || (c.JumpActive && c.JumpHostKey == "")))
		{
			AutoFetchThenStart();
			return;
		}
		_saved = c;
		_main.SaveAll(showMsg: false);
		foreach (DataGridViewRow item in (IEnumerable)_grid.Rows)
		{
			item.Cells[5].Value = "";
		}
		_runningForwards = c.Forwards.Select((Forward f) => f.Clone()).ToList();
		Log("———— 启动隧道「" + c.Name + "」 " + c.Describe() + " ————", LogLevel.Info);
		_tunnel.Start(c, text2);
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
		Task.Run(delegate
		{
			_tunnel.Stop();
		}).ContinueWith(delegate
		{
			try
			{
				((Control)_main).BeginInvoke((Delegate)new Action(UpdateStatus));
			}
			catch
			{
			}
		});
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

	public void Reconnect()
	{
		if (!_tunnel.Running)
		{
			Start();
		}
		else
		{
			if (_restarting)
			{
				return;
			}
			_restarting = true;
			Log("———— 重新连接 ————", LogLevel.Info);
			_userStopping = true;
			UpdateStatus();
			Task.Run(delegate
			{
				_tunnel.Stop();
			}).ContinueWith(delegate
			{
				try
				{
					((Control)_main).BeginInvoke((Delegate)(Action)delegate
					{
						_restarting = false;
						UpdateStatus();
						Start();
					});
				}
				catch
				{
				}
			});
		}
	}

	public async void TestPorts(bool fromTray)
	{
		if (_testing)
		{
			return;
		}
		Profile c;
		string text = FromUi(out c);
		if (text != null)
		{
			ShowError(text);
			return;
		}
		_testing = true;
		((Control)_btnTest).Enabled = false;
		int okCount = 0;
		int total = 0;
		List<string> fails = new List<string>();
		Log("测试本地端口…", LogLevel.Info);
		List<DataGridViewRow> list = (from DataGridViewRow val in (IEnumerable)_grid.Rows
			where !val.IsNewRow
			select val).ToList();
		foreach (DataGridViewRow r in list)
		{
			if (int.TryParse(Cell(r, 2), out var lp))
			{
				string la = Cell(r, 1);
				string note = Cell(r, 0);
				r.Cells[5].Value = "测试中…";
				r.Cells[5].Style.ForeColor = Color.Gray;
				string e2 = null;
				bool flag = await Task.Run(delegate
				{
					string error;
					bool result = PortUtil.TryConnect(la, lp, 2000, out error);
					e2 = error;
					return result;
				});
				total++;
				if (flag)
				{
					okCount++;
				}
				else
				{
					fails.Add(((note != "") ? note : lp.ToString()) + "：" + e2);
				}
				r.Cells[5].Value = (flag ? "✔ OK" : ("✘ " + e2));
				r.Cells[5].Style.ForeColor = (flag ? Color.FromArgb(0, 128, 0) : Color.Red);
				Log(string.Format("端口 {0}:{1}（{2}）：{3}", string.IsNullOrEmpty(la) ? "127.0.0.1" : la, lp, note, flag ? "OK，可连接" : ("失败 - " + e2)), flag ? LogLevel.Ok : LogLevel.Error);
			}
		}
		Log("提示：本地端口 OK 只说明隧道在监听；目标数据库是否可用要以客户端实际连接为准。", LogLevel.Detail);
		((Control)_btnTest).Enabled = true;
		_testing = false;
		if (fromTray || !((Control)_main).Visible)
		{
			string text2 = ((total == 0) ? "没有可测试的端口" : $"{okCount}/{total} 个本地端口可连接");
			if (fails.Count > 0)
			{
				text2 = text2 + "\n失败：" + string.Join("；", fails);
			}
			_main.Balloon(ProfileName, "测试端口", text2, (ToolTipIcon)((total > 0 && fails.Count == 0) ? 1 : 2), force: true);
		}
	}

	public void UpdateStatus()
	{
		if (!((Control)this).IsDisposed)
		{
			TunnelState state = State;
			string text;
			switch (state)
			{
			case TunnelState.Connected:
			{
				TimeSpan t = (_tunnel.ConnectedSince.HasValue ? (DateTime.Now - _tunnel.ConnectedSince.Value) : TimeSpan.Zero);
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
			((Control)_dot).ForeColor = MainForm.ColorOf(state);
			if (((Control)_statusText).Text != text)
			{
				((Control)_statusText).Text = text;
			}
			bool running = _tunnel.Running;
			((Control)_btnStart).Enabled = !running && !_restarting;
			((Control)_btnStop).Enabled = running && !_restarting;
			((Control)_btnReconnect).Enabled = running && !_restarting;
			NotifyTransition(state);
			this.StatusChanged?.Invoke();
		}
	}

	private void NotifyTransition(TunnelState s)
	{
		TunnelState lastState = _lastState;
		if (s == lastState)
		{
			return;
		}
		_lastState = s;
		switch (s)
		{
		case TunnelState.Connected:
		{
			string text = ((_runningForwards != null) ? string.Join("、", _runningForwards.Select((Forward f) => f.ClientAddress)) : "");
			_main.Balloon(ProfileName, _everConnected ? "隧道已重新连接" : "隧道已连接", text, (ToolTipIcon)1);
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
					_main.Balloon(ProfileName, "隧道已停止", "隧道意外停止，详见日志。", (ToolTipIcon)2);
				}
				else if (!((Control)_main).Visible)
				{
					_main.Balloon(ProfileName, "隧道已停止", "", (ToolTipIcon)1);
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
				if (lastState == TunnelState.Connected || _everConnected)
				{
					_main.Balloon(ProfileName, "隧道连接断开", "正在自动重连…", (ToolTipIcon)2);
				}
				else
				{
					_main.Balloon(ProfileName, "隧道连接失败", "正在自动重试…（详见日志）", (ToolTipIcon)2);
				}
			}
			else if ((DateTime.Now - _lastFailBalloon).TotalMinutes >= 10.0)
			{
				_lastFailBalloon = DateTime.Now;
				_main.Balloon(ProfileName, "隧道仍未连上", "已尝试 " + _tunnel.AttemptCount + " 次，仍在自动重连…", (ToolTipIcon)2);
			}
			break;
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			try
			{
				_tunnel.Dispose();
			}
			catch
			{
			}
		}
		base.Dispose(disposing);
	}
}
