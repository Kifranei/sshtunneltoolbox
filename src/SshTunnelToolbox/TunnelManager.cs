using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace SshTunnelToolbox;

public class TunnelManager : IDisposable
{
	private readonly object _lock = new object();

	private readonly ManualResetEvent _stopEvent = new ManualResetEvent(initialState: false);

	private readonly KillOnCloseJob _job = new KillOnCloseJob();

	private Thread _thread;

	private Process _proc;

	private volatile bool _stopRequested;

	private string _pwFile;

	private string _jumpPwFile;

	private const string PwToken = "\u0001PW\u0001";

	public TunnelState State { get; private set; }

	public int ConnectCount { get; private set; }

	public int AttemptCount { get; private set; }

	public DateTime? ConnectedSince { get; private set; }

	public bool Running => _thread?.IsAlive ?? false;

	public string Name { get; set; }

	public event Action<string, LogLevel> Log;

	public event Action StateChanged;

	private void L(string m, LogLevel lv)
	{
		this.Log?.Invoke(m, lv);
	}

	private void SetState(TunnelState s)
	{
		State = s;
		if (s != TunnelState.Connected)
		{
			ConnectedSince = null;
		}
		this.StateChanged?.Invoke();
	}

	public void Start(Profile cfg, string plinkPath)
	{
		lock (_lock)
		{
			if (!Running)
			{
				_stopRequested = false;
				_stopEvent.Reset();
				ConnectCount = 0;
				AttemptCount = 0;
				Profile snap = cfg.Clone();
				string pw = cfg.GetPassword();
				string jpw = cfg.GetJumpPassword();
				string plink = plinkPath;
				_thread = new Thread((ThreadStart)delegate
				{
					Loop(snap, pw, jpw, plink);
				})
				{
					IsBackground = true,
					Name = "plink-loop " + Name
				};
				_thread.Start();
			}
		}
	}

	public void Stop(bool wait = true)
	{
		Thread thread;
		lock (_lock)
		{
			_stopRequested = true;
			_stopEvent.Set();
			KillCurrent();
			thread = _thread;
		}
		if (wait && thread != null && thread != Thread.CurrentThread)
		{
			thread.Join(5000);
		}
	}

	private void KillCurrent()
	{
		Process proc = _proc;
		if (proc == null)
		{
			return;
		}
		try
		{
			if (!proc.HasExited)
			{
				try
				{
					using Process process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "taskkill.exe"), "/PID " + proc.Id + " /T /F")
					{
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardOutput = true,
						RedirectStandardError = true
					});
					process.WaitForExit(3000);
				}
				catch
				{
				}
				if (!proc.HasExited)
				{
					proc.Kill();
				}
			}
		}
		catch
		{
		}
		DeletePwFile();
	}

	private void DeletePwFile()
	{
		string jumpPwFile = _jumpPwFile;
		_jumpPwFile = null;
		DeleteFileQuiet(jumpPwFile);
		string pwFile = _pwFile;
		_pwFile = null;
		DeleteFileQuiet(pwFile);
	}

	private static void DeleteFileQuiet(string pwFile)
	{
		if (pwFile == null)
		{
			return;
		}
		for (int i = 0; i < 3; i++)
		{
			try
			{
				if (File.Exists(pwFile))
				{
					File.Delete(pwFile);
				}
				break;
			}
			catch
			{
				Thread.Sleep(100);
			}
		}
	}

	public static List<string> PreCheck(Profile cfg)
	{
		List<string> list = new List<string>();
		foreach (Forward forward in cfg.Forwards)
		{
			PortUtil.Listener listener = PortUtil.FindListener(forward.LocalAddress, forward.LocalPort);
			if (listener != null)
			{
				list.Add($"本地端口 {forward.LocalAddress}:{forward.LocalPort} 已被其它程序占用（{PortUtil.ProcessName(listener.Pid)}，PID {listener.Pid}），「{forward.Note}」转发会失败");
			}
		}
		return list;
	}

	private void Loop(Profile cfg, string pw, string jpw, string plink)
	{
		try
		{
			string text;
			Version version = Plink.GetVersion(plink, out text);
			L("plink 版本：" + (string.IsNullOrEmpty(text) ? "未知" : text.Split(new char[1] { '\n' })[0].Trim()), LogLevel.Detail);
			bool supportsPwFile = Plink.SupportsPwFile(version);
			bool flag = !string.IsNullOrEmpty(pw) && supportsPwFile;
			if (!string.IsNullOrEmpty(pw))
			{
				L(flag ? "密码通过临时文件 -pwfile 传递（连接后立即删除）" : "plink 版本低于 0.77 或无法识别，密码改用 -pw 传递", LogLevel.Detail);
			}
			else
			{
				L("未填写密码：-batch 模式下若服务器要求密码会直接失败（可用 Pageant 密钥代替）", LogLevel.Warn);
			}
			if (cfg.JumpActive)
			{
				L("使用跳板机：" + cfg.JumpUser + "@" + cfg.JumpHost + ":" + cfg.JumpPort + " → 目标 SSH " + cfg.Host + ":" + cfg.Port + "（相当于 ssh -J，通过 plink -proxycmd \"plink -nc\" 实现）", LogLevel.Detail);
				if (!string.IsNullOrEmpty(jpw) && !supportsPwFile)
				{
					L("plink 版本低于 0.77 或无法识别，跳板机密码改用 -pw 传递", LogLevel.Detail);
				}
				if (string.IsNullOrEmpty(jpw))
				{
					L("未填写跳板机密码：-batch 模式下若跳板机要求密码会直接失败（可用 Pageant 密钥代替）", LogLevel.Warn);
				}
				if (string.IsNullOrWhiteSpace(cfg.JumpHostKey))
				{
					L("未填写跳板机指纹：-batch 模式下遇到未缓存的跳板机主机密钥会直接失败。请填写跳板机指纹，或先用 PuTTY 手动连接一次跳板机并接受密钥。", LogLevel.Warn);
				}
			}
			if (string.IsNullOrWhiteSpace(cfg.HostKey))
			{
				L("未填写主机指纹：-batch 模式下遇到未缓存的主机密钥会直接失败。请填写指纹（如 SHA256:xxxx），或先用 PuTTY 手动连接一次并接受密钥。", LogLevel.Warn);
			}
			while (!_stopRequested)
			{
				foreach (string item in PreCheck(cfg))
				{
					L(item, LogLevel.Warn);
				}
				AttemptCount++;
				RunOnce(cfg, plink, pw, jpw, supportsPwFile);
				if (!_stopRequested)
				{
					int num = Math.Max(1, cfg.ReconnectDelaySeconds);
					SetState(TunnelState.Reconnecting);
					L($"连接已断开，{num} 秒后重连…", LogLevel.Warn);
					if (_stopEvent.WaitOne(num * 1000))
					{
						break;
					}
					continue;
				}
				break;
			}
		}
		catch (Exception ex)
		{
			L("隧道线程异常：" + ex.Message, LogLevel.Error);
		}
		finally
		{
			lock (_lock)
			{
				KillCurrent();
				_proc = null;
			}
			SetState(TunnelState.Stopped);
			L("隧道已停止", LogLevel.Info);
		}
	}

	private string WritePwFile(string pw)
	{
		string text = Path.Combine(Path.GetTempPath(), "SshTunnelToolbox");
		Directory.CreateDirectory(text);
		string path = Path.Combine(text, Guid.NewGuid().ToString("N") + ".tmp");
		File.WriteAllText(path, pw, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
		return path;
	}

	// PuTTY 会对 -proxycmd 的内容做 \ 和 % 替换（%host、%port 等），这里把字面量转义掉
	public static string EscapeProxyCmd(string s)
	{
		return (s ?? "").Replace("\\", "\\\\").Replace("%", "\\%");
	}

	// 构造内层命令：plink -ssh -batch -P 跳板端口 [-pwfile|-pw] [-hostkey] -nc 目标:端口 用户@跳板
	public static string BuildJumpCommand(Profile cfg, string plink, string pwArg, bool forLog)
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(Plink.Quote(plink));
		sb.Append(" -ssh ").Append(Plink.Quote(cfg.JumpUser.Trim() + "@" + cfg.JumpHost.Trim()));
		sb.Append(" -P ").Append(cfg.JumpPort);
		if (pwArg != null)
		{
			sb.Append(' ').Append(pwArg);
		}
		if (!string.IsNullOrWhiteSpace(cfg.JumpHostKey))
		{
			sb.Append(" -hostkey ").Append(Plink.Quote(cfg.JumpHostKey.Trim()));
		}
		string host = cfg.Host.Trim();
		if (host.Contains(":"))
		{
			host = "[" + host + "]";
		}
		if (cfg.Verbose)
		{
			sb.Append(" -v");
		}
		sb.Append(" -nc ").Append(Plink.Quote(host + ":" + cfg.Port));
		sb.Append(" -batch");
		return forLog ? sb.ToString() : EscapeProxyCmd(sb.ToString());
	}

	// 生成 plink 命令行。pwFile/jumpPwFile 不为 null 时用 -pwfile，否则有密码时用 -pw。
	// 返回真实参数；logArg 为日志用的脱敏版本，proxyLog 为跳板命令的脱敏版本（不使用跳板时为 null）
	public static string BuildArguments(Profile cfg, string plink, string pw, string pwFile, string jpw, string jumpPwFile, out string logArg, out string proxyLog)
	{
		StringBuilder stringBuilder = new StringBuilder();
		proxyLog = null;
		string proxyArg = "";
		if (cfg.JumpActive)
		{
			string jpwArg = null;
			string jpwLog = null;
			if (jumpPwFile != null)
			{
				jpwArg = jpwLog = "-pwfile " + Plink.Quote(jumpPwFile);
			}
			else if (!string.IsNullOrEmpty(jpw))
			{
				jpwArg = "-pw " + Plink.Quote(jpw);
				jpwLog = "-pw \"******\"";
			}
			proxyArg = "-proxycmd " + Plink.Quote(BuildJumpCommand(cfg, plink, jpwArg, forLog: false)) + " ";
			proxyLog = BuildJumpCommand(cfg, plink, jpwLog, forLog: true);
		}
		stringBuilder.Append("-ssh ").Append(Plink.Quote(cfg.User.Trim() + "@" + cfg.Host.Trim()));
		stringBuilder.Append(" -P ").Append(cfg.Port);
		if (pwFile != null)
		{
			stringBuilder.Append(" -pwfile ").Append(Plink.Quote(pwFile));
		}
		else if (!string.IsNullOrEmpty(pw))
		{
			stringBuilder.Append(" -pw ").Append("\u0001PW\u0001");
		}
		if (!string.IsNullOrWhiteSpace(cfg.HostKey))
		{
			stringBuilder.Append(" -hostkey ").Append(Plink.Quote(cfg.HostKey.Trim()));
		}
		stringBuilder.Append(" -N");
		if (cfg.Verbose)
		{
			stringBuilder.Append(" -v");
		}
		foreach (Forward forward in cfg.Forwards)
		{
			stringBuilder.Append(" -L ").Append(Plink.Quote(forward.ToArg()));
		}
		stringBuilder.Append(" -batch");
		logArg = stringBuilder.ToString().Replace("\u0001PW\u0001", "\"******\"");
		if (proxyLog != null)
		{
			logArg = "-proxycmd <跳板命令> " + logArg;
		}
		return proxyArg + stringBuilder.ToString().Replace("\u0001PW\u0001", Plink.Quote(pw ?? ""));
	}

	private void RunOnce(Profile cfg, string plink, string pw, string jpw, bool usePwFile)
	{
		if (usePwFile && !string.IsNullOrEmpty(pw))
		{
			_pwFile = WritePwFile(pw);
		}
		if (usePwFile && cfg.JumpActive && !string.IsNullOrEmpty(jpw))
		{
			_jumpPwFile = WritePwFile(jpw);
		}
		string arg;
		string proxyLog;
		string arguments = BuildArguments(cfg, plink, pw, _pwFile, jpw, _jumpPwFile, out arg, out proxyLog);
		SetState(TunnelState.Connecting);
		L($"启动 plink（第 {AttemptCount} 次）：{Plink.Quote(plink)} {arg}", LogLevel.Info);
		if (proxyLog != null)
		{
			L("跳板命令：" + proxyLog, LogLevel.Detail);
		}
		ProcessStartInfo startInfo = new ProcessStartInfo(plink, arguments)
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
		Process process = new Process
		{
			StartInfo = startInfo,
			EnableRaisingEvents = true
		};
		DataReceivedEventHandler value = delegate(object s, DataReceivedEventArgs e)
		{
			if (!string.IsNullOrEmpty(e.Data))
			{
				string line = e.Data;
				if (!string.IsNullOrEmpty(pw) && pw.Length >= 2)
				{
					line = line.Replace(pw, "******");
				}
				if (!string.IsNullOrEmpty(jpw) && jpw.Length >= 2)
				{
					line = line.Replace(jpw, "******");
				}
				L("plink> " + line, LogLevel.Plink);
			}
		};
		process.OutputDataReceived += value;
		process.ErrorDataReceived += value;
		try
		{
			lock (_lock)
			{
				if (_stopRequested)
				{
					DeletePwFile();
					return;
				}
				process.Start();
				_proc = process;
			}
		}
		catch (Exception ex)
		{
			DeletePwFile();
			L("无法启动 plink：" + ex.Message, LogLevel.Error);
			return;
		}
		_job.Assign(process);
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();
		DateTime now = DateTime.Now;
		bool flag = false;
		DateTime now2 = DateTime.Now;
		bool flag2 = false;
		while (!process.WaitForExit(500) && !_stopRequested)
		{
			TimeSpan timeSpan = DateTime.Now - now;
			if (!flag && timeSpan.TotalSeconds >= 3.0)
			{
				bool flag3 = true;
				foreach (Forward forward2 in cfg.Forwards)
				{
					if (!PortUtil.TryConnect(forward2.LocalAddress, forward2.LocalPort, 1000, out var _))
					{
						flag3 = false;
						break;
					}
				}
				if (flag3 && !process.HasExited)
				{
					flag = true;
					ConnectCount++;
					DeletePwFile();
					SetState(TunnelState.Connected);
					ConnectedSince = DateTime.Now;
					this.StateChanged?.Invoke();
					L(string.Format("隧道已连接（第 {0} 次连接），本地端口：{1}", ConnectCount, string.Join("、", cfg.Forwards.Select((Forward f) => f.LocalPort + "（" + f.Note + "）"))), LogLevel.Ok);
				}
				else if (timeSpan.TotalSeconds >= 30.0 && !flag2)
				{
					flag2 = true;
					L("plink 已运行 30 秒，但本地端口仍未全部监听，请查看上方 plink 输出", LogLevel.Warn);
				}
			}
			if ((_pwFile != null || _jumpPwFile != null) && timeSpan.TotalSeconds >= 30.0)
			{
				DeletePwFile();
			}
			if (!flag || !((DateTime.Now - now2).TotalSeconds >= 15.0))
			{
				continue;
			}
			now2 = DateTime.Now;
			List<Forward> list = cfg.Forwards.Where((Forward f) => PortUtil.FindListener(f.LocalAddress, f.LocalPort) == null).ToList();
			if (list.Count > 0 && !flag2)
			{
				flag2 = true;
				L("本地端口不再监听：" + string.Join("、", list.Select((Forward f) => f.LocalPort.ToString())), LogLevel.Warn);
			}
			else if (list.Count == 0)
			{
				flag2 = false;
			}
		}
		if (!process.HasExited)
		{
			lock (_lock)
			{
				KillCurrent();
			}
			try
			{
				process.WaitForExit(3000);
			}
			catch
			{
			}
		}
		try
		{
			process.WaitForExit();
		}
		catch
		{
		}
		DeletePwFile();
		int num = -1;
		try
		{
			num = process.ExitCode;
		}
		catch
		{
		}
		TimeSpan t = DateTime.Now - now;
		if (_stopRequested)
		{
			L($"plink 已结束（用户停止，运行 {Fmt(t)}）", LogLevel.Info);
		}
		else
		{
			L(string.Format("plink 已退出，退出码 {0}，本次运行 {1}{2}", num, Fmt(t), flag ? "" : "（未能建立隧道）"), LogLevel.Error);
		}
		lock (_lock)
		{
			_proc = null;
		}
		process.Dispose();
	}

	public static string Fmt(TimeSpan t)
	{
		return string.Format("{0}{1:00}:{2:00}:{3:00}", (t.Days > 0) ? (t.Days + "天 ") : "", t.Hours, t.Minutes, t.Seconds);
	}

	public void Dispose()
	{
		Stop();
		_job.Dispose();
	}
}
