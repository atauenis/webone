using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace WebOne
{
	public static class Program
	{
		public static string ConfigFileName = "**auto**webone.conf";

		public static int Load = 0;

		static void Main(string[] args)
		{
			Console.Title = "WebOne";
			Console.WriteLine("WebOne HTTP Proxy Server {0} Alpha 1\n(C) https://github.com/atauenis/webone\n\n", Assembly.GetExecutingAssembly().GetName().Version);

			//process command line arguments
			int Port = -1;
			try { Port = Convert.ToInt32(args[0]); if (args.Length > 1) ConfigFileName = args[1]; }
			catch { if(args.Length > 0) ConfigFileName = args[0]; }

			ConfigFileName = GetDefaultConfigurationFile();

			//load configuration file and set port number
			if (Port < 1) Port = ConfigFile.Port; else ConfigFile.Port = Port;

			if (!ConfigFile.HaveLogFile) LogAgent.OpenLogFile(null);

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


		/// <summary>
		/// Make info string (footer) for message pages
		/// </summary>
		/// <returns>HTML: WebOne vX.Y.Z on Windows NT 6.2.9200 Service Pack 6</returns>
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
		/// Check a string array for containing a pattern
		/// </summary>
		/// <param name="Where">Where the search should be do</param>
		/// <param name="For">Pattern to find</param>
		public static bool CheckString(string[] Where, string For)
		{
			foreach (string str in Where) { if (str.Contains(For)) return true; }
			return false;
		}

		/// <summary>
		/// Check a string for containing a something from list of RegExp patterns
		/// </summary>
		/// <param name="What">What string should be checked</param>
		/// <param name="For">Pattern to find</param>
		public static bool CheckStringRegExp(string What, string[] For)
		{
			foreach (string str in For) { if (System.Text.RegularExpressions.Regex.IsMatch(What, str)) return true; }
			return false;
		}

		/// <summary>
		/// Check a string array for containing a RegExp pattern
		/// </summary>
		/// <param name="Where">Where the search should be do</param>
		/// <param name="For">Pattern to find</param>
		public static bool CheckStringRegExp(string[] Where, string For)
		{
			foreach (string str in Where) { if (System.Text.RegularExpressions.Regex.IsMatch(str, For)) return true; }
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
		/// Fill %masks% on an URI template
		/// </summary>
		/// <param name="MaskedURL">URI template</param>
		/// <param name="PossibleURL">Previous URI (for "%URL%" mask and similar)</param>
		/// <returns>Ready URL</returns>
		public static string ProcessUriMasks(string MaskedURL, string PossibleURL = "http://webone.github.io:80/index.htm")
		{
			string str = MaskedURL;
			string URL = null;
			if (CheckString(PossibleURL, ConfigFile.ForceHttps))
				URL = new UriBuilder(PossibleURL) { Scheme = "https" }.Uri.ToString();
			else
				URL = PossibleURL;

			str = str.Replace("%URL%", URL);
			str = str.Replace("%Url%", Uri.EscapeDataString(URL));
			str = str.Replace("%ProxyHost%", Environment.MachineName);
			str = str.Replace("%ProxyPort%", ConfigFile.Port.ToString());
			str = str.Replace("%Proxy%", ConfigFile.DefaultHostName + ":" + ConfigFile.Port.ToString());

			UriBuilder builder = new UriBuilder(URL);

			if (str.Contains("%UrlNoDomain%"))
			{
				builder.Host = "butaforia-" + new Random().Next().ToString();
				str = str.Replace("%UrlNoDomain%", builder.Uri.ToString().Replace(builder.Host + ":" + builder.Port, "").Replace(builder.Scheme + "://", ""));
				builder = new UriBuilder(URL);
			}

			if (str.Contains("%UrlNoPort%"))
			{
				builder.Port = new Random().Next(1, 65535);
				str = str.Replace("%UrlNoPort%", builder.Uri.ToString().Replace(":" + builder.Port.ToString(), ""));
				builder = new UriBuilder(URL);
			}

			if (str.Contains("%UrlNoQuery%"))
			{
				builder.Query = "?noquery=" + new Random().Next().ToString();
				str = str.Replace("%UrlNoQuery%", builder.Uri.ToString().Replace(builder.Query, ""));
				builder = new UriBuilder(URL);
			}

			if (str.Contains("%UrlHttps%"))
			{
				builder.Scheme = "https";
				str = str.Replace("%UrlHttps%", builder.Uri.ToString());
				builder = new UriBuilder(URL);
			}

			if (str.Contains("%UrlHttp%"))
			{
				builder.Scheme = "http";
				str = str.Replace("%UrlHttp%", builder.Uri.ToString());
				builder = new UriBuilder(URL);
			}

			return str;
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

		/// <summary>
		/// Get all server IP addresses
		/// </summary>
		/// <returns>All IPv4/IPv6 addresses of this machine</returns>
		public static IPAddress[] GetLocalIPAddresses()
		{
			List<IPAddress> IPs = new List<IPAddress>();
			foreach ((NetworkInterface Netif, UnicastIPAddressInformation ipa) in
							 from NetworkInterface Netif in NetworkInterface.GetAllNetworkInterfaces()
							 from ipa in Netif.GetIPProperties().UnicastAddresses
							 select (Netif, ipa))
				IPs.Add(ipa.Address);
			return IPs.ToArray();
		}

		/// <summary>
		/// Find and/or create default webone.conf
		/// </summary>
		/// <returns>Path to default configuration file</returns>
		public static string GetDefaultConfigurationFile()
		{
			string CurrentDirConfigFile = "webone.conf";
			string DefaultConfigFile = "";  //  webone.conf       (in app's directory)
			string SkeletonConfigFile = ""; //  webone.conf.skel  (too)
			string UserConfigFile = "";     //  ~/.config/WebOne/webone.conf
			string CommonConfigFile = "";   //  /etc/WebOne/webone.conf

			switch (Environment.OSVersion.Platform)
			{
				default:
				case PlatformID.Unix:
					DefaultConfigFile = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName + "/webone.conf";
					SkeletonConfigFile = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName + "/webone.conf.skel";
					UserConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/WebOne/webone.conf";
					CommonConfigFile = "/etc/WebOne/webone.conf";
					break;
				case PlatformID.Win32NT:
					DefaultConfigFile = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName + @"\webone.conf";
					SkeletonConfigFile = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName + @"\webone.conf.skel";
					UserConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WebOne\webone.conf";
					CommonConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\WebOne\webone.conf";
					break;
			}

#if DEBUG
			UserConfigFile = DefaultConfigFile; //debug versions weren't deb/rpm-packaged so they can use old-style WebOne.conf placement
#endif

			//try to load custom configuration file (if any)
			if (ConfigFileName != "**auto**webone.conf") return ConfigFileName;

			//try to load webone.conf from current directory
			if (File.Exists(CurrentDirConfigFile)) return CurrentDirConfigFile;

			//try to load webone.conf from application's directory
			if (File.Exists(DefaultConfigFile)) return DefaultConfigFile;

			//try to load webone.conf from user configuration directory
			if (File.Exists(UserConfigFile)) return UserConfigFile;

			//try to load webone.conf from common configuration directory
			if (File.Exists(CommonConfigFile)) return CommonConfigFile;

			//if there are no config files, try to create from skeleton
			try
			{
				//1. Common config directory
				File.Copy(SkeletonConfigFile, CommonConfigFile);
				Console.WriteLine("Info: default configuration file is now: {0}.", CommonConfigFile);
				return CommonConfigFile;
			}
			catch
			{
				try
				{
					//2. User config directory
					File.Copy(SkeletonConfigFile, UserConfigFile);
					Console.WriteLine("Info: default configuration file is now: {0}.", UserConfigFile);
					return UserConfigFile;
				}
				catch 
				{
					//3. Return skeleton file
					if (!File.Exists(SkeletonConfigFile))
					{
						Console.WriteLine("Warning: there are no configuration file and no skeleton for it!");
						return CurrentDirConfigFile;
					}
					Console.WriteLine("Warning: please copy webone.conf.skel to webone.conf.");
					return SkeletonConfigFile; 
				}
			}
			//Probably need to add an "Quick setup" interface for configuring webone.conf from skeleton.
			//E.g. ask user for DefaultHostName, OutputEncoding and Translit.
		}

		/// <summary>
		/// Find default directory for log files
		/// </summary>
		public static string GetDefaultLogDirectory()
		{
			switch (Environment.OSVersion.Platform)
			{
				case PlatformID.Win32NT:
					return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
				case PlatformID.Unix:
					return "/var/log";
				default:
					return Environment.CurrentDirectory;
			}
		}

		/// <summary>
		/// Get path of log file, after processing the %SYSLOGDIR% mask
		/// </summary>
		public static string GetLogFilePath(string RawPath)
		{
			string LogFilePath = RawPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
			return LogFilePath.Replace("%SYSLOGDIR%", GetDefaultLogDirectory());
		}

	}
}
