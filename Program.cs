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
	/* TODO list for v0.9.4 alpha/beta

	0. Fix "Сбой установки соединения из-за неожиданного формата пакета" on SSL
	https://social.msdn.microsoft.com/Forums/en-US/e8807c4c-72b6-4254-ae64-45c2743b181e/ssltls-the-handshake-failed-due-to-an-unexpected-packet-format-mercury-for-win32-pop3?forum=ncl
		- ServerCertificateValidationCallback	=	COMPLETE, not helped

	1. Fix "authentication failed because the remote party has closed the transport stream"

	2. Headers on requests to ForceHttps domains:
	origin: httpS://www.vogons.org
	referer: httpS://www.vogons.org/index.php
	sec-fetch-mode: navigate
	sec-fetch-site: same-origin
	sec-fetch-user: ?1
	upgrade-insecure-requests: 1
	
	(https://developer.mozilla.org/ru/docs/Web/HTTP/CORS)

	3. Secure Referers on ForceHttps:
	referer: httpS://www.vogons.org/index.php

	4. Kill strict-transport-security response header

	5. Add ability to add request headers sec-fetch-*

	6. Fix "cannot load <temp file name>, it is in use by another process"

	
	Plans for 0.10.0
	0. Release after 0.9.4beta
	1. Move to .NET Core
	2. New syntax of patch rules
	3. Cache and log (sniffer) for debugging purposes
	*/
	public static class Program
	{
		public static string ConfigFileName = "webone.conf";

		public static int Load = 0;

		static void Main(string[] args)
		{
			Console.Title = "WebOne";
			Console.WriteLine("WebOne HTTP Proxy Server {0}.\n(C) https://github.com/atauenis/webone\n\n", Assembly.GetExecutingAssembly().GetName().Version);

			int Port = -1;
			try { Port = Convert.ToInt32(args[0]); if (args.Length > 1) ConfigFileName = args[1]; }
			catch { if(args.Length > 0) ConfigFileName = args[0]; }
#pragma warning disable CS1717 // Назначение выполнено для той же переменной - workaround to call ConfigFile constructor
			if (Port < 1) Port = ConfigFile.Port; else ConfigFile.Authenticate = ConfigFile.Authenticate; //else load config file (пусть прочухается static class)
#pragma warning restore CS1717 // Назначение выполнено для той же переменной

			ServicePointManager.DefaultConnectionLimit = int.MaxValue;
			//https://qna.habr.com/q/696033
			//https://github.com/atauenis/webone/issues/2

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
