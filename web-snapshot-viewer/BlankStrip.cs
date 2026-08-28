using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Generates a checkerboard placeholder GIF stored in RAM,
	/// used as src for strips not yet loaded.
	/// </summary>
	static class BlankStrip
	{
		private static readonly Color White     = Color.FromRgb(255, 255, 255);
		private static readonly Color LightGray = Color.FromRgb(204, 204, 204);

		/// <summary>
		/// Generates a checkerboard GIF of the given dimensions and returns it as bytes.
		/// Pattern: 10x10px squares alternating white and light gray.
		/// </summary>
		public static byte[] Generate(int width, int height)
		{
			using var image = new Image<Rgba32>(width, height);

			image.ProcessPixelRows(accessor =>
			{
				for (int y = 0; y < height; y++)
				{
					var row = accessor.GetRowSpan(y);
					for (int x = 0; x < width; x++)
					{
						bool isGray = ((x / 10) + (y / 10)) % 2 == 0;
						row[x] = isGray ? LightGray.ToPixel<Rgba32>() : White.ToPixel<Rgba32>();
					}
				}
			});

			using var ms = new MemoryStream();
			image.SaveAsGif(ms, new GifEncoder { ColorTableMode = GifColorTableMode.Global });
			return ms.ToArray();
		}
	}
}
