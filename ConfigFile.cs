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
		public static string[] ForceHttps = { "phantom.sannata.org", "www.phantom.sannata.org", "vogons.org" };
	}
}
