using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
namespace WebOne
{
	/// <summary>
	/// Fake HTTPS Proxy Server for non-HTTP/HTTPS protocols tunneling
	/// </summary>
	class HttpSecureNonHttpServer
	{
		Stream ClientStreamReal;
		SslStream ClientStreamTunnel;
		Stream ClientStream;
		Stream RemoteStream;
		//X509Certificate2 Certificate;
		HttpRequest RequestReal;
		HttpResponse ResponseReal;
		LogWriter Logger;

		/// <summary>
		/// Start fake HTTPS proxy server emulation for already established NetworkStream.
		/// </summary>
		public HttpSecureNonHttpServer(HttpRequest Request, HttpResponse Response, bool UseSsl, LogWriter Logger)
		{
			RequestReal = Request;
			ResponseReal = Response;
			ClientStreamReal = Request.InputStream;
			if (UseSsl) ClientStream = ClientStreamTunnel;
			else ClientStream = ClientStreamReal;
			this.Logger = Logger;

			if (UseSsl)
			{
				//todo: see HttpSecureServer.cs for code example
				throw new NotImplementedException("Sorry, TLS support for non-HTTPS is coming soon.");
			}

		}

		/// <summary>
		/// Accept an incoming "connection" by establishing tunnel &amp; start data exchange.
		/// </summary>
		public void Accept()
		{
			// Answer that this proxy supports CONNECT method
			ResponseReal.ProtocolVersionString = "HTTP/1.1";
			ResponseReal.StatusCode = 200; //better be "HTTP/1.0 200 Connection established", but "HTTP/1.1 200 OK" is OK too
			//ResponseReal.AddHeader("Via", "1.1 WebOne/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
			ResponseReal.SendHeaders();

			Logger.WriteLine(">Non-HTTP: {0}", RequestReal.RawUrl);
			string[] Parts = RequestReal.RawUrl.Split(":");
			if (Parts.Length != 2) throw new Exception("Invalid `domain:port` pair supplied for CONNECT method.");
			if (!int.TryParse(Parts[1], out int PortNumber)) throw new Exception("Invalid port number supplied for CONNECT method.");

			// Establish SSL handshake if need
			if (ClientStream is SslStream) throw new NotImplementedException("Saying again: TLS support for non-HTTPS is coming soon.");

			// Establish tunnel
			TcpClient TunnelToRemote = new();
			try
			{
				TunnelToRemote.Connect(Parts[0], PortNumber);
				RemoteStream = TunnelToRemote.GetStream();
				Logger.WriteLine(" Tunnel established.", RequestReal.RawUrl);
			}
			catch (Exception ex)
			{
				//An error occured, try to return nice error message, some clients like KVIrc will display it
				Logger.WriteLine(" Connection failed: {0}.", ex.Message);
				try { new StreamWriter(ClientStream).WriteLine("The proxy server is unable to connect: " + ex.Message); }
				catch { };
				ClientStream.Close();
				return;
			}

			// Do routing
			bool TunnelAlive = true;
			try
			{
				BinaryReader BRclient = new(ClientStream);
				BinaryWriter BWclient = new(ClientStream);
				BinaryReader BRremote = new(RemoteStream);
				BinaryWriter BWremote = new(RemoteStream);

				new Task(() =>
				{
					try
					{
						while (true)
						{
							BWremote.Write(BRclient.ReadByte());
						}
					}
					catch { TunnelAlive = false; }
				}).Start();

				new Task(() =>
				{
					try
					{
						while (true)
						{
							BWclient.Write(BRremote.ReadByte());
						}
					}
					catch { TunnelAlive = false; }
				}).Start();
			}
			catch (Exception ex)
			{
				Logger.WriteLine(" Tunnel error: {0}. Closing.", ex.ToString());
				TunnelAlive = false;
			};

			// Wait while connecion is alive
			while (TunnelAlive)
			{
				System.Threading.Thread.Sleep(1000);
				if (ClientStream is NetworkStream)
				{
					TunnelAlive = (ClientStream as NetworkStream).Socket.Connected;
				}
				else
				{
					TunnelAlive = ClientStream.CanWrite;
				}
			};

			// All done, close
			if (TunnelToRemote.Connected)
			{
				TunnelToRemote.Close();
				Logger.WriteLine(" Connection to {0} closed.", RequestReal.RawUrl);
			}
			else Logger.WriteLine(" Connection to {0} lost.", RequestReal.RawUrl);
			ClientStream.Close();

			return;
		}
	}
}
