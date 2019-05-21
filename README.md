# WebOne
This is a HTTP 1.x proxy that makes old web browsers usable again in the Web 2.0 world.

The proxy should be ran on a modern PC with .NET Framework 4.6 (or newer).
Probably should work in Mono too. Older versions of .NET are impossible to work with modern TLS chipers and are obsolete like the clients of this proxy. But you may try to recompile for Win2003 or XP with .NET 4.0.

This HTTP Proxy server is working at port 80 and compatible even with Netscape 3.
Other browsers should also work with WebOne.

### Описание по-русски
__WebOne__ - прокси-сервер HTTP, позволяющий открывать современные сайты на старых браузерах. Он снимает шифрование HTTPS и меняет кодировку с UTF-8 на Win. В будущем планируется введение патчей для контента с целью облегчения отображения динамического содержимого в устаревших браузерах.

Этот прокси-сервер необходимо запускать на современном ПК с .NET Framework 4.6, и указать IP адрес компьютера-прокси в настройках устаревшего веб-обозревателя. Порт по умолчанию 80, тип прокси HTTP 1.0.

В папке __EXE__ репозитория размещены готовые сборки для Windows 7+. Теоретически, они должны работать и в Mono (Linux). Сборка "__WebOne-NET4__" специально предназначена для работы с .NET 4.0 (Windows XP/2003), но она имеет те же ограничения по работе с SSL/TLS, что и устаревший фреймворк. Её использование не рекомендуется, и сообщения о багах с SecureChannel в ней не будут приниматься. Лучше обновитесь до Windows 7 или 10.

Проект открыт для желающих присоединиться к разработке.