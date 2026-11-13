using System;
using Microsoft.UI.Xaml;

namespace SshTunnelToolbox;

public partial class App : Application
{
	private readonly bool _trayArg;

	private MainWindow _window;

	public App(bool trayArg)
	{
		_trayArg = trayArg;
		InitializeComponent();
		UnhandledException += (s, e) =>
		{
			e.Handled = true;
			try
			{
				_window?.LogGlobal("程序出错：" + e.Message, LogLevel.Error);
				_ = Dialogs.Message("程序出错：\n" + e.Message);
			}
			catch
			{
			}
		};
	}

	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		_window = new MainWindow(_trayArg);
		_window.Start();
	}
}
