using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebOne
{
	public static class Program
	{
		public static LogWriter Log = new LogWriter();
		private static HTTPServer HTTPS;

		public const string ConfigFileAutoName = "**auto**webone.conf";
		public static string ConfigFileName = ConfigFileAutoName;
		public static string OverrideLogFile = "";
		public static int Port = -1;
		public static int Load = 0;
		public static bool DaemonMode = false;
		static bool ShutdownInitiated = false;

		public const string CmdLineArgUnnamed = "--wo-short";
		public static List<KeyValuePair<string, string>> CmdLineOptions = new List<KeyValuePair<string, string>>();


		static void Main(string[] args)
		{
			Console.Title = "WebOne";
			Console.WriteLine("WebOne HTTP Proxy Server {0}\n(C) https://github.com/atauenis/webone\n\n", Assembly.GetExecutingAssembly().GetName().Version);

			//process command line arguments
			ProcessCommandLine(args);
			ConfigFileName = GetConfigurationFileName();

			//load configuration file and set port number
			try
			{
				ConfigFileLoader.LoadFile(GetConfigurationFileName());
				ConfigFileLoader.ProcessConfiguration();
				if (Port < 1) Port = ConfigFile.Port; else ConfigFile.Port = Port;
			}
			catch(Exception ConfigLoadException)
			{
				Console.WriteLine("Error while loading configuration: {0}", ConfigLoadException.Message);
				if (!DaemonMode)
				{
					Console.WriteLine("\nPress any key to exit.");
					Console.ReadKey();
				}
				Log.WriteLine(false, false, "WebOne has been exited due to lack of configuration.");
				return;
			}

			//process remaining command line arguments and override configuration file options
			ProcessCommandLineOptions();

			//if log is not declared, say "Not using log file"
			if (OverrideLogFile == null) LogAgent.OpenLogFile(null);

			//check for --daemon mode
			if (DaemonMode)
			{
				if (!LogAgent.IsLoggingEnabled)
				{
					Console.WriteLine("Error: log file is not available, please fix the problem. Exiting.");
					return;
				}
				Console.Title = "WebOne (silent) @ " + ConfigFile.DefaultHostName + ":" + ConfigFile.Port;
				Console.WriteLine("The proxy runs in daemon mode. See all messages in the log file.");
			}

			ServicePointManager.DefaultConnectionLimit = int.MaxValue;
			//https://qna.habr.com/q/696033
			//https://github.com/atauenis/webone/issues/2

			//set console window title
			if (!DaemonMode) Console.Title = "WebOne @ " + ConfigFile.DefaultHostName + ":" + ConfigFile.Port;

			Log.WriteLine(false, false, "Configured to http://{1}:{2}/, HTTP 1.0", ConfigFileName, ConfigFile.DefaultHostName, ConfigFile.Port);
			HTTPS = new HTTPServer(ConfigFile.Port);

			//start the server from 1 or 2 attempts
			for (int StartAttempts = 0; StartAttempts < 2; StartAttempts++)
			{
				try
				{
					HTTPS.Start();
					break;
				}
				catch (HttpListenerException ex)
				{
					Log.WriteLine(true, false, "Cannot start server: {0}", ex.Message);

					if (!DaemonMode && ex.NativeErrorCode == 5)
					{
						//access (for listen TCP port) denied, show troubleshooting help
						if (ex.NativeErrorCode == 5 && Environment.OSVersion.Platform == PlatformID.Unix) //access denied @ *nix
						{
							Console.WriteLine();
							Console.WriteLine(@"You need to use ""sudo WebOne"" or use Port greater than 1024.");
							Shutdown(ex.NativeErrorCode);
							break;
						}
						if (ex.NativeErrorCode == 5 && Environment.OSVersion.Platform == PlatformID.Win32NT && StartAttempts == 0) //access denied @ Win32
						{
							Console.WriteLine();
							Console.WriteLine("Seems that Windows has been blocked running WebOne with non-admin rights.");
							Console.WriteLine("Read more in project's wiki:");
							Console.WriteLine("https://github.com/atauenis/webone/wiki/Windows-installation#how-to-run-without-admin-privileges");
							Console.Write("Do you want to add a Windows Network Shell rule to run WebOne with user rights? (Y/N)");
							if (Console.ReadKey().Key == ConsoleKey.Y)
							{
								ConfigureWindowsNetShell(Port);
								continue;
							}
							else
							{
								Shutdown(ex.NativeErrorCode);
								break;
							}
						}
					}
					Shutdown(ex.NativeErrorCode);
					break;
				}
				catch(Exception ex)
				{
					Log.WriteLine(true, false, "Server start failed: {0}", ex.Message);
					Shutdown(ex.HResult);
					break;
				}
			}

			//register Ctrl+C/kill handler
			System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += (ctx) => { Shutdown(); };
			Console.CancelKeyPress += (s, e) => { Shutdown(); };

			//wait while server is in work
			while (HTTPS.Working) { Thread.Sleep(250); }

			//the end
			Shutdown();
		}

		/// <summary>
		/// Shut down server and terminate process
		/// </summary>
		/// <param name="Code">Process exit code</param>
		public static void Shutdown(int Code = 0)
		{
			if (ShutdownInitiated) return;
			ShutdownInitiated = true;

			if(HTTPS.Working) HTTPS.Stop();

			if (!DaemonMode && !Environment.HasShutdownStarted && !ShutdownInitiated)
			{

				Console.WriteLine("\nPress any key to exit.");
				Console.ReadKey();
			}

			Log.WriteLine(false, false, "WebOne has been exited.");
			Environment.ExitCode = Code;
			new Task(() => { Environment.Exit(Code); }).Start();
			Process.GetCurrentProcess().Kill();
		}

		/// <summary>
		/// Process command line arguments
		/// </summary>
		/// <param name="args">Array of WebOne.exe startup arguments</param>
		private static void ProcessCommandLine(string[] args)
		{
			string ArgName = CmdLineArgUnnamed;
			string ArgValue = "";
			List<KeyValuePair<string, string>> Args = new List<KeyValuePair<string, string>>();

			KeyValuePair<string, string> LastArg = new KeyValuePair<string, string>();
			bool LastWasValue = false;

			foreach (string arg in args)
			{
				if (arg.StartsWith("-") || arg.StartsWith("/"))
				{
					LastWasValue = false;
					LastArg = new KeyValuePair<string, string>(ArgName, ArgValue);
					Args.Add(LastArg);

					ArgName = arg;
					ArgValue = "";
					continue;
				}
				else
				{
					if (LastWasValue)
					{
						LastArg = new KeyValuePair<string, string>(ArgName, ArgValue);
						Args.Add(LastArg);
					}
					ArgValue = arg;
					LastWasValue = true;
					continue;
				}
			}
			LastArg = new KeyValuePair<string, string>(ArgName, ArgValue);
			Args.Add(LastArg);

			foreach (KeyValuePair<string, string> kvp in Args)
			{
				CmdLineOptions.Add(kvp);
				//Console.WriteLine("Arg: '{0}' = '{1}'", kvp.Key, kvp.Value);
				switch (kvp.Key)
				{
					case "/l":
					case "-l":
					case "--log":
						if (kvp.Value == "" || kvp.Value == "no") { OverrideLogFile = null; break; }
						OverrideLogFile = kvp.Value;
						LogAgent.OpenLogFile(OverrideLogFile);
						break;
					case "/t":
					case "-t":
					case "--tmp":
					case "--temp":
					case "/p":
					case "-p":
					case "--port":
					case "--http-port":
					case "/h":
					case "-h":
					case "--host":
					case "--hostname":
					case "/a":
					case "-a":
					case "--proxy-authenticate":
					case "--dump-headers":
					case "--dump-requests":
						//will be processed in ProcessCommandLineOptions()
						break;
					case "--daemon":
						DaemonMode = true;
						break;
					case "--help":
					case "-?":
					case "/?":
						Console.WriteLine("All command line arguments can be found in WebOne Wiki:");
						Console.WriteLine("https://github.com/atauenis/webone/wiki");
						Console.WriteLine();
						Console.WriteLine("Initially made by Alexander Tauenis. Moscow, Russian Federation.");
						Environment.Exit(0);
						break;
					case CmdLineArgUnnamed:
						if (kvp.Value == string.Empty) break;
						try { Port = Convert.ToInt32(kvp.Value); Console.WriteLine("Using custom port {0}.", Port); }
						catch { ConfigFileName = kvp.Value; }
						break;
					default:
						Console.WriteLine("Unknown command line argument: {0}.", kvp.Key);
						break;
				}
			}
		}

		/// <summary>
		/// Get an character <see cref="System.Text.Encoding"/> from code page number or alias.
		/// </summary>
		/// <param name="CP">Code page number.</param>
		internal static Encoding GetCodePage(string CP)
		{
			if (CP == "Windows" || CP == "Win" || CP == "ANSI")
			{
				return CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
			}
			else if (CP == "DOS" || CP == "OEM")
			{
				return CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
			}
			else if (CP == "Mac" || CP == "Apple")
			{
				return CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.MacCodePage);
			}
			else if (CP == "EBCDIC" || CP == "IBM")
			{
				return CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.EBCDICCodePage);
			}
			else if (CP == "0" || CP == "AsIs")
			{
				return null;
			}
			else
			{
				try
				{
					Encoding enc = CodePagesEncodingProvider.Instance.GetEncoding(CP);
					if (enc == null)
						try { return CodePagesEncodingProvider.Instance.GetEncoding(int.Parse(CP)); } catch { }
					else return enc;

					if (enc == null && CP.ToLower().StartsWith("utf"))
					{
						switch (CP.ToLower())
						{
							case "utf-7":
								return Encoding.UTF7;
							case "utf-8":
								return Encoding.UTF8;
							case "utf-16":
							case "utf-16le":
								return Encoding.Unicode;
							case "utf-16be":
								return Encoding.BigEndianUnicode;
							case "utf-32":
							case "utf-32le":
								return Encoding.UTF32;
						}
					}

					Log.WriteLine(true, false, "Warning: Unknown codepage {0}, using AsIs. See MSDN 'Encoding.GetEncodings Method' article for list of valid encodings.", CP);
					return null;
				}
				catch (ArgumentException)
				{
					Log.WriteLine(true, false, "Warning: Bad codepage {0}, using {1}. Get list of available encodings at http://{2}:{3}/!codepages/.", CP, ConfigFile.OutputEncoding.EncodingName, ConfigFile.DefaultHostName, ConfigFile.Port);
					return null;
				}
			}
		}

		/// <summary>
		/// Process command line options that overrides webone.conf
		/// </summary>
		private static void ProcessCommandLineOptions()
		{
			foreach (KeyValuePair<string, string> kvp in CmdLineOptions)
			{
				try
				{
					//Console.WriteLine("Opt: '{0}' = '{1}'", kvp.Key, kvp.Value);
					switch (kvp.Key)
					{
						case "/t":
						case "-t":
						case "--tmp":
						case "--temp":
							if (kvp.Value.ToUpper() == "%TEMP%" || kvp.Value == "$TEMP" || kvp.Value == "$TMPDIR") ConfigFile.TemporaryDirectory = Path.GetTempPath();
							else ConfigFile.TemporaryDirectory = kvp.Value;
							break;
						case "/p":
						case "-p":
						case "--port":
						case "--http-port":
							Port = Convert.ToInt32(kvp.Value);
							ConfigFile.Port = Convert.ToInt32(kvp.Value);
							break;
						case "/h":
						case "-h":
						case "--host":
						case "--hostname":
							ConfigFile.DefaultHostName = kvp.Value;
							break;
						case "/a":
						case "-a":
						case "--proxy-authenticate":
							ConfigFile.Authenticate = new List<string>() { kvp.Value }; //will override all set credentials
							break;
						case "--dump-headers":
							string HdrDmpPath = "dump-hd-%Url%.log";
							if (kvp.Value != "") HdrDmpPath = kvp.Value;

							Console.WriteLine("Will dump headers to: {0}.", HdrDmpPath);
							ConfigFileSection HdrDumpSection = new ConfigFileSection("[Edit]", "--dump-headers argument");
							HdrDumpSection.Options.Add(new ConfigFileOption("AddHeaderDumping=" + HdrDmpPath, "--dump-headers argument"));
							ConfigFile.EditRules.Add(new EditSet(HdrDumpSection));
							break;
						case "--dump-requests":
							string RqDmpPath = "dump-rq-%Url%.log";
							if (kvp.Value != "") RqDmpPath = kvp.Value;

							Console.WriteLine("Will dump headers & uploads's bodies to: {0}.", RqDmpPath);
							ConfigFileSection RqDumpSection = new ConfigFileSection("[Edit]", "--dump-requests argument");
							RqDumpSection.Options.Add(new ConfigFileOption("AddRequestDumping=" + RqDmpPath, "--dump-requests argument"));
							ConfigFile.EditRules.Add(new EditSet(RqDumpSection));
							break;
					}
				}
				catch(Exception ex)
				{
					Console.WriteLine("Warning: Wrong argument '{1} {2}': {0}.", ex.Message, kvp.Key, kvp.Value);
				}
			}
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
		/// Check a string for containing a something from list of patterns
		/// </summary>
		/// <param name="What">What string should be checked</param>
		/// <param name="For">Pattern to find</param>
		public static bool CheckString (string What, List<string> For){
			return CheckString(What, For.ToArray());
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
			TimeSpan difference = DateTime.Now - BeginTime;
			return BeginTime.ToString("dd.MM.yyyy HH:mm:ss.fff") + "+" + difference.Ticks/2;
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
		/// <param name="DontTouchURL">Do not edit previous URI (%URL% mask) in any cases</param>
		/// <returns>Ready URL</returns>
		public static string ProcessUriMasks(string MaskedURL, string PossibleURL = "http://webone.github.io:80/index.htm", bool DontTouchURL = false)
		{
			string str = MaskedURL;
			string URL = null;
			if (CheckString(PossibleURL, ConfigFile.ForceHttps) && !DontTouchURL)
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

			if (str.Contains("%UrlDomain%"))
			{
				str = str.Replace("%UrlDomain%", builder.Host);
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
		/// Find and/or create default or custom (whatever is need) webone.conf
		/// </summary>
		/// <returns>Path to configuration file</returns>
		public static string GetConfigurationFileName()
		{
			string CurrentDirConfigFile = "webone.conf";
			string DefaultConfigFile = "";  //  webone.conf       (in app's directory)
			string UserConfigFile = "";     //  ~/.config/webone/webone.conf
			string CommonConfigFile = "";   //  /etc/webone.conf

			switch (Environment.OSVersion.Platform)
			{
				default:
				case PlatformID.Unix:
					DefaultConfigFile = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName + "/webone.conf";
					UserConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/webone/webone.conf";
					CommonConfigFile = "/etc/webone.conf";
					break;
				case PlatformID.Win32NT:
					DefaultConfigFile = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName + @"\webone.conf";
					UserConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WebOne\webone.conf";
					CommonConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\WebOne\webone.conf";
					break;
			}

#if DEBUG
			UserConfigFile = DefaultConfigFile; //debug versions weren't deb/rpm/zip-packaged so they can use old-style webone.conf placement
#endif

			//try to load custom configuration file (if any)
			if (ConfigFileName != ConfigFileAutoName) return ConfigFileName;

			//try to load webone.conf from current directory
			if (File.Exists(CurrentDirConfigFile)) return CurrentDirConfigFile;

			//try to load webone.conf from application's directory
			if (File.Exists(DefaultConfigFile)) return DefaultConfigFile;

			//try to load webone.conf from user configuration directory
			if (File.Exists(UserConfigFile)) return UserConfigFile;

			//try to load webone.conf from common configuration directory
			if (File.Exists(CommonConfigFile)) return CommonConfigFile;

			throw new Exception("Cannot guess configuration file name. Please run WebOne with full path to the webone.conf specifed.");
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

		/// <summary>
		/// Configure Windows Network Shell (netsh) to allow use TCP/IP <paramref name="Port"/> without admin rights and then open Windows Firewall
		/// </summary>
		/// <param name="Port">WebOne HTTP port number</param>
		public static void ConfigureWindowsNetShell(int Port)
		{
			//fix for https://github.com/atauenis/webone/issues/14
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
				throw new InvalidOperationException("Only for MS Windows");

			string address, NTdomain, NTuser;
			address = string.Format("http://*:{0}/", Port);
			NTdomain = Environment.UserDomainName;
			NTuser = Environment.UserName;

			Console.WriteLine();
			Log.WriteLine(false, false, "Started NETSH configurtion \"wizard\". Logging is not implemented here.");

			Console.Write(" Do you want to add system rule allowing user {0} to run WebOne on port {1}? (Y/N)", "\"" + NTdomain + "\\" + NTuser + "\"", Port);
			if (Console.ReadKey().Key == ConsoleKey.Y)
			{
				string args = string.Format(@"http add urlacl url={0}", address) + " user=\"" + NTdomain + "\\" + NTuser + "\"";
				ProcessStartInfo psi;
				psi = new ProcessStartInfo("netsh", args);
				psi.Verb = "runas";
				psi.CreateNoWindow = true;
				psi.WindowStyle = ProcessWindowStyle.Hidden;
				psi.UseShellExecute = true;

				Console.WriteLine("\n Running as administrator: netsh " + args);
				try { Process.Start(psi).WaitForExit(); Console.WriteLine(" OK."); }
				catch(Exception ex) { Console.WriteLine(" Error: {0}",ex.Message); }
			}
			Console.WriteLine();

			Console.Write(" Do you want to open port {0} in Windows Firewall for inbound connections? (Y/N)", Port);
			if (Console.ReadKey().Key == ConsoleKey.Y)
			{
				string args = string.Format(@"advfirewall firewall add rule name=HTTP dir=in action=allow protocol=TCP localport={0}", Port);
				ProcessStartInfo psi;
				psi = new ProcessStartInfo("netsh", args);
				psi.Verb = "runas";
				psi.CreateNoWindow = true;
				psi.WindowStyle = ProcessWindowStyle.Hidden;
				psi.UseShellExecute = true;

				Console.WriteLine("\n Running as administrator: netsh " + args);
				try { Process.Start(psi).WaitForExit(); Console.WriteLine(" OK."); }
				catch (Exception ex) { Console.WriteLine(" Error: {0}", ex.Message); }
			}
			Console.WriteLine();

			Console.WriteLine("Windows Network Shell configuration completed.");
			Console.WriteLine();
			Log.WriteLine(false, false, "Windows NETSH configurtion \"wizard\" completed.");

		}

		/// <summary>
		/// Get all inner exception messages.
		/// </summary>
		/// <param name="Ex">The upper exception.</param>
		public static string GetFullExceptionMessage(Exception Ex, bool ExcludeTopLevel = false, bool IncludeOnlyLast = false)
		{
			string msg = string.Empty;
			Exception e = ExcludeTopLevel ? Ex.InnerException : Ex;
			while(e != null) 
			{
				if (IncludeOnlyLast) msg = e.Message;
				else msg += e.Message + "\n";
				e = e.InnerException;
			}
			return msg;
		}



		/// <summary>
		/// Convert string "true/false" or similar to bool true/false.
		/// </summary>
		/// <param name="s">One of these strings: 1/0, y/n, yes/no, on/off, enable/disable, true/false.</param>
		/// <returns>Boolean true/false</returns>
		/// <exception cref="InvalidCastException">Throws if the <paramref name="s"/> is not 1/0/y/n/yes/no/on/off/enable/disable/true/false.</exception>
		public static bool ToBoolean(string s)
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
