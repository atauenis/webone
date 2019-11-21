Windows batch script to use Youtube-Dl as video converter for WebOne.
System requirements:
* Server:
* MS Windows XP+
* WebOne proxy server v0.9.2 or newer
* Youtube-DL EXE (https://github.com/ytdl-org/youtube-dl/releases)
* LibAV's avconv.exe (http://builds.libav.org/windows/)
* Fast multicore CPU
* Fast internet and LAN connection

* Client:
* Streaming video player like Windows Media Player or VLC
* Audio and video codecs (or K-Lite Codec Pack)
* Fast LAN connection

How to use:
Step 1. Unpack yt.bat into WebOne folder.
Step 2. Copy folder with avconv.exe into WebOne folder.
Step 3. Copy youtube-dl.exe into WebOne folder.
Step 4. Add to [Converters] section of webone.conf this line:
         yt.bat "%SRCURL%"
Step 5. Add to webone conf these lines:
         [FixableURL:^(http://www\.|http://)youtube.com/watch]
         Redirect=http://%Proxy%/!convert/?url=%Url%&util=yt.bat
Step 6. Open any player on client PC and open url
         http://youtube.com/watch?v=any_video_id                     * via proxy
        or
         http://proxyhost:port/!convert/?url=URL_LIKE_ABOVE&util=yt.bat  *direct
        The requested video should begin playing in the player.

Configuring:
- To change path to youtube-dl.exe and/or avconv.exe edit yt.bat and
  add "cd disk:\path\to\binaries" line on top or edit paths directly
- To change output codecs, resolution or other parameters of video
  stream edit "-vcodec mpeg2video -acodec mp3 -f mpegts" line in yt.bat.

List of available codecs and formats can be get through running avconv.exe
on proxy machine with "-codecs" and "-formats" arguments.