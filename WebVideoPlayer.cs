using System.Collections.Specialized;
using System.Web;

namespace WebOne
{
	/// <summary>
	/// HTML player for YouTube and similar sites
	/// </summary>
	class WebVideoPlayer
	{
		public InfoPage Page = new();

		public WebVideoPlayer(NameValueCollection Parameters)
		{
			Page.Title = "Retro online video player";
			Page.Header = "";
			Page.ShowFooter = false;
			Page.HtmlHeaders = "<style type='text/css'>html, body { border-style: none; } </style>";

			string VideoUrl = "/!webvideo/?";
			foreach (string Par in Parameters.AllKeys)
			{ if (Par != "type") VideoUrl += Par + "=" + HttpUtility.UrlEncode(Parameters[Par]) + "&"; }

			string ProfileName = Parameters["profile"];

			//if profile is specified instead of parameters, use profile
			if (string.IsNullOrWhiteSpace(Parameters["type"]) && !string.IsNullOrWhiteSpace(ProfileName) && ConfigFile.WebVideoProfiles.ContainsKey(ProfileName))
			{
				string Redir = "/!player/?";
				foreach (string Par in Parameters.AllKeys)
				{ if (Par != "type") Redir += Par + "=" + HttpUtility.UrlEncode(Parameters[Par]) + "&"; }
				string ProfileString = ConfigFile.WebVideoProfiles[ProfileName][1];

				Redir += ProfileString;
				Page.Content = "Loading the profile...";
				Page.HttpStatusCode = 302;
				Page.HttpHeaders.Add("Location", Redir);
				return;
			}

			//check if ROVP is not disabled
			if (!ConfigFile.WebVideoOptions.ContainsKey("Enable") || !Program.ToBoolean(ConfigFile.WebVideoOptions["Enable"] ?? "yes"))
			{
				Page.Content = "It's disabled.";
				Page.HttpStatusCode = 302;
				Page.HttpHeaders.Add("Location", "/norovp.htm");
				return;
			}

			//open requested player type
			string PlayerType = Parameters["type"];
			if (string.IsNullOrWhiteSpace(Parameters["type"])) PlayerType = "splash";

			switch (PlayerType)
			{
				case "link":
					// Link only
					Page.Content = "<center><big><a href='" + VideoUrl + "'>Download the video</a></big></center>";
					if (Parameters["f"] == null) Page.Content += "<center><p>Or select format, codecs and convert the video online.</p></center>";
					Page.AddCss = false;
					Page.Title = "Video player - link only";
					break;
				case "file":
					// Redirect to file only
					Page.Content = "<center>Please wait up to 30 sec.<br>If nothing appear, <a href='" + VideoUrl + "'>click here</a> to download manually.</center>";
					Page.HttpHeaders.Add("Refresh", "0; url=" + VideoUrl);
					Page.AddCss = false;
					Page.Title = "Video player - FILE REDIRECT";
					break;
			}

			Page.HttpStatusCode = 302;
			Page.HttpHeaders.Add("Location", "/rovp-" + PlayerType + ".htm");
			if (VideoUrl != "/!webvideo/?") Page.HttpHeaders["Location"] += "?VideoUrl=" + HttpUtility.UrlEncode(VideoUrl);
			if (PlayerType == "html5") Page.HttpHeaders["Location"] += "&JsonUrl=" + HttpUtility.UrlEncode("/!webvideo/?&dump-json=&url=" + Parameters["url"] + "&no-warnings=&quiet=");
			return;

			/* Notes about HTML-based players.
			 * 
			 * Null or empty:
			 *	Splash screen.
			 * "intro"
			 *	Intro - Help message.
			 * "embed" 
			 *	[Embed] Universal - plugin
			 * "embedwm"
			 *	[WMP] Windows Media Player - plugin
			 * "embedvlc"
			 *	[VLC] VLC Mediaplayer - plugin
			 * "objectns"
			 *	[NetShow] NetShow Player Control, Windows Media Player 5/6 Control - ActiveX
			 *	Docs: https://web.archive.org/web/20010124080600/http://msdn.microsoft.com/library/psdk/wm_media/wmplay/mmp_sdk/
			 *	Docs: https://web.archive.org/web/19990117015933/http://www.microsoft.com/devonly/tech/amov1doc/amsdk008.htm
			 *	Download: http://www.microsoft.com/netshow/download/player.htm
			 *	CODEBASE='http://www.microsoft.com/netshow/download/en/nsasfinf.cab#Version=2,0,0,912'  - NS 2.0
			 *	CODEBASE='http://www.microsoft.com/netshow/download/en/nsmp2inf.cab#Version=5,1,51,415' - NS 3.0 aka WMP 5.2 Beta
			 *	CODEBASE='http://activex.microsoft.com/activex/controls/mplayer/en/nsmp2inf.cab#Version=6,4,7,1112' - WMP 6.4 RTM
			 * "objectwm"
			 *	[WinMedia] Windows Media Player 7.0 Control - ActiveX
			 *	Download: http://microsoft.com/windows/mediaplayer/en/download/
			 *	Docs: https://learn.microsoft.com/en-us/previous-versions/windows/desktop/wmp/detailed-object-model-comparison
			 * "html5"
			 *	HTML5 VIDEO tag
			 * "dynimg"
			 *	Dynamic Image - IE only 
			 *	(http://www.jmcgowan.com/aviweb.html)
			 *	https://web.archive.org/web/19990117015933/http://www.microsoft.com/devonly/tech/amov1doc/amsdk008.htm
			 *	optimized for MPEG format & codec
			 * "link"
			 *	Link only
			 * "file"
			 *	Redirect to file only
			 * Other
			 *	Error message
			*/
		}
	}
}
