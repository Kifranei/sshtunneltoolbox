using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SshTunnelToolbox;

public static class Plink
{
	public static string[] Candidates()
	{
		List<string> list = new List<string>();
		list.Add("C:\\Program Files\\PuTTY\\plink.exe");
		string environmentVariable = Environment.GetEnvironmentVariable("ProgramW6432");
		if (!string.IsNullOrEmpty(environmentVariable))
		{
			list.Add(Path.Combine(environmentVariable, "PuTTY\\plink.exe"));
		}
		list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PuTTY\\plink.exe"));
		list.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "PuTTY\\plink.exe"));
		list.Add("C:\\Program Files (x86)\\PuTTY\\plink.exe");
		string[] array = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(new char[1] { ';' });
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim().Trim(new char[1] { '"' });
			if (text.Length != 0)
			{
				try
				{
					list.Add(Path.Combine(text, "plink.exe"));
				}
				catch
				{
				}
			}
		}
		return list.ToArray();
	}

	public static string Detect()
	{
		string[] array = Candidates();
		foreach (string text in array)
		{
			try
			{
				if (File.Exists(text))
				{
					return text;
				}
			}
			catch
			{
			}
		}
		return null;
	}

	public static Version GetVersion(string plink, out string text)
	{
		text = "";
		try
		{
			using (Process process = Process.Start(new ProcessStartInfo(plink, "-V")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				RedirectStandardInput = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			}))
			{
				process.StandardInput.Close();
				Task<string> task = process.StandardOutput.ReadToEndAsync();
				Task<string> task2 = process.StandardError.ReadToEndAsync();
				if (!process.WaitForExit(5000))
				{
					try
					{
						process.Kill();
					}
					catch
					{
					}
				}
				text = ((task.Wait(1000) ? task.Result : "") + (task2.Wait(1000) ? task2.Result : "")).Trim();
			}
			Match match = Regex.Match(text, "(?:Release|Pre-release)\\s+(\\d+)\\.(\\d+)");
			if (match.Success)
			{
				return new Version(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
			}
			if (Regex.IsMatch(text, "Development snapshot|Custom build", RegexOptions.IgnoreCase))
			{
				return new Version(99, 0);
			}
		}
		catch (Exception ex)
		{
			text = ex.Message;
		}
		return null;
	}

	public static bool SupportsPwFile(Version v)
	{
		if (v != null)
		{
			return v >= new Version(0, 77);
		}
		return false;
	}

	public static string Quote(string arg)
	{
		if (arg == null)
		{
			arg = "";
		}
		if (arg.Length > 0 && arg.IndexOfAny(new char[3] { ' ', '\t', '"' }) < 0)
		{
			return arg;
		}
		StringBuilder stringBuilder = new StringBuilder("\"");
		int num = 0;
		string text = arg;
		foreach (char c in text)
		{
			switch (c)
			{
			case '\\':
				num++;
				break;
			case '"':
				stringBuilder.Append('\\', num * 2 + 1);
				stringBuilder.Append('"');
				num = 0;
				break;
			default:
				stringBuilder.Append('\\', num);
				num = 0;
				stringBuilder.Append(c);
				break;
			}
		}
		stringBuilder.Append('\\', num * 2);
		stringBuilder.Append('"');
		return stringBuilder.ToString();
	}
}
