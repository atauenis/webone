using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// HTTP/1.1 Listener and Server (TcpClient-based).
	/// </summary>
	class HttpServer2 : HttpServer
	{
		/* This will be new version of HTTP Listener/Server.
		 * Pluses:   will support in addition to basic HTTP/1.1 also CONNECT method and all possible URIs in requests.
		 * Minuses:  ...time will show...
		 * https://www.codeproject.com/Articles/93301/Implementing-a-Multithreaded-HTTP-HTTPS-Debugging
		 */

		private int Port;
		private static TcpListener Listener;
		private LogWriter Log = new();
		private int Load;

		/// <summary>
		/// Status of this HTTP Server.
		/// </summary>
		public override bool Working { get; set; }

		/// <summary>
		/// Initizlize a HTTP Server.
		/// </summary>
		/// <param name="port">TCP Port to listen on.</param>
		public HttpServer2(int port) : base(port)
		{
			Port = port;
			Working = false;
			Listener = new(Port);// new TcpListener(IPAddress.Loopback, Port);
		}

		/// <summary>
		/// Start this HTTP Server.
		/// </summary>
		public override void Start()
		{
			//Log.WriteLine(true, false, "Starting server...\t\t\t\t     EnableNewHttpServer = yes.");
			Listener.Start();
			Listener.BeginAcceptTcpClient(ProcessConnection, null);
			Working = true;
			Log.WriteLine(true, false, " =2= Secure and FTP: \t {0}:{1}", ConfigFile.DefaultHostName, Port);
			UpdateStatistics();
		}

		/// <summary>
		/// Gracefully stop this HTTP Server.
		/// </summary>
		public override void Stop()
		{
			Working = false;
			Log.BeginTime = DateTime.Now;
			//Log.WriteLine(true, true, "Shutdown server...");
			if (Listener != null)
			{
				Listener.Stop();
			}
			Log.WriteLine(true, true, "Secure & FTP Server stopped.");
		}

		/// <summary>
		/// Process a HTTP request (callback for TcpListener).
		/// </summary>
		/// <param name="ar">Something from TcpListener.</param>
		private void ProcessConnection(IAsyncResult ar)
		{
			if (!Working) return;
			Load++;
			UpdateStatistics();
			LogWriter Logger = new();
#if DEBUG
			Logger.WriteLine("Got a connection.");
#endif
			TcpClient Client = Listener.EndAcceptTcpClient(ar);
			Listener.BeginAcceptTcpClient(ProcessConnection, null);

			try
			{
				ProcessClientRequest(Client, Logger);
			}
			catch (IOException)
			{
				/*Timeouts, unexpected socket close, etc*/
#if DEBUG
				Logger.WriteLine("Connection closed.");
#endif
			}
			catch (Exception ex)
			{
				Logger.WriteLine("Oops: {0}.", ex.Message);
				try { Client.Close(); } catch { }
			}

			Load--;
			UpdateStatistics();
		}

		/// <summary>
		/// Process incoming TCP/IP traffic from client.
		/// </summary>
		/// <param name="Client">TcpClient used to communicate with client.</param>
		/// <param name="Logger">Log writer.</param>
		private void ProcessClientRequest(TcpClient Client, LogWriter Logger)
		{
#if DEBUG
			Logger.WriteLine("Got a request.");
#endif

			Stream clientStream = Client.GetStream();

			// Read text part of HTTP request (until double line feed).
			BinaryReader br = new(clientStream);
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
						clientStream.Close();
						Client.Close();
						Logger.WriteLine("<Close empty connection.");
						return;
					}
					string[] HttpCommandParts = HttpRequestLine.Split(' ');
					if (HttpCommandParts.Length != 3 || HttpCommandParts[2].Length != 8)
					{
						clientStream.Close();
						Client.Close();
						Logger.WriteLine("<Dropped: Non-HTTP connection: {0}", HttpRequestLine);
						return;
					}

					// First line is valid, start work with the Request.
					Request = new()
					{
						HttpMethod = HttpCommandParts[0],
						RawUrl = HttpCommandParts[1],
						ProtocolVersionString = HttpCommandParts[2],
						Headers = new(),
						RemoteEndPoint = Client.Client.RemoteEndPoint as IPEndPoint,
						LocalEndPoint = Client.Client.LocalEndPoint as IPEndPoint,
						IsSecureConnection = false
					};
					if (Request.RawUrl.Contains("://"))
					{ Request.Url = new Uri(Request.RawUrl); }
					else if (Request.RawUrl.StartsWith('/'))
					{ Request.Url = new Uri("http://" + Variables["Proxy"] + Request.RawUrl); }
					else
					{ Request.Url = new Uri("http://" + Variables["Proxy"] + "/" + Request.RawUrl); }

					IsCommand = false;
					continue;
				}
				else
				{
					// Other lines - request headers, load all of them.
					if (string.IsNullOrWhiteSpace(HttpRequestLine)) continue;
					Request.Headers.Add(HttpRequestLine.Substring(0, HttpRequestLine.IndexOf(": ")), HttpRequestLine.Substring(HttpRequestLine.IndexOf(": ") + 2));

					if (HttpRequestLine == "Connection: keep-alive" || HttpRequestLine == "Proxy-Connection: keep-alive")
						Request.KeepAlive = true;
				}
			}

			if (Request == null)
			{
				clientStream.Close();
				Client.Close();
				Logger.WriteLine("<Dropped (unknown why).");
				return;
			}

			if (Request.Headers["Content-Length"] != null)
			{
				// If there's a payload, convert it to a HttpRequestContentStream.
				Request.InputStream = new HttpRequestContentStream(clientStream, int.Parse(Request.Headers["Content-Length"]));

				/*
				 * NetworkStream is not suitable for HTTP request bodies. It have no length, and read operation is endless.
				 * What is suitable - .NET's internal HttpRequestStream and ChunkedInputStream:HttpRequestStream.
				 * See .NET source: https://source.dot.net/System.Net.HttpListener/R/d562e26091bc9f8d.html
				 * They are reading traffic only until HTTP Content-Length or last HTTP Chunk into a correct .NET Stream format.
				 * 
				 * WebOne.HttpRequestContentStream is a very lightweight alternative to System.Net.HttpRequestStream.
				 */

				//UNDONE: works in Netscape 3, but half-works in Firefox 3.6. Find why & fix!
			}
			else
			{
				// No payload in request - original NetworkStream is suitable.
				Request.InputStream = clientStream;
			}

			HttpResponse Response = new(Client);
			HttpTransit Transit = new(Request, Response, Logger);
			Logger.WriteLine(">{0} {1} ({2})", Request.HttpMethod, Request.RawUrl, Transit.GetClientIdString());
			Transit.ProcessTransit();

			if (Request.KeepAlive && Response.KeepAlive)
			{
				Logger.WriteLine("<Done.");
				ProcessClientRequest(Client, new());
			}
			else
			{
				Client.Close();
				Logger.WriteLine("<Done (connection close).");
			}
		}

		/// <summary>
		/// Display count of open requests in app's titlebar.
		/// </summary>
		private void UpdateStatistics()
		{
			if (DaemonMode)
				Console.Title = string.Format("WebOne (silent) @ {0}:{1} [{2}]", ConfigFile.DefaultHostName, Port, Load);
			else
				Console.Title = string.Format("WebOne @ {0}:{1} [{2}]", ConfigFile.DefaultHostName, Port, Load);
		}
	}
}
