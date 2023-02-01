using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using static WebOne.Program;

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

		Dictionary<string, string> Variables = new Dictionary<string, string>();

		Uri RequestURL = new Uri("about:blank");
		static string LastURL = "http://999.999.999.999/CON";
		static HttpStatusCode LastCode = HttpStatusCode.OK;
		static string LastContentType = "not-a-carousel";
		List<EditSet> EditSets = new List<EditSet>();
		bool Stop = false;
		bool LocalMode = false;
		string LocalIP = "127.0.0.1";

		HttpOperation operation;
		int ResponseCode = 502;
		string ResponseBody = ":(";
		Stream TransitStream = null;
		string ContentType = "text/plain";
		Encoding SourceContentEncoding = Encoding.Default;
		Encoding OutputContentEncoding = ConfigFile.OutputEncoding;
		bool EnableTransliteration = false;

		string DumpFile = null;

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
				//check IP black list
				if (CheckString(ClientRequest.RemoteEndPoint.ToString(), ConfigFile.IpBanList))
				{
					Log.WriteLine(" Banned client.");
					ClientResponse.Close();
					return;
				}

				//check IP white list
				if (ConfigFile.IpWhiteList.Count > 0)
					if (!CheckString(ClientRequest.RemoteEndPoint.ToString(), ConfigFile.IpWhiteList))
					{
						SendError(403, "You are not in the list of allowed clients. Contact proxy server's administrator to add your IP address in it.");
						Log.WriteLine(" Non-whitelisted client.");
						return;
					}

				//check for login to proxy if need
				if (ConfigFile.Authenticate.Count > 0)
				{
					switch (ClientRequest.Url.PathAndQuery)
					{
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
								if (!ConfigFile.Authenticate.Contains(auth))
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

				//check for blacklisted URL
				if (CheckString(RequestURL.ToString(), ConfigFile.UrlBlackList))
				{
					Log.WriteLine(" Blacklisted URL.");
					SendError(403, "Access to this web page is disallowed by proxy settings.");
					return;
				}

				//get proxy's IP address
				if(ClientRequest.LocalEndPoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
					LocalIP = ClientRequest.LocalEndPoint.Address.ToString(); // IPv4
				else
					LocalIP = "[" + ClientRequest.LocalEndPoint.Address.ToString() + "]"; //IPv6

				//fill variables
				UriBuilder builder = new UriBuilder(RequestURL);
				var DefaultVars = new Dictionary<string, string>
				{
					{ "URL", RequestURL.ToString() },
					{ "Url", Uri.EscapeDataString(RequestURL.ToString()) },
					{ "UrlDomain", builder.Host },
					{ "UrlNoDomain", (builder.Query == "" ? builder.Path : builder.Path + "?" + builder.Query) },
					{ "UrlNoQuery", builder.Scheme + "://" + builder.Host + "/" +  builder.Path },
					{ "UrlNoPort", builder.Scheme + "://" + builder.Host + "/" + (builder.Query == "" ? builder.Path : builder.Path + "?" + builder.Query) },
					{ "UrlHttps", "https://" + builder.Host + "/" + (builder.Query == "" ? builder.Path : builder.Path + "?" + builder.Query) },
					{ "UrlHttp", "http://" + builder.Host + "/" + (builder.Query == "" ? builder.Path : builder.Path + "?" + builder.Query) },
					{ "Proxy", GetServerName() },
					{ "ProxyHost", ConfigFile.DefaultHostName == Environment.MachineName ? LocalIP : ConfigFile.DefaultHostName },
					{ "Method", ClientRequest.HttpMethod },
					{ "HttpVersion", ClientRequest.ProtocolVersion.ToString() }
				};
				foreach (var entry in Program.Variables) { DefaultVars.TryAdd(entry.Key, entry.Value); }

				//check for local or internal URL
				bool IsLocalhost = false;

				foreach (IPAddress address in GetLocalIPAddresses())
					if (RequestURL.Host.ToLower() == address.ToString().ToLower() || RequestURL.Host.ToLower() == "[" + address.ToString().ToLower() + "]")
						if (RequestURL.Port == ConfigFile.Port)
							IsLocalhost = true;

				if (
				RequestURL.Host.ToLower() == "localhost" ||
				RequestURL.Host.ToLower() == Environment.MachineName.ToLower() ||
				RequestURL.Host == "127.0.0.1" ||
				RequestURL.Host == LocalIP ||
				RequestURL.Host.ToLower() == "wpad" ||
				RequestURL.Host.ToLower() == ConfigFile.DefaultHostName.ToLower() ||
				RequestURL.Host == "" ||
				CheckString(RequestURL.Host.ToLower(), ConfigFile.HostNames)
				)
					if (RequestURL.Port == ConfigFile.Port)
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
									string codepages = "<p>The following code pages are available: <br>\n" +
													   "<table><tr><td><b>Name</b></td><td><b>#</b></td><td><b>Description</b></td></tr>\n";
									codepages += "<tr><td><b>AsIs</b></td><td>0</td><td>Keep original encoding (code page)</td></tr>\n";
									Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
									bool IsOutputEncodingListed = false;
									foreach (EncodingInfo cp in Encoding.GetEncodings())
									{
										codepages += "<tr><td>";
										codepages += "<b>" + cp.Name + "</b></td><td>" + cp.CodePage + "</td><td>" + cp.DisplayName;

										if (ConfigFile.OutputEncoding != null && cp.CodePage == ConfigFile.OutputEncoding.CodePage)
										{
											codepages += " <b>(Current)</b>";
											IsOutputEncodingListed = true;
										}
										/*codepages += "<td>";
										if (GetCodePage("Win").CodePage == cp.CodePage) codepages += "Windows &quot;ANSI&quot;";
										if (GetCodePage("DOS").CodePage == cp.CodePage) codepages += "DOS &quot;OEM&quot;";
										if (GetCodePage("Mac").CodePage == cp.CodePage) codepages += "MacOS classic";
										if (GetCodePage("EBCDIC").CodePage == cp.CodePage) codepages += "IBM EBCDIC";
										codepages += "</td>";*/
										codepages += "</td></tr>\n";
									}
									codepages += "</table><br>Use any of these or from <a href=http://docs.microsoft.com/en-us/dotnet/api/system.text.encoding.getencodings?view=net-6.0>.NET documentation</a>.</p>\n";

									codepages += "<p>Code pages for current server's locale:\n" +
									"<table>" +
									"<tr><td>Windows &quot;ANSI&quot;</td><td>" + GetCodePage("Win").WebName + "</td></tr>\n" +
									"<tr><td>DOS &quot;OEM&quot;</td><td>" + GetCodePage("DOS").WebName + "</td></tr>\n" +
									"<tr><td>MacOS classic</td><td>" + GetCodePage("Mac").WebName + "</td></tr>\n" +
									"<tr><td>IBM EBCDIC</td><td>" + GetCodePage("EBCDIC").WebName + "</td></tr>\n" +
									"</table>This list is correct only for language, set on proxy server machine.</p>\n";

									if (!IsOutputEncodingListed && ConfigFile.OutputEncoding != null)
										codepages += "<br>Current output encoding: <b>" + ConfigFile.OutputEncoding.WebName + "</b> &quot;" + ConfigFile.OutputEncoding.EncodingName + "&quot; (# " + ConfigFile.OutputEncoding.CodePage + ").\n";
									if (ConfigFile.OutputEncoding == null)
										codepages += "<br>Current output encoding: <b>same as source</b>.\n";

									SendInfoPage("WebOne: List of supported code pages", "Content encodings", codepages);
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
									foreach (Converter Cvt in ConfigFile.Converters)
									{
										if (Cvt.Executable == Converter)
										{
											HttpOperation HOper = new HttpOperation(Log);
											Stream SrcStream = null;

											//find source file placement
											if (FindSrcUrl.Success)
											{
												//download source file
												if (!Cvt.SelfDownload) try
													{
														HOper.URL = new Uri(SrcUrl);
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
											catch (Exception CvtEx)
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
								case "/!webvideo/":
									Dictionary<string, string> VidArgs = new();

									foreach(string UrlArg in System.Web.HttpUtility.ParseQueryString(ClientRequest.Url.Query).AllKeys)
									{
										VidArgs[UrlArg] = System.Web.HttpUtility.ParseQueryString(ClientRequest.Url.Query)[UrlArg];
									}

									if(!VidArgs.ContainsKey("url"))
									{
										string HelpMsg =
										"<p>This page allows download videos from popular sites in preferred format.</p>" +
										"<center>" +
										"<form action='/!webvideo/' method='GET'>" +
										"<table border='0'>" +
										"<tr>" +
										"    <td align='right'>Video URL</td>" +
										"    <td align='center'><input type='text' size='65'" +
										"    name='url'" +
										"    value='https://www.youtube.com/watch?v=XXXXXXX'></td>" +
										"</tr>" +
										"<tr>" +
										"    <td align='right'>Format</td>" +
										"    <td><select name='f' size='1'>" +
										"        <option selected value='avi'>AVI</option>" +
										"        <option value='mpeg1video'>MPEG 1</option>" +
										"        <option value='mpeg2video'>MPEG 2</option>" +
										"        <option value='mp4'>MPEG 4</option>" +
										"        <option value='mpegts'>MPEG TS</option>" +
										"        <option value='asf'>Microsoft ASF</option>" +
										"        <option value='asf_stream'>Microsoft ASF (stream)</option>" +
										"        <option value='mov'>QuickTime</option>" +
										"        <option value='ogg'>Ogg</option>" +
										"        <option value='webm'>WebM</option>" +
										"        <option value='swf'>Macromedia Flash</option>" +
										"        <option value='rm'>RealMedia</option>" +
										"        <option value='3gp'>3GPP</option>" +
										"    </select></td>" +
										"</tr>" +
										"<tr>" +
										"    <td align='right'>Video</td>" +
										"    <td><select name='vcodec' size='1'>" +
										"        <option value='mpeg1video'>MPEG 1</option>" +
										"        <option selected value='mpeg2video'>MPEG 2</option>" +
										"        <option value='mpeg4'>MPEG 4</option>" +
										"        <option value='wmv1'>WMV 7</option>" +
										"        <option value='wmv2'>WMV 8</option>" +
										"        <option value='h263'>H.263</option>" +
										"        <option value='h264'>H.264 AVC</option>" +
										"        <option value='hevc'>H.265 HEVC</option>" +
										"        <option value='theora'>Ogg Theora</option>" +
										"        <option value='mjpeg'>MJPEG</option>" +
										"        <option value='msvideo1'>MS Video 1</option>" +
										"        <option value='copy'>(original)</option>" +
										"    </select></td>" +
										"</tr>" +
										"<tr>" +
										"    <td align='right'>Audio</td>" +
										"    <td><select name='acodec' size='1'>" +
										"        <option value='mp2'>MPEG 2</option>" +
										"        <option selected value='mp3'>MPEG 3</option>" +
										"        <option value='wmav1'>WMA 1</option>" +
										"        <option value='wmav2'>WMA 2</option>" +
										"        <option value='pcm_dvd'>PCM</option>" +
										"        <option value='libvorbis'>Ogg Vorbis</option>" +
										"        <option value='ra_144'>RealAudio 1</option>" +
										"        <option value='copy'>(original)</option>" +
										"    </select> <select name='ac' size='1'>" +
										"        <option selected value='1'>Mono</option>" +
										"        <option value='2'>Stereo</option>" +
										"    </select></td>" +
										"</tr>" +
										"<tr>" +
										"    <td align='center' colspan='2'><input type='submit'" +
										"    value='Download'></td>" +
										"</tr>" +
										"</table>" +
										"</form>" +
										"</center>" +
										"<p>Manual use parameters:</p>" +
										"<ul>" +
										"<li><b>url</b> - Address of the video (e.g. https://www.youtube.com/watch?v=fPnO26CwqYU or similar)</li>" +
										"<li><b>f</b> - Target format of the file (e.g. avi)</li>" +
										"<li><b>vcodec</b> - Codec for video (e.g. mpeg4)</li>" +
										"<li><b>acodec</b> - Codec for audio (e.g. mp3)</li>" +
										"<li><b>content-type</b> - override MIME content type for the file (optional).</li>" +
										"<li>Also you can use many <i>youtube-dl</i> and <i>ffmpeg</i> options like <b>aspect</b>, <b>b</b>, <b>no-mark-watched</b> and other.</li>" +
										"</ul>";
										SendInfoPage("Online video converter", "Web video converting", HelpMsg);
										return;
									}

									WebVideo vid = new WebVideoConverter().ConvertVideo(VidArgs, Log);
									if(vid.Available)
									{
										ClientResponse.AddHeader("Content-Disposition", "attachment; filename=\"" + vid.FileName + "\"");
										SendStream(vid.VideoStream, vid.ContentType);
										return;
									}
									else
									{
										string ErrMsg = 
										"<p>" + vid.ErrorMessage + "</p>" + 
										"<p>Make sure that parameters are correct, and both <i>youtube-dl</i> and <i>ffmpeg</i> are properly installed on the server.</p>"+
										"<a href='/!webvideo/'>Go back.</a>";
										SendInfoPage("Online video converter", "Web video converting", ErrMsg);
										return;
									}
								case "/!clear/":
									int FilesDeleted = 0;
									foreach (FileInfo file in (new DirectoryInfo(ConfigFile.TemporaryDirectory)).EnumerateFiles("convert-*.*"))
									{
										try { file.Delete(); FilesDeleted++; }
										catch { }
									}
									SendError(200, "<b>" + FilesDeleted + "</b> temporary files have been deleted in <i>" + ConfigFile.TemporaryDirectory + "</i>.");
									return;
								case "/!ftp/":
								case "/!ftp":
									//FTP client
									SendInfoPage(new FtpClientGUI(ClientRequest).GetPage());
									return;
								case "/!pac/":
								case "/auto/":
								case "/auto":
								case "/auto.pac":
								case "/wpad.dat":
								case "/wpad.da":
									//Proxy Auto-Config
									Log.WriteLine("<Return PAC/WPAD script.");
									string LocalHostAdress = GetServerName();
									if (LocalHostAdress.StartsWith("[")) LocalHostAdress = ConfigFile.DefaultHostName + ":" + ConfigFile.Port; //on IPv6, fallback to DefaultHostName:Port

									string PacString = Program.ProcessUriMasks(ConfigFile.PAC, LocalHostAdress, false, new Dictionary<string, string>() { { "PACProxy", LocalHostAdress } });

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
							SendError(500, "Internal server error: <b>" + ex.Message + "</b><br>" + ex.GetType().ToString() + " " + ex.StackTrace.Replace("\n", "<br>"));
#else
							SendError(500, "WebOne cannot process the request because <b>" + ex.Message + "</b>.");
#endif
						}
					}
					//local proxy mode: http://localhost/http://example.com/indexr.shtml, http://localhost/http:/example.com/indexr.shtml
					if (RequestURL.LocalPath.StartsWith("/http:/") || RequestURL.LocalPath.StartsWith("/https:/"))
					{
						if (!(RequestURL.LocalPath.StartsWith("/http://") || RequestURL.LocalPath.StartsWith("/https://")))
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

				//check for HTTP-to-FTP (and similar) requests
				//https://support.microsoft.com/en-us/help/166961/how-to-ftp-with-cern-based-proxy-using-wininet-api
				if (ClientRequest.RawUrl.ToLower().StartsWith("ftp://"))
				{
					//FTP mode (CERN)
					//does not works as HttpListener (used since WebOne 0.8.5) ignores non-HTTP addresses
					InfoPage WebFtpRedirect = new();
					WebFtpRedirect.Title = "CERN Proxy Emulation Redirect";
					WebFtpRedirect.HttpStatusCode = 302;
					WebFtpRedirect.HttpHeaders.Add("Location", "http://" + GetServerName() + "/!ftp/?client=-1&uri=" + Uri.EscapeDataString(ClientRequest.RawUrl));
					SendInfoPage(WebFtpRedirect);
					return;
				}
				if (ClientRequest.RawUrl.ToLower().StartsWith("http://ftp:"))
				{
					//HTTP->FTP mode (Netscape)
					InfoPage WebFtpRedirect = new();
					WebFtpRedirect.Title = "CERN Proxy Emulation Redirect";
					WebFtpRedirect.HttpStatusCode = 302;
					WebFtpRedirect.HttpHeaders.Add("Location", "http://" + GetServerName() + "/!ftp/?client=-1&uri=" + Uri.EscapeDataString(ClientRequest.RawUrl.Substring(7)));
					SendInfoPage(WebFtpRedirect);
					return;
				}
				if (ClientRequest.RawUrl.ToLower().StartsWith("http://ftp//"))
				{
					//HTTP->FTP mode (MS IE)
					InfoPage WebFtpRedirect = new();
					WebFtpRedirect.Title = "CERN Proxy Emulation Redirect";
					WebFtpRedirect.HttpStatusCode = 302;
					WebFtpRedirect.HttpHeaders.Add("Location", "http://" + GetServerName() + "/!ftp/?client=-1&uri=" + Uri.EscapeDataString("ftp://" + ClientRequest.RawUrl.Substring(12)));
					SendInfoPage(WebFtpRedirect);
					return;
				}
				string[] BadProtocols = { "gopher", "wais" };
				if (CheckString(RequestURL.Scheme, BadProtocols))
				{
					//does not works as HttpListener (used since WebOne 0.8.5) ignores non-HTTP addresses
					//before v0.14 there was FTP ban too, now it's unbanned
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
				RequestURL.Host.ToLower() != LocalIP &&
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

				if (RequestURL.AbsoluteUri.Contains(" ")) RequestURL = new Uri(RequestURL.AbsoluteUri.Replace(" ", "%20")); //fix spaces in wrong-formed URLs

				//check for available edit sets
				foreach (EditSet set in ConfigFile.EditRules)
				{
					if (!set.CorrectHostOS) continue;
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

				//check for URL white list (if any)
				if (ConfigFile.UrlWhiteList.Count > 0)
					if (!CheckString(RequestURL.ToString(), ConfigFile.UrlWhiteList))
					{
						SendError(403, "Proxy server administrator has been limited this proxy server to work with several web sites only.");
						Log.WriteLine(" URL out of white list.");
						return;
					}

				bool BreakTransit = false; //use instead of return

				try
				{
					int CL = 0;
					if (ClientRequest.Headers["Content-Length"] != null) CL = Int32.Parse(ClientRequest.Headers["Content-Length"]);

					//make and send HTTPS request to destination server
					WebHeaderCollection whc = new WebHeaderCollection();

					//prepare headers
					if (RequestURL.Scheme.ToLower() == "https" || CheckString(RequestURL.Host, ConfigFile.ForceHttps))
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
					if (whc["User-Agent"] != null) whc["User-Agent"] = GetUserAgent(whc["User-Agent"]);

					//perform edits on the request
					foreach (EditSet Set in EditSets)
					{
						if (Set.IsForRequest)
						{
							//if URL mask is single, allow use RegEx groups (if any) for replace
							bool UseRegEx = (Set.UrlMasks.Count == 1 && new Regex(Set.UrlMasks[0]).GetGroupNames().Count() > 1);
#if DEBUG
							if (UseRegEx) Log.WriteLine(" RegExp groups are available on {0}.", Set.UrlMasks[0]);
#endif

							foreach (EditSetRule Edit in Set.Edits)
							{
								switch (Edit.Action)
								{
									case "AddHeaderDumping":
									case "AddRequestDumping":
									case "AddDumping":
										//dump initializing must be first
										DumpFile = ProcessUriMasks(Edit.Value)
										.Replace(":","-")
										.Replace("<","(")
										.Replace(">", ")")
										.Replace("?", "-")
										.Replace("|","!");
										if (DumpFile.Length > 128) { DumpFile = DumpFile.Substring(0, 128) + "-CUT.log"; } //about half of Windows path limitation
										Dump(ClientRequest.HttpMethod + " " + ClientRequest.RawUrl + " HTTP/" + ClientRequest.ProtocolVersion.ToString());
										break;
									case "AddInternalRedirect":
										string NewUrlInternal = UseRegEx ? ProcessUriMasks(new Regex(Set.UrlMasks[0]).Replace(RequestURL.AbsoluteUri, Edit.Value))
																		 : ProcessUriMasks(Edit.Value);
										Log.WriteLine(" Fix to {0} internally", NewUrlInternal);
										Dump("~Internal redirect to: " + NewUrlInternal);
										RequestURL = new Uri(NewUrlInternal);
										break;
									case "AddRedirect":
										string NewUrl302 = UseRegEx ? ProcessUriMasks(new Regex(Set.UrlMasks[0]).Replace(RequestURL.AbsoluteUri, Edit.Value))
																	: ProcessUriMasks(Edit.Value);
										Log.WriteLine(" Fix to {0}", NewUrl302);
										Dump("~Redirect using 302 to: " + NewUrl302);
										ClientResponse.AddHeader("Location", NewUrl302);
										SendError(302, "Брось каку!");
										return;
									case "AddRequestHeader":
									case "AddHeader":
										string Header = ProcessUriMasks(Edit.Value);
										Dump("~Add request header: " + Header);
										if (whc[Edit.Value.Substring(0, Edit.Value.IndexOf(": "))] == null) whc.Add(Header);
										break;
									case "AddRequestHeaderFindReplace":
										FindReplaceEditSetRule hdr_rule = (FindReplaceEditSetRule)Edit;
										foreach (var hdr in whc.AllKeys)
										{
											whc[hdr] = whc[hdr].Replace(hdr_rule.Find, hdr_rule.Replace);
											Dump("~Request header find&replace: '" + hdr_rule.Find + "' / '" + hdr_rule.Replace + "'");
										}
										break;
									case "AddOutputEncoding":
										OutputContentEncoding = GetCodePage(Edit.Value);
										Dump("~Output encoding set to: " + OutputContentEncoding.BodyName);
										break;
									case "AddTranslit":
										EnableTransliteration = ToBoolean(Edit.Value);
										Dump("~Enable transliteration");
										break;
								}
							}
						}
					}

					foreach (var hdr in whc.AllKeys)
					{
						Variables.Add("Request." + hdr, whc[hdr]);
						Dump(hdr + ": " + whc[hdr]);
					}
					Dump();

					//send the request
					operation = new HttpOperation(Log);
					operation.Method = ClientRequest.HttpMethod;
					operation.RequestHeaders = whc;
					operation.URL = RequestURL;
					SendRequest(operation);
				}
				catch (System.Net.Http.HttpRequestException httpex)
				{
					//an network error has been catched
					Log.WriteLine(" Cannot load this page: {0}.", httpex.Message);
					Dump("!Network error: " + httpex.Message);
					//try to load the page from Archive.org, then return error message if need
					if (!LookInWebArchive())
					{
						BreakTransit = true;
#if DEBUG
						//return full debug output
						string err = GetFullExceptionMessage(httpex);
						SendInfoPage("WebOne cannot load the page", "Can't load the page: " + httpex.Message, "<i>" + err.ToString().Replace("\n", "<br>") + "</i><br>URL: " + RequestURL.AbsoluteUri + "<br>Debug mode enabled.");
						BreakTransit = true;
#else
						//return nice error message
						string ErrorTitle = "connection error", ErrorMessageHeader = "Cannot load this page", ErrorMessage = "";
						if (httpex.InnerException != null)
						{
							switch (httpex.InnerException.GetType().ToString())
							{
								case "System.Net.Sockets.SocketException":
									System.Net.Sockets.SocketException sockerr = httpex.InnerException as System.Net.Sockets.SocketException;
									switch (sockerr.SocketErrorCode)
									{
										case System.Net.Sockets.SocketError.HostNotFound:
											//server not found
											ErrorMessageHeader = "Cannot find the server";
											ErrorMessage = "<p><big>" + sockerr.Message + "</big></p>" +
											"<ul><li>Check the address for typing errors such as <strong>ww</strong>.example.com instead of <strong>www</strong>.example.com.</li>" +
											"<li>Try to use an <a href='http://web.archive.org/web/" + DateTime.Now.Year + "/" + RequestURL.AbsoluteUri + "'>" + "archived copy</a> of the web site.</li>" +
											"<li>If you are unable to load any pages, check your proxy server's network connection.</li>" +
											"<li>If your proxy server or network is protected by a firewall, make sure that WebOne is permitted to access the Web.</li>" +
											"</ul>";
											break;
										case System.Net.Sockets.SocketError.TimedOut:
											//connection timeout
											ErrorMessageHeader = "The connection has timed out";
											ErrorMessage = "<p><big>" + sockerr.Message + "</big></p>" +
											"<ul><li>The site could be temporarily unavailable or too busy. Try again in a few moments.</li>" +
											"<li>Try to use an <a href='http://web.archive.org/web/" + DateTime.Now.Year + "/" + RequestURL.AbsoluteUri + "'>" + "archived copy</a> of the web site.</li>" +
											"<li>If you are unable to load any pages, check your proxy server's network connection.</li>" +
											"<li>If your proxy server or network is protected by a firewall, make sure that WebOne is permitted to access the Web.</li>" +
											"</ul>";
											break;
										case System.Net.Sockets.SocketError.ConnectionRefused:
											//connection broken
											ErrorMessageHeader = "The connection was refused";
											ErrorMessage = "<p><big>" + sockerr.Message + "</big></p>" +
											"<ul><li>The site could be temporarily unavailable or too busy. Try again in a few moments.</li>" +
											"<li>Try to use an <a href='http://web.archive.org/web/" + DateTime.Now.Year + "/" + RequestURL.AbsoluteUri + "'>" + "archived copy</a> of the web site.</li>" +
											"<li>If you are unable to load any pages, check your proxy server's network connection.</li>" +
											"<li>If your proxy server or network is protected by a firewall, make sure that WebOne is permitted to access the Web.</li>" +
											"</ul>";
											break;
										case System.Net.Sockets.SocketError.ConnectionReset:
											//connection reset
											ErrorMessageHeader = "The connection has been reset";
											ErrorMessage = "<p><big>" + sockerr.Message + "</big></p>" +
											"<ul><li>The site could be temporarily unavailable or too busy. Try again in a few moments.</li>" +
											"<li>Try to use an <a href='http://web.archive.org/web/" + DateTime.Now.Year + "/" + RequestURL.AbsoluteUri + "'>" + "archived copy</a> of the web site.</li>" +
											"<li>If you are unable to load any pages, check your proxy server's network connection.</li>" +
											"<li>If your proxy server or network is protected by a firewall, make sure that WebOne is permitted to access the Web.</li>" +
											"</ul>";
											break;
										default:
											ErrorMessageHeader = "The connection can't be stablished";
											ErrorMessage = "<p><big>" + sockerr.Message + "</big></p>" +
											"<ul><li>The site could be temporarily unavailable or too busy. Try again in a few moments.</li>" +
											"<li>Try to use an <a href='http://web.archive.org/web/" + DateTime.Now.Year + "/" + RequestURL.AbsoluteUri + "'>" + "archived copy</a> of the web site.</li>" +
											"<li>If you are unable to load any pages, check your proxy server's network connection.</li>" +
											"<li>If your proxy server or network is protected by a firewall, make sure that WebOne is permitted to access the Web.</li>" +
											"</ul><br>Error code: " + sockerr.SocketErrorCode;
											break;
									}
									break;
								case "WebOne.TlsPolicyErrorException":
									//certificate is invalid (bad date, domain name, etc)
									string polerr = "";
									switch((httpex.InnerException as TlsPolicyErrorException).PolicyError){
										case System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable:
											polerr = "Certificate is not available.";
											break;
										case System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch:
											polerr = "The certificate is issued for another site.";
											break;
										case System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors:
											polerr = "Certificate chain is incorrect.";
											break;
										default:
											polerr = httpex.InnerException.Message;
											break;
									}
									ErrorMessageHeader = "Secure connection could not be established";
									ErrorMessage = "<p><big>" + polerr + "</big></p>" +
									"<ul><li>The page you are trying to view cannot be shown because the authenticity of the received data could not be verified.</li>" +
											"<li>Make sure that the OS on the proxy server have all updates installed.</li>" +
											"<li>Check date and time on the proxy server.</li>" +
											"<li>Verify that the proxy server operating system have proper support for TLS/SSL version and chiphers used on the site.</li>" +
											"<li>Try to use an <a href='http://web.archive.org/web/" + DateTime.Now.Year + "/" + RequestURL.AbsoluteUri + "'>" + "archived copy</a> of the web site.</li>" +
											"<li>To disable this security check, set <q><b>ValidateCertificates=no</b></q> in proxy configuration file. But this will make the proxy less secure, do this at your own risk.</li>" +
									"</ul>";
									break;
								default:
									ErrorTitle = httpex.InnerException.GetType().ToString();
									ErrorMessage = "There are problems, that are preventing from displaying the page:<br>" + GetFullExceptionMessage(httpex).Replace("\n", "<br>");
									break;
							}
						}
						else
						{
							ErrorTitle = httpex.InnerException.GetType().ToString();
							ErrorMessage = "An error occured: " + httpex.Message;
						}

						ErrorMessage += "<br>URL: " + RequestURL.AbsoluteUri;
						SendInfoPage("WebOne: " + ErrorTitle, ErrorMessageHeader, ErrorMessage);
						BreakTransit = true;
#endif
					}
				}
				catch (System.Threading.Tasks.TaskCanceledException)
				{
					Dump("!Connection timeout (100 sec)");
					BreakTransit = true;

					string ErrorMessageHeader = "The connection has timed out";
					string ErrorMessage = "<p><big>The request was canceled due to Timeout of 100 seconds elapsing.</big></p>" +
					"<ul><li>The site could be temporarily unavailable or too busy. Try again in a few moments.</li>" +
					"<li>Try to use an <a href='http://web.archive.org/web/" + DateTime.Now.Year + "/" + RequestURL.AbsoluteUri + "'>" + "archived copy</a> of the web site.</li>" +
					"<li>Internet Archive sometimes became busy. Be patient, wait a some time.</li>" +
					"<li>If you are unable to load any pages, check your proxy server's network connection.</li>" +
					"<li>If your proxy server or network is protected by a firewall, make sure that WebOne is permitted to access the Web.</li>" +
					"</ul>";

					ErrorMessage += "<br>URL: " + RequestURL.AbsoluteUri;
					SendInfoPage("WebOne: Operation timeout", ErrorMessageHeader, ErrorMessage);
				}
				catch (UriFormatException)
				{
					Dump("!Invalid URL");
					BreakTransit = true;
					SendError(400, "The URL <b>" + RequestURL.AbsoluteUri + "</b> is not valid.");
				}
				catch (Exception ex)
				{
					BreakTransit = true;
					try { Dump("!Guru meditation: " + ex.Message); } catch { }
					Log.WriteLine(" ============GURU MEDITATION:\n{1}\nOn URL '{2}', Method '{3}'. Returning 500.============", null, ex.ToString(), RequestURL.AbsoluteUri, ClientRequest.HttpMethod);
					SendError(500, "Guru meditaion at URL " + RequestURL.AbsoluteUri + ":<br><b>" + ex.Message + "</b><br><i>" + ex.StackTrace.Replace("\n", "\n<br>") + "</i>");
				}

				//look in Web Archive if 404
				if (!BreakTransit && ResponseCode >= 403 && ConfigFile.SearchInArchive)
				{
					LookInWebArchive();
				}

				//shorten Web Archive error page if need
				if (!BreakTransit && ResponseCode >= 403 && RequestURL.AbsoluteUri.StartsWith("http://web.archive.org/web/") && ConfigFile.ShortenArchiveErrors)
				{					
					Log.WriteLine(" Wayback Machine error page shortened.");
					switch (ResponseCode)
					{
						case 404:
							string ErrMsg404 =
							"<p><b>The Wayback Machine has not archived that URL.</b></p>" +
							"<p>Try to slightly change the URL.</p>" +
							"<small><i>You see this message because ShortenArchiveErrors option is enabled.</i></small>";
							SendError(404, ErrMsg404);
							BreakTransit = true;
							break;
						case 403:
							string ErrMsg403 =
							"<p><b>This URL has been excluded from the Wayback Machine.</b></p>" +
							"<p>This page is not present in Web Archive.</p>" +
							"<small><i>You see this message because ShortenArchiveErrors option is enabled.</i></small>";
							SendError(404, ErrMsg403);
							BreakTransit = true;
							break;
						default:
							string ErrMsg000 =
							"<p><b>The Wayback Machine cannot give this page.</b></p>" +
							"<p>This is reason: " + ((HttpStatusCode)ResponseCode).ToString() + ".</p>" +
							"<small><i>You see this message because ShortenArchiveErrors option is enabled.</i></small>";
							SendError(404, ErrMsg000);
							BreakTransit = true;
							break;
					}
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

							if (ClientResponse.ContentLength64 > 300 * 1024) Log.WriteLine(" Sending binary.");
							ClientResponse.OutputStream.Write(RespBuffer, 0, RespBuffer.Length);

							if (DumpFile != null)
							{
								foreach (var hdr in ClientResponse.Headers.AllKeys)
								{
									Dump(hdr + ": " + ClientResponse.Headers[hdr]);
								}
								Dump("\n");

								if (ClientResponse.ContentLength64 < 1024) Dump(ResponseBody);
								else Dump("Over 1 KB response body");
							}

						}
						else
						{
							if (TransitStream.CanSeek) ClientResponse.ContentLength64 = TransitStream.Length;
							TransitStream.CopyTo(ClientResponse.OutputStream);

							if (DumpFile != null)
							{
								foreach (var hdr in ClientResponse.Headers.AllKeys)
								{
									Dump(hdr + ": " + ClientResponse.Headers[hdr]);
								}
								Dump("\nBody is binary stream");
							}
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
			catch (Exception E)
			{
				Log.WriteLine(" A error has been catched: {1}\n{0}\t Please report to author.", null, E.ToString().Replace("\n", "\n{0}\t "));
				SendError(500, "An error occured: " + E.ToString().Replace("\n", "\n<BR>"));
			}
			try { Dump("END."); } catch { }

#if DEBUG
			Log.WriteLine(" End process.");
#endif
		}

		/// <summary>
		/// Send a HTTPS request through <paramref name="HTTPO"/> operation.<br/>
		/// Then the server response will be in the <paramref name="HTTPO"/>.Response. Or an exception will be thrown if there are network problems.
		/// </summary>
		/// <param name="HTTPO">HTTPS operation client with request data</param>
		private void SendRequest(HttpOperation HTTPO)
		{
			if (operation is null) throw new NullReferenceException("Initialize `operation` first! Also don't forget to put headers, method, log agent in Operation.");
			//in future probably need to merge operation.URL with RequestURL.AbsoluteUri too. Seems that they does not differ at SendRequest time, but currently I am not 100% sure - atauenis.

			switch (operation.Method)
			{
				case "CONNECT":
					string ProtocolReplacerJS = "<script>if (window.location.protocol != 'http:') { setTimeout(function(){window.location.protocol = 'http:'; window.location.reload();}, 1000); }</script>";
					SendError(405, "The proxy does not know the " + operation.Method + " method.<BR>Please use HTTP, not HTTPS.<BR>HSTS must be disabled." + ProtocolReplacerJS);
					Log.WriteLine(" Wrong method.");
					return;
				default:
					int Content_Length = 0;
					if (ClientRequest.Headers["Content-Length"] != null) Content_Length = Int32.Parse(ClientRequest.Headers["Content-Length"]);
					if (Content_Length == 0)
					{
						//try to download (GET, HEAD, WebDAV download, etc)
#if DEBUG
						Log.WriteLine(">Downloading content (connecting)...");
#else
						Log.WriteLine(">Downloading content...");
#endif
						operation.URL = RequestURL;
						//operation.AllowAutoRedirect = AllowAutoRedirect; //UNDONE: add fix for AutoRedirect
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
						Log.WriteLine(">Uploading {0}K of {1} (connecting)...", Convert.ToInt32((operation.RequestHeaders["Content-Length"])) / 1024, operation.RequestHeaders["Content-Type"]);
#else
						Log.WriteLine(">Uploading {0}K of {1}...", Convert.ToInt32((operation.RequestHeaders["Content-Length"])) / 1024, operation.RequestHeaders["Content-Type"]);
#endif
						operation.URL = RequestURL;

						if(DumpFile == null)
						{ //if normal operation
							operation.RequestStream = ClientRequest.InputStream;
						}
						else
						{ //if need to save request dump
							MemoryStream RequestDumpStream = new MemoryStream();
							ClientRequest.InputStream.CopyTo(RequestDumpStream);
							RequestDumpStream.Position = 0;
							string DumpOfRequestBody = new StreamReader(RequestDumpStream).ReadToEnd();
							RequestDumpStream.Position = 0;
							operation.RequestStream = RequestDumpStream;
							if (DumpOfRequestBody.Length > 0)
							{
								if (DumpOfRequestBody.Length < 1024) Dump("\nCAUTION: Request body may contain private data!\n" + DumpOfRequestBody);
								else Dump("\nRequest body is longer than 1 KB");
							}
							else Dump("\n");
						}
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
						if (RequestURL.AbsoluteUri == NewLocation.Replace("https://", "http://"))
						{
							Log.WriteLine(">Reload secure...");
							Dump("Upgrade HTTP to HTTPS");

							RequestURL = new Uri(RequestURL.AbsoluteUri.Replace("http://", "https://"));
							TransitStream = null;

							string RequestMethod = operation.Method;
							WebHeaderCollection RequestHeaderCollection = (WebHeaderCollection)ClientRequest.Headers;
							operation = new HttpOperation(Log);
							operation.Method = RequestMethod;
							operation.RequestHeaders = RequestHeaderCollection;
							SendRequest(operation);

							//add to ForceHttp list
							string SecureHost = RequestURL.Host;
							if (!ConfigFile.ForceHttps.Contains(SecureHost))
								ConfigFile.ForceHttps.Add(SecureHost);

							return;
						}

						if (NewLocation.StartsWith("https://"))
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
			if (operation.ResponseHeaders != null)
				for (int i = 0; i < operation.ResponseHeaders.Count; ++i)
				{
					string header = operation.ResponseHeaders.GetKey(i);
					foreach (string value in operation.ResponseHeaders.GetValues(i))
					{
						string corrvalue = value.Replace("https://", "http://");
						corrvalue = Regex.Replace(corrvalue, "; secure", ";", RegexOptions.IgnoreCase);

						if (LocalMode)
						{
							if (corrvalue.StartsWith("http://") && !corrvalue.StartsWith("http://" + GetServerName()))
								corrvalue = corrvalue.Replace("http://", "http://" + GetServerName() + "/http://");
							corrvalue = Regex.Replace(corrvalue, "domain=[^;]*; ", " ", RegexOptions.IgnoreCase);
							corrvalue = Regex.Replace(corrvalue, "path=[^;]*; ", " ", RegexOptions.IgnoreCase);
							//TODO: fix https://github.com/atauenis/webone/issues/21
						}

						if (header.ToLower() == "set-cookie" && corrvalue.Contains(", "))
						{
							//multiple cookies per single header
							//https://stackoverflow.com/questions/51564395/add-multiple-cookies-to-clients-web-browser-via-httplistenerresponse
							//causes https://github.com/atauenis/webone/issues/21 & https://github.com/atauenis/webone/issues/35

							string[] allcookies = corrvalue.Split(", ");

							string cookieplus = "";
							foreach (var cookie in allcookies)
							{
								if (Regex.Match(cookie, "[0-9][0-9]-[A-Z][a-z][a-z]-[0-9][0-9][0-9][0-9]").Success)
								{
									cookieplus += " " + cookie;
									//Console.WriteLine("FOLDED COOKIE: {0}", cookieplus);
									ClientResponse.AppendHeader("Set-Cookie", cookieplus);
								}
								else cookieplus = cookie;
							}
						}

						if (!header.StartsWith("Content-") &&
						!header.StartsWith("Connection") &&
						!header.StartsWith("Transfer-Encoding") &&
						!header.StartsWith("Access-Control-Allow-Methods") &&
						!header.StartsWith("Strict-Transport-Security") &&
						!header.StartsWith("Content-Security-Policy") &&
						!header.StartsWith("Upgrade-Insecure-Requests") &&
						!(header.StartsWith("Vary") && corrvalue.Contains("Upgrade-Insecure-Requests")))
						{
							ClientResponse.AppendHeader(header, corrvalue);
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
									Dump("~~Find & replace (RegEx): " + frpair.Find + " -> " + frpair.Replace);
									break;
							}
						}
					}
				}
			}

			//do transliteration if need
			if (EnableTransliteration)
			{
				foreach (var Letter in ConfigFile.TranslitTable)
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
		/// <param name="StatusCode">HTTP Status code</param>
		/// <param name="ResponseStream">Stream of response body</param>
		/// <param name="ContentType">HTTP Content-Type</param>
		/// <param name="ContentLength">HTTP Content-Lenght</param>
		/// <param name="Operation">The HTTP Request/Response pair (will replace all other arguments in future)</param>
		/// <returns>ResponseBuffer+ResponseBody for texts or TransitStream for binaries</returns>
		private void MakeOutput(HttpOperation Operation)
		{
			HttpStatusCode StatusCode = operation.Response.StatusCode;
			Stream ResponseStream = operation.ResponseStream;
			string ContentType = operation.ResponseHeaders["Content-Type"] ?? "unknown/unknown";
			long ContentLength = operation.Response.Content.Headers.ContentLength ?? 0;
			this.ContentType = ContentType;
			string SrcContentType = ContentType;
			Dump("\n\n" + (int)StatusCode + " HTTP/1.0");

			Variables.TryAdd("Response.HttpStatusCode", ((int)operation.Response.StatusCode).ToString());
			Variables.TryAdd("Response.HttpVersion", operation.Response.Version.ToString());
			foreach (string hdr in operation.ResponseHeaders)
			{
				Variables.TryAdd("Response." + hdr, operation.ResponseHeaders[hdr]);
			}

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
									Dump("~~Convert using: " + Converter);
									break;
								case "AddResponseHeader":
									string RespHdr = ProcessUriMasks(Edit.Value);
									Log.WriteLine(" Add response header: {0}", RespHdr);
									operation.ResponseHeaders.Add(RespHdr);
									if (Edit.Value.StartsWith("Content-Type: ")) ContentType = Edit.Value.Substring("Content-Type: ".Length);
									Dump("~~Add response header: " + RespHdr);
									break;
								case "AddResponseHeaderFindReplace":
									FindReplaceEditSetRule resp_rule = (FindReplaceEditSetRule)Edit;
									foreach (var hdr in operation.ResponseHeaders.AllKeys)
									{
										operation.ResponseHeaders[hdr] = operation.ResponseHeaders[hdr].Replace(resp_rule.Find, resp_rule.Replace);
									}
									Dump("~~Response header find&replace: " + resp_rule.Find + " -> " + resp_rule.Replace);
									break;
								case "AddRedirect":
									Log.WriteLine(" Add redirect: {0}", ProcessUriMasks(Edit.Value));
									Redirect = ProcessUriMasks(Edit.Value);
									Dump("~~Redirect to: " + Redirect);
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
							if (!Cvt.SelfDownload)
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
				catch (Exception ConvertEx)
				{
					Log.WriteLine(" On-fly converter error: {0}", ConvertEx.Message);
					SendError(502,
						"<p><big><b>Converter error</b>: " + ConvertEx.Message + "</big></p>" +
						"Source URL: " + RequestURL.AbsoluteUri + "<br>" +
						"Utility: " + Converter + "<br>" +
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
					Log.WriteLine(" {1} {2}. Body {3}K of {4} [Binary].", null, (int)StatusCode, StatusCode, ContentLength / 1024, ContentType);
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
			//0. if body is empty, think that it's UTF8
			if (RawContent.Length < 1) return Encoding.UTF8;

			//1. check for UTF magic bytes
			if (RawContent[0] == Encoding.UTF8.GetPreamble()[0]) return Encoding.UTF8;
			if (RawContent[0] == Encoding.Unicode.GetPreamble()[0]) return Encoding.Unicode;
			if (RawContent[0] == Encoding.BigEndianUnicode.GetPreamble()[0]) return Encoding.BigEndianUnicode;
			if (RawContent[0] == Encoding.UTF32.GetPreamble()[0]) return Encoding.UTF32;

			//2. get charset from "Content-Type: text/html; charset=UTF-8" header
			if (operation.ResponseHeaders["Content-Type"] != null)
			{
				Match HeaderCharset = Regex.Match(operation.ResponseHeaders["Content-Type"], "; charset=(.*)");
				if (HeaderCharset.Success)
				{
					Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
					return Encoding.GetEncoding(HeaderCharset.Groups[1].Value.ToLower() == "utf8" ? "utf-8" : HeaderCharset.Groups[1].Value);
				}
			}

			//get ANSI charset
			Encoding WindowsEncoding = CodePagesEncodingProvider.Instance.GetEncoding(System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage);

			//3. find Meta Charset tag
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

			return CodePagesEncodingProvider.Instance.GetEncoding(Charset) ?? Encoding.UTF8;
		}

		/// <summary>
		/// Fill values on masks in a string
		/// </summary>
		/// <param name="MaskedURL">String (probably an URL) with %masks%</param>
		/// <param name="PossibleURL">Value for %URL%-based masks</param>
		/// <returns>Ready string with filled fields</returns>
		private string ProcessUriMasks(string MaskedURL, string PossibleURL = "")
		{
			if (PossibleURL == "") PossibleURL = RequestURL.AbsoluteUri;
			return Program.ProcessUriMasks(MaskedURL, PossibleURL, false, Variables);
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
		/// Get this proxy server name and port
		/// </summary>
		private string GetServerName()
		{
			string LocalHostName = ConfigFile.DefaultHostName == Environment.MachineName ? LocalIP : ConfigFile.DefaultHostName;
			if (ConfigFile.Port == 80) return LocalHostName;
			return LocalHostName + ":" + ConfigFile.Port.ToString();
		}

		/// <summary>
		/// Look for the page at RequestURL variable in Internet Archive Wayback Machine
		/// </summary>
		/// <returns>Is the response ready or not</returns>
		private bool LookInWebArchive()
		{
			//check if archived copy can be retreived instead
			if (ConfigFile.SearchInArchive)
			{
				try
				{
					Log.WriteLine(" Look in Archive.org...");
					Dump("=Look in Web Archive...");
					WebArchiveRequest war = new WebArchiveRequest(RequestURL.ToString());
					if (war.Archived)
					{
						Log.WriteLine(" Available.");
						string ArchiveURL = war.ArchivedURL;

						if (ConfigFile.HideArchiveRedirect)
						{ //internal redirect to archive
							try
							{
								//add "id_" suffix (to preserve inline links)
								Match AUrlParts = Regex.Match(ArchiveURL, "^http://web.archive.org/web/([0-9]*)/(.*)");
								//http://web.archive.org/web/$1fw_/$2
								ArchiveURL = "http://web.archive.org/web/" + AUrlParts.Groups[1].Value + "id_/" + AUrlParts.Groups[2].Value;

								RequestURL = new Uri(ArchiveURL);
								Dump("=Go to Web Archive, internal: " + RequestURL.AbsoluteUri);
#if DEBUG
								Log.WriteLine(" Internal download via Web Archive: " + RequestURL.AbsoluteUri);
#endif
								SendRequest(operation);
								return true; //archived copy is ready
							}
							catch (Exception ArchiveRetrieveException)
							{
								SendInfoPage("WebOne: Web Archive retrieve error.", "Cannot load this page from Web Archive", string.Format("<b>The requested page is found only at Web Archive, but cannot be delivered from it.</b><br>{0}", ArchiveRetrieveException.Message.Replace("\n", "<br>")));
								return true; //error page is ready
							}
						}
						else
						{ //regular redirect to archive
							//add suffix if need
							if (ConfigFile.ArchiveUrlSuffix != "")
							{
								Match AUrlParts = Regex.Match(ArchiveURL, "^http://web.archive.org/web/([0-9]*)/(.*)");
								//http://web.archive.org/web/$1fw_/$2
								ArchiveURL = "http://web.archive.org/web/" + AUrlParts.Groups[1].Value + ConfigFile.ArchiveUrlSuffix + "/" + AUrlParts.Groups[2].Value;
							}

							ResponseBody = "<html><body><h1>Server not found</h2>But an <a href=" + ArchiveURL + ">archived copy</a> is available! Redirecting to it...</body></html>";
							ClientResponse.AddHeader("Location", ArchiveURL);
							SendError(302, ResponseBody); //302 redirect is ready
							return true;
						}
					}
					else
					{
						Log.WriteLine(" No snapshots.");
						Dump("=Not in Web Archive");
						return false; //nothing ready
					}
				}
				catch (Exception ArchiveException)
				{
					SendInfoPage("WebOne: Web Archive error.", "Cannot load this page", string.Format("<b>The requested server or page is not found and a Web Archive error occured.</b><br>{0}", ArchiveException.Message.Replace("\n", "<br>")));
					return true; //error page is ready
				}
			}
			else return false; //nothing ready
		}

		/// <summary>
		/// If HTTP traffic sniffing is enabled, write a line to traffic dump
		/// </summary>
		/// <param name="str">The string to write.</param>
		private void Dump(string str = "")
		{
			if (DumpFile != null)
			{
				StreamWriter DumpWriter = new StreamWriter(new FileStream(DumpFile,FileMode.Append));
				DumpWriter.WriteLine(str);
				DumpWriter.Close();
			}
		}

		/// <summary>
		/// Send internal status page (http://proxyhost:port/!)
		/// </summary>
		private void SendInternalStatusPage()
		{
			string HelpString = "";

			if (ConfigFile.DisplayStatusPage == "no")
			{
				SendInfoPage("WebOne status page", "Sorry", "<p>The status page is disabled by server administrator.</p>");
				return;
			}
			else if (ConfigFile.DisplayStatusPage == "short")
			{
				HelpString += "<p>This is <b>" + GetServerName() + "</b>.<br>";
				HelpString += "Pending requests: <b>" + (Load - 1) + "</b>.<br>";
				HelpString += "Used memory: <b>" + (int)Environment.WorkingSet / 1024 / 1024 + "</b> MB.<br>";
				HelpString += "About: <a href=\"https://github.com/atauenis/webone/\">https://github.com/atauenis/webone/</a></p>";
				HelpString += "<p>Client IP: <b>" + ClientRequest.RemoteEndPoint + "</b>.</p>";

				HelpString += "<h2>May be useful:</h2><ul>";
				HelpString += "<li><a href='/auto.pac'>Proxy auto-configuration file</a>: /!pac/, /auto/, /auto, /auto.pac, /wpad.dat.</li>";
				HelpString += "<li><a href='/!ftp/'>Web-based FTP client</a>.</li>";
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
							  "<li><a href='/!codepages/'>/!codepages/</a> - list of available encodings for OutputEncoding setting</li>" +
							  "<li><a href='/!img-test/'>/!img-test/</a> - test if ImageMagick is working</li>" +
							  "<li><a href='/!convert/'>/!convert/</a> - run a file format converter (<a href='/!convert/?src=logo.webp&dest=gif&type=image/gif'>demo</a>)</li>" +
							  //"<li><a href='/!file/'>/!file/</a> - get a file from WebOne working directory (<a href='/!file/?name=webone.conf&type=text/plain'>demo</a>)</li>" +
							  "<li><a href='/!clear/'>/!clear/</a> - remove temporary files in WebOne working directory</li>" +
							  "<li><a href='/auto.pac'>Proxy auto-configuration file</a>: /!pac/, /auto/, /auto, /auto.pac, /wpad.dat.</li>" +
							  "<li><a href='/!ftp/'>Web-based FTP client</a>.</li>" +
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
			string BodyStyleHtml = ConfigFile.PageStyleHtml == "" ? "" : " " + ConfigFile.PageStyleHtml;
			string HtmlHead = "";
			if (ClientResponse.Headers["Refresh"] != null) HtmlHead = "<META HTTP-EQUIV=\"REFRESH\" CONTENT=\"" + ClientResponse.Headers["Refresh"] + "\">";
			HtmlHead += "<META CHARSET=\"" + (OutputContentEncoding ?? Encoding.Default).WebName + "\">";
			HtmlHead += ConfigFile.PageStyleCss == "" ? "" : "<style type='text/css'>" + ConfigFile.PageStyleCss + "</style>";
			string Html = "<HTML>" + HtmlHead + "<BODY" + BodyStyleHtml + "><H1>" + CodeStr + "</H1>" + Text + "</BODY></HTML>";

			byte[] Buffer = (OutputContentEncoding ?? Encoding.Default).GetBytes(Html);
			try
			{
				ClientResponse.StatusCode = Code;
				ClientResponse.ProtocolVersion = new Version(1, 0);

				ClientResponse.ContentType = "text/html";
				ClientResponse.ContentLength64 = Buffer.Length;
				ClientResponse.OutputStream.Write(Buffer, 0, Buffer.Length);
				ClientResponse.OutputStream.Close();
				Dump("End is internal page: code " + Code + ", " + Text);
			}
			catch (Exception ex)
			{
				if (!ConfigFile.HideClientErrors)
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
			catch (Exception ex)
			{
				int ErrNo = 500;
				if (ex is FileNotFoundException) ErrNo = 404;
				SendError(ErrNo, "Cannot open the file <i>" + FileName + "</i>.<br>" + ex.ToString().Replace("\n", "<br>"));
			}
			Dump("End is file " + FileName);
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
				Log.WriteLine("<Send stream with {2}K of {1}.", null, ContentType, Potok.Length / 1024);
			else
				Log.WriteLine("<Send {0} stream.", ContentType);
			try
			{
				ClientResponse.StatusCode = 200;
				ClientResponse.ProtocolVersion = new Version(1, 0);
				ClientResponse.ContentType = ContentType;
				if (Potok.CanSeek) ClientResponse.ContentLength64 = Potok.Length;
				if (Potok.CanSeek) Potok.Position = 0;
				Potok.CopyTo(ClientResponse.OutputStream);
				if (Close)
				{
					ClientResponse.OutputStream.Close();
					Potok.Close();
				}
			}
			catch (Exception ex)
			{
				int ErrNo = 500;
				if (ex is FileNotFoundException) ErrNo = 404;
				SendError(ErrNo, "Cannot retreive stream.<br>" + ex.ToString().Replace("\n", "<br>"));
			}
			Dump("End is stream of " + ContentType);
		}

		/// <summary>
		/// Send a information page to client
		/// </summary>
		/// <param name="Title">The information page title</param>
		/// <param name="Header1">The information page 1st level header (or null if no title)</param>
		/// <param name="Content">The information page content (HTML)</param>
		private void SendInfoPage(string Title = null, string Header1 = null, string Content = "No description is available.")
		{
			InfoPage infoPage = new(Title, Header1, Content);
			SendInfoPage(infoPage);
		}

		/// <summary>
		/// Send a information page to client
		/// </summary>
		/// <param name="Page">The information page body</param>
		private void SendInfoPage(InfoPage Page)
		{
			if(Page.HttpHeaders != null)
			foreach (var hdr in Page.HttpHeaders.AllKeys) ClientResponse.AddHeader(hdr, Page.HttpHeaders[hdr]);
			ClientResponse.StatusCode = Page.HttpStatusCode;

			if (Page.Attachment == null)
			{
				Log.WriteLine("<Return information page: {0}.", Page.Title);

				string BodyStyleHtml = ConfigFile.PageStyleHtml == "" ? "" : " " + ConfigFile.PageStyleHtml;
				string BodyStyleCss = ConfigFile.PageStyleCss == "" ? "" : "<style type='text/css'>" + ConfigFile.PageStyleCss + "</style>";
				string title = "<title>WebOne: untitled</title>"; if (Page.Title != null) title = "<title>" + Page.Title + "</title>\n";
				string header1 = ""; if (Page.Header != null) header1 = "<h1>" + Page.Header + "</h1>\n";

				string Html = "<html>\n" +
				title +
				string.Format("<meta charset=\"{0}\"/>", OutputContentEncoding == null ? "utf-8" : OutputContentEncoding.WebName) + "\n" +
				BodyStyleCss +
				"<body" + BodyStyleHtml + ">\n" +
				header1 + "\n" +
				Page.Content + "\n" +
				GetInfoString() + "\n" +
				"</body>\n</html>";

				if ((OutputContentEncoding ?? Encoding.Default) != Encoding.Default)
					Html = OutputContentEncoding.GetString(Encoding.Default.GetBytes(Html));

				byte[] Buffer = (OutputContentEncoding ?? Encoding.Default).GetBytes(Html);
				try
				{
					ClientResponse.StatusCode = Page.HttpStatusCode;
					ClientResponse.ProtocolVersion = new Version(1, 0);

					if(Page.HttpHeaders["Content-Type"] != null)
						ClientResponse.ContentType = Page.HttpHeaders["Content-Type"];
					else
						ClientResponse.ContentType = "text/html";

					ClientResponse.ContentLength64 = Buffer.Length;

					ClientResponse.OutputStream.Write(Buffer, 0, Buffer.Length);
					ClientResponse.OutputStream.Close();
					Dump("End is information page: " + Page.Header);
				}
				catch (Exception ex)
				{
					if (!ConfigFile.HideClientErrors)
						Log.WriteLine("<!Cannot return information page {1}. {2}: {3}", null, Page.Title, ex.GetType(), ex.Message);
				}

			}
			else
			{
				SendStream(Page.Attachment, Page.AttachmentContentType);
			}
		}
	}

	/// <summary>
	/// An information page (used by internal status and other pages inside WebOne)
	/// </summary>
	public class InfoPage
	{
		/// <summary>
		/// The information page title
		/// </summary>
		public string Title { get; set; }
		/// <summary>
		/// The information page 1st level header (or null if no title)
		/// </summary>
		public string Header { get; set; }
		/// <summary>
		/// The information page content (HTML)
		/// </summary>
		public string Content { get; set; }

		/// <summary>
		/// Attached binary file (used instead of the page body)
		/// </summary>
		public Stream Attachment { get; set; }

		public string AttachmentContentType 
		{
			get { return HttpHeaders["Content-Type"]; }
			set { HttpHeaders["Content-Type"] = value; }
		}

		/// <summary>
		/// Additional HTTP headers, which can be sent to the client
		/// </summary>
		public WebHeaderCollection HttpHeaders { get; set; }

		/// <summary>
		/// HTTP status code ("200 OK" by default)
		/// </summary>
		public int HttpStatusCode { get; set; }

		/// <summary>
		/// Create an information page
		/// </summary>
		/// <param name="Title">The information page title</param>
		/// <param name="Header">The information page 1st level header (or null if no title)</param>
		/// <param name="Content">The information page content (HTML)</param>
		/// <param name="HttpStatusCode">The information page HTTP status code (200/404/302/500/etc)</param>
		public InfoPage(string Title = null, string Header = null, string Content = "No description is available.", int HttpStatusCode = 200)
		{
			this.HttpStatusCode = HttpStatusCode;
			this.HttpHeaders = new WebHeaderCollection();
			this.Title = Title;
			this.Header = Header;
			this.Content = Content;
		}
	}
}
