using System;
using System.IO;
using System.Text;

namespace WebOne
{
	/// <summary>
	/// A wrapper around a <see cref="Stream"/> that can write HTTP response bodies in according to RFC 9112 §7.
	/// </summary>
	public class HttpResponseContentStream : Stream
	{
		//RTFM: https://datatracker.ietf.org/doc/html/rfc9112#section-7

		private readonly Stream inner;
		private bool UseChunkedTransfer;
		//private bool UseCompressedTransfer;
		//private string UseCompressionAlgorithm;

		/// <summary>
		/// Initialize this HttpResponseContentStream instance.
		/// </summary>
		/// <param name="inner">The stream used to communicate with client.</param>
		/// <param name="chunked">Use HTTP Chunked Transfer</param>
		public HttpResponseContentStream(Stream inner, bool chunked)
		{
			this.inner = inner;
			UseChunkedTransfer = chunked;
			//todo: add compression support (and compression + chunked).

#if !EnableChunkedTransfer
			UseChunkedTransfer = false;
#else
			//DOES NOT REALLY WORKING, HELP IS WANTED - @atauenis, 20.11.2023
#endif
		}

		public override void Flush()
		{ inner.Flush(); }

		public override long Seek(long offset, SeekOrigin origin)
		{ throw new NotImplementedException(); }

		public override void SetLength(long value)
		{ throw new NotImplementedException(); }

		public override int Read(byte[] buffer, int offset, int count)
		{ throw new NotImplementedException(); }

		/// <summary>
		/// Writes a sequence of bytes to the client.
		/// </summary>
		/// <param name="buffer">Array of bytes containing the data payload.</param>
		/// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
		/// <param name="count">The number of bytes to be written to the current stream.</param>
		public override void Write(byte[] buffer, int offset, int count)
		{
			if (UseChunkedTransfer)
			{
				// Send chunk
				Console.WriteLine("Chunked: Write {0} bytes.1", count - offset);
				byte[] StartBuffer = Encoding.ASCII.GetBytes((count - offset).ToString("X") + "\r\n");
				byte[] EndBuffer = Encoding.ASCII.GetBytes("\r\n");
				inner.Write(StartBuffer, 0, StartBuffer.Length);
				inner.Write(buffer, offset, count);
				inner.Write(EndBuffer, 0, EndBuffer.Length);
				inner.Flush();
				Console.WriteLine("Chunked: Write {0} bytes.2", count - offset);
				//SOMEWHY INTERPRETTED AS PLAIN TEXT - HELP NEED TO SOLVE
			}
			else
			{
				// Just write the body
				inner.Write(buffer, offset, count);
#if EnableChunkedTransfer
				Console.WriteLine("Plain: Write {0} bytes.", count - offset);
#endif
			}
		}

		/// <summary>
		/// If the data transfer channel between server and client is based on encoded transfer, send mark of content end,
		/// required to properly finish the transfer session.
		/// </summary>
		/// <param name="trailer">Trailing header (if any)</param>
		public void WriteTerminator(string trailer = "")
		{
			if (UseChunkedTransfer)
			{
				// Write terminating chunk if need
				Console.WriteLine("Chunked: Terminate by {0} bytes.1", trailer.Length);
				byte[] TerminatorStartBuffer = Encoding.ASCII.GetBytes("0\r\n");
				byte[] TerminatorEndBuffer = Encoding.ASCII.GetBytes(trailer + "\r\n");
				try
				{
					inner.Write(TerminatorStartBuffer, 0, TerminatorStartBuffer.Length);
					inner.Write(TerminatorEndBuffer, 0, TerminatorEndBuffer.Length);
				}
				catch { /* Sometimes an connection lost may occur here. */ };
				Console.WriteLine("Chunked: Terminate by {0} bytes.2", trailer.Length);
			}
		}

		public override bool CanRead => false;
		public override bool CanSeek => false;
		public override bool CanWrite => true;

		public override long Length
		{
			get { throw new NotImplementedException(); }
		}

		public override long Position { get; set; }
	}
}