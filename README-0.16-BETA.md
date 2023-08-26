# v0.16 Beta Release Notes

The latest testing release of WebOne, version 0.16 Beta 3, is containing a few new useful features not available in stable release 0.15.3.

WebOne 0.16's major new feature is support for native HTTPS requests through proxy. Also it features "CERN-compatible" FTP support, meaning ability to open ftp:// URLs via Web browser as well as https:// and http://. Other new feature is that WebOne supports connections to non-HTTP(S) servers by software, compatible with HTTPS proxies: mIRC, Total Commander, etc.

## Frequently Asked Questions
### How to use HTTPS over WebOne?
Set **http://proxy:8080/auto.pac** as automatic configuration URL in Web browser's settings. Or set proxy server's IP and **Port `8080`** for **HTTP**, **HTTPS** and **FTP** protocols.

### Which browsers can use HTTPS, and which are limited to HTTP only?
You can access any web server via WebOne using old HTTP protocol. It is still providing access to new sites via HTTP, independing to server settings. But if your browser supports at least SSL 2.0 with 128-bit encryption, you can open `https://` links without removing the `s` in URL. Supported browsers including:
 - Mozilla Firefox, Opera, Netscape 6+, Mozilla Suite, Safari, Google Chrome, MS Internet Explorer 5.5+, MS Edge.
 - Microsoft Internet Explorer 4 and 5.0 are requiring a patch, adding 128-bit "strong" encrypting support.
 - Netscape Navigator 2.x/3.x/4.x must be in "US Only" version with 128-bit SSL support. "Export" versions with 40-bit or 56-bit encryption will not work with WebOne.
   - 128-bit versions have installers called like `n32d408.exe`.
   - 40-bit versions have installers called like `n32e408.exe`.
 - Internet Explorer 2.0, 3.0 at this moment are unsupported.
 - Other software would work. Example is last versions of mIRC client which can connect to IRCS servers over WebOne.

### My browser is displaying a trust failure. What's happen?
It's need to install Proxy's Root Certificate (aka Certificate Authority). Go to **http://proxy:8080/!ca**, download the file to disk, and import the root (CA) certificate to your certificate store. Netscape, Firefox and Mozilla are using own certificate store (available via Preferences dialog box). Other browsers are using system certificate store.

Note that deleting `ssl.crt`/`ssl.key` in WebOne directory will cause program to recreate them, and made previously imported CA certificates invalid.

### What about Gopher, WAIS, SOCKS?
There are no support for GOPHER, WAIS or any versions of SOCKS at this moment.

### How to specify own SSL/TLS certificate?
If you want to use own custom certificate as root for HTTPS traffic encryption, you may specify path to the certificate and its private key in WebOne configuration file. Use `[SecureProxy]` section, keys `SslCertificate` and `SslPrivateKey`. With their default values, WebOne is generating both certificate and private key on first start using options from `SslRootValidAfter` and `SslRootValidBefore` keys. However any custom PEM certificates are accepted.

The current beta version does not allow to specify custom certificates for sites. They are always generating dynamic by WebOne for each site.

### Known limitations
#### Chunks
Currently there is no full support for `Transfer-Encoding: chunked` mode. Implementing chunked transfer support will require a lot of time and hard work. WebOne 0.16 Beta 3 is simply returing content which have unknown length by sending all content and then closing the connection. Some clients (mostly video players) may dislike such traffic. But what present - is what present.

If someone can implement an C#.NET Stream class which will produce HTTP-chunked output - it will be good.

#### MD5 certificates
`SslHashAlgorithm` option of webone.conf is currently working only for CA certificate building. The site certificates are always using SHA256 method. The project needs help to solve this, as main developer is currently unable to produce certificate builder with MD5/SHA1 output.

### Seems, found a bug?
**If you have problems, first try to set `UseMsHttpApi=enable` in webone.conf file.** This will temporary enable some old backend code, which may be better in some cases, and is useful for debugging. And please fill a [bug report](https://github.com/atauenis/webone/issues).

**Please report any problems experienced with this version.** It's possible via [GitHub Issues](https://github.com/atauenis/webone/issues), [VOGONS](https://www.vogons.org/viewtopic.php?f=24&t=67165), [Polygon of Ghosts](https://phantom.sannata.org/viewtopic.php?f=16&t=33291) or other possible communication ways.

The most what unknown - memory leaks, perfomance issues, SSL & CA errors. All of this needs a deep test, including comparing with [WebOne 0.15.3](https://github.com/atauenis/webone/releases/tag/v0.15.3).