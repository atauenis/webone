using System.Text;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Generates the HTML shell page of the snapshot viewer.
	/// The shell page scrolls directly and contains strip images, the cmd iframe,
	/// a click overlay, and scroll script.
	/// </summary>
	static class SnapshotPage
	{
		/// <summary>
		/// Shell page. Contains strip images, cmd iframe, click overlay, and scroll script.
		/// </summary>
		public static string GetShellPage(string sessionKey, StripSet strips)
		{
var sb = new StringBuilder();
			sb.Append("<HTML><HEAD><TITLE>Snapshot</TITLE>");
			sb.Append("<STYLE TYPE=\"text/css\">");
			sb.Append("HTML,BODY{margin:0;padding:0;background:#fff;font-size:0;line-height:0}");
			sb.Append("IMG{display:block;width:100%;border:0;margin:0;padding:0;vertical-align:top}");
			sb.Append("</STYLE>");
			sb.Append("<SCRIPT LANGUAGE=\"JavaScript\">");
			sb.Append(ClientStripManager.GetScript());
			sb.Append("</SCRIPT>");
			sb.Append("<SCRIPT LANGUAGE=\"JavaScript\">");
			sb.Append(ScrollHandler.GetScrollScript(sessionKey, strips));
			sb.Append("</SCRIPT>");
			sb.Append("</HEAD><BODY>");

			for (int i = 0; i < strips.Strips.Length; i++)
			{
				string src = i < strips.NumberStripsInViewport + 2
					? "http://" + DimensionProbe.MagicHost + "/strip?key=" + sessionKey + "&i=" + i + "&r=" + strips.Strips[i].Revision
					: "http://" + DimensionProbe.MagicHost + "/blank-strip?key=" + sessionKey;
				sb.Append(
					"<IMG ID=\"strip" + i + "\"" +
					" SRC=\"" + src + "\"" +
					" WIDTH=\"100%\"" +
					" HEIGHT=\"" + strips.Strips[i].Height + "\"" +
					" ALT=\"\">");
			}

			// Hidden cmd iframe — receives click/scroll responses and updates strip images via JS.
			sb.Append(
				"<IFRAME NAME=\"cmd\"" +
				" SRC=\"about:blank\"" +
				" FRAMEBORDER=\"0\" SCROLLING=\"NO\"" +
				" STYLE=\"position:absolute;left:-9999px;width:1px;height:1px\">" +
				"</IFRAME>");

			sb.Append(ClickHandler.GetClickScript(sessionKey, strips.ImageWidth, strips.ImageHeight));
			sb.Append("</BODY></HTML>");
			return sb.ToString();
		}
	}
}
