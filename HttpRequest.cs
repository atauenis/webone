using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;

namespace WebOne
{
	/// <summary>
	/// Describes an incoming HTTP request
	/// </summary>
	class HttpRequest
	{
		// Based on System.Net.HttpListenerRequest

		/// <summary>
		/// Specifies the HTTP method specified by the client.
		/// </summary>
		/// <returns>A System.String that contains the method used in the request.</returns>
		public string HttpMethod { get; set; }

		/// <summary>
		/// Specifies the System.Uri object requested by the client.
		/// </summary>
		/// <returns>A System.Uri object that identifies the resource requested by the client.</returns>
		public Uri Url { get; set; }

		/// <summary>
		/// Specifies the URL information as requested by the client HTTP title.
		/// </summary>
		/// <returns>A System.String that contains the raw URL for this request.</returns>
		public string RawUrl { get; set; }

		//Url & RawUrl - possible chicken and egg problem

		/// <summary>
		/// Gets the query string included in the request.
		/// </summary>
		/// <returns>A System.Collections.Specialized.NameValueCollection object that contains the query data included in the request System.Net.HttpListenerRequest.Url.</returns>
		public NameValueCollection QueryString
		{
			get
			{
				if (Url == null) throw new ArgumentNullException(nameof(Url));
				return System.Web.HttpUtility.ParseQueryString(Url.Query);
			}
		}

		/// <summary>
		/// Specifies the HTTP version used by the requesting client.
		/// </summary>
		/// <returns>A System.Version that identifies the client's version of HTTP.</returns>
		public Version ProtocolVersion { get; set; }

		/// <summary>
		/// Specifies the HTTP version used by the requesting client in text string format (e.g. "HTTP/1.1").
		/// </summary>
		/// <returns>A System.String that identifies the client's version of HTTP.</returns>
		public string ProtocolVersionString
		{
			get { return "HTTP/" + ProtocolVersion.ToString(); }
			set
			{
				if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(value));
				if (value.Length != "HTTP/1.1".Length) throw new ArgumentException(value + "is not a HTTP protocol version", nameof(value));
				ProtocolVersion = new Version(value.Substring(5));
			}
		}

		/// <summary>
		/// Specifies the collection of header name/value pairs sent in the request.
		/// </summary>
		/// <returns>A System.Net.WebHeaderCollection that contains the HTTP headers included in the request.</returns>
		public NameValueCollection Headers { get; set; }

		/// <summary>
		/// Specifies a System.Boolean value that indicates whether the request has associated body data.
		/// </summary>
		/// <returns>true if the request has associated body data; otherwise, false.</returns>
		public bool HasEntityBody { get; set; }
		//probably need to detect using "Content-Length" or "Chunked"-info header presence

		/// <summary>
		/// Specifies a stream that contains the body data sent by the client.
		/// </summary>
		/// <returns>A readable System.IO.Stream object that contains the bytes sent by the client
		/// in the body of the request. This property returns System.IO.Stream.Null if no data is sent with the request.</returns>
		public Stream InputStream { get; set; }

		/// <summary>
		/// Specifies a System.Boolean value that indicates whether the request has been sent via Secure Sockets Layer (SSL) channel.
		/// </summary>
		/// <returns>true if the request is sent using SSL; otherwise, false.</returns>
		public bool IsSecureConnection { get; set; }
		//does this really need?

		/// <summary>
		/// Specifies the client IP address and port number from which the request originated.
		/// </summary>
		/// <returns>An System.Net.IPEndPoint that represents the IP address and port number from which the request originated.</returns>
		public IPEndPoint RemoteEndPoint { get; set; }

		/// <summary>
		/// Specifies the server IP address and port number to which the request is directed.
		/// </summary>
		/// <returns>An System.Net.IPEndPoint that represents the IP address that the request is sent to.</returns>
		public IPEndPoint LocalEndPoint { get; set; }

		//SSL stuff skipped as difficult to implement now
	}
}
