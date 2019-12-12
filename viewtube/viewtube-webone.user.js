// ==UserScript==
// @name		ViewTube
// @version		2019.12.05-AT-1
// @description		Watch videos from video sharing websites with extra options.
// @author		sebaro
// @namespace		http://sebaro.pro/viewtube
// @downloadURL		https://gitlab.com/sebaro/viewtube/raw/master/viewtube.user.js
// @updateURL		https://gitlab.com/sebaro/viewtube/raw/master/viewtube.user.js
// @icon		https://gitlab.com/sebaro/viewtube/raw/master/viewtube.png
// @include		http://youtube.com*
// @include		http://www.youtube.com*
// @include		https://youtube.com*
// @include		https://www.youtube.com*
// @include		http://gaming.youtube.com*
// @include		https://gaming.youtube.com*
// @include		http://m.youtube.com*
// @include		https://m.youtube.com*
// @include		http://dailymotion.com*
// @include		http://www.dailymotion.com*
// @include		https://dailymotion.com*
// @include		https://www.dailymotion.com*
// @include		http://vimeo.com*
// @include		http://www.vimeo.com*
// @include		https://vimeo.com*
// @include		https://www.vimeo.com*
// @include		http://metacafe.com*
// @include		http://www.metacafe.com*
// @include		https://metacafe.com*
// @include		https://www.metacafe.com*
// @include		http://veoh.com*
// @include		http://www.veoh.com*
// @include		https://veoh.com*
// @include		https://www.veoh.com*
// @include		http://viki.com*
// @include		http://www.viki.com*
// @include		https://viki.com*
// @include		https://www.viki.com*
// @include		http://imdb.com*
// @include		http://www.imdb.com*
// @include		https://imdb.com*
// @include		https://www.imdb.com*
// @noframes
// @grant		none
// @run-at		document-end
// ==/UserScript==


/*

  Copyright (C) 2010 - 2019 Sebastian Luncan

  This program is free software: you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation, either version 3 of the License, or
  (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program. If not, see <http://www.gnu.org/licenses/>.

  Website: http://sebaro.pro/viewtube
  Contact: http://sebaro.pro/contact

*/


(function() {


// Don't run on frames or iframes
if (window.top != window.self) return;


// ==========Variables========== //

// Userscript
var userscript = 'ViewTube';
var website = 'http://sebaro.pro/viewtube';
var contact = 'http://sebaro.pro/contact';

// Page
var page = {win: window, doc: window.document, body: window.document.body, url: window.location.href, site: window.location.hostname.match(/([^.]+)\.[^.]+$/)[1]};

// Player
var player = {};
var myPlayerWindow;
var myPlayerPanelHeight = 30;

// Features/Options
var feature = {'autoplay': true, 'definition': true, 'container': true, 'dash': false, 'direct': false, 'widesize': true, 'fullsize': true};
var option = {'embed': 'Video', 'media': 'Auto', 'autoplay': false, 'autoget': false, 'definition': 'High Definition', 'container': 'MP4', 'dash': false, 'direct': false, 'widesize': false, 'fullsize': false, 'avcUseProxy': 'Off', 'avcWebOneHost': 'proxy host', 'avcContainer':  ' -f mpegts', 'avcVCodec': ' -vcodec copy', 'avcVSize': '', 'avcVBitrate': 'max', 'avcACodec': ' -acodec copy', 'avcAChannels': '', 'avcABitrate': 'max'};

// Embed
var embedtypes = ['Video', 'Object', 'Embed', 'Protocol'];
var embedcontent = {
  'Video': '<br><br>The video should be loading. If it doesn\'t load, make sure your browser supports HTML5\'s Video and this video codec. If you think it\'s a script issue, please report it <a href="' + contact + '" style="color:#00892C">here</a>.',
  'Object': '<br><br>The video should be loading. If it doesn\'t load, make sure a video plugin is installed. If you think it\'s a script issue, please report it <a href="' + contact + '" style="color:#00892C">here</a>.<param name="scale" value="aspect"><param name="stretchtofit" value="true"><param name="autostart" value="true"><param name="autoplay" value="true">',
  'Embed': '<br><br>The video should be loading. If it doesn\'t load, make sure a video plugin is installed. If you think it\'s a script issue, please report it <a href="' + contact + '" style="color:#00892C">here</a>.<param name="scale" value="aspect"><param name="stretchtofit" value="true"><param name="autostart" value="true"><param name="autoplay" value="true">'
};

// Media
var mediatypes = {'MP4': 'video/mp4', 'WebM': 'video/webm', 'M3U8': 'application/x-mpegURL', 'M3U8*': 'application/vnd.apple.mpegURL', 'VLC': 'application/x-vlc-plugin', 'VLC*': 'application/x-vlc-plugin'}
if (navigator.platform.indexOf('Win') != -1) {
  mediatypes['WMP'] = 'application/x-ms-wmp';
  mediatypes['WMP*'] = 'application/x-mplayer2';
  mediatypes['QT'] = 'video/quicktime';
}
else if (navigator.platform.indexOf('Mac') != -1) {
  mediatypes['QT'] = 'video/quicktime';
}
else {
  mediatypes['Totem'] = 'application/x-totem-plugin';
  mediatypes['Xine'] = 'application/x-xine-plugin';
}
var mediakeys = [];
for (var mediakey in mediatypes) {
  mediakeys.push(mediakey);
}

// Proxy server options
var proxyobject = {
	containers: {
		//'MP4': ' -f mp4', //don't work in avconv 11.3
		'MTS': ' -f mpegts', 
		'AVI': ' -f avi', 
		'ASF': '  -f asf', 
		'ASF*': ' -f asf_stream',
		'WebM': ' -f webm',
		'Ogg': ' -f ogg'
	},
	vcodecs: {
		'Original': ' -vcodec copy',
		'H.264': ' -vcodec h264',
		'Theora': ' -vcodec libtheora',
		'VP8': ' -vcodec libvpx',
		'MP1': ' -vcodec mpeg1video',
		'MP2': ' -vcodec mpeg2video',
		'MP4': ' -vcodec mpeg4',
		'MP4*': ' -vcodec msmpeg4',
		'WMV7': ' -vcodec wmv1',
		'WMV8': ' -vcodec wmv2',
		'WMV9': ' -vcodec wmv3'
	},
	acodecs: {
		'Original': ' -acodec copy',
		'MP2': ' -acodec mp2',
		'MP3': ' -acodec mp3',
		'AAC': ' -acodec aac',
		'AC3': ' -acodec ac3',
		'Vorbis': ' -acodec libvorbis',
		'WMA1': ' -acodec wmav1',
		'WMA2': ' -acodec wmav2'
	},
	vsize: {
		'Orig.': '',
		'96': ' -s 128x96',
		'144': ' -s 256x144',
		'240': ' -s 320x240',
		'360': ' -s 480x360',
		'480': ' -s 640x480',
		'720': ' -s 1280x720',
		'1080': ' -s 1920x1080'
	},
	achannels: {
		'Original': '',
		'Mono': ' -ac 1',
		'Stereo': ' -ac 2',
		'Quadro': ' -ac 4'
	}
};

// Sources
var sources = {};

// Intervals
var intervals = [];


// ==========Functions========== //

function GetAvcURL(VideoUrl){	
	//make URL of video transcoded through proxy server
	var avcSrcUrl = VideoUrl.replace('^http://','https://'); //if WebOne is default proxy for pages in browser, all links will be always HTTP

	//check if proxy should be used
	if(option['avcUseProxy'] != 'On')
	{ return VideoUrl; }

	//check for DVL mode
	if (avcSrcUrl == page.win.location.href)
	{ return avcSrcUrl; }

	//make proxy and source URL
	var avcUrl = 'http://' + option['avcWebOneHost'] + '/!convert/?url=';
	avcUrl += encodeURIComponent(avcSrcUrl) + '&util=avconv&arg=';

	//make parameters
	var avcArgs = option['avcVCodec']
		+ option['avcVSize']
		+ option['avcACodec']
		+ option['avcAChannels']
		+ option['avcContainer'];

	//make bitrate if need
	var avcVBitrate = option['avcVBitrate'];
  	if (avcVBitrate != '' && avcVBitrate != 'max' && avcVBitrate != null) { avcVBitrate = ' -b:v ' + avcVBitrate; }
	else avcVBitrate = '';
	avcArgs += avcVBitrate;

	var avcABitrate = option['avcABitrate'];
  	if (avcABitrate != '' && avcABitrate != 'max' && avcABitrate != null) { avcABitrate = ' -b:a ' + avcABitrate; }
	else avcABitrate = '';
	avcArgs += avcABitrate;
	
	//glue parameters to url
	avcArgs = avcArgs.replace('null','');
	avcUrl += encodeURIComponent(avcArgs);
	avcUrl += "&type=video/avi"; //don't important but otherwise proxy will return MIME-type of XBM that may confuse some players/browsers

	return avcUrl;
}

function SetAvcControls(){
	//show or hide A/V converter control box
	if(document.getElementById('avcUseProxy') == null) return;
	var display = option['avcUseProxy'] == 'On' ? 'block' : 'none';
	document.getElementById('avcServerControls').style.display = display;
	document.getElementById('avcControls').style.display = display;
}

function createMyElement(type, properties, event, listener) {
  var obj = page.doc.createElement(type);
  for (var propertykey in properties) {
    if (propertykey == 'target') obj.setAttribute('target', properties[propertykey]);
    else obj[propertykey] = properties[propertykey];
  }
  if (event && listener) {
    obj.addEventListener(event, listener, false);
  }
  return obj;
}

function modifyMyElement(obj, properties, event, listener) {
  for (var propertykey in properties) {
    if (propertykey == 'target') obj.setAttribute('target', properties[propertykey]);
    else obj[propertykey] = properties[propertykey];
  }
  if (event && listener) {
    obj.addEventListener(event, listener, false);
  }
}

function styleMyElement(obj, styles) {
  for (var stylekey in styles) {
    obj.style[stylekey] = styles[stylekey];
  }
}

function cleanMyElement(obj, hide) {
  if (hide) {
    for (var i = 0; i < obj.children.length; i++) {
      styleMyElement(obj.children[i], {display: 'none'});
    }
  }
  else {
    if (obj.hasChildNodes()) {
      while (obj.childNodes.length >= 1) {
        obj.removeChild(obj.firstChild);
      }
    }
  }
}

function getMyElement(obj, type, from, value, child, content) {
  var getObj, chObj, coObj;
  var pObj = (!obj) ? page.doc : obj;
  if (type == 'body') getObj = pObj.body;
  else {
    if (from == 'id') getObj = pObj.getElementById(value);
    else if (from == 'class') getObj = pObj.getElementsByClassName(value);
    else if (from == 'tag') getObj = pObj.getElementsByTagName(type);
    else if (from == 'ns') {
      if (pObj.getElementsByTagNameNS) getObj = pObj.getElementsByTagNameNS(value, type);
    }
    else if (from == 'query') {
      if (child > 0) {
	if (pObj.querySelectorAll) getObj = pObj.querySelectorAll(value);
      }
      else {
	if (pObj.querySelector)	getObj = pObj.querySelector(value);
      }
    }
  }
  chObj = (getObj && child >= 0) ? getObj[child] : getObj;
  if (content && chObj) {
    if (type == 'html' || type == 'body' || type == 'div' || type == 'option') coObj = chObj.innerHTML;
    else if (type == 'object') coObj = chObj.data;
    else if (type == 'img' || type == 'video' || type == 'embed') coObj = chObj.src;
    else coObj = chObj.textContent;
    return coObj;
  }
  else {
    return chObj;
  }
}

function appendMyElement(parent, child) {
  parent.appendChild(child);
}

function removeMyElement(parent, child) {
  parent.removeChild(child);
}

function replaceMyElement(parent, orphan, child) {
  parent.replaceChild(orphan, child);
}

function cleanMyContent(content, unesc, extra) {
  if (unesc) content = unescape(content);
  content = content.replace(/\\u0025/g, '%');
  content = content.replace(/\\u0026/g, '&');
  content = content.replace(/\\u002F/g, '/');
  content = content.replace(/\\/g, '');
  content = content.replace(/\n/g, '');
  if (extra) {
    content = content.replace(/&quot;/g, '\'').replace(/&#34;/g, '\'').replace(/&#034;/g, '\'').replace(/"/g, '\'');
    content = content.replace(/&#39;/g, '\'').replace(/&#039;/g, '\'').replace(/'/g, '\'');
    content = content.replace(/&amp;/g, 'and').replace(/&/g, 'and');
    content = content.replace(/[\/\|]/g, '-');
    content = content.replace(/[#:\*\?]/g, '');
    content = content.replace(/^\s+|\s+$/, '').replace(/\.+$/g, '');
  }
  return content;
}

function getMyContent(url, pattern, clean) {
  var myPageContent, myVideosParse, myVideosContent;
  if (!sources[url]) {
    var xmlHTTP = new XMLHttpRequest();
    xmlHTTP.open('GET', url, false);
    xmlHTTP.send();
    sources[url] = (xmlHTTP.responseText) ? xmlHTTP.responseText : xmlHTTP.responseXML;
    //console.log('Request: ' + url + ' ' + pattern);
    //console.log(sources[url]);
  }
  if (pattern == 'TEXT') {
    myVideosContent = sources[url];
  }
  else {
    myPageContent = (sources[url]) ? sources[url] : '';
    if (clean) myPageContent = cleanMyContent(myPageContent, true);
    myVideosParse = myPageContent.match(pattern);
    myVideosContent = (myVideosParse) ? myVideosParse[1] : null;
  }
  return myVideosContent;
}

function createMyPlayer() {
  /* The Content */
  player['contentWidth'] = player['playerWidth'];
  player['contentHeight'] = player['playerHeight'] - myPlayerPanelHeight;
  player['playerContent'] = createMyElement('div');
  styleMyElement(player['playerContent'], {width: player['contentWidth'] + 'px', height: player['contentHeight'] + 'px', position: 'relative', color: '#AD0000', backgroundColor: '#000000', fontSize: '14px', fontWeight: 'bold', textAlign: 'center'});
  appendMyElement(player['playerWindow'], player['playerContent']);

  /* The Video Thumbnail */
  if (player['videoThumb']) {
    player['contentImage'] = createMyElement('img', {src: player['videoThumb'], title: '{Click to start video playback}'}, 'click', function() {
      if (player['showsOptions'] && option['embed'] != 'Protocol') {
	player['showsOptions'] = false;
      }
      playMyVideo(!player['isPlaying']);
    });
    styleMyElement(player['contentImage'], {maxWidth: '100%', maxHeight: '100%', position: 'absolute', top: '0px', left: '0px', right: '0px', bottom: '0px', margin: 'auto', border: '0px', cursor: 'pointer'});
    player['contentImage'].addEventListener('load', function() {
      if (page.site == 'youtube') {
	if (this.width < 300) {
	  player['videoThumb'] = this.src.replace('maxresdefault', 'mqdefault');
	  this.src = player['videoThumb'];
	}
      }
      if (this.width/this.height >= player['contentWidth']/player['contentHeight']) {
	this.style.width = '100%';
      }
      else {
	this.style.height = '100%';
      }
    }, false);
  }

  /* The Panel */
  player['playerPanel'] = createMyElement('div');
  styleMyElement(player['playerPanel'], {width: player['playerWidth'] + 'px', height: myPlayerPanelHeight + 'px', lineHeight: (myPlayerPanelHeight - 2) + 'px', backgroundColor: '#000000', textAlign: 'center'});
  appendMyElement(player['playerWindow'], player['playerPanel']);

  /* Panel Logo */
  player['panelLogo'] = createMyElement('div', {title: '{ViewTube: click to visit the script wesite}', textContent: userscript}, 'click', function() {
    page.win.location.href = website;
  });
  styleMyElement(player['panelLogo'], {display: 'inline-block', color: '#E24994', fontSize: '14px', fontWeight: 'bold', border: '1px solid #E24994', borderRadius: '2px', padding: '0px 4px', lineHeight: 'normal', verticalAlign: 'middle', marginRight: '10px', cursor: 'pointer'});
  appendMyElement(player['playerPanel'], player['panelLogo']);

  /* Panel Video Menu */
  player['videoMenu'] = createMyElement('select', {title: '{Videos: select the video format for playback}'}, 'change', function() {
    player['videoPlay'] = this.value;
    if (player['isGetting']) {
      cleanMyElement(player['buttonGetLink'], false);
      player['isGetting'] = false;
    }
    if (player['isPlaying']) playMyVideo(option['autoplay']);
  });
  styleMyElement(player['videoMenu'], {width: '270px', display: 'inline-block', fontSize: '14px', fontWeight: 'bold', padding: '0px 3px', overflow: 'hidden', border: '1px solid #777777', color: '#CCCCCC', backgroundColor: '#000000', lineHeight: 'normal', verticalAlign: 'middle', cursor: 'pointer'});
  appendMyElement(player['playerPanel'], player['videoMenu']);
  var videosProgressive = [];
  var videosAdaptiveVideo = [];
  var videosAdaptiveAudio = [];
  var videosAdaptiveMuxed = [];
  for (var videoCode in player['videoList']) {
    if (videoCode.indexOf('Video') != -1) {
      if (videoCode.indexOf('Direct') == -1) videosAdaptiveVideo.push(videoCode);
    }
    else if (videoCode.indexOf('Audio') != -1) videosAdaptiveAudio.push(videoCode);
    else {
      if (player['videoList'][videoCode] == 'DASH') videosAdaptiveMuxed.push(videoCode);
      else videosProgressive.push(videoCode);
    }
  }
  if (videosProgressive.length > 0) {
    for (var i = 0; i < videosProgressive.length; i++) {
      player['videoItem'] = createMyElement('option', {value: videosProgressive[i], textContent: videosProgressive[i]});
      styleMyElement(player['videoItem'], {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
      appendMyElement(player['videoMenu'], player['videoItem']);
    }
  }
  if (videosAdaptiveVideo.length > 0) {
    player['videoItem'] = createMyElement('option', {value: 'DASH (Video Only)', textContent: 'DASH (Video Only)'});
    styleMyElement(player['videoItem'], {fontSize: '14px', fontWeight: 'bold', color: '#FF0000'});
    player['videoItem'].disabled = 'disabled';
    appendMyElement(player['videoMenu'], player['videoItem']);
    for (var i = 0; i < videosAdaptiveVideo.length; i++) {
      player['videoItem'] = createMyElement('option', {value: videosAdaptiveVideo[i], textContent: videosAdaptiveVideo[i]});
      styleMyElement(player['videoItem'], {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
      appendMyElement(player['videoMenu'], player['videoItem']);
    }
  }
  if (videosAdaptiveAudio.length > 0) {
    player['videoItem'] = createMyElement('option', {value: 'DASH (Audio Only)', textContent: 'DASH (Audio Only)'});
    styleMyElement(player['videoItem'], {fontSize: '14px', fontWeight: 'bold', color: '#FF0000'});
    player['videoItem'].disabled = 'disabled';
    appendMyElement(player['videoMenu'], player['videoItem']);
    for (var i = 0; i < videosAdaptiveAudio.length; i++) {
      player['videoItem'] = createMyElement('option', {value: videosAdaptiveAudio[i], textContent: videosAdaptiveAudio[i]});
      styleMyElement(player['videoItem'], {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
      appendMyElement(player['videoMenu'], player['videoItem']);
    }
  }
  if (videosAdaptiveMuxed.length > 0) {
    player['videoItem'] = createMyElement('option', {value: 'DASH (Video With Audio)', textContent: 'DASH (Video With Audio)'});
    styleMyElement(player['videoItem'], {fontSize: '14px', fontWeight: 'bold', color: '#FF0000'});
    player['videoItem'].disabled = 'disabled';
    appendMyElement(player['videoMenu'], player['videoItem']);
    for (var i = 0; i < videosAdaptiveMuxed.length; i++) {
      player['videoItem'] = createMyElement('option', {value: videosAdaptiveMuxed[i], textContent: videosAdaptiveMuxed[i]});
      styleMyElement(player['videoItem'], {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
      appendMyElement(player['videoMenu'], player['videoItem']);
    }
  }
  if (feature['direct']) {
    player['videoItem'] = createMyElement('option', {value: 'DVL (Open Video Link)', textContent: 'DVL (Open Video Link)'});
    styleMyElement(player['videoItem'], {fontSize: '14px', fontWeight: 'bold', color: '#FF0000'});
    player['videoItem'].disabled = 'disabled';
    appendMyElement(player['videoMenu'], player['videoItem']);
    player['videoItem'] = createMyElement('option', {value: 'Direct Video Link', textContent: 'Direct Video Link'});
    styleMyElement(player['videoItem'], {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
    appendMyElement(player['videoMenu'], player['videoItem']);
  }

  /* Panel Options Button */
  player['buttonOptions'] = createMyElement('div', {title: '{Options: click to show the available options}'}, 'click', function() {
    if (player['showsOptions']) {
      player['showsOptions'] = false;
      playMyVideo(option['autoplay']);
    }
    else {
      player['showsOptions'] = true;
      playMyVideo(false);
      createMyOptions();
    }
  });
  styleMyElement(player['buttonOptions'], {width: '1px', height: '14px', display: 'inline-block', paddingTop: '3px', borderLeft: '3px dotted #CCCCCC', lineHeight: 'normal', verticalAlign: 'middle', marginLeft: '20px', cursor: 'pointer'});
  appendMyElement(player['playerPanel'], player['buttonOptions']);

  /* Panel Play Button */
  player['buttonPlay'] = createMyElement('div', {title: '{Play/Stop: click to start/stop video playback}'}, 'click', function() {
    if (player['showsOptions'] && option['embed'] != 'Protocol') {
      player['showsOptions'] = false;
    }
    playMyVideo(!player['isPlaying']);
  });
  styleMyElement(player['buttonPlay'], {width: '0px', height: '0px', display: 'inline-block', borderTop: '8px solid transparent', borderBottom: '8px solid transparent', borderLeft: '15px solid #CCCCCC', lineHeight: 'normal', verticalAlign: 'middle', marginLeft: '20px', cursor: 'pointer'});
  appendMyElement(player['playerPanel'], player['buttonPlay']);

  /* Panel Get Button */
  player['buttonGet'] = createMyElement('div', {title: '{Get: click to download the selected video format}'}, 'click', function() {
    getMyVideo();
  });
  styleMyElement(player['buttonGet'], {width: '0px', height: '0px', display: 'inline-block', borderLeft: '8px solid transparent', borderRight: '8px solid transparent', borderTop: '15px solid #CCCCCC', lineHeight: 'normal', verticalAlign: 'middle', marginLeft: '20px', cursor: 'pointer'});
  appendMyElement(player['playerPanel'], player['buttonGet']);

  /* Panel Get Button Link */
  player['buttonGetLink'] = createMyElement('div', {title: '{Get: right click & save as to download the selected video format}'});
  styleMyElement(player['buttonGetLink'], {display: 'inline-block', color: '#CCCCCC', fontSize: '14px', fontWeight: 'bold', lineHeight: 'normal', verticalAlign: 'middle', marginLeft: '5px'});
  appendMyElement(player['playerPanel'], player['buttonGetLink']);

  /* Panel Widesize Button */
  if (feature['widesize']) {
    player['buttonWidesize'] = createMyElement('div', {title: '{Widesize: click to enter player widesize or return to normal size}'}, 'click', function() {
      option['widesize'] = (option['widesize']) ? false : true;
      setMyOptions('widesize', option['widesize']);
      resizeMyPlayer('widesize');
    });
    styleMyElement(player['buttonWidesize'], {border: '2px solid #CCCCCC', display: 'inline-block', lineHeight: 'normal', verticalAlign: 'middle', marginLeft: '20px', cursor: 'pointer'});
    if (option['widesize']) styleMyElement(player['buttonWidesize'], {width: '16px', height: '8px'});
    else styleMyElement(player['buttonWidesize'], {width: '20px', height: '10px'});
    appendMyElement(player['playerPanel'], player['buttonWidesize']);
  }

  /* Panel Fullsize Button */
  if (feature['fullsize']) {
    player['buttonFullsize'] = createMyElement('div', {title: '{Fullsize: click to enter player fullsize or return to normal size}'}, 'click', function() {
      option['fullsize'] = (option['fullsize']) ? false : true;
      setMyOptions('fullsize', option['fullsize']);
      resizeMyPlayer('fullsize');
    });
    styleMyElement(player['buttonFullsize'], {width: '20px', height: '14px', display: 'inline-block', lineHeight: 'normal', verticalAlign: 'middle', marginLeft: '20px', cursor: 'pointer'});
    if (option['fullsize']) styleMyElement(player['buttonFullsize'], {border: '2px solid #CCCCCC'});
    else styleMyElement(player['buttonFullsize'], {border: '2px dashed #CCCCCC'});
    appendMyElement(player['playerPanel'], player['buttonFullsize']);
  }

  /* Resize My Player */
  if (option['widesize']) resizeMyPlayer('widesize');
  if (option['fullsize']) resizeMyPlayer('fullsize');

  /* Select My Video */
  if (feature['definition'] || feature['container']) {
    if (!option['definition'] || player['videoDefinitions'].indexOf(option['definition']) == -1) option['definition'] = player['videoPlay'].replace(/Definition.*/, 'Definition');
    if (!option['container'] || player['videoContainers'].indexOf(option['container']) == -1) option['container'] = player['videoPlay'].replace(/.*\s/, '');
    selectMyVideo();
  }

  /* Play My Video */
  playMyVideo(option['autoplay']);
}

function resizeMyPlayer(size) {
  if (size == 'widesize') {
    if (option['widesize']) {
      if (player['buttonWidesize']) styleMyElement(player['buttonWidesize'], {width: '16px', height: '8px'});
      var playerWidth = player['playerWideWidth'];
      var playerHeight= player['playerWideHeight'];
      var sidebarMargin = player['sidebarMarginWide'];
    }
    else {
      if (player['buttonWidesize']) styleMyElement(player['buttonWidesize'], {width: '20px', height: '10px'});
      var playerWidth = player['playerWidth'];
      var playerHeight= player['playerHeight'];
      var sidebarMargin = player['sidebarMarginNormal'];
    }
  }
  else if (size == 'fullsize') {
    if (option['fullsize']) {
      var playerPosition = 'fixed';
      var playerWidth = page.win.innerWidth || page.doc.documentElement.clientWidth;
      var playerHeight = page.win.innerHeight || page.doc.documentElement.clientHeight;
      var playerIndex = '9999999999';
      if (!player['isFullsize']) {
	if (feature['widesize']) styleMyElement(player['buttonWidesize'], {display: 'none'});
	styleMyElement(player['buttonFullsize'], {border: '2px solid #CCCCCC'});
	appendMyElement(page.body, player['playerWindow']);
	styleMyElement(page.body, {overflow: 'hidden'});
	styleMyElement(page.body.parentNode, {overflow: 'hidden'});
	if (!player['resizeListener']) player['resizeListener'] = function() {resizeMyPlayer('fullsize')};
	page.win.addEventListener('resize', player['resizeListener'], false);
	player['isFullsize'] = true;
	if (player['isPlaying']) {
	  if (player['contentVideo'] && player['contentVideo'].paused) player['contentVideo'].play();
	}
      }
    }
    else {
      var playerPosition = 'relative';
      var playerWidth = (option['widesize']) ? player['playerWideWidth'] : player['playerWidth'];
      var playerHeight = (option['widesize']) ? player['playerWideHeight'] : player['playerHeight'];
      var playerIndex = 'auto';
      if (feature['widesize']) styleMyElement(player['buttonWidesize'], {display: 'inline-block'});
      styleMyElement(player['buttonFullsize'], {border: '2px dashed #CCCCCC'});
      appendMyElement(player['playerSocket'], player['playerWindow']);
      styleMyElement(page.body, {overflow: 'auto'});
      styleMyElement(page.body.parentNode, {overflow: 'auto'});
      page.win.removeEventListener('resize', player['resizeListener'], false);
      player['isFullsize'] = false;
      if (player['isPlaying']) {
	if (player['contentVideo'] && player['contentVideo'].paused) player['contentVideo'].play();
      }
    }
  }

  /* Resize The Player */
  if (size == 'widesize') {
    if (player['sidebarWindow']) styleMyElement(player['sidebarWindow'], {marginTop: sidebarMargin + 'px'});
    styleMyElement(player['playerSocket'], {height: playerHeight + 'px'});
    styleMyElement(player['playerWindow'], {width: playerWidth + 'px', height: playerHeight + 'px'});
  }
  else styleMyElement(player['playerWindow'], {position: playerPosition, top: '0px', left: '0px', width: playerWidth + 'px', height: playerHeight + 'px', zIndex: playerIndex});

  /* Resize The Panel */
  styleMyElement(player['playerPanel'], {width: playerWidth + 'px'});

  /* Resize The Content */
  player['contentWidth'] = playerWidth;
  player['contentHeight'] = playerHeight - myPlayerPanelHeight;
  styleMyElement(player['playerContent'], {width: player['contentWidth'] + 'px', height: player['contentHeight'] + 'px'});
  if (player['isPlaying']) {
    player['contentVideo'].width = player['contentWidth'];
    player['contentVideo'].height = player['contentHeight'];
    styleMyElement(player['contentVideo'], {width: player['contentWidth'] + 'px', height: player['contentHeight'] + 'px'});
  }
}

function createMyOptions() {
  if (!player['optionsContent']) {
    /* Options Window */
    player['optionsContent'] = createMyElement('div');
    styleMyElement(player['optionsContent'], {width: '100%', height: '100%', position: 'relative', fontSize: '14px', fontWeight: 'bold', backgroundColor: 'rgba(0, 0, 0, 0.7)' , textAlign: 'center', overflowY: 'scroll'});

    /* Embed/Media */
    var entryOption = createMyElement('div');
    styleMyElement(entryOption, {display: 'block', padding: '20px 0px 20px 0px'});
    appendMyElement(player['optionsContent'], entryOption);

    /* Embed */
    var embedOption = createMyElement('div');
    styleMyElement(embedOption, {display: 'inline-block'});
    var embedOptionLabel = createMyElement('div', {textContent: 'Embed video with'});
    styleMyElement(embedOptionLabel, {display: 'inline-block', color: '#CCCCCC', marginRight: '10px'});
    var embedOptionMenu = createMyElement('select', '', 'change', function() {
      option['embed'] = this.value;
      setMyOptions('embed', option['embed']);
    });
    styleMyElement(embedOptionMenu, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
    appendMyElement(embedOption, embedOptionLabel);
    appendMyElement(embedOption, embedOptionMenu);
    appendMyElement(entryOption, embedOption);
    var embedOptionMenuItem;
    for (var i = 0; i < embedtypes.length; i++) {
      embedOptionMenuItem = createMyElement('option', {value: embedtypes[i], textContent: embedtypes[i]});
      styleMyElement(embedOptionMenuItem, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
      appendMyElement(embedOptionMenu, embedOptionMenuItem);
    }
    embedOptionMenu.value = option['embed'];

    /* Media */
    var mediaOption = createMyElement('div');
    styleMyElement(mediaOption, {display: 'inline-block'});
    var mediaOptionLabel = createMyElement('div', {textContent: 'and play as/with'});
    styleMyElement(mediaOptionLabel, {display: 'inline-block', color: '#CCCCCC', marginRight: '10px'});
    var mediaOptionMenu = createMyElement('select', '', 'change', function() {
      option['media'] = this.value;
      setMyOptions('media', option['media']);
    });
    styleMyElement(mediaOptionMenu, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
    appendMyElement(mediaOption, mediaOptionLabel);
    appendMyElement(mediaOption, mediaOptionMenu);
    appendMyElement(entryOption, mediaOption);
    var mediaOptionMenuItem;
    mediaOptionMenuItem = createMyElement('option', {value: 'Auto', textContent: 'Auto'});
    styleMyElement(mediaOptionMenuItem, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
    appendMyElement(mediaOptionMenu, mediaOptionMenuItem);
    for (var i = 0; i < mediakeys.length; i++) {
      mediaOptionMenuItem = createMyElement('option', {value: mediakeys[i], textContent: mediakeys[i]});
      styleMyElement(mediaOptionMenuItem, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
      appendMyElement(mediaOptionMenu, mediaOptionMenuItem);
    }
    mediaOptionMenu.value = option['media'];

    /* Definition/Container */
    entryOption = createMyElement('div');
    styleMyElement(entryOption, {display: 'block', padding: '20px 0px 20px 0px'});
    appendMyElement(player['optionsContent'], entryOption);

    /* Definition */
    var definitionOption = createMyElement('div');
    styleMyElement(definitionOption, {display: 'inline-block'});
    var definitionOptionLabel = createMyElement('div', {textContent: 'Select the definition'});
    styleMyElement(definitionOptionLabel, {display: 'inline-block', color: '#CCCCCC', marginRight: '10px'});
    var definitionOptionMenu = createMyElement('select', '', 'change', function() {
      option['definition'] = this.value;
      setMyOptions('definition', option['definition']);
      if (player['isGetting']) {
	cleanMyElement(player['buttonGetLink'], false);
	player['isGetting'] = false;
      }
      selectMyVideo();
    });
    styleMyElement(definitionOptionMenu, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
    appendMyElement(definitionOption, definitionOptionLabel);
    appendMyElement(definitionOption, definitionOptionMenu);
    appendMyElement(entryOption, definitionOption);
    var definitionOptionMenuItem;
    for (var i = 0; i < player['videoDefinitions'].length; i++) {
      definitionOptionMenuItem = createMyElement('option', {value: player['videoDefinitions'][i], textContent: player['videoDefinitions'][i]});
      styleMyElement(definitionOptionMenuItem, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
      appendMyElement(definitionOptionMenu, definitionOptionMenuItem);
    }
    definitionOptionMenu.value = option['definition'];

    /* Container */
    if (feature['container']) {
      var containerOption = createMyElement('div');
      styleMyElement(containerOption, {display: 'inline-block'});
      var containerOptionLabel = createMyElement('div', {textContent: 'and the container'});
      styleMyElement(containerOptionLabel, {display: 'inline-block', color: '#CCCCCC', marginRight: '10px'});
      var containerOptionMenu = createMyElement('select', '', 'change', function() {
	option['container'] = this.value;
	setMyOptions('container', option['container']);
	if (player['isGetting']) {
	  cleanMyElement(player['buttonGetLink'], false);
	  player['isGetting'] = false;
	}
	selectMyVideo();
      });
      styleMyElement(containerOptionMenu, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
      appendMyElement(containerOption, containerOptionLabel);
      appendMyElement(containerOption, containerOptionMenu);
      appendMyElement(entryOption, containerOption);
      var containerOptionMenuItem;
      for (var i = 0; i < player['videoContainers'].length; i++) {
	containerOptionMenuItem = createMyElement('option', {value: player['videoContainers'][i], textContent: player['videoContainers'][i]});
	styleMyElement(containerOptionMenuItem, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
	appendMyElement(containerOptionMenu, containerOptionMenuItem);
      }
      containerOptionMenu.value = option['container'];
    }

    /* Autoplay */
    var autoplayOption = createMyElement('div');
    styleMyElement(autoplayOption, {display: 'block', padding: '20px 0px 20px 0px'});
    var autoplayOptionLabel = createMyElement('div', {textContent: 'Autoplay'});
    styleMyElement(autoplayOptionLabel, {display: 'inline-block', color: '#CCCCCC', marginRight: '10px'});
    var autoplayOptionMenu = createMyElement('select', '', 'change', function() {
      option['autoplay'] = (this.value == 'On') ? true : false;
      setMyOptions('autoplay', option['autoplay']);
    });
    styleMyElement(autoplayOptionMenu, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
    appendMyElement(autoplayOption, autoplayOptionLabel);
    appendMyElement(autoplayOption, autoplayOptionMenu);
    appendMyElement(player['optionsContent'], autoplayOption);
    var autoplayOptionMenuItem;
    for (var i = 0; i < ['On', 'Off'].length; i++) {
      autoplayOptionMenuItem = createMyElement('option', {value: ['On', 'Off'][i], textContent: ['On', 'Off'][i]});
      styleMyElement(autoplayOptionMenuItem, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
      appendMyElement(autoplayOptionMenu, autoplayOptionMenuItem);
    }
    if (option['autoplay']) autoplayOptionMenu.value = 'On';
    else autoplayOptionMenu.value = 'Off';

    /* DASH */
    if (feature['dash']) {
      var dashOption = createMyElement('div');
      styleMyElement(dashOption, {display: 'block', padding: '20px 0px 20px 0px'});
      var dashOptionLabel = createMyElement('div', {textContent: 'DASH (Video With Audio) playback support'});
      styleMyElement(dashOptionLabel, {display: 'inline-block', color: '#CCCCCC', marginRight: '10px'});
      var dashOptionMenu = createMyElement('select', '', 'change', function() {
	option['dash'] = (this.value == 'On') ? true : false;
	setMyOptions('dash', option['dash']);
      });
      styleMyElement(dashOptionMenu, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
      appendMyElement(dashOption, dashOptionLabel);
      appendMyElement(dashOption, dashOptionMenu);
      appendMyElement(player['optionsContent'], dashOption);
      var dashOptionMenuItem;
      for (var i = 0; i < ['On', 'Off'].length; i++) {
	dashOptionMenuItem = createMyElement('option', {value: ['On', 'Off'][i], textContent: ['On', 'Off'][i]});
	styleMyElement(dashOptionMenuItem, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
	appendMyElement(dashOptionMenu, dashOptionMenuItem);
      }
      if (option['dash']) dashOptionMenu.value = 'On';
      else dashOptionMenu.value = 'Off';
    }

    /* DVL */
    if (feature['direct']) {
      var directOption = createMyElement('div');
      styleMyElement(directOption, {display: 'block', padding: '20px 0px 20px 0px'});
      var directOptionLabel = createMyElement('div', {textContent: 'DVL (Pass the page video link to the player)'});
      styleMyElement(directOptionLabel, {display: 'inline-block', color: '#CCCCCC', marginRight: '10px'});
      var directOptionMenu = createMyElement('select', '', 'change', function() {
	option['direct'] = (this.value == 'On') ? true : false;
	setMyOptions('direct', option['direct']);
	selectMyVideo();
      });
      styleMyElement(directOptionMenu, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
      appendMyElement(directOption, directOptionLabel);
      appendMyElement(directOption, directOptionMenu);
      appendMyElement(player['optionsContent'], directOption);
      var directOptionMenuItem;
      for (var i = 0; i < ['On', 'Off'].length; i++) {
	directOptionMenuItem = createMyElement('option', {value: ['On', 'Off'][i], textContent: ['On', 'Off'][i]});
	styleMyElement(directOptionMenuItem, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
	appendMyElement(directOptionMenu, directOptionMenuItem);
      }
      if (option['direct']) directOptionMenu.value = 'On';
      else directOptionMenu.value = 'Off';
    }

	/* WebOne box */
	var avcOption = createMyElement('div');
	styleMyElement(avcOption, {display: 'block', padding: '20px 0px 20px 0px'});
	appendMyElement(player['optionsContent'], avcOption);

	//label
	var avcOptionLabel = createMyElement('div', {textContent: 'Use proxy for codec converting'});
	styleMyElement(avcOptionLabel, {display: 'inline-block', color: '#CCCCCC', marginRight: '20px'});
	appendMyElement(avcOption, avcOptionLabel);

	//on/off
	var avcUseProxy = createMyElement('select', '', 'change', function() {
		option['avcUseProxy'] = this.value;
		setMyOptions('avcUseProxy', option['avcUseProxy']);
		SetAvcControls();
	});
	avcUseProxy.id = 'avcUseProxy';
	styleMyElement(avcUseProxy, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
	appendMyElement(avcOption, avcUseProxy);

	for (var i = 0; i < ['On', 'Off'].length; i++) {
		Item = createMyElement('option', {value: ['On', 'Off'][i], textContent: ['On', 'Off'][i]});
		styleMyElement(Item, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
		appendMyElement(avcUseProxy, Item);
      	}
	avcUseProxy.value = option['avcUseProxy'];
	var displayAvcBox = option['avcUseProxy'] == 'On' ? 'block' : 'none';

	/* WebOne server controls */
	var avcServerControls = createMyElement('div', {textContent: 'WebOne proxy server: '});
	avcServerControls.id = 'avcServerControls';
	styleMyElement(avcServerControls, {display: displayAvcBox, color: '#CCCCCC', marginRight: '20px', padding: '2px 0px 2px 0px'});
	appendMyElement(avcOption, avcServerControls);

	//proxy hostname prefix
	var avcHostnamePrefix = createMyElement('div', {textContent: 'http://'});
	avcHostnamePrefix.id = 'avcHostnamePrefix';
	styleMyElement(avcHostnamePrefix, {display: 'inline-block', height: '16px', padding: '1px', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', borderRight: 'none', fontSize: '14px', fontWeight: 'bold', marginRight: '0px'});
	appendMyElement(avcServerControls, avcHostnamePrefix);

	//proxy hostname
	var avcWebOneHost = createMyElement('input', '', 'blur', function() {
		option['avcWebOneHost'] = this.value;
		setMyOptions('avcWebOneHost', option['avcWebOneHost']);
	});
	avcWebOneHost.type = 'text';
	avcWebOneHost.title = 'Proxy server name';
	avcWebOneHost.id = 'avcWebOneHost';
	avcWebOneHost.value = option['avcWebOneHost'];
	avcWebOneHost.size = 10;
 	styleMyElement(avcWebOneHost, {display: 'inline-block', height: '16px', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', borderLeft: 'none', fontSize: '14px', fontWeight: 'bold', marginRight: '10px', cursor: 'text'});
	appendMyElement(avcServerControls, avcWebOneHost);

	//format
	var avcContainer = createMyElement('select', '', 'change', function() {
		option['avcContainer'] = this.value;
		setMyOptions('avcContainer', option['avcContainer']);
	});
	avcContainer.title = 'Container';
	avcContainer.id = 'avcContainer';
	styleMyElement(avcContainer, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
	appendMyElement(avcServerControls, avcContainer);

	for (var container in proxyobject.containers) {
		avcContainerItem = createMyElement('option', {value: proxyobject.containers[container], textContent: container});
		styleMyElement(avcContainerItem, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
		appendMyElement(avcContainer, avcContainerItem);
	}
	avcContainer.value = option['avcContainer'];

	/* WebOne codec controls */
	var avcControls = createMyElement('div', {textContent: 'Video: '});
	avcControls.id = 'avcControls';
	styleMyElement(avcControls, {display: displayAvcBox, color: '#CCCCCC', marginRight: '10px', padding: '2px 0px 2px 0px'});
	appendMyElement(avcOption, avcControls);

	//video codec
	var avcVCodec = createMyElement('select', '', 'change', function() {
		option['avcVCodec'] = this.value;
		setMyOptions('avcVCodec', option['avcVCodec']);
	});
	avcVCodec.title = 'Video codec';
	avcVCodec.id = 'avcVCodec';
	styleMyElement(avcVCodec, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
	appendMyElement(avcControls, avcVCodec);

	for (var str in proxyobject.vcodecs) {
		avcVCodecItem = createMyElement('option', {value: proxyobject.vcodecs[str], textContent: str});
		styleMyElement(avcVCodecItem, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
		appendMyElement(avcVCodec, avcVCodecItem);
	}
	avcVCodec.value = option['avcVCodec'];

	//video size
	var avcVSize = createMyElement('select', '', 'change', function() {
		option['avcVSize'] = this.value;
		setMyOptions('avcVSize', option['avcVSize']);
	});
	avcVSize.title = 'Video size';
	avcVSize.id = 'avcVSize';
	styleMyElement(avcVSize, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
	appendMyElement(avcControls, avcVSize);

	for (var str in proxyobject.vsize) {
		Item = createMyElement('option', {value: proxyobject.vsize[str], textContent: str});
		styleMyElement(Item, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
		appendMyElement(avcVSize, Item);
	}
	avcVSize.value = option['avcVSize'];

	//video bitrate
	var avcVBitrate = createMyElement('input', '', 'blur', function() {
		option['avcVBitrate'] = this.value;
		setMyOptions('avcVBitrate', option['avcVBitrate']);
	});
	avcVBitrate.type = 'text';
	avcVBitrate.title = 'Video bitrate';
	avcVBitrate.id = 'avcVBitrate';
	avcVBitrate.value = option['avcVBitrate'];
	avcVBitrate.size = 2;
 	styleMyElement(avcVBitrate, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px', cursor: 'text'});
	appendMyElement(avcControls, avcVBitrate);

	//separator label
	var avcControlsSep = createMyElement('div', {textContent: 'Audio: '});
	avcControlsSep.id = 'avcControlsSep';
	styleMyElement(avcControlsSep, {display: 'inline-block', color: '#CCCCCC', marginRight: '10px', marginLeft: '10px'});
	appendMyElement(avcControls, avcControlsSep);

	//audio codec
	var avcACodec = createMyElement('select', '', 'change', function() {
		option['avcACodec'] = this.value;
		setMyOptions('avcACodec', option['avcACodec']);
	});
	avcACodec.title = 'Audio codec';
	avcACodec.id = 'avcACodec';
	styleMyElement(avcACodec, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
	appendMyElement(avcControls, avcACodec);

	for (var str in proxyobject.acodecs) {
		Item = createMyElement('option', {value: proxyobject.acodecs[str], textContent: str});
		styleMyElement(Item, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
		appendMyElement(avcACodec, Item);
	}
	avcACodec.value = option['avcACodec'];

	//audio channels
	var avcAChannels = createMyElement('select', '', 'change', function() {
		option['avcAChannels'] = this.value;
		setMyOptions('avcAChannels', option['avcAChannels']);
	});
	avcAChannels.title = 'Audio channels';
	avcAChannels.id = 'avcAChannels';
	styleMyElement(avcAChannels, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px'});
	appendMyElement(avcControls, avcAChannels);

	for (var str in proxyobject.achannels) {
		Item = createMyElement('option', {value: proxyobject.achannels[str], textContent: str});
		styleMyElement(Item, {fontSize: '14px', fontWeight: 'bold', cursor: 'pointer'});
		appendMyElement(avcAChannels, Item);
	}
	avcAChannels.value = option['avcAChannels'];

	//audio bitrate
	var avcABitrate = createMyElement('input', '', 'blur', function() {
		option['avcABitrate'] = this.value;
		setMyOptions('avcABitrate', option['avcABitrate']);
	});
	avcABitrate.type = 'text';
	avcABitrate.title = 'Audio bitrate';
	avcABitrate.id = 'avcABitrate';
	avcABitrate.value = option['avcABitrate'];
	avcABitrate.size = 2;
 	styleMyElement(avcABitrate, {display: 'inline-block', color: '#CCCCCC', backgroundColor: '#000000', border: '1px solid #777777', fontSize: '14px', fontWeight: 'bold', marginRight: '10px', cursor: 'text'});
	appendMyElement(avcControls, avcABitrate);

  }
  appendMyElement(player['playerContent'], player['optionsContent']);
}

function setMyOptions(key, value) {
  key = page.site + '_' + userscript.toLowerCase() + '_' + key;
  try {
    localStorage.setItem(key, value);
    if (localStorage.getItem(key) == value) return;
    else throw false;
  }
  catch(e) {
    var date = new Date();
    date.setTime(date.getTime() + (356*24*60*60*1000));
    var expires = '; expires=' + date.toGMTString();
    page.doc.cookie = key + '=' + value + expires + '; path=/';
  }
}

function getMyOptions() {
  for (var opt in option) {
    var key = page.site + '_' + userscript.toLowerCase() + '_' + opt;
    try {
      if (localStorage.getItem(key)) {
	option[opt] = localStorage.getItem(key);
	continue;
      }
      else throw false;
    }
    catch(e) {
      var cookies = page.doc.cookie.split(';');
      for (var i=0; i < cookies.length; i++) {
	var cookie = cookies[i];
	while (cookie.charAt(0) == ' ') cookie = cookie.substring(1, cookie.length);
	option[opt] = (cookie.indexOf(key) == 0) ? cookie.substring(key.length + 1, cookie.length) : option[opt];
      }
    }
  }
  if (!option['embed'] || embedtypes.indexOf(option['embed']) == -1) option['embed'] = 'Video';
  if (!option['media'] || mediakeys.indexOf(option['media']) == -1) option['media'] = 'Auto';
  var boolOptions = ['autoplay', 'dash', 'direct', 'widesize', 'fullsize'];
  for (var i = 0; i < boolOptions.length; i++) {
    option[boolOptions[i]] = (option[boolOptions[i]] === true || option[boolOptions[i]] == 'true') ? true : false;
  }
}

function selectMyVideo() {
  var vdoCont = (option['container'] != 'Any') ? [option['container']] : player['videoContainers'];
  var vdoDef = player['videoDefinitions'];
  var vdoList = {};
  for (var vC = 0; vC < vdoCont.length; vC++) {
    if (vdoCont[vC] != 'Any') {
      for (var vD = 0; vD < vdoDef.length; vD++) {
	var format = vdoDef[vD] + ' ' + vdoCont[vC];
	if (!vdoList[vdoDef[vD]]) {
	  for (var vL in player['videoList']) {
	    if (vL == format) {
	      vdoList[vdoDef[vD]] = vL;
	      break;
	    }
	  }
	}
      }
    }
  }
  var vdoDef2 = [];
  var keepDef = false;
  for (var vD = 0; vD < vdoDef.length; vD++) {
    if (vdoDef[vD] == option['definition'] && keepDef == false) keepDef = true;
    if (keepDef == true) vdoDef2.push(vdoDef[vD])
  }
  for (var vD = 0; vD < vdoDef2.length; vD++) {
    if (vdoList[vdoDef2[vD]]) {
      player['videoPlay'] = vdoList[vdoDef2[vD]];
      break;
    }
  }
  if (option['direct']) player['videoPlay'] = 'Direct Video Link';
  player['videoMenu'].value = player['videoPlay'];
}

function playDASHwithVLC() {
  var contentVideo = player['videoList'][player['videoPlay'].replace('Definition', 'Definition Video')];
  var contentAudio = player['videoList']['High Bitrate Audio WebM'] || player['videoList']['Medium Bitrate Audio WebM']
		    || player['videoList']['Medium Bitrate Audio MP4'] || player['videoList'][player['videoPlay'].replace('Definition', 'Definition Audio')];
  if (option['media'] == 'VLC*') {
    player['contentVideo'] = createMyElement('embed', {id: 'vtVideo', type: mediatypes[option['media']], target: contentVideo, innerHTML: embedcontent['Embed']});
    player['contentAudio'] = createMyElement('embed', {id: 'vtVideo', type: mediatypes[option['media']], target: contentAudio});
  }
  else {
    player['contentVideo'] = createMyElement('embed', {id: 'vtVideo', type: mediatypes[option['media']], src: contentVideo, innerHTML: embedcontent['Embed']});
    player['contentAudio'] = createMyElement('embed', {id: 'vtVideo', type: mediatypes[option['media']], src: contentAudio});
  }
  styleMyElement(player['contentAudio'], {position: 'absolute', zIndex: '-1', width: '1px', height: '1px'});
  appendMyElement(player['playerContent'], player['contentAudio']);
  player['contentVLCInit'] = page.win.setInterval(function() {
    if (player['contentAudio'].wrappedJSObject.playlist && player['contentVideo'].wrappedJSObject.playlist
      && player['contentAudio'].wrappedJSObject.input && player['contentVideo'].wrappedJSObject.input) {
      player['contentVLCVideoPosition'] = 0;
      player['contentVLCSync'] = page.win.setInterval(function() {
	if (!player['contentVideo'] || !player['contentVideo'].wrappedJSObject || !player['contentVideo'].wrappedJSObject.input) {
	  page.win.clearInterval(player['contentVLCSync']);
	}
	if (player['contentVideo'].wrappedJSObject.input.time != player['contentVLCVideoPosition']) {
	  if (Math.abs(player['contentVideo'].wrappedJSObject.input.time - player['contentAudio'].wrappedJSObject.input.time) >= 500) {
	    player['contentAudio'].wrappedJSObject.input.time = player['contentVideo'].wrappedJSObject.input.time;
	  }
	  player['contentVLCVideoPosition'] = player['contentVideo'].wrappedJSObject.input.time;
	}
	if (player['contentVideo'].wrappedJSObject.input.state == '4') {
	  player['contentAudio'].wrappedJSObject.playlist.pause();
	  player['contentAudioPaused'] = true;
	}
	if (player['contentVideo'].wrappedJSObject.input.state == '6') {
	  player['contentAudio'].wrappedJSObject.playlist.pause();
	  player['contentAudioPaused'] = true;
	}
	if (player['contentVideo'].wrappedJSObject.input.state == '3' && player['contentAudioPaused']) {
	  player['contentAudio'].wrappedJSObject.playlist.play();
	  player['contentAudioPaused'] = false;
	}
      }, 1000);
      page.win.clearInterval(player['contentVLCInit']);
    }
  }, 500);
}

function playDASHwithHTML5() {
  var contentVideo = player['videoList'][player['videoPlay'].replace('Definition', 'Definition Video')];
  var contentAudio = player['videoList']['High Bitrate Audio WebM'] || player['videoList']['Medium Bitrate Audio WebM']
		    || player['videoList']['Medium Bitrate Audio MP4'] || player['videoList'][player['videoPlay'].replace('Definition', 'Definition Audio')];
  player['contentVideo'] = createMyElement('video', {id: 'vtVideo', type: mediatypes[player['videoPlay'].replace(/.*\s/, '')], src: contentVideo, controls: 'controls', autoplay: 'autoplay', innerHTML: embedcontent['Video']});
  player['contentAudio'] = createMyElement('video', {id: 'vtVideo', type: mediatypes[player['videoPlay'].replace(/.*\s/, '')], src: contentAudio, autoplay: 'autoplay'});
  player['contentAudio'].pause();
  player['contentVideo'].addEventListener('play', function() {
    player['contentAudio'].play();
  }, false);
  player['contentVideo'].addEventListener('pause', function() {
    player['contentAudio'].pause();
  }, false);
  player['contentVideo'].addEventListener('ended', function() {
    player['contentVideo'].pause();
    player['contentAudio'].pause();
  }, false);
  player['contentVideo'].addEventListener('timeupdate', function() {
    if (player['contentAudio'].paused && !player['contentVideo'].paused) {
      player['contentAudio'].play();
    }
    if (Math.abs(player['contentVideo'].currentTime - player['contentAudio'].currentTime) >= 0.30) {
      player['contentAudio'].currentTime = player['contentVideo'].currentTime;
    }
  }, false);
  styleMyElement(player['contentAudio'], {display: 'none'});
  appendMyElement(player['contentVideo'], player['contentAudio']);
}

function playMyVideo(play) {
  if (play) {
    if (option['embed'] == 'Protocol') {
      if (player['videoList'][player['videoPlay']] != 'DASH') {
	if(option['avcUseProxy'] == 'On')
	  page.win.location.href = 'viewtube:' + GetAvcURL(player['videoList'][player['videoPlay']]);
	else
	  page.win.location.href = 'viewtube:' + player['videoList'][player['videoPlay']].replace('http://','https://');
      }
      else {
	var contentVideo = player['videoList'][player['videoPlay'].replace('Definition', 'Definition Video')];
	var contentAudio = player['videoList']['High Bitrate Audio WebM'] || player['videoList']['Medium Bitrate Audio WebM']
			  || player['videoList']['Medium Bitrate Audio MP4'] || player['videoList'][player['videoPlay'].replace('Definition', 'Definition Audio')];
	page.win.location.href = 'viewtube:' + contentVideo + 'SEPARATOR' + contentAudio;
      }
      return;
    }
    player['isPlaying'] = true;
    styleMyElement(player['buttonPlay'], {width: '15px', height: '15px', backgroundColor: '#CCCCCC', border: '0px'});
    cleanMyElement(player['playerContent'], false);
    if (player['videoList'][player['videoPlay']] == 'DASH') {
      if(option['avcUseProxy'] == 'On') alert('DASH is not currently supported via proxies.\nPlayback will be direct.');
      if (option['media'] == 'VLC' || option['media'] == 'VLC*') {
	playDASHwithVLC();
      }
      else {
	playDASHwithHTML5();
      }
    }
    else {
      var videoProperties, videoType, videoSource;
      videoSource = player['videoList'][player['videoPlay']].replace('http://','https://');
      if(option['avcUseProxy'] == 'On') videoSource = GetAvcURL(videoSource);

      if (option['media'] == 'Auto') {
	videoType = mediatypes[player['videoPlay'].replace(/.*\s/, '')];
      }
      else {
	videoType = mediatypes[option['media']];
      }
      if (option['embed'] == 'Video') {
	videoProperties = {id: 'vtVideo', type: videoType, src: videoSource, controls: 'controls', autoplay: 'autoplay', poster: player['videoThumb'], innerHTML: embedcontent[option['embed']]};
      }
      else if (option['embed'] == 'Object') {
	videoProperties = {id: 'vtVideo', type: videoType, data: videoSource, innerHTML: embedcontent[option['embed']]};
      }
      else if (option['embed'] == 'Embed') {
	if (option['media'] == 'VLC*') {
	  videoProperties = {id: 'vtVideo', type: videoType, target: videoSource, innerHTML: embedcontent[option['embed']]};
	}
	else {
	  videoProperties = {id: 'vtVideo', type: videoType, src: videoSource, innerHTML: embedcontent[option['embed']]};
	}
      }
      player['contentVideo'] = createMyElement(option['embed'], videoProperties);
    }
    player['contentVideo'].width = player['contentWidth'];
    player['contentVideo'].height = player['contentHeight'];
    styleMyElement(player['contentVideo'], {position: 'relative', width: player['contentWidth'] + 'px', height: player['contentHeight'] + 'px'});
    appendMyElement(player['playerContent'], player['contentVideo']);
  }
  else {
    player['isPlaying'] = false;
    styleMyElement(player['buttonPlay'], {width: '0px', height: '0px', borderTop: '8px solid transparent', borderBottom: '8px solid transparent', borderLeft: '15px solid #CCCCCC', backgroundColor: '#000000'});
    cleanMyElement(player['playerContent'], false);
    if (player['contentImage']) appendMyElement(player['playerContent'], player['contentImage']);
    else showMyMessage('!thumb');
  }
}

function getMyVideo() {
  var vdoURL = player['videoList'][player['videoPlay']].replace('http://','https://');
  if (vdoURL == 'DASH') return;
  if (vdoURL == page.url) return;
  if (option['avcUseProxy'] == 'On') vdoURL = GetAvcURL(vdoURL);
  var vdoDef = ' (' + player['videoPlay'].split(' ').slice(0, -1).join('').match(/[A-Z]/g).join('') + ')';
  var vdoExt = '.' + player['videoPlay'].split(' ').slice(-1).join('').toLowerCase();
  var vdoTle = (player['videoTitle']) ? player['videoTitle'] : '';
  if (option['autoget'] && vdoTle && player['videoPlay'] == 'High Definition MP4') {
    page.win.location.href = vdoURL + '&title=' + vdoTle + vdoDef;
  }
  var vdoLnk = createMyElement('a', {href: vdoURL, target: '_blank', textContent: '[Link]'});
  styleMyElement(vdoLnk, {color: '#CCCCCC', textDecoration: 'underline'});
  appendMyElement(player['buttonGetLink'], vdoLnk);
  player['isGetting'] = true;
}

function showMyMessage(cause, content) {
  var myScriptLogo = createMyElement('div', {textContent: userscript});
  styleMyElement(myScriptLogo, {display: 'inline-block', margin: '10px auto', color: '#E24994', fontSize: '24px', fontWeight: 'bold', textAlign: 'center', border: '1px solid #E24994', borderRadius: '2px', padding: '0px 4px'});
  var myScriptMess = createMyElement('div');
  styleMyElement(myScriptMess, {fontSize: '20px', border: '1px solid #777777', margin: '5px auto 5px auto', padding: '10px', backgroundColor: '#000000', color: '#AD0000', textAlign: 'center'});
  if (cause == '!player') {
    var myScriptAlert = createMyElement('div');
    styleMyElement(myScriptAlert, {position: 'absolute', top: '30%', left: '35%', border: '1px solid #F4F4F4', borderRadius: '3px', padding: '10px', backgroundColor: '#FFFFFF', fontSize: '14px', textAlign: 'center', zIndex: '99999'});
    appendMyElement(myScriptAlert, myScriptLogo);
    modifyMyElement(myScriptMess, {innerHTML: 'Couldn\'t get the player element. Please report it <a href="' + contact + '" style="color:#00892C">here</a>.'});
    styleMyElement(myScriptMess, {border: '1px solid #EEEEEE', backgroundColor: '#FFFFFF'});
    appendMyElement(myScriptAlert, myScriptMess);
    var myScriptAlertButton = createMyElement('div', {textContent: 'OK'}, 'click', function() {
      removeMyElement(page.body, myScriptAlert);
    });
    styleMyElement(myScriptAlertButton, {width: '100px', border: '3px solid #EEEEEE', borderRadius: '5px', margin: '0px auto', backgroundColor: '#EEEEEE', color: '#666666', fontSize: '18px', textAlign: 'center', cursor: 'pointer'});
    appendMyElement(myScriptAlert, myScriptAlertButton);
    appendMyElement(page.body, myScriptAlert);
  }
  else if (cause == '!thumb') {
    modifyMyElement(player['playerContent'], {innerHTML: '<br><br>Couldn\'t get the thumbnail for this video. Please report it <a href="' + contact + '" style="color:#00892C">here</a>.'});
  }
  else {
    appendMyElement(myPlayerWindow, myScriptLogo);
    if (cause == '!content') {
      modifyMyElement(myScriptMess, {innerHTML: 'Couldn\'t get the videos content. Please report it <a href="' + contact + '" style="color:#00892C">here</a>.'});
    }
    else if (cause == '!videos') {
      modifyMyElement(myScriptMess, {innerHTML: 'Couldn\'t get any video. Please report it <a href="' + contact + '" style="color:#00892C">here</a>.'});
    }
    else if (cause == '!support') {
      modifyMyElement(myScriptMess, {textContent: 'This video uses the RTMP protocol and is not supported.'});
    }
    else if (cause == 'embed') {
      modifyMyElement(myScriptMess, {innerHTML: 'This is an embedded video. You can watch it <a href="' + content + '" style="color:#00892C">here</a>.'});
    }
    else if (cause == 'other') {
      modifyMyElement(myScriptMess, {innerHTML: content});
    }
    appendMyElement(myPlayerWindow, myScriptMess);
  }
}


// ==========Blocker========== //

var blockObject = page.doc;
var blockInterval = 50;

function blockVideos() {
  var elVideos = getMyElement(blockObject, 'video', 'tag', '', -1, false);
  if (elVideos.length > 0) {
    for (var v = 0; v < elVideos.length; v++) {
      var elVideo = elVideos[v];
      if (elVideo && elVideo.id != 'vtVideo' && elVideo.currentSrc) {
	if (!elVideo.paused) {
	  elVideo.pause();
	  if (page.url.indexOf('youtube.com/watch') == -1) elVideo.src = "#";
	  elVideo.addEventListener('play', function() {
	    this.pause();
	    if (page.url.indexOf('youtube.com/watch') == -1) this.src = "#";
	  });
	}
      }
    }
  }
  var elEmbeds = getMyElement(blockObject, 'embed', 'tag', '', -1, false) || getMyElement(blockObject, 'object', 'tag', '', -1, false);
  if (elEmbeds.length > 0) {
    for (var e = 0; e < elEmbeds.length; e++) {
      var elEmbed = elEmbeds[e];
      if (elEmbed && elEmbed.id != 'vtVideo' && elEmbed.parentNode) {
	removeMyElement(elEmbed.parentNode, elEmbed);
      }
    }
  }
  if (blockObject !== page.doc) {
    var elFrames = getMyElement(blockObject, 'iframe', 'tag', '', -1, false);
    if (elFrames.length > 0) {
      for (var e = 0; e < elFrames.length; e++) {
	var elFrame = elFrames[e];
	if (elFrame && elFrame.parentNode) {
	  removeMyElement(elFrame.parentNode, elFrame);
	}
      }
    }
  }
}

blockVideos();


// ==========Websites========== //

function ViewTube() {

  // =====YouTube===== //

  if (page.url.indexOf('youtube.com/watch') != -1 && (getMyContent(page.url, 'kevlar_(flexy)', false) || getMyContent(page.url, 'watch-(flexy)', false))) {

    /* Video Availability */
    if (getMyContent(page.url, '"playabilityStatus":\\{"status":"(ERROR|UNPLAYABLE)"', false)) return;

    /* Decrypt Signature */
    var ytScriptSrc;
    function ytDecryptSignature(s) {return null;}
    function ytDecryptFunction() {
      var ytSignFuncName, ytSignFuncBody, ytSwapFuncName, ytSwapFuncBody, ytFuncMatch;
      ytScriptSrc = ytScriptSrc.replace(/(\r\n|\n|\r)/gm, '');
      ytSignFuncName = ytScriptSrc.match(/"signature"\s*,\s*([^\)]*?)\(/);
      if (!ytSignFuncName) ytSignFuncName = ytScriptSrc.match(/c&&.\.set\(b,(?:encodeURIComponent\()?.*?([a-zA-Z0-9$]+)\(/);
      ytSignFuncName = (ytSignFuncName) ? ytSignFuncName[1] : null;
      if (ytSignFuncName) {
	ytFuncMatch = ytSignFuncName.replace(/\$/, '\\$') + '\\s*=\\s*function\\s*' + '\\s*\\(\\w+\\)\\s*\\{(.*?)\\}';
	ytSignFuncBody = ytScriptSrc.match(ytFuncMatch);
	ytSignFuncBody = (ytSignFuncBody) ? ytSignFuncBody[1] : null;
	if (ytSignFuncBody) {
	  ytSwapFuncName = ytSignFuncBody.match(/((\$|_|\w)+)\.(\$|_|\w)+\(\w,[0-9]+\)/);
	  ytSwapFuncName = (ytSwapFuncName) ? ytSwapFuncName[1] : null;
	  if (ytSwapFuncName) {
	    ytFuncMatch = 'var\\s+' + ytSwapFuncName.replace(/\$/, '\\$') + '=\\s*\\{(.*?)\\};';
	    ytSwapFuncBody = ytScriptSrc.match(ytFuncMatch);
	    ytSwapFuncBody = (ytSwapFuncBody) ? ytSwapFuncBody[1] : null;
	  }
	  if (ytSwapFuncBody) ytSignFuncBody = 'var ' + ytSwapFuncName + '={' + ytSwapFuncBody + '};' + ytSignFuncBody;
	  ytSignFuncBody = 'try {' + ytSignFuncBody + '} catch(e) {return null}';
	  ytDecryptSignature = new Function('a', ytSignFuncBody);
	}
      }
    }

    /* Player/Sidebar */
    var ytPlayerWindow, ytSidebarWindow;

    /* Player Sizes */
    var ytPlayerWidth, ytPlayerHeight;
    var ytPlayerWideWidth, ytPlayerWideHeight;
    var ytSidebarMarginWide;
    var ytScreenWidth, ytScreenHeight;
    function ytSizes() {
      if (ytPlayerWindow) {
	if (ytPlayerWindow.clientWidth) ytPlayerWidth = ytPlayerWindow.clientWidth;
	else ytPlayerWidth = ytPlayerWindow.parentNode.clientWidth;
	ytPlayerHeight = Math.ceil(ytPlayerWidth / (16 / 9)) + myPlayerPanelHeight;
	if (ytSidebarWindow && ytSidebarWindow.clientWidth) ytPlayerWideWidth = ytPlayerWidth + ytSidebarWindow.clientWidth;
	else ytPlayerWideWidth = ytPlayerWidth + 425;
	ytPlayerWideHeight = Math.ceil(ytPlayerWideWidth / (16 / 9)) + myPlayerPanelHeight;
	ytSidebarMarginWide = ytPlayerWideHeight + 20;
      }
    }

    /* Player Sizes Update */
    page.win.addEventListener('resize', function() {
      ytSizes();
      player['playerWidth'] = ytPlayerWidth;
      player['playerHeight'] = ytPlayerHeight;
      player['playerWideWidth'] = ytPlayerWideWidth;
      player['playerWideHeight'] = ytPlayerWideHeight;
      player['sidebarMarginWide'] = ytSidebarMarginWide;
      resizeMyPlayer('widesize');
    }, false);

    /* My Player */
    myPlayerWindow = createMyElement('div');
    styleMyElement(myPlayerWindow, {position: 'relative', width: ytPlayerWidth + 'px', height: ytPlayerHeight + 'px', textAlign: 'center'});

    /* Get Player/Sidebar */
    var ytVideosReady = false;
    var ytPlayerWindowTop, ytSidebarWindowTop, ytSidebarAds, ytSidebarHead;
    var ytWaitForObjects = 5;
    var ytWaitForLoops = 50;
    var ytWaitForObject = page.win.setInterval(function() {
      /* Player Window */
      if (!ytPlayerWindow) {
	ytPlayerWindowTop = getMyElement('', 'div', 'id', 'top', -1, false);
	if (!ytPlayerWindowTop) ytPlayerWindowTop = getMyElement('', 'div', 'id', 'primary-inner', -1, false);
	if (ytPlayerWindowTop) {
	  for (var i = 0; i < ytPlayerWindowTop.children.length; i++) {
	    ytPlayerWindow = ytPlayerWindowTop.children[i];
	    if (ytPlayerWindow.id == 'player' || ytPlayerWindow.id == 'plaery') {
	      if (ytPlayerWindow.id == 'player') ytPlayerWindow.id = 'plaery'
	      cleanMyElement(ytPlayerWindow, true);
	      styleMyElement(ytPlayerWindow, {position: 'relative', width: ytPlayerWidth + 'px', height: ytPlayerHeight + 'px'});
	      appendMyElement(ytPlayerWindow, myPlayerWindow);
	      blockObject = ytPlayerWindow;
	      ytSizes();
	      ytWaitForObjects--;
	      if (ytVideosReady) ytPlayer();
	    }
	  }
	}
      }
      /* Sidebar */
      if (!ytSidebarWindow) {
	if (page.url.indexOf('list=') != -1) ytSidebarWindow = getMyElement('', 'div', 'id', 'playlist', -1, false);
	else if (getMyContent(page.url, '"livestream":"(.*?)"', false)) ytSidebarWindow = getMyElement('', 'div', 'id', 'chat', -1, false);
	else {
	  ytSidebarWindowTop = getMyElement('', 'div', 'id', 'top', -1, false);
	  if (!ytSidebarWindowTop) ytSidebarWindowTop = getMyElement('', 'div', 'id', 'secondary-inner', -1, false);
	  if (ytSidebarWindowTop) {
	    for (var i = 0; i < ytSidebarWindowTop.children.length; i++) {
	      ytSidebarWindow = ytSidebarWindowTop.children[i];
	      if (ytSidebarWindow.id == 'related') {
		break;
	      }
	    }
	  }
	}
	if (ytSidebarWindow) {
	  if (player['playerWindow'] && !player['sidebarWindow']) {
	    player['sidebarWindow'] = ytSidebarWindow;
	    ytSizes();
	    if (!option['fullsize']) resizeMyPlayer('widesize');
	  }
	  ytWaitForObjects--;
	}
      }
      /* Sidebar Ads */
      if (ytSidebarWindow) {
	/* Sidebar Ads */
	if (!ytSidebarAds) {
	  ytSidebarAds = getMyElement('', 'div', 'id', 'player-ads', -1, false);
	  if (ytSidebarAds) {
	    styleMyElement(ytSidebarAds, {display: 'none'});
	    ytWaitForObjects--;
	  }
	}
	/* Sidebar Head */
	if (!ytSidebarHead) {
	  ytSidebarHead = getMyElement('', 'div', 'id', 'head', -1, false);
	  if (ytSidebarHead) {
	    styleMyElement(ytSidebarHead, {display: 'none'});
	    ytWaitForObjects--;
	  }
	}
      }
      ytWaitForLoops--;
      if (ytWaitForLoops == 0 || ytWaitForObjects == 0) {
	if (!ytPlayerWindow) showMyMessage('!player');
	clearInterval(ytWaitForObject);
      }
    }, 500);
    intervals.push(ytWaitForObject);

    /* Create Player */
    var ytDefaultVideo = 'Low Definition MP4';
    function ytPlayer() {
      player = {
	'playerSocket': ytPlayerWindow,
	'playerWindow': myPlayerWindow,
	'videoList': ytVideoList,
	'videoDefinitions': ['Ultra High Definition', 'Quad High Definition', 'Full High Definition', 'High Definition', 'Standard Definition', 'Low Definition', 'Very Low Definition'],
	'videoContainers': ['MP4', 'WebM', 'M3U8', 'Any'],
	'videoPlay': ytDefaultVideo,
	'videoThumb': ytVideoThumb,
	'videoTitle': ytVideoTitle,
	'playerWidth': ytPlayerWidth,
	'playerHeight': ytPlayerHeight,
	'playerWideWidth': ytPlayerWideWidth,
	'playerWideHeight': ytPlayerWideHeight,
	'sidebarWindow': ytSidebarWindow,
	'sidebarMarginNormal': 0,
	'sidebarMarginWide': ytSidebarMarginWide
      };
      createMyPlayer();
    }

    /* Parse Videos */
    function ytVideos() {
      var ytVideoFormats = {
	'18': 'Low Definition MP4',
	'22': 'High Definition MP4',
	'43': 'Low Definition WebM',
	'133': 'Very Low Definition Video MP4',
	'134': 'Low Definition Video MP4',
	'135': 'Standard Definition Video MP4',
	'136': 'High Definition Video MP4',
	'137': 'Full High Definition Video MP4',
	'140': 'Medium Bitrate Audio MP4',
	'242': 'Very Low Definition Video WebM',
	'243': 'Low Definition Video WebM',
	'244': 'Standard Definition Video WebM',
	'247': 'High Definition Video WebM',
	'248': 'Full High Definition Video WebM',
	'249': 'Low Bitrate Audio WebM',
	'250': 'Medium Bitrate Audio WebM',
	'251': 'High Bitrate Audio WebM',
	'264': 'Quad High Definition Video MP4',
	'271': 'Quad High Definition Video WebM',
	'272': 'Ultra High Definition Video WebM',
	'298': 'High Definition Video MP4',
	'299': 'Full High Definition Video MP4',
	'302': 'High Definition Video WebM',
	'303': 'Full High Definition Video WebM',
	'308': 'Quad High Definition Video WebM',
	'313': 'Ultra High Definition Video WebM',
	'315': 'Ultra High Definition Video WebM',
	'333': 'Standard Definition Video WebM',
	'334': 'High Definition Video WebM',
	'335': 'Full High Definition Video WebM',
	'337': 'Ultra High Definition Video WebM'
      };
      var ytVideoFound = false;
      var ytVideos = ytVideosContent.split(',');
      var ytVideoParse, ytVideoCodeParse, ytVideoCode, myVideoCode, ytVideo, ytSign, ytSignP;
      for (var i = 0; i < ytVideos.length; i++) {
	ytVideo = ytVideos[i];
	ytVideoCodeParse = ytVideo.match(/itag=(\d{1,3})/);
	ytVideoCode = (ytVideoCodeParse) ? ytVideoCodeParse[1] : null;
	if (!ytVideoCode) continue;
	myVideoCode = ytVideoFormats[ytVideoCode];
	if (!myVideoCode) continue;
	if (!ytVideo.match(/^url/)) {
	  ytVideoParse = ytVideo.match(/(.*)(url=.*$)/);
	  if (ytVideoParse) ytVideo = ytVideoParse[2] + '&' + ytVideoParse[1];
	}
	ytVideo = cleanMyContent(ytVideo, true);
	if (myVideoCode.indexOf('Video') != -1) {
	  if (ytVideo.indexOf('source=yt_otf') != -1) continue;
	}
	ytVideo = ytVideo.replace(/url=/, '').replace(/&$/, '');
	if (ytVideo.match(/itag=/) && ytVideo.match(/itag=/g).length > 1) {
	  if (ytVideo.match(/itag=\d{1,3}&/)) ytVideo = ytVideo.replace(/itag=\d{1,3}&/, '');
	  else if (ytVideo.match(/&itag=\d{1,3}/)) ytVideo = ytVideo.replace(/&itag=\d{1,3}/, '');
	}
	if (ytVideo.match(/clen=/) && ytVideo.match(/clen=/g).length > 1) {
	  if (ytVideo.match(/clen=\d+&/)) ytVideo = ytVideo.replace(/clen=\d+&/, '');
	  else if (ytVideo.match(/&clen=\d+/)) ytVideo = ytVideo.replace(/&clen=\d+/, '');
	}
	if (ytVideo.match(/lmt=/) && ytVideo.match(/lmt=/g).length > 1) {
	  if (ytVideo.match(/lmt=\d+&/)) ytVideo = ytVideo.replace(/lmt=\d+&/, '');
	  else if (ytVideo.match(/&lmt=\d+/)) ytVideo = ytVideo.replace(/&lmt=\d+/, '');
	}
	if (ytVideo.match(/type=(video|audio).*?&/)) ytVideo = ytVideo.replace(/type=(video|audio).*?&/, '');
	else ytVideo = ytVideo.replace(/&type=(video|audio).*$/, '');
	if (ytVideo.match(/xtags=[^%=]*&/)) ytVideo = ytVideo.replace(/xtags=[^%=]*?&/, '');
	else if (ytVideo.match(/&xtags=[^%=]*$/)) ytVideo = ytVideo.replace(/&xtags=[^%=]*$/, '');
	if (ytVideo.match(/&sig=/) && !ytVideo.match(/&lsig=/)) ytVideo = ytVideo.replace(/&sig=/, '&signature=');
	else if (ytVideo.match(/&s=/)) {
	  ytSign = ytVideo.match(/&s=(.*?)(&|$)/);
	  ytSign = (ytSign) ? ytSign[1] : null;
	  if (ytSign) {
	    ytSign = ytDecryptSignature(ytSign);
	    if (ytSign) {
	      ytSignP = ytVideo.match(/&sp=(.*?)(&|$)/);
	      ytSignP = (ytSignP) ? ytSignP[1] : ((ytVideo.match(/&lsig=/)) ? 'sig' : 'signature');
	      ytVideo = ytVideo.replace(/&s=.*?(&|$)/, '&' + ytSignP + '=' + ytSign + '$1');
	    }
	    else ytVideo = '';
	  }
	  else ytVideo = '';
	}
	ytVideo = cleanMyContent(ytVideo, true);
	if (ytVideo.indexOf('ratebypass') == -1) ytVideo += '&ratebypass=yes';
	if (ytVideo && ytVideo.indexOf('http') == 0) {
	  if (!ytVideoFound) ytVideoFound = true;
	  ytVideoList[myVideoCode] = ytVideo;
	}
      }

      if (ytVideoFound) {
	/* DASH */
	feature['dash'] = true;
	if (option['dash']) {
	  if (ytVideoList['Medium Bitrate Audio MP4'] || ytVideoList['Medium Bitrate Audio WebM']) {
	    for (var myVideoCode in ytVideoList) {
	      if (myVideoCode.indexOf('Video') != -1) {
		if (!ytVideoList[myVideoCode.replace(' Video', '')]) {
		  ytVideoList[myVideoCode.replace(' Video', '')] = 'DASH';
		}
	      }
	    }
	  }
	}

	/* DVL */
	feature['direct'] = true;
	ytVideoList['Direct Video Link'] = page.url;

	option['autoget'] = true;
	ytVideosReady = true;
	if (ytPlayerWindow) ytPlayer();
      }
      else {
	if (ytVideosContent.indexOf('conn=rtmp') != -1) showMyMessage('!support');
	else showMyMessage('!videos');
      }
    }

    /* Parse HLS */
    function ytHLS() {
      var ytHLSFormats = {
	'92': 'Very Low Definition M3U8',
	'93': 'Low Definition M3U8',
	'94': 'Standard Definition M3U8',
	'95': 'High Definition M3U8',
	'96': 'Full High Definition M3U8'
      };
      ytVideoList["Any Definition M3U8"] = ytHLSVideos;
      if (ytHLSContent) {
	var ytHLSVideo, ytVideoCodeParse, ytVideoCode, myVideoCode;
	var ytHLSMatcher = new RegExp('(http.*?m3u8)', 'g');
	ytHLSVideos = ytHLSContent.match(ytHLSMatcher);
	if (ytHLSVideos) {
	  for (var i = 0; i < ytHLSVideos.length; i++) {
	    ytHLSVideo = ytHLSVideos[i];
	    ytVideoCodeParse = ytHLSVideo.match(/\/itag\/(\d{1,3})\//);
	    ytVideoCode = (ytVideoCodeParse) ? ytVideoCodeParse[1] : null;
	    if (ytVideoCode) {
	      myVideoCode = ytHLSFormats[ytVideoCode];
	      if (myVideoCode && ytHLSVideo) {
		ytVideoList[myVideoCode] = ytHLSVideo;
	      }
	    }
	  }
	}
      }

      /* DVL */
      feature['direct'] = true;
      ytVideoList['Direct Video Link'] = page.url;

      ytVideoTitle = null;
      ytDefaultVideo = 'Any Definition M3U8';
      ytVideosReady = true;
      if (ytPlayerWindow) ytPlayer();
    }

    /* Get Video ID */
    var ytVideoID = page.url.match(/(?:\?|&)v=(.*?)(&|$)/);
    ytVideoID = (ytVideoID) ? ytVideoID[1] : null;

    /* Get Video Thumbnail */
    var ytVideoThumb;
    if (ytVideoID) ytVideoThumb = 'https://img.youtube.com/vi/' + ytVideoID + '/maxresdefault.jpg';

    /* Get Video Title */
    var ytVideoTitle = getMyContent(page.url, '"videoDetails".*?"title":"((\\\\"|[^"])*?)"', false);
    if (!ytVideoTitle) ytVideoTitle = getMyContent(page.url, '"title":\\{"runs":\\[\\{"text":"((\\\\"|[^"])*?)"', false);
    if (ytVideoTitle) {
      var ytVideoAuthor = getMyContent(page.url, '"author":"((\\\\"|[^"])*?)"', false);
      if (ytVideoAuthor) ytVideoTitle = ytVideoTitle + ' by ' + ytVideoAuthor;
      ytVideoTitle = cleanMyContent(ytVideoTitle, false, true);
    }

    /* Get Videos Content */
    var ytVideosEncodedFmts, ytVideosEncodedFmtsNew, ytVideosAdaptiveFmts, ytVideosAdaptiveFmtsNew, ytVideosContent, ytHLSVideos, ytHLSContent;
    ytVideosEncodedFmts = getMyContent(page.url, '"url_encoded_fmt_stream_map\\\\?":\\s*\\\\?"(.*?)\\\\?"', false);
    if (!ytVideosEncodedFmts) {
      ytVideosEncodedFmtsNew = getMyContent(page.url, '"formats\\\\?":\\s*(\\[.*?\\])', false);
      if (ytVideosEncodedFmtsNew) {
	ytVideosEncodedFmts = '';
	ytVideosEncodedFmtsNew = cleanMyContent(ytVideosEncodedFmtsNew, false);
	ytVideosEncodedFmtsNew = ytVideosEncodedFmtsNew.match(new RegExp('"(url|cipher)":\s*".*?"', 'g'));
	if (ytVideosEncodedFmtsNew) {
	  for (var i = 0 ; i < ytVideosEncodedFmtsNew.length; i++) {
	    ytVideosEncodedFmts += ytVideosEncodedFmtsNew[i].replace(/"/g, '').replace('url:', 'url=').replace('cipher:', '') + ',';
	  }
	  if (ytVideosEncodedFmts.indexOf('%3A%2F%2F') != -1) {
	    ytVideosEncodedFmts = cleanMyContent(ytVideosEncodedFmts, true);
	  }
	}
      }
    }
    ytVideosAdaptiveFmts = getMyContent(page.url, '"adaptive_fmts\\\\?":\\s*\\\\?"(.*?)\\\\?"', false);
    if (!ytVideosAdaptiveFmts) {
      ytVideosAdaptiveFmtsNew = getMyContent(page.url, '"adaptiveFormats\\\\?":\\s*(\\[.*?\\])', false);
      if (ytVideosAdaptiveFmtsNew) {
	ytVideosAdaptiveFmts = '';
	ytVideosAdaptiveFmtsNew = cleanMyContent(ytVideosAdaptiveFmtsNew, false);
	ytVideosAdaptiveFmtsNew = ytVideosAdaptiveFmtsNew.match(new RegExp('"(url|cipher)":\s*".*?"', 'g'));
	if (ytVideosAdaptiveFmtsNew) {
	  for (var i = 0 ; i < ytVideosAdaptiveFmtsNew.length; i++) {
	    ytVideosAdaptiveFmts += ytVideosAdaptiveFmtsNew[i].replace(/"/g, '').replace('url:', 'url=').replace('cipher:', '') + ',';
	  }
	  if (ytVideosAdaptiveFmts.indexOf('%3A%2F%2F') != -1) {
	    ytVideosAdaptiveFmts = cleanMyContent(ytVideosAdaptiveFmts, true);
	  }
	}
      }
    }
    if (!ytVideosAdaptiveFmts) {
      var ytDASHVideos, ytDASHContent;
      ytDASHVideos = getMyContent(page.url, '"dash(?:mpd|ManifestUrl)\\\\?":\\s*\\\\?"(.*?)\\\\?"', false);
      if (ytDASHVideos) {
	ytDASHVideos = cleanMyContent(ytDASHVideos, false);
	ytDASHContent = getMyContent(ytDASHVideos + '?pacing=0', 'TEXT', false);
	if (ytDASHContent) {
	  var ytDASHVideo, ytDASHVideoParts, ytDASHVideoServer, ytDASHVideoParams;
	  ytDASHVideos = ytDASHContent.match(new RegExp('<BaseURL>.*?</BaseURL>', 'g'));
	  if (ytDASHVideos) {
	    ytVideosAdaptiveFmts = '';
	    for (var i = 0; i < ytDASHVideos.length; i++) {
	      ytDASHVideo = ytDASHVideos[i].replace('<BaseURL>', '').replace('</BaseURL>', '');
	      if (ytDASHVideo.indexOf('source/youtube') == -1) continue;
	      ytDASHVideoParts = ytDASHVideo.split('videoplayback/');
	      ytDASHVideoServer = ytDASHVideoParts[0] + 'videoplayback?';
	      ytDASHVideoParams = ytDASHVideoParts[1].split('/');
	      ytDASHVideo = '';
	      for (var p = 0; p < ytDASHVideoParams.length; p++) {
		if (p % 2) ytDASHVideo += ytDASHVideoParams[p] + '&';
		else ytDASHVideo += ytDASHVideoParams[p] + '=';
	      }
	      ytDASHVideo = encodeURIComponent(ytDASHVideoServer + ytDASHVideo);
	      ytDASHVideo = ytDASHVideo.replace('itag%3D', 'itag=');
	      ytVideosAdaptiveFmts += ytDASHVideo + ',';
	    }
	  }
	}
      }
    }
    if (ytVideosEncodedFmts) {
      ytVideosContent = ytVideosEncodedFmts;
    }
    else {
      ytHLSVideos = getMyContent(page.url, '"hls(?:vp|ManifestUrl)\\\\?":\\s*\\\\?"(.*?)\\\\?"', false);
      if (ytHLSVideos) {
	ytHLSVideos = cleanMyContent(ytHLSVideos, false);
	if (ytHLSVideos.indexOf('keepalive/yes/') != -1) ytHLSVideos = ytHLSVideos.replace('keepalive/yes/', '');
      }
      else {
	if (ytVideoID) {
	  var ytVideosInfoPage = page.win.location.protocol + '//' + page.win.location.hostname + '/get_video_info?video_id=' + ytVideoID + '&eurl=https://youtube.googleapis.com/v/';
	  ytVideosEncodedFmts = getMyContent(ytVideosInfoPage, 'url_encoded_fmt_stream_map=(.*?)&', false);
	  if (ytVideosEncodedFmts) {
	    ytVideosEncodedFmts = cleanMyContent(ytVideosEncodedFmts, true);
	    ytVideosContent = ytVideosEncodedFmts;
	  }
	  else {
	    ytVideosEncodedFmtsNew = getMyContent(ytVideosInfoPage, 'formats%22%3A(%5B.*?%5D)', false);
	    if (ytVideosEncodedFmtsNew) {
	      ytVideosEncodedFmts = '';
	      ytVideosEncodedFmtsNew = cleanMyContent(ytVideosEncodedFmtsNew, true);
	      ytVideosEncodedFmtsNew = ytVideosEncodedFmtsNew.match(new RegExp('"(url|cipher)":\s*".*?"', 'g'));
	      if (ytVideosEncodedFmtsNew) {
		for (var i = 0 ; i < ytVideosEncodedFmtsNew.length; i++) {
		  ytVideosEncodedFmts += ytVideosEncodedFmtsNew[i].replace(/"/g, '').replace('url:', 'url=').replace('cipher:', '') + ',';
		}
		if (ytVideosEncodedFmts.indexOf('%3A%2F%2F') != -1) {
		  ytVideosEncodedFmts = cleanMyContent(ytVideosEncodedFmts, true);
		}
		ytVideosContent = ytVideosEncodedFmts;
	      }
	    }
	  }
	  if (!ytVideosAdaptiveFmts) {
	    ytVideosAdaptiveFmts = getMyContent(ytVideosInfoPage, 'adaptive_fmts=(.*?)&', false);
	    if (ytVideosAdaptiveFmts) {
	      ytVideosAdaptiveFmts = cleanMyContent(ytVideosAdaptiveFmts, true);
	    }
	    else {
	      ytVideosAdaptiveFmtsNew = getMyContent(ytVideosInfoPage, 'adaptiveFormats%22%3A(%5B.*?%5D)', false);
	      if (ytVideosAdaptiveFmtsNew) {
		ytVideosAdaptiveFmts = '';
		ytVideosAdaptiveFmtsNew = cleanMyContent(ytVideosAdaptiveFmtsNew, true);
		ytVideosAdaptiveFmtsNew = ytVideosAdaptiveFmtsNew.match(new RegExp('"(url|cipher)":\s*".*?"', 'g'));
		if (ytVideosAdaptiveFmtsNew) {
		  for (var i = 0 ; i < ytVideosAdaptiveFmtsNew.length; i++) {
		    ytVideosAdaptiveFmts += ytVideosAdaptiveFmtsNew[i].replace(/"/g, '').replace('url:', 'url=').replace('cipher:', '') + ',';
		  }
		  if (ytVideosAdaptiveFmts.indexOf('%3A%2F%2F') != -1) {
		    ytVideosAdaptiveFmts = cleanMyContent(ytVideosAdaptiveFmts, true);
		  }
		}
	      }
	    }
	  }
	}
      }
    }
    if (ytVideosAdaptiveFmts && !ytHLSVideos) {
      if (ytVideosContent) ytVideosContent += ',' + ytVideosAdaptiveFmts;
      else ytVideosContent = ytVideosAdaptiveFmts;
    }

    /* Get Videos */
    var ytVideoList = {};
    if (ytVideosContent) {
      if (ytVideosContent.match(/^s=/) || ytVideosContent.match(/&s=/) || ytVideosContent.match(/,s=/) || ytVideosContent.match(/u0026s=/)) {
	var ytScriptURL = getMyContent(page.url, '"js":\\s*"(.*?)"', true);
	if (!ytScriptURL) ytScriptURL = getMyContent(page.url.replace(/watch.*?v=/, 'embed/').replace(/&.*$/, ''), '"js":\\s*"(.*?)"', true);
	if (ytScriptURL) {
	  ytScriptURL = page.win.location.protocol + '//' + page.win.location.hostname + ytScriptURL;
	  ytScriptSrc = getMyContent(ytScriptURL, 'TEXT', false);
	  if (ytScriptSrc) ytDecryptFunction();
	  ytVideos();
	}
	else {
	  showMyMessage('other', 'Couldn\'t get the signature link. Please report it <a href="' + contact + '" style="color:#00892C">here</a>.');
	}
      }
      else {
	ytVideos();
      }
    }
    else {
      if (ytHLSVideos) {
	ytHLSContent = getMyContent(ytHLSVideos, 'TEXT', false);
	ytHLS();
      }
      else {
	showMyMessage('!content');
      }
    }

  }

  // =====YouTube Old===== //

  else if (page.url.indexOf('youtube.com/watch') != -1) {

    /* Video Availability */
    var ytVideoUnavailable = getMyElement('', 'div', 'id', 'player-unavailable', -1, false);
    if (ytVideoUnavailable) {
      if (ytVideoUnavailable.className.indexOf('hid') == -1) {
	var ytAgeGateContent = getMyElement('', 'div', 'id', 'watch7-player-age-gate-content', -1, true);
	if (!ytAgeGateContent) return;
	else {
	  if(ytAgeGateContent.indexOf('feature=private_video') != -1) return;
	}
      }
    }

    /* Decrypt Signature */
    var ytScriptSrc;
    function ytDecryptSignature(s) {return null;}
    function ytDecryptFunction() {
      var ytSignFuncName, ytSignFuncBody, ytSwapFuncName, ytSwapFuncBody, ytFuncMatch;
      ytScriptSrc = ytScriptSrc.replace(/(\r\n|\n|\r)/gm, '');
      ytSignFuncName = ytScriptSrc.match(/"signature"\s*,\s*([^\)]*?)\(/);
      if (!ytSignFuncName) ytSignFuncName = ytScriptSrc.match(/c&&.\.set\(b,(?:encodeURIComponent\()?.*?([a-zA-Z0-9$]+)\(/);
      ytSignFuncName = (ytSignFuncName) ? ytSignFuncName[1] : null;
      if (ytSignFuncName) {
	ytFuncMatch = ytSignFuncName.replace(/\$/, '\\$') + '\\s*=\\s*function\\s*' + '\\s*\\(\\w+\\)\\s*\\{(.*?)\\}';
	ytSignFuncBody = ytScriptSrc.match(ytFuncMatch);
	ytSignFuncBody = (ytSignFuncBody) ? ytSignFuncBody[1] : null;
	if (ytSignFuncBody) {
	  ytSwapFuncName = ytSignFuncBody.match(/((\$|_|\w)+)\.(\$|_|\w)+\(\w,[0-9]+\)/);
	  ytSwapFuncName = (ytSwapFuncName) ? ytSwapFuncName[1] : null;
	  if (ytSwapFuncName) {
	    ytFuncMatch = 'var\\s+' + ytSwapFuncName.replace(/\$/, '\\$') + '=\\s*\\{(.*?)\\};';
	    ytSwapFuncBody = ytScriptSrc.match(ytFuncMatch);
	    ytSwapFuncBody = (ytSwapFuncBody) ? ytSwapFuncBody[1] : null;
	  }
	  if (ytSwapFuncBody) ytSignFuncBody = 'var ' + ytSwapFuncName + '={' + ytSwapFuncBody + '};' + ytSignFuncBody;
	  ytSignFuncBody = 'try {' + ytSignFuncBody + '} catch(e) {return null}';
	  ytDecryptSignature = new Function('a', ytSignFuncBody);
	}
      }
    }

    /* Player Size */
    var ytSidebarMarginNormal = 390;
    var ytSidebarWindow = getMyElement('', 'div', 'id', 'watch7-sidebar', -1, false);
    if (ytSidebarWindow) {
      var ytSidebarWindowStyle = ytSidebarWindow.currentStyle || window.getComputedStyle(ytSidebarWindow, null);
      if (ytSidebarWindowStyle) ytSidebarMarginNormal = -20 + parseInt(ytSidebarWindowStyle.marginTop.replace('px', ''));
      styleMyElement(ytSidebarWindow, {marginTop: ytSidebarMarginNormal + 'px'});
    }
    var ytPlayerWidth, ytPlayerHeight;
    var ytPlayerWideWidth, ytPlayerWideHeight;
    var ytSidebarMarginWide;
    var ytScreenWidth, ytScreenHeight;
    function ytSizes() {
      ytScreenWidth = page.win.innerWidth || page.doc.documentElement.clientWidth;
      ytScreenHeight = page.win.innerHeight || page.doc.documentElement.clientHeight;
      if (ytScreenWidth >= 1720 && ytScreenHeight >= 980) {
	ytPlayerWidth = 1280;
	ytPlayerHeight = 750;
	ytPlayerWideWidth = 1706;
	ytPlayerWideHeight = 990;
      }
      else if (ytScreenWidth >= 1294 && ytScreenHeight >= 630) {
	ytPlayerWidth = 854;
	ytPlayerHeight = 510;
	ytPlayerWideWidth = 1280;
	ytPlayerWideHeight = 750;
      }
      else {
	ytPlayerWidth = 640;
	ytPlayerHeight = 390;
	ytPlayerWideWidth = 1066;
	ytPlayerWideHeight = 630;
      }
      ytSidebarMarginWide = ytPlayerHeight + ytSidebarMarginNormal;
    }

    /* Get Player Window */
    var ytPlayerWindow = getMyElement('', 'div', 'id', 'player', -1, false);
    if (!ytPlayerWindow) {
      showMyMessage('!player');
    }
    else {
      /* Get Video ID */
      var ytVideoID = page.url.match(/(?:\?|&)v=(.*?)(&|$)/);
      ytVideoID = (ytVideoID) ? ytVideoID[1] : null;

      /* Get Video Thumbnail */
      var ytVideoThumb;
      if (ytVideoID) ytVideoThumb = 'https://img.youtube.com/vi/' + ytVideoID + '/maxresdefault.jpg';

      /* Get Video Title */
      var ytVideoTitle = getMyContent(page.url, 'meta\\s+property="og:title"\\s+content="(.*?)"', false);
      if (!ytVideoTitle) ytVideoTitle = getMyContent(page.url, 'meta\\s+itemprop="name"\\s+content="(.*?)"', false);
      if (ytVideoTitle) {
	var ytVideoAuthor = getMyContent(page.url, '"name":\\s*"((\\\\"|[^"])*?)"', false);
	if (ytVideoAuthor) ytVideoTitle = ytVideoTitle + ' by ' + ytVideoAuthor;
	ytVideoTitle = cleanMyContent(ytVideoTitle, false, true);
      }

      /* Get Videos Content */
      var ytVideosEncodedFmts, ytVideosEncodedFmtsNew, ytVideosAdaptiveFmts, ytVideosAdaptiveFmtsNew, ytVideosContent, ytHLSVideos, ytHLSContent;
      ytVideosEncodedFmts = getMyContent(page.url, '"url_encoded_fmt_stream_map\\\\?":\\s*\\\\?"(.*?)\\\\?"', false);
      if (!ytVideosEncodedFmts) {
	ytVideosEncodedFmtsNew = getMyContent(page.url, '"formats\\\\?":\\s*(\\[.*?\\])', false);
	if (ytVideosEncodedFmtsNew) {
	  ytVideosEncodedFmts = '';
	  ytVideosEncodedFmtsNew = cleanMyContent(ytVideosEncodedFmtsNew, false);
	  ytVideosEncodedFmtsNew = ytVideosEncodedFmtsNew.match(new RegExp('"(url|cipher)":\s*".*?"', 'g'));
	  if (ytVideosEncodedFmtsNew) {
	    for (var i = 0 ; i < ytVideosEncodedFmtsNew.length; i++) {
	      ytVideosEncodedFmts += ytVideosEncodedFmtsNew[i].replace(/"/g, '').replace('url:', 'url=').replace('cipher:', '') + ',';
	    }
	    if (ytVideosEncodedFmts.indexOf('%3A%2F%2F') != -1) {
	      ytVideosEncodedFmts = cleanMyContent(ytVideosEncodedFmts, true);
	    }
	  }
	}
      }
      ytVideosAdaptiveFmts = getMyContent(page.url, '"adaptive_fmts\\\\?":\\s*\\\\?"(.*?)\\\\?"', false);
      if (!ytVideosAdaptiveFmts) {
	ytVideosAdaptiveFmtsNew = getMyContent(page.url, '"adaptiveFormats\\\\?":\\s*(\\[.*?\\])', false);
	if (ytVideosAdaptiveFmtsNew) {
	  ytVideosAdaptiveFmts = '';
	  ytVideosAdaptiveFmtsNew = cleanMyContent(ytVideosAdaptiveFmtsNew, false);
	  ytVideosAdaptiveFmtsNew = ytVideosAdaptiveFmtsNew.match(new RegExp('"(url|cipher)":\s*".*?"', 'g'));
	  if (ytVideosAdaptiveFmtsNew) {
	    for (var i = 0 ; i < ytVideosAdaptiveFmtsNew.length; i++) {
	      ytVideosAdaptiveFmts += ytVideosAdaptiveFmtsNew[i].replace(/"/g, '').replace('url:', 'url=').replace('cipher:', '') + ',';
	    }
	    if (ytVideosAdaptiveFmts.indexOf('%3A%2F%2F') != -1) {
	      ytVideosAdaptiveFmts = cleanMyContent(ytVideosAdaptiveFmts, true);
	    }
	  }
	}
      }
      if (!ytVideosAdaptiveFmts) {
	var ytDASHVideos, ytDASHContent;
	ytDASHVideos = getMyContent(page.url, '"dash(?:mpd|ManifestUrl)\\\\?":\\s*\\\\?"(.*?)\\\\?"', false);
	if (ytDASHVideos) {
	  ytDASHVideos = cleanMyContent(ytDASHVideos, false);
	  ytDASHContent = getMyContent(ytDASHVideos + '?pacing=0', 'TEXT', false);
	  if (ytDASHContent) {
	    var ytDASHVideo, ytDASHVideoParts, ytDASHVideoServer, ytDASHVideoParams;
	    ytDASHVideos = ytDASHContent.match(new RegExp('<BaseURL>.*?</BaseURL>', 'g'));
	    if (ytDASHVideos) {
	      ytVideosAdaptiveFmts = '';
	      for (var i = 0; i < ytDASHVideos.length; i++) {
		ytDASHVideo = ytDASHVideos[i].replace('<BaseURL>', '').replace('</BaseURL>', '');
		if (ytDASHVideo.indexOf('source/youtube') == -1) continue;
		ytDASHVideoParts = ytDASHVideo.split('videoplayback/');
		ytDASHVideoServer = ytDASHVideoParts[0] + 'videoplayback?';
		ytDASHVideoParams = ytDASHVideoParts[1].split('/');
		ytDASHVideo = '';
		for (var p = 0; p < ytDASHVideoParams.length; p++) {
		  if (p % 2) ytDASHVideo += ytDASHVideoParams[p] + '&';
		  else ytDASHVideo += ytDASHVideoParams[p] + '=';
		}
		ytDASHVideo = encodeURIComponent(ytDASHVideoServer + ytDASHVideo);
		ytDASHVideo = ytDASHVideo.replace('itag%3D', 'itag=');
		ytVideosAdaptiveFmts += ytDASHVideo + ',';
	      }
	    }
	  }
	}
      }
      if (ytVideosEncodedFmts) {
	ytVideosContent = ytVideosEncodedFmts;
      }
      else {
	ytHLSVideos = getMyContent(page.url, '"hls(?:vp|ManifestUrl)\\\\?":\\s*\\\\?"(.*?)\\\\?"', false);
	if (ytHLSVideos) {
	  ytHLSVideos = cleanMyContent(ytHLSVideos, false);
	  if (ytHLSVideos.indexOf('keepalive/yes/') != -1) ytHLSVideos = ytHLSVideos.replace('keepalive/yes/', '');
	}
	else {
	  if (ytVideoID) {
	    var ytVideosInfoPage = page.win.location.protocol + '//' + page.win.location.hostname + '/get_video_info?video_id=' + ytVideoID + '&eurl=https://youtube.googleapis.com/v/';
	    ytVideosEncodedFmts = getMyContent(ytVideosInfoPage, 'url_encoded_fmt_stream_map=(.*?)&', false);
	    if (ytVideosEncodedFmts) {
	      ytVideosEncodedFmts = cleanMyContent(ytVideosEncodedFmts, true);
	      ytVideosContent = ytVideosEncodedFmts;
	    }
	    else {
	      ytVideosEncodedFmtsNew = getMyContent(ytVideosInfoPage, 'formats%22%3A(%5B.*?%5D)', false);
	      if (ytVideosEncodedFmtsNew) {
		ytVideosEncodedFmts = '';
		ytVideosEncodedFmtsNew = cleanMyContent(ytVideosEncodedFmtsNew, true);
		ytVideosEncodedFmtsNew = ytVideosEncodedFmtsNew.match(new RegExp('"(url|cipher)":\s*".*?"', 'g'));
		if (ytVideosEncodedFmtsNew) {
		  for (var i = 0 ; i < ytVideosEncodedFmtsNew.length; i++) {
		    ytVideosEncodedFmts += ytVideosEncodedFmtsNew[i].replace(/"/g, '').replace('url:', 'url=').replace('cipher:', '') + ',';
		  }
		  if (ytVideosEncodedFmts.indexOf('%3A%2F%2F') != -1) {
		    ytVideosEncodedFmts = cleanMyContent(ytVideosEncodedFmts, true);
		  }
		  ytVideosContent = ytVideosEncodedFmts;
		}
	      }
	    }
	    if (!ytVideosAdaptiveFmts) {
	      ytVideosAdaptiveFmts = getMyContent(ytVideosInfoPage, 'adaptive_fmts=(.*?)&', false);
	      if (ytVideosAdaptiveFmts) {
		ytVideosAdaptiveFmts = cleanMyContent(ytVideosAdaptiveFmts, true);
	      }
	      else {
		ytVideosAdaptiveFmtsNew = getMyContent(ytVideosInfoPage, 'adaptiveFormats%22%3A(%5B.*?%5D)', false);
		if (ytVideosAdaptiveFmtsNew) {
		  ytVideosAdaptiveFmts = '';
		  ytVideosAdaptiveFmtsNew = cleanMyContent(ytVideosAdaptiveFmtsNew, true);
		  ytVideosAdaptiveFmtsNew = ytVideosAdaptiveFmtsNew.match(new RegExp('"(url|cipher)":\s*".*?"', 'g'));
		  if (ytVideosAdaptiveFmtsNew) {
		    for (var i = 0 ; i < ytVideosAdaptiveFmtsNew.length; i++) {
		      ytVideosAdaptiveFmts += ytVideosAdaptiveFmtsNew[i].replace(/"/g, '').replace('url:', 'url=').replace('cipher:', '') + ',';
		    }
		    if (ytVideosAdaptiveFmts.indexOf('%3A%2F%2F') != -1) {
		      ytVideosAdaptiveFmts = cleanMyContent(ytVideosAdaptiveFmts, true);
		    }
		  }
		}
	      }
	    }
	  }
	}
      }
      if (ytVideosAdaptiveFmts && !ytHLSVideos) {
	if (ytVideosContent) ytVideosContent += ',' + ytVideosAdaptiveFmts;
	else ytVideosContent = ytVideosAdaptiveFmts;
      }

      /* Get Sizes */
      ytSizes();

      /* Hide Player Window */
      var ytPlaceholderPlayer = getMyElement('', 'div', 'id', 'placeholder-player', -1, false);
      if (ytPlaceholderPlayer) styleMyElement(ytPlaceholderPlayer, {display: 'none'});

      /* Hide Sidebar Ads */
      var ytSidebarAds = getMyElement('', 'div', 'id', 'watch7-sidebar-ads', -1, false);
      if (ytSidebarAds) styleMyElement(ytSidebarAds, {display: 'none'});

      /* Hide Autoplay */
      var ytAutoplay = getMyElement('', 'div', 'class', 'checkbox-on-off', 0, false);
      if (ytAutoplay) styleMyElement(ytAutoplay, {display: 'none'});

      /* Playlist */
      var ytPlaylist = getMyElement('', 'div', 'id', 'player-playlist', -1, false);
      if (ytPlaylist) {
	styleMyElement(ytPlaylist, {marginLeft: '-' + ytPlayerWidth + 'px'});
	var ytPlaceholderPlaylist = getMyElement('', 'div', 'id', 'placeholder-playlist', -1, false);
	if (ytPlaceholderPlaylist) appendMyElement(ytPlaceholderPlaylist, ytPlaylist);
      }

      /* My Player Window */
      myPlayerWindow = createMyElement('div');
      styleMyElement(myPlayerWindow, {position: 'relative', width: ytPlayerWidth + 'px', height: ytPlayerHeight + 'px', textAlign: 'center'});
      cleanMyElement(ytPlayerWindow, true);
      appendMyElement(ytPlayerWindow, myPlayerWindow);
      blockObject = ytPlayerWindow;

      /* Update Sizes */
      page.win.addEventListener('resize', function() {
	ytSizes();
	player['playerWidth'] = ytPlayerWidth;
	player['playerHeight'] = ytPlayerHeight;
	player['playerWideWidth'] = ytPlayerWideWidth;
	player['playerWideHeight'] = ytPlayerWideHeight;
	player['sidebarMarginWide'] = ytSidebarMarginWide;
	resizeMyPlayer('widesize');
	if (ytPlaylist) styleMyElement(ytPlaylist, {marginLeft: '-' + ytPlayerWidth + 'px'});
      }, false);

      /* Create Player */
      var ytDefaultVideo = 'Low Definition MP4';
      function ytPlayer() {
	player = {
	  'playerSocket': ytPlayerWindow,
	  'playerWindow': myPlayerWindow,
	  'videoList': ytVideoList,
	  'videoDefinitions': ['Ultra High Definition', 'Quad High Definition', 'Full High Definition', 'High Definition', 'Standard Definition', 'Low Definition', 'Very Low Definition'],
	  'videoContainers': ['MP4', 'WebM', 'M3U8', 'Any'],
	  'videoPlay': ytDefaultVideo,
	  'videoThumb': ytVideoThumb,
	  'videoTitle': ytVideoTitle,
	  'playerWidth': ytPlayerWidth,
	  'playerHeight': ytPlayerHeight,
	  'playerWideWidth': ytPlayerWideWidth,
	  'playerWideHeight': ytPlayerWideHeight,
	  'sidebarWindow': ytSidebarWindow,
	  'sidebarMarginNormal': ytSidebarMarginNormal,
	  'sidebarMarginWide': ytSidebarMarginWide
	};
	createMyPlayer();
      }

      /* Parse Videos */
      function ytVideos() {
	var ytVideoFormats = {
	  '18': 'Low Definition MP4',
	  '22': 'High Definition MP4',
	  '43': 'Low Definition WebM',
	  '133': 'Very Low Definition Video MP4',
	  '134': 'Low Definition Video MP4',
	  '135': 'Standard Definition Video MP4',
	  '136': 'High Definition Video MP4',
	  '137': 'Full High Definition Video MP4',
	  '140': 'Medium Bitrate Audio MP4',
	  '242': 'Very Low Definition Video WebM',
	  '243': 'Low Definition Video WebM',
	  '244': 'Standard Definition Video WebM',
	  '247': 'High Definition Video WebM',
	  '248': 'Full High Definition Video WebM',
	  '249': 'Low Bitrate Audio WebM',
	  '250': 'Medium Bitrate Audio WebM',
	  '251': 'High Bitrate Audio WebM',
	  '264': 'Quad High Definition Video MP4',
	  '271': 'Quad High Definition Video WebM',
	  '272': 'Ultra High Definition Video WebM',
	  '298': 'High Definition Video MP4',
	  '299': 'Full High Definition Video MP4',
	  '302': 'High Definition Video WebM',
	  '303': 'Full High Definition Video WebM',
	  '308': 'Quad High Definition Video WebM',
	  '313': 'Ultra High Definition Video WebM',
	  '315': 'Ultra High Definition Video WebM',
	  '333': 'Standard Definition Video WebM',
	  '334': 'High Definition Video WebM',
	  '335': 'Full High Definition Video WebM',
	  '337': 'Ultra High Definition Video WebM'
	};
	var ytVideoFound = false;
	var ytVideos = ytVideosContent.split(',');
	var ytVideoParse, ytVideoCodeParse, ytVideoCode, myVideoCode, ytVideo, ytSign, ytSignP;
	for (var i = 0; i < ytVideos.length; i++) {
	  ytVideo = ytVideos[i];
	  ytVideoCodeParse = ytVideo.match(/itag=(\d{1,3})/);
	  ytVideoCode = (ytVideoCodeParse) ? ytVideoCodeParse[1] : null;
	  if (!ytVideoCode) continue;
	  myVideoCode = ytVideoFormats[ytVideoCode];
	  if (!myVideoCode) continue;
	  if (!ytVideo.match(/^url/)) {
	    ytVideoParse = ytVideo.match(/(.*)(url=.*$)/);
	    if (ytVideoParse) ytVideo = ytVideoParse[2] + '&' + ytVideoParse[1];
	  }
	  ytVideo = cleanMyContent(ytVideo, true);
	  if (myVideoCode.indexOf('Video') != -1) {
	    if (ytVideo.indexOf('source=yt_otf') != -1) continue;
	  }
	  ytVideo = ytVideo.replace(/url=/, '').replace(/&$/, '');
	  if (ytVideo.match(/itag=/) && ytVideo.match(/itag=/g).length > 1) {
	    if (ytVideo.match(/itag=\d{1,3}&/)) ytVideo = ytVideo.replace(/itag=\d{1,3}&/, '');
	    else if (ytVideo.match(/&itag=\d{1,3}/)) ytVideo = ytVideo.replace(/&itag=\d{1,3}/, '');
	  }
	  if (ytVideo.match(/clen=/) && ytVideo.match(/clen=/g).length > 1) {
	    if (ytVideo.match(/clen=\d+&/)) ytVideo = ytVideo.replace(/clen=\d+&/, '');
	    else if (ytVideo.match(/&clen=\d+/)) ytVideo = ytVideo.replace(/&clen=\d+/, '');
	  }
	  if (ytVideo.match(/lmt=/) && ytVideo.match(/lmt=/g).length > 1) {
	    if (ytVideo.match(/lmt=\d+&/)) ytVideo = ytVideo.replace(/lmt=\d+&/, '');
	    else if (ytVideo.match(/&lmt=\d+/)) ytVideo = ytVideo.replace(/&lmt=\d+/, '');
	  }
	  if (ytVideo.match(/type=(video|audio).*?&/)) ytVideo = ytVideo.replace(/type=(video|audio).*?&/, '');
	  else ytVideo = ytVideo.replace(/&type=(video|audio).*$/, '');
	  if (ytVideo.match(/xtags=[^%=]*&/)) ytVideo = ytVideo.replace(/xtags=[^%=]*?&/, '');
	  else if (ytVideo.match(/&xtags=[^%=]*$/)) ytVideo = ytVideo.replace(/&xtags=[^%=]*$/, '');
	  if (ytVideo.match(/&sig=/) && !ytVideo.match(/&lsig=/)) ytVideo = ytVideo.replace(/&sig=/, '&signature=');
	  else if (ytVideo.match(/&s=/)) {
	    ytSign = ytVideo.match(/&s=(.*?)(&|$)/);
	    ytSign = (ytSign) ? ytSign[1] : null;
	    if (ytSign) {
	      ytSign = ytDecryptSignature(ytSign);
	      if (ytSign) {
		ytSignP = ytVideo.match(/&sp=(.*?)(&|$)/);
		ytSignP = (ytSignP) ? ytSignP[1] : ((ytVideo.match(/&lsig=/)) ? 'sig' : 'signature');
		ytVideo = ytVideo.replace(/&s=.*?(&|$)/, '&' + ytSignP + '=' + ytSign + '$1');
	      }
	      else ytVideo = '';
	    }
	    else ytVideo = '';
	  }
	  ytVideo = cleanMyContent(ytVideo, true);
	  if (ytVideo.indexOf('ratebypass') == -1) ytVideo += '&ratebypass=yes';
	  if (ytVideo && ytVideo.indexOf('http') == 0) {
	    if (!ytVideoFound) ytVideoFound = true;
	    ytVideoList[myVideoCode] = ytVideo;
	  }
	}

	if (ytVideoFound) {
	  /* DASH */
	  feature['dash'] = true;
	  if (option['dash']) {
	    if (ytVideoList['Medium Bitrate Audio MP4'] || ytVideoList['Medium Bitrate Audio WebM']) {
	      for (var myVideoCode in ytVideoList) {
		if (myVideoCode.indexOf('Video') != -1) {
		  if (!ytVideoList[myVideoCode.replace(' Video', '')]) {
		    ytVideoList[myVideoCode.replace(' Video', '')] = 'DASH';
		  }
		}
	      }
	    }
	  }

	  /* DVL */
	  feature['direct'] = true;
	  ytVideoList['Direct Video Link'] = page.url;

	  option['autoget'] = true;
	  ytPlayer();
	}
	else {
	  if (ytVideosContent.indexOf('conn=rtmp') != -1) showMyMessage('!support');
	  else showMyMessage('!videos');
	}
      }

      /* Parse HLS */
      function ytHLS() {
	var ytHLSFormats = {
	  '92': 'Very Low Definition M3U8',
	  '93': 'Low Definition M3U8',
	  '94': 'Standard Definition M3U8',
	  '95': 'High Definition M3U8',
	  '96': 'Full High Definition M3U8'
	};
	ytVideoList["Any Definition M3U8"] = ytHLSVideos;
	if (ytHLSContent) {
	  var ytHLSVideo, ytVideoCodeParse, ytVideoCode, myVideoCode;
	  var ytHLSMatcher = new RegExp('(http.*?m3u8)', 'g');
	  ytHLSVideos = ytHLSContent.match(ytHLSMatcher);
	  if (ytHLSVideos) {
	    for (var i = 0; i < ytHLSVideos.length; i++) {
	      ytHLSVideo = ytHLSVideos[i];
	      ytVideoCodeParse = ytHLSVideo.match(/\/itag\/(\d{1,3})\//);
	      ytVideoCode = (ytVideoCodeParse) ? ytVideoCodeParse[1] : null;
	      if (ytVideoCode) {
		myVideoCode = ytHLSFormats[ytVideoCode];
		if (myVideoCode && ytHLSVideo) {
		  ytVideoList[myVideoCode] = ytHLSVideo;
		}
	      }
	    }
	  }
	}

	/* DVL */
	feature['direct'] = true;
	ytVideoList['Direct Video Link'] = page.url;

	ytVideoTitle = null;
	ytDefaultVideo = 'Any Definition M3U8';
	ytPlayer();
      }

      /* Get Videos */
      var ytVideoList = {};
      if (ytVideosContent) {
	if (ytVideosContent.match(/^s=/) || ytVideosContent.match(/&s=/) || ytVideosContent.match(/,s=/) || ytVideosContent.match(/u0026s=/)) {
	  var ytScriptURL = getMyContent(page.url, '"js":\\s*"(.*?)"', true);
	  if (!ytScriptURL) ytScriptURL = getMyContent(page.url.replace(/watch.*?v=/, 'embed/').replace(/&.*$/, ''), '"js":\\s*"(.*?)"', true);
	  if (ytScriptURL) {
	    ytScriptURL = page.win.location.protocol + '//' + page.win.location.hostname + ytScriptURL;
	    ytScriptSrc = getMyContent(ytScriptURL, 'TEXT', false);
	    if (ytScriptSrc) ytDecryptFunction();
	    ytVideos();
	  }
	  else {
	    showMyMessage('other', 'Couldn\'t get the signature link. Please report it <a href="' + contact + '" style="color:#00892C">here</a>.');
	  }
	}
	else {
	  ytVideos();
	}
      }
      else {
	if (ytHLSVideos) {
	  ytHLSContent = getMyContent(ytHLSVideos, 'TEXT', false);
	  ytHLS();
	}
	else {
	  showMyMessage('!content');
	}
      }
    }

  }

  // =====Dailymotion===== //

  else if (page.url.indexOf('dailymotion.com/video') != -1) {

    /* Video Availability */
    if (getMyContent(page.url.replace(/\/video\//, "/embed/video/"), '"error":\\{"title":"(.*?)"', false)) return;
    if (getMyContent(page.url.replace(/\/video\//, "/embed/video/"), '"error_title":"(.*?)"', false)) return;

    /* Player Sizes */
    var dmPlayerWidth, dmPlayerHeight;
    function dmSizes() {
      if (dmPlayerWindow) dmPlayerWidth = dmPlayerWindow.clientWidth;
      if (dmPlayerWidth) dmPlayerHeight = Math.ceil(dmPlayerWidth / (16 / 9)) + myPlayerPanelHeight;
    }

    /* Resize Event */
    page.win.addEventListener('resize', function() {
      page.win.setTimeout(function() {
	dmSizes();
	player['playerWidth'] = dmPlayerWidth;
	player['playerHeight'] = dmPlayerHeight;
	resizeMyPlayer('widesize');
      }, 300);
    }, false);

    /* My Player Window */
    myPlayerWindow = createMyElement('div');

    /* Get Objects */
    var dmVideosReady = false;
    var dmPlayerWindow;
    var dmWaitForLoops = 50;
    var dmWaitForObject = page.win.setInterval(function() {
      if (!dmPlayerWindow) dmPlayerWindow = getMyElement('', 'div', 'id', 'player-wrapper', -1, false);
      if (dmPlayerWindow && !myPlayerWindow.parentNode) {
	cleanMyElement(myPlayerWindow, false);
	cleanMyElement(dmPlayerWindow, true);
	appendMyElement(dmPlayerWindow, myPlayerWindow);
	blockObject = dmPlayerWindow;
	dmSizes();
	styleMyElement(myPlayerWindow, {position: 'relative', width: dmPlayerWidth + 'px', height: dmPlayerHeight + 'px', textAlign: 'center'});
	styleMyElement(dmPlayerWindow, {marginTop: '-15px'})
	if (dmVideosReady) dmPlayer();
      }
      dmWaitForLoops--;
      if (dmWaitForLoops == 0) {
	if (!dmPlayerWindow) showMyMessage('!player');
	clearInterval(dmWaitForObject);
      }
      /* Hide Ads */
      var dmAdsTop = getMyElement('', 'div', 'query', '[class^="AdTop__adTop"]', -1, false);
      if (dmAdsTop && dmAdsTop.parentNode) removeMyElement(dmAdsTop.parentNode, dmAdsTop);
      var dmAdsRightBottom = getMyElement('', 'div', 'query', '[class^="AdWatchingRight__container"]', -1, false);
      if (dmAdsRightBottom && dmAdsRightBottom.parentNode) removeMyElement(dmAdsRightBottom.parentNode, dmAdsRightBottom);
      var dmAdsRight = getMyElement('', 'div', 'query', '[class^="DiscoveryVideoSection__adCell"]', -1, false);
      if (dmAdsRight && dmAdsRight.parentNode && dmAdsRight.parentNode.parentNode) removeMyElement(dmAdsRight.parentNode.parentNode, dmAdsRight.parentNode);
      /* Hide Player Placeholder */
      var dmPlayerPlaceholder = getMyElement('', 'div', 'id', 'watching-player-placeholder', -1, false);
      if (dmPlayerPlaceholder) styleMyElement(dmPlayerPlaceholder, {background: 'none',});
    }, 500);
    intervals.push(dmWaitForObject);

    /* Create Player */
    var dmDefaultVideo = 'Low Definition MP4';
    function dmPlayer() {
      if (!dmVideoList[dmDefaultVideo]) dmDefaultVideo = 'Low Definition M3U8';
      player = {
	'playerSocket': dmPlayerWindow,
	'playerWindow': myPlayerWindow,
	'videoList': dmVideoList,
	'videoDefinitions': ['Full High Definition', 'High Definition', 'Standard Definition', 'Low Definition', 'Very Low Definition'],
	'videoContainers': ['MP4', 'M3U8'],
	'videoPlay': dmDefaultVideo,
	'videoThumb': dmVideoThumb,
	'videoTitle': dmVideoTitle,
	'playerWidth': dmPlayerWidth,
	'playerHeight': dmPlayerHeight
      };
      feature['container'] = false;
      feature['widesize'] = false;
      createMyPlayer();
    }

    /* Get Video Thumbnail */
    var dmVideoThumb = getMyContent(page.url.replace(/\/video\//, "/embed/video/"), '"posters":.*?"720":"(.*?)"', false);
    if (dmVideoThumb) dmVideoThumb = cleanMyContent(dmVideoThumb, false);

    /* Get Video Title */
    var dmVideoTitle = getMyContent(page.url.replace(/\/video\//, "/embed/video/"), '"title":"((\\\\"|[^"])*?)"', false);
    if (dmVideoTitle) {
      var dmVideoAuthor = getMyContent(page.url.replace(/\/video\//, "/embed/video/"), '"screenname":"((\\\\"|[^"])*?)"', false);
      if (dmVideoAuthor) dmVideoTitle = dmVideoTitle + ' by ' + dmVideoAuthor;
      dmVideoTitle = cleanMyContent(dmVideoTitle, false, true);
    }

    /* Get Videos Content */
    var dmVideosContent = getMyContent(page.url.replace(/\/video\//, "/embed/video/"), '"qualities":\\{(.*?)\\]\\},', false);

    /* Get Videos */
    var dmVideoList = {};
    if (dmVideosContent) {
      var dmVideoFormats = {'auto': 'Low Definition MP4', '240': 'Very Low Definition MP4', '380': 'Low Definition MP4',
			     '480': 'Standard Definition MP4', '720': 'High Definition MP4', '1080': 'Full High Definition MP4'};
      var dmVideoFound = false;
      var dmVideoParser, dmVideoParse, myVideoCode, dmVideo;
      for (var dmVideoCode in dmVideoFormats) {
	dmVideoParser = '"' + dmVideoCode + '".*?"type":"video.*?mp4","url":"(.*?)"';
	dmVideoParse = dmVideosContent.match(dmVideoParser);
	if (!dmVideoParse) {
	  dmVideoParser = '"' + dmVideoCode + '".*?"type":"application.*?mpegURL","url":"(.*?)"';
	  dmVideoParse = dmVideosContent.match(dmVideoParser);
	}
	dmVideo = (dmVideoParse) ? dmVideoParse[1] : null;
	if (dmVideo) {
	  if (!dmVideoFound) dmVideoFound = true;
	  dmVideo = cleanMyContent(dmVideo, true);
	  myVideoCode = dmVideoFormats[dmVideoCode];
	  if (dmVideo.indexOf('.m3u8') != -1) myVideoCode = myVideoCode.replace('MP4', 'M3U8');
	  if (!dmVideoList[myVideoCode]) dmVideoList[myVideoCode] = dmVideo;
	}
      }

      if (dmVideoFound) {
	/* DVL */
	feature['direct'] = true;
	dmVideoList['Direct Video Link'] = page.url;

	dmVideosReady = true;
	//if (dmPlayerWindow) dmPlayer();
      }
      else {
	showMyMessage('!videos');
      }
    }
    else {
      showMyMessage('!content');
    }

  }

  // =====Vimeo===== //

  else if (page.url.indexOf('vimeo.com/') != -1) {

    /* Page Type */
    var viPageType = getMyContent(page.url, 'meta\\s+property="og:type"\\s+content="(.*?)"', false);
    if (!viPageType || (viPageType != 'video' && viPageType != 'profile')) return;

    /* Get Player Window */
    var viPlayerWindow;
    if (viPageType == 'video') viPlayerWindow = getMyElement('', 'div', 'class', 'player_area', 0, false);
    else {
      viPlayerWindow = getMyElement('', 'div', 'class', 'player_container', 1, false) || getMyElement('', 'div', 'class', 'player_container', 0, false);
    }
    if (!viPlayerWindow) {
      showMyMessage('!player');
    }
    else {
      /* Get Video Thumbnail */
      var viVideoThumb;
      if (viPageType == 'video') {
	viVideoThumb = getMyContent(page.url, 'meta\\s+property="og:image"\\s+content="(.*?)"', false);
	if (!viVideoThumb) viVideoThumb = getMyContent(page.url, 'meta\\s+name="twitter:image"\\s+content="(.*?)"', false);
      }
      else {
	viVideoThumb = getMyContent(page.url, '"src_4x":"(.*?)"', false);
	if (!viVideoThumb) viVideoThumb = getMyContent(page.url, '"src_2x":"(.*?)"', false);
	if (viVideoThumb) viVideoThumb = cleanMyContent(viVideoThumb, false);
      }

      /* Get Video Title */
      var viVideoTitle;
      if (viPageType == 'video') {
	viVideoTitle = getMyContent(page.url, 'meta\\s+property="og:title"\\s+content="(.*?)"', false);
      }
      else {
	viVideoTitle = getMyContent(page.url, '"title":"((\\\\"|[^"])*?)"', false);
      }
      if (viVideoTitle) {
	viVideoTitle = viVideoTitle.replace(/\s*on\s*Vimeo$/, '');
	var viVideoAuthor = getMyContent(page.url, '"display_name":"((\\\\"|[^"])*?)"', false);
	if (viVideoAuthor) viVideoTitle = viVideoTitle + ' by ' + viVideoAuthor;
	viVideoTitle = cleanMyContent(viVideoTitle, false, true);
      }

      /* Get Content Source */
      var viVideoSource = getMyContent(page.url, 'config_url":"(.*?)"', false);
      if (viVideoSource) viVideoSource = cleanMyContent(viVideoSource, false);
      else {
	viVideoSource = getMyContent(page.url, 'data-config-url="(.*?)"', false);
	if (viVideoSource) viVideoSource = viVideoSource.replace(/&amp;/g, '&');
      }

      /* Get Videos Content */
      var viVideosContent;
      if (viVideoSource) {
	viVideosContent = getMyContent(viVideoSource, '"progressive":\\[(.*?)\\]', false);
      }

      /* My Player Window */
      myPlayerWindow = createMyElement('div');
      styleMyElement(myPlayerWindow, {position: 'relative', width: '920px', height: '548px', textAlign: 'center', margin: '0px auto'});
      styleMyElement(viPlayerWindow, {minHeight: '548px', position: 'static'});
      if (viPlayerWindow.parentNode) {
	styleMyElement(viPlayerWindow.parentNode, {minHeight: '548px', position: 'relative'});
	if (viPageType == 'profile') {
	  styleMyElement(viPlayerWindow.parentNode, {marginLeft: '-50px'});
	}
      }
      cleanMyElement(viPlayerWindow, true);
      appendMyElement(viPlayerWindow, myPlayerWindow);
      blockObject = viPlayerWindow;

      /* Get Videos */
      if (viVideosContent) {
	var viVideoFormats = {'1440p': 'Quad High Definition MP4', '1080p': 'Full High Definition MP4', '720p': 'High Definition MP4', '540p': 'Standard Definition MP4',
			       '480p': 'Standard Definition MP4', '360p': 'Low Definition MP4', '270p': 'Very Low Definition MP4', '240p': 'Very Low Definition MP4'};
	var viVideoList = {};
	var viVideoFound = false;
	var viVideo, myVideoCode;
	var viVideos = viVideosContent.split('},');
	for (var i = 0; i < viVideos.length; i++) {
	  for (var viVideoCode in viVideoFormats) {
	    if (viVideos[i].indexOf('"quality":"' + viVideoCode + '"') != -1) {
	      viVideo = viVideos[i].match(/"url":"(.*?)"/);
	      viVideo = (viVideo) ? viVideo[1] : null;
	      if (viVideo) {
		if (!viVideoFound) viVideoFound = true;
		myVideoCode = viVideoFormats[viVideoCode];
		viVideoList[myVideoCode] = viVideo;
	      }
	    }
	  }
	}

	if (viVideoFound) {
	  /* Hide Autoplay Button */
	  var viContextClip = getMyElement('', 'div', 'class', 'context-clip', 0, false);
	  if (viContextClip) {
	    var viAutoplayElem = viContextClip.getElementsByTagName('div')[1];
	    if (viAutoplayElem && viAutoplayElem.textContent.indexOf('Autoplay') != -1) {
	      styleMyElement(viAutoplayElem, {display: 'none'});
	    }
	  }

	  /* DVL */
	  feature['direct'] = true;
	  viVideoList['Direct Video Link'] = page.url;

	  /* Create Player */
	  var viDefaultVideo = 'Low Definition MP4';
	  player = {
	    'playerSocket': viPlayerWindow,
	    'playerWindow': myPlayerWindow,
	    'videoList': viVideoList,
	    'videoDefinitions': ['Quad High Definition', 'Full High Definition', 'High Definition', 'Standard Definition', 'Low Definition', 'Very Low Definition'],
	    'videoContainers': ['MP4'],
	    'videoPlay': viDefaultVideo,
	    'videoThumb': viVideoThumb,
	    'videoTitle' : viVideoTitle,
	    'playerWidth': 920,
	    'playerHeight': 548
	  };
	  feature['container'] = false;
	  feature['widesize'] = false;
	  createMyPlayer();
	}
	else {
	  showMyMessage('!videos');
	}
      }
      else {
	showMyMessage('!content');
      }
    }

  }

  // =====Metacafe===== //

  else if (page.url.indexOf('metacafe.com/watch') != -1) {

    /* Get Player Window */
    mcPlayerWindow = getMyElement('', 'div', 'class', 'mc-player-wrap', 0, false);
    if (!mcPlayerWindow) {
      showMyMessage('!player');
    }
    else {
      /* Get Video Thumbnail */
      var mcVideoThumb = getMyContent(page.url, '"preview":"(.*?)"', false);
      if (mcVideoThumb) mcVideoThumb = cleanMyContent(mcVideoThumb, false);

      /* Get Video Title */
      var mcVideoTitle = getMyContent(page.url, 'meta\\s+property="og:title"\\s+content="(.*?)"', false);
      if (mcVideoTitle) mcVideoTitle = cleanMyContent(mcVideoTitle, false, true);

      /* Get Videos Content */
      var mcVideosContent = getMyContent(page.url, 'flashvars\\s*=\\s*\\{(.*?)\\};', false);

      /* Player Size */
      var mcPlayerWidth, mcPlayerHeight;
      function mcGetSizes() {
	mcPlayerWidth = mcPlayerWindow.clientWidth;
	mcPlayerHeight = Math.ceil(mcPlayerWidth / (16 / 9)) + myPlayerPanelHeight;
      }
      function mcUpdateSizes() {
	mcGetSizes();
	player['playerWidth'] = mcPlayerWidth;
	player['playerHeight'] = mcPlayerHeight;
	resizeMyPlayer('widesize');
      }
      mcGetSizes();

      /* My Player Window */
      myPlayerWindow = createMyElement('div');
      styleMyElement(myPlayerWindow, {position: 'relative', width: mcPlayerWidth + 'px', height: mcPlayerHeight + 'px', textAlign: 'center'});
      cleanMyElement(mcPlayerWindow, true);
      appendMyElement(mcPlayerWindow, myPlayerWindow);
      blockObject = mcPlayerWindow;

      /* Resize Event */
      page.win.addEventListener('resize', mcUpdateSizes, false);

      /* Hide Ads */
      var mcTopAd = getMyElement('', 'div', 'class', 'mc-action', 0, false);
      if (mcTopAd && mcTopAd.parentNode) removeMyElement(mcTopAd.parentNode, mcTopAd);
      var mcRightAd = getMyElement('', 'div', 'class', 'mc-action', 1, false);
      if (mcRightAd && mcRightAd.parentNode) removeMyElement(mcRightAd.parentNode, mcRightAd);

      /* Get Videos */
      if (mcVideosContent) {
	var mcVideoList = {};
	var mcVideoFound = false;
	var mcVideoFormats = {'video_alt_url2': 'High Definition M3U8', 'video_alt_url': 'Low Definition M3U8', 'video_url': 'Very Low Definition M3U8'};
	var mcVideoFormatz = {'video_alt_url2': '_720p', 'video_alt_url': '_360p', 'video_url': '_240p'};
	var mcVideoHLS = mcVideosContent.match(/"src":"(.*?)"/);
	mcVideoHLS = (mcVideoHLS) ? cleanMyContent(mcVideoHLS[1], false) : null;
	if (mcVideoHLS) {
	  var mcVideoParser, mcVideoParse, myVideoCode, mcVideo;
	  for (var mcVideoCode in mcVideoFormats) {
	    mcVideoParser = '"' + mcVideoCode + '":"(.*?)"';
	    mcVideoParse = mcVideosContent.match(mcVideoParser);
	    mcVideo = (mcVideoParse) ? mcVideoParse[1] : null;
	    if (mcVideo) {
	      if (!mcVideoFound) mcVideoFound = true;
	      myVideoCode = mcVideoFormats[mcVideoCode];
	      mcVideoList[myVideoCode] = mcVideoHLS.replace('.m3u8', mcVideoFormatz[mcVideoCode] + '.m3u8');
	    }
	  }
	}

	if (mcVideoFound) {
	  /* Create Player */
	  var mcDefaultVideo = 'Low Definition M3U8';
	  player = {
	    'playerSocket': mcPlayerWindow,
	    'playerWindow': myPlayerWindow,
	    'videoList': mcVideoList,
	    'videoDefinitions': ['High Definition', 'Low Definition', 'Very Low Definition'],
	    'videoContainers': ['M3U8'],
	    'videoPlay': mcDefaultVideo,
	    'videoThumb': mcVideoThumb,
	    'videoTitle' : mcVideoTitle,
	    'playerWidth': mcPlayerWidth,
	    'playerHeight': mcPlayerHeight
	  };
	  feature['container'] = false;
	  feature['widesize'] = false;
	  createMyPlayer();
	}
	else {
	  showMyMessage('!videos');
	}
      }
      else {
	var ytVideoId = page.url.match(/\/yt-(.*?)\//);
	if (ytVideoId && ytVideoId[1]) {
	  var ytVideoLink = 'http://youtube.com/watch?v=' + ytVideoId[1];
	  showMyMessage('embed', ytVideoLink);
	}
	else {
	  showMyMessage('!content');
	}
      }
    }

  }

  // =====Veoh===== //

  else if (page.url.indexOf('veoh.com/watch') != -1) {

    page.win.setTimeout(function() {

    /* Get Video Availability */
    if (getMyElement('', 'div', 'class', 'veoh-video-player-error', 0, false)) return;

    /* Get Player Window */
    var vePlayerWindow = getMyElement('', 'div', 'class', 'veoh-player', 0, false);
    if (!vePlayerWindow) {
      showMyMessage('!player');
    }
    else {
      /* Get Video Thumbnail */
      var veVideoThumb = getMyContent(page.url.replace(/\/watch\//, '/watch/getVideo/'), '"poster":"(.*?)"', false);

      /* Get Video Title */
      var veVideoTitle = getMyContent(page.url, 'meta\\s+name="og:title"\\s+content="(.*?)"', false);
      if (!veVideoTitle) {
	veVideoTitle = getMyContent(page.url.replace(/\/watch\//, '/watch/getVideo/'), '"title":"((\\\\"|[^"])*?)"', false);
      }
      if (veVideoTitle) veVideoTitle = cleanMyContent(veVideoTitle, false, true);

      /* Get Videos Content */
      var veVideosContent = getMyContent(page.url.replace(/\/watch\//, '/watch/getVideo/'), '"src"\\s*:\\s*\\{(.*?)\\}', false);

      /* My Player Window */
      myPlayerWindow = createMyElement('div');
      styleMyElement(myPlayerWindow, {position: 'relative', width: '640px', height: '390px', textAlign: 'center'});
      cleanMyElement(vePlayerWindow, false);
      styleMyElement(vePlayerWindow, {height: '100%'});
      appendMyElement(vePlayerWindow, myPlayerWindow);

      /* Hide Ads */
      var veBannersRight = getMyElement('', 'div', 'class', 'banners-right-container', 0, false);
      if (veBannersRight) styleMyElement(veBannersRight, {display: 'none'});

      /* Get Videos */
      if (veVideosContent) {
	var veVideoFormats = {'Regular': 'Low Definition MP4', 'HQ': 'Standard Definition MP4'};
	var veVideoList = {};
	var veVideoFound = false;
	var veVideoParser, veVideoParse, veVideo, myVideoCode;
	for (var veVideoCode in veVideoFormats) {
	  veVideoParser = veVideoCode + '":"(.*?)"';
	  veVideoParse = veVideosContent.match(veVideoParser);
	  veVideo = (veVideoParse) ? veVideoParse[1] : null;
	  if (veVideo) {
	    if (!veVideoFound) veVideoFound = true;
	    myVideoCode = veVideoFormats[veVideoCode];
	    veVideoList[myVideoCode] = cleanMyContent(veVideo, false);
	  }
	}

	if (veVideoFound) {
	  /* Create Player */
	  var veDefaultVideo = 'Low Definition MP4';
	  player = {
	    'playerSocket': vePlayerWindow,
	    'playerWindow': myPlayerWindow,
	    'videoList': veVideoList,
	    'videoDefinitions': ['Low Definition', 'Very Low Definition'],
	    'videoContainers': ['MP4'],
	    'videoPlay': veDefaultVideo,
	    'videoThumb': veVideoThumb,
	    'videoTitle' : veVideoTitle,
	    'playerWidth': 640,
	    'playerHeight': 390
	  };
	  feature['container'] = false;
	  feature['widesize'] = false;
	  createMyPlayer();
	}
	else {
	  var ytVideoId = getMyContent(page.url, 'youtube.com/embed/(.*?)("|\\?)', false);
	  if (!ytVideoId) ytVideoId = getMyContent(page.url, '"videoId":"yapi-(.*?)"', false);
	  if (ytVideoId) {
	    var ytVideoLink = 'http://youtube.com/watch?v=' + ytVideoId;
	    showMyMessage('embed', ytVideoLink);
	  }
	  else {
	    showMyMessage('!videos');
	  }
	}
      }
      else {
	showMyMessage('!content');
      }
    }

    }, 1000);

  }

  // =====Viki===== //

  else if (page.url.indexOf('viki.com/videos') != -1) {

    /* Get Player Window */
    var vkPlayerWindow = getMyElement('', 'div', 'class', 'video-column', 0, false);
    if (!vkPlayerWindow) {
      showMyMessage('!player');
    }
    else {
      /* Get Video Thumbnail */
      var vkVideoThumb = getMyContent(page.url, 'meta\\s+property="og:image"\\s+content="(.*?)"', false);
      if (vkVideoThumb) vkVideoThumb = vkVideoThumb.replace(/&amp;/g, '&');

      /* Get Video Title */
      var vkVideoTitle = getMyContent(page.url, 'meta\\s+property="og:title"\\s+content="(.*?)"', false);
      if (vkVideoTitle) vkVideoTitle = cleanMyContent(vkVideoTitle, false, true);

      /* Get Video ID */
      var vkVideoID = page.url.match(/videos\/(\d+v)/);
      vkVideoID = (vkVideoID) ? vkVideoID[1] : null;

      /* Get Videos Content */
      var vkVideosContent;
      if (vkVideoID) {
	/* SHA-1
	Copyright 2008-2018 Brian Turek, 1998-2009 Paul Johnston & Contributors
	Distributed under the BSD License
	See https://caligatio.github.com/jsSHA/ for more information
	*/
	var SHA1FuncBody;
	var SHA1Key = 'sha1js';
	try {
	  if (localStorage.getItem(SHA1Key)) {
	    SHA1FuncBody = localStorage.getItem(SHA1Key);
	  }
	  else throw false;
	}
	catch(e) {
	  SHA1FuncBody = getMyContent('https://raw.githack.com/Caligatio/jsSHA/master/src/sha1.js', 'TEXT', false);
	  localStorage.setItem(SHA1Key, SHA1FuncBody);
	}
	var SHA1Func = new Function('a', SHA1FuncBody);
	var SHA1 = new SHA1Func();
	if (SHA1.jsSHA) {
	  var shaObj = new SHA1.jsSHA("SHA-1", "TEXT");
	  var vkTimestamp = parseInt(Date.now() / 1000);
	  var vkQuery = "/v5/videos/" + vkVideoID + "/streams.json?app=100005a&t=" + vkTimestamp + "&site=www.viki.com"
	  var vkToken = "MM_d*yP@`&1@]@!AVrXf_o-HVEnoTnm$O-ti4[G~$JDI/Dc-&piU&z&5.;:}95\=Iad";
	  shaObj.setHMACKey(vkToken, "TEXT");
	  shaObj.update(vkQuery);
	  var vkSig = shaObj.getHMAC("HEX");
	  var vkSource = "https://api.viki.io" + vkQuery + "&sig=" + vkSig;
	  vkVideosContent = getMyContent(vkSource, 'TEXT', false);
	}
      }

      /* Player Size */
      var vkPlayerWidth, vkPlayerHeight;
      function vkGetSizes() {
	vkPlayerWidth = vkPlayerWindow.clientWidth - 17;
	vkPlayerHeight = Math.ceil(vkPlayerWidth / (16 / 9)) + myPlayerPanelHeight;
      }
      function vkUpdateSizes() {
	vkGetSizes();
	player['playerWidth'] = vkPlayerWidth;
	player['playerHeight'] = vkPlayerHeight;
	resizeMyPlayer('widesize');
      }
      vkGetSizes();

      /* My Player Window */
      myPlayerWindow = createMyElement('div');
      styleMyElement(myPlayerWindow, {position: 'relative', width: vkPlayerWidth + 'px', height: vkPlayerHeight + 'px', textAlign: 'center'});
      cleanMyElement(vkPlayerWindow, true);
      styleMyElement(vkPlayerWindow, {marginBottom: '10px'});
      appendMyElement(vkPlayerWindow, myPlayerWindow);
      blockObject = vkPlayerWindow;

      /* Resize Event */
      page.win.addEventListener('resize', vkUpdateSizes, false);

      /* Get Videos */
      if (vkVideosContent) {
	var vkVideoList = {};
	var vkVideoFormats = {'1080p': 'Full High Definition MP4', '720p': 'High Definition MP4', '480p': 'Standard Definition MP4',
			       '360p': 'Low Definition MP4', '240p': 'Very Low Definition MP4'};
	var vkVideoFound = false;
	var vkVideoParser, vkVideoParse, vkVideo, myVideoCode;
	for (var vkVideoCode in vkVideoFormats) {
	  vkVideoParser = '"' + vkVideoCode + '".*?"https":\{"url":"(.*?)"';
	  vkVideoParse = vkVideosContent.match(vkVideoParser);
	  vkVideo = (vkVideoParse) ? vkVideoParse[1] : null;
	  if (vkVideo) {
	    if (!vkVideoFound) vkVideoFound = true;
	    myVideoCode = vkVideoFormats[vkVideoCode];
	    vkVideoList[myVideoCode] = vkVideo;
	  }
	}

	// Unauthorized
	var vkUnauthorized = (vkVideosContent.indexOf('unauthorized') != -1) ? true : false;

	// DASH/HLS
	vkVideosContent = getMyContent(page.url.replace('/videos/', '/player5_fragment/'), 'TEXT', false);
	if (vkVideosContent) {
	  vkVideoEncDASH = vkVideosContent.match(/dash\+xml".*?stream=(.*?)"/);
	  vkVideoEncDASH = (vkVideoEncDASH) ? vkVideoEncDASH[1] : null;
	  vkVideoEncHLS = vkVideosContent.match(/x-mpegURL".*?stream=(.*?)"/);
	  vkVideoEncHLS = (vkVideoEncHLS) ? vkVideoEncHLS[1] : null;
	  if (vkVideoEncDASH || vkVideoEncHLS) {
	    vkVideoEncKey = vkVideosContent.match(/chabi:\s*'(.*?)'/);
	    vkVideoEncKey = (vkVideoEncKey) ? vkVideoEncKey[1] : null;
	    vkVideoEncIV = vkVideosContent.match(/ecta:\s*'(.*?)'/);
	    vkVideoEncIV = (vkVideoEncIV) ? vkVideoEncIV[1] : null;
	    if (vkVideoEncKey && vkVideoEncIV) {
	      /* AES
	      Copyright 2015-2018 Richard Moore
	      MIT License.
	      See https://github.com/ricmoo/aes-js/ for more information
	      */
	      var AESFuncBody;
	      var AESKey = 'aesjs';
	      try {
		if (localStorage.getItem(AESKey)) {
		  AESFuncBody = localStorage.getItem(AESKey);
		}
		else throw false;
	      }
	      catch(e) {
		AESFuncBody = getMyContent('https://raw.githack.com/ricmoo/aes-js/master/index.js', 'TEXT', false);
		localStorage.setItem(AESKey, AESFuncBody);
	      }
	      var AESFunc = new Function('a', AESFuncBody);
	      var AES = new AESFunc();
	      var AESKey = AES.aesjs.utils.utf8.toBytes(vkVideoEncKey);
	      var AESIV = AES.aesjs.utils.utf8.toBytes(vkVideoEncIV);
	      var encryptedBytes, decryptedBytes;
	      // HLS
	      encryptedBytes = AES.aesjs.utils.hex.toBytes(vkVideoEncHLS);
	      AESCBC = new AES.aesjs.ModeOfOperation.cbc(AESKey, AESIV);
	      decryptedBytes = AESCBC.decrypt(encryptedBytes);
	      var vkHLSManifest = AES.aesjs.utils.utf8.fromBytes(decryptedBytes);
	      if (vkHLSManifest) {
		if (!vkVideoFound) vkVideoFound = true;
		vkVideoList['Any Definition M3U8'] = vkHLSManifest;
	      }
	      // DASH
	      encryptedBytes = AES.aesjs.utils.hex.toBytes(vkVideoEncDASH);
	      AESCBC = new AES.aesjs.ModeOfOperation.cbc(AESKey, AESIV);
	      decryptedBytes = AESCBC.decrypt(encryptedBytes);
	      var vkDASHManifest = AES.aesjs.utils.utf8.fromBytes(decryptedBytes);
	      if (vkDASHManifest) {
		var vkDASHDomain = vkDASHManifest.split('/').splice(0, 5).join('/');
		var vkDASHContent = getMyContent(vkDASHManifest, 'TEXT', false);
		if (vkDASHContent) {
		  var vkDASHVideo;
		  var vkDASHVideos = vkDASHContent.match(new RegExp('<BaseURL>.*?</BaseURL>', 'g'));
		  if (vkDASHVideos) {
		    for (var i = 0; i < vkDASHVideos.length; i++) {
		      vkDASHVideo = vkDASHVideos[i].replace('<BaseURL>', '').replace('</BaseURL>', '');
		      if (vkDASHVideo.indexOf('http') != 0) vkDASHVideo = vkDASHDomain + '/' + vkDASHVideo;
		      for (var vkVideoCode in vkVideoFormats) {
			if (vkDASHVideo.indexOf(vkVideoCode) != -1) {
			  myVideoCode = vkVideoFormats[vkVideoCode];
			  if (vkDASHVideo.indexOf('track1') != -1) {
			    if (!vkVideoFound) vkVideoFound = true;
			    if (!vkVideoList[myVideoCode]) {
			      vkVideoList[myVideoCode.replace('MP4', 'Video MP4')] = vkDASHVideo;
			    }
			  }
			  if (vkDASHVideo.indexOf('track2') != -1) {
			    if (!vkVideoList[myVideoCode]) {
			      vkVideoList[myVideoCode.replace('MP4', 'Audio MP4')] = vkDASHVideo;
			    }
			  }
			}
		      }
		    }
		  }
		  if (option['dash']) {
		    for (var vkVideoCode in vkVideoFormats) {
		      myVideoCode = vkVideoFormats[vkVideoCode];
		      if (!vkVideoList[myVideoCode]) {
			if (vkVideoList[myVideoCode.replace('MP4', 'Video MP4')] && vkVideoList[myVideoCode.replace('MP4', 'Audio MP4')]) {
			  vkVideoList[myVideoCode] = 'DASH';
			}
		      }
		    }
		  }
		}
	      }
	    }
	  }
	}

	/* Create Player */
	if (vkVideoFound) {
	  var vkDefaultVideo = 'Low Definition MP4';
	  player = {
	    'playerSocket': vkPlayerWindow,
	    'playerWindow': myPlayerWindow,
	    'videoList': vkVideoList,
	    'videoDefinitions': ['Full High Definition', 'High Definition', 'Standard Definition', 'Low Definition', 'Very Low Definition'],
	    'videoContainers': ['MP4', 'M3U8', 'Any'],
	    'videoPlay': vkDefaultVideo,
	    'videoThumb': vkVideoThumb,
	    'videoTitle' : vkVideoTitle,
	    'playerWidth': vkPlayerWidth,
	    'playerHeight': vkPlayerHeight
	  };
	  feature['widesize'] = false;
	  feature['dash'] = true;
	  createMyPlayer();
	  vkUpdateSizes();
	}
	else {
	  if (vkUnauthorized) showMyMessage('other', 'Authorization required!');
	  else showMyMessage('!videos');
	}
      }
      else {
	showMyMessage('!content');
      }
    }

  }

  // =====IMDB===== //

  else if (page.url.indexOf('imdb.com') != -1) {

    /* Redirect To Video Page */
    if (page.url.indexOf('/video/') == -1 && page.url.indexOf('/videoplayer/') == -1) {
      page.doc.addEventListener('click', function(e) {
	var p = e.target.parentNode;
	while (p) {
	  if (p.tagName === 'A' && p.href.indexOf('/video/imdb') != -1) {
	    page.win.location.href = p.href.replace(/imdb\/inline.*/, '');
	  }
	  p = p.parentNode;
	}
      }, false);
      return;
    }

    /* Player Sizea */
    var imdbPlayerWidth, imdbPlayerHeight;
    function imdbSizes() {
      if (imdbPlayerWindow) imdbPlayerWidth = imdbPlayerWindow.clientWidth;
      if (imdbPlayerWidth) imdbPlayerHeight = Math.ceil(imdbPlayerWidth / (16 / 9)) + myPlayerPanelHeight;
    }

    /* Resize Event */
    page.win.addEventListener('resize', function() {
      imdbSizes();
      player['playerWidth'] = imdbPlayerWidth;
      player['playerHeight'] = imdbPlayerHeight;
      resizeMyPlayer('widesize');
    }, false);

    /* My Player Window */
    myPlayerWindow = createMyElement('div');

    /* Get Objects */
    var imdbVideosReady = false;
    var imdbPlayerWindow, imdbVideoWindow;
    var imdbWaitForLoops = 50;
    var imdbWaitForObject = page.win.setInterval(function() {
      if (!imdbPlayerWindow) {
	imdbPlayerWindow = getMyElement('', 'div', 'class', 'video-player__video-container', 0, false);
	if (!imdbPlayerWindow) imdbPlayerWindow = getMyElement('', 'div', 'id', 'video-container', -1, false);
	if (imdbPlayerWindow) {
	  cleanMyElement(imdbPlayerWindow, true);
	  appendMyElement(imdbPlayerWindow, myPlayerWindow);
	  blockObject = imdbPlayerWindow;
	  imdbSizes();
	  styleMyElement(myPlayerWindow, {width: imdbPlayerWidth + 'px', height: imdbPlayerHeight + 'px', textAlign: 'center'});
	  if (imdbVideosReady) imdbPlayer();
	}
      }
      imdbVideoWindow = getMyElement('', 'div', 'class', 'video-player__video', 0, false);
      if (imdbVideoWindow) styleMyElement(imdbVideoWindow, {display: 'none'});
      ytWaitForLoops--;
      if (ytWaitForLoops == 0) {
	if (!imdbPlayerWindow) showMyMessage('!player');
	clearInterval(imdbWaitForObject);
      }
    }, 500);
    intervals.push(imdbWaitForObject);

    /* Create Player */
    var imdbDefaultVideo = 'Low Definition MP4';
    function imdbPlayer() {
      player = {
	'playerSocket': imdbPlayerWindow,
	'playerWindow': myPlayerWindow,
	'videoList': imdbVideoList,
	'videoDefinitions': ['Full High Definition', 'High Definition', 'Standard Definition', 'Low Definition', 'Very Low Definition'],
	'videoContainers': ['MP4'],
	'videoPlay': imdbDefaultVideo,
	'videoThumb': imdbVideoThumb,
	'videoTitle' : imdbVideoTitle,
	'playerWidth': imdbPlayerWidth,
	'playerHeight': imdbPlayerHeight
      };
      feature['container'] = false;
      feature['widesize'] = false;
      createMyPlayer();
    }

    /* Get Video Thumbnail */
    var imdbVideoThumb;

    /* Get Video Title */
    var imdbVideoTitle = getMyContent(page.url, 'meta\\s+property="og:title"\\s+content="(.*?)"', false);
    if (imdbVideoTitle) imdbVideoTitle = cleanMyContent(imdbVideoTitle, false, true);

    /* Get Video Id */
    var imdbVideoId = page.url.replace(/.*videoplayer\//, '').replace(/(\/|\?).*/, '');

    /* Get Videos Content */
    var imdbVideosContent = getMyContent(page.url, '"' + imdbVideoId + '":\\{("aggregateUpVotes.*?videoId)', false);

    /* Get Videos */
    var imdbVideoList = {};
    if (imdbVideosContent) {
      var imdbVideoFormats = {'1080p': 'Full High Definition MP4', '720p': 'High Definition MP4', '480p': 'Standard Definition MP4',
			       '360p': 'Low Definition MP4', 'SD': 'Low Definition MP4', '240p': 'Very Low Definition MP4'};
      var imdbVideoFound = false;
      var imdbVideoParser, imdbVideoParse, myVideoCode, imdbVideo;
      for (var imdbVideoCode in imdbVideoFormats) {
	imdbVideoParser = '"definition":"' + imdbVideoCode + '".*?"videoUrl":"(.*?)"';
	imdbVideoParse = imdbVideosContent.match(imdbVideoParser);
	imdbVideo = (imdbVideoParse) ? imdbVideoParse[1] : null;
	if (imdbVideo) {
	  imdbVideo = cleanMyContent(imdbVideo, false);
	  if (!imdbVideoFound) imdbVideoFound = true;
	  myVideoCode = imdbVideoFormats[imdbVideoCode];
	  if (!imdbVideoList[myVideoCode]) imdbVideoList[myVideoCode] = imdbVideo;
	}
      }

      if (imdbVideoFound) {
	imdbVideosReady = true;
	imdbVideoThumb = imdbVideosContent.match(/"slate".*?"url":"(.*?)"/);
	imdbVideoThumb = (imdbVideoThumb) ? imdbVideoThumb[1] : null;
	if (imdbVideoThumb) imdbVideoThumb = cleanMyContent(imdbVideoThumb, false);
	if (imdbPlayerWindow) imdbPlayer();
      }
      else {
	showMyMessage('!videos');
      }
    }
    else {
      imdbVideo = getMyContent(page.url, '"videoUrl":"(.*?)"', false);
      if (imdbVideo) {
	imdbVideo = cleanMyContent(imdbVideo, false);
	imdbVideoList[imdbDefaultVideo] = imdbVideo;
	imdbVideosReady = true;
	imdbVideoThumb = getMyContent(page.url, '"slate":"(.*?)"', false);
	if (imdbVideoThumb) imdbVideoThumb = cleanMyContent(imdbVideoThumb, false);
	if (imdbPlayerWindow) imdbPlayer();
      }
      else {
	showMyMessage('!content');
      }
    }

  }

}


// ==========Run========== //

getMyOptions();
ViewTube();

page.win.setInterval(function() {
  if (page.url != page.win.location.href) {
    for (var i = 0; i < intervals.length; i++){
      clearInterval(intervals[i]);
    }
    intervals = [];
    if (player['playerWindow'] && player['playerWindow'].parentNode) {
      removeMyElement(player['playerWindow'].parentNode, player['playerWindow']);
    }
    page.doc = page.win.document;
    page.body = page.doc.body;
    page.url = page.win.location.href;
    blockInterval = 50;
    if (player['playerSocket']) blockObject = player['playerSocket'];
    blockVideos();
    ViewTube();
  }
  // Block videos
  if (blockObject && blockInterval > 0) {
    blockVideos();
    if (blockInterval > 0) blockInterval--;
  }
}, 500);


})();

