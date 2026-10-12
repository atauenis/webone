using System.Web;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Generates a lightweight HTML page that detects browser viewport dimensions
	/// and redirects to the snapshot endpoint with those dimensions.
	/// </summary>
	static class DimensionProbe
	{
		internal const string MagicHost = "snapshot.webone.internal";

		/// <summary>
		/// Returns an HTML probe page that reads window.innerWidth/innerHeight and redirects
		/// to the snapshot endpoint with the original URL and detected dimensions.
		/// </summary>
		/// <param name="originalUrl">The original URL the browser was trying to reach.</param>
		public static string GetPage(string originalUrl)
		{
			string safeUrl = HttpUtility.JavaScriptStringEncode(originalUrl);
			return
				"<!DOCTYPE html><html><head><title>Loading snapshot...</title></head><body><script>" +
				"var t='http://" + MagicHost + "/snap';" +
				"var u=encodeURIComponent('" + safeUrl + "');" +
				"location.replace(t+'?url='+u+'&w='+window.innerWidth+'&h='+window.innerHeight);" +
				"</script></body></html>";
		}
	}
}
