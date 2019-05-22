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
		public static string LastURL = "http://999.999.999.999/CON";//dirty workaround for phantom.sannata.org and similar sites

		byte[] Buffer = new byte[10485760]; //todo: adjust for real request size
		string Request = "";
		string RequestHeaders = "";
		string RequestBody = "";
		int RequestHeadersEnd;

		string ResponseHeaders = "HTTP/1.0 200\n";
		string ResponseBody = ":(";
		byte[] ResponseBuffer;
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
			try
			{
				//get request
				NetworkStream ClientStream = Client.GetStream();

				while (ClientStream.DataAvailable)
				{
					ClientStream.Read(Buffer, Count, 1);
					Count++;

				}
				Request += Encoding.ASCII.GetString(Buffer).Trim('\0'); //cut empty 10 megabytes

				if (Request.Length == 0) { SendError(Client, 400); return; }

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
				SendError(Client, 400);
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
			}

			//fix "carousels"
			string RefererUri = RequestHeaderCollection["Referer"];
			string RequestUri = ReqMatch.Groups[1].Value;

			if (RequestUri.StartsWith("/"))
			{
				if (RequestUri.StartsWith("/http:") || RequestUri.StartsWith("/https:"))
					RequestUri = RequestUri.Substring(1); //debug mode: http://localhost:80/http://example.com/indexr.shtml
				else {
					//bad debug mode, try to use last used host: http://localhost:80/favicon.ico
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
				RequestUri = "https" + RequestUri.Substring(4);
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
				HttpResponse response;
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
						if(header != "Content-Length")
						ResponseHeaders += (header + ": " + value + "\n");
						//Console.WriteLine(header + ": " + value);
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
					ResponseHeaders = "HTTP/1.0 200\nContent-type: " + ContentType + "\nContent-Length:" + ResponseBody.Length.ToString() + ResponseHeaders;
					byte[] RespBuffer = Encoding.Default.GetBytes(ResponseHeaders + "\n");
					RespBuffer = RespBuffer.Concat(ResponseBuffer ?? Encoding.Default.GetBytes(ResponseBody)).ToArray();
					Client.GetStream().Write(RespBuffer, 0, RespBuffer.Length);
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
			ResponseBuffer = Encoding.Default.GetBytes(ResponseBody);
			ContentType = response.ContentType;
			Console.Write("Body {0}K of {1}", ResponseBody.Length / 1024, ContentType);
			if (ContentType.Contains("utf-8")) ContentType = ContentType.Substring(0, ContentType.IndexOf(';'));
			if (response.ContentType.StartsWith("text") ||
			response.ContentType.Contains("javascript") || 
			response.ContentType.Contains("json") ||
			response.ContentType.Contains("cdf") ||
			response.ContentType.Contains("xml"))
			{
				//если сервер вернул текст, сделать правки, иначе прогнать как есть дальше
				ResponseBody = ProcessBody(ResponseBody);
				ResponseBuffer = Encoding.Default.GetBytes(ResponseBody);
			}else {
				Console.Write("[Binary]");
				ResponseBuffer = response.RawContent;
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
			Body = Encoding.Default.GetString(Encoding.Convert(Encoding.UTF8, Encoding.Default, Encoding.UTF8.GetBytes(Body)));
			return Body;
		}

		/// <summary>
		/// Send a HTTP error to client
		/// </summary>
		/// <param name="Client">TcpListener client</param>
		/// <param name="Code">Error code number</param>
		/// <param name="Text">Error description for user</param>
		private void SendError(TcpClient Client, int Code, string Text = "")
		{
			Text += Program.GetInfoString();
			string CodeStr = Code.ToString() + " " + ((HttpStatusCode)Code).ToString();
			string Html = "<html><body><h1>" + CodeStr + "</h1>"+Text+"</body></html>";
			string Str = "HTTP/1.0 " + CodeStr + "\nContent-type: text/html\nContent-Length:" + Html.Length.ToString() + "\n\n" + Html;
			byte[] Buffer = Encoding.ASCII.GetBytes(Str);
			try
			{
				Client.GetStream().Write(Buffer, 0, Buffer.Length);
				Client.Close();
			}
			catch {
				Console.WriteLine("Cannot return HTTP error " + Code);
			}
		}
	}
}
