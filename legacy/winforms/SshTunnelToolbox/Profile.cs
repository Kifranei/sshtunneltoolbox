using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace SshTunnelToolbox;

[DataContract]
public class Profile
{
	private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SshTunnelToolbox.v1");

	[DataMember(Name = "name", Order = 1)]
	public string Name { get; set; }

	[DataMember(Name = "host", Order = 2)]
	public string Host { get; set; }

	[DataMember(Name = "port", Order = 3)]
	public int Port { get; set; }

	[DataMember(Name = "user", Order = 4)]
	public string User { get; set; }

	[DataMember(Name = "passwordProtected", Order = 5)]
	public string PasswordProtected { get; set; }

	[DataMember(Name = "hostKey", Order = 6)]
	public string HostKey { get; set; }

	[DataMember(Name = "reconnectDelaySeconds", Order = 7)]
	public int ReconnectDelaySeconds { get; set; }

	[DataMember(Name = "autoConnect", Order = 8)]
	public bool AutoConnect { get; set; }

	[DataMember(Name = "verbose", Order = 9)]
	public bool Verbose { get; set; }

	[DataMember(Name = "forwards", Order = 10)]
	public List<Forward> Forwards { get; set; }

	// ---- 跳板机（ProxyJump，相当于 ssh -J）；旧配置没有这些字段时按“不使用跳板机”处理 ----
	[DataMember(Name = "useJump", Order = 11, IsRequired = false, EmitDefaultValue = true)]
	public bool UseJump { get; set; }

	[DataMember(Name = "jumpHost", Order = 12, IsRequired = false)]
	public string JumpHost { get; set; }

	[DataMember(Name = "jumpPort", Order = 13, IsRequired = false)]
	public int JumpPort { get; set; }

	[DataMember(Name = "jumpUser", Order = 14, IsRequired = false)]
	public string JumpUser { get; set; }

	[DataMember(Name = "jumpPasswordProtected", Order = 15, IsRequired = false)]
	public string JumpPasswordProtected { get; set; }

	[DataMember(Name = "jumpHostKey", Order = 16, IsRequired = false)]
	public string JumpHostKey { get; set; }

	public bool JumpActive => UseJump && !string.IsNullOrWhiteSpace(JumpHost);

	public Profile()
	{
		Name = "";
		Host = "";
		Port = 22;
		User = "";
		PasswordProtected = "";
		HostKey = "";
		ReconnectDelaySeconds = 5;
		Forwards = new List<Forward>();
		JumpHost = "";
		JumpPort = 22;
		JumpUser = "";
		JumpPasswordProtected = "";
		JumpHostKey = "";
	}

	public Profile Clone()
	{
		Profile obj = (Profile)MemberwiseClone();
		obj.Forwards = (Forwards ?? new List<Forward>()).Select((Forward f) => f.Clone()).ToList();
		return obj;
	}

	public void Normalize()
	{
		if (Name == null)
		{
			Name = "";
		}
		if (Host == null)
		{
			Host = "";
		}
		if (User == null)
		{
			User = "";
		}
		if (HostKey == null)
		{
			HostKey = "";
		}
		if (PasswordProtected == null)
		{
			PasswordProtected = "";
		}
		if (Forwards == null)
		{
			Forwards = new List<Forward>();
		}
		if (Port <= 0 || Port > 65535)
		{
			Port = 22;
		}
		if (JumpHost == null)
		{
			JumpHost = "";
		}
		if (JumpUser == null)
		{
			JumpUser = "";
		}
		if (JumpPasswordProtected == null)
		{
			JumpPasswordProtected = "";
		}
		if (JumpHostKey == null)
		{
			JumpHostKey = "";
		}
		if (JumpPort <= 0 || JumpPort > 65535)
		{
			JumpPort = 22;
		}
		if (ReconnectDelaySeconds <= 0)
		{
			ReconnectDelaySeconds = 5;
		}
	}

	public string GetPassword()
	{
		if (string.IsNullOrEmpty(PasswordProtected))
		{
			return "";
		}
		try
		{
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(PasswordProtected), Entropy, (DataProtectionScope)0);
			return Encoding.UTF8.GetString(bytes);
		}
		catch
		{
			return "";
		}
	}

	public void SetPassword(string pw)
	{
		if (string.IsNullOrEmpty(pw))
		{
			PasswordProtected = "";
			return;
		}
		byte[] inArray = ProtectedData.Protect(Encoding.UTF8.GetBytes(pw), Entropy, (DataProtectionScope)0);
		PasswordProtected = Convert.ToBase64String(inArray);
	}

	public string GetJumpPassword()
	{
		if (string.IsNullOrEmpty(JumpPasswordProtected))
		{
			return "";
		}
		try
		{
			byte[] bytes = ProtectedData.Unprotect(Convert.FromBase64String(JumpPasswordProtected), Entropy, (DataProtectionScope)0);
			return Encoding.UTF8.GetString(bytes);
		}
		catch
		{
			return "";
		}
	}

	public void SetJumpPassword(string pw)
	{
		if (string.IsNullOrEmpty(pw))
		{
			JumpPasswordProtected = "";
			return;
		}
		byte[] inArray = ProtectedData.Protect(Encoding.UTF8.GetBytes(pw), Entropy, (DataProtectionScope)0);
		JumpPasswordProtected = Convert.ToBase64String(inArray);
	}

	public string Describe()
	{
		string text = User + "@" + Host + ":" + Port;
		if (JumpActive)
		{
			text = text + "（经跳板机 " + JumpUser + "@" + JumpHost + ":" + JumpPort + "）";
		}
		return text;
	}

	public static Profile DefaultEmpty()
	{
		return new Profile
		{
			Name = "默认配置",
			Port = 22,
			ReconnectDelaySeconds = 5
		};
	}
}
