namespace WebOne
{
	/// <summary>
	/// Abstract HTTP Server (Listener)
	/// </summary>
	abstract class HttpServer
	{
		/// <summary>
		/// Status of this HTTP Listener &amp; Server
		/// </summary>
		public abstract bool Working { get; set; }

		/// <summary>
		/// Initizlize a HTTP Listener &amp; Server
		/// </summary>
		/// <param name="port">TCP Port to listen on</param>
		public HttpServer(int port) { }

		/// <summary>
		/// Start this HTTP Listener &amp; Server
		/// </summary>
		public abstract void Start();

		/// <summary>
		/// Gracefully stop this HTTP Listener &amp; Server
		/// </summary>
		public abstract void Stop();
	}
}
