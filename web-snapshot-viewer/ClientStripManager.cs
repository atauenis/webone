using System.Collections.Generic;
using System.Text;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Manages all client-side strip rendering and updates.
	///
	/// Client-side JS (injected via GetScript):
	///   - sendCmd(url)          — navigates the cmd iframe to send a request to the server
	///   - updateStrip(name,url) — updates the src of a strip image element
	///   - reloadPage()          — reloads the shell page
	///
	/// Server-side builders (responses loaded inside the cmd iframe):
	///   - BuildPartialUpdate    — updates only changed strips
	///   - BuildViewportUpdate   — updates only visible strips
	///   - BuildFullUpdate       — updates all strips
	///   - BuildViewReload       — reloads the shell page
	/// </summary>
	static class ClientStripManager
	{
		/// <summary>
		/// Returns the JS library injected into the shell page.
		/// Must be included before any other script that uses these functions.
		/// </summary>
		public static string GetScript()
		{
			return
				// Navigates the cmd iframe to send a request to the server.
				"function sendCmd(url){" +
				  "window.frames['cmd'].location=url;" +
				"}" +
				// Updates the src of a strip image element directly.
				"function updateStrip(name,url){" +
				  "var img=document.getElementById(name);" +
				  "if(img)img.src=url;" +
				"}" +
				// Reloads the shell page to rebuild all strip images.
				"function reloadPage(){" +
				  "window.location=window.location.href;" +
				"}";
		}

		/// <summary>
		/// Builds a JS page loaded in the cmd iframe that updates only the changed strips.
		/// </summary>
		public static string BuildPartialUpdate(string key, StripSet strips, List<int> changed)
		{
			var sb = new StringBuilder();
			sb.Append("<HTML><BODY><SCRIPT LANGUAGE=\"JavaScript\">");

			foreach (int i in changed)
			{
				string url = "http://" + DimensionProbe.MagicHost +
				             "/strip?key=" + key +
				             "&i=" + i +
				             "&r=" + strips.Strips[i].Revision;
				sb.Append("parent.updateStrip('strip" + i + "','" + url + "');");
			}

			sb.Append("</SCRIPT></BODY></HTML>");
			return sb.ToString();
		}

		/// <summary>
		/// Builds a JS page loaded in the cmd iframe that updates only the strips
		/// currently visible in the driver viewport.
		/// </summary>
		public static string BuildViewportUpdate(string key, StripSet strips)
		{
			int scrollY    = strips.LastScrollY;
			int vpHeight   = strips.ViewportHeight > 0 ? strips.ViewportHeight : strips.ImageHeight;
			int firstStrip = System.Math.Max(0, scrollY / strips.StripHeight);
			int lastStrip  = System.Math.Min(strips.Strips.Length - 1, (scrollY + vpHeight) / strips.StripHeight);

			var sb = new StringBuilder();
			sb.Append("<HTML><BODY><SCRIPT LANGUAGE=\"JavaScript\">");

			for (int i = firstStrip; i <= lastStrip; i++)
			{
				string url = "http://" + DimensionProbe.MagicHost +
				             "/strip?key=" + key +
				             "&i=" + i +
				             "&r=" + strips.Strips[i].Revision;
				sb.Append("parent.updateStrip('strip" + i + "','" + url + "');");
			}

			sb.Append("</SCRIPT></BODY></HTML>");
			return sb.ToString();
		}

		/// <summary>
		/// Builds a JS page loaded in the cmd iframe that updates all strips.
		/// </summary>
		public static string BuildFullUpdate(string key, StripSet strips)
		{
			var changed = new List<int>(strips.Strips.Length);
			for (int i = 0; i < strips.Strips.Length; i++)
				changed.Add(i);
			return BuildPartialUpdate(key, strips, changed);
		}

		/// <summary>
		/// Builds a JS page loaded in the cmd iframe that reloads the shell page.
		/// Used when the strip count changed and images need to be recreated.
		/// </summary>
		public static string BuildViewReload(string key)
		{
			return
				"<HTML><BODY><SCRIPT LANGUAGE=\"JavaScript\">" +
				"parent.reloadPage();" +
				"</SCRIPT></BODY></HTML>";
		}
	}
}
