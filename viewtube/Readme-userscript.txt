ViewTube-webone
===============

This is a patched version of ViewTube user script that utilize WebOne 0.9.2+
to convert YouTube videos into particular formats (containers) and codecs.

It can be used to download ready to play on older computers versions of videos
or to watch videos directly through browsers (player plugins).


Original version of ViewTube was developed by sebaro:
(C) 2010 - 2019 Sebastian Luncan (http://sebaro.pro/viewtube).

Patch by Alexander Tauenis (https://github.com/atauenis/webone).


System requirements:
* Firefox 3.5+, Opera 11.64+, or Google Chrome
* Greasemonkey or Tampermonkey add-on (except on Opera Presto)
* WebOne proxy server running on another PC with avconv

On all browsers it's need to install Greasemonkey or Tampermonkey add-ons.
The only exception is Opera 11/12, where the user script can work without
extensions too (don't forget to allow it on HTTPS pages via opera:config).


How to use:
After the user script been installed, default Youtube player gets replaced by
ViewTube frame. Use the bottom bar to control the frame (play/options/download).

Open the settings frame. It will contain options regarding displaying the
video. First option ("Embed video with") sets way how to show the video:
* Video - Use browser built-in HTML5 player. Only for modern browsers.
          It's fast, but very limited to browser capabilities.
* Object and Embed - Use plugins provided by installed media players.
                     Plugin is selecting via right menu ("play with").
                     Only VLC plugin is currently supported. Probably,
                     some versions of QuickTime will work too.
* Protocol - Use external player (see http://sebaro.pro/viewtube for details).
             Do not use with viewtube.reg / viewtube.vbs scripts!
             They are only for the original version of ViewTube.

Second option contains definition and container for video. But it's better
to select them via the box in the bottom bar.

Further options are more detailed described in Readme of ViewTube:
https://github.com/sebaro/ViewTube/blob/master/README.md


On bottom of the settings frame there will be an option, called
"Use proxy for codec convertion". Turn it on. WebOne options became below.

Enter in first box IP or hostname of proxy server computer.

The second box containing the video format (container) that will be used to play
the video. Try all listed.

The long row is related to codec conversion. It containing:
* video codec (may be set to "original" to do not touch the video stream);
* video resolution;
* video bitrate (you may use "K" and "M" to shorten "kb/s" and "mb/s" here or
  leave "max" or empty to let AVconv set the bitrate);
* audio codec (may be set to to "original" to don't touch the audio stream);
* audio channels;
* audio bitrate (similar to video bitrate).

All settings that are set here will override settings above.

Now if the proxy usage is enabled, ViewTube will work through WebOne.
To return ViewTube to default state, disable the proxy option.


Notes:
Some codecs require encoder DLLs to be installed on the proxy machine.
WebM format can contain only VP8 video stream and AC3 audio stream.
Ogg format can conatin only Theora video and Vorbis audio streams.

The most troubleless way to watch videos on Windows 98/2000 is to use
Protocol and VLC Media Player or download the videos and play locally
via Media Player Classic and K-Lite Codec Pack.