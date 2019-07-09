using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebOne
{
	/// <summary>
	/// Транзитная передача HTTP-контента с исправлением содержимого
	/// </summary>
	class Transit
	{
		static string LastURL = "http://999.999.999.999/CON";//dirty workaround for phantom.sannata.org and similar sites
		byte[] UTF8BOM = Encoding.UTF8.GetPreamble();

		byte[] Buffer = new byte[ConfigFile.RequestBufferSize]; //todo: adjust for real request size
		string Request = "";
		string RequestHeaders = "";
		string RequestBody = "";
		int RequestHeadersEnd;
		string RequestUri = "about:blank";
		bool LocalMode = false;

		HttpResponse response;
		int ResponseCode = 502;
		string ResponseHeaders;
		string ResponseBody = ":(";
		byte[] ResponseBuffer;
		Stream TransitStream = null;
		string ContentType = "";

		//Based on https://habr.com/ru/post/120157/
		//Probably the class name needs to be changed

		/// <summary>
		/// Convert a Web 2.0 page to Web 1.0-like page.
		/// </summary>
		/// <param name="Client">TcpListener client</param>
		public Transit(TcpClient Client)
		{
			Console.Write("\n>");
			try
			{

				int Count = 0;
				NetworkStream ClientStream;
				try
				{
					//get request
					ClientStream = Client.GetStream();

					for (int i = 0; i < ConfigFile.ClientTimeout; i++) { Console.CursorLeft = Console.CursorLeft; if (ClientStream.DataAvailable) break; } //wait for slow clients
																																						   //while (true) { if (ClientStream.DataAvailable) break; }

					while (ClientStream.DataAvailable)
					{
						ClientStream.Read(Buffer, Count, 1);
						Count++;
					}
					Request += Encoding.Default.GetString(Buffer).Trim('\0'); //cut empty 10 megabytes

					//check if the request is empty and report to try again.
					if (Request.Length == 0) { Console.Write(" Empty request."); SendError(Client, 302, "The request wasn't heard. Please try again.", "\nLocation: "); return; }

					RequestHeadersEnd = Request.IndexOf("\r\n\r\n");
					RequestHeaders = Request.Substring(0, RequestHeadersEnd);
					RequestBody = Request.Substring(RequestHeadersEnd + 4);

					/*Console.Write("-{0} of {1}-", Request.IndexOf("\r\n\r\n"), Request.Length);
					Console.Write("-POST body={0}-", Request.Substring(Request.IndexOf("\r\n\r\n")).Trim("\r\n\r\n".ToCharArray()));*/
				}
				catch (System.IO.IOException ioe)
				{
					Console.WriteLine("Can't read from client: " + ioe.ToString());
					SendError(Client, 500);
					return;
				}


				//find URL, method and headers
				Match ReqMatch = Regex.Match(Request, @"^\w+\s+([^\s]+)[^\s]*\s+HTTP/.*|");
				if (ReqMatch == Match.Empty)
				{
					//If the request seems to be invalid, raise HTTP 400 error.
					Console.WriteLine(" Invalid request");
					SendError(Client, 400, "Invalid request");
					return;
				}

				string RequestMethod = "";
				string[] Headers = RequestHeaders.Split('\n');
				RequestMethod = Headers[0].Substring(0, Headers[0].IndexOf(" "));

				WebHeaderCollection RequestHeaderCollection = new WebHeaderCollection();
				foreach (string hdr in Headers)
				{
					if (hdr.Contains(": ")) //exclude method & URL
					{
						RequestHeaderCollection.Add(hdr.Replace("\r","")); //todo: add non-latin character urlencoding to prevent ArgumentException on cyrillic URLs
					}
					//if (hdr.Contains("ookie")) Console.WriteLine("Sent cookie: " + hdr);
				}

				//check for login to proxy if need
				if (ConfigFile.Authenticate != "")
				{
					if (RequestHeaderCollection["Proxy-Authorization"] == null || RequestHeaderCollection["Proxy-Authorization"] == "")
					{
						SendError(Client, 407, "Hello! This Web 2.0-to-1.0 proxy server is private. Please enter your credentials.", "\n" + @"Proxy-Authenticate: Basic realm=""Log in to WebOne""");
						return;
					}
					else
					{
						string auth = Encoding.Default.GetString(Convert.FromBase64String(RequestHeaderCollection["Proxy-Authorization"].Substring(6)));
						if (auth != ConfigFile.Authenticate)
						{
							SendError(Client, 407, "Your password is not correct. Please try again.", "\n" + @"Proxy-Authenticate: Basic realm=""Your WebOne credentials are incorrect""");
							return;
						}
					}
				}

				//fix "carousels"
				string RefererUri = RequestHeaderCollection["Referer"];
				RequestUri = ReqMatch.Groups[1].Value;

				//check for local or internal URL
				if (RequestUri.StartsWith("/"))
				{
					if (RequestUri.StartsWith("/!"))
					{
						//internal URLs
						Console.Write("Internal: " + RequestUri);
						switch (RequestUri.Substring(2, RequestUri.Length - 3))
						{
							case "codepages":
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
								SendError(Client, 200, codepages);
								break;
							default:
								SendError(Client, 200, "Unknown internal URL: " + RequestUri.Substring(2, RequestUri.Length - 3));
								break;
						}
						return;
					}
					Console.Write("Local:");
					LocalMode = true;
					if (RequestUri.StartsWith("/http:") || RequestUri.StartsWith("/https:"))
						RequestUri = RequestUri.Substring(1); //local mode: http://localhost:80/http://example.com/indexr.shtml
					else
					{
						//bad local mode, try to use last used host: http://localhost:80/favicon.ico
						List<int> includes = new List<int>();

						int i = LastURL.IndexOf("/", 0);
						while (i > -1)
						{
							includes.Add(i);
							i = LastURL.IndexOf("/", i + 1);
						}
						if (includes.Count < 3)
						{
							Console.Write("Bad direct URL ''", RequestUri);
							SendError(Client, 400, "Bad direct URL: " + RequestUri + "\n<br>Expected something like " + LastURL);
							return;
						}
						RequestUri = "http://" + LastURL.Substring(includes[0] + 2, includes[2] - includes[0] - 2) + RequestUri;
					}

				}

				//check for HTTP-to-FTP requests
				//https://support.microsoft.com/en-us/help/166961/how-to-ftp-with-cern-based-proxy-using-wininet-api
				string ProtocolName = RequestUri.Substring(0, RequestUri.IndexOf(":"));
				string[] BadProtocols = { "ftp", "gopher", "wais" };
				if (Program.CheckString(ProtocolName, BadProtocols))
				{
					Console.Write(" " + RequestUri + " is a CERN Proxy request.");
					SendError(Client, 101, "Cannot work with " + ProtocolName.ToUpper() + " protocol. Please connect directly bypassing the proxy.", "\nUpgrade: " + ProtocolName.ToUpper());
					return;
				}

				//dirty workarounds for HTTP>HTTPS redirection bugs
				if ((RequestUri == RefererUri || RequestUri == LastURL) && RequestUri != "" && RequestMethod != "POST")
				{
					Console.Write("Carousel");
					if (!LastURL.StartsWith("https") && !RequestUri.StartsWith("https")) //if http is gone, try https
						RequestUri = "https" + RequestUri.Substring(4);
					if (LastURL.StartsWith("https")) //if can't use https, try again http
						RequestUri = "http" + RequestUri.Substring(4);
				}

				Console.Write(" " + RequestUri + " ");
				LastURL = RequestUri;

				//check for too new frameworks & replace with older versions
				foreach (string str in ConfigFile.FixableURLs)
				{
					if (Regex.Match(RequestUri, str).Success)
					{
						try
						{
							string ValidMask = "";
							if (ConfigFile.FixableUrlActions[str].ContainsKey("ValidMask")) ValidMask = ConfigFile.FixableUrlActions[str]["ValidMask"];

							string Redirect = "about:mozilla";
							if (ConfigFile.FixableUrlActions[str].ContainsKey("Redirect")) Redirect = ConfigFile.FixableUrlActions[str]["Redirect"];

							if (ValidMask == "" || !Regex.Match(RequestUri, ValidMask).Success/*!RequestUri.Contains(ConfigFile.FixableUrlActions[str]["ValidMask"])*/)
							{
								string NewURL = Redirect.Replace("%URL%", RequestUri).Replace("%UrlNoDomain%", RequestUri.Substring(RequestUri.IndexOf("/") + 2).Substring((RequestUri.Substring(RequestUri.IndexOf("/") + 2)).IndexOf("/") + 1));
								Console.Write("Fix to {1}", RequestUri, NewURL, ValidMask);
								SendError(Client, 302, "Брось каку!", "\nLocation: " + NewURL);
								return;
							}
						}
						catch (Exception rex)
						{
							Console.Write("Cannot redirect! " + rex.Message);
							SendError(Client, 200, rex.ToString().Replace("\n", "\n<br>"));
						}
					}
				}

				//make reply
				//SendError(Client, 200);

				HTTPC https = new HTTPC();
				bool StWrong = false; //break operation if something is wrong.
				Console.Write("Try to " + RequestMethod.ToLower());

				if (RequestUri.Contains("??")) { StWrong = true; SendError(Client, 400, "Too many questions."); }
				if (RequestUri.Length == 0) { StWrong = true; SendError(Client, 400, "Empty URL."); }

				try
				{
					int CL = 0;
					if (RequestHeaderCollection["Content-Length"] != null) CL = Int32.Parse(RequestHeaderCollection["Content-Length"]);

					switch (RequestMethod)
					{
						case "GET":
							//try to get...
							response = https.GET(RequestUri, new CookieContainer(), RequestHeaderCollection);
							MakeOutput(response);
							break;
						case "POST":
							//try to post...
							response = https.POST(RequestUri, new CookieContainer(), RequestBody, RequestHeaderCollection);
							MakeOutput(response);
							break;
						case "CONNECT":
							SendError(Client, 405, "The proxy does not know the " + RequestMethod + " method.<BR>Please use HTTP, not HTTPS.");
							Console.WriteLine(" Wrong method.");
							return;
						default:
							if (CL == 0)
							{
								//try to download (HEAD, WebDAV download, etc)
								response = https.GET(RequestUri, new CookieContainer(), RequestHeaderCollection, RequestMethod);
								MakeOutput(response);
								break;
							}
							else 
							{
								//try to upload (PUT, WebDAV, etc)
								Console.Write(" CL={0}K", Convert.ToInt32((RequestHeaderCollection["Content-Length"])) / 1024);
								response = https.POST(RequestUri, new CookieContainer(), RequestBody, RequestHeaderCollection, RequestMethod);
								MakeOutput(response);
								break;
							}
					}

					ResponseCode = (int)response.StatusCode;

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
								ResponseHeaders += (header + ": " + value.Replace("; secure", "") + "\n").Replace("https://", "http://");
								//Console.WriteLine(header + ": " + value.Replace("; secure", "").Replace("no-cache=\"set-cookie\"", ""));
								//if (header.Contains("ookie")) Console.WriteLine("Got cookie: " + value);
							}
						}
					}

					/*if (RequestMethod=="OPTIONS")
					{
						ResponseHeaders += "Access-Control-Allow-Methods: POST, GET, OPTIONS\n";
					}*/


				}
				catch (WebException wex)
				{
					if (wex.Response == null) ResponseCode = 502;
					else ResponseCode = (int)(wex.Response as HttpWebResponse).StatusCode;
					Console.Write("...WEB EXCEPTION=");
					if (ResponseCode == 502) Console.Write(wex.Status); else Console.Write(ResponseCode + " " + (wex.Response as HttpWebResponse).StatusCode);

					string err = ": " + wex.Status.ToString();
					/*if(wex.Response != null)
					{
						err = " because HTTP status code is not okay: " + (wex.Response as HttpWebResponse).StatusCode.ToString();

						switch((wex.Response as HttpWebResponse).StatusCode) {
							case HttpStatusCode.NotModified:
								SendError(Client, 304, "Not modified, see in cache.");
								StWrong = true;
								break;
							case HttpStatusCode.NotFound:
								SendError(Client, 404, "The requested document is not found on server.");
								StWrong = true;
								break;
							case HttpStatusCode.Forbidden:
								SendError(Client, 403, "You're not welcome here.");
								StWrong = true;
								break;
							default:
								break;
						}
					}*/
#if DEBUG
				ResponseBody = "<html><body>Cannot load this page" + err + "<br><i>" + wex.ToString().Replace("\n", "<br>") + "</i><br>URL: " + RequestUri + Program.GetInfoString() + "</body></html>";
#else
					ResponseBody = "<html><body>Cannot load this page" + err + " (<i>" + wex.Message + "</i>)<br>URL: " + RequestUri + Program.GetInfoString() + "</body></html>";
#endif

					if (wex.Response != null)
					{
						for (int i = 0; i < wex.Response.Headers.Count; ++i)
						{
							string header = wex.Response.Headers.GetKey(i);
							foreach (string value in wex.Response.Headers.GetValues(i))
							{
								if (!header.StartsWith("Connection") && !header.StartsWith("Transfer-Encoding"))
								{
									ResponseHeaders += (header + ": " + value.Replace("; secure", "").Replace("no-cache=\"set-cookie\"", "") + "\n");
									if (RequestMethod == "OPTIONS" && header == "Allow") Console.Write("[Options allowed: {0}]", value);
								}
							}
						}
						ContentType = wex.Response.ContentType;
						ResponseBody = new StreamReader(wex.Response.GetResponseStream()).ReadToEnd();
						//ResponseBody = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(new StreamReader(wex.Response.GetResponseStream()).ReadToEnd()));
						//todo: add correct returning of error pages through MakeOutput subprogram.
					}

					Console.WriteLine(".Failed[{0}].", ResponseCode);
				}
				catch (UriFormatException)
				{
					StWrong = true;
					SendError(Client, 400, "The URL <b>" + RequestUri + "</b> is not valid.");
				}
				catch (Exception ex)
				{
					StWrong = true;
					Console.WriteLine("============GURU MEDITATION:\n{0}\nOn URL '{1}', Method '{2}'. Returning 500.============", ex.ToString(), RequestUri, RequestMethod);
					SendError(Client, 500, "Guru meditaion at URL " + RequestUri + ":<br><b>" + ex.Message + "</b><br><i>" + ex.StackTrace.Replace("\n", "\n<br>") + "</i>");
				}

				try
				{
					//try to return...
					if (!StWrong)
					{
						ResponseHeaders += "Via: HTTP/1.0 WebOne/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + "\n";
						ResponseHeaders += "Connection: close\n";
						//ResponseHeaders += "Warning: 214 WebOne/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + @" ""Patched for old browser""" + "\n";

						/*if (ResponseCode == 301 && RequestUri == response.Headers["Location"].Replace("https:","http:")) 
						{
							/*SendError(Client, 302, "Please try again to enter a carousel to HTTPS.", "\nLocation: \nRefresh: 0\n");
							return;*//*
							ResponseHeaders += "Refresh: 0;" + RequestUri + "\n";
							Console.Write("Redirect to reload. ");
						}*/

						if (Program.CheckString(ContentType, ConfigFile.TextTypes) || ContentType == "")
							ResponseHeaders = "HTTP/1.0 " + ResponseCode + "\n" + ResponseHeaders + "Content-Type: " + ContentType + "\nContent-Length: " + ResponseBody.Length;
						else
							ResponseHeaders = "HTTP/1.0 " + ResponseCode + "\n" + ResponseHeaders + "Content-Type: " + ContentType + "\nContent-Length: " + response.ContentLength;

						byte[] RespBuffer = (ConfigFile.OutputEncoding ?? Encoding.Default).GetBytes(ResponseHeaders + "\n\n");
						if (TransitStream == null)
						{
							RespBuffer = RespBuffer.Concat(ResponseBuffer ?? (ConfigFile.OutputEncoding ?? Encoding.Default).GetBytes(ResponseBody)).ToArray();
							ClientStream.Write(RespBuffer, 0, RespBuffer.Length);
						}
						else
						{
							ClientStream.Write(RespBuffer, 0, RespBuffer.Length);
							TransitStream.CopyTo(ClientStream);
						}
						Client.Close();
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("Cannot return reply to the client. " + ex.Message + ex.StackTrace);
				}

			}
			catch(Exception ex)
			{
				Console.WriteLine("A error has been catched: {0}\nPlease report to author.", ex.ToString());
				SendError(Client, 500, "WTF?! " + ex.ToString().Replace("\n", "\n<BR>"));
				return;
			}
			Console.WriteLine("The client is served.");
		}

		/// <summary>
		/// Make reply's body as byte array
		/// </summary>
		/// <param name="response">The HTTP Response</param>
		/// <returns>ResponseBuffer+ResponseBody for texts or TransitStream for binaries</returns>
		private void MakeOutput(HttpResponse response) {
			Console.Write("...");
			Console.Write(response.StatusCode);
			ContentType = response.ContentType;
			Console.Write("...Body {0}K of {1}.", response.ContentLength / 1024, ContentType);

			if (Program.CheckString(ContentType, ConfigFile.TextTypes))
			{
				//если сервер вернул текст, сделать правки, иначе прогнать как есть дальше
				Console.Write("[Text]");

				if (ConfigFile.OutputEncoding == null)
				{
					//if don't touch codepage (OutputEncoding=AsIs)
					ResponseBody = Encoding.Default.GetString(response.RawContent);
					ResponseBody = ProcessBody(ResponseBody);
					ResponseBuffer = Encoding.Default.GetBytes(ResponseBody);
					Console.WriteLine(".");
					return;
				}

				bool ForceUTF8 = ContentType.ToLower().Contains("utf-8");
				if(!ForceUTF8 && response.RawContent.Length > 0) ForceUTF8 = response.RawContent[0] == Encoding.UTF8.GetPreamble()[0];
				foreach (string utf8url in ConfigFile.ForceUtf8) { if (Regex.IsMatch(RequestUri, utf8url)) ForceUTF8 = true; }
				//todo: add fix for "invalid range in character class" at www.yandex.ru with Firefox 3.6 if OutputEncoding!=AsIs

				if (ForceUTF8) ResponseBody = Encoding.UTF8.GetString(response.RawContent);
				else ResponseBody = Encoding.Default.GetString(response.RawContent);
				
				if (Regex.IsMatch(ResponseBody, @"<meta.*UTF-8.*>", RegexOptions.IgnoreCase)) { ResponseBody = Encoding.UTF8.GetString(response.RawContent); }
				//ResponseBuffer = ConfigFile.OutputEncoding.GetBytes(ResponseBody);

				if (ContentType.ToLower().Contains("utf-8")) ContentType = ContentType.Substring(0, ContentType.IndexOf(';'));
				ResponseBody = ProcessBody(ResponseBody);
				ResponseBuffer = ConfigFile.OutputEncoding.GetBytes(ResponseBody);
			}
			else
			{
				Console.Write("[Binary]");
				TransitStream = response.Stream;
			}
			Console.WriteLine(".");
			return;
		}

		/// <summary>
		/// Process the reply's body and fix too modern stuff
		/// </summary>
		/// <param name="Body">The original body</param>
		/// <returns>The fixed body, compatible with old browsers</returns>
		private string ProcessBody(string Body) {
			Body = Body.Replace("https", "http");
			if(LocalMode) Body = Body.Replace("http://", "http://" + Environment.MachineName + "/http://");//replace with real hostname
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
			
			/*if(true) {
				//content patching
				Body = Regex.Replace(Body, @"<script(.|\n)*\/script>", "<!-- WebOne-JS $& /WebOne-JS -->", RegexOptions.Multiline);
				Body = Regex.Replace(Body, @"<style(.|\n)*\/style>", "<!-- WebOne-CSS $& /WebOne-CSS -->", RegexOptions.Multiline);
			}*/
			return Body;
		}

		/// <summary>
		/// Send a HTTP error to client
		/// </summary>
		/// <param name="Client">TcpListener client</param>
		/// <param name="Code">Error code number</param>
		/// <param name="Text">Error description for user</param>
		/// <param name="ExtraHeaders">Additional headers (like "Location: filename.ext" for 302 or "Refresh: 10; filename.ext" for 200)</param>
		private void SendError(TcpClient Client, int Code, string Text = "", string ExtraHeaders = "")
		{
			Console.Write("["+Code+"]\n");
			Text += Program.GetInfoString();
			string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
			string Refresh = "<META HTTP-EQUIV=REFRESH CONTENT=0>";
			if (Code != 302 || ExtraHeaders.StartsWith("Refresh:") || ExtraHeaders.StartsWith("Location:")) Refresh = "";
			string Html = "<html>" + Refresh + "<body><h1>" + CodeStr + "</h1>"+Text+"</body></html>";
			string Str = "HTTP/1.0 " + CodeStr + "\nContent-type: text/html\nContent-Length:" + Html.Length.ToString() + ExtraHeaders + "\n\n" + Html;
			byte[] Buffer = Encoding.Default.GetBytes(Str);
			try
			{
				Client.GetStream().Write(Buffer, 0, Buffer.Length);
				Client.Close();
			}
			catch {
				Console.WriteLine("Cannot return HTTP error " + Code);
			}
		}

		/// <summary>
		/// Read a Stream as byte array
		/// </summary>
		/// <param name="input">The stream to be read</param>
		/// <returns></returns>
		private byte[] ReadFully(Stream input)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				input.CopyTo(ms);
				return ms.ToArray();
			}
		}
	}
}
