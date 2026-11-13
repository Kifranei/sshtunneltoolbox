using System;
using System.Collections.Generic;
using System.Text;

namespace SshTunnelToolbox;

// 解析形如 “ssh -N -L 2222:192.0.2.20:22 user@10.0.0.1” 或带 -J 跳板的命令，用于一键导入配置
public class SshCommand
{
	public string Host = "";

	public int Port = 22;

	public string User = "";

	public string JumpHost = "";

	public int JumpPort = 22;

	public string JumpUser = "";

	public string HostKey = "";

	public List<Forward> Forwards = new List<Forward>();

	public List<string> Notes = new List<string>();

	public static List<string> Tokenize(string s)
	{
		List<string> list = new List<string>();
		StringBuilder sb = new StringBuilder();
		bool any = false;
		char q = '\0';
		foreach (char c in s ?? "")
		{
			if (q != '\0')
			{
				if (c == q)
				{
					q = '\0';
				}
				else
				{
					sb.Append(c);
				}
			}
			else if (c == '"' || c == '\'')
			{
				q = c;
				any = true;
			}
			else if (char.IsWhiteSpace(c))
			{
				if (any || sb.Length > 0)
				{
					list.Add(sb.ToString());
				}
				sb.Clear();
				any = false;
			}
			else
			{
				sb.Append(c);
			}
		}
		if (any || sb.Length > 0)
		{
			list.Add(sb.ToString());
		}
		return list;
	}

	// [user@]host[:port]，host 可为 [IPv6]
	public static bool ParseUserHostPort(string s, out string user, out string host, out int port, out string error)
	{
		user = "";
		host = "";
		port = 22;
		error = null;
		s = (s ?? "").Trim();
		if (s.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
		{
			s = s.Substring(6).TrimEnd('/');
		}
		int at = s.LastIndexOf('@');
		if (at >= 0)
		{
			user = s.Substring(0, at);
			s = s.Substring(at + 1);
		}
		if (s.StartsWith("["))
		{
			int rb = s.IndexOf(']');
			if (rb < 0)
			{
				error = "地址格式错误：" + s;
				return false;
			}
			host = s.Substring(1, rb - 1);
			string rest = s.Substring(rb + 1);
			if (rest.StartsWith(":") && !TryPort(rest.Substring(1), out port))
			{
				error = "端口无效：" + rest.Substring(1);
				return false;
			}
		}
		else
		{
			int colon = s.IndexOf(':');
			if (colon >= 0 && s.IndexOf(':', colon + 1) < 0)
			{
				host = s.Substring(0, colon);
				if (!TryPort(s.Substring(colon + 1), out port))
				{
					error = "端口无效：" + s.Substring(colon + 1);
					return false;
				}
			}
			else
			{
				host = s;
			}
		}
		if (host == "")
		{
			error = "缺少主机地址";
			return false;
		}
		return true;
	}

	private static bool TryPort(string s, out int port)
	{
		return int.TryParse(s, out port) && port >= 1 && port <= 65535;
	}

	// 拆分 -L 参数，支持 [IPv6]
	private static List<string> SplitColon(string s)
	{
		List<string> list = new List<string>();
		StringBuilder sb = new StringBuilder();
		bool br = false;
		foreach (char c in s)
		{
			if (c == '[')
			{
				br = true;
			}
			else if (c == ']')
			{
				br = false;
			}
			else if (c == ':' && !br)
			{
				list.Add(sb.ToString());
				sb.Clear();
			}
			else
			{
				sb.Append(c);
			}
		}
		list.Add(sb.ToString());
		return list;
	}

	public static bool ParseForward(string spec, out Forward f, out string error)
	{
		f = null;
		error = null;
		List<string> p = SplitColon(spec.Trim());
		string bind = "127.0.0.1";
		if (p.Count == 4)
		{
			bind = p[0];
			p.RemoveAt(0);
			if (bind == "" || bind == "*")
			{
				bind = "0.0.0.0";
			}
			else if (bind.Equals("localhost", StringComparison.OrdinalIgnoreCase))
			{
				bind = "127.0.0.1";
			}
		}
		if (p.Count != 3)
		{
			error = "-L 参数格式应为 [本地地址:]本地端口:目标主机:目标端口，实际为：" + spec;
			return false;
		}
		if (!TryPort(p[0], out var lport))
		{
			error = "-L 本地端口无效：" + spec;
			return false;
		}
		if (p[1] == "")
		{
			error = "-L 目标主机为空：" + spec;
			return false;
		}
		if (!TryPort(p[2], out var tport))
		{
			error = "-L 目标端口无效：" + spec;
			return false;
		}
		f = new Forward
		{
			Note = p[1] + ":" + tport,
			LocalAddress = bind,
			LocalPort = lport,
			TargetHost = p[1],
			TargetPort = tport
		};
		return true;
	}

	public static SshCommand Parse(string cmd, out string error)
	{
		error = null;
		SshCommand r = new SshCommand();
		List<string> t = Tokenize(cmd);
		int i = 0;
		if (t.Count > 0)
		{
			string first = t[0].Replace('\\', '/');
			int slash = first.LastIndexOf('/');
			string exe = ((slash >= 0) ? first.Substring(slash + 1) : first).ToLowerInvariant();
			if (exe == "ssh" || exe == "ssh.exe" || exe == "plink" || exe == "plink.exe")
			{
				i = 1;
			}
		}
		string dest = null;
		bool portSet = false;
		string loginUser = null;
		const string flagsNoArg = "46AaCfGgKkMNnqsTtVvXxYy";
		const string flagsArg = "BbcDEeFIiJLlmOoPpQRSWw";
		for (; i < t.Count; i++)
		{
			string a = t[i];
			if (a.Length >= 2 && a[0] == '-' && dest == null)
			{
				// PuTTY 风格的长参数
				string lower = a.ToLowerInvariant();
				if (lower == "-ssh" || lower == "-batch" || lower == "-noagent" || lower == "-agent" || lower == "-v")
				{
					continue;
				}
				if (lower == "-hostkey" || lower == "-pw" || lower == "-pwfile" || lower == "-proxycmd")
				{
					if (i + 1 >= t.Count)
					{
						error = a + " 缺少参数";
						return null;
					}
					string v = t[++i];
					if (lower == "-hostkey")
					{
						r.HostKey = v;
					}
					else if (lower == "-proxycmd")
					{
						r.Notes.Add("已忽略 -proxycmd（请改用“通过跳板机连接”）");
					}
					else
					{
						r.Notes.Add("已忽略命令里的密码参数，请在界面里填写密码");
					}
					continue;
				}
				// OpenSSH 风格：可组合短参数，如 -NL 2222:host:22、-p2222
				for (int k = 1; k < a.Length; k++)
				{
					char c = a[k];
					if (flagsNoArg.IndexOf(c) >= 0)
					{
						continue;
					}
					if (flagsArg.IndexOf(c) < 0)
					{
						error = "不认识的参数：-" + c;
						return null;
					}
					string val;
					if (k + 1 < a.Length)
					{
						val = a.Substring(k + 1);
					}
					else
					{
						if (i + 1 >= t.Count)
						{
							error = "-" + c + " 缺少参数";
							return null;
						}
						val = t[++i];
					}
					k = a.Length;
					switch (c)
					{
					case 'L':
					{
						if (!ParseForward(val, out var f, out error))
						{
							return null;
						}
						r.Forwards.Add(f);
						break;
					}
					case 'p':
					case 'P':
						if (!TryPort(val, out r.Port))
						{
							error = "端口无效：" + val;
							return null;
						}
						portSet = true;
						break;
					case 'l':
						loginUser = val;
						break;
					case 'J':
					{
						if (val.Contains(","))
						{
							error = "只支持一级跳板机（-J 里不能有逗号）";
							return null;
						}
						if (!ParseUserHostPort(val, out r.JumpUser, out r.JumpHost, out r.JumpPort, out error))
						{
							return null;
						}
						break;
					}
					case 'R':
					case 'D':
						error = "暂不支持 -" + c + "（只支持本地转发 -L）";
						return null;
					case 'i':
						r.Notes.Add("已忽略私钥参数 -i：plink 需要 .ppk 私钥，可加载到 Pageant 后使用");
						break;
					default:
						r.Notes.Add("已忽略参数 -" + c + " " + val);
						break;
					}
				}
				continue;
			}
			if (dest == null)
			{
				dest = a;
				continue;
			}
			error = "ssh 命令里不应包含远程要执行的命令：" + a + "（隧道只需要 -N）";
			return null;
		}
		if (dest == null)
		{
			error = "没有找到 用户@主机";
			return null;
		}
		if (!ParseUserHostPort(dest, out r.User, out r.Host, out var p2, out error))
		{
			return null;
		}
		if (!portSet)
		{
			r.Port = p2;
		}
		if (r.User == "" && loginUser != null)
		{
			r.User = loginUser;
		}
		if (r.JumpHost != "" && r.JumpUser == "")
		{
			r.JumpUser = r.User;
		}
		if (r.Forwards.Count == 0)
		{
			error = "命令里没有 -L 端口转发";
			return null;
		}
		return r;
	}
}
