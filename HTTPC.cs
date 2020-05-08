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
	/// HTTP download/upload operation client
	/// </summary>
	public class HttpOperation : IDisposable
	{
		private readonly string UA_Mozilla = "Mozilla/5.0 (Windows NT 4.0; WOW64; rv:99.0) Gecko/20100101 Firefox/99.0";
		private readonly string[] HeaderBanList = 
		{
			"Proxy-Connection", 
			"Accept", 
			"Connection", 
			"Content-Length", 
			"Content-Type", 
			"Expect", 
			"Date", 
			"Host", 
			"If-Modified-Since", 
			"Range", 
			"Referer", 
			"Transfer-Encoding", 
			"User-Agent", 
			"Accept-Encoding", 
			"Accept-Charset" 
		};

		private LogWriter Log;

		/// <summary>
		/// Prepare to do an HTTP operation
		/// </summary>
		/// <param name="Log">Log writer for this operation</param>
		public HttpOperation(LogWriter Log)
		{
			this.Log = Log;
			ResetRequest();
		}

		~HttpOperation()
		{
			Dispose();
		}

		/// <summary>
		/// URL of resource to be accessed
		/// </summary>
		public string URL;

		/// <summary>
		/// HTTP Method
		/// </summary>
		public string Method;

		/// <summary>
		/// Cookie container
		/// </summary>
		public CookieContainer Cookies;

		/// <summary>
		/// Request's headers
		/// </summary>
		public WebHeaderCollection RequestHeaders;

		/// <summary>
		/// Request's data stream (raw)
		/// </summary>
		public Stream RequestStream;

		/// <summary>
		/// Allow 302 redirection handling in .NET FW or not
		/// </summary>
		public bool AllowAutoRedirect = false;

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
		public Stream ResponseStream { get; private set; }

		/// <summary>
		/// Unload current request (and response) to free resources for next request
		/// </summary>
		public void ResetRequest()
		{
			if (Response != null) Response.Close();
			if (Response != null) Response.Dispose();

			URL = null;
			Request = null;

			Response = null;
			ResponseHeaders = null;
			ResponseStream = null;
		}

		public void Dispose()
		{
#if DEBUG
			Log.WriteLine(" Destruct HttpOperation.");
#endif
			ResetRequest();
			RequestHeaders = null;
			RequestStream = null;
		}

		/// <summary>
		/// Perform a HTTP request (content retreive or upload) on this operation
		/// </summary>
		public void SendRequest()
		{
			foreach (string SslHost in ConfigFile.ForceHttps)
			{
				if (URL.Substring(7).StartsWith(SslHost))
				{
					URL = "https" + URL.Substring(4);
#if DEBUG
					Log.WriteLine(" Willfully secure request.");
#endif
				}
			}

			Request = (HttpWebRequest)WebRequest.Create(URL);
			AddHeaders(RequestHeaders, Request);
			Request.ServicePoint.ConnectionLimit = int.MaxValue;

			Request.Method = Method;
			Request.AllowAutoRedirect = AllowAutoRedirect;
			Request.CookieContainer = Cookies;
			Request.ProtocolVersion = HttpVersion.Version11;
			Request.KeepAlive = true;
			Request.ServicePoint.Expect100Continue = false;
			Request.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(CheckServerCertificate);

			if(RequestStream != null)
			{
				//if POST or upload
				using (var BodyWriter = new StreamWriter(Request.GetRequestStream()))
				{
					RequestStream.CopyTo(BodyWriter.BaseStream);
					BodyWriter.Close();
				}
			}
		}

		/// <summary>
		/// Retreive server response on active HTTP request
		/// </summary>
		public void GetResponse()
		{
			if (Request == null) throw new InvalidOperationException("The request must be sent before its response can be get");
			try
			{
				Response = (HttpWebResponse)Request.GetResponse();
				ResponseHeaders = Response.Headers;
				if (ResponseHeaders["Content-Encoding"] != null) ResponseHeaders["Content-Encoding"] = "identity";
				ResponseStream = Decompress(Response);
			}
			catch (WebException ex)
			{
				if (ex.Response == null) throw;
				Response = (HttpWebResponse)ex.Response;

				ResponseHeaders = Response.Headers;
				if (ResponseHeaders["Content-Encoding"] != null) ResponseHeaders["Content-Encoding"] = "identity";
				ResponseStream = Decompress(Response);

			}

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

		/// <summary>
		/// Check remote HTTPS server certificate
		/// </summary>
		/// <returns></returns>
		bool CheckServerCertificate(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certification, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
		{
			if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None)
				Log.WriteLine(" Danger: {0}", sslPolicyErrors.ToString());

			if (!ConfigFile.ValidateCertificates) return true;
			if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
				return true;
			throw new Exception("TLS Policy Error(s): " + sslPolicyErrors.ToString());
		}

		/// <summary>
		/// Get HTTP data stream in readable view (decompressed if need)
		/// </summary>
		/// <param name="webResponse">Response which containing the stream</param>
		/// <returns>Http Stream/GZipStream/DeflateStream with data</returns>
		Stream Decompress(HttpWebResponse webResponse)
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
