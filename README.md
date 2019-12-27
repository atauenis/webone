# WebOne
This is a HTTP 1.x proxy server that makes old web browsers and media players usable again in the Web 2.0 world.

![](https://raw.githubusercontent.com/atauenis/webone/master/docs/Demo.png)

The proxy is an adapter between the modern Web and old software. It is designed to run on an modern PC in same network with older computers.

WebOne HTTP Proxy Server is working by default on port 80 and is compatible even with Netscape Navigator 3. Set IP address or hostname of PC with WebOne as HTTP proxy server (or set http://proxyhost:port/auto.pac as Automatic proxy configuration URL) in old browser's settings and begin WWW surfing again. There also a local mode (http://proxyhost:port/http://domain/filename.ext) for browsers that cannot work with proxies.

The program's settings are in the __webone.conf__ file (but any other file can be used too).

Stable binaries are in the __EXE__ folder of this repository. Latest source code can be always found in the __master__ branch of [Git repository](https://github.com/atauenis/webone).

Windows 7 (2008 R2) SP1+ and .NET Framework 4.6+ are required on server PC.
Probably should work in Mono too. It also can be run in WinXP/2003/POSready, but these systems have some TLS problems, so it is better to use a newer OS with latest updates.

Administrator privileges are required. Don't forget to allow incoming traffic to Port 80 in system firewall.

To check working of WebOne, open in browser http://proxyhost:port/!/, the server diagnostics page.

Note that this app is not intended for daily use, as removing any encryption from web traffic and use of really old and unsupported browser may cause security problems.

The server can be started even on public hosts. But don't forget to enable password protection in config file to make the proxy non-public.

See [WebOne wiki](https://github.com/atauenis/webone/wiki) for complete list of features and full documentation.

### Описание по-русски
__WebOne__ - прокси-сервер HTTP, позволяющий открывать современные сайты на старых браузерах. Он снимает шифрование HTTPS и меняет кодировку с UTF-8 на указанную в настройках (например, на ANSI). Также прокси умеет понижать версии js-фреймворков настолько, насколько это возможно простой заменой оных на более старые. Доступны функции по замене участков контента (например, тяжёлых скриптов) на более удобоваримые старыми браузерами и перенаправлению битых ссылок на архивные копии. Для совсем старинных браузеров имеется конвертор png/webp графики в gif. Им же можно сжимать слишком тяжёлые jpeg картинки. Также доступна функция конвертирования видео с YouTube в любой удобный кодек.

Этот прокси-сервер необходимо запускать на современном ПК с .NET Framework 4.6, IP адрес которого нужно указать в настройках устаревшего веб-обозревателя. Порт по умолчанию 80, тип прокси HTTP 1.0 (или же можно указать путь к скрипту автоматической настройки: http://proxyhost:port/auto.pac).

Для запуска необходимы права администратора.

Настройки прокси-сервера хранятся в файле __webone.conf__ в каталоге с программой. Для использования другого файла, запускайте прокси с параметром "webone.exe _config_file_name.ext_".

В папке __EXE__ репозитория размещены готовые сборки для Windows 7 SP1+. Теоретически, они должны работать и в Mono (Linux). Сервер также запускается и с .NET 4.0 (Windows XP/2003), но тогда будут те же ограничения по работе с SSL/TLS, что и у устаревшего фреймворка. В случае ошибок TLS возможно отображение ошибки "_Неожиданный EOF или 0 байт из транспортного потока_" или _SecureChannelFailure "Не удалось создать защищённый канал"_. Поэтому рекомендуется использовать более современную ОС.

Для запуска необходимо минимум 3 файла: webone.exe, webone.conf и convert.exe. Также можно сохранить файл logo.webp для проверки ImageMagick. Для конвертации видеофайлов или просмотра YouTube на устаревших ПК в каталог желательно положить ffmpeg.exe, youtube-dl.exe и скрипт yt.bat.

Проект открыт для желающих присоединиться к разработке.

Подробная информация (на английском) в [wiki проекта](https://github.com/atauenis/webone/wiki).