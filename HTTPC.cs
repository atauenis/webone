using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Modern HTTP(S) client.
	/// </summary>
	class HTTPC : IDisposable
	{
		//based on http://www.cyberforum.ru/post8143282.html

		private const string UA_Mozilla = "Mozilla/5.0 (Windows NT 4.0; WOW64; rv:99.0) Gecko/20100101 Firefox/99.0";
		private string[] HeaderBanList = { "Proxy-Connection", "Accept", "Connection", "Content-Length", "Content-Type", "Expect", "Date", "Host", "If-Modified-Since", "Range", "Referer", "Transfer-Encoding", "User-Agent", "Accept-Encoding", "Accept-Charset" };
		HttpWebResponse webResponse = null;
		DateTime BeginTime = new DateTime(1970, 01, 01, 00, 00, 00);

		~HTTPC() { Dispose(); }

		/// <summary>
		/// Perform a GET-like request (content retrieve)
		/// </summary>
		/// <param name="Host">URL</param>
		/// <param name="CC">Cookie container</param>
		/// <param name="Headers">HTTP headers</param>
		/// <param name="Method">HTTP method (GET by default)</param>
		/// <param name="AllowAutoRedirect">Allow 302 redirection handling in .NET FW or not</param>
		/// <param name="BeginTime">Initial time (for log)</param>
		/// <returns>Server's response.</returns>
		public HttpOperation GET(string Host, CookieContainer CC, WebHeaderCollection Headers, string Method, bool AllowAutoRedirect, DateTime BeginTime)
		{
			this.BeginTime = BeginTime;
			HttpWebRequest webRequest = null;
			try
			{
				foreach (string SslHost in ConfigFile.ForceHttps)
				{
					if (Host.Substring(7).StartsWith(SslHost))
					{
						Host = "https" + Host.Substring(4);
#if DEBUG
						Console.WriteLine("{0}\t Willfully secure request.", GetTime(BeginTime));
#endif
					}
				}

				webRequest = (HttpWebRequest)WebRequest.Create(Host);
				AddHeaders(Headers, webRequest);
				webRequest.ServicePoint.ConnectionLimit = int.MaxValue;

				webRequest.Method = Method;
				webRequest.AllowAutoRedirect = AllowAutoRedirect;
				webRequest.CookieContainer = CC;
				webRequest.ProtocolVersion = HttpVersion.Version11;
				webRequest.KeepAlive = true;
				webRequest.ServicePoint.Expect100Continue = false;
				webRequest.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(CheckServerCertificate);

				webResponse = (HttpWebResponse)webRequest.GetResponse();
				return new HttpOperation(webResponse, webRequest);
			}
			catch (WebException ex)
			{
				if(ex.Response == null) throw;
				return new HttpOperation((HttpWebResponse)ex.Response, webRequest);
			}
		}


		/// <summary>
		/// Perform a POST-like request (content upload)
		/// </summary>
		/// <param name="Host">URL</param>
		/// <param name="CC">Cookie Container</param>
		/// <param name="BodyStream">Stream of request's body</param>
		/// <param name="Headers">HTTP headers</param>
		/// <param name="Method">HTTP method (POST by default)</param>
		/// <param name="AllowAutoRedirect">Allow 302 redirection handling in .NET FW or not</param>
		/// <param name="BeginTime">Initial time (for log)</param>
		/// <returns></returns>
		public HttpOperation POST(string Host, CookieContainer CC, Stream BodyStream, WebHeaderCollection Headers, string Method, bool AllowAutoRedirect, DateTime BeginTime)
		{
			this.BeginTime = BeginTime;
			try
			{
				foreach (string SslHost in ConfigFile.ForceHttps)
				{
					if (Host.Substring(7).StartsWith(SslHost))
					{
						Host = "https" + Host.Substring(4);
						#if DEBUG
						Console.WriteLine("{0}\t Willfully secure request.", GetTime(BeginTime));
						#endif
					}
				}

				HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(Host);
				AddHeaders(Headers, webRequest);
				webRequest.ServicePoint.ConnectionLimit = int.MaxValue;

				webRequest.Method = Method;
				webRequest.AllowAutoRedirect = AllowAutoRedirect;
				webRequest.CookieContainer = CC;
				webRequest.ProtocolVersion = HttpVersion.Version11;
				webRequest.KeepAlive = true;
				webRequest.ServicePoint.Expect100Continue = false;
				webRequest.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(CheckServerCertificate);

				using (var RequestStream = new StreamWriter(webRequest.GetRequestStream()))
				{
					BodyStream.CopyTo(RequestStream.BaseStream);
					RequestStream.Close();
				}

				webResponse = (HttpWebResponse)webRequest.GetResponse();
				return new HttpOperation(webResponse, webRequest);
			}
			catch (Exception)
			{
				throw;
			}
		}
		

		/// <summary>
		/// Check remote HTTPS server certificate
		/// </summary>
		/// <returns></returns>
		public bool CheckServerCertificate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certification, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
		{
			if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
				Console.WriteLine("{0}\t Danger: {1}", GetTime(BeginTime), sslPolicyErrors.ToString());

			if (!ConfigFile.ValidateCertificates) return true;
			if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
				return true;
			throw new Exception("TLS Policy Error(s): " + sslPolicyErrors.ToString());
		}


		/// <summary>
		/// Readd non-easy headers to request
		/// </summary>
		/// <param name="Headers">Raw request header collection</param>
		/// <param name="HWR">HttpWebRequest object</param>
		void AddHeaders(WebHeaderCollection Headers, HttpWebRequest HWR)
		{
			//see https://docs.microsoft.com/en-us/dotnet/api/system.net.httpwebrequest.headers?view=netframework-4.6#remarks
			string UA = GetUserAgent(Headers["User-Agent"]);
			string Referer = Headers["Referer"];
			string Accept = Headers["Accept"];
			string ContentLength = Headers["Content-Length"];
			string ContentType = Headers["Content-Type"];
			string Expect = Headers["Expect"];
			string Date = Headers["Date"];
			string IfModifiedSince = Headers["If-Modified-Since"];
			string Range = Headers["Range"];
			
			foreach (string str in HeaderBanList) { Headers.Remove(str); }
			HWR.Headers = Headers;

			HWR.UserAgent = UA ?? UA_Mozilla;
			if (Referer != null) HWR.Referer = Referer;
			HWR.Accept = Accept ?? "*/*";
			if (ContentLength != null) HWR.ContentLength = long.Parse(ContentLength);
			if (ContentType != null) HWR.ContentType = ContentType;
			if (Expect != null) HWR.Expect = Expect;
			if (Date != null) HWR.Date = DateTime.Parse(Date);
			DateTime IfModifiedSinceDT;
			if (IfModifiedSince != null && DateTime.TryParse(IfModifiedSince, out IfModifiedSinceDT)) HWR.IfModifiedSince = IfModifiedSinceDT;
			if (Range != null) AddRangeHeader(Range, HWR);
		}

		/// <summary>
		/// Process Range header for AddHeaders()
		/// </summary>
		/// <param name="RangeHeader">Range header string</param>
		/// <param name="HWR">HttpWebRequest object</param>
		void AddRangeHeader(string RangeHeader, HttpWebRequest HWR)
		{
			//see https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Range
			string Unit = RangeHeader.Substring(0, RangeHeader.IndexOf("="));
			string[] Ranges = RangeHeader.Substring(RangeHeader.IndexOf("=") + 1).Split(',');
			foreach (string RangeString in Ranges)
			{
				string[] FromTo = RangeString.Replace(" ", "").Split('-');
				if (FromTo.Length != 2) return;
				if (FromTo[0] == "") // bytes=-99
				{
					HWR.AddRange(Unit, 0 - int.Parse(FromTo[1]));
					return;
				}
				if (FromTo[1] == "") // bytes=99-
				{
					HWR.AddRange(Unit, int.Parse(FromTo[0]));
					return;
				}
				// bytes=100-200
				HWR.AddRange(Unit, int.Parse(FromTo[0]), int.Parse(FromTo[1]));
			}
		}

		public void Dispose()
		{
			#if DEBUG
				Console.WriteLine("{0}\t Destruct HTTPC.", GetTime(BeginTime));
			#endif
			if (webResponse != null) webResponse.Close();
			if (webResponse != null) webResponse.Dispose();
		}
	}

	/// <summary>
	/// Decoded HTTP response
	/// </summary>
	public class HttpOperation
	{
		/* Plans on future:
		 * 1. Переделать с результата работы HTTPC на контейнер для операции с участием HTTPC.
		 * 2. Сделать состояния операции: составление/отправка/ожидание/есть ответ.
		 * 3. Make disposing.
		 * 4. Научить обрабатывать ошибки HTTP 100-999, но кидаться Exception на ошибки сети.
		 */
		public HttpOperation(HttpWebResponse webResponse, HttpWebRequest webRequest)
		{
			this.Request = webRequest;
			this.Response = webResponse;

			this.ResponseHeaders = webResponse.Headers;
			if (this.ResponseHeaders["Content-Encoding"] != null) this.ResponseHeaders["Content-Encoding"] = "identity";
			this.Stream = Decompress(this.Response);
		}


		/// <summary>
		/// Source HttpWebRequest
		/// </summary>
		public HttpWebRequest Request { get; private set; }
		
		/// <summary>
		/// Source HttpWebResponse <!--(if any)-->
		/// </summary>
		public HttpWebResponse Response { get; private set; }
		
		/// <summary>
		/// Corrected response headers <!--(if any)-->
		/// </summary>
		public WebHeaderCollection ResponseHeaders { get; private set; }
		
		/// <summary>
		/// Decompressed response data stream <!--(if any)-->
		/// </summary>
		public Stream Stream { get; private set; }


		/// <summary>
		/// Get HTTP data stream in readable view (decompressed if need)
		/// </summary>
		/// <param name="webResponse">Response which containing the stream</param>
		/// <returns>Http Stream/GZipStream/DeflateStream with data</returns>
		private Stream Decompress(HttpWebResponse webResponse)
		{
			Stream responseStream = webResponse.GetResponseStream();
			if (webResponse.ContentEncoding != null)
			{
				if (webResponse.ContentEncoding.ToLower().Contains("gzip"))
					responseStream = new GZipStream(responseStream, CompressionMode.Decompress);
				else if (webResponse.ContentEncoding.ToLower().Contains("deflate"))
					responseStream = new DeflateStream(responseStream, CompressionMode.Decompress);
			}

			return responseStream;
		}
	}
}
