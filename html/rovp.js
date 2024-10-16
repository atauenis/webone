/* Scripts for Retro Online Video Player web form. */

function validate(form) {
	var good = true;

	//check Container
	switch(form.f.value)
	{
		case "mpeg1video":
			if (form.vcodec.value != 'mpeg1video') {
				alert('MPEG1 muxer supports only codec MPEG1 for video.');
				return false;
			}
			break;
		case "mpeg2video":
			if (form.vcodec.value != 'mpeg2video') {
				alert('MPEG2 muxer supports only codec MPEG2 for video.');
				return false;
			}
			break;
		case 'ogg':
			if (form.vcodec.value != 'theora') { good = false; }
			if (form.acodec.value != 'vorbis -strict -2') { good = false; }
			if (!good) {
				alert('Ogg is supporting only Theora video and Vorbis audio codecs.');
				return false;
			}
			break;
		case 'webm':
			switch (form.vcodec.value) {
				case 'vp8': case 'vp9': case 'av1':
					good = true; break;
				default:
					good = false;
			}
			if (good) switch (form.acodec.value) {
				case 'vorbis -strict -2': case 'opus -strict -2':
					good = true; break;
				default:
					good = false;
			}
			if (!good) {
				alert('Only VP8 or VP9 or AV1 video and Vorbis or Opus audio and WebVTT subtitles are supported for WebM.');
				return false;
			}
			break;
		case 'swf':
			if (form.type[0].checked || form.type[7].checked || form.type[8].checked) { good = true; }
			else {
				good = false;
				alert('Flash videos can be played only via Macromedia/Adobe plug-in or player.');
				return false
			}
			switch (form.vcodec.value)
			{
				case "mjpeg":
				case "flv":
				case "vp6":
					good = true;
					break;
				default:
					good = false
					break;
            }
			if(form.acodec.value != 'mp3') { good = false; }
			if (!good)
			{
				alert('SWF muxer only supports FLV/MJPEG video and MP3 audio.');
				return false;
			}
	}
	if (!good) return false;

	//check Video Codec
	switch(form.vcodec.value)
	{
		case 'h263':
			switch (form.vf.value) {
				case 'scale="128x96"':
				case 'scale="176x144"':
				case 'scale="352x288"':
				case 'scale="704x576"':
				case 'scale="1408x1152"':
					good = true;
					break;
				default:
					good = false;
					alert('H.263 - Valid resolutions are 128x96, 176x144, 352x288, 704x576, and 1408x1152.');
					return false;
			}
			break;
		case 'msvideo1':
			switch (form.vf.value) {
				case 'scale="1024x768"':
				case 'scale="800x600"':
				case 'scale="640x480"':
				case 'scale="320x240"':
					good = true;
					break;
				default:
					good = false;
					alert('MS Video 1 - width and height must be multiples of 4. Choose 4:3 video format.');
					return false;
			}
			break;
	}
	if (!good) return false;

	//check Audio Codec
	switch(form.acodec.value)
	{
		case 'vorbis -strict -2':
			if (form.ac.value != '2') { good = false; }
			if (!good) {
				alert('Current FFmpeg Vorbis encoder only supports 2 channels.');
				return false;
			}
	}

	return good;
}
