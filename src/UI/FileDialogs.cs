using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SshTunnelToolbox;

/// <summary>经典 Win32 打开/保存对话框。WinUI 的 FileOpenPicker 在管理员进程里不可用，所以统一用 comdlg32。</summary>
internal static class FileDialogs
{
	private const int OFN_OVERWRITEPROMPT = 0x2, OFN_NOCHANGEDIR = 0x8, OFN_PATHMUSTEXIST = 0x800, OFN_FILEMUSTEXIST = 0x1000, OFN_EXPLORER = 0x80000;

	private const int BufChars = 4096;

	/// <param name="filter">形如 "JSON 配置 (*.json)|*.json|所有文件 (*.*)|*.*"</param>
	public static string Open(IntPtr owner, string title, string filter, string initialPath = null)
	{
		return Show(owner, title, filter, initialPath, save: false, defExt: null);
	}

	public static string Save(IntPtr owner, string title, string filter, string fileName, string defExt)
	{
		return Show(owner, title, filter, fileName, save: true, defExt: defExt);
	}

	private static string Show(IntPtr owner, string title, string filter, string initialPath, bool save, string defExt)
	{
		IntPtr buf = Marshal.AllocHGlobal(BufChars * 2);
		try
		{
			char[] zero = new char[BufChars];
			string init = "";
			string dir = null;
			if (!string.IsNullOrEmpty(initialPath))
			{
				try
				{
					if (Directory.Exists(initialPath))
					{
						dir = initialPath;
					}
					else
					{
						init = Path.GetFileName(initialPath);
						string d = Path.GetDirectoryName(initialPath);
						if (!string.IsNullOrEmpty(d) && Directory.Exists(d))
						{
							dir = d;
						}
					}
				}
				catch
				{
				}
			}
			init.CopyTo(0, zero, 0, Math.Min(init.Length, BufChars - 1));
			Marshal.Copy(zero, 0, buf, BufChars);
			var ofn = new Win32.OPENFILENAMEW
			{
				lStructSize = Marshal.SizeOf<Win32.OPENFILENAMEW>(),
				hwndOwner = owner,
				lpstrFilter = (filter ?? "所有文件 (*.*)|*.*").Replace('|', '\0') + "\0\0",
				nFilterIndex = 1,
				lpstrFile = buf,
				nMaxFile = BufChars,
				lpstrInitialDir = dir,
				lpstrTitle = title,
				lpstrDefExt = defExt,
				Flags = OFN_EXPLORER | OFN_NOCHANGEDIR | OFN_PATHMUSTEXIST | (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST)
			};
			bool ok = save ? Win32.GetSaveFileNameW(ref ofn) : Win32.GetOpenFileNameW(ref ofn);
			if (!ok)
			{
				return null;
			}
			string result = Marshal.PtrToStringUni(buf);
			return string.IsNullOrEmpty(result) ? null : result;
		}
		finally
		{
			Marshal.FreeHGlobal(buf);
		}
	}
}
