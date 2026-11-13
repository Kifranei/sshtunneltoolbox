using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace SshTunnelToolbox;

[DataContract]
public class AppConfig
{
	public const int CurrentVersion = 2;

	public const string DefaultPlink = "C:\\Program Files\\PuTTY\\plink.exe";

	public const string CloseAsk = "ask";

	public const string CloseTray = "tray";

	public const string CloseExit = "exit";

	[DataMember(Name = "host", Order = 20, EmitDefaultValue = false)]
	private string V1Host;

	[DataMember(Name = "port", Order = 21, EmitDefaultValue = false)]
	private int? V1Port;

	[DataMember(Name = "user", Order = 22, EmitDefaultValue = false)]
	private string V1User;

	[DataMember(Name = "passwordProtected", Order = 23, EmitDefaultValue = false)]
	private string V1PasswordProtected;

	[DataMember(Name = "hostKey", Order = 24, EmitDefaultValue = false)]
	private string V1HostKey;

	[DataMember(Name = "reconnectDelaySeconds", Order = 25, EmitDefaultValue = false)]
	private int? V1Delay;

	[DataMember(Name = "autoConnect", Order = 26, EmitDefaultValue = false)]
	private bool? V1AutoConnect;

	[DataMember(Name = "verbose", Order = 27, EmitDefaultValue = false)]
	private bool? V1Verbose;

	[DataMember(Name = "forwards", Order = 28, EmitDefaultValue = false)]
	private List<Forward> V1Forwards;

	[DataMember(Name = "version", Order = 1)]
	public int Version { get; set; }

	[DataMember(Name = "plinkPath", Order = 2)]
	public string PlinkPath { get; set; }

	[DataMember(Name = "closeAction", Order = 3)]
	public string CloseAction { get; set; }

	[DataMember(Name = "minimizeToTray", Order = 4)]
	public bool MinimizeToTray { get; set; }

	[DataMember(Name = "startHidden", Order = 5)]
	public bool StartHidden { get; set; }

	[DataMember(Name = "balloons", Order = 6)]
	public bool Balloons { get; set; }

	[DataMember(Name = "selectedProfile", Order = 7)]
	public int SelectedProfile { get; set; }

	[DataMember(Name = "profiles", Order = 8)]
	public List<Profile> Profiles { get; set; }

	/// <summary>界面主题：system（跟随系统，默认）/ light / dark。2.0.0 新增，1.0.0 读取时会忽略该字段。</summary>
	[DataMember(Name = "theme", Order = 9, EmitDefaultValue = false)]
	public string Theme { get; set; }

	private bool HasV1Data
	{
		get
		{
			if (V1Host == null && V1User == null && V1Forwards == null)
			{
				return V1PasswordProtected != null;
			}
			return true;
		}
	}

	public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SshTunnelToolbox");

	public static string FilePath => Path.Combine(Dir, "config.json");

	public static string V1BackupPath => Path.Combine(Dir, "config.v1.bak");

	public AppConfig()
	{
		SetDefaults();
	}

	private void SetDefaults()
	{
		Version = 2;
		PlinkPath = "C:\\Program Files\\PuTTY\\plink.exe";
		CloseAction = "tray";
		MinimizeToTray = true;
		StartHidden = false;
		Balloons = true;
		SelectedProfile = 0;
		Profiles = new List<Profile>();
		Theme = "system";
	}

	[OnDeserializing]
	private void OnDeserializing(StreamingContext ctx)
	{
		SetDefaults();
		Version = 0;
		Profiles = null;
	}

	public static AppConfig Defaults()
	{
		return new AppConfig
		{
			Profiles = { Profile.DefaultEmpty() }
		};
	}

	private bool MigrateV1()
	{
		if (Profiles != null && Profiles.Count > 0)
		{
			ClearV1();
			return false;
		}
		if (!HasV1Data)
		{
			return false;
		}
		Profile item = new Profile
		{
			Name = "默认配置",
			Host = (V1Host ?? ""),
			Port = (V1Port ?? 22),
			User = (V1User ?? ""),
			PasswordProtected = (V1PasswordProtected ?? ""),
			HostKey = (V1HostKey ?? ""),
			ReconnectDelaySeconds = (V1Delay ?? 5),
			AutoConnect = (V1AutoConnect == true),
			Verbose = (V1Verbose == true),
			Forwards = (V1Forwards ?? new List<Forward>())
		};
		Profiles = new List<Profile> { item };
		ClearV1();
		return true;
	}

	private void ClearV1()
	{
		V1Host = null;
		V1Port = null;
		V1User = null;
		V1PasswordProtected = null;
		V1HostKey = null;
		V1Delay = null;
		V1AutoConnect = null;
		V1Verbose = null;
		V1Forwards = null;
	}

	private void Normalize()
	{
		if (string.IsNullOrWhiteSpace(PlinkPath))
		{
			PlinkPath = "C:\\Program Files\\PuTTY\\plink.exe";
		}
		if (CloseAction != "ask" && CloseAction != "tray" && CloseAction != "exit")
		{
			CloseAction = "tray";
		}
		if (Theme != "light" && Theme != "dark")
		{
			Theme = "system";
		}
		if (Profiles == null)
		{
			Profiles = new List<Profile>();
		}
		Profiles.RemoveAll((Profile p) => p == null);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		int num = 1;
		foreach (Profile profile in Profiles)
		{
			profile.Normalize();
			if (string.IsNullOrWhiteSpace(profile.Name))
			{
				profile.Name = "配置" + num;
			}
			string text = profile.Name.Trim();
			string text2 = text;
			int num2 = 2;
			while (!hashSet.Add(text2))
			{
				text2 = text + " (" + num2++ + ")";
			}
			profile.Name = text2;
			num++;
		}
		if (Profiles.Count == 0)
		{
			Profiles.Add(Profile.DefaultEmpty());
		}
		if (SelectedProfile < 0 || SelectedProfile >= Profiles.Count)
		{
			SelectedProfile = 0;
		}
	}

	public static AppConfig Load(out string warning)
	{
		return LoadFrom(FilePath, out warning);
	}

	public static AppConfig LoadFrom(string path, out string warning)
	{
		warning = null;
		try
		{
			if (File.Exists(path))
			{
				AppConfig appConfig;
				using (FileStream fileStream = File.OpenRead(path))
				{
					appConfig = (AppConfig)((XmlObjectSerializer)new DataContractJsonSerializer(typeof(AppConfig))).ReadObject((Stream)fileStream);
				}
				if (appConfig != null)
				{
					bool flag = appConfig.Version < 2 && appConfig.MigrateV1();
					appConfig.Normalize();
					if (flag || appConfig.Version < 2)
					{
						string text = Path.Combine(Path.GetDirectoryName(path), "config.v1.bak");
						try
						{
							if (!File.Exists(text))
							{
								File.Copy(path, text);
							}
						}
						catch
						{
						}
						appConfig.Version = 2;
						try
						{
							appConfig.SaveTo(path);
							if (flag)
							{
								warning = "已把旧版配置迁移为配置「" + appConfig.Profiles[0].Name + "」（密码保留；旧文件备份为 config.v1.bak）";
							}
						}
						catch (Exception ex)
						{
							warning = "旧版配置已读入，但保存新格式失败：" + ex.Message;
						}
					}
					foreach (Profile profile in appConfig.Profiles)
					{
						if (!string.IsNullOrEmpty(profile.PasswordProtected) && profile.GetPassword() == "")
						{
							warning = ((warning == null) ? "" : (warning + "\n")) + "配置「" + profile.Name + "」保存的密码无法解密（可能是换了 Windows 用户），请重新输入密码并保存。";
						}
						if (!string.IsNullOrEmpty(profile.JumpPasswordProtected) && profile.GetJumpPassword() == "")
						{
							warning = ((warning == null) ? "" : (warning + "\n")) + "配置「" + profile.Name + "」保存的跳板机密码无法解密，请重新输入并保存。";
						}
					}
					return appConfig;
				}
			}
		}
		catch (Exception ex2)
		{
			warning = "配置文件读取失败，已使用默认配置：" + ex2.Message;
			try
			{
				File.Copy(path, path + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: true);
			}
			catch
			{
			}
		}
		return Defaults();
	}

	public void Save()
	{
		SaveTo(FilePath);
	}

	public void SaveTo(string path)
	{
		Version = 2;
		ClearV1();
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		string text = path + ".tmp";
		using (FileStream fileStream = new FileStream(text, FileMode.Create, FileAccess.Write))
		{
			XmlDictionaryWriter val = JsonReaderWriterFactory.CreateJsonWriter((Stream)fileStream, (Encoding)new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), false, true, "  ");
			try
			{
				((XmlObjectSerializer)new DataContractJsonSerializer(typeof(AppConfig))).WriteObject(val, (object)this);
				((XmlWriter)val).Flush();
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		if (File.Exists(path))
		{
			File.Delete(path);
		}
		File.Move(text, path);
	}
}
