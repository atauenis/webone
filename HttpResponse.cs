using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

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
		/// Gets or sets the HTTP status code description message, starting with a space character.
		/// </summary>
		/// <returns>An status message corresponding to <see cref="StatusCode"/> or set overriden.</returns>
		public string StatusMessage
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(statusMsg)) return statusMsg;
				switch (StatusCode)
				{
					case 100: return " Continue";
					case 101: return " Switching Protocols";
					case 200: return " OK";
					case 201: return " Created";
					case 202: return " Accepted";
					case 203: return " Non-Authoritative Information";
					case 204: return " No Content";
					case 205: return " Reset Content";
					case 206: return " Partial Content";
					case 300: return " Multiple Choices";
					case 301: return " Moved Permanently";
					case 302: return " Moved Temporarily";
					case 303: return " See Other";
					case 304: return " Not Modified";
					case 305: return " Use Proxy";
					case 400: return " Bad Request";
					case 401: return " Unauthorized";
					case 402: return " Payment Required";
					case 403: return " Forbidden";
					case 404: return " Not Found";
					case 405: return " Method Not Allowed";
					case 406: return " Not Acceptable";
					case 407: return " Proxy Authentication Required";
					case 408: return " Request Timeout";
					case 409: return " Conflict";
					case 410: return " Gone";
					case 411: return " Length Required";
					case 412: return " Precondition Failed";
					case 413: return " Request Entity Too Large";
					case 414: return " Request-URI Too Long";
					case 415: return " Unsupported Media Type";
					case 418: return " I'm a teapot";
					case 500: return " Internal Server Error";
					case 501: return " Not Implemented";
					case 502: return " Bad Gateway";
					case 503: return " Service Unavailable";
					case 504: return " Gateway Timeout";
					case 505: return " HTTP Version Not Supported";
					default: return ""; //don't append message for unknown status codes
				}
			}
			set
			{
				if (value.StartsWith(" ") || value == "") statusMsg = value;
				else throw new ArgumentException("HTTP status message must start from space.", nameof(value));
			}
		}

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
		/// <returns>The value of the response's Content-Length header. Or &quot;-1&quot; to use chunked transfer.</returns>
		public long ContentLength64
		{
			get => contentLength64;
			set
			{
				contentLength64 = value;

				if (value < 0) // Content-Length less than 0 means stream with unknown length.
				{
					if (ProtocolVersion == new Version(1, 1))
					{
						// Enable chunked transfer (HTTP/1.1 only).
						if (MshttpapiBackend == null)
						{
							if (Headers["Transfer-Encoding"] == null) AddHeader("Transfer-Encoding", "chunked");
							else Headers["Transfer-Encoding"] = "chunked";
							return;
						}
						else
						{
							MshttpapiBackend.SendChunked = true;
							return;
						}
					}
					else
					{
						// "Chunked encoding upload is not supported on the HTTP/1.0 protocol."
						// With MSHTTPAPI backend will cause ProtocolViolationException, with TCP backend will result in garbaged content.
						// So simply send as is if version is old.
						// Length-less transfers are incompatible with persistent connections. That's why chunks were introduced in HTTP/1.1.
						KeepAlive = false;
						if (Headers["Connection"] == null) AddHeader("Connection", "Close");
						else Headers["Connection"] = "Close";
						return;
					}

				}

				if (MshttpapiBackend != null) { MshttpapiBackend.ContentLength64 = value; }
				if (Headers["Content-Length"] == null)
					AddHeader("Content-Length", contentLength64.ToString());
				else
					Headers["Content-Length"] = contentLength64.ToString();
			}
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
				if (SimpleContentType)
				{
					//strip RFC 2068 §14.18 to RFC 1945 §10.5
					if (contentType.Contains(';')) contentType = contentType.Substring(0, contentType.IndexOf(';'));
				}

				if (MshttpapiBackend != null) MshttpapiBackend.ContentType = contentType;

				if (Headers["Content-Type"] == null)
					Headers.Add("Content-Type", contentType);
				else
					Headers["Content-Type"] = contentType;
			}
		}

		/// <summary>
		/// Gets or sets value, meaning use of HTTP/1.0 style of Content-Type header (<c>true</c>). Set to <c>false</c> to use HTTP/1.1 style.
		/// </summary>
		public bool SimpleContentType { get; set; }

		/// <summary>
		/// Specifies a System.IO.Stream object to which a response body can be written.
		/// </summary>
		/// <returns>A System.IO.Stream object to which a response body can be written.</returns>
		public Stream OutputStream
		{
			get
			{
				if (HeadersSent)
				{
					if (MshttpapiBackend != null) return outputStream; //MsHttpApi processes everything itself
					if (outputStream is HttpResponseContentStream) return outputStream; //already configured

					// Set up HttpResponseContentStream according to headers data (transfer-encoding)
					outputStream = new HttpResponseContentStream(outputStream, (ContentLength64 < 0 && ProtocolVersion == new Version(1, 1)));
					return outputStream;
				}
				else { throw new InvalidOperationException("Call SendHeaders() first before sending body."); }
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
		/// SslSltream, used to send this HTTP Response (or null if another backend is used).
		/// </summary>
		public SslStream SslBackend { get; set; }

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
			ProtocolVersion = new Version(1, 1);

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
			ProtocolVersion = new Version(1, 1);

			Headers = new WebHeaderCollection();
		}

		/// <summary>
		/// Initialize an instance of an response to a HTTPS request, used with a SslStream instance.
		/// </summary>
		/// <param name="Backend">SslStream which will be used to communicate with client.</param>
		public HttpResponse(SslStream Backend)
		{
			SslBackend = Backend;
			OutputStream = Backend;
			ProtocolVersion = new Version(1, 1);

			Headers = new WebHeaderCollection();
		}


		public bool HeadersSent = false;
		private string contentType;
		private long contentLength64;
		private Stream outputStream;
		private string statusMsg = "";


		/// <summary>
		/// Send the response headers to client. This should be called before writing response body to <see cref="OutputStream"/>.
		/// </summary>
		public void SendHeaders()
		{
			if (HeadersSent) throw new InvalidOperationException("HTTP response headers are already sent.");

			if (Headers["Server"] == null && Headers["Via"] == null) Headers.Add("Server", "WebOne/" + Program.Variables["WOVer"]);
			if (Headers["Connection"] == null) { Headers.Add("Connection", "Keep-Alive"); KeepAlive = true; }

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
				string BufferS = ProtocolVersionString + " " + StatusCode + StatusMessage + "\r\n";
				BufferS += Headers.ToString();
				if (!BufferS.EndsWith("\r\n\r\n")) { BufferS += "\r\n\r\n"; }
				byte[] BufferB = Encoding.ASCII.GetBytes(BufferS);
				TcpclientBackend.GetStream().Write(BufferB, 0, BufferB.Length);
				HeadersSent = true;
				return;
			}
			if (SslBackend != null)
			{
				string BufferS = ProtocolVersionString + " " + StatusCode + StatusMessage + "\r\n";
				BufferS += Headers.ToString();
				if (!BufferS.EndsWith("\r\n\r\n")) { BufferS += "\r\n\r\n"; }
				byte[] BufferB = Encoding.ASCII.GetBytes(BufferS);
				SslBackend.Write(BufferB, 0, BufferB.Length);
				HeadersSent = true;
				return;
			}
			throw new Exception("Backend is unsupported or not set.");
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
				if (outputStream is HttpResponseContentStream) (outputStream as HttpResponseContentStream).WriteTerminator();
				if (TcpclientBackend.Connected) TcpclientBackend.GetStream().Flush();
				if (!KeepAlive) TcpclientBackend.Close();
				return;
			}
			if (SslBackend != null)
			{
				if (outputStream is HttpResponseContentStream) (outputStream as HttpResponseContentStream).WriteTerminator();
				if (SslBackend.CanWrite) SslBackend.Flush();
				if (!KeepAlive) SslBackend.Close();
				return;
			}
			throw new Exception("Backend is unsupported or not set.");
		}

		/// <summary>
		/// Check is the client browser still connected to the proxy.
		/// </summary>
		public bool IsConnected
		{
			get
			{
				try
				{
					if (MshttpapiBackend != null) return MshttpapiBackend.OutputStream.CanWrite;
					if (TcpclientBackend != null) return TcpclientBackend.Connected;
					if (SslBackend != null) return SslBackend.CanWrite;
					throw new Exception("Backend is unsupported or not set.");
				}
				catch (ObjectDisposedException) { return false; }
			}
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
			if (name.ToLowerInvariant() == "content-type")
			{
				ContentType = value;
			}
			else
			{
				Headers.Add(name, value);
			}
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
