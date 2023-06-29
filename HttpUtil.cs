using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Utilities for processing HTTP traffic.
	/// </summary>
	static class HttpUtil
	{
		// GC does not cleaning up static classes. Place here only lightweight on RAM usage code!

		/// <summary>
		/// Emulates client connections for SSL/TLS network services. Similar to <seealso cref="TcpClient"/>, but is working over <seealso cref="SslStream"/>.
		/// </summary>
		public struct SslClient
		{
			/// <summary>
			/// The stream of content transferred through SSL/TLS layer.
			/// </summary>
			public SslStream Stream;
			/// <summary>
			/// Specifies local end point (Client IP).
			/// </summary>
			public IPEndPoint LocalEndPoint;
			/// <summary>
			/// Specifies remote end point (this Proxy IP).
			/// </summary>
			public IPEndPoint RemoteEndPoint;
			/// <summary>
			/// Specifies SSL server host name and port, specified by client.
			/// </summary>
			public string TargetServer;
			/// <summary>
			/// Grade of SSL encrypting
			/// </summary>
			public string Encrypting;
		}

		/// <summary>
		/// Kind of content requested by client.
		/// </summary>
		public enum RequestKind
		{
			/*
			GET /123
			Host: example.com
			-> http://example.com/123   StandardRemote

			GET /123
			Host: 127.0.0.1
			-> http://127.0.0.1/123     StandardLocal

			GET /123
			no host, assuming 127.0.0.1
			-> http://127.0.0.1/123     StandardLocal

			GET /http://example.com/123
			Host: 
			-> http://example.com/123   AlternateProxy

			GET /http://example.com/123
			no host, assuming 127.0.0.1
			-> http://example.com/123   AlternateProxy

			GET /123
			Host: 127.0.0.1
			Referer: http://127.0.0.1/http://example.ru/p/
			-> http://example.ru/p/123  DirtyAlternateProxy

			GET http://example.com/123
			Host: example.com
			-> http://example.com/123   StandardProxy

			GET ftp://example.com/pub
			Host: example.com
			-> ftp://example.com/pub    StandardProxy

			GET ftp://example.com/pub
			no host, assuming 127.0.0.1
			-> ftp://example.com/pub    StandardProxy

			CONNECT example.com:443
			Host: example.com
			-> null                     StandardSslProxy

			CONNECT example.com:443
			no host, assuming 127.0.0.1
			-> null                     StandardSslProxy
			 */

			/// <summary>
			/// Standard HTTP request to other server.
			/// </summary>
			StandardRemote,
			/// <summary>
			/// Standard HTTP request to this server.
			/// </summary>
			StandardLocal,
			/// <summary>
			/// Standard HTTP request to this server; "Alternate Proxy Mode".
			/// </summary>
			AlternateProxy,
			/// <summary>
			/// Standard HTTP request to this server; "Alternate Proxy Mode", relative URLs ("dirty").
			/// </summary>
			DirtyAlternateProxy,
			/// <summary>
			/// Standard request to a HTTP proxy server.
			/// </summary>
			StandardProxy,
			/// <summary>
			/// Standard connection request to a SSL-enabled server over a HTTP proxy server.
			/// </summary>
			StandardSslProxy

			//note: "Alternate proxy mode" = "Local proxy" (terminology used in older versions of WebOne)
		}

		/// <summary>
		/// Get kind of content requested by client.
		/// </summary>
		/// <param name="RawUrl">URL from HTTP Command.</param>
		/// <param name="HostHeader">"Host" header value (if any).</param>
		/// <param name="IsCONNECT">Is "CONNECT" method used.</param>
		/// <returns>Kind of content requested by client.</returns>
		public static RequestKind GetKindOfRequest(string RawUrl, string HostHeader = null, string RefererHeader = null, bool IsCONNECT = false)
		{
			string Host = HostHeader ?? "127.0.0.1";
			int Port = ConfigFile.Port;

			// Detect port number (if any)
			if (!Host.StartsWith("/") && Host.Contains(":"))
			{
				Port = int.Parse(Host.Substring(Host.LastIndexOf(":") + 1));
				Host = Host.Substring(0, Host.LastIndexOf(":"));
			}

			if (RawUrl.StartsWith("/"))
			{
				// Standard* or Dirty
				if (IsLocalhost(Host, Port)) //check Host name and Port number
				{
					// Target is this server, so StandardLocal or AlternateProxy or DirtyAlternateProxy
					if (RawUrl.ToLower().StartsWith("/http:") || RawUrl.ToLower().StartsWith("/https:") || RawUrl.ToLower().StartsWith("/ftp:")) 
					return RequestKind.AlternateProxy;

					if (RefererHeader != null)
					{
						// There's a Referer header, so check for possible DirtyAlternateMode
						if (string.IsNullOrEmpty(RefererHeader) && RefererHeader.Contains("/http://")) return RequestKind.DirtyAlternateProxy;
						if (string.IsNullOrEmpty(RefererHeader) && RefererHeader.Contains("/https://")) return RequestKind.DirtyAlternateProxy;
					}

					return RequestKind.StandardLocal;
				}
				else
				{
					// Target is other server, so StandardRemote
					return RequestKind.StandardRemote;
				}
			}
			else
			{
				// Proxy or StandardSslProxy
				if (RawUrl.Contains("://")) return RequestKind.StandardProxy;
				else if (IsCONNECT) return RequestKind.StandardSslProxy;
				else throw new Exception("Cannot guess kind of requested target.");
			}
		}

		/// <summary>
		/// Find if the <paramref name="Host"/> refers to the local machine.
		/// </summary>
		/// <param name="Host">Host name or IP v4/v6 address to be checked.</param>
		/// <param name="Port">Port to be checked.</param>
		/// <returns>True if local machine; False if another machine.</returns>
		public static bool IsLocalhost(string Host, int Port)
		{
			if (Host.Contains(':') && !Host.EndsWith("]")) //"ipv4:port" or "[i::p::v::6]:port" but not "[i::p::v::6]"
			throw new ArgumentException("Forget to split 'host:port' pair.", nameof(Host));
			return CheckString(Host, GetLocalHostNames(), true) && Port == ConfigFile.Port;
		}

		/// <summary>
		/// Get all possible aliases for 127.0.0.1.
		/// </summary>
		/// <returns>List of IP addresses and host names, related to this machine.</returns>
		public static List<string> GetLocalHostNames()
		{
			List<string> localhosts = new();
			foreach (IPAddress address in GetLocalIPAddresses())
			{
				if (address.AddressFamily == AddressFamily.InterNetworkV6)
					localhosts.Add("[" + address.ToString() + "]");
				else
					localhosts.Add(address.ToString());
			}

			localhosts.Add("wpad");
			localhosts.Add("localhost");
			localhosts.Add(Environment.MachineName);
			localhosts.Add(ConfigFile.DefaultHostName);
			localhosts.AddRange(ConfigFile.HostNames);
			return localhosts;
		}
	}
}
