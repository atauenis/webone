using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Configuration file entries
	/// </summary>
	static class ConfigFile
	{
		private static LogWriter Log = new LogWriter();

		/// <summary>
		/// TCP port that should be used by the Proxy Server
		/// </summary>
		public static int Port = 80;

		/// <summary>
		/// List of domains that should be open only using HTTPS
		/// </summary>
		public static List<string> ForceHttps = new List<string>();

		/// <summary>
		/// List of URLs that should be always downloaded as UTF-8
		/// </summary>
		public static List<string> ForceUtf8 = new List<string>();

		/// <summary>
		/// List of parts of Content-Types that describing text files
		/// </summary>
		public static List<string> TextTypes = new List<string>(){ "text/", "javascript"};

		/// <summary>
		/// Encoding to be used in output content
		/// </summary>
		public static Encoding OutputEncoding = Encoding.Default;

		/// <summary>
		/// Credentials for proxy authentication
		/// </summary>
		public static string Authenticate = "";

		/// <summary>
		/// (Legacy) List of URLs that should be always 302ed
		/// </summary>
		public static List<string> FixableURLs = new List<string>();

		/// <summary>
		/// (Legacy) Dictionary of URLs that should be always 302ed if they're looks like too new JS frameworks
		/// </summary>
		public static Dictionary<string, Dictionary<string, string>> FixableUrlActions =  new Dictionary<string, Dictionary<string, string>>();

		/// <summary>
		/// (Legacy) List of Content-Types that should be always 302ed
		/// </summary>
		public static List<string> FixableTypes = new List<string>();

		/// <summary>
		/// (Legacy) Dictionary of Content-Types that should be always 302ed to converter
		/// </summary>
		public static Dictionary<string, Dictionary<string, string>> FixableTypesActions = new Dictionary<string, Dictionary<string, string>>();

		/// <summary>
		/// (Legacy) List of possible content patches
		/// </summary>
		public static List<string> ContentPatches = new List<string>();

		/// <summary>
		/// (Legacy) Dictionary of possible content patches
		/// </summary>
		public static Dictionary<string, Dictionary<string, string>> ContentPatchActions = new Dictionary<string, Dictionary<string, string>>();

		/// <summary>
		/// List of domains where 302 redirections should be passed through .NET FW
		/// </summary>
		public static List<string> InternalRedirectOn = new List<string>();

		/// <summary>
		/// Hide "Can't read from client" and "Cannot return reply to the client" error messages in log
		/// </summary>
		public static bool HideClientErrors = false;

		/// <summary>
		/// Search for copies of removed sites in web.archive.org
		/// </summary>
		public static bool SearchInArchive = false;

		/// <summary>
		/// Make Web.Archive.Org error messages laconic (for retro browsers)
		/// </summary>
		public static bool ShortenArchiveErrors = false;

		/// <summary>
		/// List of enabled file format converters
		/// </summary>
		public static List<Converter> Converters = new List<Converter>();

		/// <summary>
		/// User-agent string of the Proxy
		/// </summary>
		public static string UserAgent = "%Original% WebOne/%WOVer%";

		/// <summary>
		/// Proxy default host name (or IP)
		/// </summary>
		public static string DefaultHostName = Environment.MachineName;

		/// <summary>
		/// Break network operations when remote TLS certificate is bad
		/// </summary>
		public static bool ValidateCertificates = true;

		/// <summary>
		/// List of traffic edit sets
		/// </summary>
		public static List<EditSet> EditRules = new List<EditSet>(); //how about to rename to EditSets?

		/// <summary>
		/// Table for alphabet transliteration
		/// </summary>
		public static List<KeyValuePair<string, string>> TranslitTable = new List<KeyValuePair<string, string>>();

		/// <summary>
		/// Temporary files' directory
		/// </summary>
		public static string TemporaryDirectory = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;

		/// <summary>
		/// Allow or disallow clients see webone.conf content
		/// </summary>
		public static bool AllowConfigFileDisplay = true;

		/// <summary>
		/// Set status page display style: no, short, full
		/// </summary>
		public static string DisplayStatusPage = "full";



		// Hint: All parser-related stuff known from v0.2.0 - 0.10.7 has been rewritten and moved to ConfigFileLoader class.
		//       Don't look for the parser here.



		// The code below is obsolete, and probably too will be moved to trash soon.

		/// <summary>
		/// Convert legacy FixableURL sections to new syntax (edit sets)
		/// </summary>
		private static void AddLegacyFixableURLs()
		{
			foreach(string FixUrl in FixableURLs)
			{
				List<string> RawES = new List<string>();
				RawES.Add("OnUrl="+FixUrl);

				foreach (KeyValuePair<string, string> FixUrlAct in FixableUrlActions[FixUrl])
				{
					switch (FixUrlAct.Key.ToLower())
					{
						case "validmask":
							RawES.Add("IgnoreUrl=" + FixUrlAct.Value);
							continue;
						case "redirect":
							RawES.Add("AddRedirect=" + FixUrlAct.Value);
							continue;
						case "internal":
							if(FixUrlAct.Value.ToLower() == "yes" && FixableUrlActions[FixUrl].ContainsKey("Redirect"))
							RawES.Add("AddInternalRedirect=" + FixableUrlActions[FixUrl]["Redirect"]);
							continue;
						default:
							Log.WriteLine(true, false, "Unknown legacy FixableURL option: {0}", FixUrlAct.Key);
							continue;
					}
				}
				EditRules.Add(new EditSet(RawES));
				//Log.WriteLine(true, false, new EditSet(RawES));
			}
		}

		/// <summary>
		/// Convert legacy FixableType sections to new syntax (edit sets)
		/// </summary>
		private static void AddLegacyFixableTypes()
		{
			foreach (string FixT in FixableTypes)
			{
				List<string> RawES = new List<string>();
				RawES.Add("OnContentType=" + FixT);
				RawES.Add("OnCode=2");

				foreach (KeyValuePair<string, string> FixTAct in FixableTypesActions[FixT])
				{
					switch (FixTAct.Key.ToLower())
					{
						case "ifurl":
							RawES.Add("OnUrl=" + FixTAct.Value);
							continue;
						case "noturl":
							RawES.Add("IgnoreUrl=" + FixTAct.Value);
							continue;
						case "redirect":
							RawES.Add("AddRedirect=" + FixTAct.Value);
							continue;
						default:
							Log.WriteLine(true, false, "Unknown legacy FixableType option: {0}", FixTAct.Key);
							continue;
					}
				}
				EditRules.Add(new EditSet(RawES));
				//Log.WriteLine(true, false, new EditSet(RawES));
			}
		}


		/// <summary>
		/// Convert legacy ContentPatch sections to new syntax (edit sets)
		/// </summary>
		private static void AddLegacyContentPatches()
		{
			foreach (string Patch in ContentPatches)
			{
				List<string> RawES = new List<string>();
				RawES.Add("AddFind=" + Patch);
				RawES.Add("OnCode=2");

				foreach (KeyValuePair<string, string> PatchAct in ContentPatchActions[Patch])
				{
					switch (PatchAct.Key.ToLower())
					{
						case "replace":
							RawES.Add("AddReplace=" + PatchAct.Value);
							continue;
						case "ifurl":
							RawES.Add("OnUrl=" + PatchAct.Value);
							continue;
						case "iftype":
							RawES.Add("OnContentType=" + PatchAct.Value);
							continue;
						default:
							Log.WriteLine(true, false, "Unknown legacy ContentPatch option: {0}", PatchAct.Key);
							continue;
					}
				}
				EditRules.Add(new EditSet(RawES));
				//Log.WriteLine(true, false, new EditSet(RawES));
			}
		}
	}
}
