using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Loader of configuration files
	/// </summary>
	static class ConfigFileLoader
	{
		static List<string> LoadedFiles = new List<string>();
		public static List<KeyValuePair<string, string>> RawEntries = new List<KeyValuePair<string, string>>();
		public static List<ConfigFileSection> RawSections = new List<ConfigFileSection>();

		/// <summary>
		/// Add all lines from this configuration file onto RawEntries
		/// </summary>
		/// <param name="Body">Array of configuration file lines</param>
		/// <param name="FileName">User-friendly file name</param>
		public static void LoadConfigFileContent(string[] Body, string FileName)
		{
			Console.WriteLine("Using configuration file {0}.", FileName.Replace(@"\\", @"\").Replace("//", "/"));

			for (int i = 0; i < Body.Length; i++)
			{
				string LineNo = FileName + ", line " + (i + 1);
				string Line = Body[i].TrimStart().TrimEnd();

				try
				{

					if (Line.StartsWith(";")) continue; //skip comments
					if (Line.Length == 0) continue; //skip empty lines
					if (Line.StartsWith("[Include:"))
					{
						LoadInclude(Line);
						continue;
					}

					RawEntries.Add(new KeyValuePair<string, string>(LineNo, Line));
				}
				catch (Exception ex)
				{
					Log.WriteLine(true, false, "Error in {0}: {1} Line ignored.", LineNo, ex.Message);
				}
			}
		}

		/// <summary>
		/// Process "[Include:path/filename.ext]" directive.
		/// </summary>
		/// <param name="IncludeString">"[Include:path/filename.ext]"-formatted string</param>
		public static void LoadInclude(string IncludeString)
		{
			if (!(IncludeString.StartsWith("[Include:") && IncludeString.EndsWith("]"))) throw new Exception("Incorrect include section.");

			int Start = "[Include:".Length;
			int Length = IncludeString.Length - 1 - Start;

			string Includable = IncludeString.Substring(Start, Length);

			//get %WOConfigDir% value
			string DefaultConfigDir = ".";
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
					string WinDefaultConfigDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\WebOne\";
					string WinUserDefaultConfigDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WebOne\";
					if (Directory.Exists(WinDefaultConfigDir)) DefaultConfigDir = WinDefaultConfigDir;
					if (Directory.Exists(WinUserDefaultConfigDir)) DefaultConfigDir = WinUserDefaultConfigDir;
					break;
				case PlatformID.Unix:
					string LinuxDefaultConfigDir = "/etc/webone.conf.d/";
					string LinuxUserDefaultConfigDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/webone/";
					string MacOSDefaultConfigDir = "/Library/Application Support/WebOne/";
					string MacOSUserDefaultConfigDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Library/Application Support/WebOne/";
					if (Directory.Exists(LinuxDefaultConfigDir)) DefaultConfigDir = LinuxDefaultConfigDir;
					if (Directory.Exists(LinuxUserDefaultConfigDir)) DefaultConfigDir = LinuxUserDefaultConfigDir;
					if (Directory.Exists(MacOSDefaultConfigDir)) DefaultConfigDir = MacOSDefaultConfigDir;
					if (Directory.Exists(MacOSUserDefaultConfigDir)) DefaultConfigDir = MacOSUserDefaultConfigDir;
					break;
			}
			if (ConfigFileName.StartsWith("./") || ConfigFileName.StartsWith(@".\")) DefaultConfigDir = ".";
			if (!string.IsNullOrEmpty(CustomConfigFile)) DefaultConfigDir = new FileInfo(CustomConfigFile).DirectoryName;
			Includable = Includable.Replace("%WOConfigDir%", DefaultConfigDir);
			Variables["WOConfigDir"] = DefaultConfigDir;

			if (Includable.Contains('*') || Includable.Contains('?')) //it's a file mask
			{
				int MaskStart = Includable.LastIndexOf(Path.DirectorySeparatorChar);
				if (MaskStart < 0) MaskStart = Includable.LastIndexOf(Path.AltDirectorySeparatorChar);
				if (MaskStart > 0) // "/etc/webone.conf.d/*.conf" or even "C:/program files/webone/myconf/*.conf"
					LoadDirectory(Includable.Substring(0, MaskStart), Includable.Substring(MaskStart + 1));
				else // simply "*.conf"
					LoadDirectory(".", Includable);
			}
			else if (File.GetAttributes(Includable).HasFlag(FileAttributes.Directory)) LoadDirectory(Includable); else LoadFile(Includable);
		}

		/// <summary>
		/// Load all entries from all configuration files in the directory.
		/// </summary>
		/// <param name="PathMask">Path to the directory and filename mask.</param>
		public static void LoadDirectory(string Path, string Mask = "*.*")
		{
			if (!Directory.Exists(Path)) throw new DirectoryNotFoundException();

			foreach (string f in Directory.GetFiles(Path, Mask))
			{
				try { LoadFile(f); }
				catch (Exception e) { throw new Exception(string.Format("Can't load included {0}: {1}", f, e.Message)); };
			}
		}

		/// <summary>
		/// Load all entries from this configuration file.
		/// </summary>
		/// <param name="Path">Path of the configuration file.</param>
		public static void LoadFile(string Path)
		{
			Path = ExpandMaskedVariables(Path);
			if (!File.Exists(Path)) throw new FileNotFoundException();

			string FullPath = new FileInfo(Path).FullName;
			if (LoadedFiles.Contains(FullPath)) return; //prevent infinite loops
			LoadedFiles.Add(FullPath);

			string ShortFileName = Path;
			if (Path.StartsWith(@"./")) ShortFileName = Path[2..];
			if (Path.StartsWith(@".\")) ShortFileName = Path[2..];
			LoadConfigFileContent(File.ReadAllLines(Path), ShortFileName);
		}

		/// <summary>
		/// Parse loaded configuration files (RawEntries) and load them to ConfigFile properties
		/// </summary>
		public static void ProcessConfiguration()
		{
			foreach (KeyValuePair<string, string> entry in RawEntries)
			{
				if (entry.Value.StartsWith('['))
				{
					//section title
					RawSections.Add(new ConfigFileSection(entry.Value, entry.Key));
				}
				else
				{
					//section value
					if (RawSections.Count > 0)
					{
						RawSections[RawSections.Count - 1].Options.Add(new ConfigFileOption(entry.Value, entry.Key));
					}
					else throw new Exception("Found option outside section.");
				}
			}

			foreach (var Section in RawSections)
			{
				switch (Section.Kind)
				{
					case "Server":
						foreach (ConfigFileOption Option in Section.Options)
						{
							switch (Option.Key)
							{
								case "Port":
									ConfigFile.Port = Convert.ToInt32(Option.Value);
									break;
								case "Port2":
								case "HttpPort":
								case "HttpsPort":
								case "FtpPort":
									Log.WriteLine(true, false, "Warning: Use of '{0}' is deprecated, use 'Port' at {1}.", Option.Key, Option.Location);
									break;
								case "OutputEncoding":
									ConfigFile.OutputEncoding = GetCodePage(Option.Value);
									break;
								case "Authenticate":
									ConfigFile.Authenticate.Add(Option.Value);
									break;
								case "AuthenticateMessage":
									ConfigFile.AuthenticateMessage = Option.Value;
									break;
								case "AuthenticateRealm":
									ConfigFile.AuthenticateRealm = Option.Value;
									break;
								case "HideClientErrors":
									ConfigFile.HideClientErrors = ToBoolean(Option.Value);
									break;
								case "SearchInArchive":
									ConfigFile.SearchInArchive = ToBoolean(Option.Value);
									break;
								case "HideArchiveRedirect":
									ConfigFile.HideArchiveRedirect = ToBoolean(Option.Value);
									break;
								case "ShortenArchiveErrors":
									ConfigFile.ShortenArchiveErrors = ToBoolean(Option.Value);
									break;
								case "ArchiveUrlSuffix":
									ConfigFile.ArchiveUrlSuffix = Option.Value;
									break;
								case "SecurityProtocols":
									SecurityProtocolType spt = SecurityProtocolType.SystemDefault;
									if (!Enum.TryParse(Option.Value, out spt))
									{
										Log.WriteLine(true, false, "Warning: Bad TLS version {1} ({0}), using {2} ({2:D}).", Option.Value, spt, ServicePointManager.SecurityProtocol);
									}
									ServicePointManager.SecurityProtocol = spt;
									break;
								case "UserAgent":
									ConfigFile.UserAgent = Option.Value;
									break;
								case "DefaultHostName":
									ConfigFile.DefaultHostName = Option.Value.Replace("%HostName%", Environment.MachineName);
									bool ValidHostName = (Environment.MachineName.ToLower() == ConfigFile.DefaultHostName.ToLower());
									if (!ValidHostName) foreach (System.Net.IPAddress LocIP in Program.GetLocalIPAddresses())
										{ if (LocIP.ToString() == ConfigFile.DefaultHostName) ValidHostName = true; }
									if (!ValidHostName)
									{ try { if (System.Net.Dns.GetHostEntry(ConfigFile.DefaultHostName).AddressList.Count() > 0) ValidHostName = true; } catch { } }
									if (!ValidHostName) Log.WriteLine(true, false, "Warning: DefaultHostName setting is not applicable to this computer!");
									break;
								case "ValidateCertificates":
									ConfigFile.ValidateCertificates = ToBoolean(Option.Value);
									break;
								case "TemporaryDirectory":
									if (Option.Value.ToUpper() == "%TEMP%" || Option.Value == "$TEMP" || Option.Value == "$TMPDIR") ConfigFile.TemporaryDirectory = Path.GetTempPath();
									else ConfigFile.TemporaryDirectory = Option.Value;
									break;
								case "LogFile":
									if (Program.OverrideLogFile != null && Program.OverrideLogFile == "")
										LogAgent.OpenLogFile(Program.GetLogFilePath(Option.Value.Replace(@"\\", @"\").Replace("//", "/")), false);
									break;
								case "AppendLogFile":
									if (Program.OverrideLogFile != null && Program.OverrideLogFile == "")
										LogAgent.OpenLogFile(Program.GetLogFilePath(Option.Value.Replace(@"\\", @"\").Replace("//", "/")), true);
									break;
								case "DisplayStatusPage":
									ConfigFile.DisplayStatusPage = Option.Value;
									break;
								case "ArchiveDateLimit":
									int ArchiveDateLimit = 0;
									int.TryParse(Option.Value, out ArchiveDateLimit);
									if (ArchiveDateLimit > 0)
									{
										if (ArchiveDateLimit > 10000000 && ArchiveDateLimit < 99990000)
										{
											ConfigFile.ArchiveDateLimit = ArchiveDateLimit;
											break;
										}
									}
									Log.WriteLine(true, false, "Warning: The ArchiveDateLimit must be in YYYYMMDD format.");
									break;
								case "UpperProxy":
									ConfigFile.UpperProxy = Option.Value;
									break;
								case "PageStyleHtml":
									ConfigFile.PageStyleHtml = Option.Value;
									break;
								case "MultipleHttp2Connections":
									ConfigFile.MultipleHttp2Connections = ToBoolean(Option.Value);
									break;
								case "RemoteHttpVersion":
									if (System.Text.RegularExpressions.Regex.IsMatch(Option.Value, @"([=><a])[u0-3][t\.][o0-9]"))
									{ ConfigFile.RemoteHttpVersion = Option.Value; }
									else
									{ Log.WriteLine(true, false, "Warning: Incorrect RemoteHttpVersion '{0}'.", Option.Value); }
									break;
								case "AllowHttpCompression":
									ConfigFile.AllowHttpCompression = ToBoolean(Option.Value);
									break;
								case "EnableWebFtp":
									ConfigFile.EnableWebFtp = ToBoolean(Option.Value);
									break;
								case "UseMsHttpApi":
									ConfigFile.UseMsHttpApi = ToBoolean(Option.Value);
									break;
								case "EnableManualConverting":
									ConfigFile.EnableManualConverting = ToBoolean(Option.Value);
									break;
								case "ContentDirectory":
									if (Directory.Exists(Option.Value))
										ConfigFile.ContentDirectory = Option.Value;
									else
										Log.WriteLine(true, false, "Warning: Incorrect ContentDirectory '{0}'.", Option.Value);
									break;
								default:
									Log.WriteLine(true, false, "Warning: Unknown server option {0} in {1}.", Option.Key, Option.Location);
									break;
							}
						}
						break;
					case "ForceHttps":
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.ForceHttps.Add(Line.RawString);
						}
						break;
					case "TextTypes":
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.TextTypes.Add(Line.RawString);
						}
						break;
					case "ForceUtf8":
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.ForceUtf8.Add(Line.RawString);
						}
						break;
					case "Converters":
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.Converters.Add(new Converter(Line.RawString));
						}
						break;
					case "Edit":
						ConfigFile.EditRules.Add(new EditSet(Section));
						break;
					case "Translit":
						foreach (ConfigFileOption Line in Section.Options)
						{
							if (Line.HaveKeyValue) ConfigFile.TranslitTable.Add(new KeyValuePair<string, string>(Line.Key, Line.Value));
						}
						break;
					case "IpBanList":
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.IpBanList.Add(Line.RawString);
						}
						break;
					case "IpWhiteList":
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.IpWhiteList.Add(Line.RawString);
						}
						break;
					case "UrlBlackList":
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.UrlBlackList.Add(Line.RawString);
						}
						break;
					case "UrlWhiteList":
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.UrlWhiteList.Add(Line.RawString);
						}
						break;
					case "HostNames":
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.HostNames.Add(Line.RawString);
						}
						break;
					case "Authenticate":
						foreach (ConfigFileOption Line in Section.Options)
						{
							if (Line.RawString.Split(":").Length == 2 && !Line.RawString.Contains(" "))
								//login:password pair
								ConfigFile.Authenticate.Add(Line.RawString);
							else
							{
								//option name/value pair
								if (Line.HaveKeyValue)
								{
									switch (Line.Key)
									{
										case "AuthenticateMessage":
											ConfigFile.AuthenticateMessage = Line.Value;
											break;
										case "AuthenticateRealm":
											ConfigFile.AuthenticateRealm = Line.Value;
											break;
										case "OpenForLocalIPs":
											ConfigFile.OpenForLocalIPs = ToBoolean(Line.Value);
											break;
										default:
											Log.WriteLine(true, false, "Warning: Invalid authentication option at {0}.", Line.Location);
											break;
									}
								}
								else
								{
									Log.WriteLine(true, false, "Warning: Invalid authentication credentials at {0}.", Line.Location);
								}
							}
						}
						break;
					case "MimeTypes":
						foreach (ConfigFileOption Line in Section.Options)
						{
							if (Line.HaveKeyValue)
							{
								ConfigFile.MimeTypes.Add(Line.Key.ToLower(), Line.Value.ToLower());
							}
							else
							{
								Log.WriteLine(true, false, "Warning: Invalid content type definition at {0}.", Line.Location);
							}
						}
						break;
					case "PAC":
						ConfigFile.PAC = "";
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.PAC += Line.RawString + "\n";
						}
						break;
					case "PageStyleCss":
						ConfigFile.PageStyleCss = "";
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.PageStyleCss += Line.RawString + "\n";
						}
						break;
					case "WebVideoOptions":
						foreach (ConfigFileOption Line in Section.Options)
						{
							if (Line.HaveKeyValue) ConfigFile.WebVideoOptions[Line.Key] = Line.Value;
							else Log.WriteLine(true, false, "Warning: Incorrect online video convert option at {0}.", Line.Location);
						}
						break;
					case "SecureProxy":
						if (ConfigFile.UseMsHttpApi)
						{
							Log.WriteLine(true, false, "Info: [SecureProxy] options are ignored when UseMsHttpApi=1.");
							break;
						}
						foreach (ConfigFileOption Option in Section.Options)
						{
							switch (Option.Key)
							{
								case "SslEnable":
									ConfigFile.SslEnable = ToBoolean(Option.Value);
									break;
								case "SslCertificate":
									ConfigFile.SslCertificate = ExpandMaskedVariables(Option.Value).Replace(@"\\", @"\").Replace("//", "/");
									break;
								case "SslPrivateKey":
									ConfigFile.SslPrivateKey = ExpandMaskedVariables(Option.Value).Replace(@"\\", @"\").Replace("//", "/");
									break;
								case "SslProtocols":
									if (!Enum.TryParse(Option.Value, out ConfigFile.SslProtocols))
									{
										Log.WriteLine(true, false, "Warning: incorrect SSL/TLS protocol set '{0}', at {1}.", Option.Value, Option.Location);
									}
									break;
								case "SslCipherSuites":
									var Suites = Option.Value.Split(',');
									foreach (var SuiteName in Suites)
									{
										if (Enum.TryParse(SuiteName, out TlsCipherSuite Suite))
										{
											ConfigFile.SslCipherSuites.Add(Suite);
										}
										else
										{
											Log.WriteLine(true, false, "Warning: incorrect cipher suite '{0}', at {1}.", SuiteName.Trim(), Option.Location);
										}
									}

									try
									{
										ConfigFile.SslCipherSuitesPolicy = new CipherSuitesPolicy(ConfigFile.SslCipherSuites);
									}
									catch (PlatformNotSupportedException)
									{
										ConfigFile.SslCipherSuitesPolicy = null;
										Log.WriteLine(true, false, "Warning: Order of cipher suites cannot be overriden on this OS, at {0}.", Option.Location);
										//"Platform is not a Linux system with OpenSSL 1.1.1 or higher or a macOS."
										//https://learn.microsoft.com/en-us/dotnet/api/system.net.security.ciphersuitespolicy.-ctor?view=net-6.0#exceptions
									}
									break;
								case "SslHashAlgorithm":
									switch (Option.Value.ToUpper())
									{
										case "MD5":
											ConfigFile.SslHashAlgorithm = System.Security.Cryptography.HashAlgorithmName.MD5;
											break;
										case "SHA1":
											ConfigFile.SslHashAlgorithm = System.Security.Cryptography.HashAlgorithmName.SHA1;
											break;
										case "SHA2":
										case "SHA256":
											ConfigFile.SslHashAlgorithm = System.Security.Cryptography.HashAlgorithmName.SHA256;
											break;
										case "SHA3":
										case "SHA384":
											ConfigFile.SslHashAlgorithm = System.Security.Cryptography.HashAlgorithmName.SHA384;
											break;
										case "SHA5":
										case "SHA512":
											ConfigFile.SslHashAlgorithm = System.Security.Cryptography.HashAlgorithmName.SHA512;
											break;
										default:
											Log.WriteLine(true, false, "Warning: '{0}' is not a valid hash algorithm name, at {1}.", Option.Value, Option.Location);
											break;
									}
									break;
								case "SslRootSubject":
									if (!Option.Value.Contains("CN="))
										Log.WriteLine(true, false, "Warning: '{0}' is not a valid X.500 distinguished subject name, at {1}.", Option.Value, Option.Location);
									else
										ConfigFile.SslRootSubject = Option.Value;
									break;
								case "SslRootValidAfter":
									ConfigFile.SslRootValidAfter = ToDateTimeOffset(Option.Value);
									break;
								case "SslRootValidBefore":
									ConfigFile.SslRootValidBefore = ToDateTimeOffset(Option.Value);
									break;
								case "SslCertVaildBeforeNow":
									int SslCertVaildBeforeNow = int.Parse(Option.Value);
									if (SslCertVaildBeforeNow <= 0)
										ConfigFile.SslCertVaildBeforeNow = SslCertVaildBeforeNow;
									else
										Log.WriteLine(true, false, "Warning: SslCertVaildBeforeNow must be a negative whole number of days, at {0}.", Option.Location);
									break;
								case "SslCertVaildAfterNow":
									int SslCertVaildAfterNow = int.Parse(Option.Value);
									if (SslCertVaildAfterNow >= 1)
										ConfigFile.SslCertVaildAfterNow = SslCertVaildAfterNow;
									else
										Log.WriteLine(true, false, "Warning: SslCertVaildAfterNow must be a positive whole number of days, at {0}.", Option.Location);
									break;
								case "SslSiteCerts":
									ConfigFile.SslSiteCerts = ExpandMaskedVariables(Option.Value).Replace(@"\\", @"\").Replace("//", "/");
									break;
								case "SslSiteCertGenerator":
									ConfigFile.SslSiteCertGenerator = ExpandMaskedVariables(Option.Value).Replace(@"\\", @"\").Replace("//", "/");
									break;
								case "AllowNonHttpsCONNECT":
									ConfigFile.AllowNonHttpsCONNECT = ToBoolean(Option.Value);
									break;
							}
						}
						break;
					case "NonHttpSslServers":
						foreach (ConfigFileOption Line in Section.Options)
						{
							ConfigFile.NonHttpSslServers.Add(Line.RawString.ToLowerInvariant());
						}
						break;
					case "NonHttpConnectRedirect":
						foreach (ConfigFileOption Line in Section.Options)
						{
							if (Line.HaveKeyValue
							&& Line.Value.Split(':').Length == 2
							&& !string.IsNullOrWhiteSpace(Line.Value.Split(':')[0])
							&& int.TryParse(Line.Value.Split(':')[1], out int devnull))
							{
								ConfigFile.NonHttpConnectRedirect.Add(Line.Key, Line.Value);
							}
							else
							{
								Log.WriteLine(true, false, "Warning: Incorrect non-HTTP connection redirect rule at {0}.", Line.Location);
								continue;
							}
						}
						break;
					case "Http10Only":
						foreach (ConfigFileOption Line in Section.Options)
						{
							CheckRegExp(Line.RawString, Line.Location);
							ConfigFile.Http10Only.Add(Line.RawString);
						}
						break;
					case "FixableURL":
					case "FixableType":
					case "ContentPatch":
						Log.WriteLine(true, false, "Warning: {0} section at {1} is no longer supported.", Section.Kind, Section.Location);
						Log.WriteLine(true, false, "See https://github.com/atauenis/webone/wiki/Configuration-file about [{0}].", Section.Kind, Section.Location);
						break;
					default:
						Log.WriteLine(true, false, "Warning: Unknown section {0} in {1}.", Section.Kind, Section.Location);
						break;
				}
			}

			if (ConfigFile.PAC == "") ConfigFile.PAC = DefaultPAC;

			if (ConfigFile.SslRootValidAfter <= new DateTimeOffset())
			{ ConfigFile.SslRootValidAfter = new DateTimeOffset(1970, 01, 01, 00, 00, 00, new TimeSpan(0)); }

			if (ConfigFile.SslRootValidBefore <= new DateTimeOffset())
			{ ConfigFile.SslRootValidBefore = new DateTimeOffset(2070, 12, 31, 23, 59, 59, new TimeSpan(0)); }

			Variables.Add("Proxy", ConfigFile.DefaultHostName + ":" + ConfigFile.Port.ToString());
			Variables.Add("ProxyHost", ConfigFile.DefaultHostName);
			Variables.Add("ProxyPort", ConfigFile.Port.ToString());

			Console.WriteLine("Configuration load complete.");
			foreach (string f in LoadedFiles) { Log.WriteLine(false, false, "Configuration file {0} load complete.", f); }
		}
	}
}
