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
		int Port = 80;
		bool Work = true;
		private static HttpListener _listener;
		int Load = 0;


		/// <summary>
		/// Start a new HTTP Listener
		/// </summary>
		/// <param name="port"></param>
		public HTTPServer(int port) {
			Console.WriteLine("Starting server...");
			Port = port;
			_listener = new HttpListener();
			_listener.Prefixes.Add("http://*:" + Port + "/");
			_listener.Start();
			_listener.BeginGetContext(ProcessRequest, null);
			Console.WriteLine("Listening for HTTP 1.x on port {0}.", port);
			UpdateStatistics();
			while (Work) { Console.Read(); }
		}


		/// <summary>
		/// Process a HTTP request (callback for HttpListener)
		/// </summary>
		/// <param name="ar">Something from HttpListener</param>
		private void ProcessRequest(IAsyncResult ar)
		{
			Load++;
			UpdateStatistics();
			DateTime BeginTime = DateTime.UtcNow;
			#if DEBUG
			Console.WriteLine("{0}\tGot a request.", BeginTime.ToString("HH:mm:ss.fff"));
			#endif
			HttpListenerContext ctx = _listener.EndGetContext(ar);
			_listener.BeginGetContext(ProcessRequest, null);
			HttpListenerRequest req = ctx.Request;
			#if DEBUG
			Console.WriteLine("{0}\t>{1} {2}", GetTime(BeginTime), req.HttpMethod, req.Url);
			#else
			Console.WriteLine("{0}\t>{1} {2}", BeginTime.ToString("HH:mm:ss.fff"), req.HttpMethod, req.Url);
			#endif

			HttpListenerResponse resp = ctx.Response;
			Transit Tranzit = new Transit(req, resp, BeginTime);

			Console.WriteLine("{0}\t<Done.", GetTime(BeginTime));
			Load--;
			UpdateStatistics();
		}


		/// <summary>
		/// Display count of open requests in app's titlebar
		/// </summary>
		private void UpdateStatistics() {
			Console.Title = string.Format("WebOne @ {0}:{1} [{2}]", Environment.MachineName, Port, Load);
		}
	}
}
