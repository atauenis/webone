using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Utilities for processing HTTP traffic
	/// </summary>
	static class HttpUtil
	{
		/// <summary>
		/// Process incoming TCP/IP traffic from client.
		/// </summary>
		/// <param name="Backend">TcpClient used to communicate with client.</param>
		/// <param name="Logger">Log writer.</param>
		public static void ProcessClientRequest(TcpClient Backend, LogWriter Logger)
		{
			ProcessClientRequest(Backend as object, Logger);
		}

		/// <summary>
		/// Process incoming TCP/IP traffic from client.
		/// </summary>
		/// <param name="Backend">HttpUtil.SslClient used to communicate with client.</param>
		/// <param name="Logger">Log writer.</param>
		public static void ProcessClientRequest(SslClient Backend, LogWriter Logger)
		{
			ProcessClientRequest(Backend as object, Logger);
		}

		/// <summary>
		/// Process incoming TCP/IP traffic from client.
		/// </summary>
		/// <param name="Backend">HttpListenerRequest, TcpClient or HttpUtil.SslClient used to communicate with client.</param>
		/// <param name="Logger">Log writer.</param>
		private static void ProcessClientRequest(object Backend, LogWriter Logger)
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
							// UNDONE: establish tunnel to remote server with only SSL decoding.
							//         meaning: (Backend.Stream) <-> (new SslStream over NetworkStream to remote server & port)
							Logger.WriteLine("<Dropped: Non-HTTPS connection: {0}", HttpRequestLine);
							throw new NotImplementedException("Currently cannot work with non-HTTP protocols inside SSL stream.");
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

					//undone: put below and made case insensitive, check Opera 7 support
					if (HttpRequestLine == "Connection: keep-alive" || HttpRequestLine == "Proxy-Connection: keep-alive")
						Request.KeepAlive = true;
				}
			}

			if (Request == null)
			{
				Logger.WriteLine("<Dropped (unknown why).");
				return;
			}

			// Define URI from HTTP Command and HTTP Host header.
			RequestKind Kind = GetKindOfRequest(Request.RawUrl, Request.Headers["Host"], Request.HttpMethod == "CONNECT");
			Request.Kind = Kind;
			string Host = Request.Headers["Host"] ?? Variables["Proxy"];
			switch (Kind)
			{
				case RequestKind.Proxy:
					Request.Url = new Uri(Request.RawUrl);
					break;
				case RequestKind.StandardLocal:
				case RequestKind.StandardRemote:
					Request.Url = new Uri("http://" + Host + Request.RawUrl);
					break;
				case RequestKind.DirtyProxy:
					Request.Url = new Uri(Request.RawUrl[1..]);
					break;
				case RequestKind.SslProxy:
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
			// undone: copy & adapt code above

			// Ready to start HTTP transit process.
			HttpResponse Response;
			if (Backend is HttpListenerResponse) Response = new(Backend as HttpListenerResponse);
			else if (Backend is TcpClient) Response = new((TcpClient)Backend);
			else if (Backend is SslClient) Response = new(((SslClient)Backend).Stream);
			else throw new ArgumentException("Incorrect backend.", nameof(Backend));

			HttpTransit Transit = new(Request, Response, Logger);
			if (Backend is SslClient)
				Logger.WriteLine(">[SSL] {0} {1} ({2})", Request.HttpMethod, Request.RawUrl, Transit.GetClientIdString());
			else
				Logger.WriteLine(">{0} {1} ({2})", Request.HttpMethod, Request.RawUrl, Transit.GetClientIdString());
			Transit.ProcessTransit();

			// Restart processing if the connection is persistent. Or exit if not.
			if (Request.KeepAlive && Response.KeepAlive)
			{
				Logger.WriteLine("<Done.");
				ProcessClientRequest(Backend, new());
				return;
			}
			else
			{
				Logger.WriteLine("<Done (connection close).");
				return;
			}
		}

		/// <summary>
		/// Emulates client connections for SSL/TLS network services. Similar to <seealso cref="TcpClient"/>, but is working over <seealso cref="SslStream"/>.
		/// </summary>
		public struct SslClient
		{
			/// <summary>
			/// The stream of content transferred through SSL/TLS layer.
			/// </summary>
			public SslStream Stream;
			/// <summary>
			/// Specifies local end point (Client IP).
			/// </summary>
			public IPEndPoint LocalEndPoint;
			/// <summary>
			/// Specifies remote end point (this Proxy IP).
			/// </summary>
			public IPEndPoint RemoteEndPoint;
			/// <summary>
			/// Specifies SSL server host name and port, specified by client.
			/// </summary>
			public string TargetServer;
			/// <summary>
			/// Grade of SSL encrypting
			/// </summary>
			public string Encrypting;
		}

		/// <summary>
		/// Kind of content requested by client.
		/// </summary>
		public enum RequestKind
		{
			/*
			GET /123
			Host: example.com
			-> http://example.com/123   StandardRemote

			GET /123
			Host: 127.0.0.1
			-> http://127.0.0.1/123     StandardLocal

			GET /123
			no host, assuming 127.0.0.1
			-> http://127.0.0.1/123     StandardLocal

			GET /http://example.com/123
			Host: 127.0.0.1
			-> http://example.com/123   DirtyProxy

			GET /http://example.com/123
			no host, assuming 127.0.0.1
			-> http://example.com/123   DirtyProxy

			GET http://example.com/123
			Host: example.com
			-> http://example.com/123   Proxy

			GET ftp://example.com/pub
			Host: example.com
			-> ftp://example.com/pub    Proxy

			GET ftp://example.com/pub
			no host, assuming 127.0.0.1
			-> ftp://example.com/pub    Proxy

			CONNECT example.com:443
			Host: example.com
			-> null                     SslProxy

			CONNECT example.com:443
			no host, assuming 127.0.0.1
			-> null                     SslProxy
			 */

			/// <summary>
			/// Standard HTTP request to other server.
			/// </summary>
			StandardRemote,
			/// <summary>
			/// Standard HTTP request to this server.
			/// </summary>
			StandardLocal, //also may be "Very Dirty Proxy mode".
			/// <summary>
			/// Standard HTTP request to this server; "Dirty Proxy" mode.
			/// </summary>
			DirtyProxy,
			/// <summary>
			/// Request to a HTTP proxy server.
			/// </summary>
			Proxy,
			/// <summary>
			/// Connection request to a HTTPS proxy server.
			/// </summary>
			SslProxy
		}

		/// <summary>
		/// Get kind of content requested by client.
		/// </summary>
		/// <param name="RawUrl">URL from HTTP Command.</param>
		/// <param name="HostHeader">"Host" header value (if any).</param>
		/// <param name="IsCONNECT">Is "CONNECT" method used.</param>
		/// <returns>Kind of content requested by client.</returns>
		public static RequestKind GetKindOfRequest(string RawUrl, string HostHeader = null, bool IsCONNECT = false)
		{
			string Host = HostHeader ?? "127.0.0.1";
			int Port = ConfigFile.Port;

			// Detect port number (if any)
			if (Host.Contains(":"))
			{
				Port = int.Parse(Host.Substring(Host.IndexOf(":") + 1));
				Host = Host.Substring(0, Host.IndexOf(":"));
			}

			if (RawUrl.StartsWith("/"))
			{
				// Standard* or Dirty
				if (IsLocalhost(Host, Port)) //check Host name and Port number
				{
					// Target is this server, so StandardLocal or DirtyProxy
					if (RawUrl.Contains("://")) return RequestKind.DirtyProxy;
					return RequestKind.StandardLocal;
				}
				else
				{
					// Target is other server, so StandardRemote
					return RequestKind.StandardRemote;
				}
			}
			else
			{
				// Proxy or SslProxy
				if (RawUrl.Contains("://")) return RequestKind.Proxy;
				else if (IsCONNECT) return RequestKind.SslProxy;
				else throw new Exception("Cannot guess kind of requested target.");
			}
		}

		/// <summary>
		/// Find if the <paramref name="Host"/> refers to the local machine.
		/// </summary>
		/// <param name="Host">Host name or IP v4/v6 address to be checked.</param>
		/// <param name="Port">Port to be checked.</param>
		/// <returns>True if local machine; False if another machine.</returns>
		public static bool IsLocalhost(string Host, int Port)
		{
			if (Host.Contains(':')) throw new ArgumentException("Forget to split 'host:port' pair.", nameof(Host));
			return CheckString(Host, GetLocalHostNames(), true) && (Port == ConfigFile.Port || Port == ConfigFile.Port2);
		}

		/// <summary>
		/// Get all possible aliases for 127.0.0.1.
		/// </summary>
		/// <returns>List of IP addresses and host names, related to this machine.</returns>
		public static List<string> GetLocalHostNames()
		{
			List<string> localhosts = new();
			foreach (IPAddress address in GetLocalIPAddresses())
			{
				if (address.AddressFamily == AddressFamily.InterNetworkV6)
					localhosts.Add("[" + address.ToString() + "]");
				else
					localhosts.Add(address.ToString());
			}

			localhosts.Add("wpad");
			localhosts.Add("localhost");
			localhosts.Add(Environment.MachineName);
			localhosts.Add(ConfigFile.DefaultHostName);
			localhosts.AddRange(ConfigFile.HostNames);
			return localhosts;
		}
	}
}
