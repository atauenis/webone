using System;
using System.Net;
using System.Text;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// HTTP/1.1 Listener and Server (HttpListener-based)
	/// </summary>
	class HttpServer1 : HttpServer
	{
		/* This is the old version of HTTP Server, used from WebOne 0.8.5 to 0.15.3.
		 * Pluses:   very stable, very fast, very compatible, professional-made, easy to maintain.
		 * Minuses:  doesn't accept CONNECT method, doesn't accept non-HTTP URIs (CERN-style requests), wants 'Host:' header, rely on Microsoft-HTTPAPI.
		 * In v0.16.0 and up, kept for compatibility purposes.
		 */

		private int Port;
		private static HttpListener _listener = new();
		private LogWriter Log = new();

		/// <summary>
		/// Status of this HTTP Listener &amp; Server
		/// </summary>
		public override bool Working { get; set; }

		/// <summary>
		/// Initizlize a HTTP Listener &amp; Server
		/// </summary>
		/// <param name="port">TCP Port to listen on</param>
		public HttpServer1(int port) : base(port)
		{
			Port = port;
			Working = false;
		}

		/// <summary>
		/// Start this HTTP Listener &amp; Server
		/// </summary>
		public override void Start()
		{
			Log.WriteLine(true, false, "Starting server...");
			//if (_listener == null) _listener = new HttpListener();
			try { int test = _listener.Prefixes.Count; }
			catch { _listener = new HttpListener(); /*initialize HttpListener if it is not ready*/ }
			_listener.Prefixes.Add("http://*:" + Port + "/");
			_listener.Start();
			_listener.BeginGetContext(ProcessRequest, null);
			Working = true;
			Log.WriteLine(true, false, "Listening for HTTP 1.x on port {0}.", Port);
			UpdateStatistics();
		}

		/// <summary>
		/// Gracefully stop this HTTP Listener &amp; Server
		/// </summary>
		public override void Stop()
		{
			Working = false;
			Log.BeginTime = DateTime.Now;
			Log.WriteLine(true, true, "Shutdown server...");
			if (_listener != null)
			{
				if (_listener.IsListening) _listener.Stop();
				_listener.Prefixes.Clear();
			}
			Log.WriteLine(true, true, "Server stopped.");
		}


		/// <summary>
		/// Process a HTTP request (callback for HttpListener)
		/// </summary>
		/// <param name="ar">Something from HttpListener</param>
		private void ProcessRequest(IAsyncResult ar)
		{
			if (!Working) return;
			Load++;
			UpdateStatistics();
			LogWriter Logger = new();
#if DEBUG
			Logger.WriteLine("Got a request.");
#endif

			string RawUrl = "";
			try
			{
				HttpListenerContext ctx = _listener.EndGetContext(ar);
				_listener.BeginGetContext(ProcessRequest, null);
				HttpListenerRequest req = ctx.Request;

				RawUrl = req.RawUrl;
				string ClientId = req.RemoteEndPoint.Address.ToString();
				if (req.Headers["Proxy-Authorization"] != null && req.Headers["Proxy-Authorization"].StartsWith("Basic "))
				{
					string ClientUserName = null;
					ClientUserName = Encoding.Default.GetString(Convert.FromBase64String(req.Headers["Proxy-Authorization"][6..])); //6 = "Basic "
					ClientUserName = ClientUserName.Substring(0, ClientUserName.IndexOf(":"));
					ClientId = ClientUserName + ", " + ClientId;
				}
				Logger.WriteLine(">{0} {1} ({2})", req.HttpMethod, req.RawUrl, ClientId);

				HttpListenerResponse resp = ctx.Response;
				HttpTransit Tranzit = new HttpTransit(req, resp, Logger);
				Logger.WriteLine("<Done.");
			}
			catch (Exception ex)
			{
				Logger.WriteLine("Broken request ({0}): {1}. Aborted.", RawUrl, ex.Message);
			}

			Load--;
			UpdateStatistics();
		}


		/// <summary>
		/// Display count of open requests in app's titlebar
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
