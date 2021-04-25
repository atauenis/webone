using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
		/// Add all lines from this file onto RawEntries
		/// </summary>
		/// <param name="Body">This configuration file body</param>
		/// <param name="FileName">This configuration file name</param>
		public static void LoadConfigFileContent(string[] Body, string FileName)
		{
			Console.WriteLine("Using configuration file {0}.", FileName);

			for(int i = 0; i<Body.Length; i++)
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
				catch(Exception ex)
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
					if (Directory.Exists(@"C:\ProgramData\WebOne\")) DefaultConfigDir = @"C:\ProgramData\WebOne\";
					break;
				case PlatformID.Unix:
					if (Directory.Exists(@"/etc/webone.conf.d/")) DefaultConfigDir = @"/etc/webone.conf.d/";
					break;
					//may be rewritten to a separate function, see Program.GetConfigurationFileName() code
			}
			Includable = Includable.Replace("%WOConfigDir%", DefaultConfigDir);

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

			foreach(string f in Directory.GetFiles(Path, Mask)){
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
			if (!File.Exists(Path)) throw new FileNotFoundException();

			string FullPath = new FileInfo(Path).FullName;
			if (LoadedFiles.Contains(FullPath)) return; //prevent infinite loops
			LoadedFiles.Add(FullPath);

			string ShortFileName = new FileInfo(Path).Name;
			LoadConfigFileContent(File.ReadAllLines(Path), ShortFileName);
		}

		/// <summary>
		/// Parse loaded configuration files (RawEntries) and load them to ConfigFile properties
		/// </summary>
		public static void ProcessConfiguration()
		{
			foreach(KeyValuePair<string, string> entry in RawEntries){
				if(entry.Value.StartsWith('['))
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

			foreach(var Section in RawSections)
			{
				switch(Section.Kind)
				{
					case "Server":
						foreach(ConfigFileOption Option in Section.Options)
						{
							switch(Option.Key)
							{
								case "Port":
									ConfigFile.Port = Convert.ToInt32(Option.Value);
									break;
								case "OutputEncoding":
									if (Option.Value == "Windows" || Option.Value == "Win" || Option.Value == "ANSI")
									{
										ConfigFile.OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
										continue;
									}
									else if (Option.Value == "DOS" || Option.Value == "OEM")
									{
										ConfigFile.OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
										continue;
									}
									else if (Option.Value == "Mac" || Option.Value == "Apple")
									{
										ConfigFile.OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.MacCodePage);
										continue;
									}
									else if (Option.Value == "EBCDIC" || Option.Value == "IBM")
									{
										ConfigFile.OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.EBCDICCodePage);
										continue;
									}
									else if (Option.Value == "0" || Option.Value == "AsIs")
									{
										ConfigFile.OutputEncoding = null;
										continue;
									}
									else
									{
										try
										{
											ConfigFile.OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(Option.Value);
											if (ConfigFile.OutputEncoding == null)
												try { ConfigFile.OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(int.Parse(Option.Value)); } catch { }

											if (ConfigFile.OutputEncoding == null && Option.Value.ToLower().StartsWith("utf"))
											{
												switch (Option.Value.ToLower())
												{
													case "utf-7":
														ConfigFile.OutputEncoding = Encoding.UTF7;
														break;
													case "utf-8":
														ConfigFile.OutputEncoding = Encoding.UTF8;
														break;
													case "utf-16":
													case "utf-16le":
														ConfigFile.OutputEncoding = Encoding.Unicode;
														break;
													case "utf-16be":
														ConfigFile.OutputEncoding = Encoding.BigEndianUnicode;
														break;
													case "utf-32":
													case "utf-32le":
														ConfigFile.OutputEncoding = Encoding.UTF32;
														break;
												}
											}

											if (ConfigFile.OutputEncoding == null)
											{ Log.WriteLine(true, false, "Warning: Unknown codepage {0}, using AsIs. See MSDN 'Encoding.GetEncodings Method' article for list of valid encodings.", Option.Value); };
										}
										catch (ArgumentException) { Log.WriteLine(true, false, "Warning: Bad codepage {0}, using {1}. Get list of available encodings at http://{2}:{3}/!codepages/.", Option.Value, ConfigFile.OutputEncoding.EncodingName, ConfigFile.DefaultHostName, ConfigFile.Port); }
									}
									break;
								case "Authenticate":
									ConfigFile.Authenticate = Option.Value;
									break;
								case "HideClientErrors":
									ConfigFile.HideClientErrors = ToBoolean(Option.Value);
									break;
								case "SearchInArchive":
									ConfigFile.SearchInArchive = ToBoolean(Option.Value);
									break;
								case "ShortenArchiveErrors":
									ConfigFile.ShortenArchiveErrors = ToBoolean(Option.Value);
									break;
								case "SecurityProtocols":
									try { System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)(int.Parse(Option.Value)); }
									catch (NotSupportedException) { Log.WriteLine(true, false, "Warning: Bad TLS version {1} ({0}), using {2} ({2:D}).", Option.Value, (System.Net.SecurityProtocolType)(int.Parse(Option.Value)), System.Net.ServicePointManager.SecurityProtocol); };
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
									if(Program.OverrideLogFile != null && Program.OverrideLogFile == "")
										LogAgent.OpenLogFile(Program.GetLogFilePath(Option.Value), false);
									break;
								case "AppendLogFile":
									if (Program.OverrideLogFile != null && Program.OverrideLogFile == "")
										LogAgent.OpenLogFile(Program.GetLogFilePath(Option.Value), true);
									break;
								case "AllowConfigFileDisplay":
									ConfigFile.AllowConfigFileDisplay = ToBoolean(Option.Value);
									break;
								case "DisplayStatusPage":
									ConfigFile.DisplayStatusPage = Option.Value;
									break;
								default:
									Log.WriteLine(true, false, "Warning: Unknown server option {0} in {1}.", Option.Key, Option.Location);
									break;
							}
						}
						break;
					case "ForceHttps":
						foreach(ConfigFileOption Line in Section.Options)
						{
							ConfigFile.ForceHttps.Add(Line.RawString);
						}
						break;
					case "TextTypes":
						foreach(ConfigFileOption Line in Section.Options)
						{
							ConfigFile.TextTypes.Add(Line.RawString);
						}
						break;
					case "ForceUtf8":
						foreach(ConfigFileOption Line in Section.Options)
						{
							ConfigFile.ForceUtf8.Add(Line.RawString);
						}
						break;
					case "InternalRedirectOn":
						foreach(ConfigFileOption Line in Section.Options)
						{
							ConfigFile.InternalRedirectOn.Add(Line.RawString);
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

			Console.WriteLine("Configuration load complete.");
			foreach (string f in LoadedFiles) { Log.WriteLine(false, false, "Configuration file {0} load complete.", f); }
		}

		/// <summary>
		/// Convert string "true/false" or similar to bool true/false.
		/// </summary>
		/// <param name="s">One of these strings: 1/0, y/n, yes/no, on/off, enable/disable, true/false.</param>
		/// <returns>Boolean true/false</returns>
		/// <exception cref="InvalidCastException">Throws if the <paramref name="s"/> is not 1/0/y/n/yes/no/on/off/enable/disable/true/false.</exception>
		public static bool ToBoolean(this string s)
		{
			//from https://stackoverflow.com/posts/21864625/revisions
			string[] trueStrings = { "1", "y", "yes", "on", "enable", "true" };
			string[] falseStrings = { "0", "n", "no", "off", "disable", "false" };


			if (trueStrings.Contains(s, StringComparer.OrdinalIgnoreCase))
				return true;
			if (falseStrings.Contains(s, StringComparer.OrdinalIgnoreCase))
				return false;

			throw new InvalidCastException("only the following are supported for converting strings to boolean: "
				+ string.Join(",", trueStrings)
				+ " and "
				+ string.Join(",", falseStrings));
		}
	}
}
