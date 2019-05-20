using System;
using System.Collections.Generic;
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
		//Based on https://habr.com/ru/post/120157/
		//Probably the class name needs to be changed

		/// <summary>
		/// Convert a Web 2.0 page to Web 1.0-like page.
		/// </summary>
		/// <param name="Client">TcpListener client</param>
		public Transit(TcpClient Client)
		{
			Console.Write("\n>");

			string Request = "";
			byte[] Buffer = new byte[1024];

			int Count;
			try
			{
				while ((Count = Client.GetStream().Read(Buffer, 0, Buffer.Length)) > 0)
				{
					Request += Encoding.ASCII.GetString(Buffer, 0, Count);
					// End parsing the request on a "\r\n\r\n" sequency or at 10th megabyte.
					if (Request.IndexOf("\r\n\r\n") >= 0 || Request.Length > 1024 * 1024 * 10)
					{
						break;
					}
				}
			}catch(System.IO.IOException ioe) {
				Console.WriteLine("Can't read from client: " + ioe.ToString());
				SendError(Client,500);
				return;
			}

			// Парсим строку запроса с использованием регулярных выражений
			// При этом отсекаем все переменные GET-запроса
			//Match ReqMatch = Regex.Match(Request, @"^\w+\s+([^\s\?]+)[^\s]*\s+HTTP/.*|");
			Match ReqMatch = Regex.Match(Request, @"^\w+\s+([^\s]+)[^\s]*\s+HTTP/.*|");

			if (ReqMatch == Match.Empty)
			{
				//If the request seems to be invalid, raise HTTP 400 error.
				SendError(Client, 400);
				return;
			}

			string RequestUri = ReqMatch.Groups[1].Value;
			if (RequestUri.StartsWith("/")) RequestUri = RequestUri.Substring(1);

			// Приводим ее к изначальному виду, преобразуя экранированные символы
			// Например, "%20" -> " "
			//RequestUri = Uri.UnescapeDataString(RequestUri);
			Console.Write(" " + RequestUri + " ");


			//SendError(Client, 200);


			HTTPC https = new HTTPC();
			string Html = ":(";
			string ContentType = "text/html";

			bool StWrong = false; //break operation if something is wrong.
			Console.Write("Try to get");
			
			try
			{
				//try to get...
				HttpResponse response = https.GET(RequestUri, new CookieContainer());
				Console.Write("...");
				Console.Write(response.StatusCode);
				Console.Write("...");
				var body = response.Content;
				Console.WriteLine("Body {0}K of {1}", body.Length / 1024, ContentType);
				if (response.ContentType.StartsWith("text"))
					Html = ProcessBody(response.Content);
				else
					Html = response.Content;
			} catch (WebException wex) {
				Html = "Cannot load this page: " + wex.Status.ToString() + "<br><i>" + wex.ToString().Replace("\n", "<br>") + "</i>";
				Console.WriteLine("Failed.");
			}
			catch (UriFormatException)
			{ 
				StWrong = true;
				SendError(Client, 400, "The URL <b>" + RequestUri + "</b> is not valid.");
			}

			try
			{
				//try to return...
				if (!StWrong)
				{
					string Str = "HTTP/1.0 200\nContent-type: " + ContentType + "\nContent-Length:" + Html.Length.ToString() + "\n\n" + Html;
					byte[] RespBuffer = Encoding.UTF8.GetBytes(Str);
					Client.GetStream().Write(RespBuffer, 0, RespBuffer.Length);
					Client.Close();
				}
			}catch (Exception ex) {
				Console.WriteLine("Cannot return reply to the client. " + ex.Message);
			}

			Console.WriteLine("The client is served.");
		}

		/// <summary>
		/// Process the reply's body and fix too modern stuff
		/// </summary>
		/// <param name="Body">The original body</param>
		/// <returns>The fixed body, compatible with old browsers</returns>
		private string ProcessBody(string Body) {
			Body = Body.Replace("https", "http");
			//Body = Encoding.Convert(Encoding.UTF8, Encoding.GetEncoding(1251), Encoding.UTF8.GetBytes(Body)).ToString();
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
			Text += "<hr>WebOne Proxy Server<br>on " + Environment.OSVersion.VersionString;
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
