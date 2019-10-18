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
	class NewHTTPServer //will be renamed after removal of original HTTPServer.cs and Transit.cs
	{
		int Port = 80;
		bool Work = true;
		private static HttpListener _listener;


		/// <summary>
		/// Start a new HTTP Listener
		/// </summary>
		/// <param name="port"></param>
		public NewHTTPServer(int port) {
			Console.WriteLine("Starting server...");
			Port = port;
			_listener = new HttpListener();
			_listener.Prefixes.Add("http://*:" + Port + "/");
			_listener.Start();
			_listener.BeginGetContext(ProcessRequest, null);
			Console.WriteLine("Listening for HTTP 1.x on port {0}.", port);
			while (Work) { Console.Read(); }
		}


		/// <summary>
		/// Process a HTTP request (callback for HttpListener)
		/// </summary>
		/// <param name="ar">Something from HttpListener</param>
		private void ProcessRequest(IAsyncResult ar)
		{
			DateTime BeginTime = DateTime.UtcNow;
			Console.WriteLine("{0}\tGot a request.", BeginTime.ToString("HH:mm:ss.fff"));
			HttpListenerContext ctx = _listener.EndGetContext(ar);
			_listener.BeginGetContext(ProcessRequest, null);
			HttpListenerRequest req = ctx.Request;
			Console.WriteLine("{0}\t>{1} {2}", GetTime(BeginTime), req.HttpMethod, req.Url);

			HttpListenerResponse resp = ctx.Response;
			NewTransit Tranzit = new NewTransit(req, resp, BeginTime);

			Console.WriteLine("{0}\t<Done.", GetTime(BeginTime));
		}
	}
}
