#!/bin/sh
#Download a YouTube video and return it as a stream of MTS with MPEG2 data.
#Requires youtube-dl and ffmpeg
youtube-dl "$1" -o - | ffmpeg -i pipe: -vcodec mpeg2video -acodec mp2 -f mpegts pipe: