using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

			string VideoUrl = Program.ProcessUriMasks("http://%Proxy%/!webvideo/?");
			foreach (string Par in Parameters.AllKeys)
			{ if (Par != "type") VideoUrl += Par + "=" + HttpUtility.UrlEncode(Parameters[Par]) + "&"; }

			// Initial page & iframe status
			string SampleUrl = Parameters["url"] ?? "https://www.youtube.com/watch?v=XXXXXXX";
			string PreferPage = "intro";
			if(Parameters["gui"]=="1")
			{
				Parameters["type"] = null;
				if (Parameters["prefer"] != null) PreferPage = Parameters["prefer"] + "&url=" + SampleUrl;
			}

			if(!Program.ToBoolean(ConfigFile.WebVideoOptions["Enable"] ?? "yes"))
			{
				Page.Content = "Sorry, proxy server administrator has disabled the online video download feature.";
				return;
			}

			switch (Parameters["type"])
			{
				case "":
				case null:
					string Frameset =
					"<form action='/!player/' method='GET' target='player_frame' align='center'>" +
					"    <table border='0' width='100%'>" +
					"        <tr>" +
					"            <td align='right'>Video</td>" +
					"            <td align='center' colspan='4'><input type='text'" +
					"            size='65' name='url' style='width: 100%'" +
					"            value='" + SampleUrl + "'></td>" +
					"            <td align='center' rowspan='3' colspan='2'><input" +
					"            type='submit' value='Load video'" +
					"            style='height: 70px;'></td>" +
					"        </tr>" +
					"        <tr>" +
					"            <td align='right'>Format</td>" +
					"            <td align='left'><select name='f' size='1'" +
					"            title='Audio/video container'>" +
					"                <option value='avi'>AVI</option>" +
					"                <option value='mpeg1video'>MPEG 1</option>" +
					"                <option value='mpeg2video'>MPEG 2</option>" +
					//"                <option value='mp4'>MPEG 4</option>" + //muxer does not support non seekable output
					"                <option selected value='mpegts'>MPEG TS</option>" +
					"                <option value='asf'>Microsoft ASF</option>" +
					"                <option value='asf_stream'>Microsoft ASF (stream)</option>" +
					//"                <option value='mov'>QuickTime</option>" + //muxer does not support non seekable output
					"                <option value='ogg'>Ogg</option>" +              // Theora & Vorbis only
					"                <option value='webm'>WebM</option>" +            // Only VP8 or VP9 or AV1 video and Vorbis or Opus audio and WebVTT subtitles are supported for WebM.
					"                <option value='swf'>Macromedia Flash</option>" + // SWF muxer only supports VP6, FLV1 and MJPEG
					//"                <option value='rm'>RealMedia</option>" + //[rm @ 06cc61c0] Invalid codec tag
					//"                <option value='3gp'>3GPP</option>" + //muxer does not support non seekable output
					"            </select></td>" +
					"            <td align='right'>Codecs</td>" +
					"            <td align='left'><select name='vcodec' size='1'" +
					"            title='Video codec'>" +
					"                <option value='mpeg1video'>MPEG 1</option>" +
					"                <option value='mpeg2video'>MPEG 2</option>" +
					"                <option value='mpeg4'>MPEG 4</option>" +
					"                <option value='wmv1'>WMV 7</option>" +
					"                <option value='wmv2'>WMV 8</option>" +
					"                <option value='h263'>H.263</option>" +          // Valid sizes are 128x96, 176x144, 352x288, 704x576, and 1408x1152.
					"                <option selected value='h264'>H.264 AVC</option>" +
					"                <option value='hevc'>H.265 HEVC</option>" +
					"                <option value='theora'>Ogg Theora</option>" +
					"                <option value='vp8'>VP8</option>" +
					"                <option value='vp9'>VP9</option>" +
					"                <option value='mjpeg'>MJPEG</option>" +
					"                <option value='msvideo1'>MS Video 1</option>" + // width and height must be multiples of 4
					"                <option value='copy'>(original)</option>" +
					"            </select> <select name='vf' size='1'" +
					"            title='Video resolution'>" +
					"                <option value='scale=\"1080:-1\"'>1080p</option>" +
					"                <option value='scale=\"720:-1\"'>720p</option>" +
					"                <option selected value='scale=\"480:-1\"'>480p</option>" +
					"                <option value='scale=\"360:-1\"'>360p</option>" +
					"                <option value='scale=\"240:-1\"'>240p</option>" +
					"                <option value='scale=\"144:-1\"'>144p</option>" +
					"                <option value='scale=\"1024x768\"'>1024x768</option>" +
					"                <option value='scale=\"800x600\"'>800x600</option>" +
					"                <option value='scale=\"640x480\"'>640x480</option>" +
					"                <option value='scale=\"320x200\"'>320x200</option>" +
					"                <option value='scale=\"704x576\"'>704x576</option>" +
					"                <option value='scale=\"352x288\"'>352x288</option>" +
					"                <option value='scale=\"176x144\"'>176x144</option>" +
					"                <option value='scale=\"128x96\"'>128x96</option>" +
					"                <option value='scale=\"-1:-1\"'>(original)</option>" +
					"            </select> &nbsp; <select name='acodec' size='1'" +
					"            title='Audio codec'>" +
					"                <option value='mp2'>MPEG 2</option>" +
					"                <option selected value='mp3'>MPEG 3</option>" +
					"                <option value='wmav1'>WMA 1</option>" +
					"                <option value='wmav2'>WMA 2</option>" +
					"                <option value='aac'>AAC</option>" +
					"                <option value='pcm_dvd'>PCM</option>" +
					"                <option value='vorbis -strict -2'>Ogg Vorbis</option>" + // Current FFmpeg Vorbis encoder only supports 2 channels.
					"                <option value='opus -strict -2'>Opus</option>" +
					"                <option value='ra_144'>RealAudio 1</option>" +
					"                <option value='copy'>(original)</option>" +
					"            </select> <select name='ac' size='1'" +
					"            title='Audio channels'>" +
					"                <option selected value='1'>Mono</option>" +
					"                <option value='2'>Stereo</option>" +
					"            </select></td>" +
					"            <td>(<a href='http://github.com/atauenis/webone/wiki/YouTube-playback'>?</a>)</td>" +
					"        </tr>" +
					"        <tr>" +
					"            <td align='right'></td>" +
					"            <td align='center' colspan='4'>" +
					"            <input type='radio'name='type' value='embed'>Embed, " +
					"            <input type='radio'name='type' value='embedwm' checked>WMP, " +
					"            <input type='radio'name='type' value='embedvlc'>VLC, " +
					"            <input type='radio' name='type' value='objectwm'>WinMedia, " +
					"            <input type='radio' name='type' value='objectns'>NetShow, " +
					"            <input type='radio' name='type' value='dynimg'>DynImg, " +
					"            <input type='radio' name='type' value='html5'>HTML5, " +
					"            <input type='radio' name='type' value='link'>link, " +
					"            <input type='radio' name='type' value='file'>file" +
					"        </td>" +
					"        </tr>" +
					"    </table>" +
					"</form>" +
					"" +
					"<iframe name='player_frame' src='/!player/?type=" + PreferPage + "' " +
					"border='0' width='100%' height='100%' style='border-style: none;'>" +
					"Use the toolbar to watch a video.</iframe>"; ;
					Page.Content = Frameset;
					break;
				case "intro":
					Page.Content = "<p align='center'><big>Use the toolbar above to watch a video.</big></p>";
					Page.AddCss = false;
					Page.Title = "Video player - INTRO";
					break;
				case "embed":
					// universal AVI - plugin
					string EmbHtml = "<embed id='MediaPlayer' " +
					"showcontrols='true' showpositioncontrols='true' showstatusbar='tue' showgotobar='true'" +
					"src='" + VideoUrl + "' autostart='true' style='width: 100%; height: 100%;' />";
					Page.Content = EmbHtml;
					Page.AddCss = false;
					Page.Title = "Video player - Universal";
					break;
				case "embedwm":
					// Windows Media Player - plugin
					string WMP64html = "<embed id='MediaPlayer' type='application/x-mplayer2'" +
					"pluginspage='http://microsoft.com/windows/mediaplayer/en/download/'" +
					"showcontrols='true' showpositioncontrols='true' showstatusbar='tue' showgotobar='true'" +
					"src='" + VideoUrl + "' autostart='true' style='width: 100%; height: 100%;' />";
					Page.Content = WMP64html;
					Page.AddCss = false;
					Page.Title = "Video player - WMP";
					break;
				case "embedvlc":
					// VLC Mediaplayer - plugin
					string VlcHtml = "<embed id='MediaPlayer' type='application/x-vlc-plugin'" +
					"codebase='http://download.videolan.org/pub/videolan/vlc/last/win32/axvlc.cab'" +
					"pluginspage='http://www.videolan.org'" +
					"showcontrols='true' showpositioncontrols='true' showstatusbar='tue' showgotobar='true'" +
					"src='" + VideoUrl + "' autostart='true' style='width: 100%; height: 100%;' />";
					Page.Content = VlcHtml;
					Page.AddCss = false;
					Page.Title = "Video player - VLC";
					break;
				case "objectns":
					// ActiveMovie Control or NetShow Player 2.x - ActiveX
					// Download: http://www.microsoft.com/netshow/download/player.htm
					// CODEBASE='http://www.microsoft.com/netshow/download/en/nsasfinf.cab#Version=2,0,0,912'
					// CODEBASE='http://www.microsoft.com/netshow/download/en/nsmp2inf.cab#Version=5,1,51,415'
					// NetShow 2.0 for Win95/NT -> nscore.exe from nscore.cab
					string NSActiveXhtml = "<center><object ID='MediaPlayer' style='width: 100%; height: 100%;' "+
					"CLASSID='CLSID:2179C5D3-EBFF-11CF-B6FD-00AA00B4E220' " +
					"codebase='http://www.microsoft.com/netshow/download/en/nsasfinf.cab#Version=2,0,0,912'>" +
					"<param name='FileName' value='" + VideoUrl + "'>" +
					"<param name='ShowControls' value='true'>" +
					"<param name='ShowDisplay' value='true'>" +
					"<param name='ShowStatusBar' value='true'>" +
					"<param name='ShowPositionControls' value='true'>" +
					"<param name='ShowGoToBar' value='true'>" +
					"<param name='Controls' value='true'>" +
					"<param name='AutoSize' value='true'>" +
					"</object></center>";
					Page.Content = NSActiveXhtml;
					Page.AddCss = false;
					Page.Title = "Video player - NetShow ActiveX";
					break;
				case "objectwm":
					// Windows Media Player 6.4 - ActiveX
					// Download: http://microsoft.com/windows/mediaplayer/en/download/
					string WMPActiveXhtml = "<object ID='MediaPlayer' style='width: 100%; height: 100%;' "+
					"CLASSID='CLSID:6BF52A52-394A-11d3-B153-00C04F79FAA6' "+
					"standby='Loading Microsoft Windows Media Player components...' " +
					"codebase='http://activex.microsoft.com/activex/controls/mplayer/en/nsmp2inf.cab#Version=6,4,7,1112'>" +
					"<param name='URL' value='" + VideoUrl + "'>" +
					"</object>";
					Page.Content = WMPActiveXhtml;
					Page.AddCss = false;
					Page.Title = "Video player - WMP ActiveX";
					break;
				case "html5":
					// HTML5 VIDEO tag
					Page.Content = "<center><video id='MediaPlayer' src='" + VideoUrl + "' controls='yes' style='width: 100%; height: 100%;'>"
					+ "Try another player type, as HTML5 is not supported.</video></center>";
					Page.AddCss = false;
					//idea: made multi-source code with hard-coded containers (ogg, webm, etc)
					Page.Title = "Video player - HTML5";
					break;
				case "dynimg":
					// Dynamic Image - IE 2.0 only 
					// (http://www.jmcgowan.com/aviweb.html)
					// also: https://web.archive.org/web/19990117015933/http://www.microsoft.com/devonly/tech/amov1doc/amsdk008.htm
					// tries to work also in IE 3-6.
					string PlaceholderUrl = "http://www.linuxtopia.org/HowToGuides/HTML_tutorials/graphics/moonflag.gif";
					Page.Content = "<center><IMG DYNSRC='" + VideoUrl + "' CONTROLS START='FILEOPEN' SRC='" + PlaceholderUrl + "'></center>";
					Page.AddCss = false;
					Page.Title = "Video player - Dynamic Image";
					break;
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
				default:
					Page.Content = "Unknown player type.";
					Page.AddCss = false;
					Page.Title = "Video player - ERROR";
					break;
			}
		}
	}
}
