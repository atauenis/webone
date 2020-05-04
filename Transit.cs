using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Threading;
using static WebOne.Program;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace WebOne
{
/// <summary>
/// Transit transfer of content with applying some edits
/// </summary>
	class Transit
	{
		HttpListenerRequest ClientRequest;
		HttpListenerResponse ClientResponse;
		DateTime BeginTime;


		byte[] UTF8BOM = Encoding.UTF8.GetPreamble();

		Uri RequestURL = new Uri("about:blank");
		static string LastURL = "http://999.999.999.999/CON";
		static HttpStatusCode LastCode = HttpStatusCode.OK;
		static string LastContentType = "not-a-carousel";
		bool ShouldRedirectInNETFW = false;
		List<EditSet> EditSets = new List<EditSet>();
		bool Stop = false;
		bool LocalMode = false;

		HttpOperation operation;
		int ResponseCode = 502;
		string ResponseBody = ":(";
		Stream TransitStream = null;
		string ContentType = "text/plain";
		Encoding ContentEncoding = Encoding.Default;


		/// <summary>
		/// Convert a Web 2.0 page to Web 1.0-like page.
		/// </summary>
		/// <param name="ClientRequest">Request from HttpListener</param>
		/// <param name="ClientResponse">Response for HttpListener</param>
		/// <param name="BeginTime">Initial time (for log)</param>
		public Transit(HttpListenerRequest ClientRequest, HttpListenerResponse ClientResponse, DateTime BeginTime)
		{
			this.ClientRequest = ClientRequest;
			this.ClientResponse = ClientResponse;
			this.BeginTime = BeginTime;
			#if DEBUG
			Console.WriteLine("{0}\t Begin process.", GetTime(BeginTime));
			#endif
			try
			{

				ShouldRedirectInNETFW = false;

				//check for login to proxy if need
				if (ConfigFile.Authenticate != "")
				{
					if (ClientRequest.Headers["Proxy-Authorization"] == null || ClientRequest.Headers["Proxy-Authorization"] == "")
					{
						Console.WriteLine("{0}\t Unauthorized client.", GetTime(BeginTime));
						ClientResponse.AddHeader("Proxy-Authenticate", @"Basic realm=""Log in to WebOne""");
						SendError(407, "Hello! This Web 2.0-to-1.0 proxy server is private. Please enter your credentials.");
						return;
					}
					else
					{
						string auth = Encoding.Default.GetString(Convert.FromBase64String(ClientRequest.Headers["Proxy-Authorization"].Substring(6)));
						if (auth != ConfigFile.Authenticate)
						{
							Console.WriteLine("{0}\t Incorrect login: '{1}'.", GetTime(BeginTime), auth);
							ClientResponse.AddHeader("Proxy-Authenticate", @"Basic realm=""Your WebOne credentials are incorrect""");
							SendError(407, "Your password is not correct. Please try again.");
							return;
						}
					}
				}

				//get request URL and referer URL
				string RefererUri = ClientRequest.Headers["Referer"];
				try { RequestURL = new UriBuilder(ClientRequest.RawUrl).Uri; }
				catch { RequestURL = ClientRequest.Url; };

				//check for local or internal URL
				bool IsLocalhost = false;

				foreach (IPAddress address in GetLocalIPAddresses())
					if (RequestURL.Host.ToLower() == address.ToString().ToLower())
						IsLocalhost = true;

				if (RequestURL.Host.ToLower() == "localhost" || RequestURL.Host.ToLower() == Environment.MachineName.ToLower() || RequestURL.Host == "127.0.0.1" || RequestURL.Host.ToLower() == "wpad" || RequestURL.Host.ToLower() == ConfigFile.DefaultHostName.ToLower() || RequestURL.Host == "")
					IsLocalhost = true;

				if (IsLocalhost)
				{
					bool PAC = false;
					string[] PacUrls = { "/auto/", "/auto", "/auto.pac", "/wpad.dat", "/wpad.da" }; //Netscape PAC/Microsoft WPAD
					foreach (string PacUrl in PacUrls) { if (RequestURL.LocalPath == PacUrl) { PAC = true; break; } }

					if (RequestURL.PathAndQuery.StartsWith("/!") || PAC || RequestURL.AbsolutePath == "/")
					{
						try
						{ 
							//internal URLs
							Console.WriteLine("{0}\t Internal: {1} ", GetTime(BeginTime), RequestURL.PathAndQuery);
							switch (RequestURL.AbsolutePath.ToLower())
							{
								case "/":
								case "/!":
								case "/!/":
									string HelpString = "This is <b>" + Environment.MachineName + ":" + ConfigFile.Port + "</b>.<br>";
									HelpString +="Used memory: <b>" + (double)Environment.WorkingSet/1024/1024 + "</b> MB.<br>";
									HelpString += "Pending requests: <b>" + (Program.Load - 1) + "</b>.<br>";
									HelpString += "Available security: <b>" + ServicePointManager.SecurityProtocol + "</b> (" + (int)ServicePointManager.SecurityProtocol + ").<br>";

									HelpString += "<h2>Aliases:</h2><ul>";
									bool EvidentAlias = false;
									foreach (IPAddress address in GetLocalIPAddresses())
									{
										HelpString += "<li>" + (address.ToString() == ConfigFile.DefaultHostName ? "<b>" + address.ToString() + "</b>" : address.ToString()) + ":" + ConfigFile.Port + "</li>";
										if(!EvidentAlias) EvidentAlias = address.ToString() == ConfigFile.DefaultHostName;
									}
									if (!EvidentAlias) HelpString += "<li><b>" + ConfigFile.DefaultHostName + "</b>:" + ConfigFile.Port + "</li>";
									HelpString += "</ul>";
									HelpString += "</ul>";

									HelpString += "<p>Client IP: <b>" + ClientRequest.RemoteEndPoint + "</b>.</p>";

									HelpString += "<h2>Internal URLs:</h2><ul>" +
									//			  "<li><a href='/!codepages/'>/!codepages/</a> - list of available encodings for OutputEncoding setting</li>" +
												  "<li><a href='/!img-test/'>/!img-test/</a> - test if ImageMagick is working</li>" +
												  "<li><a href='/!convert/'>/!convert/</a> - run a file format converter (<a href='/!convert/?src=logo.webp&dest=gif&type=image/gif'>demo</a>)</li>" +
												  "<li><a href='/!file/'>/!file/</a> - get a file from WebOne working directory (<a href='/!file/?name=webone.conf&type=text/plain'>demo</a>)</li>" +
												  "<li><a href='/!clear/'>/!clear/</a> - remove temporary files in WebOne working directory</li>"+
												  "<li><a href='/auto.pac'>Proxy auto-configuration file</a>: /!pac/, /auto/, /auto, /auto.pac, /wpad.dat.</li>"+
												  "</ul>";

									HelpString += "<h2>Headers sent by browser</h2><ul>";
									HelpString += "<li><b>" + ClientRequest.HttpMethod + " " + ClientRequest.RawUrl + " HTTP/" + ClientRequest.ProtocolVersion + "</b></li>";
									foreach (string hdrn in ClientRequest.Headers.Keys)
									{
										HelpString += "<li>" + hdrn + ": " + ClientRequest.Headers[hdrn] + "</li>";
									}
									HelpString += "</ul>";
									SendError(200, HelpString);
									return;
								case "/!codepages/":
									string codepages = "The following code pages are available: <br>\n" +
													   "<table><tr><td><b>Name</b></td><td><b>#</b></td><td><b>Description</b></td></tr>\n";
									codepages += "<tr><td><b>AsIs</b></td><td>0</td><td>Leave code pages as is</td></tr>\n";
									Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
									bool IsOutputEncodingListed = false;
									foreach (EncodingInfo cp in Encoding.GetEncodings())
									{
										codepages += "<tr><td>";

										//don't work since transfer to .Net Core:
										/*if (Encoding.Default.EncodingName.Contains(" ") && cp.DisplayName.Contains(Encoding.Default.EncodingName.Substring(0, Encoding.Default.EncodingName.IndexOf(" "))))
											codepages += "<b><u>" + cp.Name + "</u></b></td><td><u>" + cp.CodePage + "</u></td><td><u>" + cp.DisplayName + (cp.CodePage == Encoding.Default.CodePage ? "</u> (<i>system default</i>)" : "</u>");
										else
											codepages += "<b>" + cp.Name + "</b></td><td>" + cp.CodePage + "</td><td>" + cp.DisplayName;*/

										codepages += "<b>" + cp.Name + "</b></td><td>" + cp.CodePage + "</td><td>" + cp.DisplayName;


										if (ConfigFile.OutputEncoding != null && cp.CodePage == ConfigFile.OutputEncoding.CodePage)
										{
											codepages += " <b>(Current)</b>";
											IsOutputEncodingListed = true;
										}

										codepages += "</td></tr>\n";
									}
									//codepages += "</table><br>Use any of these. Underlined are for your locale.";
									codepages += "</table><br>Use any of these or from <a href=http://docs.microsoft.com/en-us/dotnet/api/system.text.encoding.getencodings?view=netcore-3.1>Microsoft documentation</a>.";

									if (!IsOutputEncodingListed && ConfigFile.OutputEncoding != null)
										codepages += "<br>Current output encoding: <b>" + ConfigFile.OutputEncoding.WebName + "</b> &quot;" + ConfigFile.OutputEncoding.EncodingName + "&quot; (# " + ConfigFile.OutputEncoding.CodePage + ").";
									if (ConfigFile.OutputEncoding == null)
										codepages += "<br>Current output encoding: <b>same as source</b>.";

									SendError(200, codepages);
									break;
								case "/!img-test/":
									SendError(200, @"ImageMagick test.<br><img src=""/!convert/?src=logo.webp&dest=gif&type=image/gif"" alt=""ImageMagick logo"" width=640 height=480><br>A wizard should appear nearby.");
									break;
								case "/!convert/":
									string SrcUrl = "", Src = "", Dest = "xbm", DestMime = "image/x-xbitmap", Converter = "convert", Args1 = "", Args2 = "";

									//parse URL
									Match FindSrc = Regex.Match(RequestURL.Query, @"(src)=([^&]+)");
									Match FindSrcUrl = Regex.Match(RequestURL.Query, @"(url)=([^&]+)");
									Match FindDest = Regex.Match(RequestURL.Query, @"(dest)=([^&]+)");
									Match FindDestMime = Regex.Match(RequestURL.Query, @"(type)=([^&]+)");
									Match FindConverter = Regex.Match(RequestURL.Query, @"(util)=([^&]+)");
									Match FindArg1 = Regex.Match(RequestURL.Query, @"(arg)=([^&]+)");
									Match FindArg2 = Regex.Match(RequestURL.Query, @"(arg2)=([^&]+)");

									if (FindSrc.Success)
										Src = Uri.UnescapeDataString(FindSrc.Groups[2].Value);

									if (FindSrcUrl.Success)
										SrcUrl = Uri.UnescapeDataString(FindSrcUrl.Groups[2].Value);
										//BUG: sometimes URL gets unescaped when opening via WMP
										//     (mostly via UI, and all load retries via FF plugin, strange but 1st plugin's attempt is valid)

									if (FindDest.Success)
										Dest = Uri.UnescapeDataString(FindDest.Groups[2].Value);

									if (FindDestMime.Success)
										DestMime = Uri.UnescapeDataString(FindDestMime.Groups[2].Value);
									
									if (FindConverter.Success)
										Converter = Uri.UnescapeDataString(FindConverter.Groups[2].Value);

									if (FindArg1.Success)
										Args1 = Uri.UnescapeDataString(FindArg1.Groups[2].Value);

									if (FindArg2.Success)
										Args2 = Uri.UnescapeDataString(FindArg2.Groups[2].Value);

									if (Src == "CON:") throw new ArgumentException("Bad source file name");

									//detect info page requestion
									if (!FindSrcUrl.Success && !FindSrc.Success)
									{
										SendError(200, "<big>Here you can summon ImageMagick to convert a picture file</big>.<br>" +
										"<p>Usage: /!convert/?url=https://example.com/filename.ext&dest=gif&type=image/gif<br>" +
										"or: /!convert/?src=filename.ext&dest=gif&type=image/gif</p>" +
										"<p>See <a href=\"http://github.com/atauenis/webone/wiki\">WebOne wiki</a> for help on this.</p>");
										break;
									}

									//find converter and use it
									foreach(Converter Cvt in ConfigFile.Converters)
									{
										if(Cvt.Executable == Converter)
										{
											HttpOperation HOper = new HttpOperation(BeginTime);
											Stream SrcStream = null;

											//find source file placement
											if (FindSrcUrl.Success)
											{
												//download source file
												if (!Cvt.SelfDownload) try
												{
													HOper.URL = SrcUrl;
													HOper.Method = "GET";
													HOper.RequestHeaders = new WebHeaderCollection();
#if DEBUG
													Console.WriteLine("{0}\t>Downloading source stream (connecting)...", GetTime(BeginTime));
#else
													Console.WriteLine("{0}\t>Downloading source stream...", GetTime(BeginTime));
#endif
													HOper.SendRequest();
#if DEBUG
													Console.WriteLine("{0}\t>Downloading source stream (receiving)...", GetTime(BeginTime));
#endif
													HOper.GetResponse();
													SrcStream = HOper.ResponseStream;
												}
												catch (Exception DlEx)
												{
													Console.WriteLine("{0}\t Converter cannot download source: {1}", GetTime(BeginTime), DlEx.Message);
													SendError(503,
														"<p><big><b>Converter cannot download the source</b>: " + DlEx.Message + "</big></p>" +
														"Source URL: " + SrcUrl);
													return;
												}
											}
											else
											{
												//open local source file
												SrcUrl = "http://0.0.0.0/localfile";
												if (!Cvt.SelfDownload) try
												{
													if (!File.Exists(Src))
													{
														if (File.Exists(new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName + Path.DirectorySeparatorChar + Src))
															Src = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName + Path.DirectorySeparatorChar + Src;
														else
															throw new FileNotFoundException("No such file: " + Src);
													}
													SrcStream = File.OpenRead(Src);
												}
												catch (Exception OpenEx)
												{
													Console.WriteLine("{0}\t Converter cannot open source: {1}", GetTime(BeginTime), OpenEx.Message);
													SendError(503,
														"<p><big><b>Converter cannot open the source</b>: " + OpenEx.Message + "</big></p>" +
														"Source URL: " + SrcUrl);
													return;
												}
											}

											//go to converter
											try
											{
												//run converter & return result
												SendStream(Cvt.Run(BeginTime, SrcStream, Args1, Args2, Dest, SrcUrl), DestMime, true);
												return;
											}
											catch(Exception CvtEx)
											{
												Console.WriteLine("{0}\t Converter error: {1}", GetTime(BeginTime), CvtEx.Message);
												SendError(502, 
													"<p><big><b>Converter error</b>: " + CvtEx.Message + "</big></p>" +
													"Source URL: " + SrcUrl + "<br>" +
													"Utility: " + Cvt.Executable);
												return;
											}
										}
									}

									SendError(503, "<big>Converter &quot;<b>" + Converter + "</b>&quot; is unknown</big>.<br>" +
									"<p>This converter is not listed in configuration file.</p>" +
									"<p>See <a href=\"http://github.com/atauenis/webone/wiki\">WebOne wiki</a> for help on this.</p>");
									break;
								case "/!file/":
									string FileName, MimeType="text/plain";
									Match FindName = Regex.Match(RequestURL.Query, @"(name)=([^&]+)");
									Match FindMime = Regex.Match(RequestURL.Query, @"(type)=([^&]+)");

									if (FindMime.Success)
										MimeType = FindMime.Groups[2].Value;

									if (FindName.Success)
									{
										FileName = FindName.Groups[2].Value;

										if (!File.Exists(FileName))
										{ FileName = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName + Path.DirectorySeparatorChar + FileName; }

										if (new FileInfo(FileName).DirectoryName != Directory.GetCurrentDirectory() &&
											new FileInfo(FileName).DirectoryName != System.Reflection.Assembly.GetExecutingAssembly().Location)
										{
											SendError(403, "Cannot access to files outside current and proxy installation directory");
											return;
										}
									
										SendFile(FileName, MimeType);
										return;
									}
									SendError(200, "Get a local file.<br>Usage: /!file/?name=filename.ext&type=text/plain");
									return;
								case "/!clear/":
									int FilesDeleted = 0;
									foreach (FileInfo file in (new DirectoryInfo(ConfigFile.TemporaryDirectory)).EnumerateFiles("convert-*.*"))
									{
										try { file.Delete(); FilesDeleted++; }
										catch { }
									}
									SendError(200, "<b>" + FilesDeleted + "</b> temporary files have been deleted in <i>" + ConfigFile.TemporaryDirectory + "</i>.");
									return;
								case "/!pac/":
								case "/auto/":
								case "/auto":
								case "/auto.pac":
								case "/wpad.dat":
								case "/wpad.da":
									//Proxy Auto-Config
									Console.WriteLine("{0}\t<Return PAC/WPAD script.", GetTime(BeginTime));
									string PacString =
									@"function FindProxyForURL(url, host) {" +
									@"if (url.substring(0, 5) == ""http:"")" +
									@"{ return ""PROXY "+ ConfigFile.DefaultHostName + ":"+ConfigFile.Port+@"""; }" +
									@"else { return ""DIRECT""; }"+
									@"} /*WebOne PAC*/";
									byte[] Buffer = Encoding.Default.GetBytes(PacString);
									try
									{
										ClientResponse.StatusCode = 200;
										ClientResponse.ProtocolVersion = new Version(1, 0);

										ClientResponse.ContentType = "application/x-ns-proxy-autoconfig";
										ClientResponse.ContentLength64 = PacString	.Length;
										ClientResponse.OutputStream.Write(Buffer, 0, Buffer.Length);
										ClientResponse.OutputStream.Close();
									}
									catch
									{
										Console.WriteLine("{0}\t<!Cannot return PAC.", GetTime(BeginTime));
									}
									return;
								default:
									SendError(200, "Unknown internal URL: " + RequestURL.PathAndQuery);
									break;
							}
							return;
						}
						catch (Exception ex)
						{
							Console.WriteLine("{0}\t Internal server error: {1}", GetTime(BeginTime), ex.ToString());
#if DEBUG
							SendError(500, "Internal server error: <b>" + ex.Message + "</b><br>" + ex.GetType().ToString() + " " + ex.StackTrace.Replace("\n","<br>"));
#else
							SendError(500, "WebOne cannot process the request because <b>" + ex.Message + "</b>.");
#endif
						}
					}
					//local proxy mode: http://localhost/http://example.com/indexr.shtml
					if (RequestURL.LocalPath.StartsWith("/http:") || RequestURL.AbsoluteUri.StartsWith("/https:"))
					{
						RequestURL = new Uri(RequestURL.LocalPath.Substring(1) + RequestURL.Query);
						Console.WriteLine("{0}\t Local: {1}", GetTime(BeginTime), RequestURL);
						LocalMode = true;
					}
					else
					{
						//dirty local mode, try to use last used host: http://localhost/favicon.ico
						RequestURL = new Uri("http://" + new Uri(LastURL).Host + RequestURL.LocalPath);
						if (RequestURL.Host == "999.999.999.999") { SendError(404, "The proxy server cannot guess domain name."); return; }
						Console.WriteLine("{0}\t Dirty local: {1}", GetTime(BeginTime), RequestURL);
						LocalMode = true;
					}
				}

				//check for HTTP-to-FTP requests
				//https://support.microsoft.com/en-us/help/166961/how-to-ftp-with-cern-based-proxy-using-wininet-api
				string[] BadProtocols = { "ftp", "gopher", "wais" };
				if (CheckString(RequestURL.Scheme, BadProtocols))
				{
					Console.WriteLine("{0}\t CERN Proxy request to {1} detected.", GetTime(BeginTime), RequestURL.Scheme.ToUpper());
					ClientResponse.AddHeader("Upgrade", RequestURL.Scheme.ToUpper());
					SendError(101, "Cannot work with " + RequestURL.Scheme.ToUpper() + " protocol. Please connect directly bypassing the proxy.");
					return;
				}

				//dirty workarounds for HTTP>HTTPS redirection bugs ("carousels")
				//should redirect on 302s or reloadings from 200 and only on htmls
				if ((RequestURL.AbsoluteUri == RefererUri || RequestURL.AbsoluteUri == LastURL) &&
				RequestURL.AbsoluteUri != "" && 
				(LastContentType.StartsWith("text/htm") || LastContentType == "") &&
				(((int)LastCode > 299 && (int)LastCode < 400) || LastCode == HttpStatusCode.OK) &&
				ClientRequest.HttpMethod != "POST" && 
				ClientRequest.HttpMethod != "CONNECT" && 
				!Program.CheckString(RequestURL.AbsoluteUri, ConfigFile.ForceHttps) && 
				RequestURL.Host.ToLower() != Environment.MachineName.ToLower() &&
				RequestURL.Host.ToLower() != ConfigFile.DefaultHostName.ToLower())
				{
					Console.WriteLine("{0}\t Carousel detected.", GetTime(BeginTime));
					if (!LastURL.StartsWith("https") && !RequestURL.AbsoluteUri.StartsWith("https")) //if http is gone, try https
						RequestURL = new Uri("https" + RequestURL.AbsoluteUri.Substring(4));
					if (LastURL.StartsWith("https")) //if can't use https, try again http
						RequestURL = new Uri("http" + RequestURL.AbsoluteUri.Substring(4));
				}

				//make referer secure if need
				if (RequestURL.Host == new Uri(RefererUri ?? "about:blank").Host)
					if (RequestURL.AbsoluteUri.StartsWith("https://") && !RefererUri.StartsWith("https://"))
						RefererUri = "https" + RefererUri.Substring(4);

				LastURL = RequestURL.AbsoluteUri;
				LastContentType = "unknown/unknown"; //will be populated in MakeOutput
				LastCode = HttpStatusCode.OK; //same

				//make reply
				//SendError(200, "Okay, bro! Open " + RequestURL);
				bool StWrong = false; //break operation if something is wrong.

				if (RequestURL.AbsoluteUri.Contains("??")) { StWrong = true; SendError(400, "Too many questions."); }
				if (RequestURL.AbsoluteUri.Length == 0) { StWrong = true; SendError(400, "Empty URL."); }
				if (RequestURL.AbsoluteUri == "") return;

				//check for available edit sets
				foreach (EditSet set in ConfigFile.EditRules)
				{
					if (CheckStringRegExp(RequestURL.AbsoluteUri, set.UrlMasks.ToArray()) && !CheckStringRegExp(RequestURL.AbsoluteUri, set.UrlIgnoreMasks.ToArray()))
					{
						EditSets.Add(set);
					}
				}

				try
				{
					int CL = 0;
					if (ClientRequest.Headers["Content-Length"] != null) CL = Int32.Parse(ClientRequest.Headers["Content-Length"]);

					//make and send HTTPS request to destination server
					WebHeaderCollection whc = new WebHeaderCollection();

					//prepare headers
					if(RequestURL.Scheme.ToLower() == "https")
					{
						foreach(string h in ClientRequest.Headers.Keys) {
							whc.Add(h, ClientRequest.Headers[h].Replace("http://", "https://"));
						}
					}
					else
					{
						foreach(string h in ClientRequest.Headers.Keys) {
							whc.Add(h, ClientRequest.Headers[h]);
						}
					}
					if (whc["Origin"] == null & whc["Referer"] != null) whc.Add("Origin: " + new Uri(whc["Referer"]).Scheme + "://" + new Uri(whc["Referer"]).Host);

					//perform edits on the request
					foreach(EditSet Set in EditSets)
					{
						if(Set.IsForRequest)
						foreach(KeyValuePair<string, string> Edit in Set.Edits)
						{
							switch (Edit.Key)
							{
								case "AddInternalRedirect":
									Console.WriteLine("{0}\t Fix to {1} internally", GetTime(BeginTime), ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri));
									RequestURL = new Uri(Edit.Value);
									break;
								case "AddRedirect":
									Console.WriteLine("{0}\t Fix to {1}", GetTime(BeginTime), ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri));
									ClientResponse.AddHeader("Location", ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri));
									SendError(302, "Брось каку!");
									return;
								case "AddHeader":
									//Console.WriteLine("{0}\t Add request header: {1}", GetTime(BeginTime), Edit.Value);
									if (whc[Edit.Value.Substring(0, Edit.Value.IndexOf(": "))] == null) whc.Add(ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri));
									break;
							}
						}
					}

					//send the request
					SendRequest(operation, ClientRequest.HttpMethod, whc, CL);
				}
				catch (WebException wex)
				{
					if (wex.Response == null) ResponseCode = 502;
					else ResponseCode = (int)(wex.Response as HttpWebResponse).StatusCode;
					if (ResponseCode == 502) Console.WriteLine("{0}\t Cannot load this page: {1}.", GetTime(BeginTime), wex.Status);
					else Console.WriteLine("{0}\t Web exception: {1} {2}.", GetTime(BeginTime), ResponseCode, (wex.Response as HttpWebResponse).StatusCode);

					ContentType = "text/html";
#if DEBUG
					string err = ": " + wex.Status.ToString();
					ResponseBody = "<html><title>WebOne error</title><body>Cannot load this page" + err + "<br><i>" + wex.ToString().Replace("\n", "<br>") + "</i><br>URL: " + RequestURL.AbsoluteUri + Program.GetInfoString() + "</body></html>";
#else
					string NiceErrMsg = "<p><big>" + wex.Message + ".</big></p>Status: " + wex.Status;
					if (wex.InnerException != null && wex.Status != WebExceptionStatus.UnknownError) NiceErrMsg = " <p><big>" + wex.Message + "<br>" + wex.InnerException.Message + ".</big></p>Status: " + wex.Status + " + " + wex.InnerException.GetType().ToString();
					ResponseBody = "<html><title>WebOne: " + wex.Status + "</title><body><h1>Cannot load this page</h1>" + NiceErrMsg + "<br>URL: " + RequestURL.AbsoluteUri + GetInfoString() + "</body></html>";
#endif

					//check if archived copy can be retreived instead
					if (ConfigFile.SearchInArchive)
						if ((wex.Status == WebExceptionStatus.NameResolutionFailure) ||
							(wex.Response != null && (wex.Response as HttpWebResponse).StatusCode == HttpStatusCode.NotFound))
						{
							try
							{
								Console.WriteLine("{0}\t Look in Archive.org...", GetTime(BeginTime));
								HttpWebRequest ArchiveRequest = (HttpWebRequest)WebRequest.Create("https://archive.org/wayback/available?url=" + RequestURL.AbsoluteUri);
								ArchiveRequest.UserAgent = "WebOne/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
								ArchiveRequest.Method = "GET";
								string ArchiveResponse = "";
								MemoryStream ms = new MemoryStream();
								ArchiveRequest.GetResponse().GetResponseStream().CopyTo(ms);
								ArchiveResponse = Encoding.UTF8.GetString(ms.ToArray());
								string ArchiveURL = "";

								//parse archive.org json reply
								if (ArchiveResponse.Contains(@"""available"": true"))
								{
									Match ArchiveMatch = Regex.Match(ArchiveResponse, @"""url"": ""http://web.archive.org/.*"","); ;
									if (ArchiveMatch.Success)
									{
										Console.WriteLine("{0}\t Available.", GetTime(BeginTime));
										ArchiveURL = ArchiveMatch.Value.Substring(8, ArchiveMatch.Value.IndexOf(@""",") - 8);
										ResponseBody = "<html><body><h1>Server not found</h2>But an <a href=" + ArchiveURL + ">archived copy</a> is available! Redirecting to it...</body></html>";
										ClientResponse.AddHeader("Location", ArchiveURL);
										SendError(302, ResponseBody);
										return;
									}
									else
									{
										Console.WriteLine("{0}\t Available, but somewhere.", GetTime(BeginTime));
									}
								}
								else
								{
									Console.WriteLine("{0}\t No snapshots.", GetTime(BeginTime));
									if (RequestURL.AbsoluteUri.StartsWith("http://web.archive.org/web/") && ConfigFile.ShortenArchiveErrors)
									{
										string ErrMsg =
										"<p><b>The Wayback Machine has not archived that URL.</b></p>" +
										"<p>This page is not available on the web because page does not exist<br>" +
										"Try to slightly change the URL.</p>" +
										"<small><i>You see this message because ShortenArchiveErrors option is enabled.</i></small>";
										SendError(404, ErrMsg);
									}
								}
							}
							catch (Exception ArchiveException)
							{
								ResponseBody = String.Format("<html><body><b>Server not found and a Web Archive error occured.</b><br>{0}</body></html>", ArchiveException.Message.Replace("\n", "<br>"));
							}
						}
				}
				catch (UriFormatException)
				{
					StWrong = true;
					SendError(400, "The URL <b>" + RequestURL.AbsoluteUri + "</b> is not valid.");
				}
				catch (Exception ex)
				{
					StWrong = true;
					Console.WriteLine("{0}\t ============GURU MEDITATION:\n{1}\nOn URL '{2}', Method '{3}'. Returning 500.============", GetTime(BeginTime), ex.ToString(), RequestURL.AbsoluteUri, ClientRequest.HttpMethod);
					SendError(500, "Guru meditaion at URL " + RequestURL.AbsoluteUri + ":<br><b>" + ex.Message + "</b><br><i>" + ex.StackTrace.Replace("\n", "\n<br>") + "</i>");
				}

				//try to return...
				try
				{
					if (!StWrong)
					{
						ClientResponse.ProtocolVersion = new Version(1, 0);
						ClientResponse.StatusCode = ResponseCode;
						ClientResponse.AddHeader("Via", "HTTP/1.0 WebOne/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);


						if (CheckString(ContentType, ConfigFile.TextTypes) || ContentType == "")
						{
							ClientResponse.AddHeader("Content-Type", ContentType);
						}
						else
						{
							ClientResponse.AddHeader("Content-Type", ContentType);
						}

						if (TransitStream == null)
						{
							byte[] RespBuffer;
							RespBuffer = (ConfigFile.OutputEncoding ?? ContentEncoding).GetBytes(ResponseBody).ToArray();

							ClientResponse.ContentLength64 = RespBuffer.Length;

							if (ClientResponse.ContentLength64 > 300*1024) Console.WriteLine("{0}\t Sending binary.", GetTime(BeginTime));
							ClientResponse.OutputStream.Write(RespBuffer, 0, RespBuffer.Length);
						}
						else
						{
							if(TransitStream.CanSeek) ClientResponse.ContentLength64 = TransitStream.Length;
							TransitStream.CopyTo(ClientResponse.OutputStream);
						}
						ClientResponse.OutputStream.Close();
#if DEBUG
						Console.WriteLine("{0}\t Document sent.", GetTime(BeginTime));
#endif
					}
#if DEBUG
					else Console.WriteLine("{0}\t Abnormal return (something was wrong).", GetTime(BeginTime));
#endif
				}
				catch (Exception ex)
				{
					if (!ConfigFile.HideClientErrors)
#if DEBUG
						Console.WriteLine("{0}\t<Can't return reply. " + ex.Message + ex.StackTrace, GetTime(BeginTime));
#else
						Console.WriteLine("{0}\t<Can't return reply. " + ex.Message, GetTime(BeginTime));
#endif
				}
			}
			catch(Exception E)
			{
				string time = GetTime(BeginTime);
				Console.WriteLine("{0}\t A error has been catched: {1}\n{0}\t Please report to author.", time, E.ToString().Replace("\n", "\n{0}\t "));
				SendError(500, "An error occured: " + E.ToString().Replace("\n", "\n<BR>"));
				if (operation != null) operation.Dispose();
			}
			if (operation != null) operation.Dispose();
#if DEBUG
			Console.WriteLine("{0}\t End process.", GetTime(BeginTime));
#endif
		}

		/// <summary>
		/// Send a HTTPS request and put the response to shared variable "response"
		/// </summary>
		/// <param name="https">HTTPS client</param>
		/// <param name="RequestMethod">Request method</param>
		/// <param name="RequestHeaderCollection">Request headers</param>
		/// <param name="Content_Length">Request content length</param>
		/// <returns>Response status code (and the Response in shared variable)</returns>
		private void SendRequest(HttpOperation HTTPO, string RequestMethod, WebHeaderCollection RequestHeaderCollection, int Content_Length)
		{
			bool AllowAutoRedirect = CheckString(RequestURL.AbsoluteUri, ConfigFile.InternalRedirectOn);
			if (!AllowAutoRedirect) AllowAutoRedirect = ShouldRedirectInNETFW;

			switch (RequestMethod)
			{
				case "CONNECT":
					string ProtocolReplacerJS = "<script>if (window.location.protocol != 'http:') { setTimeout(function(){window.location.protocol = 'http:'; window.location.reload();}, 1000); }</script>";
					SendError(405, "The proxy does not know the " + RequestMethod + " method.<BR>Please use HTTP, not HTTPS.<BR>HSTS must be disabled." + ProtocolReplacerJS);
					Console.WriteLine("{0}\t Wrong method.", GetTime(BeginTime));
					return;
				default:
					operation = new HttpOperation(BeginTime);
					if (Content_Length == 0)
					{
						//try to download (GET, HEAD, WebDAV download, etc)
#if DEBUG
						Console.WriteLine("{0}\t>Downloading content (connecting)...", GetTime(BeginTime));
#else
						Console.WriteLine("{0}\t>Downloading content...", GetTime(BeginTime));
#endif
						operation.URL = RequestURL.AbsoluteUri;
						operation.Method = RequestMethod;
						operation.RequestHeaders = RequestHeaderCollection;
						operation.AllowAutoRedirect = AllowAutoRedirect;
						operation.SendRequest();
#if DEBUG
						Console.WriteLine("{0}\t>Downloading content (receiving)...", GetTime(BeginTime));
#endif
						operation.GetResponse();
						MakeOutput(operation);
						break;
					}
					else
					{
						//try to upload (POST, PUT, WebDAV, etc)
						Console.WriteLine("{0}\t>Uploading {1}K of {2}...", GetTime(BeginTime), Convert.ToInt32((RequestHeaderCollection["Content-Length"])) / 1024, RequestHeaderCollection["Content-Type"]);
#if DEBUG
						Console.WriteLine("{0}\t>Uploading {1}K of {2} (connecting)...", GetTime(BeginTime), Convert.ToInt32((RequestHeaderCollection["Content-Length"])) / 1024, RequestHeaderCollection["Content-Type"]);
#else
						Console.WriteLine("{0}\t>Uploading {1}K of {2}...", GetTime(BeginTime), Convert.ToInt32((RequestHeaderCollection["Content-Length"])) / 1024, RequestHeaderCollection["Content-Type"]);
#endif
						operation.URL = RequestURL.AbsoluteUri;
						operation.Method = RequestMethod;
						operation.RequestHeaders = RequestHeaderCollection;
						operation.RequestStream = ClientRequest.InputStream;
						operation.AllowAutoRedirect = AllowAutoRedirect;
						operation.SendRequest();
#if DEBUG
						Console.WriteLine("{0}\t>Uploading content (receiving)...", GetTime(BeginTime));
#endif
						operation.GetResponse();
						MakeOutput(operation);
						break;
					}
			}

			if (Stop) return; //if converting has occur and the request should not be processed next

			//todo: this may be moved to MakeOutput!
			ResponseCode = (int)operation.Response.StatusCode;

			//check for security upgrade
			if (ResponseCode == 301 || ResponseCode == 302 || ResponseCode == 308)
			{
				if (RequestURL.AbsoluteUri == ((operation.ResponseHeaders ?? new WebHeaderCollection())["Location"] ?? "nowhere").Replace("https://", "http://")
					&& !CheckString(RequestURL.AbsoluteUri, ConfigFile.InternalRedirectOn))
				{
					Console.WriteLine("{0}\t>Reload secure...", GetTime(BeginTime));

					RequestURL = new Uri(RequestURL.AbsoluteUri.Replace("http://", "https://"));
					TransitStream = null;
					SendRequest(operation, RequestMethod, RequestHeaderCollection, Content_Length);

					//add to ForceHttp list
					List<string> ForceHttpsList = ConfigFile.ForceHttps.ToList<string>();
					string SecureHost = RequestURL.Host;
					if (!ForceHttpsList.Contains(SecureHost))
						ForceHttpsList.Add(SecureHost);
					ConfigFile.ForceHttps = ForceHttpsList.ToArray();

					return;
				}
			}

			//process response headers
			if(operation.ResponseHeaders != null)
			for (int i = 0; i < operation.ResponseHeaders.Count; ++i)
			{
				string header = operation.ResponseHeaders.GetKey(i);
				foreach (string value in operation.ResponseHeaders.GetValues(i))
				{
					if (!header.StartsWith("Content-") &&
					!header.StartsWith("Connection") &&
					!header.StartsWith("Transfer-Encoding") &&
					!header.StartsWith("Access-Control-Allow-Methods") &&
					!header.StartsWith("Strict-Transport-Security") &&
					!header.StartsWith("Content-Security-Policy") &&
					!header.StartsWith("Upgrade-Insecure-Requests") &&
					!(header.StartsWith("Vary") && value.Contains("Upgrade-Insecure-Requests")))
					{
						ClientResponse.AddHeader(header, value.Replace("; secure", "").Replace("https://", "http://"));
					}
				}
			}
		}


		/// <summary>
		/// Process the reply's body and fix too modern stuff
		/// </summary>
		/// <param name="Body">The original body</param>
		/// <returns>The fixed body, compatible with old browsers</returns>
		private string ProcessBody(string Body)
		{
			Body = Body.Replace("https", "http");
			//if (LocalMode) Body = Body.Replace("http://", "http://" + ConfigFile.DefaultHostname + "/http://");//replace with real hostname
			if (ConfigFile.OutputEncoding != null)
			{
				Body = Body.Replace("harset=\"utf-8\"", "harset=\"" + ConfigFile.OutputEncoding.WebName + "\"");
				Body = Body.Replace("harset=\"UTF-8\"", "harset=\"" + ConfigFile.OutputEncoding.WebName + "\"");
				Body = Body.Replace("harset=utf-8", "harset=" + ConfigFile.OutputEncoding.WebName);
				Body = Body.Replace("harset=UTF-8", "harset=" + ConfigFile.OutputEncoding.WebName);
				Body = Body.Replace("CHARSET=UTF-8", "CHARSET=" + ConfigFile.OutputEncoding.WebName);
				Body = Body.Replace("ncoding=\"utf-8\"", "ncoding=\"" + ConfigFile.OutputEncoding.WebName + "\"");
				Body = Body.Replace("ncoding=\"UTF-8\"", "ncoding=\"" + ConfigFile.OutputEncoding.WebName + "\"");
				Body = Body.Replace("ncoding=utf-8", "ncoding=" + ConfigFile.OutputEncoding.WebName);
				Body = Body.Replace("ncoding=UTF-8", "ncoding=" + ConfigFile.OutputEncoding.WebName);
				Body = Body.Replace("ENCODING=UTF-8", "ENCODING=" + ConfigFile.OutputEncoding.WebName);
				Body = Body.Replace(ConfigFile.OutputEncoding.GetString(UTF8BOM), "");
				Body = ConfigFile.OutputEncoding.GetString(Encoding.Convert(Encoding.UTF8, ConfigFile.OutputEncoding, Encoding.UTF8.GetBytes(Body)));
			}

			//content patching
			int patched = 0;
			List<string> Finds = new List<string>();
			List<string> Replacions = new List<string>();

			//find edits
			foreach (EditSet Set in EditSets)
			{
				if (Set.ContentTypeMasks.Count == 0 || CheckStringRegExp(ContentType, Set.ContentTypeMasks.ToArray()))
				{
					if (CheckHttpStatusCode(Set.OnCode, operation.Response.StatusCode))
					{ 
						foreach (KeyValuePair<string, string> Edit in Set.Edits)
						{
							switch (Edit.Key)
							{
								case "AddFind":
									Finds.Add(Edit.Value);
									break;
								case "AddReplace":
									Replacions.Add(Edit.Value);
									break;
							}
						}
					}
				}
			}

			//do edits
			if (Finds.Count != Replacions.Count)
				Console.WriteLine("{0}\t Invalid amount of Find/Replace!", GetTime(BeginTime));
				//todo: add warning to constructor of EditSet
			else if(Finds.Count > 0)
				for (int i = 0; i < Finds.Count; i++)
				{
					Body = Regex.Replace(Body, Finds[i], Replacions[i], RegexOptions.Singleline);
					patched++;
				}

			if (patched > 0) Console.WriteLine("{0}\t {1} patch(-es) applied...", GetTime(BeginTime), patched);

			//do transliteration if need
			if(ConfigFile.TranslitTable.Count > 0)
			{
				foreach(var Letter in ConfigFile.TranslitTable)
				{
					Body = Body.Replace(Letter.Key, Letter.Value);
				}
			}

			//fix the body if it will be deliveried through Local mode
			if(LocalMode)
			Body = Body.Replace("http://", "http://" + ConfigFile.DefaultHostName + "/http://");

			return Body;
		}


		/// <summary>
		/// Prepare response body for tranfer to client
		/// </summary>
		/// <param name="Operation">HttpOperation which describes the source response</param>
		/// <returns>ResponseBuffer+ResponseBody for texts or TransitStream for binaries</returns>
		private void MakeOutput(HttpOperation Operation)
		{
			MakeOutput(Operation.Response.StatusCode, Operation.ResponseStream, Operation.Response.ContentType, Operation.Response.ContentLength);
		}

		/// <summary>
		/// Prepare response body for tranfer to client
		/// </summary>
		/// <param name="StatusCode">HTTP Status code</param>
		/// <param name="ResponseStream">Stream of response body</param>
		/// <param name="ContentType">HTTP Content-Type</param>
		/// <param name="ContentLength">HTTP Content-Lenght</param>
		/// <returns>ResponseBuffer+ResponseBody for texts or TransitStream for binaries</returns>
		private void MakeOutput(HttpStatusCode StatusCode, Stream ResponseStream, string ContentType, long ContentLength)
		{
			this.ContentType = ContentType;
			string SrcContentType = ContentType;

			//perform edits on the response: common tasks for all bin/text
			string Converter = null;
			string ConvertDest = "";
			string ConvertArg1 = "";
			string ConvertArg2 = "";
			string Redirect = null;

			foreach (EditSet Set in EditSets)
			{
				if (Set.ContentTypeMasks.Count == 0 || CheckStringRegExp(Set.ContentTypeMasks.ToArray(), ContentType))
				{
					if (CheckHttpStatusCode(Set.OnCode, operation.Response.StatusCode))
					{
						foreach (KeyValuePair<string, string> Edit in Set.Edits)
						{
							switch (Edit.Key)
							{
								case "AddConvert":
									Converter = Edit.Value;
									Stop = true;
									break;
								case "AddConvertDest":
									ConvertDest = Edit.Value;
									break;
								case "AddConvertArg1":
									ConvertArg1 = Edit.Value;
									break;
								case "AddConvertArg2":
									ConvertArg2 = Edit.Value;
									break;
								case "AddResponseHeader":
									Console.WriteLine("{0}\t Add response header: {1}", GetTime(BeginTime), ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri));
									operation.ResponseHeaders.Add(ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri));
									if (Edit.Value.StartsWith("Content-Type: ")) ContentType = Edit.Value.Substring("Content-Type: ".Length);
									break;
								case "AddRedirect":
									Console.WriteLine("{0}\t Add redirect: {1}", GetTime(BeginTime), ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri));
									Redirect = ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri);
									break;
							}
						}
					}
				}
			}

			//check for edit: AddRedirect
			if (Redirect != null)
			{
				Console.WriteLine("{0}\t {1} {2}. Body {3}K of {4} [Need to redirect].", GetTime(BeginTime), (int)StatusCode, StatusCode, ContentLength / 1024, SrcContentType);
				ClientResponse.AddHeader("Location", Redirect);
				SendError(302, "Redirect requested.");
				return;
			}

			//check for edit: AddConvert
			if (Converter != null)
			{
				Console.WriteLine("{0}\t {1} {2}. Body {3}K of {4} [Wants {5}].", GetTime(BeginTime), (int)StatusCode, StatusCode, ContentLength / 1024, SrcContentType, Converter);

				try
				{
					foreach (Converter Cvt in ConfigFile.Converters)
					{
						if (Cvt.Executable == Converter)
						{
							if(!Cvt.SelfDownload)
								SendStream(Cvt.Run(BeginTime, ResponseStream, ConvertArg1, ConvertArg2, ConvertDest, RequestURL.AbsoluteUri), ContentType, true);
							else
							{
								SendStream(Cvt.Run(BeginTime, null, ConvertArg1, ConvertArg2, ConvertDest, RequestURL.AbsoluteUri), ContentType, true);
								//if(operation.Response != null) Console.WriteLine("{0}\t '{1}' will download the source again.", GetTime(BeginTime), Converter); //for future
							}
							return;
						}
					}
					SendError(503, "<big>Converter &quot;<b>" + Converter + "</b>&quot; is unknown</big>.<br>" +
					"<p>This converter is not well listed in configuration file.</p>" +
					"<p>See <a href=\"http://github.com/atauenis/webone/wiki\">WebOne wiki</a> for help on this.</p>");
					return;
				}
				catch(Exception ConvertEx)
				{
					Console.WriteLine("{0}\t On-fly converter error: {1}", GetTime(BeginTime), ConvertEx.Message);
					SendError(502,
						"<p><big><b>Converter error</b>: " + ConvertEx.Message + "</big></p>" +
						"Source URL: " + RequestURL.AbsoluteUri + "<br>" +
						"Utility: " + Converter + "<br>"+
						"Mode: seamless from '" + SrcContentType + "' to '" + ContentType + "'");
					return;
				}
			}

			LastCode = StatusCode;
			LastContentType = ContentType;

			if (Program.CheckString(ContentType, ConfigFile.TextTypes))
			{
				//if server returns text, make edits
				Console.WriteLine("{0}\t {1} {2}. Body {3}K of {4} [Text].", GetTime(BeginTime), (int)StatusCode, StatusCode, ContentLength / 1024, ContentType);
				byte[] RawContent = null;
				RawContent = ReadAllBytes(ResponseStream);

				ContentEncoding = FindContentCharset(RawContent);

				if (ConfigFile.OutputEncoding == null)
				{
					//if don't touch codepage (OutputEncoding=AsIs)
					ResponseBody = ContentEncoding.GetString(RawContent);
					ResponseBody = ProcessBody(ResponseBody);
#if DEBUG
					Console.WriteLine("{0}\t Body maked.", GetTime(BeginTime));
#endif
					return;
				}

				bool ForceUTF8 = ContentType.ToLower().Contains("utf-8");
				if (!ForceUTF8 && RawContent.Length > 0) ForceUTF8 = ContentEncoding == Encoding.UTF8;
				foreach (string utf8url in ConfigFile.ForceUtf8) { if (Regex.IsMatch(RequestURL.AbsoluteUri, utf8url)) ForceUTF8 = true; }
				//todo: add fix for "invalid range in character class" at www.yandex.ru with Firefox 3.6 if OutputEncoding!=AsIs

				if (ForceUTF8) ResponseBody = Encoding.UTF8.GetString(RawContent);
				else ResponseBody = ContentEncoding.GetString(RawContent);

				if (Regex.IsMatch(ResponseBody, @"<meta.*UTF-8.*>", RegexOptions.IgnoreCase)) { ResponseBody = Encoding.UTF8.GetString(RawContent); }

				if (ContentType.ToLower().Contains("utf-8")) ContentType = ContentType.Substring(0, ContentType.IndexOf(';'));
				ResponseBody = ProcessBody(ResponseBody);
				this.ContentType = ContentType;
			}
			else
			{
				if (operation != null)
					Console.WriteLine("{0}\t {1} {2}. Body {3}K of {4} [Binary].", GetTime(BeginTime), (int)StatusCode, StatusCode, operation.Response.ContentLength / 1024, ContentType);
				else
					Console.WriteLine("{0}\t {1} {2}. Body is {3} [Binary], incomplete.", GetTime(BeginTime), (int)StatusCode, StatusCode, ContentType);

				TransitStream = ResponseStream;
				this.ContentType = ContentType;
			}
#if DEBUG
			Console.WriteLine("{0}\t Body maked.", GetTime(BeginTime));
#endif
			return;

		}

		/// <summary>
		/// Detect and return file encoding
		/// </summary>
		/// <param name="RawContent">The file as byte array</param>
		/// <returns>Code page</returns>
		private Encoding FindContentCharset(byte[] RawContent)
		{
			if (RawContent.Length < 1) return Encoding.UTF8;
			//check for UTF magic bytes
			if (RawContent[0] == Encoding.UTF8.GetPreamble()[0]) return Encoding.UTF8;
			if (RawContent[0] == Encoding.Unicode.GetPreamble()[0]) return Encoding.Unicode;
			if (RawContent[0] == Encoding.BigEndianUnicode.GetPreamble()[0]) return Encoding.BigEndianUnicode;
			if (RawContent[0] == Encoding.UTF32.GetPreamble()[0]) return Encoding.UTF32;

			//get ANSI charset
			Encoding WindowsEncoding = CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);

			//find Meta Charset tag
			string Content = Encoding.Default.GetString(RawContent);
			Match MetaCharset = Regex.Match(Content, "<meta http-equiv .*charset=.*>", RegexOptions.IgnoreCase);
			if (!MetaCharset.Success) return WindowsEncoding;

			Match CharsetMatch = Regex.Match(MetaCharset.Value, "charset=.*['\"]", RegexOptions.IgnoreCase);
			if (!CharsetMatch.Success) return WindowsEncoding;

			//parse tag
			string Charset = CharsetMatch.Value["charset=".Length..].Replace("\"", "");
			switch (Charset.ToLower())
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

			return CodePagesEncodingProvider.Instance.GetEncoding(Charset) ?? Encoding.UTF8;
		}

		/// <summary>
		/// Check HTTP status code for compliance with expectations in Set of edits
		/// </summary>
		/// <param name="ExpectedCode">Expected code from Set of edits</param>
		/// <param name="RealCode">Real HTTP status code</param>
		/// <returns>true if the code is in compliance with Set of edits, false if not</returns>
		private bool CheckHttpStatusCode(int? ExpectedCode, HttpStatusCode RealCode)
		{
			if (ExpectedCode != null &&
			(
				(ExpectedCode == (int)RealCode) ||
				(ExpectedCode == 0 && (int)RealCode < 300) ||
				(ExpectedCode == 2 && (int)RealCode < 300 && (int)RealCode > 199) ||
				(ExpectedCode == 3 && (int)RealCode < 400 && (int)RealCode > 299) ||
				(ExpectedCode == 4 && (int)RealCode > 399)
			)
			|| ExpectedCode == null
		   )
				return true;
			else
				return false;

		}

		/// <summary>
		/// Send a HTTP error to client
		/// </summary>
		/// <param name="Code">HTTP Status code</param>
		/// <param name="Text">Text of message</param>
		private void SendError(int Code, string Text = "")
		{
			Console.WriteLine("{0}\t<Return code {1}.", GetTime(BeginTime), Code);
			Text += GetInfoString();
			string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
			string Refresh = "";
			if (ClientResponse.Headers["Refresh"] != null) Refresh = "<META HTTP-EQUIV=\"REFRESH\" CONTENT=\""+ ClientResponse.Headers["Refresh"] +"\">";
			string Html = "<html>" + Refresh + "<body><h1>" + CodeStr + "</h1>" + Text + "</body></html>";

			if ((ConfigFile.OutputEncoding ?? Encoding.Default) != Encoding.Default)
				Html = ConfigFile.OutputEncoding.GetString(Encoding.Default.GetBytes(Html));

			byte[] Buffer = (ConfigFile.OutputEncoding ?? Encoding.Default).GetBytes(Html);
			try
			{
				ClientResponse.StatusCode = Code;
				ClientResponse.ProtocolVersion = new Version(1, 0);

				ClientResponse.ContentType = "text/html";
				ClientResponse.ContentLength64 = Buffer.Length;
				ClientResponse.OutputStream.Write(Buffer, 0, Buffer.Length);
				ClientResponse.OutputStream.Close();
			}
			catch(Exception ex)
			{
				if(!ConfigFile.HideClientErrors)
				Console.WriteLine("{0}\t<!Cannot return code {1}. {2}: {3}", GetTime(BeginTime), Code, ex.GetType(), ex.Message);
			}
		}

		/// <summary>
		/// Send a file to client
		/// </summary>
		/// <param name="FileName">Full path to the file</param>
		/// <param name="ContentType">File's content-type.</param>
		private void SendFile(string FileName, string ContentType)
		{
			Console.WriteLine("{0}\t<Send file {1}.", GetTime(BeginTime), FileName);
			try
			{
				ClientResponse.StatusCode = 200;
				ClientResponse.ProtocolVersion = new Version(1, 0);
				ClientResponse.ContentType = ContentType;
				FileStream potok = File.OpenRead(FileName);
				potok.CopyTo(ClientResponse.OutputStream);
				potok.Close();
				ClientResponse.OutputStream.Close();
			}
			catch(Exception ex)
			{
				int ErrNo = 500;
				if (ex is FileNotFoundException) ErrNo = 404;
				SendError(ErrNo, "Cannot open the file <i>" + FileName + "</i>.<br>" + ex.ToString().Replace("\n", "<br>"));
			}
		}

		/// <summary>
		/// Send a stream to client (assuming that it is filled completely and can seek)
		/// </summary>
		/// <param name="Potok">The stream</param>
		/// <param name="ContentType">Expected content type</param>
		/// <param name="Close">Close the response after end of transfer</param>
		private void SendStream(Stream Potok, string ContentType, bool Close = true)
		{
			if (!Potok.CanRead) throw new ArgumentException("Cannot send write-only stream", "Potok");
			if (Potok.CanSeek)
				Console.WriteLine("{0}\t<Send stream with {2}K of {1}.", GetTime(BeginTime), ContentType, Potok.Length/1024);
			else
				Console.WriteLine("{0}\t<Send {1} stream.", GetTime(BeginTime), ContentType);
			try
			{
				ClientResponse.StatusCode = 200;
				ClientResponse.ProtocolVersion = new Version(1, 0);
				ClientResponse.ContentType = ContentType;
				if(Potok.CanSeek) ClientResponse.ContentLength64 = Potok.Length;
				if(Potok.CanSeek) Potok.Position = 0;
				Potok.CopyTo(ClientResponse.OutputStream);
				if(Close) ClientResponse.OutputStream.Close();
			}
			catch(Exception ex)
			{
				int ErrNo = 500;
				if (ex is FileNotFoundException) ErrNo = 404;
				SendError(ErrNo, "Cannot retreive stream.<br>" + ex.ToString().Replace("\n", "<br>"));
			}
		}
	}
}
