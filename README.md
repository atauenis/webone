# WebOne
This is a HTTP 1.x proxy server that makes old web browsers and media players usable again in the Web 2.0 world.

![](https://raw.githubusercontent.com/atauenis/webone/master/docs/Demo.png)

The proxy is an adapter between the modern Web and old software. It is designed to run on an modern PC in same network with older computers.

WebOne HTTP Proxy Server is working by default on port 8080 and is compatible even with Netscape Navigator 3. Set IP address or hostname of PC with WebOne as HTTP proxy server (or set http://proxyhost:port/auto.pac as Automatic proxy configuration URL) in old browser's settings and begin WWW surfing again. There also a local mode (http://proxyhost:port/http://domain/filename.ext) for browsers that cannot work with proxies.

The program's settings are in the __webone.conf__ file (but any other file name can be used too).

See [WebOne wiki](https://github.com/atauenis/webone/wiki) for complete list of features and full documentation.

## Server prerequisites
Windows 7 (2008 R2) SP1+ / Linux / macOS and .NET Core 3.1 Runtime are required on server PC. See [.NET Core 3.1 System Requirements](https://github.com/dotnet/core/blob/master/release-notes/3.1/3.1-supported-os.md).

Image and/or video format converting is performing via external utilities: `convert` (from ImageMagick), `ffmpeg`, `youtube-dl`.

## Install
Manuals about how to set up a WebOne proxy on [Windows](https://github.com/atauenis/webone/wiki/Windows-installation) / [Linux](https://github.com/atauenis/webone/wiki/Linux-installation) / [MacOS](https://github.com/atauenis/webone/wiki/MacOS-X-installation) servers are in the Wiki.

## Run
To start proxy on Linux or macOS, enter server's IP address or DNS name in `DefaultHostName` field of __/etc/webone.conf__ file and then run:

```
$ sudo systemctl start webone   if run from installed deb/rpm package as service (default)
or
$ webone                        if run from installed deb/rpm package as program
or
$ dotnet webone.dll             if run from binary archive
```

*Note: if the Port number is set less than 1024 (e.g. 80), `sudo` is need on Linux/macOS.*

*Tip: you may store your own configuration in /etc/webone.conf.d/ directory. It will override webone.conf settings and will not be overwritten on package updates.*

On Windows simply run `webone.exe`. On first launch it will show UAC warning about system settings change - it is normal, as WebOne would configure Windows to allow running proxies without administrator rights. However, you may do this step [manually](https://github.com/atauenis/webone/wiki/Windows-installation#how-to-run-without-admin-privileges) and deny the UAC request.

On macOS use `webone` from zip archive through Terminal (Mac binaries are experimental and not well tested):
```
$ chmod +x webone            <- need only on first run
$ webone
```

Working of WebOne can be checked via web browser by opening http://proxyhost:port/.

Note that this app is not intended for daily use, as removing any encryption from web traffic and use of really old and unsupported browser may cause security problems.

The server can be started even on public hosts. But don't forget to enable password protection in config file to make the proxy non-public.

## Build
Latest source code can be always found in the __master__ branch of [Git repository](https://github.com/atauenis/webone). Forks and pull requests are welcome!

To build packages or archives, install [dotnet-packaging](https://github.com/qmfrederik/dotnet-packaging/) add-on. With it the building is easy: use `dotnet publish` & `dotnet deb || dotnet rpm || dotnet zip` tools.

Windows developers can utilize `Build.bat` script for cross-platform building. "Full" Win7SP1+ builds are made by hand by merging *ReleaseWin32* zip with content of `Win32-full` directory and some magic.

## Who are the author(s)?
Currently the project is maintained by a single person, Alexander Tauenis. However WebOne project welcomes any new contributors. 

## Feedback
Any questions can be written on official [VOGONS thread](https://www.vogons.org/viewtopic.php?f=24&t=67165), [phantom.sannata.ru thread](https://phantom.sannata.org/viewtopic.php?f=16&t=33291), and GitHub [Issues](https://github.com/atauenis/webone/issues) and [Discussions](https://github.com/atauenis/webone/discussions) tabs.

# Описание по-русски
__WebOne__ - прокси-сервер HTTP, позволяющий открывать современные сайты на старых браузерах. Своеобразный переходник между реальным Web 2.0 и историческим Web 1.0. Работает по принципу MITM.

Он имеет следующие функции:
* Снятие шифрования HTTPS и двухстороннее преобразование HTTPS 1.1 <-> HTTP 1.0.
* Замена кодировки в ответах серверов на любую, включая транслит.
* Подмена отдельных файлов (например, новых тяжёлых JS-фреймворков на более старые и лёгкие).
* Корректирование частей текстового трафика (например, патчинг JS или XML/CDF/RSS).
* Конвертация или пережатие графических и видеофайлов "на лету" (используя внешние конвертеры).
* Переадресация с несуществующих адресов на Web Archive.

Этот прокси-сервер необходимо запускать на любом современном ПК с .NET Core 3.1, IP адрес которого указывается в настройках устаревшего веб-обозревателя. Порт по умолчанию 8080, тип прокси HTTP 1.0. Доступен файл автоматической настройки: http://proxyhost:port/auto.pac .

Настройки прокси-сервера хранятся в файле __webone.conf__ или любом другом в одном из следующих мест:

* _Каталог программы._
* /etc/webone.conf
* /etc/webone.conf.d/*.conf
* C:\Users\\_username_\AppData\Roaming\WebOne.conf
* C:\ProgramData\WebOne\\*.conf
* _Указанный в аргументе при запуске через `webone PATH\FILENAME.EXT`._

Файл протокола (__webone.log__) по умолчанию сохраняется по адресу ``/var/log/webone.log`` или `` C:\Users\username\AppData\Roaming\webone.log``.

На Linux используются конвертеры из пакетов __imagemagick__ (convert) и __ffmpeg__. В Windows-версии прилагается конвертер **convert**, а в Full-версии дополнительно имеется **ffmpeg** и **youtube-dl** с вспомогательным скриптом для скачивания видео с YouTube (**yt.bat**).

Проект открыт для всех желающих присоединиться к разработке.

Подробная документация (на английском) в [wiki проекта](https://github.com/atauenis/webone/wiki).