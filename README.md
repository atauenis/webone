# WebOne
This is a HTTP 1.x proxy that makes old web browsers usable again in the Web 2.0 world.

The proxy should be ran on a modern PC with .NET Framework 4.6 (or newer).
Probably should work in Mono too.

WebOne HTTP Proxy server is working at port 80 and compatible even with Netscape Navigator 3. Set IP (or hostname) of the PC with WebOne as HTTP 1.0 Proxy Server in old browser's settings and begin WWW surfing again. To use an other port, set a shortcut to launch "webone.exe _PORT_" where PORT is a number from 1 to 65535. There also a local mode (http://proxyhost:port/http://domain/filename.ext) for browsers that cannot work with proxies.

The program's settings are in the webone.conf file.

Ready for use binaries are in the __EXE__ folder. There are also a special build for Win2003 or XP (.NET 4.0) called __WebOne-NET4__. But the old .NET from 2010 is impossible to work with modern TLS ciphers and is obsolete like the clients of this proxy. In such cases a _SecureChannelFailure_ or a "_Received an unexpected EOF or 0 bytes from the transport stream_" error occurs.

Note that this app is not intended for daily use, as removing any encryption from web traffic and use of really old and unsupported browser may cause security problems.

### Описание по-русски
__WebOne__ - прокси-сервер HTTP, позволяющий открывать современные сайты на старых браузерах. Он снимает шифрование HTTPS и меняет кодировку с UTF-8 на Win. В будущем планируется введение патчей для контента с целью облегчения отображения динамического содержимого в устаревших браузерах.

Этот прокси-сервер необходимо запускать на современном ПК с .NET Framework 4.6, и указать IP адрес компьютера-прокси в настройках устаревшего веб-обозревателя. Порт по умолчанию 80, тип прокси HTTP 1.0.

Настройки прокси-сервера хранятся в файле webone.conf в каталоге с программой.

В папке __EXE__ репозитория размещены готовые сборки для Windows 7+. Теоретически, они должны работать и в Mono (Linux). Сборка "__WebOne-NET4__" специально предназначена для работы с .NET 4.0 (Windows XP/2003), но она имеет те же ограничения по работе с SSL/TLS, что и устаревший фреймворк. Её использование не рекомендуется. В случае ошибок TLS возможно отображение ошибки "_Неожиданный EOF или 0 байт из транспортного потока_" или _SecureChannelFailure "Не удалось создать защищённый канал"_.

Проект открыт для желающих присоединиться к разработке.