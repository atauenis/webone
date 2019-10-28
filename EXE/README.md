# WebOne
This is a HTTP 1.x proxy that makes old web browsers usable again in the Web 2.0 world.

The proxy should be ran on a modern PC with .NET Framework 4.6 (or newer).
Probably should work in Mono too. Administrator privileges  are required.

WebOne HTTP Proxy server is working by default on port 80 and compatible even with Netscape Navigator 3. Set IP or hostname of the PC with WebOne as HTTP Proxy Server in old browser's settings and begin WWW surfing again. There also a local mode (http://proxyhost:port/http://domain/filename.ext) for browsers that cannot work with proxies. Also you can enter http://proxyhost:port/auto.pac as Automatic proxy configuration URL, and the elderly browser will configure itself for WebOne automatically.

The program's settings are in the __webone.conf__ file. To use another file start the program as "webone.exe _config_file_name.ext_". To start the proxy on a port other than set in config file, set a shortcut to launch "webone.exe _PORT_" where PORT is a number from 1 to 65535.

Compiled stable build for Windows 7 SP1+ is in the __EXE__ folder of this repository. It's still compatible with WinXP/2003, but I'm recommend to use it with a modern system to prevent TLS problems.

Note that this app is not intended for daily use, as removing any encryption from web traffic and use of really old and unsupported browser may cause security problems.

The server can be started even on public hosts. But don't forget to enable password protection in config file to make the proxy non-public.

See [WebOne wiki](https://github.com/atauenis/webone/wiki) for complete list of features and full documentation.

### Описание по-русски
__WebOne__ - прокси-сервер HTTP, позволяющий открывать современные сайты на старых браузерах. Он снимает шифрование HTTPS и меняет кодировку с UTF-8 на указанную в настройках (например, на ANSI). Также прокси умеет понижать версии js-фреймворков настолько, насколько это возможно простой заменой оных на более старые. Доступны функции по замене участков контента (например, тяжёлых скриптов) на более удобоваримые старыми браузерами и перенаправлению битых ссылок на архивные копии. Для совсем старинных браузеров имеется конвертор png/webp графики в gif. Им же можно сжимать слишком тяжёлые jpeg картинки.

Этот прокси-сервер необходимо запускать на современном ПК с .NET Framework 4.6, и указать IP адрес компьютера-прокси в настройках устаревшего веб-обозревателя. Порт по умолчанию 80, тип прокси HTTP 1.0 (или же можно указать путь к скрипту автоматической настройки: http://proxyhost:port/auto.pac). Для запуска необходимы права администратора.

Настройки прокси-сервера хранятся в файле __webone.conf__ в каталоге с программой. Для использования другого файла, запускайте прокси с параметром "webone.exe _config_file_name.ext_".

В папке __EXE__ репозитория размещены готовые сборки для Windows 7 SP1+. Теоретически, они должны работать и в Mono (Linux). Сервер также может запускаться и с .NET 4.0 (Windows XP/2003), но тогда будут те же ограничения по работе с SSL/TLS, что и у устаревшего фреймворка. В случае ошибок TLS возможно отображение ошибки "_Неожиданный EOF или 0 байт из транспортного потока_" или _SecureChannelFailure "Не удалось создать защищённый канал"_. Поэтому рекомендуется использовать более современную ОС.

Для запуска необходимо минимум 3 файла: webone.exe, webone.conf и convert.exe. Также можно сохранить файл logo.webp для проверки ImageMagick.

Проект открыт для желающих присоединиться к разработке.

Подробная информация (на английском) в [wiki проекта](https://github.com/atauenis/webone/wiki).