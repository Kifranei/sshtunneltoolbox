using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SshTunnelToolbox;

public class SettingsForm : Form
{
	public static readonly string[] CloseKeys = new string[3] { "ask", "tray", "exit" };

	public static readonly string[] CloseTexts = new string[3] { "每次询问", "最小化到托盘", "直接退出" };

	private readonly TextBox _plink;

	private readonly ComboBox _close;

	private readonly CheckBox _minToTray;

	private readonly CheckBox _startHidden;

	private readonly CheckBox _autoStart;

	private readonly CheckBox _balloons;

	public string PlinkPath => ((Control)_plink).Text.Trim();

	public string CloseAction => CloseKeys[Math.Max(0, ((ListControl)_close).SelectedIndex)];

	public bool MinimizeToTray => _minToTray.Checked;

	public bool StartHidden => _startHidden.Checked;

	public bool AutoStartOn => _autoStart.Checked;

	public bool Balloons => _balloons.Checked;

	public SettingsForm(AppConfig cfg, bool autoStart)
	{
		((ContainerControl)this).AutoScaleDimensions = new SizeF(96f, 96f);
		((ContainerControl)this).AutoScaleMode = (AutoScaleMode)2;
		((Control)this).Font = new Font("Microsoft YaHei UI", 9f);
		((Control)this).Text = "设置（所有配置共用）";
		((Form)this).Icon = MainForm.AppIcon;
		((Form)this).FormBorderStyle = (FormBorderStyle)3;
		((Form)this).MaximizeBox = false;
		((Form)this).MinimizeBox = false;
		((Form)this).ShowInTaskbar = false;
		((Form)this).StartPosition = (FormStartPosition)4;
		((Control)this).AutoSize = true;
		((Form)this).AutoSizeMode = (AutoSizeMode)0;
		((Control)this).Padding = new Padding(14, 12, 14, 12);
		FlowLayoutPanel val = new FlowLayoutPanel
		{
			FlowDirection = (FlowDirection)1,
			WrapContents = false,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			Location = new Point(14, 12)
		};
		((Control)val).Controls.Add((Control)(object)Header("plink.exe（PuTTY）"));
		FlowLayoutPanel val2 = Row();
		_plink = new TextBox
		{
			Text = (cfg.PlinkPath ?? ""),
			Width = 380,
			Margin = new Padding(0, 4, 6, 3)
		};
		((Control)val2).Controls.Add((Control)(object)_plink);
		((Control)val2).Controls.Add((Control)(object)Btn("浏览…", delegate
		{
			Browse();
		}));
		((Control)val2).Controls.Add((Control)(object)Btn("自动查找", delegate
		{
			string text = Plink.Detect();
			if (text != null)
			{
				((Control)_plink).Text = text;
			}
			else
			{
				MessageBox.Show((IWin32Window)(object)this, "没有找到 plink.exe（已查找 C:\\Program Files\\PuTTY、C:\\Program Files (x86)\\PuTTY 和 PATH）。", ((Control)this).Text);
			}
		}));
		((Control)val).Controls.Add((Control)(object)val2);
		((Control)val).Controls.Add((Control)(object)Header("托盘"));
		FlowLayoutPanel val3 = Row();
		((Control)val3).Controls.Add((Control)new Label
		{
			Text = "关闭窗口时：",
			AutoSize = true,
			Margin = new Padding(0, 7, 4, 3)
		});
		_close = new ComboBox
		{
			DropDownStyle = (ComboBoxStyle)2,
			Width = 140,
			Margin = new Padding(0, 3, 3, 3)
		};
		ComboBox.ObjectCollection items = _close.Items;
		object[] closeTexts = CloseTexts;
		items.AddRange(closeTexts);
		((ListControl)_close).SelectedIndex = Math.Max(0, Array.IndexOf(CloseKeys, cfg.CloseAction));
		((Control)val3).Controls.Add((Control)(object)_close);
		((Control)val).Controls.Add((Control)(object)val3);
		_minToTray = Chk("最小化到托盘（不勾选：普通最小化到任务栏）", cfg.MinimizeToTray);
		_startHidden = Chk("启动时隐藏到托盘", cfg.StartHidden);
		_autoStart = Chk("开机自启（当前用户，无需管理员；启动后直接在托盘运行）", autoStart);
		_balloons = Chk("显示气泡通知（连接 / 断开 / 重连；连续失败不会反复弹出）", cfg.Balloons);
		((Control)val).Controls.Add((Control)(object)_minToTray);
		((Control)val).Controls.Add((Control)(object)_startHidden);
		((Control)val).Controls.Add((Control)(object)_autoStart);
		((Control)val).Controls.Add((Control)(object)_balloons);
		FlowLayoutPanel val4 = Row();
		((Control)val4).Margin = new Padding(0, 14, 0, 0);
		Button val5 = Btn("确定", delegate
		{
			((Form)this).DialogResult = (DialogResult)1;
			((Form)this).Close();
		});
		Button val6 = Btn("取消", delegate
		{
			((Form)this).DialogResult = (DialogResult)2;
			((Form)this).Close();
		});
		((Control)val4).Controls.Add((Control)(object)val5);
		((Control)val4).Controls.Add((Control)(object)val6);
		((Control)val).Controls.Add((Control)(object)val4);
		((Control)this).Controls.Add((Control)(object)val);
		((Form)this).AcceptButton = (IButtonControl)(object)val5;
		((Form)this).CancelButton = (IButtonControl)(object)val6;
	}

	private static Label Header(string t)
	{
		return new Label
		{
			Text = t,
			AutoSize = true,
			Font = new Font("Microsoft YaHei UI", 9.5f, (FontStyle)1),
			Margin = new Padding(0, 8, 0, 2)
		};
	}

	private static FlowLayoutPanel Row()
	{
		return new FlowLayoutPanel
		{
			FlowDirection = (FlowDirection)0,
			WrapContents = false,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			Margin = new Padding(0)
		};
	}

	private static CheckBox Chk(string t, bool v)
	{
		return new CheckBox
		{
			Text = t,
			AutoSize = true,
			Checked = v,
			Margin = new Padding(0, 3, 0, 3)
		};
	}

	private static Button Btn(string t, EventHandler h)
	{
		Button val = new Button
		{
			Text = t,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			Padding = new Padding(8, 1, 8, 1),
			Margin = new Padding(0, 2, 6, 2),
			UseVisualStyleBackColor = true
		};
		((Control)val).Click += h;
		return val;
	}

	private void Browse()
	{
		OpenFileDialog val = new OpenFileDialog
		{
			Filter = "plink.exe|plink.exe|程序 (*.exe)|*.exe",
			Title = "选择 plink.exe"
		};
		try
		{
			try
			{
				string directoryName = Path.GetDirectoryName(((Control)_plink).Text.Trim());
				if (Directory.Exists(directoryName))
				{
					((FileDialog)val).InitialDirectory = directoryName;
				}
			}
			catch
			{
			}
			if ((int)((CommonDialog)val).ShowDialog((IWin32Window)(object)this) == 1)
			{
				((Control)_plink).Text = ((FileDialog)val).FileName;
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}
}
