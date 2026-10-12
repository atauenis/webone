using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// A single horizontal strip of a page screenshot.
	/// </summary>
	class StripData
	{
		/// <summary>SHA256 of the strip's raw RGB pixels. Used to detect changes.</summary>
		public byte[] Hash;
		/// <summary>JPEG-encoded bytes of this strip. Served directly to the browser.</summary>
		public byte[] Jpeg;
		/// <summary>Timestamp string appended to the image URL to bust browser cache on change.</summary>
		public string Revision;
		/// <summary>Height of this strip in screenshot pixels.</summary>
		public int Height;
	}

	/// <summary>
	/// The full set of strips for one page snapshot session.
	/// </summary>
	class StripSet
	{
		public StripData[] Strips;
		public int ImageWidth;
		public int ImageHeight;
		public int StripHeight;
		public int ViewportHeight;
		public int NumberStripsInViewport;
		public int LastScrollY;
		public byte[] BlankStripGif;
		public DateTime CreatedAt;
	}

	/// <summary>
	/// Splits PNG screenshots into horizontal strips, hashes each strip for change detection,
	/// and encodes changed strips to JPEG for serving.
	/// </summary>
	static class StripManager
	{
		/// <summary>
		/// Creates a <see cref="StripSet"/> from a full-page PNG screenshot.
		/// </summary>
		public static StripSet CreateStrips(byte[] png, int stripHeight, int viewportHeight = 0)
		{
			using var image = Image.Load<Rgb24>(png);
			int count = (int)Math.Ceiling((double)image.Height / stripHeight);
			string rev = Revision();
			var strips = new StripData[count];

			for (int i = 0; i < count; i++)
			{
				int y = i * stripHeight;
				int h = Math.Min(stripHeight, image.Height - y);
				using var strip = image.Clone(ctx => ctx.Crop(new Rectangle(0, y, image.Width, h)));
				strips[i] = new StripData
				{
					Hash = Hash(strip),
					Jpeg = EncodeJpeg(strip),
					Revision = rev,
					Height = h
				};
			}

			return new StripSet
			{
				Strips = strips,
				ImageWidth = image.Width,
				ImageHeight = image.Height,
				StripHeight = stripHeight,
				ViewportHeight = viewportHeight,
				NumberStripsInViewport = viewportHeight / stripHeight,
				BlankStripGif = BlankStrip.Generate(image.Width, stripHeight),
				CreatedAt = DateTime.UtcNow
			};
		}

		/// <summary>
		/// Compares a new PNG screenshot against an existing <see cref="StripSet"/> and updates
		/// only the strips that changed. Unchanged strips keep their existing JPEG and revision.
		/// </summary>
		/// <returns>Indices of strips that changed.</returns>
		public static List<int> UpdateStrips(StripSet existing, byte[] newPng)
		{
			var changed = new List<int>();
			using var image = Image.Load<Rgb24>(newPng);
			int count = (int)Math.Ceiling((double)image.Height / existing.StripHeight);
			string rev = Revision();

			// Resize strips array if page height changed
			if (count != existing.Strips.Length)
			{
				var resized = new StripData[count];
				Array.Copy(existing.Strips, resized, Math.Min(existing.Strips.Length, count));
				for (int i = existing.Strips.Length; i < count; i++)
					resized[i] = new StripData { Hash = Array.Empty<byte>(), Jpeg = Array.Empty<byte>(), Revision = "0" };
				existing.Strips = resized;
			}

			for (int i = 0; i < count; i++)
			{
				int y = i * existing.StripHeight;
				int h = Math.Min(existing.StripHeight, image.Height - y);
				using var strip = image.Clone(ctx => ctx.Crop(new Rectangle(0, y, image.Width, h)));
				byte[] newHash = Hash(strip);

				if (!HashEqual(existing.Strips[i]?.Hash, newHash))
				{
					existing.Strips[i] = new StripData
					{
						Hash = newHash,
						Jpeg = EncodeJpeg(strip),
						Revision = rev,
						Height = h
					};
					changed.Add(i);
				}
			}

			existing.ImageWidth = image.Width;
			existing.ImageHeight = image.Height;
			return changed;
		}

		private static byte[] Hash(Image<Rgb24> image)
		{
			// Copy raw RGB pixel bytes and hash them — deterministic and lossless.
			var pixelBytes = new byte[image.Width * image.Height * 3];
			image.CopyPixelDataTo(pixelBytes);
			return SHA256.HashData(pixelBytes);
		}

		private static byte[] EncodeJpeg(Image<Rgb24> image)
		{
			using var ms = new MemoryStream();
			image.SaveAsJpeg(ms, new JpegEncoder { Quality = Program.JpegQuality, ColorType = JpegEncodingColor.Rgb });
			return ms.ToArray();
		}

		private static bool HashEqual(byte[] a, byte[] b)
		{
			if (a == null || b == null || a.Length != b.Length) return false;
			for (int i = 0; i < a.Length; i++)
				if (a[i] != b[i]) return false;
			return true;
		}

		private static string Revision() =>
			DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
	}
}
