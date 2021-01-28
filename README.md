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
To start proxy on Linux or macOS, enter server's DNS name or IP in `DefaultHostName` field in __webone.conf__ and then run:

```
$ WebOne                  if run from installed deb/rpm package
or
$ dotnet WebOne.dll       if run from binary archive
```

*Note: if the Port number is set less than 1024 (e.g. 80), `sudo` is need on Linux/macOS.*

On Windows simply run `WebOne.exe`.

On macOS use `WebOne` from zip archive (Mac binaries are experimental).

Working of WebOne can be checked via server diagnostics page at http://proxyhost:port/!/.

Note that this app is not intended for daily use, as removing any encryption from web traffic and use of really old and unsupported browser may cause security problems.

The server can be started even on public hosts. But don't forget to enable password protection in config file to make the proxy non-public.

## Build
Latest source code can be always found in the __master__ branch of [Git repository](https://github.com/atauenis/webone). Forks and pull requests are welcome!

To build packages or archives, install [dotnet-packaging](https://github.com/qmfrederik/dotnet-packaging/) add-on. With it the building is easy: use `dotnet publish` & `dotnet deb || dotnet rpm || dotnet zip` tools.

Windows developers can utilize `Build.bat` script for cross-platform building. "Full" Win7SP1+ builds are made by hand by merging *ReleaseWin32* zip with content of `Win32-full` directory and some magic.

# Описание по-русски
__WebOne__ - прокси-сервер HTTP, позволяющий открывать современные сайты на старых браузерах. Своеобразный переходник между реальным Web 2.0 и историческим Web 1.0. 

Он имеет следующие функции:
* Снятие шифрования HTTPS и двухстороннее преобразование HTTPS 1.1 <-> HTTP 1.0.
* Замена кодировки в ответах серверов.
* Подмена отдельных файлов (например, новых тяжёлых JS-фреймворков на более старые и лёгкие).
* Корректирование частей текстового трафика (например, патчинг JS или XML/CDF/RSS).
* Конвертация или пережатие графических и видеофайлов, в том числе "на лету" (используя внешние конвертеры).
* Переадресация с несуществующих адресов на Web Archive.

Этот прокси-сервер необходимо запускать на современном ПК с .NET Core 3.1, IP адрес которого нужно указать в настройках устаревшего веб-обозревателя. Порт по умолчанию 8080, тип прокси HTTP 1.0 (или же можно указать путь к скрипту автоматической настройки: http://proxyhost:port/auto.pac).

Системы Windows XP/2003 в качестве серверных больше не поддерживаются. Последняя версия прокси для .NET FW 4.0 была WebOne 0.9.3.

Для запуска на порту с номером менее 1024 необходимы права администратора или root.

Настройки прокси-сервера хранятся в файле __webone.conf__ в каталоге с программой или по адресу ``/etc/WebOne/``, ``~/.config/WebOne/``, ``C:\Users\username\AppData\Roaming\WebOne\``, ``C:\ProgramData\WebOne\``. Для использования другого файла, запускайте прокси с параметром "webone.exe _config_file_name.ext_".

Файл протокола (__webone.log__) по умолчанию сохраняется по адресу ``/var/log/webone.log`` или `` C:\Users\username\AppData\Roaming\webone.log``.

На Linux используются конвертеры из пакетов __imagemagick__ (convert) и __ffmpeg__. В Windows-версии прилагается конвертер **convert**, а в Full-версии дополнительно имеется **ffmpeg** и **youtube-dl** с вспомогательным скриптом для скачивания видео с YouTube (**yt.bat**).

Проект открыт для всех желающих присоединиться к разработке.

Подробная информация (на английском) в [wiki проекта](https://github.com/atauenis/webone/wiki).