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

		HttpClientResponse response;
		int ResponseCode = 502;
		string ResponseBody = ":(";
		Stream TransitStream = null;
		string ContentType = "text/plain";


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
				var LocalIPs = Dns.GetHostEntry(Environment.MachineName).AddressList;
				foreach (IPAddress LocIP in LocalIPs) if (RequestURL.Host == LocIP.ToString()) IsLocalhost = true;
				if (RequestURL.Host.ToLower() == "localhost" || RequestURL.Host.ToLower() == Environment.MachineName.ToLower() || RequestURL.Host == "127.0.0.1" || RequestURL.Host.ToLower() == "wpad" || RequestURL.Host.ToLower() == ConfigFile.DefaultHostName.ToLower() || RequestURL.Host == "")
					IsLocalhost = true;

				if (IsLocalhost)
				{
					bool PAC = false;
					string[] PacUrls = { "/auto/", "/auto", "/auto.pac", "/wpad.dat", "/wpad.da" }; //Netscape PAC/Microsoft WPAD
					foreach (string PacUrl in PacUrls) { if (RequestURL.LocalPath == PacUrl) { PAC = true; break; } }

					if (RequestURL.PathAndQuery.StartsWith("/!") || PAC)
					{
						try
						{ 
							//internal URLs
							Console.WriteLine("{0}\t Internal: {1} ", GetTime(BeginTime), RequestURL.PathAndQuery);
							switch (RequestURL.AbsolutePath.ToLower())
							{
								case "/!":
								case "/!/":
									string HelpString = "This is <b>" + Environment.MachineName + ":" + ConfigFile.Port + "</b>.<br>";
									HelpString +="Used memory: <b>" + (double)Environment.WorkingSet/1024/1024 + "</b> MB.<br>";
									HelpString += "Pending requests: <b>" + (Program.Load - 1) + "</b>.<br>";
									HelpString += "Available security: <b>" + ServicePointManager.SecurityProtocol + "</b> (" + (int)ServicePointManager.SecurityProtocol + ").<br>";

									HelpString += "<h2>Aliases:</h2><ul>";
									foreach (IPAddress LocIP in Dns.GetHostEntry(Environment.MachineName).AddressList)
									{ HelpString += "<li>" + (LocIP.ToString() == ConfigFile.DefaultHostName ? "<b>" + LocIP.ToString() + "</b>" : LocIP.ToString()) + ":" + ConfigFile.Port + "</li>"; }
									HelpString += "</ul>";
									HelpString += "</ul>";

									HelpString += "<p>Client IP: <b>" + ClientRequest.RemoteEndPoint + "</b>.</p>";

									HelpString += "<h2>Internal URLs:</h2><ul>" +
												  "<li><a href='/!codepages/'>/!codepages/</a> - list of available encodings for OutputEncoding setting</li>" +
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

									//prepare temporary file names
									int Rnd = new Random().Next();
									string DestName = "convert-" + Rnd + "." + Dest;
									string TmpFile = "orig-" + Rnd + ".tmp"; //for downloaded original if any
									if (FindSrcUrl.Success) Src = TmpFile;

									//prepare converter name
									string ConvCmdLine = string.Format("{0} {1} {2} {3}", Src, Args1, DestName, Args2);
									bool HasConverter = false, UseStdout = true, UseStdin = true, SelfDownload = false;
									foreach (string Cvt in ConfigFile.Converters)
									{
										//todo: make parsing paying attention to quotes, not simply by space character
										if (Cvt.IndexOf(" ") < 0) break;
										string CvtName = Cvt.Substring(0, Cvt.IndexOf(" "));
										if (CvtName == Converter)
										{
											HasConverter = true;
											if (Cvt.Contains("%DEST%")) UseStdout = false;
											if (Cvt.Contains("%SRC%")) UseStdin = false;
											SelfDownload = Cvt.Contains("%SRCURL%");

											Converter = CvtName;
											ConvCmdLine = ProcessUriMasks(Cvt, SrcUrl != "" ? SrcUrl : "http://webone.github.io/index.htm").Substring(Cvt.IndexOf(" ") + 1)
											.Replace("%SRC%", Src)
											.Replace("%ARG1%", Args1)
											.Replace("%DEST%", DestName)
											.Replace("%ARG2%", Args2)
											.Replace("%DESTEXT%", Dest)
											.Replace("%SRCURL%", SrcUrl);
										}
									}
									if (!HasConverter) throw new ArgumentException("Converter '" + Converter + "' is not allowed");

									Stream ConvStdin = new MemoryStream();
									MemoryStream ConvStdout = new MemoryStream();
									HTTPC SrcDloader = new HTTPC();

									//download source if need
									if (SrcUrl != "" && !SelfDownload)
									{
										if (!UseStdin)
										{
											//download source to tmp file
											try
											{
												Console.WriteLine("{0}\t>Downloading source...", GetTime(BeginTime));
												using(HTTPC TmpFileDloader = new HTTPC())
												{
													HttpClientResponse HCR = TmpFileDloader.GET(
														SrcUrl,
														new CookieContainer(),
														new WebHeaderCollection() { "User-agent: " + GetUserAgent(ClientRequest.Headers["User-agent"]) },
														"GET",
														true,
														BeginTime
													);
													FileStream TmpFileStream = File.OpenWrite(TmpFile);
													HCR.Stream.CopyTo(TmpFileStream);
													TmpFileStream.Close();
												}
												Src = TmpFile;
											}
											catch (Exception DownloadEx)
											{
												Console.WriteLine("{0}\t Can't download source: {1}.", GetTime(BeginTime), (DownloadEx.InnerException ?? DownloadEx).Message);
												SendError(500, "Cannot download:<br>" + (DownloadEx.InnerException ?? DownloadEx).ToString().Replace("\n", "<BR>"));
												return;
											}
										}
										else
										{
											//download source file to a Stream
											//test: http://localhost/!convert/?url=http%3A%2F%2Fcommondatastorage.googleapis.com%2Fgtv-videos-bucket%2Fsample%2FBigBuckBunny.mp4&util=../avconv&arg=-vcodec%20wmv1%20-acodec%20wmav1%20-f%20asf&type=video/x-ms-asf
											//or http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4 via proxy
											try
											{ 
												Console.WriteLine("{0}\t>Downloading source stream...", GetTime(BeginTime));

												HttpClientResponse HCR = SrcDloader.GET(
													SrcUrl,
													new CookieContainer(),
													new WebHeaderCollection() { "User-agent: " + GetUserAgent(ClientRequest.Headers["User-agent"]) },
													"GET",
													true,
													BeginTime
												);
												
												ConvStdin = HCR.Stream;

												Src = "CON:";
												#if DEBUG
												Console.WriteLine("{0}\t Stream begin: {1} ({2}).", GetTime(BeginTime), HCR.ContentType ?? "no content type", HCR.ContentLength);
												#endif

											}
											catch (Exception DlStreamEx)
											{
												Console.WriteLine("{0}\t Can't download source: {1}.", GetTime(BeginTime), (DlStreamEx.InnerException ?? DlStreamEx).Message);
												SendError(500, "Source stream error:<br>" + (DlStreamEx.InnerException ?? DlStreamEx).ToString().Replace("\n", "<BR>"));
												return;
											}
										}
									}
									else if(!SelfDownload)
									{
										//open local
										if(UseStdin)
										{
											//load source file to a FileStream
											try
											{
												ConvStdin = new FileStream(Src, FileMode.Open, FileAccess.Read);
												ConvStdin.Position = 0;
												Src = "CON:";
											}
											catch (Exception StreamEx)
											{
												Console.WriteLine("{0}\t Cannot open src file: {1}.", GetTime(BeginTime), (StreamEx.InnerException ?? StreamEx).Message);
												SendError(500, "Cannot open source file:<br>" + (StreamEx.InnerException ?? StreamEx).Message.ToString().Replace("\n", "<BR>"));
												return;
											}
										}
										//else it is already in ConvCmdLine
									}

									//run converter
									ProcessStartInfo ConvProcInfo = null;
									Process ConvProc = null;
									float ConvCpuLoad = 0;
									if (Src != "")
									try
									{
										if (!File.Exists(Src) && Src != "CON:" && !SelfDownload) throw new FileNotFoundException("Source file not found");

										Console.WriteLine("{0}\t Converting: {1} {2}...", GetTime(BeginTime), Converter, ConvCmdLine);

										ConvProcInfo = new ProcessStartInfo();
										ConvProcInfo.FileName = Converter;
										ConvProcInfo.Arguments = ConvCmdLine;
										ConvProcInfo.StandardOutputEncoding = Encoding.GetEncoding("latin1");//important part; thx to https://stackoverflow.com/a/5446177/7600726
										ConvProcInfo.RedirectStandardOutput = true;
										ConvProcInfo.RedirectStandardInput = true;
										ConvProcInfo.UseShellExecute = false;
										ConvProc = Process.Start(ConvProcInfo);

										if (UseStdout)
										{
											if(UseStdin)
											{ 
												#if DEBUG
												Console.WriteLine("{0}\t Writing stdin...", GetTime(BeginTime));
												#endif
												new Task(() => { try { ConvStdin.CopyTo(ConvProc.StandardInput.BaseStream); } catch { } }).Start();
											}

											#if DEBUG
											Console.WriteLine("{0}\t Reading stdout...", GetTime(BeginTime));
											#endif
											new Task(() => { while (ConvStdin.CanRead) { } if(!ConvProc.HasExited) ConvProc.Kill(); Console.WriteLine(); }).Start();
											new Task(() => { while (!ConvProc.HasExited) { if (ClientResponse.StatusCode == 500) { if (!ConvProc.HasExited) { ConvProc.Kill(); } } } }).Start();
											new Task(() => { while (!ConvProc.HasExited) { CheckIdle(ref ConvCpuLoad, ref ConvProc); }}).Start();
											SendStream(ConvProc.StandardOutput.BaseStream, DestMime, false);
											ConvProc.WaitForExit();
											ClientResponse.Close();

											if (SrcUrl != "") File.Delete(TmpFile);
											ConvStdin.Close();

											if (UseStdin) SrcDloader.Dispose();
											return;
										}
										else
										{
											if(UseStdin)
											{ 
												#if DEBUG
												Console.WriteLine("{0}\t Writing stdin...", GetTime(BeginTime));
												#endif
												new Task(() => { ConvStdin.CopyTo(ConvProc.StandardInput.BaseStream); }).Start();
												new Task(() => { while (ConvStdin.CanRead) { } if(!ConvProc.HasExited) ConvProc.Kill(); Console.WriteLine(); }).Start();
											}
												new Task(() => { while (!ConvProc.HasExited) { if (ClientResponse.StatusCode == 500) { if (!ConvProc.HasExited) { ConvProc.Kill(); } } } }).Start();
												new Task(() => { while (!ConvProc.HasExited) { CheckIdle(ref ConvCpuLoad, ref ConvProc); }}).Start();
											ConvProc.WaitForExit();

											if (!File.Exists(DestName)) throw new Exception("Convertion failed - no result found");
											SendFile(DestName, DestMime);
											File.Delete(DestName);

											if (SrcUrl != "") File.Delete(TmpFile);
											ConvStdin.Close();

											if (UseStdin) SrcDloader.Dispose();
											return;
										}
									}
									catch(Exception ConvEx) {
										if (ConvProc != null && !ConvProc.HasExited) ConvProc.Kill();
										Console.WriteLine("{0}\t Can't convert: {1}.", GetTime(BeginTime), ConvEx.Message);
										SendError(500, "Cannot convert:<br>" + ConvEx.ToString().Replace("\n","<BR>"));
										ConvStdin.Close();
										return;
									}

									SendError(200, "Summon ImageMagick to convert a picture file.<br>"+
									"Usage: /!convert/?src=filename.ext&dest=gif&type=image/gif<br>"+
									"or: /!convert/?url=https://example.com/filename.ext&dest=gif&type=image/gif");
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

										if (new FileInfo(FileName).DirectoryName != Directory.GetCurrentDirectory())
										{
											SendError(200, "Cannot access to files outside Proxy's directory");
											return;
										}
									
										SendFile(FileName, MimeType);
										return;
									}
									SendError(200, "Get a local file.<br>Usage: /!file/?name=filename.ext&type=text/plain");
									return;
								case "/!clear/":
									int FilesDeleted = 0;
									foreach (FileInfo file in (new DirectoryInfo(Directory.GetCurrentDirectory())).EnumerateFiles("*.tmp"))
									{
										try { file.Delete(); FilesDeleted++; }
										catch { }
									}
									SendError(200, FilesDeleted + " temporary files have been deleted.");
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
						RequestURL = new Uri(RequestURL.LocalPath.Replace("/http:/", "http://"));
						Console.WriteLine("{0}\t Local: {1}", GetTime(BeginTime), RequestURL);
					}
					else
					{
						//dirty local mode, try to use last used host: http://localhost/favicon.ico
						RequestURL = new Uri("http://" + new Uri(LastURL).Host + RequestURL.LocalPath);
						Console.WriteLine("{0}\t Dirty local: {1}", GetTime(BeginTime), RequestURL);
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

				//check for too new frameworks & replace with older versions
				foreach (string str in ConfigFile.FixableURLs)
				{
					if (Regex.Match(RequestURL.AbsoluteUri, str).Success)
					{
						try
						{
							string ValidMask = "";
							if (ConfigFile.FixableUrlActions[str].ContainsKey("ValidMask")) ValidMask = ConfigFile.FixableUrlActions[str]["ValidMask"];

							string Redirect = "about:mozilla";
							if (ConfigFile.FixableUrlActions[str].ContainsKey("Redirect")) Redirect = ConfigFile.FixableUrlActions[str]["Redirect"];

							string InternalRedirect = "no";
							if (ConfigFile.FixableUrlActions[str].ContainsKey("Internal")) InternalRedirect = ConfigFile.FixableUrlActions[str]["Internal"];

							if (ValidMask == "" || !Regex.Match(RequestURL.AbsoluteUri, ValidMask).Success)
							{
								string NewURL = ProcessUriMasks(Redirect, RequestURL.AbsoluteUri);
								
								ShouldRedirectInNETFW = ConfigFile.ToBoolean(InternalRedirect);

								if (!ShouldRedirectInNETFW)
								{
									Console.WriteLine("{0}\t Fix to {1}", GetTime(BeginTime), NewURL);
									ClientResponse.AddHeader("Location", NewURL);
									SendError(302, "Брось каку!");
									return;
								}
								else
								{
									Console.WriteLine("{0}\t Fix to {1} internally", GetTime(BeginTime), NewURL);
									RequestURL = new Uri(NewURL);
								}
							}
						}
						catch (Exception rex)
						{
							Console.WriteLine("{0}\t Cannot redirect! {1}", GetTime(BeginTime), rex.Message);
							SendError(200, rex.ToString().Replace("\n", "\n<br>"));
							return;
						}
					}
				}


				LastURL = RequestURL.AbsoluteUri;
				LastContentType = "unknown/unknown"; //will be populated in MakeOutput
				LastCode = HttpStatusCode.OK; //same

				//make reply
				//SendError(200, "Okay, bro! Open " + RequestURL);
				using (HTTPC https = new HTTPC()) { 
					bool StWrong = false; //break operation if something is wrong.

					if (RequestURL.AbsoluteUri.Contains("??")) { StWrong = true; SendError(400, "Too many questions."); }
					if (RequestURL.AbsoluteUri.Length == 0) { StWrong = true; SendError(400, "Empty URL."); }
					if (RequestURL.AbsoluteUri == "") return;

					try
					{
						int CL = 0;
						if (ClientRequest.Headers["Content-Length"] != null) CL = Int32.Parse(ClientRequest.Headers["Content-Length"]);

						//send HTTPS request to destination server
						WebHeaderCollection whc = new WebHeaderCollection();
						foreach(string h in ClientRequest.Headers.Keys) {
							whc.Add(h, ClientRequest.Headers[h]);
						}
						SendRequest(https, ClientRequest.HttpMethod, whc, CL);
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
						ResponseBody = "<html><body>Cannot load this page" + err + "<br><i>" + wex.ToString().Replace("\n", "<br>") + "</i><br>URL: " + RequestURL.AbsoluteUri + Program.GetInfoString() + "</body></html>";
#else
						string NiceErrMsg = "<p><big>" + wex.Message + ".</big></p>Status: " + wex.Status;
						if (wex.InnerException != null) NiceErrMsg = "<p><big>" + wex.Message + "<br>" + wex.InnerException.Message + ".</big></p>Status: " + wex.Status + " + " + wex.InnerException.GetType().ToString();
						ResponseBody = "<html><title>WebOne: " + wex.Status + "</title><body><h1>Cannot load this page</h1>" + NiceErrMsg + "<br>URL: " + RequestURL.AbsoluteUri + GetInfoString() + "</body></html>";
	#endif

						//check if archived copy can be retreived instead
						bool Archived = false;
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
										Archived = true;
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
											Archived = false;
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
											Archived = true;
										}
									}
								}
								catch (Exception ArchiveException)
								{
									ResponseBody = String.Format("<html><body><b>Server not found and a Web Archive error occured.</b><br>{0}</body></html>", ArchiveException.Message.Replace("\n", "<br>"));
								}
							}

						//check if there are any response and the error isn't fatal
						if (wex.Response != null)
						{
							for (int i = 0; i < wex.Response.Headers.Count; ++i)
							{
								string header = wex.Response.Headers.GetKey(i);
								foreach (string value in wex.Response.Headers.GetValues(i))
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
							ContentType = wex.Response.ContentType;
							MakeOutput((HttpStatusCode)ResponseCode, wex.Response.GetResponseStream(),wex.Response.ContentType, wex.Response.ContentLength);
						}
						#if DEBUG
						else if (!Archived)
							Console.WriteLine("{0}\t Failed: {1}.", GetTime(BeginTime), ResponseCode);
						#endif
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
								RespBuffer = (ConfigFile.OutputEncoding ?? Encoding.Default).GetBytes(ResponseBody).ToArray();

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
			}
			catch(Exception E)
			{
				string time = GetTime(BeginTime);
				Console.WriteLine("{0}\t A error has been catched: {1}\n{0}\t Please report to author.", time, E.ToString().Replace("\n", "\n{0}\t "));
				SendError(500, "An error occured: " + E.ToString().Replace("\n", "\n<BR>"));
			}
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
		private void SendRequest(HTTPC https, string RequestMethod, WebHeaderCollection RequestHeaderCollection, int Content_Length)
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
					if (Content_Length == 0)
					{
						//try to download (GET, HEAD, WebDAV download, etc)
						Console.WriteLine("{0}\t>Downloading content...", GetTime(BeginTime));
						response = https.GET(RequestURL.AbsoluteUri, new CookieContainer(), RequestHeaderCollection, RequestMethod, AllowAutoRedirect, BeginTime);
						MakeOutput(response.StatusCode, response.Stream, response.ContentType, response.ContentLength);
						break;
					}
					else
					{
						//try to upload (POST, PUT, WebDAV, etc)
						Console.WriteLine("{0}\t>Uploading {1}K of {2}...", GetTime(BeginTime), Convert.ToInt32((RequestHeaderCollection["Content-Length"])) / 1024, RequestHeaderCollection["Content-Type"]);
						response = https.POST(RequestURL.AbsoluteUri, new CookieContainer(), ClientRequest.InputStream, RequestHeaderCollection, RequestMethod, AllowAutoRedirect, BeginTime);
						MakeOutput(response.StatusCode, response.Stream, response.ContentType, response.ContentLength);
						break;
					}
			}

			ResponseCode = (int)response.StatusCode;

			//check for security upgrade
			if (ResponseCode == 301 || ResponseCode == 302 || ResponseCode == 308)
			{
				if (RequestURL.AbsoluteUri == (response.Headers["Location"] ?? "nowhere").Replace("https://", "http://")
					&& !CheckString(RequestURL.AbsoluteUri, ConfigFile.InternalRedirectOn))
				{
					Console.WriteLine("{0}\t>Reload secure...", GetTime(BeginTime));

					RequestURL = new Uri(RequestURL.AbsoluteUri.Replace("http://", "https://"));
					TransitStream = null;
					SendRequest(https, RequestMethod, RequestHeaderCollection, Content_Length);

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
			for (int i = 0; i < response.Headers.Count; ++i)
			{
				string header = response.Headers.GetKey(i);
				foreach (string value in response.Headers.GetValues(i))
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
			foreach (string mask in ConfigFile.ContentPatches)
			{
				string IfURL = ".*";
				if (ConfigFile.ContentPatchActions[mask].ContainsKey("IfURL"))
					IfURL = ConfigFile.ContentPatchActions[mask]["IfURL"];

				string IfType = ".*";
				if (ConfigFile.ContentPatchActions[mask].ContainsKey("IfType"))
					IfType = ConfigFile.ContentPatchActions[mask]["IfType"];

				string Replace = "<!--Removed by WebOne:$&End-->";
				if (ConfigFile.ContentPatchActions[mask].ContainsKey("Replace"))
					Replace = ConfigFile.ContentPatchActions[mask]["Replace"];

				if (Regex.IsMatch(RequestURL.AbsoluteUri, IfURL) && Regex.IsMatch(ContentType, IfType))
				{
					try
					{
						Body = Regex.Replace(Body, mask, Replace, RegexOptions.Singleline);
						patched++;
					}
					catch (Exception rex)
					{
						Console.WriteLine("{0}\t Cannot make edit: {1}!", GetTime(BeginTime), rex.Message);
					}
				}
			}

			if (patched > 0) Console.WriteLine("{0}\t {1} patch(-es) applied...", GetTime(BeginTime), patched);
			return Body;
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

			//check for need of converting
			foreach (string str in ConfigFile.FixableTypes)
			{
				if (Regex.Match(ContentType, str).Success)
				{
					bool Need = true;

					string Redirect = "http://" + ConfigFile.DefaultHostName + "/!convert/";
					if (ConfigFile.FixableTypesActions[str].ContainsKey("Redirect")) Redirect = ConfigFile.FixableTypesActions[str]["Redirect"];
					Redirect = ProcessUriMasks(Redirect, RequestURL.AbsoluteUri);
					
					string IfUrl = ".*";
					if (ConfigFile.FixableTypesActions[str].ContainsKey("IfUrl")) IfUrl = ConfigFile.FixableTypesActions[str]["IfUrl"];

					string NotUrl = "";
					if (ConfigFile.FixableTypesActions[str].ContainsKey("NotUrl")) NotUrl = ConfigFile.FixableTypesActions[str]["NotUrl"];

					Need = Regex.IsMatch(RequestURL.AbsoluteUri, IfUrl);
					Need = !Regex.IsMatch(RequestURL.AbsoluteUri, NotUrl);

					if (Need)
					{
						Console.WriteLine("{0}\t {1} {2}. Body {3}K of {4} [Need to convert].", GetTime(BeginTime), (int)StatusCode, StatusCode, ContentLength / 1024, ContentType);
						ClientResponse.AddHeader("Location", Redirect);
						SendError(302, "Need to convert this.");
						return;
					}

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

				if (ConfigFile.OutputEncoding == null)
				{
					//if don't touch codepage (OutputEncoding=AsIs)
					ResponseBody = Encoding.Default.GetString(RawContent);
					ResponseBody = ProcessBody(ResponseBody);
					#if DEBUG
					Console.WriteLine("{0}\t Body maked.", GetTime(BeginTime));
					#endif
					return;
				}

				bool ForceUTF8 = ContentType.ToLower().Contains("utf-8");
				if (!ForceUTF8 && RawContent.Length > 0) ForceUTF8 = RawContent[0] == Encoding.UTF8.GetPreamble()[0];
				foreach (string utf8url in ConfigFile.ForceUtf8) { if (Regex.IsMatch(RequestURL.AbsoluteUri, utf8url)) ForceUTF8 = true; }
				//todo: add fix for "invalid range in character class" at www.yandex.ru with Firefox 3.6 if OutputEncoding!=AsIs

				if (ForceUTF8) ResponseBody = Encoding.UTF8.GetString(RawContent);
				else ResponseBody = Encoding.Default.GetString(RawContent);

				if (Regex.IsMatch(ResponseBody, @"<meta.*UTF-8.*>", RegexOptions.IgnoreCase)) { ResponseBody = Encoding.UTF8.GetString(RawContent); }

				if (ContentType.ToLower().Contains("utf-8")) ContentType = ContentType.Substring(0, ContentType.IndexOf(';'));
				ResponseBody = ProcessBody(ResponseBody);
				this.ContentType = ContentType;
			}
			else
			{
				if(response != null)
					Console.WriteLine("{0}\t {1} {2}. Body {3}K of {4} [Binary].", GetTime(BeginTime), (int)StatusCode, StatusCode, response.ContentLength / 1024, ContentType);
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
			if (ClientResponse.Headers["Refresh"] != null) Refresh = "<META HTTP-EQUIV=REFRESH CONTENT="+ ClientResponse.Headers["Refresh"] +">";
			string Html = "<html>" + Refresh + "<body><h1>" + CodeStr + "</h1>" + Text + "</body></html>";
			byte[] Buffer = Encoding.Default.GetBytes(Html);
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


		/// <summary>
		/// Fill %masks% on an URI template
		/// </summary>
		/// <param name="MaskedURL">URI template</param>
		/// <param name="PossibleURL">Previous URI (for "%URL%" mask and similar)</param>
		/// <returns>Ready URL</returns>
		private string ProcessUriMasks(string MaskedURL, string PossibleURL = "http://webone.github.io:80/index.htm")
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
				builder.Host = "butaforia-" +  new Random().Next().ToString();
				str = str.Replace("%UrlNoDomain%", builder.Uri.ToString().Replace(builder.Host + ":" + builder.Port,"").Replace(builder.Scheme + "://",""));
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
		/// Get CPU load for process
		/// </summary>
		/// <param name="process">The process object</param>
		/// <returns>CPU usage in percents</returns>
		// UNDONE: UNTESTED - Transition from .NET FW to .NET Core!
		private double GetUsage(Process process)
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
		/// Check process for idle mode and kill it if yes
		/// </summary>
		/// <param name="AverageLoad">Average process load</param>
		/// <param name="Proc">Which process</param>
		private void CheckIdle(ref float AverageLoad, ref Process Proc)
		{
			Thread.Sleep(1000);
			AverageLoad = (float)(AverageLoad + GetUsage(Proc)) / 2;

			if (Math.Round(AverageLoad, 2) <= 0 && !Proc.HasExited)
			{
				//the process is counting crows. Fire!
				Proc.Kill();
				Console.WriteLine("\n{0}\t Idle process killed.", GetTime(BeginTime));
			}
		}

	}
}
