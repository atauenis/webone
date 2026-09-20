using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Keeps the Playwright driver scroll position in sync with the client browser.
	/// </summary>
	static class ScrollHandler
	{
		// 1×1 transparent GIF returned so the browser Image() request completes cleanly.
		private static readonly byte[] TransparentGif = Convert.FromBase64String(
			"R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

		/// <summary>
		/// Returns the scroll-sync JS served at /scroll.js.
		/// ES3-only, Safari 1 compatible.
		/// </summary>
		public static string GetScrollScript(string sessionKey, StripSet strips)
		{
			string scrollBase = "http://" + DimensionProbe.MagicHost + "/scroll-pos?key=" + sessionKey + "&y=";
			string stripBase  = "http://" + DimensionProbe.MagicHost + "/strip?key=" + sessionKey + "&i=";

			// Build strip URL array: _srcs[i] = url for strip i.
			var srcs = new System.Text.StringBuilder();
			srcs.Append("var _srcs=[");
			for (int i = 0; i < strips.Strips.Length; i++)
			{
				if (i > 0) srcs.Append(",");
				srcs.Append("'" + stripBase + i + "&r=" + strips.Strips[i].Revision + "'");
			}
			srcs.Append("];");

			return
				srcs.ToString() +
				"function _getScrollY(){" +
				  "if(window.pageYOffset!=null)return window.pageYOffset;" +
				  "return 0;" +
				"}" +
				"function _sendScroll(sy){" +
				  "var img=new Image();" +
				  "img.src='" + scrollBase + "'+sy+'&t='+(new Date().getTime());" +
				"}" +
				"function _loadStrips(sy){" +
				  "var firstStrip=Math.floor(sy/" + strips.StripHeight + ");" +
				  "var lastStrip=Math.min(document.images.length-1,firstStrip+" + strips.NumberStripsInViewport + "+2);" +
				  "for(var i=firstStrip;i<=lastStrip;i++){" +
				    "document.images[i].src=_srcs[i];" +
				  "}" +
				"}" +
				"var _lastY=_getScrollY();" +
				"function _scroll(){" +
				  "var sy=_getScrollY();" +
				  "if(sy==_lastY)return;" +
				  "_lastY=sy;" +
				  "_loadStrips(sy);" +
				  "_sendScroll(sy);" +
				  "window.status='scroll y='+sy;" +
				"}" +
				"window.onscroll=_scroll;" +
				"setInterval('_scroll()',200);";
		}

		/// <summary>
		/// Serves the scroll-sync JS at /scroll.js.
		/// <summary>
		/// Scrolls the Playwright page to the given Y position.
		/// Returns a 1×1 transparent GIF so the browser Image() request completes.
		/// </summary>
		public static void HandleScrollPos(
			Uri requestUrl,
			HttpResponse clientResponse,
			LogWriter log,
			Dictionary<string, StripSet> cache)
		{
			var qs = HttpUtility.ParseQueryString(requestUrl.Query);
			string key = qs["key"];
			string yStr = qs["y"];

			if (!string.IsNullOrEmpty(key) && int.TryParse(yStr, out int scrollY))
			{
				log.WriteLine(" [ScrollPos] y={0}", scrollY);
				Console.WriteLine("[ScrollPos] y={0}", scrollY);
				if (cache.TryGetValue(key, out var stripSet))
					stripSet.LastScrollY = scrollY;
				Task.Run(() => ScreenshotEngine.ScrollTo(key, scrollY));
			}

			clientResponse.StatusCode = 200;
			clientResponse.ContentType = "image/gif";
			clientResponse.ContentLength64 = TransparentGif.Length;
			clientResponse.SendHeaders();
			clientResponse.OutputStream.Write(TransparentGif, 0, TransparentGif.Length);
			clientResponse.Close();
		}
	}
}
