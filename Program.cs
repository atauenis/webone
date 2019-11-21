using System;
using System.Collections.Generic;
using System.IO;
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

		public static int Load = 0;

		static void Main(string[] args)
		{
			Console.Title = "WebOne";
			Console.WriteLine("WebOne HTTP Proxy Server {0}.\n(C) 2019 Alexander Tauenis.\nhttps://github.com/atauenis/webone\n\n", Assembly.GetExecutingAssembly().GetName().Version);

			int Port = -1;
			try { Port = Convert.ToInt32(args[0]); if (args.Length > 1) ConfigFileName = args[1]; }
			catch { if(args.Length > 0) ConfigFileName = args[0]; }
#pragma warning disable CS1717 // Назначение выполнено для той же переменной - workaround to call ConfigFile constructor
			if (Port < 1) Port = ConfigFile.Port; else ConfigFile.Authenticate = ConfigFile.Authenticate; //else load config file (пусть прочухается static class)
#pragma warning restore CS1717 // Назначение выполнено для той же переменной

			Console.Title = "WebOne @ " + ConfigFile.DefaultHostName + ":" + Port;

			try
			{
				new HTTPServer(Port);
			}
			catch(Exception ex)
			{
				Console.WriteLine("Cannot start server: {0}!", ex.Message);
				#if DEBUG
				throw;
				#endif
			}

			Console.WriteLine("Press any key to exit.");
			Console.ReadKey();
		}


		public static string GetInfoString() {
			return "<hr>WebOne Proxy Server " + Assembly.GetExecutingAssembly().GetName().Version + "<br>on " + Environment.OSVersion.VersionString;
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


		/// <summary>
		/// Make a string with timestamp
		/// </summary>
		/// <param name="BeginTime">Initial time</param>
		/// <returns>The initial time and difference with the current time</returns>
		public static string GetTime(DateTime BeginTime)
		{
			TimeSpan difference = DateTime.UtcNow - BeginTime;
			return BeginTime.ToString("HH:mm:ss.fff") + "+" + difference.Ticks;
		}


		/// <summary>
		/// Read all bytes from a Stream (like StreamReader.ReadToEnd)
		/// </summary>
		/// <param name="stream">Source Stream</param>
		/// <returns>All bytes of it</returns>
		public static byte[] ReadAllBytes(Stream stream)
		{
			using (var ms = new MemoryStream())
			{
				stream.CopyTo(ms);
				return ms.ToArray();
			}
		}


		/// <summary>
		/// Get user-agent string for a request
		/// </summary>
		/// <param name="ClientUA">Client's user-agent</param>
		/// <returns>Something like "Mozilla/3.04Gold (U; Windows NT 3.51) WebOne/1.0.0.0 (Unix)"</returns>
		public static string GetUserAgent(string ClientUA = "")
		{
			return ConfigFile.UserAgent
			.Replace("%Original%", ClientUA ?? "Mozilla/5.0 (Kundryuchy-Leshoz)")
			.Replace("%WOVer%", Assembly.GetExecutingAssembly().GetName().Version.ToString())
			.Replace("%WOSystem%", Environment.OSVersion.Platform.ToString());
		}
	}
}
