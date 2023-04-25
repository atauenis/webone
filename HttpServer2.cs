using System;
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
			Listener = new TcpListener(IPAddress.Loopback, Port);
		}

		/// <summary>
		/// Start this HTTP Server.
		/// </summary>
		public override void Start()
		{
			Log.WriteLine(true, false, "Starting server...\t\t\t\t     EnableNewHttpServer = yes.");
			Listener.Start();
			Listener.BeginAcceptTcpClient(ProcessConnection, null);
			Working = true;
			Log.WriteLine(true, false, "Listening for connections on port {0}.", Port);
			UpdateStatistics();
		}

		/// <summary>
		/// Gracefully stop this HTTP Server.
		/// </summary>
		public override void Stop()
		{
			Working = false;
			Log.BeginTime = DateTime.Now;
			Log.WriteLine(true, true, "Shutdown server...");
			if (Listener != null)
			{
				Listener.Stop();
			}
			Log.WriteLine(true, true, "Server stopped.");
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
			catch (Exception ex)
			{
				Logger.WriteLine("Broken request ({0}): {1}. Aborted.", "unknown URL", ex.Message);
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
			StreamReader clientStreamReader = new(clientStream);

			//read the first line HTTP command
			string HttpCommand = clientStreamReader.ReadLine();
			if (string.IsNullOrEmpty(HttpCommand))
			{
				clientStreamReader.Close();
				clientStream.Close();
				Client.Close();
				Logger.WriteLine("<Close empty connection.");
				return;
			}

			//break up the line into three components & process it
			string[] HttpCommandParts = HttpCommand.Split(' ');
			if (HttpCommandParts.Length != 3 || HttpCommandParts[2].Length != 8)
			{
				clientStreamReader.Close();
				clientStream.Close();
				Client.Close();
				Logger.WriteLine("<Dropped: Non-HTTP connection: {0}", HttpCommand);
				return;
			}
			HttpRequest Request = new()
			{
				HttpMethod = HttpCommandParts[0],
				RawUrl = HttpCommandParts[1],
				ProtocolVersionString = HttpCommandParts[2],
				Headers = new(),
				RemoteEndPoint = new IPEndPoint(0, 0), //find how to get it
				LocalEndPoint = new IPEndPoint(0, 0),  //too
				IsSecureConnection = false
			};
			if (Request.RawUrl.ToLower().StartsWith("http://")
			|| Request.RawUrl.ToLower().StartsWith("https://")
			|| Request.RawUrl.ToLower().StartsWith("ftp://")
			|| Request.RawUrl.ToLower().StartsWith("gopher://")
			|| Request.RawUrl.ToLower().StartsWith("wais://"))
			{ Request.Url = new Uri(Request.RawUrl); }
			else if (Request.RawUrl.StartsWith('/'))
			{ Request.Url = new Uri("http://" + Variables["Proxy"] + Request.RawUrl); }
			else
			{ Request.Url = new Uri("http://" + Variables["Proxy"] + "/" + Request.RawUrl); }

			//load all request headers
			string HttpHeaderLine = null;
			while (true)
			{
				HttpHeaderLine = clientStreamReader.ReadLine();

				if (string.IsNullOrWhiteSpace(HttpHeaderLine)) break;
				Request.Headers.Add(HttpHeaderLine.Substring(0, HttpHeaderLine.IndexOf(": ")), HttpHeaderLine.Substring(HttpHeaderLine.IndexOf(": ") + 2));

				if (HttpHeaderLine == "Connection: keep-alive" || HttpHeaderLine == "Proxy-Connection: keep-alive")
					Request.KeepAlive = true;
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
				Console.Title = string.Format("WebOne (silent) @ {0}:{1} [{2}]", ConfigFile.DefaultHostName, Port, Load, Variables["WOVer"]);
			else
				Console.Title = string.Format("WebOne v{3} @ {0}:{1} [{2}]", ConfigFile.DefaultHostName, Port, Load, Variables["WOVer"]);
		}
	}
}
