using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Fake HTTPS server, which simulates a remote HTTPS server.
	/// </summary>
	class HttpSecureServer
	{
		Stream ClientStreamReal;
		WolfSslServerStream ClientStreamTunnel;
		X509Certificate2 Certificate;
		HttpRequest RequestReal;
		HttpResponse ResponseReal;
		LogWriter Logger;

		/// <summary>
		/// Start fake HTTPS server emulation for already established NetworkStream.
		/// </summary>
		public HttpSecureServer(HttpRequest Request, HttpResponse Response, LogWriter Logger)
		{
			// Get outer HTTP/1.1 part of tunnel
			RequestReal = Request;
			ResponseReal = Response;
			ClientStreamReal = Request.InputStream;
			this.Logger = Logger;
#if DEBUG
			Logger.WriteLine(">SSL: {0}", Request.RawUrl);
#endif

			if (!ConfigFile.SslEnable) return;

			//Certificate = RootCertificate; //temporary - WebOne CA certificate

			// Make a fake certificate for current domain, signed by CA certificate
			string HostName = RequestReal.RawUrl.Substring(0, RequestReal.RawUrl.IndexOf(":"));
			Certificate = CertificateUtil.MakeChainSignedCert("CN=" + HostName, RootCertificate, ConfigFile.SslHashAlgorithm);
		}

		/// <summary>
		/// Accept an incoming "connection" by establishing SSL tunnel &amp; start data exchange.
		/// </summary>
		public void Accept()
		{
			// Check if HTTPS is not disabled in webone.conf
			if (!ConfigFile.SslEnable)
			{
				Log.WriteLine("<Secure CONNECT is disabled.");
				string Html =
				"<HTML><HEAD>" +
				"<TITLE>WebOne: SSL is not supported</TITLE></HEAD>" +
				"<BODY><P>Incoming HTTPS connections support is disabled on this server.</P>" + GetInfoString() + "</BODY></HTML>";

				if (File.Exists(ConfigFile.ContentDirectory + "/Err-SslDisabled.htm"))
				{
					Html = File.ReadAllText(ConfigFile.ContentDirectory + "/Err-SslDisabled.htm");
				}

				byte[] Buffer = (System.Text.Encoding.Default).GetBytes(Html);
				try
				{
					ResponseReal.StatusCode = 501;
					ResponseReal.ProtocolVersion = new Version(1, 1);

					ResponseReal.ContentType = "text/html";
					ResponseReal.ContentLength64 = Buffer.Length;
					ResponseReal.SendHeaders();
					ResponseReal.OutputStream.Write(Buffer, 0, Buffer.Length);
					ResponseReal.Close();
				}
				catch (Exception ex)
				{
					if (!ConfigFile.HideClientErrors)
						Log.WriteLine("<!Cannot return 501. {2}: {3}", null, 302, ex.GetType(), ex.Message);
				}
				return;
			}

			// Answer that this proxy supports HTTPS
			ResponseReal.ProtocolVersion = new Version(1, 1);
			ResponseReal.StatusCode = 200; //better be "HTTP/1.1 200 Connection established", but "HTTP/1.1 200 OK" is OK too
			ResponseReal.AddHeader("Via", "1.1 WebOne/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
			ResponseReal.SendHeaders();

			try
			{
				// Perform SSL handshake (via wolfSSL, not .NET's OpenSSL-backed SslStream --
				// see WolfSslServerStream for why) and establish an inner tunnel
				byte[] certDer = Certificate.Export(X509ContentType.Cert);
				byte[] keyDer = Certificate.GetRSAPrivateKey().ExportRSAPrivateKey();

				ClientStreamTunnel = new WolfSslServerStream(ClientStreamReal, false);
				ClientStreamTunnel.AuthenticateAsServer(certDer, keyDer);
			}
			catch (Exception HandshakeEx)
			{
				string err = HandshakeEx.Message;
				if (HandshakeEx.InnerException != null) err = HandshakeEx.InnerException.Message;
				Logger.WriteLine("!SSL Handshake failed: {0} ({1})", err, HandshakeEx.HResult);
				ClientStreamReal.Close();
				return;
			}

			// Work with unencrypted HTTP inside tunnel
			try
			{
				LogWriter Logger = new();
				HttpUtil.SslClient sslc = new();
				sslc.Stream = ClientStreamTunnel;
				sslc.LocalEndPoint = RequestReal.LocalEndPoint;
				sslc.RemoteEndPoint = RequestReal.RemoteEndPoint;
				sslc.TargetServer = RequestReal.RawUrl;
				sslc.Encrypting = string.Format("{0} with {1}",
				ClientStreamTunnel.SslProtocolName,
				ClientStreamTunnel.CipherName);
				Logger.WriteLine(" SSL: {0}", sslc.Encrypting);
				new HttpRequestProcessor().ProcessClientRequest(sslc, Logger, RequestReal.RawUrl.Split(':')[0]);
			}
			catch (IOException)
			{
				// Unexpected close (hello, Kaspersky AV traffic scan)
				if (!ConfigFile.HideClientErrors) Logger.WriteLine(" SSL Client disconnected.");
				ClientStreamTunnel.Close();
			}
			catch (Exception ex)
			{
				Logger.WriteLine("SslOops: {0}.", ex.Message);
				try { ClientStreamTunnel.Close(); } catch { }
			}
			Logger.WriteLine("<Close SSL.");
		}
	}
}
