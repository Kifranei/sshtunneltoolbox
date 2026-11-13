using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace SshTunnelToolbox;

public static class Program
{
	public const string AppTitle = "SSH 隧道工具箱";

	[STAThread]
	private static int Main(string[] args)
	{
		// 单文件发布时运行库与 resources.pri 被解压到临时目录，需告知 Windows App SDK 去那里找
		if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY")))
		{
			Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
		}
		WinRT.ComWrappersSupport.InitializeComWrappers();
		using Mutex mutex = new Mutex(initiallyOwned: true, "Local\\SshTunnelToolbox_SingleInstance", out bool createdNew);
		if (!createdNew)
		{
			Win32.MessageBoxW(IntPtr.Zero, "SSH 隧道工具箱已经在运行（请查看右下角托盘图标）。", AppTitle, Win32.MB_OK | Win32.MB_ICONINFORMATION);
			return 0;
		}
		bool trayArg = false;
		foreach (string a in args ?? Array.Empty<string>())
		{
			if (string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "/tray", StringComparison.OrdinalIgnoreCase))
			{
				trayArg = true;
			}
		}
		Application.Start(p =>
		{
			DispatcherQueueSynchronizationContext ctx = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
			SynchronizationContext.SetSynchronizationContext(ctx);
			_ = new App(trayArg);
		});
		GC.KeepAlive(mutex);
		return 0;
	}
}
