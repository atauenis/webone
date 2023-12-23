﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Authentication;
using System.Security.Cryptography;
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

		/// <summary>
		/// Allow multiple HTTP/2.0 connections to servers (faster, but may overload remote servers)
		/// </summary>
		public static bool MultipleHttp2Connections = true;

		/// <summary>
		/// Version of HTTP(S) protocol, used to communicate with servers
		/// </summary>
		public static string RemoteHttpVersion = "auto";

		/// <summary>
		/// Options for online video converting
		/// </summary>
		public static Dictionary<string, string> WebVideoOptions = new();

		/// <summary>
		/// Allow gz/deflate/br compression of remote HTTP connections
		/// </summary>
		public static bool AllowHttpCompression = true;

		/// <summary>
		/// MIME Content-Types for known file extensions
		/// </summary>
		public static Dictionary<string, string> MimeTypes = new();

		/// <summary>
		/// Enable built-in Web-FTP client
		/// </summary>
		public static bool EnableWebFtp = true;

		/// <summary>
		/// Use Microsoft HTTPAPI (HttpListener) for processing incoming traffic
		/// </summary>
		public static bool UseMsHttpApi = false;

		/// <summary>
		/// List of user agents without HTTP/1.1 support (work as HTTP/1.0)
		/// </summary>
		public static List<string> Http10Only = new();

		/// <summary>
		/// Enable http://proxy/!convert/?util=convert&url=url&dest=gif&type=image/gif tool page
		/// </summary>
		public static bool EnableManualConverting = true;



		/// <summary>
		/// Enable work with CONNECT method (enable HTTPS Proxy)
		/// </summary>
		public static bool SslEnable = true;

		/// <summary>
		/// Path to SSL/TLS Certificate (used as CA for fake certificates)
		/// </summary>
		public static string SslCertificate = "ssl.crt";

		/// <summary>
		/// Path to SSL/TLS Certificate private key (used as CA for fake certificates)
		/// </summary>
		public static string SslPrivateKey = "ssl.key";

		/// <summary>
		/// Protocols used in SSL/TLS tunnels through this Secure proxy
		/// </summary>
		public static SslProtocols SslProtocols = SslProtocols.None;

		/// <summary>
		/// Certificate hashing algorithm used in CA and sites certificates
		/// </summary>
		public static HashAlgorithmName SslHashAlgorithm = HashAlgorithmName.SHA1;

		/// <summary>
		/// Certificate subject to used in CA certificate at time of its generating
		/// </summary>
		public static string SslRootSubject = CertificateUtil.DefaultCASubject;

		/// <summary>
		/// Date after which the CA certificate should be considered valid. Used only when generating it
		/// </summary>
		public static DateTimeOffset SslRootValidAfter;

		/// <summary>
		/// Date before which the CA certificate should be considered valid. Used only when generating it
		/// </summary>
		public static DateTimeOffset SslRootValidBefore;

		/// <summary>
		/// Days before current day when site certificates are considered valid
		/// </summary>
		public static int SslCertVaildBeforeNow = -7;

		/// <summary>
		/// Days after current day when site certificates are considered valid
		/// </summary>
		public static int SslCertVaildAfterNow = 7;

		/// <summary>
		/// Path to directory with fake certificates for sites
		/// </summary>
		public static string SslSiteCerts = "";

		/// <summary>
		/// Command which creates fake site certificates
		/// </summary>
		public static string SslSiteCertGenerator = "";


		/// <summary>
		/// Allow using CONNECT method to connect to non-HTTPS servers
		/// </summary>
		public static bool AllowNonHttpsCONNECT = true;

		/// <summary>
		/// List of non-HTTPS servers with TLS, which can be accessed via CONNECT method
		/// </summary>
		public static List<string> NonHttpSslServers = new();

		/// <summary>
		/// Redirect these connections for non-HTTP protocols
		/// </summary>
		public static Dictionary<string, string> NonHttpConnectRedirect = new();



		// Hint: All parser-related stuff known from v0.2.0 - 0.10.7 has been rewritten and moved to ConfigFileLoader class.
		//       Don't look for the parser here.



	}
}
