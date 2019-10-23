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
		bool ShouldRedirectInNETFW = false;

		HttpResponse response;
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

				//fix "carousels"
				string RefererUri = ClientRequest.Headers["Referer"];
				RequestURL = ClientRequest.Url;

				//check for local or internal URL
				if (RequestURL.Host == "localhost" || RequestURL.Host == Environment.MachineName || RequestURL.Host == "127.0.0.1")
				{
					if (RequestURL.PathAndQuery.StartsWith("/!"))
					{
						//internal URLs
						Console.WriteLine("{0}\t Internal: {1} ", GetTime(BeginTime), RequestURL.PathAndQuery);
						switch (RequestURL.AbsolutePath)
						{
							case "/!codepages/":
								string codepages = "The following code pages are available: <br>\n" +
												   "<table><tr><td><b>Name</b></td><td><b>#</b></td><td><b>Description</b></td></tr>\n";
								codepages += "<tr><td><b>AsIs</b></td><td>0</td><td>Leave page's code page as is</td></tr>\n";
								foreach (EncodingInfo cp in Encoding.GetEncodings())
								{
									if (Encoding.Default.EncodingName.Contains(" ") && cp.DisplayName.Contains(Encoding.Default.EncodingName.Substring(0, Encoding.Default.EncodingName.IndexOf(" "))))
										codepages += "<tr><td><b><u>" + cp.Name + "</u></b></td><td><u>" + cp.CodePage + "</u></td><td><u>" + cp.DisplayName + (cp.CodePage == Encoding.Default.CodePage ? "</u> (<i>system default</i>)" : "</u>") + "</td></tr>\n";
									else
										codepages += "<tr><td><b>" + cp.Name + "</b></td><td>" + cp.CodePage + "</td><td>" + cp.DisplayName + "</td></tr>\n";
								}
								codepages += "</table><br>Use any of these. Underlined are for your locale.";
								SendError(200, codepages);
								break;
							default:
								SendError(200, "Unknown internal URL: " + RequestURL.PathAndQuery);
								break;
						}
						return;
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
					SendError(101, "Cannot work with " + RequestURL.Scheme.ToUpper() + " protocol. Please connect directly bypassing the proxy.");//, "\nUpgrade: " + RequestURL.Scheme.ToUpper());
					return;
				}

				//dirty workarounds for HTTP>HTTPS redirection bugs
				if ((RequestURL.AbsoluteUri == RefererUri || RequestURL.AbsoluteUri == LastURL) && RequestURL.AbsoluteUri != "" && ClientRequest.HttpMethod != "POST" && ClientRequest.HttpMethod != "CONNECT" && !Program.CheckString(RequestURL.AbsoluteUri, ConfigFile.ForceHttps))
				{
					Console.WriteLine("{0}\t Carousel detected.", GetTime(BeginTime));
					if (!LastURL.StartsWith("https") && !RequestURL.AbsoluteUri.StartsWith("https")) //if http is gone, try https
						RequestURL = new Uri("https" + RequestURL.AbsoluteUri.Substring(4));
					if (LastURL.StartsWith("https")) //if can't use https, try again http
						RequestURL = new Uri("http" + RequestURL.AbsoluteUri.Substring(4));
				}

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
								string NewURL = Redirect.Replace("%URL%", RequestURL.AbsoluteUri).Replace("%UrlNoDomain%", RequestURL.AbsoluteUri.Substring(RequestURL.AbsoluteUri.IndexOf("/") + 2).Substring((RequestURL.AbsoluteUri.Substring(RequestURL.AbsoluteUri.IndexOf("/") + 2)).IndexOf("/") + 1));
								//need to fix urlnodomain to extract using System.Uri instead of InStr/Mid$.

								if (Redirect.Contains("%UrlNoPort%"))
								{
									
									Uri NewUri = RequestURL;
									var builder = new UriBuilder(NewUri);
									builder.Port = -1;
									NewUri = builder.Uri;
									NewURL = NewUri.ToString();
								}
								/*else {
									NewUri = new Uri(NewURL);
									NewURL = NewUri.ToString();
								}*/
								
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
						}
					}
				}


				LastURL = RequestURL.AbsoluteUri;

				//make reply
				//SendError(200, "Okay, bro! Open " + RequestURL);
				HTTPC https = new HTTPC();
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
					string err = ": " + wex.Status.ToString();
#if DEBUG
					ResponseBody = "<html><body>Cannot load this page" + err + "<br><i>" + wex.ToString().Replace("\n", "<br>") + "</i><br>URL: " + RequestURL.AbsoluteUri + Program.GetInfoString() + "</body></html>";
#else
					ResponseBody = "<html><body>Cannot load this page" + err + " (<i>" + wex.Message + "</i>)<br>URL: " + RequestURL.AbsoluteUri + Program.GetInfoString() + "</body></html>";
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

							if(ClientResponse.ContentLength64 > 300*1024) Console.WriteLine("{0}\t Sending binary.", GetTime(BeginTime));
							ClientResponse.OutputStream.Write(RespBuffer, 0, RespBuffer.Length);
						}
						else
						{
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
				SendError(500, "WTF?! " + E.ToString().Replace("\n", "\n<BR>"));
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
			string RequestBody;

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
						response = https.GET(RequestURL.AbsoluteUri, new CookieContainer(), RequestHeaderCollection, RequestMethod, AllowAutoRedirect);
						MakeOutput(response.StatusCode, response.Stream, response.ContentType, response.ContentLength);
						break;
					}
					else
					{
						//try to upload (POST, PUT, WebDAV, etc)
						Console.WriteLine("{0}\t>Reading input stream...", GetTime(BeginTime));
						StreamReader body_sr = new StreamReader(ClientRequest.InputStream);
						RequestBody = body_sr.ReadToEnd();
						Console.WriteLine("{0}\t>Uploading {1}K...", GetTime(BeginTime), Convert.ToInt32((RequestHeaderCollection["Content-Length"])) / 1024);
						response = https.POST(RequestURL.AbsoluteUri, new CookieContainer(), RequestBody, RequestHeaderCollection, RequestMethod, AllowAutoRedirect);
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
			//if (LocalMode) Body = Body.Replace("http://", "http://" + Environment.MachineName + "/http://");//replace with real hostname
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
					Console.WriteLine("{0}\t Body maked.", GetTime(BeginTime));
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
				Console.WriteLine("{0}\t {1} {2}. Body {3}K of {4} [Binary].", GetTime(BeginTime), (int)StatusCode, StatusCode, response.ContentLength / 1024, ContentType);
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
				ClientResponse.ContentLength64 = Html.Length;
				ClientResponse.OutputStream.Write(Buffer, 0, Buffer.Length);
				ClientResponse.OutputStream.Close();
			}
			catch
			{
				Console.WriteLine("{0}\t<!Cannot return code {1}.", GetTime(BeginTime), Code);
			}
		}
	}
}
