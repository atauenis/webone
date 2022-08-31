using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace WebOne
{
	/// <summary>
	/// HTTP download/upload operation client (new version)
	/// </summary>
	class HttpOperation
	{
		private LogWriter Log;

		//old from HTTPC
		internal string URL { get; set; }
		internal string Method { get; set; }
		internal Stream RequestStream { get; set; }
		internal Stream ResponseStream { get; private set; }


		//new v0.12.2 (httpclient net6.0)
		internal HttpRequestMessage Request { get; set; }
		internal WebHeaderCollection RequestHeaders { get; set; }
		internal HttpResponseMessage Response { get; private set; }
		internal WebHeaderCollection ResponseHeaders { get; private set; }


		/// <summary>
		/// Create HTTP operation client instance
		/// </summary>
		/// <param name="Log">Log writer for this operation</param>
		internal HttpOperation(LogWriter Log)
		{
			this.Log = Log;
	}

		internal void SendRequest()
		{
			Request = new HttpRequestMessage();
			Request.RequestUri = new Uri(URL);
			Request.Method = new HttpMethod(Method);
			foreach (var rqhdr in RequestHeaders.AllKeys)
			{
				if(!rqhdr.StartsWith("Proxy-"))
				Request.Headers.TryAddWithoutValidation(rqhdr, RequestHeaders[rqhdr]);
			}
			if(RequestStream != null) Request.Content = new StreamContent(RequestStream);

			foreach (string SslHost in ConfigFile.ForceHttps)
			{
				if (Request.RequestUri.Host.StartsWith(SslHost))
				{
					UriBuilder ub = new(Request.RequestUri);
					ub.Scheme = "https";
					if (ub.Port == 80) ub.Port = 443;
					Request.RequestUri = ub.Uri;
					URL = ub.ToString();
#if DEBUG
					Log.WriteLine(" Willfully secure request.");
#endif
				}
			}

			Response = Program.HTTPClient.Send(Request);
			ResponseHeaders = null;
			ResponseStream = null;
		}

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
