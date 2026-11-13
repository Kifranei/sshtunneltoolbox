using System.Drawing;
using System.Windows.Forms;

namespace SshTunnelToolbox;

public class CloseAskForm : Form
{
	private readonly CheckBox _remember;

	public string Choice { get; private set; }

	public bool Remember => _remember.Checked;

	public CloseAskForm(bool running)
	{
		((ContainerControl)this).AutoScaleDimensions = new SizeF(96f, 96f);
		((ContainerControl)this).AutoScaleMode = (AutoScaleMode)2;
		((Control)this).Font = new Font("Microsoft YaHei UI", 9f);
		((Control)this).Text = "SSH 隧道工具箱";
		((Form)this).Icon = MainForm.AppIcon;
		((Form)this).FormBorderStyle = (FormBorderStyle)3;
		((Form)this).MaximizeBox = false;
		((Form)this).MinimizeBox = false;
		((Form)this).ShowInTaskbar = false;
		((Form)this).StartPosition = (FormStartPosition)4;
		((Control)this).AutoSize = true;
		((Form)this).AutoSizeMode = (AutoSizeMode)0;
		((Control)this).Padding = new Padding(14, 12, 14, 10);
		FlowLayoutPanel val = new FlowLayoutPanel
		{
			FlowDirection = (FlowDirection)1,
			WrapContents = false,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			Location = new Point(14, 12)
		};
		Label val2 = new Label
		{
			AutoSize = true,
			MaximumSize = new Size(420, 0),
			Margin = new Padding(0, 0, 0, 10),
			Text = (running ? "有隧道正在运行。关闭窗口要怎么处理？\n\n「最小化到托盘」：隧道继续在后台运行\n「退出程序」：停止所有隧道并退出" : "要最小化到托盘，还是退出程序？")
		};
		_remember = new CheckBox
		{
			Text = "记住我的选择（以后可在「托盘」设置里修改）",
			AutoSize = true,
			Margin = new Padding(0, 0, 0, 10)
		};
		FlowLayoutPanel val3 = new FlowLayoutPanel
		{
			FlowDirection = (FlowDirection)0,
			WrapContents = false,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			Margin = new Padding(0)
		};
		Button val4 = Btn("最小化到托盘", "tray");
		Button val5 = Btn("退出程序", "exit");
		Button val6 = Btn("取消", null);
		((Control)val3).Controls.Add((Control)(object)val4);
		((Control)val3).Controls.Add((Control)(object)val5);
		((Control)val3).Controls.Add((Control)(object)val6);
		((Control)val).Controls.Add((Control)(object)val2);
		((Control)val).Controls.Add((Control)(object)_remember);
		((Control)val).Controls.Add((Control)(object)val3);
		((Control)this).Controls.Add((Control)(object)val);
		((Form)this).AcceptButton = (IButtonControl)(object)val4;
		((Form)this).CancelButton = (IButtonControl)(object)val6;
	}

	private Button Btn(string text, string choice)
	{
		Button val = new Button
		{
			Text = text,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			Padding = new Padding(10, 3, 10, 3),
			Margin = new Padding(0, 0, 8, 0),
			UseVisualStyleBackColor = true
		};
		((Control)val).Click += delegate
		{
			Choice = choice;
			((Form)this).DialogResult = (DialogResult)((choice != null) ? 1 : 2);
			((Form)this).Close();
		};
		return val;
	}
}
