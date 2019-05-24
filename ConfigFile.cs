using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebOne
{
	/// <summary>
	/// Config file entries and parser
	/// </summary>
	static class ConfigFile
	{
		//тут будут все ключи из конфиг-файла программы. Но пока файла нет, все они жёстко захардкожены здесь.
		static string ConfigFileName = "/dev/ceiling"; //с потолка

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
			Console.WriteLine("Using configuration file {0}.", ConfigFileName);
		}
	}
}
