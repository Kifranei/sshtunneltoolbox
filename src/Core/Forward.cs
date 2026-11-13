using System.Runtime.Serialization;

namespace SshTunnelToolbox;

[DataContract]
public class Forward
{
	[DataMember(Name = "note", Order = 1)]
	public string Note { get; set; }

	[DataMember(Name = "localAddress", Order = 2)]
	public string LocalAddress { get; set; }

	[DataMember(Name = "localPort", Order = 3)]
	public int LocalPort { get; set; }

	[DataMember(Name = "targetHost", Order = 4)]
	public string TargetHost { get; set; }

	[DataMember(Name = "targetPort", Order = 5)]
	public int TargetPort { get; set; }

	public string ClientAddress => ((string.IsNullOrWhiteSpace(LocalAddress) || LocalAddress.Trim() == "0.0.0.0") ? "127.0.0.1" : LocalAddress.Trim()) + ":" + LocalPort;

	public Forward()
	{
		LocalAddress = "127.0.0.1";
	}

	public Forward(string note, int lport, string thost, int tport)
	{
		Note = note;
		LocalAddress = "127.0.0.1";
		LocalPort = lport;
		TargetHost = thost;
		TargetPort = tport;
	}

	public Forward Clone()
	{
		return (Forward)MemberwiseClone();
	}

	public string ToArg()
	{
		string text = (string.IsNullOrWhiteSpace(LocalAddress) ? "127.0.0.1" : LocalAddress.Trim());
		string text2 = TargetHost.Trim();
		if (text2.Contains(":"))
		{
			text2 = "[" + text2 + "]";
		}
		return text + ":" + LocalPort + ":" + text2 + ":" + TargetPort;
	}
}
