# WebOne
This is a HTTP 1.x proxy server that makes old web browsers, media players and messengers usable again in the Web 2.0 world.

> **This fork** ports the TLS layer from OpenSSL to [wolfSSL](https://www.wolfssl.com/), for
> better legacy protocol/cipher support (SSLv3, RC4, static RSA, etc.) via an actively-maintained,
> CVE-patched library. See `WolfSSL/README.md` for how it's wired in, and **`NOTICE.md` for a
> licensing consideration this introduces** (the wolfSSL C# wrapper is GPLv3-licensed, unlike
> upstream WebOne's BSD-style license) before you redistribute this fork.

![](https://raw.githubusercontent.com/airi-kozume/webone/master/docs/Demo.png)

The proxy is an adapter between the modern Web and old software. It is designed to run on an modern PC in same network with older computers.

WebOne HTTP Proxy Server is working by default on port 8080 and is compatible even with Netscape Navigator 3. Set IP address or hostname of PC with WebOne as HTTP/HTTPS/FTP proxy server (or set http://proxyhost:port/auto.pac as Automatic proxy configuration URL) in old browser's settings and begin WWW surfing again. There also is alternative mode (http://proxyhost:port/http://domain/filename.ext) for browsers that cannot work with proxies.

The program's settings are in the __webone.conf__ file (but any other file name can be used too).

See **[WebOne wiki](https://github.com/atauenis/webone/wiki)** for complete list of features and full documentation.

## Server prerequisites
Windows 8+ (or Windows Server 2012+) / Linux / macOS and .NET 8.0 Runtime are required on server PC. See [.NET 8.0 System Requirements](https://github.com/dotnet/core/blob/main/release-notes/8.0/supported-os.md).

**This fork additionally requires wolfSSL's native library** (`libwolfssl.so` on Linux,
`wolfssl.dylib` on macOS, `wolfssl.dll` on Windows) to be present and loadable wherever WebOne
runs -- it's how the client-facing TLS layer works now (see `WolfSSL/README.md`), and it is
**not** built or bundled automatically by `build.sh`/`build.bat`. Only **Linux has actually been
built and tested** with this fork's wolfSSL changes (see `WolfSSL/README.md` for the exact
configure flags used); the Windows/macOS platform claim above is inherited from upstream WebOne
and has **not** been re-verified for this fork's wolfSSL-backed TLS layer specifically. If you get
it working on Windows or macOS, a PR documenting the steps would help everyone.

## Image and video converting
* Picture format converting is performing via `convert` utility from ImageMagick (bundled with WebOne).
* To watch YouTube.com videos through proxy, install `ffmpeg` together with `yt-dlp` (included in `win-x64.full` zips, and can be installed manually on Linux/macOS).

## Install
Manuals about how to set up a WebOne proxy on [Windows](https://github.com/atauenis/webone/wiki/Windows-installation) / [Linux](https://github.com/atauenis/webone/wiki/Linux-installation) / [macOS](https://github.com/atauenis/webone/wiki/MacOS-X-installation) servers are in the Wiki.

## Run
*	On Windows simply run `webone.exe`. Then open port 8080 in Windows Firewall settings.

*	On Linux the proxy is installing as a service, so it can be configured via regular service management commands:
	```
	$ sudo service webone start
	or
	$ sudo systemctl start webone
	```
	Other service commands, such as `start`/`stop`/`restart`/`status`/`enable`/`disable`, also work.
	
*   On macOS launch `webone` from Terminal, as the application is not signed for developer verification.
	```
	$ ./webone				(simply)
	$ ./webone 5170				(start on specific port, e.g. 5170)
	$ sudo ./webone 80			(if port is less than 1024, root rights are need)
	$ ./webone /some/folder/myconfig.conf	(to use specific configuration file instead of default)
	$ dotnet webone.dll			(alternative way, armv6 only)
	```
	These commands also can be used on Linux when systemd service is disabled.

*	*Tip:* you may store your own configuration in `/etc/webone.conf.d/` directory. It will override `webone.conf` settings and will not be overwritten on package updates.



Working of WebOne can be checked via web browser by opening http://proxyhost:port/.

Note that this app is not intended for daily use, as removing any encryption from web traffic and use of really old and unsupported browser may cause security problems.

The server can be started even on public hosts. But don't forget to enable password protection in config file to make the proxy non-public.

## Build
Latest source code for this fork is on the __master__ branch of [this Git repository](https://github.com/airi-kozume/webone). Upstream WebOne (without the wolfSSL changes) is tracked separately at [atauenis/webone](https://github.com/atauenis/webone) (![](https://img.shields.io/github/v/tag/atauenis/webone?include_prereleases&label=)), which also has a __dev__ branch this fork does not currently mirror.

The program is built using Microsoft .NET 8.0 SDK and [dotnet-packaging](https://github.com/qmfrederik/dotnet-packaging/) add-on. With them the building is easy on all platforms: use `dotnet publish` & `dotnet deb || dotnet rpm || dotnet zip` tools.

Windows developers can utilize `build.bat` script for cross-platform building. And there is similar `build.sh` script for Linux and macOS environments.

## Feedback
For issues specific to this fork's wolfSSL changes, use this repository's own [Issues](https://github.com/airi-kozume/webone/issues) tab. For general WebOne questions, the official [VOGONS thread](https://www.vogons.org/viewtopic.php?f=24&t=67165), [phantom.sannata.ru thread](https://phantom.sannata.org/viewtopic.php?f=16&t=33291), and upstream's GitHub [Discussions](https://github.com/atauenis/webone/discussions) tab are the right places.

## Who are the author(s)?
Currently the project is maintained mostly by a single person, Alexander Tauenis (ATauenis). Also thanks to [contributors](https://github.com/atauenis/webone/graphs/contributors) who made pull requests and bug reports.

## How to help the project
WebOne project welcomes any help in the development. Forks, Pull Requests, and Bug Reports are welcome.

Even if you don't know C#, but have skills on HTML/CSS/JS development, it's possible to help with [creating web traffic edits](https://github.com/atauenis/webone/wiki/Sets-of-edits).
