using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebOne
{
	public static class Program
	{
		public static LogWriter Log = new LogWriter();

		//private static HttpServer HTTPS;
		private static HttpServer1 PrimaryServer;
		private static HttpServer2 SecondaryServer;

		public const string ConfigFileAutoName = "**auto**webone.conf";
		public static string CustomConfigFile = "";
		public static string ConfigFileName = ConfigFileAutoName;
		public static string OverrideLogFile = "";
		public static int Port = -1;
		public static int Port2 = -1;
		public static int Load = 0;

		public static string Protocols = "HTTP 1.1";
		public static bool DaemonMode = false;
		static bool ShutdownInitiated = false;

		const string CmdLineArgUnnamed = "--wo-short";
		static List<KeyValuePair<string, string>> CmdLineOptions = new List<KeyValuePair<string, string>>();

		public static System.Net.Http.SocketsHttpHandler HTTPHandler = new();
		public static System.Net.Http.HttpClient HTTPClient = new(HTTPHandler);

		public const string DefaultPAC =
			"function FindProxyForURL(url, host){\n" +
			"if (url.substring(0, 5) == 'http:')\n" +
			"{ return 'PROXY %PACProxy%'; }\n" +
			"if (url.substring(0, 6) == 'https:')\n" +
			"{ return 'PROXY %PACProxy2%'; }\n" +
			"if (url.substring(0, 4) == 'ftp:')\n" +
			"{ return 'PROXY %PACProxy2%'; }\n" +
			"} /*WebOne PAC*/ ";

		public static X509Certificate2 RootCertificate;
		public static Dictionary<string, X509Certificate2> FakeCertificates = new();

		/// <summary>
		/// The entry point of webone.dll (WebOne.exe, /usr/local/bin/webone, ./webone)
		/// </summary>
		/// <param name="args">Command line arguments of WebOne.exe</param>
		static void Main(string[] args)
		{
			Variables.Add("WOVer",
			Assembly.GetExecutingAssembly().GetName().Version.Major + "." +
			Assembly.GetExecutingAssembly().GetName().Version.Minor + "." +
			Assembly.GetExecutingAssembly().GetName().Version.Build
			+ "-beta1"
			);
			Variables.Add("WOSystem", Environment.OSVersion.ToString());

			Console.Title = "WebOne";
			Console.WriteLine("WebOne HTTP Proxy Server {0}\nhttps://github.com/atauenis/webone\n\n", Variables["WOVer"]);

			//process command line arguments
			ProcessCommandLine(args);
			ConfigFileName = GetConfigurationFileName();

			//load configuration file and set port number
			try
			{
				ConfigFileLoader.LoadFile(ConfigFileName);
				ConfigFileLoader.ProcessConfiguration();
				if (Port < 1) Port = ConfigFile.Port; else ConfigFile.Port = Port;
				if (Port2 < 1) Port2 = ConfigFile.Port2; else ConfigFile.Port2 = Port2;
			}
			catch (Exception ConfigLoadException)
			{
				Console.WriteLine("Error while loading configuration: {0}", ConfigLoadException.Message);
				if (!DaemonMode) try
					{
						Console.WriteLine("\nPress any key to exit.");
						Console.ReadKey();
					}
					catch (InvalidOperationException) { /* prevent crash on non-interactive terminals */ }
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

			//initialize system HTTP socket message handler for static HttpClient used by HttpOperation class instances
			HTTPHandler.SslOptions.RemoteCertificateValidationCallback = CheckServerCertificate;
			HTTPHandler.AllowAutoRedirect = false;
			HTTPHandler.AutomaticDecompression = ConfigFile.AllowHttpCompression ? DecompressionMethods.All : DecompressionMethods.None;
			HTTPHandler.UseCookies = false;
			if (ConfigFile.UpperProxy != "")
			{
				if (ConfigFile.UpperProxy == "no" || ConfigFile.UpperProxy == "off" || ConfigFile.UpperProxy == "disable" || ConfigFile.UpperProxy == "false" || ConfigFile.UpperProxy == "direct")
				{
					HTTPHandler.UseProxy = false;
				}
				else
				{
					WebProxy UpperProxy = new(ConfigFile.UpperProxy);
					HTTPHandler.Proxy = UpperProxy;
				}
			}
			HTTPHandler.EnableMultipleHttp2Connections = ConfigFile.MultipleHttp2Connections;

			//set console window title
			if (!DaemonMode) Console.Title = "WebOne @ " + ConfigFile.DefaultHostName + ":" + ConfigFile.Port;

			if (ConfigFile.SslEnable)
			{
				//load or create & load SSL PEM (.crt & .key files) for CA (aka root certificate)
				try
				{
					const int MinPemLentgh = 52; //minimum size of PEM files - header&footer only
					bool HaveCrtKey = File.Exists(ConfigFile.SslCertificate) && File.Exists(ConfigFile.SslPrivateKey);
					if (HaveCrtKey) HaveCrtKey = (new FileInfo(ConfigFile.SslCertificate).Length > MinPemLentgh) && (new FileInfo(ConfigFile.SslPrivateKey).Length > MinPemLentgh);
					if (HaveCrtKey)
					{ Log.WriteLine(true, false, "Using as SSL Certificate Authority: {0}, {1}.", ConfigFile.SslCertificate, ConfigFile.SslPrivateKey); }
					else
					{
						Log.WriteLine(true, false, "Creating root SSL Certificate & Private Key for CA...");
						CertificateUtil.MakeSelfSignedCert(ConfigFile.SslCertificate, ConfigFile.SslPrivateKey);
						Log.WriteLine(true, false, "CA Certificate: {0};   Key: {1}.", ConfigFile.SslCertificate, ConfigFile.SslPrivateKey);
					}
					RootCertificate = new X509Certificate2(X509Certificate2.CreateFromPemFile(ConfigFile.SslCertificate, ConfigFile.SslPrivateKey).Export(X509ContentType.Pkcs12));
					Protocols += ", HTTPS 1.1";
				}
				catch (Exception CertCreateEx)
				{
					Log.WriteLine(true, false, "Unable to create CA Certificate: {0}.", CertCreateEx.Message);
					Log.WriteLine(true, false, CertCreateEx.StackTrace.Replace("\n", " ; ")); //only for debug purposes at this moment
					Log.WriteLine(true, false, "End of CA build error information. HTTPS won't be available!");
					ConfigFile.SslEnable = false;
				}
			}
			Protocols += ", CERN-compatible";

			Log.WriteLine(false, false, "Configured to http://{1}:{2}/, {3}", ConfigFileName, ConfigFile.DefaultHostName, ConfigFile.Port, Protocols);

			//initialize server
			try
			{
				PrimaryServer = new(ConfigFile.Port);
				SecondaryServer = new(ConfigFile.Port2);
			}
			catch (Exception ex)
			{
				Log.WriteLine(true, false, "Server initilize failed: {0}", ex.Message);
				Shutdown(ex.HResult);
				return;
			}

			//start the server from 1 or 2 attempts
			for (int StartAttempts = 0; StartAttempts < 2; StartAttempts++)
			{
				try
				{
					//Log.WriteLine(true, false, "Starting servers...");
					PrimaryServer.Start();
					SecondaryServer.Start();
					Console.WriteLine(" =3= Auto-configuration: http://{0}:{1}/auto.pac", ConfigFile.DefaultHostName, Port);
					Log.WriteLine(true, false, "Ready for incoming connections.");
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
								ConfigureWindowsNetShell(Port, Port2);
								continue;
							}
							else
							{
								Console.WriteLine("\nYou always can configure the system using instructions from WebOne wiki.");
								Shutdown(ex.NativeErrorCode);
								break;
							}
						}
					}
					Shutdown(ex.NativeErrorCode);
					break;
				}
				catch (Exception ex)
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
			//while (HTTPS.Working) { Thread.Sleep(250); }
			while (PrimaryServer.Working) { Thread.Sleep(250); }

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

			//if (HTTPS != null && HTTPS.Working) HTTPS.Stop();
			if (PrimaryServer != null && PrimaryServer.Working) PrimaryServer.Stop();
			if (SecondaryServer != null && SecondaryServer.Working) SecondaryServer.Stop();

			if (!DaemonMode && !Environment.HasShutdownStarted && !ShutdownInitiated) try
				{
					Console.WriteLine("\nPress any key to exit.");
					Console.ReadKey();
				}
				catch (InvalidOperationException) { /* prevent crash on non-interactive terminals */ }

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
				if (arg.StartsWith("-") || (Environment.OSVersion.Platform == PlatformID.Win32NT && arg.StartsWith("/")))
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
				// Console.WriteLine("Arg: '{0}' = '{1}'", kvp.Key, kvp.Value);
				switch (kvp.Key)
				{
					case "/cfg":
					case "-cfg":
					case "-config":
						CustomConfigFile = ExpandMaskedVariables(kvp.Value);
						break;
					case "/l":
					case "-l":
					case "--log":
						if (kvp.Value == "" || kvp.Value == "no") { OverrideLogFile = null; break; }
						OverrideLogFile = ExpandMaskedVariables(kvp.Value);
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
					case "--dump":
					case "--dump-headers":
					case "--dump-requests":
						//all will be processed in ProcessCommandLineOptions()
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
						if (string.IsNullOrWhiteSpace(kvp.Value)) break;

						if (int.TryParse(kvp.Value, out int CustomPort))
						{
							Port = CustomPort;
							Port2 = CustomPort + 1;
							Console.WriteLine("Using custom ports {0}, {1}.", Port, Port2);
							break;
						}

						CustomConfigFile = kvp.Value;
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
			switch (CP.ToLower())
			{
				case "windows":
				case "win":
				case "ansi":
					return CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);
				/* Microsoft Windows code pages:
				 * windows-1250	Czech, Polish, Slovak, Hungarian, Slovene, Serbo-Croatian, Montenegrian, Romanian (<1993), Gagauz, Rotokas, Albanian, English, German, Luxembourgish
				 * windows-1251	Russian, Ukrainian, Belarusian, Bulgarian, Serbian Cyrillic, Bosnian Cyrillic, Macedonian, Rusyn
				 * windows-1252	(All of ISO-8859-1 plus full support for French and Finnish)
				 * windows-1253 Greek
				 * windows-1254	Turkish
				 * windows-1255	Hebrew
				 * windows-1256	Arabic
				 * windows-1257	Estonian, Latvian, Lithuanian, Latgalian
				 * windows-1258 Vietnamese
				 * windows-874	Thai
				 */
				case "dos":
				case "oem":
				case "ascii":
					return CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
				/* MS-DOS, IBM OS/2 code pages:
				 * 437	Default: English, German, Swedish
				 * 720	Arabic in Egypt, Iraq, Jordan, Saudi Arabia, and Syria
				 * 737	Greek
				 * 775	Estonian, Lithuanian and Latvian
				 * 850	West European: at least Spanish, Italian, French
				 * 852	Bosnian, Croatian, Czech, Hungarian, Polish, Romanian, Moldavian, Serbian, Slovak or Slovene
				 * 855	Serbian, Macedonian and Bulgarian
				 * 857	Turkish
				 * 860	Portuguese (mostly - Brasilian)
				 * 861	Icelandic
				 * 862	Hebrew
				 * 863	French in Canada (mainly in Quebec province)
				 * 864	Arabic in Egypt, Iraq, Jordan, Saudi Arabia, and Syria (?)
				 * 865	Danish and Norwegian
				 * 866	Russian, Ukrainian, Byelarussian
				 * 874	Thai
				 * 932	Japan
				 * 936	Chinese simplified (PRC)
				 * 949	Korean
				 * 950	Chinese traditional (Taiwan island)
				 */
				case "mac":
				case "apple":
					return CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.MacCodePage);
				/* Apple MacOS (Classic) code pages:
				 * macintosh				(Latin default)
				 * x-mac-arabic
				 * x-mac-ce					(Czech, Slovak, Polish, Estonian, Latvian, Lithuanian)
				 * x-mac-chinesetrad		(Taiwan island)
				 * x-mac-croatian
				 * x-mac-cyrillic			(Russian, Bulgarian, Belarusian, Macedonian, Serbian)
				 * x-mac-greek
				 * x-mac-hebrew
				 * x-mac-icelandic
				 * x-mac-japanese
				 * x-mac-romanian			(Romanian & Moldavian)
				 * x-mac-thai
				 * x-mac-turkish
				 * x-mac-ukrainian
				 */
				case "ebcdic":
				case "ibm":
					/* Old IBM mainframes (EBCDIC) code pages:
					 * ---== To be written ==---
					 */
					return CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.EBCDICCodePage);
				case "iso":
				case "iso-8859":
				case "iso8859":
					CultureInfo ci = CultureInfo.CurrentCulture;
					switch (ci.TwoLetterISOLanguageName.ToLower())
					{
						default:
							/*
							 * ISO-8859-1 = Latin-1 (Western European)
							 * English, Faeroese, German, Icelandic, Irish, Italian, Norwegian, Portuguese, Rhaeto-Romanic, Scottish Gaelic, Spanish, Catalan, and Swedish
							 * Danish (partial), Dutch (partial), Finnish (partial), French (partial)
							 * Not supported on some macOS servers! To skip NULL return, use CP1252, which is 75% same as Latin-1.
							 */
							return CodePagesEncodingProvider.Instance.GetEncoding("iso-8859-1") ?? CodePagesEncodingProvider.Instance.GetEncoding("windows-1252");
						case "bs":
						case "pl":
						case "cr":
						case "cz":
						case "sk":
						case "sl":
						//case "sr":
						case "hu":
							/*
							 * ISO-8859-2 = Latin-2 (Central European)
							 * Bosnian, Polish, Croatian, Czech, Slovak, Slovene, Serbian Latin, and Hungarian
							 */
							return CodePagesEncodingProvider.Instance.GetEncoding("iso-8859-2");
						case "mt":
						case "eo":
							/*
							 * ISO-8859-3 = Latin-3 (South European)
							 * Maltese and Esperanto
							 */
							return CodePagesEncodingProvider.Instance.GetEncoding("iso-8859-3");
						case "kl":
						case "se":
							/*
							 * ISO-8859-4 = Latin-4 (North European)
							 * Greenlandic, and Sami
							 * Sometimes also Estonian, Latvian, Lithuanian
							 */
							return CodePagesEncodingProvider.Instance.GetEncoding("iso-8859-4");
						case "be":
						case "bu":
						case "mk":
						case "ru":
						case "sr":
						case "ua":
							/*
							 * ISO-8859-5 = Cyrillic
							 * Belarusian, Bulgarian, Macedonian, Russian, Serbian Cyrillic, and Ukrainian (partial)
							 */
							return CodePagesEncodingProvider.Instance.GetEncoding("iso-8859-5");
						case "ar":
							/*
							 * ISO-8859-6 = Arabic
							 * Arabic language
							 */
							return CodePagesEncodingProvider.Instance.GetEncoding("iso-8859-6");
						case "gr":
							/*
							 * ISO-8859-7 = Greek
							 * Modern Greek, Ancient Greek
							 */
							return CodePagesEncodingProvider.Instance.GetEncoding("iso-8859-7");
						case "hw":
							/*
							 * ISO-8859-8 = Hebrew
							 * Hebrew
							 */
							return CodePagesEncodingProvider.Instance.GetEncoding("iso-8859-8");
						case "tr":
							/*
							 * ISO-8859-9 = Latin-9 (Turkish)
							 * Turkish
							 */
							return CodePagesEncodingProvider.Instance.GetEncoding("iso-8859-9");
						//case "es":
						case "lv":
						case "lt":
							/*
							 * ISO-8859-13 - Latin-7 (Baltic Rim)
							 * Estonian, Latvian and Lithuanian
							 */
							return CodePagesEncodingProvider.Instance.GetEncoding("iso-8859-13");
						case "fr":
						case "fi":
						case "es":
							/*
							 * ISO-8859-15 - Latin-9 / Latin-0
							 * French, Finnish and Estonian.
							 */
							return CodePagesEncodingProvider.Instance.GetEncoding("iso-8859-15");
							/*
							 * ISO-8859 parts # 10, 11, 12, 14, 16 are not supported by .NET 6.0:
							 * 10 - Latin-6 (Nordic)
							 * 11 - Thai
							 * 12 - Devanagari
							 * 14 - Latin-8 (Celtic)
							 * 16 - Latin-10 (South-Eastern)
							*/
					}
				case "0":
				case "asis":
					return null;
				default:
					//parse from specified number or name
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
#pragma warning disable SYSLIB0001 // The UTF-7 encoding is insecure since .NET 5.0
									return Encoding.UTF7;
#pragma warning restore SYSLIB0001
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
							ConfigFile.TemporaryDirectory = ExpandMaskedVariables(kvp.Value);
							break;
						case "/p":
						case "-p":
						case "--port":
						case "--http-port":
							Port = Convert.ToInt32(kvp.Value);
							ConfigFile.Port = Convert.ToInt32(kvp.Value);
							break;
						case "/s":
						case "-s":
						case "--ftp-port":
						case "--https-port":
							Port2 = Convert.ToInt32(kvp.Value);
							ConfigFile.Port2 = Convert.ToInt32(kvp.Value);
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
						case "--dump":
						case "--dump-headers":
						case "--dump-requests":
							string DumpFilePath = "dump-%Url%.log";
							if (kvp.Value != "") DumpFilePath = kvp.Value;
							Log.WriteLine(true, false, "Will save all HTTP traffic to: {0}.", DumpFilePath);
							ConfigFileSection DumpSection = new ConfigFileSection("[Edit]", "[command line]");
							DumpSection.Options.Add(new ConfigFileOption("AddDumping=" + DumpFilePath, "[command line]"));
							ConfigFile.EditRules.Add(new EditSet(DumpSection));
							break;
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("Warning: Wrong argument '{1} {2}': {0}.", ex.Message, kvp.Key, kvp.Value);
				}
			}
		}

		/// <summary>
		/// Make info string (footer) for message pages
		/// </summary>
		/// <returns>HTML: WebOne vX.Y.Z on Windows NT 6.2.9200 Service Pack 6</returns>
		public static string GetInfoString()
		{
			return "<hr>WebOne Proxy Server " + Variables["WOVer"] + "<br>on " + Variables["WOSystem"];
		}


		/// <summary>
		/// Check a string for containing a something from list of patterns
		/// </summary>
		/// <param name="What">What string should be checked</param>
		/// <param name="For">Pattern to find</param>
		/// <param name="CaseInsensitive">Ignore character case when checking</param>
		public static bool CheckString(string What, string[] For, bool CaseInsensitive = false)
		{
			if (CaseInsensitive)
			{
				foreach (string str in For) { if (What.Contains(str, StringComparison.InvariantCultureIgnoreCase)) return true; }
				return false;
			}
			else
			{
				foreach (string str in For) { if (What.Contains(str)) return true; }
				return false;
			}
		}

		/// <summary>
		/// Check a string for containing a something from list of patterns
		/// </summary>
		/// <param name="What">What string should be checked</param>
		/// <param name="For">Pattern to find</param>
		/// <param name="CaseInsensitive">Ignore character case when checking</param>
		public static bool CheckString(string What, List<string> For, bool CaseInsensitive = false)
		{
			return CheckString(What, For.ToArray(), CaseInsensitive);
		}

		/// <summary>
		/// Check a string array for containing a pattern
		/// </summary>
		/// <param name="Where">Where the search should be do</param>
		/// <param name="For">Pattern to find</param>
		/// <param name="CaseInsensitive">Ignore character case when checking</param>
		public static bool CheckString(string[] Where, string For, bool CaseInsensitive = false)
		{
			if (CaseInsensitive)
			{
				foreach (string str in Where) { if (str.Contains(For, StringComparison.InvariantCultureIgnoreCase)) return true; }
				return false;
			}
			else
			{
				foreach (string str in Where) { if (str.Contains(For)) return true; }
				return false;
			}
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
			return BeginTime.ToString("dd.MM.yyyy HH:mm:ss.fff") + "+" + difference.Ticks / 2;
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
		/// Get CPU load for process.
		/// </summary>
		/// <param name="process">The process.</param>
		/// <returns>CPU usage in percents.</returns>
		internal static double GetUsage(Process process)
		{
			//thx to: https://stackoverflow.com/a/49064915/7600726
			//see also https://www.mono-project.com/archived/mono_performance_counters/

			if (process.HasExited) return double.MinValue;

			// Preparing variable for application instance name
			string name = "";

			foreach (string instance in new PerformanceCounterCategory("Process").GetInstanceNames())
			{
				if (process.HasExited) return double.MinValue;
				if (instance.StartsWith(process.ProcessName))
				{
					using (PerformanceCounter processId = new PerformanceCounter("Process", "ID Process", instance, true))
					{
						if (process.Id == (int)processId.RawValue)
						{
							name = instance;
							break;
						}
					}
				}
			}

			PerformanceCounter cpu = new PerformanceCounter("Process", "% Processor Time", name, true);

			// Getting first initial values
			cpu.NextValue();

			// Creating delay to get correct values of CPU usage during next query
			Thread.Sleep(500);

			if (process.HasExited) return double.MinValue;
			return Math.Round(cpu.NextValue() / Environment.ProcessorCount, 2);
		}

		/// <summary>
		/// Check a process for idle state (long period of no CPU load) and kill if it's idle.
		/// </summary>
		/// <param name="Proc">The process.</param>
		/// <param name="AverageLoad">Average CPU load by the process.</param>
		internal static void PreventProcessIdle(ref Process Proc, ref float AverageLoad, LogWriter Log)
		{
			AverageLoad = (float)(AverageLoad + GetUsage(Proc)) / 2;

			if (!Proc.HasExited)
				if (Math.Round(AverageLoad, 6) <= 0 && !Proc.HasExited)
				{
					//the process is counting crows. Fire!
					Proc.Kill();
					if (Console.GetCursorPosition().Left > 0) Console.WriteLine();
					Log.WriteLine(" Idle process {0} killed.", Proc.ProcessName);
				}
		}


		/// <summary>
		/// Fill %masks% on an URI template
		/// </summary>
		/// <param name="MaskedURL">URI template</param>
		/// <param name="PossibleURL">Previous URI (for "%URL%" mask and similar)</param>
		/// <param name="DontTouchURL">Do not edit previous URI (%URL% mask) in any cases</param>
		/// <param name="AdditionalVariables">Additional %masks% which can be processed</param>
		/// <returns>Ready URL</returns>
		public static string ProcessUriMasks(string MaskedURL, string PossibleURL = "http://webone.github.io:80/index.htm", bool DontTouchURL = false, Dictionary<string, string> AdditionalVariables = null)
		{
			//this function should be rewritten or removed in future,
			//when will implement more powerful manipulation of headers, cookies, content. (v0.12?)

			string str = MaskedURL;
			string URL = null;
			if (CheckString(PossibleURL, ConfigFile.ForceHttps) && !DontTouchURL)
				URL = new UriBuilder(PossibleURL) { Scheme = "https" }.Uri.ToString();
			else
				URL = PossibleURL;

			UriBuilder builder = new UriBuilder(URL);

			var UrlVars = new Dictionary<string, string>
			{
				{ "URL", URL },
				{ "Url", Uri.EscapeDataString(URL) },
				{ "UrlDomain", builder.Host },
				{ "UrlNoDomain", (builder.Query == "" ? builder.Path : builder.Path + "?" + builder.Query) },
				{ "UrlNoQuery", builder.Scheme + "://" + builder.Host + "/" +  builder.Path },
				{ "UrlNoPort", builder.Scheme + "://" + builder.Host + "/" + (builder.Query == "" ? builder.Path : builder.Path + "?" + builder.Query) },
				{ "UrlHttps", "https://" + builder.Host + "/" + (builder.Query == "" ? builder.Path : builder.Path + "?" + builder.Query) },
				{ "UrlHttp", "http://" + builder.Host + "/" + (builder.Query == "" ? builder.Path : builder.Path + "?" + builder.Query) }
			};
			if (AdditionalVariables != null) foreach (var entry in AdditionalVariables) { UrlVars.TryAdd(entry.Key, entry.Value); }
			str = ExpandMaskedVariables(str, UrlVars);
			return str;
		}

		/// <summary>
		/// Internal variables which can be used in strings
		/// </summary>
		public static Dictionary<string, string> Variables = new Dictionary<string, string>();

		/// <summary>
		/// Replace all environment/WebOne variable %masks% ($masks) in a string with their real values
		/// </summary>
		/// <param name="MaskedString">A string with some %masks% inside</param>
		/// <param name="AdditionalVariables">Additional variables, which also can be used in masked string</param>
		/// <returns>A string with real variable values</returns>
		public static string ExpandMaskedVariables(string MaskedString, Dictionary<string, string> AdditionalVariables = null)
		{
			//Workaround for https://github.com/dotnet/runtime/issues/25792
			//So this is a better version of Environment.ExpandEnvironmentVariables(String)
			//where both UNIX ($EnvVar) and DOS (%EnvVar%) syntaxes are allowed, and %TEMP% and $TMPDIR are synonyms.
			//Also any WebOne internal variables can be used here.

			string str = MaskedString, tempdir = Path.GetTempPath(), logdir = GetDefaultLogDirectory();
			str = str.Replace("$TMPDIR", tempdir).Replace("%TEMP%", tempdir, StringComparison.CurrentCultureIgnoreCase);
			str = str.Replace("$SYSLOGDIR", logdir).Replace("%SYSLOGDIR%", logdir, StringComparison.CurrentCultureIgnoreCase);

			//get custom variables (e.g. HTTP headers, etc)
			Dictionary<string, string> AddVars = new Dictionary<string, string>(Variables);
			if (AdditionalVariables != null) foreach (var entry in AdditionalVariables) { AddVars.TryAdd(entry.Key, entry.Value); }
			foreach (KeyValuePair<string, string> Var in AddVars)
			{
				str = str
				.Replace("%" + (string)Var.Key + "%", (string)Var.Value)
				.Replace("$" + (string)Var.Key, (string)Var.Value);
			}

			//get environment variables and home directory
			if (Environment.OSVersion.Platform == PlatformID.Unix)
			{
				foreach (System.Collections.DictionaryEntry EnvVar in Environment.GetEnvironmentVariables())
				{
					str = str
					.Replace("%" + (string)EnvVar.Key + "%", (string)EnvVar.Value, StringComparison.CurrentCultureIgnoreCase)
					.Replace("$" + (string)EnvVar.Key, (string)EnvVar.Value);
				}
				str = str.Replace("~/", Environment.SpecialFolder.UserProfile + "/");
			}
			else
			{
				str = Environment.ExpandEnvironmentVariables(str);
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
			return ExpandMaskedVariables(ConfigFile.UserAgent, new Dictionary<string, string> { { "Original", ClientUA ?? "Mozilla/5.0 (Kundryuchy-Leshoz)" } });
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

			if (CustomConfigFile != "")
			{
				if (File.Exists(CustomConfigFile))
				{
					CustomConfigFile = new FileInfo(CustomConfigFile).FullName;
					Console.WriteLine("Using custom configuration from {0}.", CustomConfigFile);
					return CustomConfigFile;
				}
				else
				{
					Console.WriteLine("ERROR: Custom configuration file is not found: {0}.", CustomConfigFile);
					if (!DaemonMode) try
						{
							Console.WriteLine("\nPress any key to exit.");
							Console.ReadKey();
						}
						catch (InvalidOperationException) { /* prevent crash on non-interactive terminals (#87) */ }
					Environment.Exit(0);
				}
			}

			switch (Environment.OSVersion.Platform)
			{
				default:
				case PlatformID.Unix:
					CurrentDirConfigFile = "./webone.conf";
					DefaultConfigFile = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName + "/webone.conf";
					UserConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.config/webone/webone.conf";
					CommonConfigFile = "/etc/webone.conf";

					string MacUserConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Library/Application Support/WebOne/webone.conf";
					string MacCommonConfigFile = "/Library/Application Support/WebOne/webone.conf";
					if (File.Exists(MacUserConfigFile)) UserConfigFile = MacUserConfigFile;
					if (File.Exists(MacCommonConfigFile)) CommonConfigFile = MacCommonConfigFile;
					break;
				case PlatformID.Win32NT:
					CurrentDirConfigFile = @".\webone.conf";
					DefaultConfigFile = new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName + @"\webone.conf";
					UserConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\WebOne\webone.conf";
					CommonConfigFile = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) + @"\WebOne\webone.conf";
					break;
			}

#if DEBUG
			UserConfigFile = DefaultConfigFile; //debug versions weren't deb/rpm/zip-packaged so they can use old-style webone.conf placement
#endif

			//try to load custom configuration file (if any)
			if (ConfigFileName != ConfigFileAutoName) return ConfigFileName; //unreachable

			//try to load webone.conf from current directory
			if (File.Exists(CurrentDirConfigFile)) return CurrentDirConfigFile;

			//try to load webone.conf from application's directory
			if (File.Exists(DefaultConfigFile)) return DefaultConfigFile;

			//try to load webone.conf from user configuration directory
			if (File.Exists(UserConfigFile)) return UserConfigFile;

			//try to load webone.conf from common configuration directory
			if (File.Exists(CommonConfigFile)) return CommonConfigFile;

			throw new Exception("Cannot guess configuration file name. Please run WebOne with /full/path/to/webone.conf specifed or create webone.conf in program directory.");
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
					if (Environment.OSVersion.Version.Major < 10) //Linux
						return "/var/log";
					else //macOS or Darwin
						return Environment.GetEnvironmentVariable("HOME") + "/Library/Logs";
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
			return ExpandMaskedVariables(LogFilePath);
		}

		/// <summary>
		/// Configure Windows Network Shell (netsh) to allow use TCP/IP <paramref name="Port"/> without admin rights and then open Windows Firewall
		/// </summary>
		/// <param name="Port1">WebOne HTTP port number</param>
		/// <param name="Port2">WebOne HTTPS/FTP port number</param>
		public static void ConfigureWindowsNetShell(int Port1, int Port2)
		{
			//fix for https://github.com/atauenis/webone/issues/14
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
				throw new InvalidOperationException("Only for MS Windows");

			string address, NTdomain, NTuser;
			address = string.Format("http://*:{0}/", Port1);
			NTdomain = Environment.UserDomainName;
			NTuser = Environment.UserName;

			Console.WriteLine();
			Console.WriteLine();
			Log.WriteLine(false, false, "Started NETSH configurtion \"wizard\". Logging is not implemented here.");

			Console.Write(" Do you want to add system rule allowing user {0} to run WebOne on port {1}? (Y/N)", "\"" + NTdomain + "\\" + NTuser + "\"", Port1);
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
				catch (Exception ex) { Console.WriteLine(" Error: {0}", ex.Message); }
			}
			Console.WriteLine();

			Console.Write(" Do you want to open port {0} in Windows Firewall for inbound connections? (Y/N)", Port1);
			if (Console.ReadKey().Key == ConsoleKey.Y)
			{
				string args = string.Format(@"advfirewall firewall add rule name=WebOneHTTP dir=in action=allow protocol=TCP localport={0}", Port1);
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

			Console.Write(" Do you want to open port {0} in Windows Firewall for inbound connections? (Y/N)", Port2);
			if (Console.ReadKey().Key == ConsoleKey.Y)
			{
				string args = string.Format(@"advfirewall firewall add rule name=WebOneHTTPS dir=in action=allow protocol=TCP localport={0}", Port2);
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
			while (e != null)
			{
				if (IncludeOnlyLast) msg = e.Message;
				else msg += e.Message + "\n";
				e = e.InnerException;
			}
			return msg;
		}

		/// <summary>
		/// Check remote HTTPS server certificate
		/// </summary>
		/// <returns>Rate of certificate goodness</returns>
		/// <exception cref="TlsPolicyErrorException">If ValidateCertificates configuration option is enabled and if the certificate is broken, this exception raises.</exception>
		public static bool CheckServerCertificate(object sender, X509Certificate certification, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			if (sslPolicyErrors != SslPolicyErrors.None)
				Log.WriteLine(" Danger: {0}", sslPolicyErrors.ToString());

			if (!ConfigFile.ValidateCertificates) return true;
			if (sslPolicyErrors == SslPolicyErrors.None)
				return true;
			throw new TlsPolicyErrorException(sender as SslStream, certification, chain, sslPolicyErrors);
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
			string[] falseStrings = { "0", "n", "no", "off", "disable", "false", "", null };


			if (trueStrings.Contains(s, StringComparer.OrdinalIgnoreCase))
				return true;
			if (falseStrings.Contains(s, StringComparer.OrdinalIgnoreCase))
				return false;

			throw new InvalidCastException("Only the following are supported for converting strings to boolean: "
				+ string.Join(",", trueStrings)
				+ " and "
				+ string.Join(",", falseStrings)
				.Replace(",,", "")); //hide empty & null
		}

	}
}
