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
		LogWriter Log;


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
		Encoding SourceContentEncoding = Encoding.Default;
		Encoding OutputContentEncoding = ConfigFile.OutputEncoding;
		bool EnableTransliteration = false;

		bool DumpHeaders = false;
		bool DumpRequestBody = false;
		string DumpPath = "\0dump-%Url%.log";
		string OriginalURL = "";
		string DumpOfRequestBody = "Nothing dumped.";
		WebHeaderCollection DumpOfHeaders = new WebHeaderCollection();

		/// <summary>
		/// Convert a Web 2.0 page to Web 1.0-like page.
		/// </summary>
		/// <param name="ClientRequest">Request from HttpListener</param>
		/// <param name="ClientResponse">Response for HttpListener</param>
		public Transit(HttpListenerRequest ClientRequest, HttpListenerResponse ClientResponse, LogWriter Log)
		{
			this.ClientRequest = ClientRequest;
			this.ClientResponse = ClientResponse;
			this.Log = Log;
			#if DEBUG
			Log.WriteLine(" Begin process.");
			#endif
			try
			{

				ShouldRedirectInNETFW = false;

				//check for login to proxy if need
				if (ConfigFile.Authenticate.Count > 0)
				{
					switch(ClientRequest.Url.PathAndQuery){
						case "/!pac/":
						case "/auto/":
						case "/auto":
						case "/auto.pac":
						case "/wpad.dat":
						case "/wpad.da":
							//PAC is always unprotected
							break;
						default:
							if (ClientRequest.Headers["Proxy-Authorization"] == null || ClientRequest.Headers["Proxy-Authorization"] == "")
							{
								Log.WriteLine(" Unauthorized client.");
								ClientResponse.AddHeader("Proxy-Authenticate", @"Basic realm=""" + ConfigFile.AuthenticateRealm + @"""");
								SendError(407, ConfigFile.AuthenticateMessage);
								return;
							}
							else
							{
								string auth = Encoding.Default.GetString(Convert.FromBase64String(ClientRequest.Headers["Proxy-Authorization"].Substring(6)));
								if(!ConfigFile.Authenticate.Contains(auth))
								{
									Log.WriteLine(" Incorrect login: '{0}'.", auth);
									ClientResponse.AddHeader("Proxy-Authenticate", @"Basic realm=""" + ConfigFile.AuthenticateRealm + @" (retry)""");
									SendError(407, ConfigFile.AuthenticateMessage + "<p>Your login or password is not correct. Please try again.</p>");
									return;
								}
							}
							break;
					}
				}

				//get request URL and referer URL
				try { RequestURL = new UriBuilder(ClientRequest.RawUrl).Uri; }
				catch { RequestURL = ClientRequest.Url; };

				string RefererUri = ClientRequest.Headers["Referer"];

				//check for local or internal URL
				bool IsLocalhost = false;

				foreach (IPAddress address in GetLocalIPAddresses()) //todo: add external list support here
					if (RequestURL.Host.ToLower() == address.ToString().ToLower() || RequestURL.Host.ToLower() == "[" + address.ToString().ToLower() + "]")
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
						//request to internal URL
						try
						{
							Log.WriteLine(" Internal: {0} ", RequestURL.PathAndQuery);
							switch (RequestURL.AbsolutePath.ToLower())
							{
								case "/":
								case "/!":
								case "/!/":
									SendInternalStatusPage();
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
											HttpOperation HOper = new HttpOperation(Log);
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
													Log.WriteLine(">Downloading source stream (connecting)...");
#else
													Log.WriteLine(">Downloading source stream...");
#endif
													HOper.SendRequest();
#if DEBUG
													Log.WriteLine(">Downloading source stream (receiving)...");
#endif
													HOper.GetResponse();
													SrcStream = HOper.ResponseStream;
												}
												catch (Exception DlEx)
												{
													Log.WriteLine(" Converter cannot download source: {0}", DlEx.Message);
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
													Log.WriteLine(" Converter cannot open source: {0}", OpenEx.Message);
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
												SendStream(Cvt.Run(Log, SrcStream, Args1, Args2, Dest, SrcUrl), DestMime, true);
												return;
											}
											catch(Exception CvtEx)
											{
												Log.WriteLine(" Converter error: {0}", CvtEx.Message);
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
									/*string FileName, MimeType="text/plain";
									Match FindName = Regex.Match(RequestURL.Query, @"(name)=([^&]+)");
									Match FindMime = Regex.Match(RequestURL.Query, @"(type)=([^&]+)");

									if (FindMime.Success)
										MimeType = FindMime.Groups[2].Value;

									if (FindName.Success)
									{
										FileName = FindName.Groups[2].Value;

										if (!File.Exists(FileName))
										{ FileName = new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName + Path.DirectorySeparatorChar + FileName; }

										if (Path.TrimEndingDirectorySeparator(new FileInfo(FileName).DirectoryName) != Path.TrimEndingDirectorySeparator(Directory.GetCurrentDirectory()) &&
											Path.TrimEndingDirectorySeparator(new FileInfo(FileName).DirectoryName) != Path.TrimEndingDirectorySeparator(System.Reflection.Assembly.GetExecutingAssembly().Location))
										{
											SendError(403, "Cannot access to files outside current and proxy installation directory");
											return;
										}
									
										SendFile(FileName, MimeType);
										return;
									}
									SendError(200, "Get a local file.<br>Usage: /!file/?name=filename.ext&type=text/plain");
									return;
									*/
									SendError(403, "<del>Get a local file.<br>Usage: /!file/?name=filename.ext&type=text/plain</del>"+
									"<br><b>Disabled in this version due to security reasons.</b>"+
									"<br>To see used configuration file, open <a href=/!webone.conf>/!webone.conf</a>.");
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
									Log.WriteLine("<Return PAC/WPAD script.");
									string PacString =
									@"function FindProxyForURL(url, host) {" +
									@"if (url.substring(0, 5) == ""http:"")" +
									@"{ return ""PROXY "+ GetServerName() +@"""; }" +
									@"else { return ""DIRECT""; }"+
									@"} /*WebOne PAC*/";
									byte[] PacBuffer = Encoding.Default.GetBytes(PacString);
									try
									{
										ClientResponse.StatusCode = 200;
										ClientResponse.ProtocolVersion = new Version(1, 0);

										ClientResponse.ContentType = "application/x-ns-proxy-autoconfig";
										ClientResponse.ContentLength64 = PacString.Length;
										ClientResponse.OutputStream.Write(PacBuffer, 0, PacBuffer.Length);
										ClientResponse.OutputStream.Close();
									}
									catch
									{
										Log.WriteLine("Cannot return PAC!");
									}
									SaveHeaderDump();
									return;
								case "/robots.txt":
									//attempt to include in google index; kick the bot off
									Log.WriteLine("<Return robot kicker.");
									string Robots = "User-agent: *\nDisallow: / ";
									byte[] RobotsBuffer = Encoding.Default.GetBytes(Robots);
									try
									{
										ClientResponse.StatusCode = 200;
										ClientResponse.ProtocolVersion = new Version(1, 0);

										ClientResponse.ContentType = "text/plain";
										ClientResponse.ContentLength64 = Robots.Length;
										ClientResponse.OutputStream.Write(RobotsBuffer, 0, RobotsBuffer.Length);
										ClientResponse.OutputStream.Close();
									}
									catch
									{
										Log.WriteLine("Cannot return robot kicker!");
									}
									SaveHeaderDump();
									return;

								default:
									SendError(404, "Unknown internal URL: " + RequestURL.PathAndQuery);
									break;
							}
							return;
						}
						catch (Exception ex)
						{
							Log.WriteLine("!Internal server error: {0}", ex.ToString());
#if DEBUG
							SendError(500, "Internal server error: <b>" + ex.Message + "</b><br>" + ex.GetType().ToString() + " " + ex.StackTrace.Replace("\n","<br>"));
#else
							SendError(500, "WebOne cannot process the request because <b>" + ex.Message + "</b>.");
#endif
						}
					}
					//local proxy mode: http://localhost/http://example.com/indexr.shtml, http://localhost/http:/example.com/indexr.shtml
					if (RequestURL.LocalPath.StartsWith("/http:/") || RequestURL.LocalPath.StartsWith("/https:/"))
					{
						if(!(RequestURL.LocalPath.StartsWith("/http://") || RequestURL.LocalPath.StartsWith("/https://")))
						RequestURL = new Uri(RequestURL.AbsoluteUri.Replace("/http:/", "/http://").Replace("/https:/", "/https://"));
					}

					if (RequestURL.LocalPath.StartsWith("/http://") || RequestURL.LocalPath.StartsWith("/https://"))
					{
						RequestURL = new Uri(RequestURL.LocalPath.Substring(1) + RequestURL.Query);
						Log.WriteLine(" Local: {0}", RequestURL);
						LocalMode = true;
					}
					else
					{
						//dirty local mode, try to use last used host: http://localhost/favicon.ico
						RequestURL = new Uri("http://" + new Uri(LastURL).Host + RequestURL.LocalPath);
						if (RequestURL.Host == "999.999.999.999") { SendError(404, "The proxy server cannot guess domain name."); return; }
						Log.WriteLine(" Dirty local: {0}", RequestURL);
						LocalMode = true;
					}
				}

				if (LocalMode && ClientRequest.Headers["User-Agent"] != null && ClientRequest.Headers["User-Agent"].Contains("WebOne"))
				{
					SendError(403, "Loop requests are probhited.");
					return;
				}

				//check for HTTP-to-FTP requests
				//https://support.microsoft.com/en-us/help/166961/how-to-ftp-with-cern-based-proxy-using-wininet-api
				string[] BadProtocols = { "ftp", "gopher", "wais" };
				if (CheckString(RequestURL.Scheme, BadProtocols))
				{
					Log.WriteLine(" CERN Proxy request to {0} detected.", RequestURL.Scheme.ToUpper());
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
					Log.WriteLine(" Carousel detected.");
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

				if (RequestURL.AbsoluteUri.Contains("??")) { SendError(400, "Too many questions."); return; }
				if (RequestURL.AbsoluteUri.Length == 0) { SendError(400, "Empty URL."); return; }
				if (RequestURL.AbsoluteUri == "") return;

				if (RequestURL.AbsoluteUri.Contains(" ")) RequestURL = new Uri(RequestURL.AbsoluteUri.Replace(" ","%20")); //fix spaces in wrong-formed URLs

				//check for available edit sets
				foreach (EditSet set in ConfigFile.EditRules)
				{
					if (CheckStringRegExp(RequestURL.AbsoluteUri, set.UrlMasks.ToArray()) &&
						!CheckStringRegExp(RequestURL.AbsoluteUri, set.UrlIgnoreMasks.ToArray()))
					{
						if (set.HeaderMasks.Count > 0 && ClientRequest.Headers != null)
						{
							//check if there are headers listed in OnHeader detection rules
							bool HaveGoodMask = false;
							foreach (string HdrMask in set.HeaderMasks)
							{
								foreach (string RqHdrName in ClientRequest.Headers.AllKeys)
								{
									string header = RqHdrName + ": " + ClientRequest.Headers[RqHdrName];
									if (Regex.IsMatch(header, HdrMask)) 
									{
										HaveGoodMask = true;
										EditSets.Add(set);
										break;
									}
								}
								if (HaveGoodMask) break;
							}
						}
						else
						{
							EditSets.Add(set);
						}
					}
				}

				bool BreakTransit = false; //use instead of return

				try
				{
					int CL = 0;
					if (ClientRequest.Headers["Content-Length"] != null) CL = Int32.Parse(ClientRequest.Headers["Content-Length"]);

					//make and send HTTPS request to destination server
					WebHeaderCollection whc = new WebHeaderCollection();

					//prepare headers
					if (RequestURL.Scheme.ToLower() == "https")
					{
						foreach (string h in ClientRequest.Headers.Keys)
						{
							whc.Add(h, ClientRequest.Headers[h].Replace("http://", "https://"));
						}
					}
					else
					{
						foreach (string h in ClientRequest.Headers.Keys)
						{
							whc.Add(h, ClientRequest.Headers[h]);
						}
					}
					if (whc["Origin"] == null & whc["Referer"] != null) whc.Add("Origin: " + new Uri(whc["Referer"]).Scheme + "://" + new Uri(whc["Referer"]).Host);

					//perform edits on the request
					foreach (EditSet Set in EditSets)
					{
						if (Set.IsForRequest)
						{
							//if URL mask is single, allow use RegEx groups (if any) for replace
							bool UseRegEx = (Set.UrlMasks.Count == 1 && new Regex(Set.UrlMasks[0]).GetGroupNames().Count() > 1);
#if DEBUG
							if(UseRegEx) Log.WriteLine(" RegExp groups are available on {0}.", Set.UrlMasks[0]);
#endif

							foreach (EditSetRule Edit in Set.Edits)
							{
								switch (Edit.Action)
								{
									case "AddInternalRedirect":
										string NewUrlInternal = UseRegEx ? ProcessUriMasks(new Regex(Set.UrlMasks[0]).Replace(RequestURL.AbsoluteUri, Edit.Value), RequestURL.AbsoluteUri)
																		 : ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri);
										Log.WriteLine(" Fix to {0} internally", NewUrlInternal);
										SaveHeaderDump("Internal redirect to " + NewUrlInternal + "\nThen continue.");
										RequestURL = new Uri(NewUrlInternal);
										break;
									case "AddRedirect":
										string NewUrl302 = UseRegEx ? ProcessUriMasks (new Regex(Set.UrlMasks[0]).Replace(RequestURL.AbsoluteUri, Edit.Value), RequestURL.AbsoluteUri)
																	: ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri);
										Log.WriteLine(" Fix to {0}", NewUrl302);
										ClientResponse.AddHeader("Location", NewUrl302);
										SendError(302, "Брось каку!");
										return;
									case "AddRequestHeader":
									case "AddHeader":
										//Log.WriteLine(" Add request header: {0}", Edit.Value);
										if (whc[Edit.Value.Substring(0, Edit.Value.IndexOf(": "))] == null) whc.Add(ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri));
										break;
									case "AddRequestHeaderFindReplace":
										FindReplaceEditSetRule hdr_rule = (FindReplaceEditSetRule)Edit;
										foreach(var hdr in whc.AllKeys)
										{
											whc[hdr] = whc[hdr].Replace(hdr_rule.Find, hdr_rule.Replace);
										}
										break;
									case "AddHeaderDumping":
									case "AddRequestDumping":
										DumpHeaders = true;
										DumpPath = ProcessUriMasks(
											Edit.Value,
											RequestURL.ToString(),
											true
											);
										DumpRequestBody = Edit.Action == "AddRequestDumping";
										break;
									case "AddOutputEncoding":
										OutputContentEncoding = GetCodePage(Edit.Value);
										break;
									case "AddTranslit":
										EnableTransliteration = ToBoolean(Edit.Value);
										break;
								}
							}
						}
					}

					//save dump of headers if need for debugging (a-la Chromium devtools Network tab)
					if (DumpHeaders) {
						OriginalURL = RequestURL.AbsoluteUri;
						foreach (string hdrname in whc.AllKeys)
						{
							if (hdrname == "User-Agent")
							{
								DumpOfHeaders.Add("REAL-User-Agent", whc[hdrname]);
								DumpOfHeaders.Add("SENT-User-Agent", GetUserAgent(whc[hdrname]));
							}
							else DumpOfHeaders.Add(hdrname, whc[hdrname]);
						}
					}

					//send the request
					SendRequest(operation, ClientRequest.HttpMethod, whc, CL);
				}
				catch (WebException wex)
				{
					if (wex.Response == null) ResponseCode = 502;
					else ResponseCode = (int)(wex.Response as HttpWebResponse).StatusCode;
					if (ResponseCode == 502) Log.WriteLine(" Cannot load this page: {0}.", wex.Status);
					else Log.WriteLine(" Web exception: {0} {1}.", ResponseCode, (wex.Response as HttpWebResponse).StatusCode);


					//check if archived copy can be retreived instead
					if (ConfigFile.SearchInArchive)
						if ((wex.Status == WebExceptionStatus.NameResolutionFailure) ||
							(wex.Response != null && (wex.Response as HttpWebResponse).StatusCode == HttpStatusCode.NotFound))
						{
							try
							{
								Log.WriteLine(" Look in Archive.org...");
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
										Log.WriteLine(" Available.");
										ArchiveURL = ArchiveMatch.Value.Substring(8, ArchiveMatch.Value.IndexOf(@""",") - 8);
										ResponseBody = "<html><body><h1>Server not found</h2>But an <a href=" + ArchiveURL + ">archived copy</a> is available! Redirecting to it...</body></html>";
										ClientResponse.AddHeader("Location", ArchiveURL);
										SendError(302, ResponseBody);
										return;
									}
									else
									{
										Log.WriteLine(" Available, but somewhere.");
									}
								}
								else
								{
									Log.WriteLine(" No snapshots.");
									if (RequestURL.AbsoluteUri.StartsWith("http://web.archive.org/web/") && ConfigFile.ShortenArchiveErrors)
									{
										string ErrMsg =
										"<p><b>The Wayback Machine has not archived that URL.</b></p>" +
										"<p>This page is not available on the web because page does not exist<br>" +
										"Try to slightly change the URL.</p>" +
										"<small><i>You see this message because ShortenArchiveErrors option is enabled.</i></small>";
										SendError(404, ErrMsg);
										BreakTransit = true;
									}
								}
							}
							catch (Exception ArchiveException)
							{
								SendInfoPage("WebOne: Web Archive error.", "Cannot load this page", string.Format("<b>The requested server or page is not found and a Web Archive error occured.</b><br>{0}", ArchiveException.Message.Replace("\n", "<br>")));
								BreakTransit = true;
							}
						}

					ContentType = "text/html";
#if DEBUG
					string err = ": " + wex.Status.ToString();
					SendInfoPage("WebOne cannot load the page", "Can't load the page: " + wex.Status.ToString(), "<i>" + wex.ToString().Replace("\n", "<br>") + "</i><br>URL: " + RequestURL.AbsoluteUri + "<br>Debug mode enabled.");
					BreakTransit = true;
#else
					string NiceErrMsg;

					switch(wex.Status){
						case WebExceptionStatus.UnknownError:
							if (wex.InnerException != null)
							{
								if(wex.Message.Contains(GetFullExceptionMessage(wex, true, true)))
								{
									NiceErrMsg = " <p><big>" + wex.Message + "</big></p>Kind of error: " + wex.InnerException.GetType().ToString();
								}
								else
								{
									NiceErrMsg = " <p><big>" + wex.Message + "</big></p><p>" + GetFullExceptionMessage(wex, true, true).Replace("\n", "<br>") + "</p>Kind of error: " + wex.InnerException.GetType().ToString();
								}
							}
							else
								NiceErrMsg = " <p><big>" + wex.Message + "</big></p>Kind of error: " + wex.GetType().ToString() + " (no inner exceptions)";
							break;
						default:
							NiceErrMsg = "<p><big>" + wex.Message + ".</big></p>Status: " + wex.Status;
							break;
					}

					string ErrorMessage = NiceErrMsg + "<br>URL: " + RequestURL.AbsoluteUri;
					SendInfoPage("WebOne: " + wex.Status, "Cannot load this page", ErrorMessage);
					BreakTransit = true;
#endif
				}
				catch (UriFormatException)
				{
					BreakTransit = true;
					SendError(400, "The URL <b>" + RequestURL.AbsoluteUri + "</b> is not valid.");
				}
				catch (Exception ex)
				{
					BreakTransit = true;
					Log.WriteLine(" ============GURU MEDITATION:\n{1}\nOn URL '{2}', Method '{3}'. Returning 500.============", null, ex.ToString(), RequestURL.AbsoluteUri, ClientRequest.HttpMethod);
					SendError(500, "Guru meditaion at URL " + RequestURL.AbsoluteUri + ":<br><b>" + ex.Message + "</b><br><i>" + ex.StackTrace.Replace("\n", "\n<br>") + "</i>");
				}

				//try to return...
				try
				{
					if (!BreakTransit)
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
							RespBuffer = (OutputContentEncoding ?? SourceContentEncoding).GetBytes(ResponseBody).ToArray();

							ClientResponse.ContentLength64 = RespBuffer.Length;

							if (ClientResponse.ContentLength64 > 300*1024) Log.WriteLine(" Sending binary.");
							ClientResponse.OutputStream.Write(RespBuffer, 0, RespBuffer.Length);
						}
						else
						{
							if(TransitStream.CanSeek) ClientResponse.ContentLength64 = TransitStream.Length;
							TransitStream.CopyTo(ClientResponse.OutputStream);
						}
						ClientResponse.OutputStream.Close();
#if DEBUG
						Log.WriteLine(" Document sent.");
#endif
					}
#if DEBUG
					else Log.WriteLine(" Original document lost.");
#endif
				}
				catch (Exception ex)
				{
					if (!ConfigFile.HideClientErrors)
#if DEBUG
						Log.WriteLine("<Can't return reply. " + ex.Message + ex.StackTrace);
#else
						Log.WriteLine("<Can't return reply. " + ex.Message);
#endif
				}
			}
			catch(Exception E)
			{
				Log.WriteLine(" A error has been catched: {1}\n{0}\t Please report to author.", null, E.ToString().Replace("\n", "\n{0}\t "));
				SendError(500, "An error occured: " + E.ToString().Replace("\n", "\n<BR>"));
				if (operation != null) operation.Dispose();
			}
			SaveHeaderDump();
			if (operation != null) operation.Dispose();
#if DEBUG
			Log.WriteLine(" End process.");
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
					Log.WriteLine(" Wrong method.");
					return;
				default:
					operation = new HttpOperation(Log);
					if (Content_Length == 0)
					{
						//try to download (GET, HEAD, WebDAV download, etc)
#if DEBUG
						Log.WriteLine(">Downloading content (connecting)...");
#else
						Log.WriteLine(">Downloading content...");
#endif
						operation.URL = RequestURL.AbsoluteUri;
						operation.Method = RequestMethod;
						operation.RequestHeaders = RequestHeaderCollection;
						operation.AllowAutoRedirect = AllowAutoRedirect;
						operation.SendRequest();
#if DEBUG
						Log.WriteLine(">Downloading content (receiving)...");
#endif
						operation.GetResponse();
						MakeOutput(operation);
						break;
					}
					else
					{
						//try to upload (POST, PUT, WebDAV, etc)
#if DEBUG
						Log.WriteLine(">Uploading {0}K of {1} (connecting)...",Convert.ToInt32((RequestHeaderCollection["Content-Length"])) / 1024, RequestHeaderCollection["Content-Type"]);
#else
						Log.WriteLine(">Uploading {0}K of {1}...", Convert.ToInt32((RequestHeaderCollection["Content-Length"])) / 1024, RequestHeaderCollection["Content-Type"]);
#endif
						operation.URL = RequestURL.AbsoluteUri;
						operation.Method = RequestMethod;
						operation.RequestHeaders = RequestHeaderCollection;
						if (!DumpRequestBody)
							operation.RequestStream = ClientRequest.InputStream;
						else
						{
							//if need to create a dump of request's body
							MemoryStream RequestDumpStream = new MemoryStream();
							ClientRequest.InputStream.CopyTo(RequestDumpStream);
							RequestDumpStream.Position = 0;
							DumpOfRequestBody = new StreamReader(RequestDumpStream).ReadToEnd();
							RequestDumpStream.Position = 0;
							operation.RequestStream = RequestDumpStream;
						}
						operation.AllowAutoRedirect = AllowAutoRedirect;
						operation.SendRequest();
#if DEBUG
						Log.WriteLine(">Uploading content (receiving)...");
#endif
						operation.GetResponse();
						MakeOutput(operation);
						break;
					}
			}

			if (Stop) return; //if converting has occur and the request should not be processed next

			//todo: rewrite and move to MakeOutput or operation.SendRequest!
			ResponseCode = (int)operation.Response.StatusCode;

			//check for security upgrade
			if (ResponseCode >= 301 && ResponseCode <= 399)
			{
				if (operation.ResponseHeaders != null)
				{
					string NewLocation = operation.ResponseHeaders["Location"];

					if (NewLocation != null)
					{
						if (RequestURL.AbsoluteUri == NewLocation.Replace("https://", "http://")
							&& !CheckString(RequestURL.AbsoluteUri, ConfigFile.InternalRedirectOn))
						{
							Log.WriteLine(">Reload secure...");

							RequestURL = new Uri(RequestURL.AbsoluteUri.Replace("http://", "https://"));
							TransitStream = null;
							SendRequest(operation, RequestMethod, RequestHeaderCollection, Content_Length);

							//add to ForceHttp list
							string SecureHost = RequestURL.Host;
							if (!ConfigFile.ForceHttps.Contains(SecureHost))
								ConfigFile.ForceHttps.Add(SecureHost);

							return;
						}

						if(NewLocation.StartsWith("https://")
						   && !CheckString(RequestURL.AbsoluteUri, ConfigFile.InternalRedirectOn))
						{
#if DEBUG
							Log.WriteLine(" The next request will be secure.");
#endif

							//add to ForceHttp list
							string SecureHost = RequestURL.Host;
							if (!ConfigFile.ForceHttps.Contains(SecureHost))
								ConfigFile.ForceHttps.Add(SecureHost);

							//return;
						}
					}
				}
			}

			//process response headers
			if(operation.ResponseHeaders != null)
			for (int i = 0; i < operation.ResponseHeaders.Count; ++i)
			{
				string header = operation.ResponseHeaders.GetKey(i);
				foreach (string value in operation.ResponseHeaders.GetValues(i))
				{
					string corrvalue = value.Replace("https://", "http://");
					if(LocalMode)
					{
						if (corrvalue.StartsWith("http://") && !corrvalue.StartsWith("http://" + GetServerName()))
							corrvalue = corrvalue.Replace("http://", "http://" + GetServerName() + "/http://");

						corrvalue = corrvalue.Replace("; Domain=http://", "; Domain="  + GetServerName());
						corrvalue = corrvalue.Replace("; domain=http://", "; domain=" + GetServerName());
						corrvalue = corrvalue.Replace("; Domain=.", "; Domain=" + ConfigFile.DefaultHostName + "; WebOne-orig-domain=");
						corrvalue = corrvalue.Replace("; domain=.", "; domain=" + ConfigFile.DefaultHostName + "; webone-orig-domain=");
						corrvalue = corrvalue.Replace("; Path=/", "; WebOne-NoPath=/");
						corrvalue = corrvalue.Replace("; path=/", "; webone-nopath=/");
					}
					
					//todo: rewrite cookie processing due to bug #21

					corrvalue = corrvalue
						.Replace("; secure", "")
						.Replace("; Secure", "");

					if (!header.StartsWith("Content-") &&
					!header.StartsWith("Connection") &&
					!header.StartsWith("Transfer-Encoding") &&
					!header.StartsWith("Access-Control-Allow-Methods") &&
					!header.StartsWith("Strict-Transport-Security") &&
					!header.StartsWith("Content-Security-Policy") &&
					!header.StartsWith("Upgrade-Insecure-Requests") &&
					!(header.StartsWith("Vary") && corrvalue.Contains("Upgrade-Insecure-Requests")))
					{
						ClientResponse.AddHeader(header, corrvalue);
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

			if (OutputContentEncoding != null)
			{
				Body = Body.Replace("harset=\"utf-8\"", "harset=\"" + OutputContentEncoding.WebName + "\"");
				Body = Body.Replace("harset=\"UTF-8\"", "harset=\"" + OutputContentEncoding.WebName + "\"");
				Body = Body.Replace("harset=utf-8", "harset=" + OutputContentEncoding.WebName);
				Body = Body.Replace("harset=UTF-8", "harset=" + OutputContentEncoding.WebName);
				Body = Body.Replace("CHARSET=UTF-8", "CHARSET=" + OutputContentEncoding.WebName);
				Body = Body.Replace("ncoding=\"utf-8\"", "ncoding=\"" + OutputContentEncoding.WebName + "\"");
				Body = Body.Replace("ncoding=\"UTF-8\"", "ncoding=\"" + OutputContentEncoding.WebName + "\"");
				Body = Body.Replace("ncoding=utf-8", "ncoding=" + OutputContentEncoding.WebName);
				Body = Body.Replace("ncoding=UTF-8", "ncoding=" + OutputContentEncoding.WebName);
				Body = Body.Replace("ENCODING=UTF-8", "ENCODING=" + OutputContentEncoding.WebName);
				Body = Body.Replace(OutputContentEncoding.GetString(UTF8BOM), "");
				Body = OutputContentEncoding.GetString(Encoding.Convert(Encoding.UTF8, OutputContentEncoding, Encoding.UTF8.GetBytes(Body)));
			}
			//perform edits on the response body
			foreach (EditSet Set in EditSets)
			{
				if (Set.ContentTypeMasks.Count == 0 || CheckStringRegExp(ContentType, Set.ContentTypeMasks.ToArray()))
				{
					if (CheckHttpStatusCode(Set.OnCode, operation.Response.StatusCode))
					{ 
						foreach (EditSetRule Edit in Set.Edits)
						{
							switch (Edit.Action)
							{
								case "AddFindReplace":
									FindReplaceEditSetRule frpair = Edit as FindReplaceEditSetRule;
									Body = Regex.Replace(Body, frpair.Find, frpair.Replace, RegexOptions.Singleline);
									break;
							}
						}
					}
				}
			}

			//do transliteration if need
			if(EnableTransliteration)
			{
				foreach(var Letter in ConfigFile.TranslitTable)
				{
					Body = Body.Replace(Letter.Key, Letter.Value);
				}
			}

			//fix the body if it will be deliveried through Local mode
			if (LocalMode)
			{
				Body = Body.Replace("http://", "http://" + GetServerName() + "/http://");
				Body = Body.Replace("href=\"./", "href=\"http://" + GetServerName() + "/http://" + RequestURL.Host + "/");
				Body = Body.Replace("src=\"./", "src=\"http://" + GetServerName() + "/http://" + RequestURL.Host + "/");
				Body = Body.Replace("action=\"./", "action=\"http://" + GetServerName() + "/http://" + RequestURL.Host + "/");
				Body = Body.Replace("href=\"//", "href=\"http://" + GetServerName() + "/http://");
				Body = Body.Replace("src=\"//", "src=\"http://" + GetServerName() + "/http://");
				Body = Body.Replace("action=\"//", "action=\"http://" + GetServerName() + "/http://");
				Body = Body.Replace("href=\"/", "href=\"http://" + GetServerName() + "/http://" + RequestURL.Host + "/");
				Body = Body.Replace("src=\"/", "src=\"http://" + GetServerName() + "/http://" + RequestURL.Host + "/");
				Body = Body.Replace("action=\"/", "action=\"http://" + GetServerName() + "/http://" + RequestURL.Host + "/");
			}
			
			return Body;
		}


		/// <summary>
		/// Prepare response body for tranfer to client
		/// </summary>
		/// <param name="Operation">HttpOperation which describes the source response</param>
		/// <returns>ResponseBuffer+ResponseBody for texts or TransitStream for binaries</returns>
		private void MakeOutput(HttpOperation Operation)
		{
			MakeOutput(Operation.Response.StatusCode, Operation.ResponseStream, Operation.Response.ContentType, Operation.Response.ContentLength, Operation);
		}

		/// <summary>
		/// Prepare response body for tranfer to client
		/// </summary>
		/// <param name="StatusCode">HTTP Status code</param>
		/// <param name="ResponseStream">Stream of response body</param>
		/// <param name="ContentType">HTTP Content-Type</param>
		/// <param name="ContentLength">HTTP Content-Lenght</param>
		/// <param name="Operation">The HTTP Request/Response pair (will replace all other arguments in future)</param>
		/// <returns>ResponseBuffer+ResponseBody for texts or TransitStream for binaries</returns>
		private void MakeOutput(HttpStatusCode StatusCode, Stream ResponseStream, string ContentType, long ContentLength, HttpOperation Operation)
		{
			//todo: rewrite and remove all arguments except Operation
			this.ContentType = ContentType;
			string SrcContentType = ContentType;

			//perform edits on the response: common tasks for all bin/text
			string Converter = null;
			string ConvertDest = "";
			string ConvertArg1 = "";
			string ConvertArg2 = "";
			string Redirect = null;

			//perform edits on the response
			foreach (EditSet Set in EditSets)
			{
				if (Set.ContentTypeMasks.Count == 0 || CheckStringRegExp(Set.ContentTypeMasks.ToArray(), ContentType))
				{
					if (CheckHttpStatusCode(Set.OnCode, operation.Response.StatusCode))
					{
						foreach (EditSetRule Edit in Set.Edits)
						{
							switch (Edit.Action)
							{
								case "AddConverting":
									ConvertEditSetRule rule = (ConvertEditSetRule)Edit;
									Converter = rule.Converter;
									ConvertDest = rule.ConvertDest;
									ConvertArg1 = rule.ConvertArg1;
									ConvertArg2 = rule.ConvertArg2;
									Stop = true;
									break;
								case "AddResponseHeader":
									Log.WriteLine(" Add response header: {0}", ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri));
									operation.ResponseHeaders.Add(ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri));
									if (Edit.Value.StartsWith("Content-Type: ")) ContentType = Edit.Value.Substring("Content-Type: ".Length);
									break;
								case "AddResponseHeaderFindReplace":
									FindReplaceEditSetRule resp_rule = (FindReplaceEditSetRule)Edit;
									foreach (var hdr in operation.ResponseHeaders.AllKeys)
									{
										operation.ResponseHeaders[hdr] = operation.ResponseHeaders[hdr].Replace(resp_rule.Find, resp_rule.Replace);
									}
									break;
								case "AddRedirect":
									Log.WriteLine(" Add redirect: {0}", ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri));
									Redirect = ProcessUriMasks(Edit.Value, RequestURL.AbsoluteUri);
									break;
								case "AddHeaderDumping":
								case "AddRequestDumping":
									DumpHeaders = true;
									DumpPath = ProcessUriMasks(
										Edit.Value,
										RequestURL.ToString(),
										true
										);
									DumpRequestBody = Edit.Action == "AddRequestDumping";
									break;
							}
						}
					}
				}
			}

			//check for edit: AddRedirect
			if (Redirect != null)
			{
				Log.WriteLine(" {1} {2}. Body {3}K of {4} [Need to redirect].", null, (int)StatusCode, StatusCode, ContentLength / 1024, SrcContentType);
				ClientResponse.AddHeader("Location", Redirect);
				SendError(302, "Redirect requested.");
				return;
			}

			//check for edit: AddConvert
			if (Converter != null)
			{
				Log.WriteLine(" {1} {2}. Body {3}K of {4} [Wants {5}].", null, (int)StatusCode, StatusCode, ContentLength / 1024, SrcContentType, Converter);

				try
				{
					foreach (Converter Cvt in ConfigFile.Converters)
					{
						if (Cvt.Executable == Converter)
						{
							if(!Cvt.SelfDownload)
								SendStream(Cvt.Run(Log, ResponseStream, ConvertArg1, ConvertArg2, ConvertDest, RequestURL.AbsoluteUri), ContentType, true);
							else
							{
								SendStream(Cvt.Run(Log, null, ConvertArg1, ConvertArg2, ConvertDest, RequestURL.AbsoluteUri), ContentType, true);
								//if(operation.Response != null) Log.WriteLine(" '{1}' will download the source again.", null, Converter); //for future
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
					Log.WriteLine(" On-fly converter error: {0}", ConvertEx.Message);
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
				Log.WriteLine(" {1} {2}. Body {3}K of {4} [Text].", null, (int)StatusCode, StatusCode, ContentLength / 1024, ContentType);
#if DEBUG
				if (Operation.ResponseHeaders["Location"] != null)
				{
					Log.WriteLine(" {0} redirect to {1}.", (int)StatusCode, Operation.ResponseHeaders["Location"]);
					Log.WriteLine(" {0} redirect from {1}.", (int)StatusCode, Operation.Request.RequestUri.AbsoluteUri);
				}
#endif
				byte[] RawContent = null;
				RawContent = ReadAllBytes(ResponseStream);

				SourceContentEncoding = FindContentCharset(RawContent);

				if (OutputContentEncoding == null)
				{
					//if don't touch codepage (OutputContentEncoding=AsIs)
					ResponseBody = SourceContentEncoding.GetString(RawContent);
					ResponseBody = ProcessBody(ResponseBody);
#if DEBUG
					Log.WriteLine(" Body maked (codepage AsIs).");
#endif
					return;
				}

				bool ForceUTF8 = ContentType.ToLower().Contains("utf-8");
				if (!ForceUTF8 && RawContent.Length > 0) ForceUTF8 = SourceContentEncoding == Encoding.UTF8;
				foreach (string utf8url in ConfigFile.ForceUtf8) { if (Regex.IsMatch(RequestURL.AbsoluteUri, utf8url)) ForceUTF8 = true; }
				//todo: add fix for "invalid range in character class" at www.yandex.ru with Firefox 3.6 if OutputEncoding!=AsIs

				if (ForceUTF8) ResponseBody = Encoding.UTF8.GetString(RawContent);
				else ResponseBody = SourceContentEncoding.GetString(RawContent);

				if (Regex.IsMatch(ResponseBody, @"<meta.*UTF-8.*>", RegexOptions.IgnoreCase)) { ResponseBody = Encoding.UTF8.GetString(RawContent); }

				if (ContentType.ToLower().Contains("utf-8")) ContentType = ContentType.Substring(0, ContentType.IndexOf(';'));
				ResponseBody = ProcessBody(ResponseBody);
				this.ContentType = ContentType;
			}
			else
			{
				if (operation != null)
					Log.WriteLine(" {1} {2}. Body {3}K of {4} [Binary].", null, (int)StatusCode, StatusCode, operation.Response.ContentLength / 1024, ContentType);
				else
					Log.WriteLine(" {1} {2}. Body is {3} [Binary], incomplete.", null, (int)StatusCode, StatusCode, ContentType);

#if DEBUG
				if (Operation.ResponseHeaders["Location"] != null)
				{
					Log.WriteLine(" {0} redirect to {1}.", (int)StatusCode, Operation.ResponseHeaders["Location"]);
					Log.WriteLine(" {0} redirect from {1}.", (int)StatusCode, Operation.Request.RequestUri.AbsoluteUri);
				}
#endif

				TransitStream = ResponseStream;
				this.ContentType = ContentType;
			}
#if DEBUG
			Log.WriteLine(" Body maked (codepage changed).");
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
		/// Save header dump if need
		/// </summary>
		/// <param name="Epilogue">The epilogue (the finish) for log entry.</param>
		private void SaveHeaderDump(string Epilogue = "Complete.")
		{
			if (DumpHeaders == false || DumpPath.Contains ("\0")) return;

			Log.WriteLine(" Save headers to: {0}", DumpPath);
			string SniffLog = string.Format("{0} request to {1} HTTP/{2}\n", ClientRequest.HttpMethod, RequestURL.ToString(), ClientRequest.ProtocolVersion);
			if (OriginalURL != RequestURL.AbsoluteUri) SniffLog += "Original URL was: " + OriginalURL + "\n";

			foreach(string hdrname in DumpOfHeaders.AllKeys)
			{
				SniffLog += hdrname + ": " + DumpOfHeaders[hdrname] + "\n";
			}

			if (DumpRequestBody)
			{
				SniffLog += ClientRequest.HasEntityBody ? "Body goes below. CAUTION: Private area!\n" + DumpOfRequestBody + "\n\n" : "No body.\n\n";
			}
			else { SniffLog += ClientRequest.HasEntityBody ? "Body is hidden.\n\n" : "No body.\n\n"; }
			

			if (ClientResponse != null)
			{
				SniffLog += string.Format("Response {0} {1} HTTP/{2}\n", ClientResponse.StatusCode, ClientResponse.StatusDescription, ClientResponse.ProtocolVersion);
				foreach (string hdrname in ClientResponse.Headers.AllKeys)
				{
					SniffLog += hdrname + ": " + ClientResponse.Headers[hdrname] + "\n";
				}
			}
			else { SniffLog += "Custom response.\n"; }

			SniffLog += Epilogue;

			SniffLog = SniffLog.Replace("\n", "\r\n");

			try
			{
				if (File.Exists(DumpPath)) SniffLog = "\n\n---------\n\n" + SniffLog;
				var SniffWriter = new StreamWriter(DumpPath, true);
				SniffWriter.Write(SniffLog);
				SniffWriter.Close();
			}
			catch(Exception ex)
			{
				Log.WriteLine(" Cannot save headers: {0}!", ex.Message);
			}
		}

		/// <summary>
		/// Get this proxy server name and port
		/// </summary>
		private string GetServerName()
		{
			if (ConfigFile.Port == 80) return ConfigFile.DefaultHostName;
			return ConfigFile.DefaultHostName + ":" + ConfigFile.Port.ToString();
		}

		/// <summary>
		/// Send internal status page (http://proxyhost:port/!)
		/// </summary>
		private void SendInternalStatusPage(){
			string HelpString = "";

			if(ConfigFile.DisplayStatusPage == "no")
			{
				SendInfoPage("WebOne status page", "Sorry", "<p>The status page is disabled by server administrator.</p>");
				return;
			}

			if (ConfigFile.DisplayStatusPage == "short")
			{
				HelpString += "<p>This is <b>" + GetServerName() + "</b>.<br>";
				HelpString += "Pending requests: <b>" + (Load - 1) + "</b>.<br>";
				HelpString += "Used memory: <b>" + (int)Environment.WorkingSet / 1024 / 1024 + "</b> MB.<br>";
				HelpString += "About: <a href=\"https://github.com/atauenis/webone/\">https://github.com/atauenis/webone/</a></p>";
				HelpString += "<p>Client IP: <b>" + ClientRequest.RemoteEndPoint + "</b>.</p>";

				HelpString += "<h2>May be useful:</h2><ul>";
				HelpString += "<li><a href='/auto.pac'>Proxy auto-configuration file</a>: /!pac/, /auto/, /auto, /auto.pac, /wpad.dat.</li>";
				HelpString += "</ul>";
			}
			else if (ConfigFile.DisplayStatusPage == "full")
			{
				HelpString += "This is <b>" + Environment.MachineName + ":" + ConfigFile.Port + "</b>.<br>";
				HelpString += "Used memory: <b>" + (double)Environment.WorkingSet / 1024 / 1024 + "</b> MB.<br>";
				HelpString += "Pending requests: <b>" + (Load - 1) + "</b>.<br>";
				HelpString += "Available security: <b>" + ServicePointManager.SecurityProtocol + "</b> (" + (int)ServicePointManager.SecurityProtocol + ").<br>";

				HelpString += "<h2>Aliases:</h2><ul>";
				bool EvidentAlias = false;
				foreach (IPAddress address in GetLocalIPAddresses())
				{
					HelpString += "<li>" + (address.ToString() == ConfigFile.DefaultHostName ? "<b>" + address.ToString() + "</b>" : address.ToString()) + ":" + ConfigFile.Port + "</li>";
					if (!EvidentAlias) EvidentAlias = address.ToString() == ConfigFile.DefaultHostName;
				}
				if (!EvidentAlias) HelpString += "<li><b>" + ConfigFile.DefaultHostName + "</b>:" + ConfigFile.Port + "</li>";
				HelpString += "</ul>";
				HelpString += "</ul>";

				HelpString += "<p>Client IP: <b>" + ClientRequest.RemoteEndPoint + "</b>.</p>";


				HelpString += "<h2>Internal URLs:</h2><ul>" +
							  //			  "<li><a href='/!codepages/'>/!codepages/</a> - list of available encodings for OutputEncoding setting</li>" +
							  "<li><a href='/!img-test/'>/!img-test/</a> - test if ImageMagick is working</li>" +
							  "<li><a href='/!convert/'>/!convert/</a> - run a file format converter (<a href='/!convert/?src=logo.webp&dest=gif&type=image/gif'>demo</a>)</li>" +
							  //"<li><a href='/!file/'>/!file/</a> - get a file from WebOne working directory (<a href='/!file/?name=webone.conf&type=text/plain'>demo</a>)</li>" +
							  "<li><a href='/!clear/'>/!clear/</a> - remove temporary files in WebOne working directory</li>" +
							  "<li><a href='/auto.pac'>Proxy auto-configuration file</a>: /!pac/, /auto/, /auto, /auto.pac, /wpad.dat.</li>" +
							  "</ul>";
			}

			else
			{
				HelpString = "<h2>It works!</h2>";
			}


			HelpString += "<h2>Headers sent by browser</h2><ul>";
			HelpString += "<li><b>" + ClientRequest.HttpMethod + " " + ClientRequest.RawUrl + " HTTP/" + ClientRequest.ProtocolVersion + "</b></li>";
			foreach (string hdrn in ClientRequest.Headers.Keys)
			{
				HelpString += "<li>" + hdrn + ": " + ClientRequest.Headers[hdrn] + "</li>";
			}
			HelpString += "</ul>";
			SendInfoPage("WebOne status page", null, HelpString);
			return;

		}

		/// <summary>
		/// Send a HTTP error to client
		/// </summary>
		/// <param name="Code">HTTP Status code</param>
		/// <param name="Text">Text of message</param>
		private void SendError(int Code, string Text = "")
		{
			Log.WriteLine("<Return code {0}.", Code);
			Text += GetInfoString();
			string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
			string Refresh = "";
			if (ClientResponse.Headers["Refresh"] != null) Refresh = "<META HTTP-EQUIV=\"REFRESH\" CONTENT=\""+ ClientResponse.Headers["Refresh"] +"\">";
			Refresh += "<META CHARSET=\"" + (OutputContentEncoding ?? Encoding.Default).WebName + "\">";
			string Html = "<HTML>" + Refresh + "<BODY><H1>" + CodeStr + "</H1>" + Text + "</BODY></HTML>";

			byte[] Buffer = (OutputContentEncoding ?? Encoding.Default).GetBytes(Html);
			try
			{
				ClientResponse.StatusCode = Code;
				ClientResponse.ProtocolVersion = new Version(1, 0);

				ClientResponse.ContentType = "text/html";
				ClientResponse.ContentLength64 = Buffer.Length;
				ClientResponse.OutputStream.Write(Buffer, 0, Buffer.Length);
				ClientResponse.OutputStream.Close();
				SaveHeaderDump("End is internal page: code " + Code + ", " + Text);
			}
			catch(Exception ex)
			{
				if(!ConfigFile.HideClientErrors)
					Log.WriteLine("<!Cannot return code {1}. {2}: {3}", null, Code, ex.GetType(), ex.Message);
			}
		}

		/// <summary>
		/// Send a file to client
		/// </summary>
		/// <param name="FileName">Full path to the file</param>
		/// <param name="ContentType">File's content-type.</param>
		private void SendFile(string FileName, string ContentType)
		{
			Log.WriteLine("<Send file {0}.", FileName);
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
			SaveHeaderDump("End is file " + FileName);
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
				Log.WriteLine("<Send stream with {2}K of {1}.", null, ContentType, Potok.Length/1024);
			else
				Log.WriteLine("<Send {0} stream.", ContentType);
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
			SaveHeaderDump("End is stream of " + ContentType);
		}

		/// <summary>
		/// Send a information page to client
		/// </summary>
		/// <param name="Title">The information page title</param>
		/// <param name="Header1">The information page 1st level header (or null if no title)</param>
		/// <param name="Content">The information page content (HTML)</param>
		private void SendInfoPage(string Title = null, string Header1 = null, string Content = "No description is available.")
		{
			Log.WriteLine("<Return information page: {0}.", Title);

			string title = "<title>WebOne: untitled</title>"; if (Title != null) title = "<title>" + Title + "</title>\n";
			string header1 = ""; if (Header1 != null) header1 = "<h1>" + Header1 + "</h1>\n";

			string Html = "<html>\n" +
			title +
			string.Format("<meta charset=\"{0}\"/>", OutputContentEncoding == null ? "utf-16" : OutputContentEncoding.WebName) +
			"<body>" +
			header1 +
			Content +
			GetInfoString() + 
			"</body>\n</html>";

			if ((OutputContentEncoding ?? Encoding.Default) != Encoding.Default)
				Html = OutputContentEncoding.GetString(Encoding.Default.GetBytes(Html));

			byte[] Buffer = (OutputContentEncoding ?? Encoding.Default).GetBytes(Html);
			try
			{
				ClientResponse.StatusCode = 200;
				ClientResponse.ProtocolVersion = new Version(1, 0);

				ClientResponse.ContentType = "text/html";
				ClientResponse.ContentLength64 = Buffer.Length;
				ClientResponse.OutputStream.Write(Buffer, 0, Buffer.Length);
				ClientResponse.OutputStream.Close();
				SaveHeaderDump("End is information page: " + Header1);
			}
			catch (Exception ex)
			{
				if (!ConfigFile.HideClientErrors)
					Log.WriteLine("<!Cannot return information page {1}. {2}: {3}", null, Title, ex.GetType(), ex.Message);
			}
		}
	}
}
