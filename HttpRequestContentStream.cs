using System;
using System.IO;
using System.Net.Sockets;

namespace WebOne
{
	/// <summary>
	/// A wrapper around a <see cref="NetworkStream"/> that keeps track of the number of bytes read and written (useful for HTTP playload transfer).
	/// </summary>
	public class HttpRequestContentStream : Stream
	{
		//An very lightweight alternative to non-public System.Net.HttpRequestStream.
		//Thanks to https://stackoverflow.com/a/34275894/7600726

		//in future need to implement HttpRequestChunkedStream which will be for chunked HTTP 1.1 payload tranfer w/o known length

		private readonly Stream NetStream;
		private readonly int ContentLength;

		private long totalBytesWritten;
		private long totalBytesRead;


		/// <summary>
		/// Initialize a new instance of <see cref="HttpRequestContentStream"/>.
		/// </summary>
		/// <param name="NetStream"><see cref="NetworkStream"/> containing HTTP request payload.</param>
		/// <param name="ContentLength">Length of HTTP request payload (value of "Content-Length" header).</param>
		/// <exception cref="ArgumentException">If the <paramref name="NetStream"/> is not a <see cref="System.Net.Sockets.NetworkStream"/>.</exception>
		public HttpRequestContentStream(Stream NetStream, int ContentLength)
		{
			if (NetStream is not NetworkStream) throw new ArgumentException("Only streams of type NetworkStream are acceptable!", nameof(NetStream));
			if (ContentLength < 1) throw new ArgumentOutOfRangeException(nameof(ContentLength), "Content-Length must be adequate!");
			this.NetStream = NetStream;
			this.ContentLength = ContentLength;
			totalBytesRead = 0;
			totalBytesWritten = 0;
		}

		public override void Flush()
		{
			NetStream.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			//this function gets called by CopyTo() until return value = 0
			if (totalBytesRead >= ContentLength) return 0;

			int readBytes = NetStream.Read(buffer, offset, count > ContentLength ? ContentLength : count);
			totalBytesRead += readBytes;
			return readBytes;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			NetStream.Write(buffer, offset, count);
			totalBytesWritten += count;
		}

		public override bool CanRead => NetStream.CanRead;
		public override bool CanSeek => NetStream.CanSeek;
		public override bool CanWrite => NetStream.CanWrite;
		public override bool CanTimeout => NetStream.CanTimeout;

		public override long Length
		{
			get { return (long)ContentLength; }
		}

		public override long Position
		{
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}

		/// <summary>
		/// Count of bytes written to this HttpRequestContentStream.
		/// </summary>
		public long TotalBytesWritten => totalBytesWritten;

		/// <summary>
		/// Count of bytes read from this HttpRequestContentStream.
		/// </summary>
		public long TotalBytesRead => totalBytesRead;
	}
}