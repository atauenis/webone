using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WebOne
{
/// <summary>
/// Modern HTTP(S) client.
/// </summary>
	class HTTPC
	{
		//based on http://www.cyberforum.ru/post8143282.html

		private const string UA_Mozilla = "Mozilla/5.0 (Windows NT 4.0; WOW64; rv:99.0) Gecko/20100101 Firefox/99.0";
		private string[] HeaderBanList = { "Proxy-Connection", "User-Agent", "Host", "Accept", "Referer", "Connection", "Content-type", "Content-length", "If-Modified-Since", "Accept-Encoding", "Accept-Charset", "Date" };
		HttpWebResponse webResponse = null;


		public HTTPC()
		{

		}

		~HTTPC() {
			if (webResponse != null) webResponse.Close();
		}

		/// <summary>
		/// Perform a GET-like request
		/// </summary>
		/// <param name="host">URL</param>
		/// <param name="cc">Cookie container</param>
		/// <param name="headers">HTTP headers</param>
		/// <param name="method">HTTP method (GET by default)</param>
		/// <returns>Server's response.</returns>
		public HttpResponse GET(string host, CookieContainer cc, WebHeaderCollection headers, string method = "GET")
		{
			try
			{
				foreach(string badhost in ConfigFile.ForceHttps)
				{
					if (host.Substring(7).StartsWith(badhost))
					{
						host = "https" + host.Substring(4);
						Console.Write(" secure");
					}
				}

				HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(host);
				string UA = headers["User-Agent"] + " WebOne/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
				string Accept = headers["Accept"];
				string Referer = headers["Referer"];
				//undone: add other headers that cannot be passed directly to the webRequest.Headers

				foreach (string str in HeaderBanList) { headers.Remove(str); }
				webRequest.Headers = headers;

				webRequest.Accept = Accept ?? "*/*";
				webRequest.UserAgent = UA ?? UA_Mozilla;
				if(Referer != null) webRequest.Referer = Referer;
				webRequest.Method = method;
				webRequest.AllowAutoRedirect = true;
				webRequest.CookieContainer = cc;
				webRequest.ProtocolVersion = HttpVersion.Version11;
				webRequest.KeepAlive = true;
				webRequest.AllowAutoRedirect = false;

				webResponse = (HttpWebResponse)webRequest.GetResponse();
				return new HttpResponse(webResponse);
			}
			catch (Exception)
			{
				throw;
			}
			/*finally
			{
				if (webResponse != null)
					webResponse.Close();
			}*/

		}
		
		/// <summary>
		/// Perform a POST or POST-like request (content upload)
		/// </summary>
		/// <param name="host">URL</param>
		/// <param name="cc">Cookie Container</param>
		/// <param name="data">Raw post data</param>
		/// <param name="headers">HTTP headers</param>
		/// <param name="method">HTTP method (POST by default)</param>
		/// <returns></returns>
		//public HttpResponse POST(string host, CookieContainer cc, NameValueCollection param)
		public HttpResponse POST(string host, CookieContainer cc, string data, WebHeaderCollection headers, string method = "POST")
		{
			try
			{
				foreach (string badhost in ConfigFile.ForceHttps)
				{
					if (host.Substring(7).StartsWith(badhost))
					{
						host = "https" + host.Substring(4);
						Console.Write(" secure");
					}
				}

				HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(host);
				string UA = headers["User-Agent"] + " WebOne/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
				string Accept = headers["Accept"];
				string Referer = headers["Referer"];
				//undone: add other headers that cannot be passed directly to the webRequest.Headers

				foreach (string str in HeaderBanList) { headers.Remove(str); }
				webRequest.Headers = headers;

				webRequest.Accept = Accept;
				webRequest.UserAgent = UA;
				webRequest.Referer = Referer;
				webRequest.Method = method;
				webRequest.AllowAutoRedirect = true;
				webRequest.CookieContainer = cc;
				webRequest.ProtocolVersion = HttpVersion.Version10;
				webRequest.KeepAlive = true;
				webRequest.ContentType = "application/x-www-form-urlencoded";
				webRequest.ContentLength = data.Length;
				webRequest.ServicePoint.Expect100Continue = false;
				/*try
				{
					webRequest.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(AcceptAllCertifications);
				}
				catch (MissingMethodException) { }*/
				webRequest.AllowAutoRedirect = false;

				using (var requestStream = new StreamWriter(webRequest.GetRequestStream()))
				{
					requestStream.Write(data);
					requestStream.Close();
				}

				webResponse = (HttpWebResponse)webRequest.GetResponse();
				return new HttpResponse(webResponse);

			}
			catch (Exception)
			{
				throw;
			}
			/*finally
			{
				if (webResponse != null)
					webResponse.Close();
			}*/
		}

		/// <summary>
		/// Get default headers
		/// </summary>
		/// <returns>Accept-Language: ru-RU,ru;
		/// Accept-Encoding: gzip, deflate</returns>
		public static WebHeaderCollection GetHeader()
		{
			WebHeaderCollection Headers = new WebHeaderCollection();
			Headers = new WebHeaderCollection();
			Headers.Add("Accept-Language", "ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3");
			Headers.Add("Accept-Encoding", "gzip, deflate");
			return Headers;
		}

		public static bool AcceptAllCertifications(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certification, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
		{
			if (sslPolicyErrors != System.Net.Security.SslPolicyErrors.None) Console.WriteLine("SSL policy error " + sslPolicyErrors.ToString() + ", да и в рот оно любись.");
			return true;
		}

	}

	public class HttpResponse
	{
		public HttpResponse(HttpWebResponse webResponse)
		{
			this.CharacterSet = webResponse.CharacterSet;
			this.ContentEncoding = webResponse.ContentEncoding;
			this.ContentLength = webResponse.ContentLength;
			this.ContentType = webResponse.ContentType;
			this.Cookies = webResponse.Cookies;
			this.Headers = webResponse.Headers;
			if (this.Headers["Content-Encoding"] != null) this.Headers["Content-Encoding"] = "identity";
			//this.IsMutuallyAuthenticated = webResponse.IsMutuallyAuthenticated;
			this.LastModified = webResponse.LastModified;
			this.Method = webResponse.Method;
			this.ProtocolVersion = webResponse.ProtocolVersion;
			this.ResponseUri = webResponse.ResponseUri;
			this.Server = webResponse.Server;
			this.StatusCode = webResponse.StatusCode;
			this.StatusDescription = webResponse.StatusDescription;
			/*try
			{
				this.SupportsHeaders = webResponse.SupportsHeaders;
			}
			catch (MissingMethodException) { }*/
			this.Instance = webResponse;
			this.Stream = GetStream();
		}

		public string CharacterSet { get; private set; }
		public string ContentEncoding { get; private set; }
		public long ContentLength { get; private set; }
		public string ContentType { get; private set; }
		public CookieCollection Cookies { get; private set; }
		public WebHeaderCollection Headers { get; private set; }
		//public bool IsMutuallyAuthenticated { get; private set; }
		public DateTime LastModified { get; private set; }
		public string Method { get; private set; }
		public Version ProtocolVersion { get; private set; }
		public Uri ResponseUri { get; private set; }
		public string Server { get; private set; }
		public HttpStatusCode StatusCode { get; private set; }
		public string StatusDescription { get; private set; }
		//public bool SupportsHeaders { get; private set; }
		public HttpWebResponse Instance { get; private set; }
		private byte[] rawContent;
		public byte[] RawContent
		{
			get 
			{
				if (rawContent == null) rawContent = ReadFully(Stream);
				return rawContent;
			}
			set
			{
				rawContent = value;
			} 
		}
		public Stream Stream { get; private set; }


		private Stream GetStream() {
			return Decompress(this.Instance);
		}

		private static byte[] ReadFully(Stream input)
		{
			//can be called only once per session because NetworkStreams cannot seek or re-read again
			//Console.Write("Reading response...");
			using (MemoryStream ms = new MemoryStream())
			{
				input.CopyTo(ms);
				return ms.ToArray();
			}
		}

		private Stream Decompress(HttpWebResponse webResponse)
		{
			Stream responseStream = webResponse.GetResponseStream();
			if (webResponse.ContentEncoding.ToLower().Contains("gzip"))
				responseStream = new GZipStream(responseStream, CompressionMode.Decompress);
			else if (webResponse.ContentEncoding.ToLower().Contains("deflate"))
				responseStream = new DeflateStream(responseStream, CompressionMode.Decompress);

			return responseStream;
		}
	}
}
