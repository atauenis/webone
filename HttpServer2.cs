using System;

namespace WebOne
{
	/// <summary>
	/// HTTP/1.1 Listener and Server (TcpClient-based)
	/// </summary>
	class HttpServer2 : HttpServer
	{
		/* This will be new version of HTTP Listener/Server.
		 * Pluses:   will support in addition to basic HTTP/1.1 also CONNECT method and all possible URIs in requests.
		 * Minuses:  not written yet =)
		 * https://www.codeproject.com/Articles/93301/Implementing-a-Multithreaded-HTTP-HTTPS-Debugging
		 */
		public override bool Working { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public HttpServer2(int port) : base(port)
		{
			throw new NotImplementedException();
		}

		public override void Start()
		{
			throw new NotImplementedException();
		}

		public override void Stop()
		{
			throw new NotImplementedException();
		}

		//   UNDONE. See https://www.codeproject.com/Articles/93301/Implementing-a-Multithreaded-HTTP-HTTPS-Debugging for ideas.
		//     DONE: unseal HttpListenerRequest
		//     DONE: unseal HttpListenerResponse
		//           (both come from HttpListenerContext)
		//     DONE: edit HttpTransit to use unsealed universal editions of them
	}
}
