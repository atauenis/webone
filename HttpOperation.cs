using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace WebOne
{
	/// <summary>
	/// HTTP download/upload operation client
	/// </summary>
	class HttpOperation
	{
		private LogWriter Log;

		/// <summary>
		/// Uniform Resource Locator of content which should be accessed on the HTTP operation
		/// </summary>
		internal Uri URL { get; set; }
		/// <summary>
		/// HTTP Method, used on this operation
		/// </summary>
		internal string Method { get; set; }

		/// <summary>
		/// Full HTTP client request
		/// </summary>
		internal HttpRequestMessage Request { get; set; }
		/// <summary>
		/// HTTP request headers
		/// </summary>
		internal WebHeaderCollection RequestHeaders { get; set; }
		/// <summary>
		/// HTTP request body (if any)
		/// </summary>
		internal Stream RequestStream { get; set; }

		/// <summary>
		/// Full HTTP server response
		/// </summary>
		internal HttpResponseMessage Response { get; private set; }
		/// <summary>
		/// HTTP response headers
		/// </summary>
		internal WebHeaderCollection ResponseHeaders { get; private set; }
		/// <summary>
		/// HTTP response body (if any)
		/// </summary>
		internal Stream ResponseStream { get; private set; }


		/// <summary>
		/// Create HTTP operation client instance
		/// </summary>
		/// <param name="Log">Log writer for this operation</param>
		internal HttpOperation(LogWriter Log)
		{
			this.Log = Log;
		}

		/// <summary>
		/// Perform a HTTP request (content retreive or upload) on this operation
		/// </summary>
		internal void SendRequest()
		{
			Request = new HttpRequestMessage();
			Request.RequestUri = URL;
			Request.Method = new HttpMethod(Method);

			if(ConfigFile.RemoteHttpVersion == "auto")
			{
				if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Major < 10)
				{
					Request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
					Request.Version = new Version(1, 1);
					//HTTP/2 in .NET 6 works only on Linux, macOS and Windows 10+ (partial support on Win8+ too)
				}
				else
				{
					Request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
					Request.Version = new Version(2, 0);
					//Why not HTTP/3? Its support is limited in .NET 6: https://devblogs.microsoft.com/dotnet/http-3-support-in-dotnet-6/
				}
			}
			else
			{
				Request.Version = new Version(ConfigFile.RemoteHttpVersion[1..]);
				switch(ConfigFile.RemoteHttpVersion[0])
				{
					case '=':
						Request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
						break;
					case '>':
						Request.VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
						break;
					case '<':
						Request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
						break;
					default:
						throw new ArgumentException("Bad RemoteHttpVersion option in configuration file");
				}
			}
			
			foreach (var rqhdr in RequestHeaders.AllKeys)
			{
				if (!rqhdr.StartsWith("Proxy-") &&
				rqhdr != "Host" && 
				rqhdr != "Content-Encoding" &&
				rqhdr != "Accept-Encoding")
					Request.Headers.TryAddWithoutValidation(rqhdr, RequestHeaders[rqhdr]);
			}
			if (RequestStream != null)
			{
				Request.Content = new StreamContent(RequestStream);
				if (RequestHeaders["Content-Length"] != null) Request.Content.Headers.TryAddWithoutValidation("Content-Length", RequestHeaders["Content-Length"]);
				if (RequestHeaders["Content-Type"] != null) Request.Content.Headers.TryAddWithoutValidation("Content-Type", RequestHeaders["Content-Type"]);
				if (RequestHeaders["Content-Disposition"] != null) Request.Content.Headers.TryAddWithoutValidation("Content-Disposition", RequestHeaders["Content-Disposition"]);
				if (RequestHeaders["Content-Location"] != null) Request.Content.Headers.TryAddWithoutValidation("Content-Location", RequestHeaders["Content-Location"]);
				if (RequestHeaders["Content-Range"] != null) Request.Content.Headers.TryAddWithoutValidation("Content-Range", RequestHeaders["Content-Range"]);
			}

			foreach (string SslHost in ConfigFile.ForceHttps)
			{
				if (Request.RequestUri.Host.StartsWith(SslHost))
				{
					UriBuilder ub = new(Request.RequestUri);
					ub.Scheme = "https";
					if (ub.Port == 80) ub.Port = 443;
					Request.RequestUri = ub.Uri;
					URL = ub.Uri;
#if DEBUG
					Log.WriteLine(" Willfully secure request.");
#endif
				}
			}

			try
			{
				var resp = Program.HTTPClient.SendAsync(Request);
				resp.Wait();
				Response = resp.Result;
				//Response = Program.HTTPClient.Send(Request);
			}
			catch (AggregateException ex)
			{
				throw ex.InnerException;
			}
			ResponseHeaders = null;
			ResponseStream = null;
		}

		/// <summary>
		/// Retreive server response on HTTP request of this operation
		/// </summary>
		internal void GetResponse()
		{
			if (Response == null) throw new InvalidOperationException("HTTP request must be sent and be successful first.");

			ResponseHeaders = new WebHeaderCollection();
			foreach (var rshdr in Response.Headers)
			{
				foreach (var rshdrval in rshdr.Value)
				{
					ResponseHeaders.Add(rshdr.Key, rshdrval);
				}
			}

			foreach (var rshdr in Response.Content.Headers)
			{
				foreach (var rshdrval in rshdr.Value)
				{
					ResponseHeaders.Add(rshdr.Key, rshdrval);
				}
			}

			foreach (var rshdr in Response.TrailingHeaders)
			{
				foreach (var rshdrval in rshdr.Value)
				{
					ResponseHeaders.Add(rshdr.Key, rshdrval);
				}
			}

			ResponseStream = Response.Content.ReadAsStream();
		}
	}

	/// <summary>
	/// An TLS/SSL error representation
	/// </summary>
	internal class TlsPolicyErrorException : Exception
	{
		public new string Message;
		public SslStream ProblematicStream;
		public X509Certificate Certificate;
		public X509Chain Chain;
		public SslPolicyErrors PolicyError;

		public TlsPolicyErrorException(SslStream ProblematicStream, X509Certificate Certificate, X509Chain Chain, SslPolicyErrors PolicyError)
		{
			this.ProblematicStream = ProblematicStream;
			this.Certificate = Certificate;
			this.Chain = Chain;
			this.PolicyError = PolicyError;
			this.Message = "TLS Policy Error(s): " + PolicyError.ToString();
		}
	}
}
