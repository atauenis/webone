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

		static string[] SpecialSections = { "ForceHttps", "TextTypes", "ForceUtf8", "InternalRedirectOn" };

		/// <summary>
		/// TCP port that should be used by the Proxy Server
		/// </summary>
		public static int Port = 80;

		/// <summary>
		/// Size of request buffer size.
		/// Too long size will enlarge memory usage, too short size may cause problems with POST queries.
		/// </summary>
		public static long RequestBufferSize = 10485760;

		/// <summary>
		/// Timeout for connections. For fast clients set less than 10, for slow set more than 9000.
		/// </summary>
		public static long ClientTimeout = 1000;

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
							default:
								Console.WriteLine("Warning: The special section {0} is not implemented in this build.", Section);
								continue;
						}
						continue;
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
								case "RequestBufferSize":
									RequestBufferSize = Convert.ToInt32(ParamValue);
									break;
								case "ClientTimeout":
									ClientTimeout = Convert.ToInt32(ParamValue);
									break;
								case "OutputEncoding":
									if (ParamValue == "Windows" || ParamValue == "Win" || ParamValue == "ANSI")
									{
										OutputEncoding = Encoding.Default;
										continue;
									}
									else if (ParamValue == "0" || ParamValue == "AsIs")
									{
										OutputEncoding = null;
										continue;
									}
									else
									{
										try { OutputEncoding = Encoding.GetEncoding(ParamValue); }
										catch (ArgumentException) { Console.WriteLine("Warning: Bad codepage {0}, using {1}. Get list of available encodings at http://{2}:{3}/!codepages/.", ParamValue, OutputEncoding.EncodingName, Environment.MachineName, Port); }
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
