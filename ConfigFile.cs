using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WebOne
{
	/// <summary>
	/// Configuration file entries
	/// </summary>
	static class ConfigFile
	{
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
		public static List<string> TextTypes = new List<string>() { "text/", "javascript" };

		/// <summary>
		/// Encoding to be used in output content
		/// </summary>
		public static Encoding OutputEncoding = Encoding.Default;

		/// <summary>
		/// Credentials for proxy authentication
		/// </summary>
		public static List<string> Authenticate = new List<string>();

		/// <summary>
		/// (Legacy) List of URLs that should be always 302ed
		/// </summary>
		public static List<string> FixableURLs = new List<string>();

		/// <summary>
		/// (Legacy) Dictionary of URLs that should be always 302ed if they're looks like too new JS frameworks
		/// </summary>
		public static Dictionary<string, Dictionary<string, string>> FixableUrlActions = new Dictionary<string, Dictionary<string, string>>();

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
		/// Hide "Can't read from client" and "Cannot return reply to the client" error messages in log
		/// </summary>
		public static bool HideClientErrors = false;

		/// <summary>
		/// Search for copies of removed sites in web.archive.org
		/// </summary>
		public static bool SearchInArchive = false;

		/// <summary>
		/// Hide HTTP 302 redirect to web.archive.org, and continue use original URL (but retrieve archived copy)
		/// </summary>
		public static bool HideArchiveRedirect = false;

		/// <summary>
		/// Set suffix of timestamp in web.archive.org URLs ("fw_" = hide toolbar, "id_" = original links, "" = default)
		/// </summary>
		public static string ArchiveUrlSuffix = "";

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
		/// Set status page display style: no, short, full
		/// </summary>
		public static string DisplayStatusPage = "full";

		/// <summary>
		/// Proxy authentication request page text
		/// </summary>
		public static string AuthenticateMessage = "<p>Hello! This Web 2.0-to-1.0 proxy server is private. Please reload this page and enter your credentials in browser's pop-up window.</p>";

		/// <summary>
		/// Proxy authentication realm (request message text)
		/// </summary>
		public static string AuthenticateRealm = "WebOne";

		/// <summary>
		/// List of banned client IP addresses
		/// </summary>
		public static List<string> IpBanList = new();

		/// <summary>
		/// List of allowed client IP addresses
		/// </summary>
		public static List<string> IpWhiteList = new();

		/// <summary>
		/// List of disallowed URLs
		/// </summary>
		public static List<string> UrlBlackList = new();

		/// <summary>
		/// List of only allowed URLs
		/// </summary>
		public static List<string> UrlWhiteList = new();

		/// <summary>
		/// List of the proxy server's host names
		/// </summary>
		public static List<string> HostNames = new();

		/// <summary>
		/// Upper limit of Web Archive page date
		/// </summary>
		public static int ArchiveDateLimit = 0;

		/// <summary>
		/// Another proxy server, used by WebOne to connect to Internet
		/// </summary>
		public static string UpperProxy = "";

		/// <summary>
		/// Proxy Automatic Configuration script
		/// </summary>
		public static string PAC = "";

		/// <summary>
		/// Internal pages style in HTML format (TEXT="#000000" BGCOLOR="#C0C0C0" LINK="#0000EE" VLINK="#551A8B" ALINK="#FF0000")
		/// </summary>
		public static string PageStyleHtml = "";
		/// <summary>
		/// Internal pages style in CSS format (body { background-color: #C0C0C0; color: #000000; })
		/// </summary>
		public static string PageStyleCss = "";



		// Hint: All parser-related stuff known from v0.2.0 - 0.10.7 has been rewritten and moved to ConfigFileLoader class.
		//       Don't look for the parser here.



	}
}
