using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace SshTunnelToolbox;

// 获取服务器主机指纹：plink -v 配一个一定不匹配的 -hostkey，plink 会在
// 校验前打印 “Host key fingerprint is:”，然后中止（不会尝试登录）。
// 无论 PuTTY 注册表里是否已缓存该主机密钥都能拿到。
public class HostKeyResult
{
	public bool Ok;

	public string KeyType = "";

	public string Fingerprint = "";

	public string Error = "";

	public string Output = "";
}

public static class HostKeyFetcher
{
	// 16 字节全 0 的 MD5 形式，所有 plink 版本都接受该格式，且不可能匹配真实密钥
	public const string BogusHostKey = "00:00:00:00:00:00:00:00:00:00:00:00:00:00:00:00";

	private static readonly Regex Sha256Re = new Regex("SHA256:[A-Za-z0-9+/]{43}=?");

	private static readonly Regex Md5Re = new Regex("(?<![0-9a-fA-F:])([0-9a-fA-F]{2}(?::[0-9a-fA-F]{2}){15})(?![0-9a-fA-F:])");

	private static readonly Regex TypeRe = new Regex("^(ssh-[A-Za-z0-9-]+|ecdsa-[A-Za-z0-9-]+|rsa2?|dss|ssh-dss)\\s+\\d+\\b");

	// 从 plink 输出中解析指纹。优先取 “Host key fingerprint is:” 之后那一行（外层连接的主机），
	// 跳过 “proxy:” 开头的跳板命令输出。
	public static bool Parse(string output, out string keyType, out string fingerprint)
	{
		keyType = "";
		fingerprint = "";
		List<string> lines = new List<string>();
		foreach (string raw in (output ?? "").Replace("\r", "").Split('\n'))
		{
			string t = raw.Trim();
			if (t.Length > 0 && !t.StartsWith("proxy", StringComparison.OrdinalIgnoreCase))
			{
				lines.Add(t);
			}
		}
		List<string> candidates = new List<string>();
		for (int i = 0; i < lines.Count; i++)
		{
			string l = lines[i];
			int idx = l.IndexOf("fingerprint is", StringComparison.OrdinalIgnoreCase);
			if (idx < 0)
			{
				continue;
			}
			string rest = l.Substring(idx + "fingerprint is".Length).TrimStart(':', ' ');
			if (rest.Length > 0)
			{
				candidates.Add(rest);
			}
			if (i + 1 < lines.Count)
			{
				candidates.Add(lines[i + 1]);
			}
		}
		foreach (string c in candidates)
		{
			if (TryLine(c, out keyType, out fingerprint))
			{
				return true;
			}
		}
		return false;
	}

	private static bool TryLine(string line, out string keyType, out string fingerprint)
	{
		keyType = "";
		fingerprint = "";
		Match m = Sha256Re.Match(line);
		if (m.Success)
		{
			fingerprint = m.Value.TrimEnd('=');
		}
		else
		{
			m = Md5Re.Match(line);
			if (!m.Success)
			{
				return false;
			}
			fingerprint = m.Groups[1].Value.ToLowerInvariant();
		}
		Match tm = TypeRe.Match(line);
		if (tm.Success)
		{
			keyType = tm.Groups[1].Value;
		}
		return true;
	}

	// 构造参数；proxyCmdArg 为已经引用好的 “-proxycmd "..." ” 片段（可为空）
	public static string BuildArguments(string host, int port, string proxyCmdArg)
	{
		string h = (host ?? "").Trim();
		StringBuilder sb = new StringBuilder();
		sb.Append(proxyCmdArg ?? "");
		sb.Append("-ssh -v -batch -noagent -P ").Append(port);
		sb.Append(" -hostkey ").Append(BogusHostKey);
		sb.Append(" -N ").Append(Plink.Quote(h));
		return sb.ToString();
	}

	// 直连或经跳板（jumpCfg.JumpActive 时，用 jumpCfg 的跳板设置走 -proxycmd）获取 host:port 的指纹
	public static HostKeyResult Fetch(string plink, string host, int port, Profile jumpCfg, string jumpPw, int timeoutMs = 15000)
	{
		HostKeyResult r = new HostKeyResult();
		string pwFile = null;
		try
		{
			string proxyArg = "";
			if (jumpCfg != null && jumpCfg.JumpActive)
			{
				Profile p = jumpCfg.Clone();
				p.Host = host;
				p.Port = port;
				p.Verbose = false;
				string jpwArg = null;
				if (!string.IsNullOrEmpty(jumpPw))
				{
					if (Plink.SupportsPwFile(Plink.GetVersion(plink, out var _)))
					{
						string dir = Path.Combine(Path.GetTempPath(), "SshTunnelToolbox");
						Directory.CreateDirectory(dir);
						pwFile = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".tmp");
						File.WriteAllText(pwFile, jumpPw, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
						jpwArg = "-pwfile " + Plink.Quote(pwFile);
					}
					else
					{
						jpwArg = "-pw " + Plink.Quote(jumpPw);
					}
				}
				proxyArg = "-proxycmd " + Plink.Quote(TunnelManager.BuildJumpCommand(p, plink, jpwArg, forLog: false)) + " ";
			}
			string args = BuildArguments(host, port, proxyArg);
			StringBuilder output = new StringBuilder();
			object gate = new object();
			using (Process proc = new Process())
			{
				proc.StartInfo = new ProcessStartInfo(plink, args)
				{
					UseShellExecute = false,
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					RedirectStandardInput = true,
					StandardOutputEncoding = Encoding.UTF8,
					StandardErrorEncoding = Encoding.UTF8
				};
				DataReceivedEventHandler h = delegate(object s, DataReceivedEventArgs e)
				{
					if (e.Data != null)
					{
						lock (gate)
						{
							output.AppendLine(e.Data);
						}
					}
				};
				proc.OutputDataReceived += h;
				proc.ErrorDataReceived += h;
				proc.Start();
				try
				{
					proc.StandardInput.Close();
				}
				catch
				{
				}
				proc.BeginOutputReadLine();
				proc.BeginErrorReadLine();
				bool exited = proc.WaitForExit(timeoutMs);
				if (!exited)
				{
					KillTree(proc);
					r.Error = "超时（" + timeoutMs / 1000 + " 秒）未拿到指纹";
				}
				else
				{
					proc.WaitForExit();
				}
				Thread.Sleep(50);
				lock (gate)
				{
					r.Output = output.ToString();
				}
			}
			if (!string.IsNullOrEmpty(jumpPw))
			{
				r.Output = r.Output.Replace(jumpPw, "******");
			}
			if (Parse(r.Output, out r.KeyType, out r.Fingerprint))
			{
				r.Ok = true;
				r.Error = "";
			}
			else if (r.Error == "")
			{
				r.Error = LastError(r.Output);
			}
		}
		catch (Exception ex)
		{
			r.Error = "无法运行 plink：" + ex.Message;
		}
		finally
		{
			if (pwFile != null)
			{
				try
				{
					File.Delete(pwFile);
				}
				catch
				{
				}
			}
		}
		return r;
	}

	private static string LastError(string output)
	{
		string last = null;
		foreach (string raw in (output ?? "").Replace("\r", "").Split('\n'))
		{
			string t = raw.Trim();
			if (t.Length == 0)
			{
				continue;
			}
			if (t.IndexOf("FATAL ERROR", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf("unable", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				last = t;
			}
		}
		return last ?? "plink 输出中没有找到主机指纹";
	}

	private static void KillTree(Process proc)
	{
		try
		{
			if (Environment.OSVersion.Platform == PlatformID.Win32NT)
			{
				using Process k = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "taskkill.exe"), "/PID " + proc.Id + " /T /F")
				{
					UseShellExecute = false,
					CreateNoWindow = true
				});
				k.WaitForExit(3000);
			}
		}
		catch
		{
		}
		try
		{
			if (!proc.HasExited)
			{
				proc.Kill();
			}
		}
		catch
		{
		}
	}
}
