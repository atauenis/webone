using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using static WebOne.Program;
namespace WebOne
{
	/// <summary>
	/// Fake HTTPS Proxy Server for non-HTTP/HTTPS protocols tunneling
	/// </summary>
	class HttpSecureNonHttpServer
	{
		Stream ClientStream;
		Stream RemoteStream;
		X509Certificate2 Certificate = null;
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
			ClientStream = Request.InputStream;
			this.Logger = Logger;

			if (UseSsl)
			{
				if (!ConfigFile.SslEnable) return;
				string HostName = RequestReal.RawUrl.Substring(0, RequestReal.RawUrl.IndexOf(":"));
				Certificate = CertificateUtil.MakeChainSignedCert("CN=" + HostName, RootCertificate, ConfigFile.SslHashAlgorithm);
			}
		}

		/// <summary>
		/// Accept an incoming "connection" by establishing tunnel &amp; start data exchange.
		/// </summary>
		public void Accept()
		{
			if (ConfigFile.AllowNonHttpsCONNECT)
			{
				// Answer that this proxy supports CONNECT method
				ResponseReal.ProtocolVersionString = "HTTP/1.1";
				ResponseReal.StatusCode = 200; //better be "HTTP/1.0 200 Connection established", but "HTTP/1.1 200 OK" is OK too
				ResponseReal.SendHeaders();
			}
			else
			{
				// Reject connection request
				string OnlyHTTPS = "This proxy is performing only HTTP and HTTPS tunneling.";
				ResponseReal.ProtocolVersionString = "HTTP/1.1";
				ResponseReal.StatusCode = 502;
				ResponseReal.ContentType = "text/plain";
				ResponseReal.ContentLength64 = OnlyHTTPS.Length;
				ResponseReal.SendHeaders();
				ResponseReal.OutputStream.Write(System.Text.Encoding.Default.GetBytes(OnlyHTTPS), 0, OnlyHTTPS.Length);
				ResponseReal.Close();
				Logger.WriteLine("<Not a HTTPS CONNECT, goodbye.");
				return;
			}

			Logger.WriteLine(">Non-HTTP: {0}", RequestReal.RawUrl);
			string[] Parts = RequestReal.RawUrl.Split(":");
			if (Parts.Length != 2) throw new Exception("Invalid `domain:port` pair supplied for CONNECT method.");
			if (!int.TryParse(Parts[1], out int PortNumber)) throw new Exception("Invalid port number supplied for CONNECT method.");

			// Perform an SSL/TLS handshake if need
			if (Certificate != null)
			{
				if (!ConfigFile.SslEnable)
				{
					Logger.WriteLine("<SSL is disabled, goodbye.");
					return;
				}

				SslStream ClientStreamTunnel = new(RequestReal.InputStream, true);
				try
				{
					ClientStreamTunnel.AuthenticateAsServer(Certificate, false, ConfigFile.SslProtocols, false);
					ClientStream = ClientStreamTunnel;
				}
				catch (Exception HandshakeEx)
				{
					string err = HandshakeEx.Message;
					if (HandshakeEx.InnerException != null) err = HandshakeEx.InnerException.Message;
					Logger.WriteLine("!SSL client handshake failed: {0} ({1})", err, HandshakeEx.HResult);
					ClientStream.Close();
					return;
				}
			}

			// Establish tunnel
			TcpClient TunnelToRemote = new();
			try
			{
				TunnelToRemote.Connect(Parts[0], PortNumber);

				if (Certificate != null)
				{
					RemoteStream = new SslStream(TunnelToRemote.GetStream(), true);
					(RemoteStream as SslStream).AuthenticateAsClient(RequestReal.RawUrl.Substring(0, RequestReal.RawUrl.IndexOf(":")));
					Logger.WriteLine(" Secure tunnel established.", RequestReal.RawUrl);
				}
				else
				{
					RemoteStream = TunnelToRemote.GetStream();
					Logger.WriteLine(" Tunnel established.", RequestReal.RawUrl);
				}
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
				else if (ClientStream is SslStream)
				{
					TunnelAlive = (RequestReal.InputStream as NetworkStream).Socket.Connected;
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
