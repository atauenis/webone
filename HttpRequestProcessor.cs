using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using static WebOne.HttpUtil;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Parser of raw HTTP traffic, which decodes, process, and codes back HTTP's bytes.
	/// </summary>
	class HttpRequestProcessor
	{
		/// <summary>
		/// Initialize instance of raw HTTP traffic parser (processor).
		/// </summary>
		public HttpRequestProcessor() { }

		/// <summary>
		/// Process incoming TCP/IP traffic from client.
		/// </summary>
		/// <param name="Backend">TcpClient used to communicate with client.</param>
		/// <param name="Logger">Log writer.</param>
		public void ProcessClientRequest(TcpClient Backend, LogWriter Logger)
		{
			ProcessClientRequest(Backend as object, Logger);
		}

		/// <summary>
		/// Process incoming TCP/IP traffic from client.
		/// </summary>
		/// <param name="Backend">HttpUtil.SslClient used to communicate with client.</param>
		/// <param name="Logger">Log writer.</param>
		/// <param name="SslLogPrefix">Prefix to be shown in log entries.</param>
		public void ProcessClientRequest(SslClient Backend, LogWriter Logger, string SslLogPrefix)
		{
			ProcessClientRequest(Backend as object, Logger, SslLogPrefix);
		}

		/// <summary>
		/// Process incoming TCP/IP traffic from client.
		/// </summary>
		/// <param name="Backend">HttpListenerRequest, TcpClient or HttpUtil.SslClient used to communicate with client.</param>
		/// <param name="Logger">Log writer.</param>
		/// <param name="SslLogPrefix">Used only on HTTPS requests. Prefix to be shown in log entries.</param>
		private void ProcessClientRequest(object Backend, LogWriter Logger, string SslLogPrefix = "SSL")
		{
#if DEBUG
			if (Backend is SslClient)
				Logger.WriteLine("Got a secure request.");
			else
				Logger.WriteLine("Got a request.");
#endif
			// Prepare data stream
			Stream ClientStream;
			switch (Backend)
			{
				case TcpClient tcpc:
					ClientStream = tcpc.GetStream();
					break;
				case SslClient sslc:
					ClientStream = sslc.Stream;
					break;
				default:
					throw new ArgumentException("Incorrect backend.", nameof(Backend));
			}

			// Read text part of HTTP request (until double line feed).
			BinaryReader br = new(ClientStream);
			List<char> rqChars = new();
			while (true)
			{
				rqChars.Add(br.ReadChar());

				if (rqChars.Count < 2) continue;
				if (rqChars[rqChars.Count - 1] == '\r')
				{
					if (rqChars[rqChars.Count - 3] == '\r' && rqChars[rqChars.Count - 2] == '\n')
					{
						rqChars.Add(br.ReadChar());
						break;
					}
				}
			}

			// Process HTTP command and headers.
			HttpRequest Request = null;
			bool IsCommand = true;
			foreach (string HttpRequestLine in new string(rqChars.ToArray()).Split("\r\n"))
			{
				if (string.IsNullOrWhiteSpace(HttpRequestLine)) continue;
				if (IsCommand)
				{
					// First line - HTTP command.
					if (string.IsNullOrEmpty(HttpRequestLine))
					{
						Logger.WriteLine("<Close empty connection.");
						return;
					}
					string[] HttpCommandParts = HttpRequestLine.Split(' ');
					if (HttpRequestLine.StartsWith("CONNECT") && HttpRequestLine.Contains('\n'))
					{
						//fix "Dropped: Non-HTTP connection: CONNECT www.sannata.org:443 HTTP/1.0User-Agent: Mozilla/3.04Gold (WinNT; U)"
						string HttpRequestLineNetscapeBug = HttpRequestLine.Substring(0, HttpRequestLine.IndexOf('\n'));
						HttpCommandParts = HttpRequestLineNetscapeBug.Split(' ');
					}
					else if (HttpCommandParts.Length != 3 || HttpCommandParts[2].Length != 8)
					{
						if (Backend is SslClient)
						{
							// Non-HTTP protocol inside SSL tunnel.
							// This is used by MSN Messenger, IRCS, POP3-SSL, SMTP-SSL, IMAP-SSL and other apps supporting HTTPS-proxies.
							Logger.WriteLine("<Dropped: Non-HTTPS connection: {0}", HttpRequestLine);
							throw new Exception("Write your domain:port to webone.conf/[NonHttpSslServers], please.");
						}
						else
						{
							Logger.WriteLine("<Dropped: Non-HTTP connection: {0}", HttpRequestLine);
							return;
						}
					}

					// First line is valid, start work with the Request.
					Request = new()
					{
						HttpMethod = HttpCommandParts[0],
						RawUrl = HttpCommandParts[1],
						ProtocolVersionString = HttpCommandParts[2],
						Headers = new(),
						IsSecureConnection = Backend is SslClient
					};

					// Define Client & Proxy IP addresses
					if (Backend is HttpListenerRequest hlr)
					{
						Request.RemoteEndPoint = hlr.RemoteEndPoint;
						Request.LocalEndPoint = hlr.LocalEndPoint;
					}
					else if (Backend is TcpClient tcpc)
					{
						Request.RemoteEndPoint = tcpc.Client.RemoteEndPoint as IPEndPoint;
						Request.LocalEndPoint = tcpc.Client.LocalEndPoint as IPEndPoint;
					}
					else if (Backend is SslClient sslc)
					{
						Request.RemoteEndPoint = sslc.RemoteEndPoint;
						Request.LocalEndPoint = sslc.LocalEndPoint;
					}

					// Okay, ready to parse headers.
					IsCommand = false;
					continue;
				}
				else
				{
					// Other lines - request headers, load all of them.
					if (string.IsNullOrWhiteSpace(HttpRequestLine)) continue;
					Request.Headers.Add(HttpRequestLine.Substring(0, HttpRequestLine.IndexOf(": ")), HttpRequestLine.Substring(HttpRequestLine.IndexOf(": ") + 2));
				}
			}

			if (Request == null)
			{
				Logger.WriteLine("<Dropped (unknown HTTP derivative).");
				return;
			}

			if (Request.RawUrl.StartsWith("ftp:/") && !Request.RawUrl.StartsWith("ftp://"))
			{
				Logger.WriteLine("<Dropped (bad FTP protocol address)."); //IBM WebExplorer bug
				return;
			}

			// Define URI from HTTP Command and HTTP Host header.
			RequestKind Kind = GetKindOfRequest(Request.RawUrl, Request.Headers["Host"], null, Request.HttpMethod == "CONNECT");
			Request.Kind = Kind;
			string Host = Request.Headers["Host"] ?? Variables["Proxy"];
			switch (Kind)
			{
				case RequestKind.StandardProxy:
					Request.Url = new Uri(Request.RawUrl);
					break;
				case RequestKind.StandardLocal:
				case RequestKind.StandardRemote:
					Request.Url = new Uri("http://" + Host + Request.RawUrl);
					break;
				case RequestKind.AlternateProxy:
					Request.Url = new Uri(Request.RawUrl[1..]);
					break;
				case RequestKind.StandardSslProxy:
					Request.Url = null;
					break;
			}

			// Configure content transfer stream
			if (Request.Headers["Content-Length"] != null && Request.Headers["Content-Length"] != "0")
			{
				// If there's a payload, convert it to a HttpRequestContentStream.
				Request.InputStream = new HttpRequestContentStream(ClientStream, int.Parse(Request.Headers["Content-Length"]));

				/*
				 * NetworkStream/SslStream is not suitable for HTTP request bodies. It have no length, and read operation is endless.
				 * What is suitable - .NET's internal HttpRequestStream and ChunkedInputStream:HttpRequestStream.
				 * See .NET source: https://source.dot.net/System.Net.HttpListener/R/d562e26091bc9f8d.html
				 * They are reading traffic only until HTTP Content-Length or last HTTP Chunk into a correct .NET Stream format.
				 * 
				 * WebOne.HttpRequestContentStream is a very lightweight alternative to System.Net.HttpRequestStream.
				 */
			}
			else if (false)
			{
				// Will be used in future for chunked payload transfer.
				// See note above.
			}
			else
			{
				// No payload in request - original NetworkStream is suitable.
				Request.InputStream = ClientStream;
			}

			// Configure persistent connection mode (a.k.a. Keep-Alive)
			if ((Request.Headers["Connection"] ?? "").ToLower().Contains("keep-alive") ||
			(Request.Headers["Proxy-Connection"] ?? "").ToLower().Contains("keep-alive"))
			{ Request.KeepAlive = true; }

			// Ready to start HTTP transit process.
			HttpResponse Response;
			if (Backend is HttpListenerResponse) Response = new(Backend as HttpListenerResponse);
			else if (Backend is TcpClient) Response = new((TcpClient)Backend);
			else if (Backend is SslClient) Response = new(((SslClient)Backend).Stream);
			else throw new ArgumentException("Incorrect backend.", nameof(Backend));

			HttpTransit Transit = new(Request, Response, Logger);
			if (Backend is SslClient)
				Logger.WriteLine(">[{3}] {0} {1} ({2})", Request.HttpMethod, Request.RawUrl, Transit.GetClientIdString(), SslLogPrefix);
			else
				Logger.WriteLine(">{0} {1} ({2})", Request.HttpMethod, Request.RawUrl, Transit.GetClientIdString());
			Transit.ProcessTransit();

			// Restart processing if the connection is persistent. Or exit if not.
			if (Request.KeepAlive && Response.KeepAlive)
			{
				Logger.WriteLine("<Done.");
				ProcessClientRequest(Backend, new(), Request.Headers["Host"] ?? "Keep-Alive, no Host");
				return;
			}
			else
			{
				Logger.WriteLine("<Done (connection close).");
				return;
			}
		}
	}
}