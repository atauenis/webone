using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace WebOne
{
	/// <summary>
	/// Describes an response to an incoming HTTP request
	/// </summary>
	class HttpResponse
	{
		// Based on System.Net.HttpListenerResponse

		// Important differences:
		// HttpListenerResponse - properties can be set once, and gets sent just after set
		// HttpResponse - all properties can be set multiple times, but sent after command

		/// <summary>
		/// Gets or sets the HTTP status code to be returned to the client.
		/// </summary>
		/// <returns>An System.Int32 value that specifies the HTTP status code for the requested resource.
		/// The default is System.Net.HttpStatusCode.OK, indicating that the server successfully
		/// processed the client's request and included the requested resource in the response body.
		/// </returns>
		/// <exception cref="T:System.Net.ProtocolViolationException">The value specified for a set operation is not valid. Valid values are between 100 and 999 inclusive.</exception>
		public int StatusCode { get; set; }

		/// <summary>
		/// Gets or sets the HTTP version used for the response.
		/// </summary>
		/// <returns>A System.Version object indicating the version of HTTP used when responding to the client.</returns>
		public Version ProtocolVersion { get; set; }

		/// <summary>
		/// Gets or sets the HTTP version used by the requesting client in text string format (e.g. "HTTP/1.1").
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
		/// Gets or sets the collection of header name/value pairs returned by the server.
		/// </summary>
		/// <returns>A System.Net.WebHeaderCollection instance that contains all the explicitly set HTTP headers to be included in the response.</returns>
		public WebHeaderCollection Headers { get; set; }

		/*/// <summary>
		/// Gets or sets a value indicating whether the server requests a persistent connection.
		/// </summary>
		/// <returns>true if the server requests a persistent connection; otherwise, false. The default is true.</returns>
		public bool KeepAlive { get; set; }
		//header or TCP/IP option?*/

		/// <summary>
		/// Gets or sets the number of bytes in the body data included in the response.
		/// </summary>
		/// <returns>The value of the response's Content-Length header.</returns>
		public long ContentLength64
		{
			get => contentLength64;
			set
			{
				contentLength64 = value;
				if (MshttpapiBackend != null) MshttpapiBackend.ContentLength64 = contentLength64;

				if (Headers["Content-Length"] == null)
					AddHeader("Content-Length", contentLength64.ToString());
				else
					Headers["Content-Length"] = contentLength64.ToString();
			}

			//think about chunked transfers with unknown content length
		}

		/// <summary>
		/// Gets or sets the MIME type of the content returned.
		/// </summary>
		/// <returns>A System.String instance that contains the text of the response's Content-Type header.</returns>
		public string ContentType
		{
			get => contentType;
			set
			{
				contentType = value;
				if (MshttpapiBackend != null) MshttpapiBackend.ContentType = contentType;

				if (Headers["Content-Type"] == null)
					AddHeader("Content-Type", contentType);
				else
					Headers["Content-Type"] = contentType;
			}
		}

		/// <summary>
		/// Specifies a System.IO.Stream object to which a response body can be written.
		/// </summary>
		/// <returns>A System.IO.Stream object to which a response body can be written.</returns>
		public Stream OutputStream
		{
			get
			{
				if (false) throw new InvalidOperationException("Content-Length not set."); //todo in future

				if (HeadersSent) return outputStream;
				else throw new InvalidOperationException("Call SendHeaders() first before sending body.");
			}
			set
			{ outputStream = value; }
		}

		/// <summary>
		/// HttpListenerResponse, used to send this HTTP Response (or null if another backend is used).
		/// </summary>
		public HttpListenerResponse MshttpapiBackend { get; set; }

		/// <summary>
		/// TcpClient, used to send this HTTP Response (or null if another backend is used).
		/// </summary>
		public TcpClient TcpclientBackend { get; set; }

		/// <summary>
		/// Specifies value that indicates whether the client connection can be persistent after this response.
		/// </summary>
		/// <returns>true if the connection should be kept open; otherwise, false.</returns>
		public bool KeepAlive { get; set; }
		// note: this means that all request bytes are read by Proxy, and next bytes will be next request start ("GET /index.htm HTTP/1.1")

		/// <summary>
		/// Initialize an instance of an response to a HTTP request, used with a HttpListenerContext instance.
		/// </summary>
		/// <param name="Backend">HttpListenerResponse from HttpListenerContext which will be used to communicate with client.</param>
		public HttpResponse(HttpListenerResponse Backend)
		{
			MshttpapiBackend = Backend;
			OutputStream = Backend.OutputStream;

			Headers = new WebHeaderCollection();
		}

		/// <summary>
		/// Initialize an instance of an response to a HTTP request, used with a TcpClient instance.
		/// </summary>
		/// <param name="Backend">TcpClient which will be used to communicate with client.</param>
		public HttpResponse(TcpClient Backend)
		{
			TcpclientBackend = Backend;
			OutputStream = Backend.GetStream();

			Headers = new WebHeaderCollection();
		}


		private bool HeadersSent = false;
		private string contentType;
		private long contentLength64;
		private Stream outputStream;


		/// <summary>
		/// Send the response headers to client. This should be called before writing response body to <see cref="OutputStream"/>.
		/// </summary>
		public void SendHeaders()
		{
			if (HeadersSent) throw new InvalidOperationException("HTTP response headers are already sent.");

			if (MshttpapiBackend != null)
			{
				MshttpapiBackend.StatusCode = StatusCode;
				MshttpapiBackend.ProtocolVersion = ProtocolVersion;
				MshttpapiBackend.Headers = Headers;
				HeadersSent = true;
				return;
			}
			if (TcpclientBackend != null)
			{
				//will be written
				StreamWriter ClientStreamWriter = new StreamWriter(TcpclientBackend.GetStream());
				ClientStreamWriter.WriteLine(ProtocolVersionString + " " + StatusCode);
				string HeadersString = Headers.ToString().Replace("\r\n", "\n").Replace("\n\n", "");
				ClientStreamWriter.WriteLine(HeadersString);
				ClientStreamWriter.WriteLine();
				ClientStreamWriter.Flush();
				HeadersSent = true;
				return;
			}
			throw new Exception("Backend not set.");
		}

		/// <summary>
		/// Sends the response to the client and releases the resources held by this HttpResponse instance.
		/// </summary>
		public void Close()
		{
			if (!HeadersSent) SendHeaders();
			if (MshttpapiBackend != null) { MshttpapiBackend.Close(); return; }
			if (TcpclientBackend != null)
			{
				TcpclientBackend.GetStream().Flush();
				if (!KeepAlive) TcpclientBackend.Close();
				return;
			}
			throw new Exception("Backend not set.");
		}

		/// <summary>
		/// Adds the specified header and value to the HTTP headers for this response.
		/// </summary>
		/// <param name="name">The name of the HTTP header to set.</param>
		/// <param name="value">The value for the name header.</param>
		/// <exception cref="T:System.ArgumentNullException">name is null or an empty string ("").</exception>
		/// <exception cref="T:System.ArgumentException:">
		/// You are not allowed to specify a value for the specified header. -or- name or value contains invalid characters.
		/// </exception>
		public void AddHeader(string name, string value)
		{
			Headers.Add(name, value);
		}

		/// <summary>
		/// Appends a value to the specified HTTP header to be sent with this response.
		/// </summary>
		/// <param name="name">The name of the HTTP header to append value to.</param>
		/// <param name="value">The value to append to the name header.</param>
		/// <exception cref="T:System.ArgumentNullException">name is null or an empty string ("").</exception>
		/// <exception cref="T:System.ArgumentException:">
		/// You are not allowed to specify a value for the specified header. -or- name or value contains invalid characters.
		/// </exception>
		public void AppendHeader(string name, string value)
		{
			Headers[name] += value;
		}
	}
}
