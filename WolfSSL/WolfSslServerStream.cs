using System;
using System.IO;
using System.Runtime.InteropServices;
using wolfSSL.CSharp;

namespace WebOne
{
	/// <summary>
	/// TLS server stream backed directly by wolfSSL's native API, standing in for
	/// <see cref="System.Net.Security.SslStream"/> for client-facing (accept) TLS.
	/// .NET's own OpenSSL-backed SslStream on Linux can't be pointed at wolfSSL --
	/// its native crypto shim expects OpenSSL's own ABI/symbol surface, which
	/// wolfSSL's OpenSSL-compat layer only replicates at the source level, not the
	/// binary level (recompiling against wolfSSL's headers works; swapping .so files
	/// under an OpenSSL-linked binary does not). This class instead P/Invokes
	/// wolfSSL's native functions directly via the wolfSSL_CSharp wrapper, reading
	/// and writing through the wrapped <see cref="Stream"/> via wolfSSL's IO callback
	/// mechanism (SetIORecv/SetIOSend) rather than requiring a raw Socket.
	/// </summary>
	class WolfSslServerStream : Stream
	{
		private static readonly wolfssl.CallbackIORecv_delegate RecvDelegate = IORecv;
		private static readonly wolfssl.CallbackIOSend_delegate SendDelegate = IOSend;
		private static readonly object InitLock = new();
		private static bool Initialized = false;

		private readonly Stream Inner;
		private readonly bool LeaveInnerStreamOpen;
		private IntPtr Ctx = IntPtr.Zero;
		private IntPtr Ssl = IntPtr.Zero;

		/// <summary>TLS version string as reported by wolfSSL (e.g. "TLSv1", "SSLv3").</summary>
		public string SslProtocolName { get; private set; } = "";

		/// <summary>Negotiated cipher suite name as reported by wolfSSL.</summary>
		public string CipherName { get; private set; } = "";

		public WolfSslServerStream(Stream innerStream, bool leaveInnerStreamOpen = false)
		{
			Inner = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
			LeaveInnerStreamOpen = leaveInnerStreamOpen;

			lock (InitLock)
			{
				if (!Initialized)
				{
					wolfssl.Init();
					Initialized = true;
				}
			}
		}

		/// <summary>
		/// Perform the TLS server handshake using an in-memory certificate and
		/// private key (DER-encoded), matching WebOne's CertificateUtil which
		/// generates certificates on the fly and never writes them to disk.
		/// </summary>
		/// <param name="certDer">DER-encoded leaf certificate.</param>
		/// <param name="keyDer">DER-encoded RSA private key (PKCS#1).</param>
		/// <exception cref="AuthenticationException">Handshake or setup failure.</exception>
		public void AuthenticateAsServer(byte[] certDer, byte[] keyDer)
		{
			Ctx = wolfssl.CTX_new(wolfssl.usev23_server());
			if (Ctx == IntPtr.Zero)
				throw new AuthenticationException("wolfSSL: failed to create CTX");

			// The "flexible" v23 method otherwise refuses genuine SSLv3 ClientHellos
			// with a record-layer version error, despite the library being built
			// with --enable-sslv3.
			wolfssl.CTX_SetMinVersion(Ctx, wolfssl.WOLFSSL_SSLV3);

			wolfssl.SetIORecv(Ctx, RecvDelegate);
			wolfssl.SetIOSend(Ctx, SendDelegate);

			if (wolfssl.CTX_use_certificate_buffer(Ctx, certDer, wolfssl.SSL_FILETYPE_ASN1) != wolfssl.SUCCESS)
				throw new AuthenticationException("wolfSSL: failed to load certificate");

			if (wolfssl.CTX_use_PrivateKey_buffer(Ctx, keyDer, wolfssl.SSL_FILETYPE_ASN1) != wolfssl.SUCCESS)
				throw new AuthenticationException("wolfSSL: failed to load private key");

			Ssl = wolfssl.new_ssl(Ctx);
			if (Ssl == IntPtr.Zero)
				throw new AuthenticationException("wolfSSL: failed to create SSL object");

			if (wolfssl.set_fd(Ssl, Inner) != wolfssl.SUCCESS)
				throw new AuthenticationException("wolfSSL: set_fd failed: " + wolfssl.get_error(Ssl));

			if (wolfssl.accept(Ssl) != wolfssl.SUCCESS)
				throw new AuthenticationException("wolfSSL: handshake failed: " + wolfssl.get_error(Ssl));

			SslProtocolName = wolfssl.get_version(Ssl);
			CipherName = wolfssl.get_current_cipher(Ssl);
		}

		/// <summary>
		/// wolfSSL IO recv callback: reads from the Stream stashed as the IO
		/// context by <see cref="wolfssl.set_fd"/> (a GCHandle-wrapped object,
		/// not necessarily a Socket -- set_fd was widened for this purpose).
		/// </summary>
		private static int IORecv(IntPtr ssl, IntPtr buf, int sz, IntPtr ctx)
		{
			if (sz <= 0) return wolfssl.CBIO_ERR_GENERAL;
			try
			{
				Stream s = (Stream)GCHandle.FromIntPtr(ctx).Target;
				byte[] tmp = new byte[sz];
				int n = s.Read(tmp, 0, sz);
				if (n <= 0) return wolfssl.CBIO_ERR_CONN_CLOSE;
				Marshal.Copy(tmp, 0, buf, n);
				return n;
			}
			catch (IOException) { return wolfssl.CBIO_ERR_CONN_CLOSE; }
			catch (Exception) { return wolfssl.CBIO_ERR_GENERAL; }
		}

		/// <summary>wolfSSL IO send callback: writes to the wrapped Stream.</summary>
		private static int IOSend(IntPtr ssl, IntPtr buf, int sz, IntPtr ctx)
		{
			if (sz <= 0) return wolfssl.CBIO_ERR_GENERAL;
			try
			{
				Stream s = (Stream)GCHandle.FromIntPtr(ctx).Target;
				byte[] tmp = new byte[sz];
				Marshal.Copy(buf, tmp, 0, sz);
				s.Write(tmp, 0, sz);
				return sz;
			}
			catch (IOException) { return wolfssl.CBIO_ERR_CONN_CLOSE; }
			catch (Exception) { return wolfssl.CBIO_ERR_GENERAL; }
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			byte[] tmp = new byte[count];
			int n = wolfssl.read(Ssl, tmp, count);
			if (n < 0) throw new IOException("wolfSSL read failed: " + wolfssl.get_error(Ssl));
			Array.Copy(tmp, 0, buffer, offset, n);
			return n;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			byte[] tmp = buffer;
			if (offset != 0 || count != buffer.Length)
			{
				tmp = new byte[count];
				Array.Copy(buffer, offset, tmp, 0, count);
			}
			int n = wolfssl.write(Ssl, tmp, count);
			if (n != count) throw new IOException("wolfSSL write failed: " + wolfssl.get_error(Ssl));
		}

		public override void Flush() => Inner.Flush();
		public override bool CanRead => Ssl != IntPtr.Zero;
		public override bool CanWrite => Ssl != IntPtr.Zero;
		public override bool CanSeek => false;
		public override long Length => throw new NotSupportedException();
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (Ssl != IntPtr.Zero)
				{
					try { wolfssl.shutdown(Ssl); } catch { /* best-effort */ }
					wolfssl.free(Ssl);
					Ssl = IntPtr.Zero;
				}
				if (Ctx != IntPtr.Zero)
				{
					wolfssl.CTX_free(Ctx);
					Ctx = IntPtr.Zero;
				}
				if (!LeaveInnerStreamOpen)
					Inner.Dispose();
			}
			base.Dispose(disposing);
		}
	}

	/// <summary>Thrown for wolfSSL setup/handshake failures (mirrors SslStream's AuthenticationException).</summary>
	class AuthenticationException : Exception
	{
		public AuthenticationException(string message) : base(message) { }
	}
}
