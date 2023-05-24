using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
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
		SslStream ClientStreamTunnel;
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

			Logger.WriteLine(">SSL: {0}", Request.RawUrl);

			// Get WebOne CA certificate
			//Certificate = new X509Certificate2(ConfigFile.SslCertificate); //if DER format
			//Certificate = new X509Certificate2(X509Certificate2.CreateFromPemFile(ConfigFile.SslCertificate, ConfigFile.SslPrivateKey).Export(X509ContentType.Pkcs12)); //if PEM format
			Certificate = RootCertificate; //temporary

			// Make a fake certificate for current domain
			// (in future)
			// see https://github.com/wheever/ProxHTTPSProxyMII/blob/master/CertTool.py#L58 for ideas
			/*
			//Certificate = new X509Certificate2(ConfigFile.SslCertificate, "");
			using RSA rsa = RSA.Create();
			CertificateRequest certRequest = new CertificateRequest("cn=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

			// We're just going to create a temporary certificate, that won't be valid for long
			X509Certificate2 certificate = certRequest.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddDays(1));

			// export the private key
			string privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey(), Base64FormattingOptions.InsertLineBreaks);

			//File.WriteAllText(keyFilename, KEY_HEADER + privateKey + KEY_FOOTER);

			// Export the certificate
			byte[] exportData = certificate.Export(X509ContentType.Cert);

			string crt = Convert.ToBase64String(exportData, Base64FormattingOptions.InsertLineBreaks);
			//File.WriteAllText(certFilename, CRT_HEADER + crt + CRT_FOOTER);

			Certificate = certificate;//.CopyWithPrivateKey(rsa);*/
		}

		/// <summary>
		/// Accept an incoming "connection" by establishing SSL tunnel &amp; start data exchange.
		/// </summary>
		public void Accept()
		{
			// Answer that this proxy supports HTTPS
			ResponseReal.ProtocolVersionString = "HTTP/1.1";
			ResponseReal.StatusCode = 200; //better be "HTTP/1.0 200 Connection established", but "HTTP/1.1 200 OK" is OK too
			ResponseReal.AddHeader("Via", "HTTPS/1.0 WebOne/" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
			ResponseReal.SendHeaders();

			// Perform SSL handshake and establish a inner tunnel
			ClientStreamTunnel = new SslStream(ClientStreamReal, false);
			//ClientStream.AuthenticateAsServer(Certificate, false, SslProtocols.Tls | SslProtocols.Ssl3 | SslProtocols.Ssl2, true);
			ClientStreamTunnel.AuthenticateAsServer(Certificate, false, SslProtocols.Default, false);

			// Work with unencrypted HTTP inside tunnel
			try
			{
				// UNDONE: rewrite and move to separate .cs file, common used with HttpServer2
				/* Requirements:
				 * Must support both HttpServer2 and HttpSecureServer.
				 * Must check for correct HTTP look.
				 * If called from SSL, invalid (not looking as HTTP) traffic should be redirected true transparently to remote server.
				 *    - assuming use with something like IRCS, POP3-SSL, SMTP-SSL, IMAP-SSL, etc.
				 * If called already from SSL, ban CONNECT method.
				 *    - security reasons (don't provocate police).
				 */

				LogWriter Logger = new();
#if DEBUG
				Logger.WriteLine(" Got a Secure request.");
#endif
				// Read text part of HTTP request (until double line feed).
				BinaryReader br = new(ClientStreamTunnel);
				List<char> rqChars = new();
				while (true)
				{
					rqChars.Add(br.ReadChar());

					if (rqChars.Count < 2) continue;
					if (rqChars[rqChars.Count - 1] == '\r')
					{
						if (rqChars[rqChars.Count - 3] == '\r' && rqChars[rqChars.Count - 2] == '\n')
						{
							rqChars.Add(br.ReadChar());
							break;
						}
					}
				}

				// Process HTTP command and headers.
				HttpRequest Request = null;
				bool IsCommand = true;
				foreach (string HttpRequestLine in new string(rqChars.ToArray()).Split("\r\n"))
				{
					if (string.IsNullOrWhiteSpace(HttpRequestLine)) continue;
					if (IsCommand)
					{
						// First line - HTTP command.
						if (string.IsNullOrEmpty(HttpRequestLine))
						{
							ClientStreamTunnel.Close();
							ClientStreamReal.Close();
							Logger.WriteLine("<Close empty Secure connection.");
							return;
						}
						string[] HttpCommandParts = HttpRequestLine.Split(' ');
						if (HttpRequestLine.StartsWith("CONNECT"))
						{
							ClientStreamTunnel.Close();
							ClientStreamReal.Close();
							Logger.WriteLine("<Dropped: Attempt to use other HTTPS-Proxy: {0}", HttpRequestLine);
							return;
						}
						else if (HttpCommandParts.Length != 3 || HttpCommandParts[2].Length != 8)
						{
							ClientStreamTunnel.Close();
							ClientStreamReal.Close();
							Logger.WriteLine("<Dropped: Non-HTTP(S) connection: {0}", HttpRequestLine);
							return;
						}

						// First line is valid, start work with the Request.
						Request = new()
						{
							HttpMethod = HttpCommandParts[0],
							RawUrl = HttpCommandParts[1],
							ProtocolVersionString = HttpCommandParts[2],
							Headers = new(),
							RemoteEndPoint = new(0, 0), // Client.Client.RemoteEndPoint as IPEndPoint,
							LocalEndPoint = new(0, 0), // Client.Client.LocalEndPoint as IPEndPoint,
							IsSecureConnection = true
						};
						if (Request.RawUrl.Contains("://"))
						{ Request.Url = new Uri(Request.RawUrl); }
						else if (Request.RawUrl.StartsWith('/'))
						{ Request.Url = new Uri("http://" + Variables["Proxy"] + Request.RawUrl); }
						else
						{ Request.Url = new Uri("http://" + Variables["Proxy"] + "/" + Request.RawUrl); }

						IsCommand = false;
						continue;
					}
					else
					{
						// Other lines - request headers, load all of them.
						if (string.IsNullOrWhiteSpace(HttpRequestLine)) continue;
						Request.Headers.Add(HttpRequestLine.Substring(0, HttpRequestLine.IndexOf(": ")), HttpRequestLine.Substring(HttpRequestLine.IndexOf(": ") + 2));

						if (HttpRequestLine == "Connection: keep-alive" || HttpRequestLine == "Proxy-Connection: keep-alive")
							Request.KeepAlive = true;
					}
				}

				if (Request == null)
				{
					ClientStreamTunnel.Close();
					ClientStreamReal.Close();
					Logger.WriteLine("<Dropped (unknown why).");
					return;
				}

				if (Request.Headers["Content-Length"] != null)
				{
					// If there's a payload, convert it to a HttpRequestContentStream.
					Request.InputStream = new HttpRequestContentStream(ClientStreamTunnel, int.Parse(Request.Headers["Content-Length"]));

					/*
					 * SslStream is not suitable for HTTP(S) request bodies. It have no length, and read operation is endless.
					 * What is suitable - .NET's internal HttpRequestStream and ChunkedInputStream:HttpRequestStream.
					 * See .NET source: https://source.dot.net/System.Net.HttpListener/R/d562e26091bc9f8d.html
					 * They are reading traffic only until HTTP Content-Length or last HTTP Chunk into a correct .NET Stream format.
					 * 
					 * WebOne.HttpRequestContentStream is a very lightweight alternative to System.Net.HttpRequestStream.
					 */
				}
				else
				{
					// No payload in request - original NetworkStream is suitable.
					Request.InputStream = ClientStreamTunnel;
				}

				HttpResponse Response = new(ClientStreamTunnel);
				HttpTransit Transit = new(Request, Response, Logger);
				Logger.WriteLine(">(SSL) {0} {1} ({2})", Request.HttpMethod, Request.RawUrl, Transit.GetClientIdString());
				Transit.ProcessTransit();

				if (false && Request.KeepAlive && Response.KeepAlive)
				{
					Logger.WriteLine("<Done.");
					//ProcessClientRequest(Client, new());
				}
				else
				{
					//Client.Close();
					Logger.WriteLine("<Done (connection close).");
				}

				ClientStreamTunnel.Close();
			}
			catch (IOException)
			{
				// Unexpected close (hello, Kaspersky AV traffic scan)
				Logger.WriteLine(" SSL Client disconnected");
				ClientStreamTunnel.Close();
			}
			Logger.WriteLine("<Close SSL.");
		}
	}
}
