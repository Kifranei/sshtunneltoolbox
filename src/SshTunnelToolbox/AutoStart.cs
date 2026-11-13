using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SshTunnelToolbox;

internal static class AutoStart
{
	private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string ValueName = "SshTunnelToolbox";

	public static string Command => "\"" + Application.ExecutablePath + "\" --tray";

	public static bool IsEnabled => !string.IsNullOrEmpty(Current());

	public static bool IsStale
	{
		get
		{
			string text = Current();
			if (!string.IsNullOrEmpty(text))
			{
				return !string.Equals(text.Trim(), Command, StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
	}

	public static string Current()
	{
		try
		{
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: false);
			return (registryKey == null) ? null : (registryKey.GetValue("SshTunnelToolbox") as string);
		}
		catch
		{
			return null;
		}
	}

	public static void Set(bool enable)
	{
		using RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run");
		if (enable)
		{
			registryKey.SetValue("SshTunnelToolbox", Command, RegistryValueKind.String);
		}
		else if (registryKey.GetValue("SshTunnelToolbox") != null)
		{
			registryKey.DeleteValue("SshTunnelToolbox", throwOnMissingValue: false);
		}
	}
}
