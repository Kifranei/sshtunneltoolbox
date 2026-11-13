using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SshTunnelToolbox;

public sealed class KillOnCloseJob : IDisposable
{
	private struct BASIC
	{
		public long PerProcessUserTimeLimit;

		public long PerJobUserTimeLimit;

		public uint LimitFlags;

		public UIntPtr MinimumWorkingSetSize;

		public UIntPtr MaximumWorkingSetSize;

		public uint ActiveProcessLimit;

		public UIntPtr Affinity;

		public uint PriorityClass;

		public uint SchedulingClass;
	}

	private struct IO
	{
		public ulong a;

		public ulong b;

		public ulong c;

		public ulong d;

		public ulong e;

		public ulong f;
	}

	private struct EXTENDED
	{
		public BASIC Basic;

		public IO Io;

		public UIntPtr ProcessMemoryLimit;

		public UIntPtr JobMemoryLimit;

		public UIntPtr PeakProcessMemoryUsed;

		public UIntPtr PeakJobMemoryUsed;
	}

	private IntPtr _h;

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern IntPtr CreateJobObject(IntPtr a, string name);

	[DllImport("kernel32.dll")]
	private static extern bool SetInformationJobObject(IntPtr job, int infoClass, IntPtr info, uint len);

	[DllImport("kernel32.dll", SetLastError = true)]
	private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(IntPtr h);

	public KillOnCloseJob()
	{
		_h = CreateJobObject(IntPtr.Zero, null);
		if (_h == IntPtr.Zero)
		{
			return;
		}
		EXTENDED structure = new EXTENDED
		{
			Basic = 
			{
				LimitFlags = 8192u
			}
		};
		int num = Marshal.SizeOf(typeof(EXTENDED));
		IntPtr intPtr = Marshal.AllocHGlobal(num);
		try
		{
			Marshal.StructureToPtr(structure, intPtr, fDeleteOld: false);
			SetInformationJobObject(_h, 9, intPtr, (uint)num);
		}
		finally
		{
			Marshal.FreeHGlobal(intPtr);
		}
	}

	public bool Assign(Process p)
	{
		if (_h == IntPtr.Zero)
		{
			return false;
		}
		try
		{
			return AssignProcessToJobObject(_h, p.Handle);
		}
		catch
		{
			return false;
		}
	}

	public void Dispose()
	{
		if (_h != IntPtr.Zero)
		{
			CloseHandle(_h);
			_h = IntPtr.Zero;
		}
	}
}
