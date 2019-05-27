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
		static List<string> StringListConstructor = new List<string>();

		static string ConfigFileName = "/dev/ceiling"; //с потолка
		static string[] SpecialSections = { "ForceHttps", "TextTypes"/*, "UA:", "URL:" */}; //like "UA:Mozilla/3.*"

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
		/// For fast clients set less than 10, for slow set more than 9000.
		/// </summary>
		public static long SlowClientHack = 1000;

		/// <summary>
		/// List of domains that should be open only using HTTPS
		/// </summary>
		public static string[] ForceHttps = { "www.phantom.sannata.org" };

		/// <summary>
		/// List of parts of Content-Types that describing text files
		/// </summary>
		public static string[] TextTypes = { "text/", "javascript"};

		/// <summary>
		/// Encoding to be used in output content
		/// </summary>
		public static Encoding OutputEncoding = Encoding.Default;

		/// <summary>
		/// List of URLs that should be always 302ed
		/// </summary>
		public static List<string> FixableURLs = new List<string>();

		/// <summary>
		/// Dictionary of URLs that should be always 302ed if they're looks like too new JS frameworks
		/// </summary>
		public static Dictionary<string, Dictionary<string, string>> FixableUrlActions =  new Dictionary<string, Dictionary<string, string>>();

		static ConfigFile()
		{
			ConfigFileName = "webone.conf";
			Console.WriteLine("Using configuration file {0}.", ConfigFileName);

			if (!File.Exists(ConfigFileName)) return;

			try
			{
				string[] CfgFile = System.IO.File.ReadAllLines(ConfigFileName);
				string Section = "";
				for (int i = 0; i < CfgFile.Count(); i++)
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

						continue;
					}
					if (i > 1 && CfgFile[i] == "" && CfgFile[i - 1] == "") //section separator
					{
						//doesn't work, needs to be investigated!
						Section = "";
						StringListConstructor.Clear();
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
							default:
								Console.WriteLine("The special section {0} is not implemented in this build.", Section);
								//тут будут обрабатываться сложные параметрные группы
								continue;
						}
						continue;
					}

					int BeginValue = CfgFile[i].IndexOf("=");//regular sections
					if (BeginValue == 0) continue; //bad line
					string ParamName = CfgFile[i].Substring(0, BeginValue);
					string ParamValue = CfgFile[i].Substring(BeginValue + 1);
					//Console.WriteLine("{0}.{1}={2}", Section, ParamName, ParamValue);

					if (Section.StartsWith("FixableURL"))
					{
						//Console.WriteLine("URL Fix rule: {0}/{1} = {2}",Section.Substring(11),ParamName,ParamValue);
						FixableUrlActions[Section.Substring(11)].Add(ParamName, ParamValue);
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
								case "SlowClientHack":
									SlowClientHack = Convert.ToInt32(ParamValue);
									break;
								case "OutputEncoding":
									if (CfgFile[i] == "0" || CfgFile[i] == "Windows")
									{
										OutputEncoding = Encoding.Default;
									}
									else
									{
										try { OutputEncoding = Encoding.GetEncoding(ParamValue); }
										catch (ArgumentException) { Console.WriteLine("Warning: Bad codepage {0}, using {1}. Get list of available encodings at http://{2}:{3}/!codepages/.", ParamValue, OutputEncoding.EncodingName, Environment.MachineName, Port); }
									}
									continue;
								default:
									Console.WriteLine("Unknown server option: " + ParamName);
									break;
							}
							break;
						default:
							Console.WriteLine("Unknown section: " + Section);
							break;
					}

				}
			}
			catch(Exception ex) {
				Console.WriteLine("Config parser error: {0}.",ex.ToString());
				#if DEBUG
				throw;
				#endif
			}
			Console.WriteLine("{0} load complete.", ConfigFileName);
		}
	}
}
