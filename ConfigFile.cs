using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace WebOne
{
	/// <summary>
	/// Config file entries and parser
	/// </summary>
	static class ConfigFile
	{
		static string ConfigFileName = Program.ConfigFileName;
		static List<string> StringListConstructor = new List<string>();

		static string[] SpecialSections = { "ForceHttps", "TextTypes", "ForceUtf8", "InternalRedirectOn", "Converters" };

		/// <summary>
		/// TCP port that should be used by the Proxy Server
		/// </summary>
		public static int Port = 80;

		/// <summary>
		/// List of domains that should be open only using HTTPS
		/// </summary>
		public static string[] ForceHttps = { "www.phantom.sannata.org.example" };

		/// <summary>
		/// List of URLs that should be always downloaded as UTF-8
		/// </summary>
		public static string[] ForceUtf8 = { "yandex.ru.example" };

		/// <summary>
		/// List of parts of Content-Types that describing text files
		/// </summary>
		public static string[] TextTypes = { "text/", "javascript"};

		/// <summary>
		/// Encoding to be used in output content
		/// </summary>
		public static Encoding OutputEncoding = Encoding.Default;

		/// <summary>
		/// Credentials for proxy authentication
		/// </summary>
		public static string Authenticate = "";

		/// <summary>
		/// List of URLs that should be always 302ed
		/// </summary>
		public static List<string> FixableURLs = new List<string>();

		/// <summary>
		/// Dictionary of URLs that should be always 302ed if they're looks like too new JS frameworks
		/// </summary>
		public static Dictionary<string, Dictionary<string, string>> FixableUrlActions =  new Dictionary<string, Dictionary<string, string>>();

		/// <summary>
		/// List of Content-Types that should be always 302ed
		/// </summary>
		public static List<string> FixableTypes = new List<string>();

		/// <summary>
		/// Dictionary of Content-Types that should be always 302ed to converter
		/// </summary>
		public static Dictionary<string, Dictionary<string, string>> FixableTypesActions = new Dictionary<string, Dictionary<string, string>>();


		/// <summary>
		/// List of possible content patches
		/// </summary>
		public static List<string> ContentPatches = new List<string>();

		/// <summary>
		/// Dictionary of possible content patches
		/// </summary>
		public static Dictionary<string, Dictionary<string, string>> ContentPatchActions = new Dictionary<string, Dictionary<string, string>>();

		/// <summary>
		/// List of domains where 302 redirections should be passed through .NET FW
		/// </summary>
		public static string[] InternalRedirectOn = { "flickr.com.example", "www.flickr.com.example"};

		/// <summary>
		/// Hide "Can't read from client" and "Cannot return reply to the client" error messages in log
		/// </summary>
		public static bool HideClientErrors = false;

		/// <summary>
		/// Search for copies of removed sites in web.archive.org
		/// </summary>
		public static bool SearchInArchive = false;

		/// <summary>
		/// Make Web.Archive.Org error messages laconic (for retro browsers)
		/// </summary>
		public static bool ShortenArchiveErrors = false;

		/// <summary>
		/// List of enabled file format converters
		/// </summary>
		public static List<Converter> Converters = new List<Converter>();

		/// <summary>
		/// User-agent string of the Proxy
		/// </summary>
		public static string UserAgent = "%Original% WebOne/%WOVer%";

		/// <summary>
		/// Proxy default host name (or IP)
		/// </summary>
		public static string DefaultHostName = Environment.MachineName;

		/// <summary>
		/// Break network operations when remote TLS certificate is bad
		/// </summary>
		public static bool ValidateCertificates = true;

		static ConfigFile()
		{
			//ConfigFileName = "webone.conf";
			Console.WriteLine("Using configuration file {0}.", ConfigFileName);
			int i = 0;
			try
			{
				if (!File.Exists(ConfigFileName)) { Console.WriteLine("{0}: no such config file. Using defaults.", ConfigFileName); return; };
				
				string[] CfgFile = System.IO.File.ReadAllLines(ConfigFileName);
				string Section = "";
				for (i = 0; i < CfgFile.Count(); i++)
				{
					if (CfgFile[i] == "") continue; //empty lines
					if (CfgFile[i].StartsWith(";")) continue; //comments
					if (CfgFile[i].StartsWith("[")) //section
					{
						Section = CfgFile[i].Substring(1, CfgFile[i].Length - 2);
						StringListConstructor.Clear();

						if (Section.StartsWith("FixableURL:")) 
						{
							FixableURLs.Add(Section.Substring(11));
							FixableUrlActions.Add(Section.Substring(11), new Dictionary<string, string>());
						}

						if (Section.StartsWith("FixableType:"))
						{
							FixableTypes.Add(Section.Substring(12));
							FixableTypesActions.Add(Section.Substring(12), new Dictionary<string, string>());
						}

						if (Section.StartsWith("ContentPatch:"))
						{
							ContentPatches.Add(Section.Substring(13));
							ContentPatchActions.Add(Section.Substring(13), new Dictionary<string, string>());
						}

						if (Section.StartsWith("ContentPatchFind:"))
						{
							Console.WriteLine("Warning: ContentPatchFind sections are no longer supported. See wiki.");
						}

						continue;
					}


					//Console.WriteLine(Section);
					if (Program.CheckString(Section, SpecialSections)) //special sections (patterns, lists, etc)
					{
						//Console.WriteLine("{0}+={1}", Section, CfgFile[i]);
						switch (Section)
						{
							case "ForceHttps":
								StringListConstructor.Add(CfgFile[i]);
								ForceHttps = StringListConstructor.ToArray();
								continue;
							case "TextTypes":
								StringListConstructor.Add(CfgFile[i]);
								TextTypes = StringListConstructor.ToArray();
								continue;
							case "ForceUtf8":
								StringListConstructor.Add(CfgFile[i]);
								ForceUtf8 = StringListConstructor.ToArray();
								continue;
							case "InternalRedirectOn":
								StringListConstructor.Add(CfgFile[i]);
								InternalRedirectOn = StringListConstructor.ToArray();
								continue;
							case "Converters":
								Converters.Add(new Converter(CfgFile[i]));
								continue;
							default:
								Console.WriteLine("Warning: The special section {0} is not implemented in this build.", Section);
								continue;
						}
						//continue; //statement cannot be reached
					}

					int BeginValue = CfgFile[i].IndexOf("=");//regular sections
					if (BeginValue < 1) continue; //bad line
					string ParamName = CfgFile[i].Substring(0, BeginValue);
					string ParamValue = CfgFile[i].Substring(BeginValue + 1);
					//Console.WriteLine("{0}.{1}={2}", Section, ParamName, ParamValue);
					
					//Console.WriteLine(Section);
					if (Section.StartsWith("FixableURL"))
					{
						//Console.WriteLine("URL Fix rule: {0}/{1} = {2}",Section.Substring(11),ParamName,ParamValue);
						FixableUrlActions[Section.Substring(11)].Add(ParamName, ParamValue);
						continue;
					}

					if (Section.StartsWith("FixableType"))
					{
						FixableTypesActions[Section.Substring(12)].Add(ParamName, ParamValue);
						continue;
					}

					if (Section.StartsWith("ContentPatch:"))
					{
						if (!ContentPatches.Contains(Section.Substring(13))) ContentPatches.Add(Section.Substring(13));
						ContentPatchActions[Section.Substring(13)].Add(ParamName, ParamValue);
						continue;
					}


					switch (Section)
					{
						case "Server":
							switch (ParamName)
							{
								case "Port":
									Port = Convert.ToInt32(ParamValue);
									break;
								case "OutputEncoding":
									if (ParamValue == "Windows" || ParamValue == "Win" || ParamValue == "ANSI")
									{
										//OutputEncoding = Encoding.Default; //.NET 4.0
										OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
										continue;
									}
									else if (ParamValue == "DOS" || ParamValue == "OEM")
									{
										OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
										continue;
									}
									else if (ParamValue == "Mac" || ParamValue == "Apple")
									{
										OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.MacCodePage);
										continue;
									}
									else if (ParamValue == "EBCDIC" || ParamValue == "IBM")
									{
										OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.EBCDICCodePage);
										continue;
									}
									else if (ParamValue == "0" || ParamValue == "AsIs")
									{
										OutputEncoding = null;
										continue;
									}
									else
									{
										try
										{
											//OutputEncoding = Encoding.GetEncoding(ParamValue); 
											OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(ParamValue);
											if (OutputEncoding == null)
												try { OutputEncoding = CodePagesEncodingProvider.Instance.GetEncoding(int.Parse(ParamValue)); } catch { }

											if (OutputEncoding == null && ParamValue.ToLower().StartsWith("utf"))
											{
												switch (ParamValue.ToLower())
												{
													case "utf-7":
														OutputEncoding = Encoding.UTF7;
														break;
													case "utf-8":
														OutputEncoding = Encoding.UTF8;
														break;
													case "utf-16":
													case "utf-16le":
														OutputEncoding = Encoding.Unicode;
														break;
													case "utf-16be":
														OutputEncoding = Encoding.BigEndianUnicode;
														break;
													case "utf-32":
													case "utf-32le":
														OutputEncoding = Encoding.UTF32;
														break;
												}
											}
											
											if (OutputEncoding == null)
											{ Console.WriteLine("Warning: Unknown codepage {0}, using AsIs. See MSDN 'Encoding.GetEncodings Method' article for list of valid encodings.", ParamValue); };
										}
										catch (ArgumentException) { Console.WriteLine("Warning: Bad codepage {0}, using {1}. Get list of available encodings at http://{2}:{3}/!codepages/.", ParamValue, OutputEncoding.EncodingName, ConfigFile.DefaultHostName, Port); }
									}
									continue;
								case "Authenticate":
									Authenticate = ParamValue;
									continue;
								case "HideClientErrors":
									HideClientErrors = ToBoolean(ParamValue);
									continue;
								case "SearchInArchive":
									SearchInArchive = ToBoolean(ParamValue);
									continue;
								case "ShortenArchiveErrors":
									ShortenArchiveErrors = ToBoolean(ParamValue);
									continue;
								case "SecurityProtocols":
									try { System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)(int.Parse(ParamValue)); }
									catch (NotSupportedException) { Console.WriteLine("Warning: Bad TLS version {1} ({0}), using {2} ({2:D}).", ParamValue, (System.Net.SecurityProtocolType)(int.Parse(ParamValue)), System.Net.ServicePointManager.SecurityProtocol); };
									continue;
								case "UserAgent":
									UserAgent = ParamValue;
									continue;
								case "DefaultHostName":
									DefaultHostName = ParamValue.Replace("%HostName%", Environment.MachineName);
									bool ValidHostName = (Environment.MachineName.ToLower() == DefaultHostName.ToLower());
									if (!ValidHostName) foreach (System.Net.IPAddress LocIP in Program.GetLocalIPAddresses())
									{ if (LocIP.ToString() == DefaultHostName) ValidHostName = true; }
									if (!ValidHostName) Console.WriteLine("Warning: DefaultHostName setting is not applicable to this computer!");
									continue;
								case "ValidateCertificates":
									ValidateCertificates = ToBoolean(ParamValue);
									continue;
								default:
									Console.WriteLine("Warning: Unknown server option: " + ParamName);
									break;
							}
							break;
						default:
							Console.WriteLine("Warning: Unknown section: " + Section);
							break;
					}

				}
			}
			catch(Exception ex) {
				#if DEBUG
				Console.WriteLine("Error on line {1}: {0}.\nGo to debugger.",ex.ToString(), i);
				throw;
				#else
				Console.WriteLine("Error on line {1}: {0}.\nAll next lines are ignored.", ex.Message, i);
				#endif
			}
			if (i < 1) Console.WriteLine("Warning: curiously short file. Probably line endings are not valid for this OS.");
			Console.WriteLine("{0} load complete.", ConfigFileName);
		}

		/// <summary>
		/// Convert string "true/false" or similar to bool true/false
		/// </summary>
		/// <param name="s">One of these strings: 1/0, y/n, yes/no, on/off, enable/disable, true/false</param>
		/// <returns>Boolean true/false</returns>
		/// <exception cref="InvalidCastException">Throws if the <paramref name="s"/> is not 1/0/y/n/yes/no/on/off/enable/disable/true/false</exception>
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
