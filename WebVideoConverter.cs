using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using static WebOne.Program;

namespace WebOne
{
	/// <summary>
	/// Web video dowloader & converter
	/// </summary>
	class WebVideoConverter
	{
		/// <summary>
		/// Download and convert an online video (from hostings like YouTube, VK, etc)
		/// </summary>
		public WebVideo ConvertVideo(Dictionary<string, string> Arguments, LogWriter Log)
		{
			WebVideo video = new();
			try
			{
				string YoutubeDlArgs = "";
				string FFmpegArgs = "";
				bool UseFFmpeg = true;
				bool GetYoutubeJson = false;

				// Check options
				if (!Arguments.ContainsKey("url"))
				{ throw new InvalidOperationException("Internet video address is missing."); }

				// Load default options
				if (!ConfigFile.WebVideoOptions.ContainsKey("Enable")) ConfigFile.WebVideoOptions["Enable"] = "false";
				if (!ConfigFile.WebVideoOptions.ContainsKey("YouTubeDlApp")) ConfigFile.WebVideoOptions["YouTubeDlApp"] = "yt-dlp";
				if (!ConfigFile.WebVideoOptions.ContainsKey("FFmpegApp")) ConfigFile.WebVideoOptions["FFmpegApp"] = "ffmpeg";
				foreach (var x in ConfigFile.WebVideoOptions)
				{ if (!Arguments.ContainsKey(x.Key)) Arguments[x.Key] = x.Value; }

				// Configure output file type
				string PreferredMIME = "application/octet-stream", PreferredName = "video.avi";
				if (Arguments.ContainsKey("f")) // (ffmpeg output format)
				{
					switch (Arguments["f"])
					{
						case "avi":
							PreferredMIME = "video/msvideo";
							PreferredName = "onlinevideo.avi";
							break;
						case "mpeg1video":
						case "mpeg2video":
							PreferredMIME = "video/mpeg";
							PreferredName = "onlinevideo.mpg";
							break;
						case "mpeg4":
							PreferredMIME = "video/mp4";
							PreferredName = "onlinevideo.mp4";
							break;
						case "mpegts":
							PreferredMIME = "video/mp2t";
							PreferredName = "onlinevideo.mts";
							break;
						case "asf":
						case "asf_stream":
						case "wmv":
							PreferredMIME = "video/x-ms-asf";
							PreferredName = "onlinevideo.asf";
							break;
						case "mov":
							PreferredMIME = "video/qucktime";
							PreferredName = "onlinevideo.mov";
							break;
						case "ogg":
							PreferredMIME = "video/ogg";
							PreferredName = "onlinevideo.ogg";
							break;
						case "webm":
							PreferredMIME = "video/webm";
							PreferredName = "onlinevideo.webm";
							break;
						case "swf":
							PreferredMIME = "application/x-shockwave-flash";
							PreferredName = "onlinevideo.swf";
							break;
						case "rm":
							PreferredMIME = "application/vnd.rn-realmedia";
							PreferredName = "onlinevideo.rm";
							break;
						case "3gp":
							PreferredMIME = "video/3gpp";
							PreferredName = "onlinevideo.3gp";
							break;
						default:
							PreferredMIME = "application/octet-stream";
							PreferredName = "onlinevideo." + Arguments["f"];
							break;
					}
				}
				if (Arguments.ContainsKey("j") ||
				   Arguments.ContainsKey("J") ||
				   Arguments.ContainsKey("dump-json") ||
				   Arguments.ContainsKey("dump-single-json") ||
				   Arguments.ContainsKey("print-json"))
				{
					PreferredMIME = "application/json";
					PreferredName = "metadata.json";
				}

				// Set output file type over auto-detected (if need)
				if (!Arguments.ContainsKey("content-type"))
				{ Arguments.Add("content-type", PreferredMIME); }
				if (!Arguments.ContainsKey("filename"))
				{ Arguments.Add("filename", PreferredName); }

				// Load all parameters
				foreach (var Arg in Arguments)
				{
					if ((Arg.Key.StartsWith("vf") && Arguments["vcodec"] != "copy") ||
					   (Arg.Key.StartsWith("af") && Arguments["acodec"] != "copy"))
					{
						// Don't apply filters if codec is original
						FFmpegArgs += string.Format(" -{0} {1}", Arg.Key, Arg.Value);
						continue;
					}
					if (Arg.Key.StartsWith("filter"))
					{
						/* Currently may cause FFMPEG errors if combined with `-vcodec copy`:
						 * Filtergraph 'scale=480:-1' was defined for video output stream 0:0 but codec copy was selected.
						 * Filtering and streamcopy cannot be used together.
						 */
						FFmpegArgs += string.Format(" -{0} {1}", Arg.Key, Arg.Value);
						continue;
					}
					switch (Arg.Key.ToLowerInvariant())
					{
						//Key: Enable or disable ROVP
						case "enable":
							if (!ToBoolean(Arg.Value)) throw new Exception("This feature is disabled by administrator.");
							continue;
						//Keys: Common
						case "url":
						case "content-type":
						case "filename":
						case "prefer":
						case "gui":
						case "youtubedlapp":
						case "ffmpegapp":
							continue;
						case "noffmpeg":
							UseFFmpeg = !ToBoolean(Arg.Value);
							continue;
						//Keys: Youtube-dl
						/* 
						 * From:							yt-dlp.exe --help
						 * Finding regex mask:				\-[a-zA-Z]+
						 * Then "\n" replacement mask:		":\ncase "
						 * Then remove duplicates (AkelPad can).
						 * Then remove all keys with:		"--write".
						 * Then remove all keys with:		"force-write".
						 * Then remove all keys with:		"force-overwrites".
						 * Then remove all keys with:		"update".
						 * Then remove the key:				"U".
						 * May be other cleanup???
						 * Then remove dashes:				"--", "-"
						 * Then sort lines:					A -> Z
						 * Then Visual Studio will suggest lines to remove.
						 * Need to update for each release!
						 */
						case "abort-on-error":
						case "abort-on-unavailable-fragments":
						case "add-chapters":
						case "add-headers":
						case "add-metadata":
						case "age-limit":
						case "alias":
						case "allow-dynamic-mpd":
						case "ap-list-mso":
						case "ap-mso":
						case "ap-password":
						case "ap-username":
						case "audio-format":
						case "audio-multistreams":
						case "audio-quality":
						case "batch-file":
						case "bidi-workaround":
						case "break-match-filters":
						case "break-on-existing":
						case "break-per-input":
						case "buffer-size":
						case "cache-dir":
						case "check-all-formats":
						case "check-formats":
						case "clean-info-json":
						case "client-certificate":
						case "client-certificate-key":
						case "client-certificate-password":
						case "color":
						case "compat-options":
						case "concat-playlist":
						case "concurrent-fragments":
						case "config-locations":
						case "console-title":
						case "continue":
						case "convert-":
						case "convert-subs":
						case "convert-thumbnails":
						case "cookies":
						case "cookies-from-browser":
						case "date":
						case "dateafter":
						case "datebefore":
						case "default-search":
						case "download-archive":
						case "download-sections":
						case "downloader":
						case "downloader-args":
						case "dump-pages":
						case "embed-chapters":
						case "embed-info-json":
						case "embed-metadata":
						case "embed-subs":
						case "embed-thumbnail":
						case "enable-file-urls":
						case "encoding":
						case "exec":
						case "external-downloader":
						case "external-downloader-args":
						case "extract-audio":
						case "extractor-args":
						case "extractor-descriptions":
						case "extractor-retries":
						case "ffmpeg-location":
						case "file-access-retries":
						case "fixup":
						case "flat-playlist":
						case "force-download-archive":
						case "force-ipv":
						case "force-keyframes-at-cuts":
						case "format":
						case "format-sort":
						case "format-sort-force":
						case "fragment-retries":
						case "geo-verification-proxy":
						case "get-audio":
						case "get-comments":
						case "help":
						case "hls-split-discontinuity":
						case "hls-use-mpegts":
						case "http-chunk-size":
						case "ies":
						case "ignore-config":
						case "ignore-dynamic-mpd":
						case "ignore-errors":
						case "ignore-no-formats-error":
						case "impersonate":
						case "js-runtimes":
						case "keep-fragments":
						case "keep-video":
						case "lazy-playlist":
						case "legacy-server-connect":
						case "limit-rate":
						case "list-extractors":
						case "list-formats":
						case "list-impersonate-targets":
						case "list-subs":
						case "list-thumbnails":
						case "live-from-start":
						case "load-info-json":
						case "mark-watched":
						case "match-filters":
						case "max-downloads":
						case "max-filesize":
						case "max-sleep-interval":
						case "merge-output-format":
						case "min-filesize":
						case "min-sleep-interval":
						case "mtime":
						case "netrc":
						case "netrc-cmd":
						case "netrc-location":
						case "newline":
						case "no-abort-on-error":
						case "no-abort-on-unavailable-fragments":
						case "no-add-chapters":
						case "no-add-metadata":
						case "no-allow-dynamic-mpd":
						case "no-audio-multistreams":
						case "no-batch-file":
						case "no-break-match-filters":
						case "no-break-on-existing":
						case "no-break-per-input":
						case "no-cache-dir":
						case "no-check-certificates":
						case "no-check-formats":
						case "no-clean-info-json":
						case "no-config":
						case "no-config-locations":
						case "no-continue":
						case "no-cookies":
						case "no-cookies-from-browser":
						case "no-download":
						case "no-download-archive":
						case "no-embed-chapters":
						case "no-embed-info-json":
						case "no-embed-metadata":
						case "no-embed-subs":
						case "no-embed-thumbnail":
						case "no-exec":
						case "no-flat-playlist":
						case "no-force-keyframes-at-cuts":
						case "no-force-overwrites":
						case "no-format-sort-force":
						case "no-get-comments":
						case "no-hls-split-discontinuity":
						case "no-hls-use-mpegts":
						case "no-ignore-dynamic-mpd":
						case "no-ignore-errors":
						case "no-ignore-no-formats-error":
						case "no-js-":
						case "no-js-runtimes":
						case "no-keep-fragments":
						case "no-keep-video":
						case "no-lazy-playlist":
						case "no-live-from-start":
						case "no-mark-watched":
						case "no-match-filters":
						case "no-mtime":
						case "no-overwrites":
						case "no-part":
						case "no-playlist":
						case "no-plugin-dirs":
						case "no-post-overwrites":
						case "no-prefer-free-formats":
						case "no-progress":
						case "no-quiet":
						case "no-remote-components":
						case "no-remove-chapters":
						case "no-resize-buffer":
						case "no-restrict-filenames":
						case "no-simulate":
						case "no-skip-unavailable-fragments":
						case "no-split-chapters":
						case "no-sponsorblock":
						case "no-update":
						case "no-video-multistreams":
						case "no-wait-for-video":
						case "no-warnings":
						case "no-windows-filenames":
						case "no-write-auto-subs":
						case "no-write-automatic-subs":
						case "no-write-comments":
						case "no-write-description":
						case "no-write-info-json":
						case "no-write-playlist-metafiles":
						case "no-write-subs":
						case "no-write-thumbnail":
						case "output":
						case "output-na-placeholder":
						case "parse-metadata":
						case "part":
						case "password":
						case "paths":
						case "playlist-items":
						case "playlist-random":
						case "playlist-reverse":
						case "plugin-dirs":
						case "postprocessor-args":
						case "ppa":
						case "prefer-free-formats":
						case "prefer-insecure":
						case "preset-alias":
						case "print":
						case "print-to-file":
						case "print-traffic":
						case "progress":
						case "progress-delta":
						case "progress-template":
						case "proxy":
						case "quiet":
						case "recode-video":
						case "remote-components":
						case "remove-chapters":
						case "remux-video":
						case "replace-in-metadata":
						case "resize-buffer":
						case "restrict-filenames":
						case "retries":
						case "retry-sleep":
						case "rm-cache-dir":
						case "S-force":
						case "simulate":
						case "skip-download":
						case "skip-playlist-after-errors":
						case "skip-unavailable-fragments":
						case "sleep-interval":
						case "sleep-requests":
						case "sleep-subtitles":
						case "socket-timeout":
						case "source-address":
						case "split-chapters":
						case "sponsorblock-api":
						case "sponsorblock-chapter-title":
						case "sponsorblock-mark":
						case "sponsorblock-remove":
						case "sub-":
						case "sub-format":
						case "sub-langs":
						case "throttled-rate":
						case "trim-filenames":
						case "twofactor":
						case "use-extractors":
						case "use-postprocessor":
						case "username":
						case "verbose":
						case "version":
						case "video-multistreams":
						case "video-password":
						case "wait-for-video":
						case "windows-filenames":
						case "xattrs":
						case "xff":
						case "yes-playlist":
						case "a":
						case "audio":
						case "based":
						case "c":
						case "compatible":
						case "dl":
						case "dlc":
						case "dlp-ejs":
						case "encode":
						case "extracted":
						case "factor":
						case "filler":
						case "Forwarded-For":
						case "free":
						case "generated":
						case "generic":
						case "h":
						case "i":
						case "inf":
						case "k":
						case "language":
						case "letter":
						case "live":
						case "MAX":
						case "modified":
						case "n":
						case "o":
						case "only":
						case "P":
						case "preview":
						case "processed":
						case "Processing":
						case "q":
						case "R":
						case "range":
						case "restricted":
						case "restriction":
						case "S":
						case "sensitive":
						case "separated":
						case "side":
						case "specific":
						case "STOP":
						case "system":
						case "title":
						case "tty":
						case "v":
						case "w":
						case "x":
							YoutubeDlArgs += string.Format("--{0} {1} ", Arg.Key, Arg.Value);
							continue;
						//Keys: Youtube-Dl (JSON output)
						case "j":
						case "J":
						case "dump-json":
						case "dump-single-json":
						case "print-json":
							YoutubeDlArgs += string.Format(" --{0} {1} ", Arg.Key, Arg.Value);
							UseFFmpeg = false;
							GetYoutubeJson = true;
							continue;
						//Keys: FFmpeg
						case "loglevel":
						case "max_alloc":
						case "filter_threads":
						case "filter_complex_threads":
						case "stats":
						case "max_error_rate":
						case "bits_per_raw_sample":
						case "vol":
						case "codec":
						case "pre":
						case "t":
						case "to":
						case "fs":
						case "ss":
						case "sseof":
						case "seek_timestamp":
						case "timestamp":
						case "metadata":
						case "program":
						case "target":
						case "apad":
						case "frames":
						case "filter_script":
						case "reinit_filter":
						case "discard":
						case "disposition":
						case "vframes":
						case "r":
						case "s":
						case "aspect":
						case "vn":
						case "vcodec":
						case "timecode":
						case "pass":
						case "ab":
						case "b":
						case "dn":
						case "aframes":
						case "aq":
						case "ar":
						case "ac":
						case "an":
						case "acodec":
						case "sn":
						case "scodec":
						case "stag":
						case "fix_sub_duration":
						case "canvas_size":
						case "spre":
						case "f":
							FFmpegArgs += string.Format(" -{0} {1} ", Arg.Key, Arg.Value);
							continue;
						//Keys: FFmpeg (other)
						case "vf":
						case "af":
						case "filter":
							//ffmpeg filters parsed above
							continue;
						//Unknown or blacklisted keys. Don't process.
						default:
							Log.WriteLine(" Unsupported argument: {0}", Arg.Key);
							continue;
					}
				}

				// Configure YT-DLP and FFmpeg processes and prepare data stream
				ProcessStartInfo YoutubeDlStart = new();
				YoutubeDlStart.FileName = ConfigFile.WebVideoOptions["YouTubeDlApp"] ?? "yt-dlp";
				YoutubeDlStart.Arguments = string.Format("\"{0}\" {1} -o -", Arguments["url"], YoutubeDlArgs);
				YoutubeDlStart.RedirectStandardOutput = true;
				YoutubeDlStart.RedirectStandardError = true;

				ProcessStartInfo FFmpegStart = new();
				FFmpegStart.FileName = ConfigFile.WebVideoOptions["FFmpegApp"] ?? "ffmpeg";
				FFmpegStart.Arguments = string.Format("-i pipe: {0} pipe:", FFmpegArgs);
				FFmpegStart.RedirectStandardInput = true;
				FFmpegStart.RedirectStandardOutput = true;
				FFmpegStart.RedirectStandardError = false;

				video.Available = true;
				video.ErrorMessage = "";
				video.ContentType = Arguments["content-type"];
				video.FileName = Arguments["filename"];

				// Start both processes
				Process YoutubeDl = null;
				Process FFmpeg = null;
				if (UseFFmpeg)
				{
					Log.WriteLine(" Video convert: {0} {1} | {2} {3}", YoutubeDlStart.FileName, YoutubeDlStart.Arguments, FFmpegStart.FileName, FFmpegStart.Arguments);
					YoutubeDl = Process.Start(YoutubeDlStart);
					FFmpeg = Process.Start(FFmpegStart);
				}
				else
				{
					Log.WriteLine(" Video convert: {0} {1}", YoutubeDlStart.FileName, YoutubeDlStart.Arguments);
					YoutubeDl = Process.Start(YoutubeDlStart);
				}

				// Calculate approximately end time
				DateTime EndTime = DateTime.Now.AddSeconds(30);

				// Enable YT-DLP error handling
				if (!GetYoutubeJson)
				{
					YoutubeDl.ErrorDataReceived += (o, e) =>
					{
						Console.WriteLine("{0}", e.Data);
						if (e.Data != null && e.Data.StartsWith("ERROR:"))
						{
							video.Available = false;
							video.ErrorMessage = "Online video failed to download: " + e.Data[7..];
							Log.WriteLine(false, false, " yt-dlp: {0}", e.Data);
						}
						if (e.Data != null && e.Data.StartsWith("WARNING:"))
						{
							Log.WriteLine(false, false, " yt-dlp: {0}", e.Data);
						}
						if (e.Data != null && Regex.IsMatch(e.Data, @"\[download\].*ETA (\d\d:\d\d:\d\d|\d\d:\d\d)"))
						{
							Match match = Regex.Match(e.Data, @"\[download\].*ETA (\d\d:\d\d:\d\d|\d\d:\d\d)");
							//assuming, it's succcessfull & have 2 groups

							string ETA = (Regex.IsMatch(match.Groups[1].Value, @"\d\d:\d\d:\d\d")) ? match.Groups[1].Value : "00:" + match.Groups[1].Value;
							try
							{ EndTime = DateTime.Now.Add(TimeSpan.Parse(ETA)); }
							catch (OverflowException)
							{
								//"The TimeSpan string '25:34:39' could not be parsed because at least one of the numeric components is out of range or contains too many digits."
							}
						}
					};
					YoutubeDl.BeginErrorReadLine();
				}

				// (Here FFmpeg error handling might be useful, but it's output is hard to parse)

				// Redirect STDIN/STDOUT streams
				if (!GetYoutubeJson)
				{
					if (UseFFmpeg)
					{
						// - Redirect yt-dlp STDOUT to FFmpeg STDIN stream, and FFmpeg STDOUT to return stream
						new Task(() =>
						{
							YoutubeDl.StandardOutput.BaseStream.CopyTo(FFmpeg.StandardInput.BaseStream);
						}).Start();
						video.VideoStream = FFmpeg.StandardOutput.BaseStream;
					}
					else
					{
						// - Redirect yt-dlp STDOUT to return stream
						video.VideoStream = YoutubeDl.StandardOutput.BaseStream;
					}
				}
				if (GetYoutubeJson)
				{
					// - Redirect yt-dlp STDERR to return stream (video metadata JSON)
					video.VideoStream = YoutubeDl.StandardError.BaseStream;
				}

				// Initialize idleness hunters
				new Task(() =>
				{
					while (DateTime.Now < EndTime) { Thread.Sleep(1000); }
					float YoutubeDlCpuLoad = 0;
					while (!YoutubeDl.HasExited)
					{
						Thread.Sleep(1000);
						PreventProcessIdle(ref YoutubeDl, ref YoutubeDlCpuLoad, Log);
					}
				}).Start();
				if (UseFFmpeg) new Task(() =>
				 {
					 while (DateTime.Now < EndTime) { Thread.Sleep(1000); }
					 float FFmpegCpuLoad = 0;
					 while (!FFmpeg.HasExited)
					 {
						 Thread.Sleep(1000);
						 PreventProcessIdle(ref FFmpeg, ref FFmpegCpuLoad, Log);
					 }
				 }).Start();

				// Wait for YT-DLP & FFmpeg to start working or end with error
				Thread.Sleep(5000);
			}
			catch (Exception VidCvtError)
			{
				video.Available = false;
				video.ErrorMessage = VidCvtError.Message;
				Log.WriteLine("Cannot convert video: {0} - {1}", VidCvtError.GetType(), VidCvtError.Message);
			}
			return video;
		}
	}

	/// <summary>
	/// Converted web video
	/// </summary>
	class WebVideo
	{
		/// <summary>
		/// The video file stream
		/// </summary>
		public Stream VideoStream { get; internal set; }
		/// <summary>
		/// The video container MIME content type
		/// </summary>
		public string ContentType { get; internal set; }
		/// <summary>
		/// The video container file name
		/// </summary>
		public string FileName { get; internal set; }
		/// <summary>
		/// Is the download &amp; convert successful
		/// </summary>
		public bool Available { get; internal set; }
		/// <summary>
		/// Error messages (if any)
		/// </summary>
		public string ErrorMessage { get; internal set; }

		public WebVideo()
		{
			Available = false;
			ErrorMessage = "WebVideo not configured!";
			ContentType = "text/plain";
			FileName = "webvideo.err";
		}
	}
}
