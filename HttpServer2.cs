using System;
using System.IO;
using System.Net.Sockets;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// HTTP/1.1 Listener and Server (TcpClient-based).
	/// </summary>
	class HttpServer2 : HttpServer
	{
		/* This is the new version of HTTP Listener/Server, made for WebOne 0.16+.
		 * Pluses:   almost full HTTP 0.9/1.0/1.1 support, including CONNECT method and GET to ftp:// addresses
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
			TcpClient Client = null;
			try
			{
				Client = Listener.EndAcceptTcpClient(ar);
				Listener.BeginAcceptTcpClient(ProcessConnection, null);
			}
			catch
			{
				Logger.WriteLine("Connection unexpectedly lost.");
				Load--;
				UpdateStatistics();
				return;
			}

			try
			{
				HttpUtil.ProcessClientRequest(Client, Logger);
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
