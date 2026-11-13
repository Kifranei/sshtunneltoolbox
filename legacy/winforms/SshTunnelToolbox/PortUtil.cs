using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace SshTunnelToolbox;

public static class PortUtil
{
	private struct ROW
	{
		public uint state;

		public uint localAddr;

		public uint localPort;

		public uint remoteAddr;

		public uint remotePort;

		public uint owningPid;
	}

	public class Listener
	{
		public IPAddress Address;

		public int Port;

		public int Pid;
	}

	[DllImport("iphlpapi.dll", SetLastError = true)]
	private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwOutBufLen, bool sort, int ipVersion, int tblClass, uint reserved);

	public static List<Listener> Listeners()
	{
		List<Listener> list = new List<Listener>();
		int dwOutBufLen = 0;
		GetExtendedTcpTable(IntPtr.Zero, ref dwOutBufLen, sort: false, 2, 3, 0u);
		if (dwOutBufLen <= 0)
		{
			return list;
		}
		IntPtr intPtr = Marshal.AllocHGlobal(dwOutBufLen);
		try
		{
			if (GetExtendedTcpTable(intPtr, ref dwOutBufLen, sort: false, 2, 3, 0u) != 0)
			{
				return list;
			}
			int num = Marshal.ReadInt32(intPtr);
			int num2 = Marshal.SizeOf(typeof(ROW));
			for (int i = 0; i < num; i++)
			{
				ROW rOW = (ROW)Marshal.PtrToStructure(new IntPtr(intPtr.ToInt64() + 4 + i * num2), typeof(ROW));
				int port = (int)(((rOW.localPort & 0xFF) << 8) | ((rOW.localPort >> 8) & 0xFF));
				list.Add(new Listener
				{
					Address = new IPAddress(rOW.localAddr),
					Port = port,
					Pid = (int)rOW.owningPid
				});
			}
			return list;
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}
	}

	public static string ProcessName(int pid)
	{
		try
		{
			using Process process = Process.GetProcessById(pid);
			return process.ProcessName + ".exe";
		}
		catch
		{
			return "?";
		}
	}

	public static Listener FindListener(string addr, int port)
	{
		if (!IPAddress.TryParse(string.IsNullOrWhiteSpace(addr) ? "127.0.0.1" : addr.Trim(), out IPAddress address))
		{
			address = IPAddress.Loopback;
		}
		try
		{
			foreach (Listener item in Listeners())
			{
				if (item.Port == port && (item.Address.Equals(address) || item.Address.Equals(IPAddress.Any) || address.Equals(IPAddress.Any)))
				{
					return item;
				}
			}
		}
		catch
		{
		}
		return null;
	}

	public static bool TryConnect(string addr, int port, int timeoutMs, out string error)
	{
		error = null;
		string host = ((string.IsNullOrWhiteSpace(addr) || addr.Trim() == "0.0.0.0") ? "127.0.0.1" : addr.Trim());
		try
		{
			using TcpClient tcpClient = new TcpClient(AddressFamily.InterNetwork);
			IAsyncResult asyncResult = tcpClient.BeginConnect(host, port, null, null);
			if (!asyncResult.AsyncWaitHandle.WaitOne(timeoutMs))
			{
				error = "超时";
				return false;
			}
			tcpClient.EndConnect(asyncResult);
			return true;
		}
		catch (SocketException ex)
		{
			error = ((ex.SocketErrorCode == SocketError.ConnectionRefused) ? "连接被拒绝（端口未监听）" : ex.Message);
			return false;
		}
		catch (Exception ex2)
		{
			error = ex2.Message;
			return false;
		}
	}
}
