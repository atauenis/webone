using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WebOne
{
	public static class Program
	{
		public static string ConfigFileName = "webone.conf";


		static void Main(string[] args)
		{
			Console.Title = "WebOne";
			Console.WriteLine("WebOne HTTP Proxy Server {0}.\n(C) 2019 Alexander Tauenis.\nhttps://github.com/atauenis/webone\n\n", Assembly.GetExecutingAssembly().GetName().Version);

			int Port = -1;
			try { Port = Convert.ToInt32(args[0]); if (args.Length > 1) ConfigFileName = args[1]; }
			catch { if(args.Length > 0) ConfigFileName = args[0]; }
			if (Port < 1) Port = ConfigFile.Port; else ConfigFile.Authenticate = ConfigFile.Authenticate; //what?!!
			Console.Title = "WebOne @ " + Environment.MachineName + ":" + Port;
			
			NewHTTPServer Server = new NewHTTPServer(Port);

			//HTTPServer Server = new HTTPServer(Port);

			Console.WriteLine("That's all. Closing.");
		}

		public static string GetInfoString() {
			string OnNet40 = "";
			return "<hr>WebOne Proxy Server " + Assembly.GetExecutingAssembly().GetName().Version + "<br>on " + Environment.OSVersion.VersionString + OnNet40;
		}

		/// <summary>
		/// Check a string for containing a something from list of patterns
		/// </summary>
		/// <param name="What">What string should be checked</param>
		/// <param name="For">Pattern to find</param>
		public static bool CheckString(string What, string[] For) {
			foreach (string str in For) { if (What.Contains(str)) return true; }
			return false;
		}
	}
}
