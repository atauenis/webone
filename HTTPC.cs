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
	class HTTPC
	{
		//based on http://www.cyberforum.ru/post8143282.html

		private const string UA_Mozilla = "Mozilla/5.0 (Windows NT 4.0; WOW64; rv:99.0) Gecko/20100101 Firefox/99.0";
		private string[] HeaderBanList = { "Proxy-Connection", "User-Agent", "Host", "Accept", "Referer", "Connection", "Content-type", "Content-length", "If-Modified-Since", "Accept-Encoding", "Accept-Charset", "Date" };
		HttpWebResponse webResponse = null;

		~HTTPC() {
			if (webResponse != null) webResponse.Close();
		}

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
		public HttpResponse GET(string Host, CookieContainer CC, WebHeaderCollection Headers, string Method, bool AllowAutoRedirect, DateTime BeginTime)
		{
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
				string UA = Headers["User-Agent"] + " WebOne/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
				string Accept = Headers["Accept"];
				string Referer = Headers["Referer"];

				foreach (string str in HeaderBanList) { Headers.Remove(str); }
				webRequest.Headers = Headers;

				webRequest.Accept = Accept ?? "*/*";
				webRequest.UserAgent = UA ?? UA_Mozilla;
				if(Referer != null) webRequest.Referer = Referer;
				webRequest.Method = Method;
				webRequest.AllowAutoRedirect = AllowAutoRedirect;
				webRequest.CookieContainer = CC;
				webRequest.ProtocolVersion = HttpVersion.Version11;
				webRequest.KeepAlive = true;
				webRequest.ServicePoint.Expect100Continue = false;

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
		public HttpResponse POST(string Host, CookieContainer CC, Stream BodyStream, WebHeaderCollection Headers, string Method, bool AllowAutoRedirect, DateTime BeginTime)
		{
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

				string UA = Headers["User-Agent"] + " WebOne/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
				string Accept = Headers["Accept"];
				string Referer = Headers["Referer"];
				string ContentType = Headers["Content-Type"];// ?? "application/x-www-form-urlencoded";
				long ContentLength = long.Parse(Headers["Content-Length"] ?? "0");
				
				foreach (string str in HeaderBanList) { Headers.Remove(str); }
				webRequest.Headers = Headers;

				webRequest.Accept = Accept ?? "*/*";
				webRequest.UserAgent = UA ?? UA_Mozilla;
				if (Referer != null) webRequest.Referer = Referer;
				webRequest.Method = Method;
				webRequest.AllowAutoRedirect = AllowAutoRedirect;
				webRequest.CookieContainer = CC;
				webRequest.ProtocolVersion = HttpVersion.Version11;
				webRequest.KeepAlive = true;
				webRequest.ContentType = ContentType;
				webRequest.ContentLength = ContentLength;
				webRequest.ServicePoint.Expect100Continue = false;
				/*try
				{
					webRequest.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(AcceptAllCertifications);
				}
				catch (MissingMethodException) { }*/
				webRequest.AllowAutoRedirect = Program.CheckString(Host, ConfigFile.InternalRedirectOn);

				using (var RequestStream = new StreamWriter(webRequest.GetRequestStream()))
				{
					BodyStream.CopyTo(RequestStream.BaseStream);
					RequestStream.Close();
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
			//Headers.Add("Accept-Language", "ru-RU,ru;q=0.8,en-US;q=0.5,en;q=0.3");
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
		//probably need to be thrown away
		//except Decompress function
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
