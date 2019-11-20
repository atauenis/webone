exe = "C:\Progra~1\VideoLAN\VLC\vlc.exe" 'Player name
str1 = "http://PROXYHOST/!convert/?url=" 'Enter valid IP or hostname of the proxy
str2 = "&util=avconv&arg=-vcodec%20wmv1%20-acodec%20wmav1%20-f%20asf&type=video/avi"
str = Replace(WScript.Arguments.Item(0), "viewtube:", "")
str = Replace(str, "://", "%3A%2F%2F")
str = Replace(str, "/", "%2F")
str = Replace(str, "?", "%3F")
str = Replace(str, "&", "%26")
str = Replace(str, "=", "%3D")
str = Replace(str, ",", "%2C")
run = exe + " """ + str1 + str + str2 + """"
Set WshShell = WScript.CreateObject("WScript.Shell")
'WshShell.Run "cmd /c ""echo " + run + " > C:\last.log""", 1, True
WshShell.Run run, 1, True
