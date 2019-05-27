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
		bool LocalMode = false;

		HttpResponse response;
		string ResponseHeaders;
		string ResponseBody = ":(";
		byte[] ResponseBuffer;
		Stream TransitStream = null;
		string ContentType = "text/html";

		//Based on https://habr.com/ru/post/120157/
		//Probably the class name needs to be changed

		/// <summary>
		/// Convert a Web 2.0 page to Web 1.0-like page.
		/// </summary>
		/// <param name="Client">TcpListener client</param>
		public Transit(TcpClient Client)
		{
			Console.Write("\n>");

			int Count = 0;
			NetworkStream ClientStream;
			try
			{
				//get request
				ClientStream = Client.GetStream();

				for(int i = 0; i < ConfigFile.SlowClientHack; i++) { Console.CursorLeft = Console.CursorLeft; } //wait for slow clients

				while (ClientStream.DataAvailable)
				{
					ClientStream.Read(Buffer, Count, 1);
					Count++;

				}
				Request += Encoding.ASCII.GetString(Buffer).Trim('\0'); //cut empty 10 megabytes

				//check if the request is empty and report to try again.
				if (Request.Length == 0) { Console.Write(" Empty request."); SendError(Client, 302, "The request wasn't heard. Please try again.","\nLocation: "); return; }

				RequestHeadersEnd = Request.IndexOf("\r\n\r\n");
				RequestHeaders = Request.Substring(0, RequestHeadersEnd);
				RequestBody = Request.Substring(RequestHeadersEnd + 4);

				/*Console.Write("-{0} of {1}-", Request.IndexOf("\r\n\r\n"), Request.Length);
				Console.Write("-POST body={0}-", Request.Substring(Request.IndexOf("\r\n\r\n")).Trim("\r\n\r\n".ToCharArray()));*/
			}
			catch (System.IO.IOException ioe) {
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
					RequestHeaderCollection.Add(hdr);
				}
				//if (hdr.Contains("ookie")) Console.WriteLine("Sent cookie: " + hdr);
			}

			//fix "carousels"
			string RefererUri = RequestHeaderCollection["Referer"];
			string RequestUri = ReqMatch.Groups[1].Value;

			//check for local or internal URL
			if (RequestUri.StartsWith("/"))
			{
				if(RequestUri.StartsWith("/!")) {
					//internal URLs
					Console.Write("Internal: " + RequestUri);
					switch(RequestUri.Substring(2,RequestUri.Length - 3)) {
						case "codepages":
							string codepages = "The following codepages are available: <br>\n" +
							                   "<table><tr><td><b>Name</b></td><td><b>#</b></td><td><b>Description</b></td></tr>\n";
							foreach(EncodingInfo cp in Encoding.GetEncodings()) {
								if(Encoding.Default.EncodingName.Contains(" ") && cp.DisplayName.Contains(Encoding.Default.EncodingName.Substring(0, Encoding.Default.EncodingName.IndexOf(" "))))
								codepages += "<tr><td><b><u>" + cp.Name + "</u></b></td><td><u>" + cp.CodePage + "</u></td><td><u>" + cp.DisplayName + (cp.CodePage == Encoding.Default.CodePage ? "</u> (<i>system default</i>)" : "</u>") + "</td></tr>\n";
								else
								codepages += "<tr><td><b>" + cp.Name + "</b></td><td>" + cp.CodePage + "</td><td>" + cp.DisplayName + "</td></tr>\n";
							}
							codepages += "</table><br>Use any of these. Underlined are for your locale.";
							SendError(Client, 200, codepages);
							break;
						default:
							SendError(Client, 200, "Unknown internal URL: " + RequestUri.Substring(2,RequestUri.Length - 3));
							break;
					}
					return;
				}
				Console.Write("Local:");
				LocalMode = true;
				if (RequestUri.StartsWith("/http:") || RequestUri.StartsWith("/https:"))
					RequestUri = RequestUri.Substring(1); //local mode: http://localhost:80/http://example.com/indexr.shtml
				else {
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

			//dirty workarounds for HTTP>HTTPS redirection bugs
			if ((RequestUri == RefererUri || RequestUri == LastURL) && RequestUri != "")
			{
				Console.Write("Carousel");
				if(!LastURL.StartsWith("https") && !RequestUri.StartsWith("https")) //if http is gone, try https
					RequestUri = "https" + RequestUri.Substring(4);
				if (LastURL.StartsWith("https")) //if can't use https, try again http
					RequestUri = "http" + RequestUri.Substring(4);
			}

			Console.Write(" " + RequestUri + " ");
			LastURL = RequestUri;

			//make reply
			//SendError(Client, 200);

			HTTPC https = new HTTPC();
			bool StWrong = false; //break operation if something is wrong.
			Console.Write("Try to " + RequestMethod.ToLower());

			if (RequestUri.Contains("??")) { StWrong = true; SendError(Client, 400, "Too many questions."); }
			if (RequestUri.Length == 0) { StWrong = true; SendError(Client, 400, "Empty URL."); }

			try
			{
				switch (RequestMethod) {
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
					default:
						SendError(Client, 405, "The proxy does not know the " + RequestMethod + " method.");
						Console.WriteLine(" Wrong method.");
						return;
				}

				//ResponseHeaders = "HTTP/1.0 200\n";
				for (int i = 0; i < response.Headers.Count; ++i)
				{
					string header = response.Headers.GetKey(i);
					foreach (string value in response.Headers.GetValues(i))
					{
						if (!header.StartsWith("Content-"))
						{
							ResponseHeaders += (header + ": " + value.Replace("; secure", "").Replace("no-cache=\"set-cookie\"", "") + "\n");
							//Console.WriteLine(header + ": " + value.Replace("; secure", "").Replace("no-cache=\"set-cookie\"", ""));
							//if (header.Contains("ookie")) Console.WriteLine("Got cookie: " + value);
						}
					}
				}
				

			} catch (WebException wex) {
				ResponseBody = "Cannot load this page: " + wex.Status.ToString() + "<br><i>" + wex.ToString().Replace("\n", "<br>") + "</i><br>URL: " + RequestUri + Program.GetInfoString();
				Console.WriteLine("Failed.");
			}
			catch (UriFormatException)
			{ 
				StWrong = true;
				SendError(Client, 400, "The URL <b>" + RequestUri + "</b> is not valid.");
			}
			catch(Exception ex) {
				StWrong = true;
				Console.WriteLine("============GURU MEDITATION:\n{0}\nOn URL '{1}', Method '{2}'. Returning 500.============", ex.ToString(), RequestUri, RequestMethod);
				SendError(Client, 500, "Guru meditaion at URL " + RequestUri + ":<br><b>" + ex.Message + "</b><br><i>" + ex.StackTrace + "</i>" + Program.GetInfoString());
			}

			try
			{
			//try to return...
				if (!StWrong)
				{
					ResponseHeaders = "HTTP/1.0 200\n" + ResponseHeaders + "Content-Type: " + ContentType + "\nContent-Length: " + ResponseBody.Length;
					byte[] RespBuffer = ConfigFile.OutputEncoding.GetBytes(ResponseHeaders + "\n\n");
					if (TransitStream == null)
					{
						RespBuffer = RespBuffer.Concat(ResponseBuffer ?? ConfigFile.OutputEncoding.GetBytes(ResponseBody)).ToArray();
						ClientStream.Write(RespBuffer, 0, RespBuffer.Length);
					}
					else
					{
						RespBuffer = ResponseBuffer;
						ClientStream.Write(RespBuffer, 0, RespBuffer.Length);
						TransitStream.CopyTo(ClientStream);
					}
					Client.Close();
				}
			}catch (Exception ex) {
				Console.WriteLine("Cannot return reply to the client. " + ex.Message);
			}

			Console.WriteLine("The client is served.");
		}

		/// <summary>
		/// Make reply's body as byte array
		/// </summary>
		/// <param name="response">The HTTP Response</param>
		/// <returns>Array of bytes (the body)</returns>
		private byte[] MakeOutput(HttpResponse response) {
			Console.Write("...");
			Console.Write(response.StatusCode);
			Console.Write("...");
			ResponseBody = response.Content;
			ResponseBuffer = ConfigFile.OutputEncoding.GetBytes(ResponseBody);
			ContentType = response.ContentType;
			Console.Write("Body {0}K of {1}", ResponseBody.Length / 1024, ContentType);
			if (ContentType.ToLower().Contains("utf-8")) ContentType = ContentType.Substring(0, ContentType.IndexOf(';'));
			if (Program.CheckString(ContentType, ConfigFile.TextTypes))
			{
				//если сервер вернул текст, сделать правки, иначе прогнать как есть дальше
				//ContentType = ContentType.ToLower().Replace("utf-8","windows-"+ConfigFile.OutputEncoding.CodePage);
				ResponseBody = ProcessBody(ResponseBody);
				ResponseBuffer = ConfigFile.OutputEncoding.GetBytes(ResponseBody);
			}
			else
			{
				Console.Write("[Binary]");
				//ResponseBuffer = response.RawContent;
				TransitStream = response.Stream;
			}
			Console.WriteLine(".");
			return ResponseBuffer;
		}

		/// <summary>
		/// Process the reply's body and fix too modern stuff
		/// </summary>
		/// <param name="Body">The original body</param>
		/// <returns>The fixed body, compatible with old browsers</returns>
		private string ProcessBody(string Body) {
			Body = Body.Replace("https", "http");
			if(LocalMode) Body = Body.Replace("http://", "http://localhost/http://");//replace with real hostname
			Body = Body.Replace("harset=\"utf-8\"", "harset=\"" + ConfigFile.OutputEncoding.WebName + "\"");
			Body = Body.Replace("harset=\"UTF-8\"", "harset=\"" + ConfigFile.OutputEncoding.WebName + "\"");
			Body = Body.Replace("harset=utf-8", "harset=" + ConfigFile.OutputEncoding.WebName);
			Body = Body.Replace("harset=UTF-8", "harset=" + ConfigFile.OutputEncoding.WebName);
			Body = Body.Replace("CHARSET=UTF-8", "CHARSET=" + ConfigFile.OutputEncoding.WebName);
			Body = Body.Replace(ConfigFile.OutputEncoding.GetString(UTF8BOM), "BOM");
			Body = ConfigFile.OutputEncoding.GetString(Encoding.Convert(Encoding.UTF8, ConfigFile.OutputEncoding, Encoding.UTF8.GetBytes(Body)));
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
			Console.Write("["+Code+"]");
			Text += Program.GetInfoString();
			string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
			string Refresh = "<META HTTP-EQUIV=REFRESH CONTENT=0>";
			if (Code != 302 || ExtraHeaders.StartsWith("Refresh:")) Refresh = "";
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
