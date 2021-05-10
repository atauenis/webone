using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Threading;
using static WebOne.Program;

namespace WebOne
{
/// <summary>
/// HTTP Listener and Server
/// </summary>
	class HTTPServer
	{
		private int Port;
		private static HttpListener _listener = new HttpListener();
		private LogWriter Log = new LogWriter();

		/// <summary>
		/// Status of this HTTP Listener
		/// </summary>
		public bool Working { get; private set; }

		/// <summary>
		/// Initizlize a HTTP Listener &amp; Server
		/// </summary>
		/// <param name="port"></param>
		public HTTPServer(int port) {
			Port = port;
			Working = false;
		}

		/// <summary>
		/// Start this HTTP Listener
		/// </summary>
		public void Start() {
			Log.WriteLine(true, false, "Starting server...");
			if (_listener == null) _listener = new HttpListener();
			_listener.Prefixes.Add("http://*:" + Port + "/");
			_listener.Start();
			_listener.BeginGetContext(ProcessRequest, null);
			Working = true;
			Log.WriteLine(true, false, "Listening for HTTP 1.x on port {0}.", Port);
			UpdateStatistics();
		}

		/// <summary>
		/// Gracefully stop this HTTP Listener
		/// </summary>
		public void Stop(){
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
			LogWriter Logger = new LogWriter();
#if DEBUG
			Logger.WriteLine("Got a request.");
#endif
			HttpListenerContext ctx = _listener.EndGetContext(ar);
			_listener.BeginGetContext(ProcessRequest, null);
			HttpListenerRequest req = ctx.Request;

			string ClientId = req.RemoteEndPoint.Address.ToString();
			if(req.Headers["Proxy-Authorization"] != null && req.Headers["Proxy-Authorization"].StartsWith("Basic "))
			{
				string ClientUserName = null;
				ClientUserName = Encoding.Default.GetString(Convert.FromBase64String(req.Headers["Proxy-Authorization"][6..])); //6 = "Basic "
				ClientUserName = ClientUserName.Substring(0, ClientUserName.IndexOf(":"));
				ClientId = ClientUserName + ", " + ClientId;
			}
			Logger.WriteLine(">{0} {1} ({2})", req.HttpMethod, req.RawUrl, ClientId);

			HttpListenerResponse resp = ctx.Response;
			Transit Tranzit = new Transit(req, resp, Logger);

			Logger.WriteLine("<Done.");
			Load--;
			UpdateStatistics();
		}


		/// <summary>
		/// Display count of open requests in app's titlebar
		/// </summary>
		private void UpdateStatistics() {
			if(DaemonMode)
				Console.Title = string.Format("WebOne (silent) @ {0}:{1} [{2}]", ConfigFile.DefaultHostName, Port, Load);
			else
				Console.Title = string.Format("WebOne @ {0}:{1} [{2}]", ConfigFile.DefaultHostName, Port, Load);
		}
	}
}
