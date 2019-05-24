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
		static string ConfigFileName = "/dev/ceiling"; //с потолка
		static string[] SpecialSections = { "ForceHttps", "TextTypes", "UA:", "URL:" }; //like "UA:Mozilla/3.*"

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
		public static string[] ForceHttps = { "phantom.sannata.org", "www.phantom.sannata.org", "vogons.org" };

		/// <summary>
		/// List of parts of Content-Types that describing text files
		/// </summary>
		public static string[] TextTypes = { "text/", "javascript", "json", "cdf", "xml"};

		/// <summary>
		/// Encoding to be used in output content
		/// </summary>
		public static Encoding OutputEncoding = Encoding.Default;

		static ConfigFile() {
			ConfigFileName = "webone.conf";
			Console.WriteLine("Using configuration file {0}.", ConfigFileName);

			if (!File.Exists(ConfigFileName)) return;
			string[] CfgFile = System.IO.File.ReadAllLines(ConfigFileName);
			string Section = "";
			for(int i = 0; i<CfgFile.Count(); i++) {
				if (CfgFile[i] == "") continue; //empty lines
				if (CfgFile[i].StartsWith(";")) continue; //comments
				if (CfgFile[i].StartsWith("[")) //section
				{
					Section = CfgFile[i].Substring(1, CfgFile[i].Length - 2);
					continue;
				}
				if(i > 1 && CfgFile[i] == "" && CfgFile[i-1] == "") //section separator
				{
					Section = "";
					continue;
				}

				/*Console.WriteLine(Section);
				if(Program.CheckString(Section, SpecialSections)) //special sections (patterns, lists, etc)
				{
					Console.WriteLine("Special: " + Section);
					continue;
				}*/
				
				int BeginValue = CfgFile[i].IndexOf("=");//regular sections
				if (BeginValue == 0) continue; //bad line
				string ParamName = CfgFile[i].Substring(0, BeginValue);
				string ParamValue = CfgFile[i].Substring(BeginValue + 1);
				//Console.WriteLine("{0}.{1}={2}", Section, ParamName, ParamValue);

				switch (Section) {
					case "Server":
						switch(ParamName) {
							case "Port":
								Port = Convert.ToInt32(ParamValue);
								break;
							default:
								Console.WriteLine("Unknown server option: " + ParamName);
								break;
						}
						break;
					default:
						Console.WriteLine("Unknown section: " + ParamName);
						break;
				}

			}
			Console.WriteLine("{0} load complete.", ConfigFileName);
		}
	}
}
