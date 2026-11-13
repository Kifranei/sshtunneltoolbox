using System;
using System.Threading;
using System.Windows.Forms;

namespace SshTunnelToolbox;

internal static class Program
{
	public const string AppTitle = "SSH 隧道工具箱";

	[STAThread]
	private static void Main(string[] args)
	{
		bool createdNew;
		using (new Mutex(initiallyOwned: true, "Local\\SshTunnelToolbox_SingleInstance", out createdNew))
		{
			if (!createdNew)
			{
				MessageBox.Show("SSH 隧道工具箱已经在运行（请查看右下角托盘图标）。", "SSH 隧道工具箱", (MessageBoxButtons)0, (MessageBoxIcon)64);
				return;
			}
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.SetUnhandledExceptionMode((UnhandledExceptionMode)2);
			Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
			{
				MessageBox.Show("程序出错：\n" + e.Exception.Message, "SSH 隧道工具箱", (MessageBoxButtons)0, (MessageBoxIcon)16);
			};
			bool trayArg = false;
			string[] array = args ?? new string[0];
			foreach (string a in array)
			{
				if (string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "/tray", StringComparison.OrdinalIgnoreCase))
				{
					trayArg = true;
				}
			}
			Application.Run((Form)(object)new MainForm(trayArg));
		}
	}
}
