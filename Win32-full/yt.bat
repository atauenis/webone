@rem Download a YouTube video and return it as a stream of MTS with MPEG2 data.
@rem Requires youtube-dl.exe and ffmpeg.exe in same folder as the batch and WebOne.
@youtube-dl "%1" -o - | ffmpeg -i pipe: -vcodec mpeg2video -acodec mp2 -f mpegts pipe: