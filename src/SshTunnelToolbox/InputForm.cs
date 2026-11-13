using System;
using System.Drawing;
using System.Windows.Forms;

namespace SshTunnelToolbox;

public class InputForm : Form
{
	private readonly TextBox _box;

	private readonly Func<string, string> _validate;

	public string Value => ((Control)_box).Text.Trim();

	public InputForm(string title, string prompt, string value, Func<string, string> validate, int boxWidth = 320)
	{
		_validate = validate;
		((ContainerControl)this).AutoScaleDimensions = new SizeF(96f, 96f);
		((ContainerControl)this).AutoScaleMode = (AutoScaleMode)2;
		((Control)this).Font = new Font("Microsoft YaHei UI", 9f);
		((Control)this).Text = title;
		((Form)this).Icon = MainForm.AppIcon;
		((Form)this).FormBorderStyle = (FormBorderStyle)3;
		((Form)this).MaximizeBox = false;
		((Form)this).MinimizeBox = false;
		((Form)this).ShowInTaskbar = false;
		((Form)this).StartPosition = (FormStartPosition)4;
		((Control)this).AutoSize = true;
		((Form)this).AutoSizeMode = (AutoSizeMode)0;
		((Control)this).Padding = new Padding(12, 10, 12, 10);
		FlowLayoutPanel val = new FlowLayoutPanel
		{
			FlowDirection = (FlowDirection)1,
			WrapContents = false,
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			Location = new Point(12, 10)
		};
		((Control)val).Controls.Add((Control)new Label
		{
			Text = prompt,
			AutoSize = true,
			Margin = new Padding(0, 0, 0, 4)
		});
		_box = new TextBox
		{
			Text = (value ?? ""),
			Width = boxWidth,
			Margin = new Padding(0, 0, 0, 10)
		};
		((Control)val).Controls.Add((Control)(object)_box);
		FlowLayoutPanel val2 = new FlowLayoutPanel
		{
			AutoSize = true,
			AutoSizeMode = (AutoSizeMode)0,
			WrapContents = false,
			Margin = new Padding(0)
		};
		Button val3 = new Button
		{
			Text = "确定",
			AutoSize = true,
			Padding = new Padding(10, 2, 10, 2),
			Margin = new Padding(0, 0, 8, 0),
			UseVisualStyleBackColor = true
		};
		Button val4 = new Button
		{
			Text = "取消",
			AutoSize = true,
			Padding = new Padding(10, 2, 10, 2),
			Margin = new Padding(0),
			DialogResult = (DialogResult)2,
			UseVisualStyleBackColor = true
		};
		((Control)val3).Click += delegate
		{
			string text = ((_validate != null) ? _validate(Value) : null);
			if (text != null)
			{
				MessageBox.Show((IWin32Window)(object)this, text, ((Control)this).Text, (MessageBoxButtons)0, (MessageBoxIcon)48);
				((Control)_box).Focus();
				((TextBoxBase)_box).SelectAll();
			}
			else
			{
				((Form)this).DialogResult = (DialogResult)1;
				((Form)this).Close();
			}
		};
		((Control)val2).Controls.Add((Control)(object)val3);
		((Control)val2).Controls.Add((Control)(object)val4);
		((Control)val).Controls.Add((Control)(object)val2);
		((Control)this).Controls.Add((Control)(object)val);
		((Form)this).AcceptButton = (IButtonControl)(object)val3;
		((Form)this).CancelButton = (IButtonControl)(object)val4;
		((Form)this).Shown += delegate
		{
			((Control)_box).Focus();
			((TextBoxBase)_box).SelectAll();
		};
	}
}
