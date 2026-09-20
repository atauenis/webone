using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Handles click interactivity for the web snapshot viewer.
	///
	/// Flow:
	///   1. User clicks anywhere in the view page.
	///   2. JS (from <see cref="GetClickScript"/>) captures coordinates, scales them,
	///      and navigates the hidden cmd iframe to /click?key=X&amp;x=Y&amp;y=Z.
	///   3. <see cref="Handle"/> forwards the click to Playwright, compares strips,
	///      and returns a small JS page that updates only the changed strip iframes.
	///   4. Unchanged strip iframes are never touched — the browser keeps them cached.
	/// </summary>
	static class ClickHandler
	{
		/// <summary>
		/// Returns a transparent absolutely-positioned overlay div that covers the entire page
		/// and captures all clicks, plus the JS that scales coordinates and forwards them to
		/// the hidden cmd iframe. Compatible with Safari 1 (CSS2 position:absolute).
		/// </summary>
		public static string GetClickScript(string sessionKey, int screenshotWidth, int screenshotHeight)
		{
			return
				// Overlay sits on top of all strip iframes via z-index and catches every click.
				"<DIV ID=\"_ov\" STYLE=\"position:absolute;top:0;left:0;width:100%;z-index:9999;cursor:pointer\"></DIV>" +
				"<SCRIPT LANGUAGE=\"JavaScript\">" +
				"var _key='" + sessionKey + "';" +
				"var _sw=" + screenshotWidth + ";" +
				"var _sh=" + screenshotHeight + ";" +
				"var _ov=document.getElementById('_ov');" +
				// Size overlay to cover all strips at the browser's display scale.
				"var _sc=(_sw>0&&window.innerWidth>0)?(_sw/window.innerWidth):1;" +
				"_ov.style.height=Math.ceil(_sh/_sc)+'px';" +
				"_ov.onclick=function(e){" +
				  "e=e||window.event;" +
				  "var px=e.pageX || 0;" +
				  "var py=e.pageY || 0;" +
				  //"var px=Math.round(pointerX*_sc);" +
				  //"var py=Math.round(pointerY*_sc);" +
				  "sendCmd('http://" + DimensionProbe.MagicHost + "/click?key='+_key+'&x='+px+'&y='+py);" +
				"};" +
				"</SCRIPT>";
		}

		/// <summary>
		/// Processes a click: forwards to Playwright, compares strips, returns a JS page
		/// that updates only the changed strip iframes in the parent (view) document.
		/// If the page height changed (new strips added/removed), forces a full view reload instead.
		/// </summary>
		public static void Handle(
			Uri requestUrl,
			HttpRequest clientRequest,
			HttpResponse clientResponse,
			LogWriter log,
			Dictionary<string, StripSet> cache)
		{
			var qs = HttpUtility.ParseQueryString(requestUrl.Query);
			string key = qs["key"];
			string xStr = qs["x"];
			string yStr = qs["y"];

			if (string.IsNullOrEmpty(key) || !int.TryParse(xStr, out int x) || !int.TryParse(yStr, out int y))
			{
				SendHtml(clientResponse, "<HTML><BODY><H1>Invalid click request.</H1></BODY></HTML>");
				return;
			}

			log.WriteLine(" [Click] key={0} x={1} y={2}", key, x, y);

			byte[] newPng;
			try
			{
				newPng = Task.Run(() => ScreenshotEngine.ClickAndScreenshot(key, x, y)).GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				log.WriteLine(" [Click] Error: {0}", ex.Message);
				SendHtml(clientResponse,
					"<HTML><BODY><H1>Click failed</H1>" +
					"<PRE>" + HttpUtility.HtmlEncode(ex.Message) + "</PRE>" +
					"</BODY></HTML>");
				return;
			}

			if (newPng == null)
			{
				log.WriteLine(" [Click] Session not found for key={0}", key);
				SendHtml(clientResponse, "<HTML><BODY><H1>Session expired. Please reload.</H1></BODY></HTML>");
				return;
			}

			if (!cache.TryGetValue(key, out var stripSet))
			{
				SendHtml(clientResponse, "<HTML><BODY><H1>Strip set not found.</H1></BODY></HTML>");
				return;
			}

			int oldCount = stripSet.Strips.Length;
			List<int> changed = StripManager.UpdateStrips(stripSet, newPng);
			bool heightChanged = stripSet.Strips.Length != oldCount;

			log.WriteLine(" [Click] {0} strip(s) changed.", changed.Count);

			if (heightChanged)
			{
				// Page height changed — full view reload needed to add/remove strip iframes.
				log.WriteLine(" [Click] Strip count changed ({0} -> {1}), forcing view reload.", oldCount, stripSet.Strips.Length);
				SendHtml(clientResponse, ClientStripManager.BuildViewReload(key));
				return;
			}

			// Partial update — JS updates only the changed strip images.
			SendHtml(clientResponse, ClientStripManager.BuildPartialUpdate(key, stripSet, changed));
		}

		private static void SendHtml(HttpResponse response, string html)
		{
			byte[] buffer = Encoding.UTF8.GetBytes(html);
			response.StatusCode = 200;
			response.ContentType = "text/html";
			response.ContentLength64 = buffer.Length;
			response.SendHeaders();
			response.OutputStream.Write(buffer, 0, buffer.Length);
			response.Close();
		}
	}
}
