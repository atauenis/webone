using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
			WebVideo video = new WebVideo();
			try
			{
				string YoutubeDlArgs = "";
				string FFmpegArgs = "";
				bool NoFFmpeg = false;

				if (!Arguments.ContainsKey("url"))
				{ throw new InvalidOperationException("Internet video address is missing."); }

				if (!Arguments.ContainsKey("content-type"))
				{ Arguments.Add("content-type", "application/octet-stream"); }

				if (!Arguments.ContainsKey("filename"))
				{ Arguments.Add("filename", "converted.avi"); }

				foreach (var Arg in Arguments)
				{
					switch (Arg.Key.ToLowerInvariant())
					{
						case "url":
						case "content-type":
						case "filename":
							continue;
						case "dont-convert":
							if (ToBoolean(Arg.Value))
								NoFFmpeg = true;
							continue;
						case "abort-on-error":
						case "ignore-config":
						case "mark-watched":
						case "no-mark-watched":
						case "proxy":
						case "socket-timeout":
						case "source-address":
						case "4":
						case "6":
						case "geo-verification-proxy":
						case "geo-bypass":
						case "no-geo-bypass":
						case "geo-bypass-country":
						case "geo-bypass-ip-block":
						case "include-ads":
						case "limit-rate":
						case "retries":
						case "fragment-retries":
						case "skip-unavailable-fragments":
						case "abort-on-unavailable-fragment":
						case "keep-fragments":
						case "buffer-size":
						case "no-resize-buffer":
						case "http-chunk-size":
						case "xattr-set-filesize ":
						case "hls-prefer-native":
						case "hls-prefer-ffmpeg":
						case "hls-use-mpegts":
						case "external-downloader":
						case "external-downloader-args":
						case "cookies":
						case "no-cache-dir":
						case "newline":
						case "no-progress":
						case "no-check-certificate":
						case "prefer-insecure":
						case "user-agent":
						case "referer":
						case "add-header":
						case "bidi-workaround":
						case "sleep-interval":
						case "max-sleep-interval":
						case "format":
						case "youtube-skip-dash-manifest":
						case "merge-output-format":
						case "username":
						case "password":
						case "twofactor":
						case "video-password":
						case "ap-mso":
						case "ap-username":
						case "ap-password":
						case "extract-audio":
						case "audio-format":
						case "audio-quality":
						case "recode-video":
						case "postprocessor-args":
						case "embed-subs":
						case "embed-thumbnail":
						case "add-metadata":
						case "metadata-from-title":
						case "xattrs":
						case "fixup":
						case "prefer-avconv":
						case "prefer-ffmpeg":
						case "convert-subs":
							YoutubeDlArgs += string.Format(" --{0} {1}", Arg.Key, Arg.Value);
							continue;
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
						case "filter":
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
						case "vf":
						case "ab":
						case "b":
						case "dn":
						case "aframes":
						case "aq":
						case "ar":
						case "ac":
						case "an":
						case "acodec":
						case "af":
						case "sn":
						case "scodec":
						case "stag":
						case "fix_sub_duration":
						case "canvas_size":
						case "spre":
						case "f":
							FFmpegArgs += string.Format(" -{0} {1}", Arg.Key, Arg.Value);
							continue;
						default:
							Log.WriteLine(" Unsupported argument: {0}", Arg.Key);
							continue;
					}
				}

				ProcessStartInfo YoutubeDlStart = new();
				YoutubeDlStart.FileName = "youtube-dl";
				YoutubeDlStart.Arguments = string.Format("\"{0}\"{1} -o -", Arguments["url"], YoutubeDlArgs);
				YoutubeDlStart.RedirectStandardOutput = true;
				YoutubeDlStart.RedirectStandardError = true;

				ProcessStartInfo FFmpegStart = new();
				FFmpegStart.FileName = "ffmpeg";
				FFmpegStart.Arguments = string.Format("-i pipe: {0} pipe:", FFmpegArgs);
				FFmpegStart.RedirectStandardInput = true;
				FFmpegStart.RedirectStandardOutput = true;

				video.Available = true;
				video.ErrorMessage = "";
				video.ContentType = Arguments["content-type"];
				video.FileName = Arguments["filename"];

				if (NoFFmpeg)
				{
					Log.WriteLine(" Video convert: {0} {1}", YoutubeDlStart.FileName, YoutubeDlStart.Arguments);
					Process YoutubeDl = Process.Start(YoutubeDlStart);
					video.VideoStream = YoutubeDl.StandardOutput.BaseStream;

					YoutubeDl.ErrorDataReceived += (o, e) =>
					{
						Console.WriteLine("{0}", e.Data);
						if (e.Data != null && e.Data.StartsWith("ERROR:"))
						{
							video.Available = false;
							video.ErrorMessage = "Online video failed to download: " + e.Data[7..];
							Log.WriteLine(false, false, " youtube-dl: {0}", e.Data);
						}
						if (e.Data != null && e.Data.StartsWith("WARNING:"))
						{
							Log.WriteLine(false, false, " youtube-dl: {0}", e.Data);
						}
					};
					YoutubeDl.BeginErrorReadLine();

					new Task(() =>
					{
						Thread.Sleep(60000);
						float YoutubeDlCpuLoad = 0;
						while (!YoutubeDl.HasExited)
						{ Thread.Sleep(1000); PreventProcessIdle(ref YoutubeDl, ref YoutubeDlCpuLoad, Log); }
					}).Start();

					Thread.Sleep(5000); //wait for youtube-dl to start work or end with error
				}
				else
				{
					Log.WriteLine(" Video convert: {0} {1} | {2} {3}", YoutubeDlStart.FileName, YoutubeDlStart.Arguments, FFmpegStart.FileName, FFmpegStart.Arguments);
					Process YoutubeDl = Process.Start(YoutubeDlStart);
					Process FFmpeg = Process.Start(FFmpegStart);

					YoutubeDl.ErrorDataReceived += (o, e) =>
					{
						Console.WriteLine("{0}", e.Data);
						if (e.Data != null && e.Data.StartsWith("ERROR:"))
						{
							video.Available = false;
							video.ErrorMessage = "Online video failed to download: " + e.Data[7..];
							Log.WriteLine(false, false, " youtube-dl: {0}", e.Data);
						}
						if (e.Data != null && e.Data.StartsWith("WARNING:"))
						{
							Log.WriteLine(false, false, " youtube-dl: {0}", e.Data);
						}
					};
					YoutubeDl.BeginErrorReadLine();

					new Task(() =>
					{
						YoutubeDl.StandardOutput.BaseStream.CopyTo(FFmpeg.StandardInput.BaseStream);
					}).Start();
					video.VideoStream = FFmpeg.StandardOutput.BaseStream;

					new Task(() =>
					{
						Thread.Sleep(60000);
						float YoutubeDlCpuLoad = 0;
						while (!YoutubeDl.HasExited)
						{ Thread.Sleep(1000); PreventProcessIdle(ref YoutubeDl, ref YoutubeDlCpuLoad, Log); }
					}).Start();

					new Task(() =>
					{
						Thread.Sleep(60000);
						float FFmpegCpuLoad = 0;
						while (!FFmpeg.HasExited)
						{ Thread.Sleep(1000); PreventProcessIdle(ref FFmpeg, ref FFmpegCpuLoad, Log); }
					}).Start();

					Thread.Sleep(5000); //wait for youtube-dl & ffmpeg to start work or end with error
				}
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
