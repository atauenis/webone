These files can be used to watch YouTube videos in external video player.
System requirements:
* MS Windows with WSH 5.x (XP SP3 is okay)
* A browser with ViewTube userscript (http://sebaro.pro/viewtube/)
* VLC Media Player (or other streaming video player)
* WebOne proxy server running on another PC with avconv

This script does not needs to WebOne be set as default HTTP proxy server on
client PC.

How to use:
Step 1. Copy these files to C:\ (readme.txt is not need).
Step 2. Run 'viewtube.reg' and import registry entries.
Step 3. Enter IP of proxy PC in 2nd line of VBS file.
Step 4. (Re-)Start browser, open YouTube and select "Protocol" player
        in ViewTube frame. Also choose resolution (SD/HD/FHD/4K).
Step 5. Click "Play" button.
Step 6. VLC Media Player should appear and open the video file via
        WebOne which will convert it via avconv to Windows Media Video format.

Configuration:
- To change path of this script, edit viewtube.reg and re-import it.
- To change player, edit 1st line of VBS.
- To change proxy IP or hostname, edit 2nd line of VBS.
- To change codec or select a custom resolution open VBS and edit
  3rd line of it (&arg=-vcodec%20wmv1%20-acodec%20wmav1%20-f%20asf).
  See AVConv docs for arugments. Note that these arguments must be
  URL-encoded.
- To change path to avconv.exe edit "&util=path/to/avconv" to need, then
  edit proxy's webone.conf [Converters] section and verify that
  it contains a valid row like: "path/to/avconv -i pipe: %ARG1% pipe:"
 
List of available codecs and formats can be get through running avconv.exe
on proxy machine with "-codecs" and "-formats" arguments.

Uninstallation:
Use RegEdit to delete HKEY_CLASSES_ROOT\viewtube or bring it to previous state.