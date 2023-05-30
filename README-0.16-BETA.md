# v0.16 Beta Release Notes

This is a pre-release of WebOne Proxy Server, version 0.16 Beta 1. It is half-completed work, containing preview of new features and may be not enough stable.

WebOne 0.16's major new feature is support for HTTPS requests through proxy. Implementing the HTTPS support required also to rewrite from scratch most of code, related to regular HTTP/1.1. Previous WebOne versions (including 0.15.3) used Microsoft's library for decoding (and coding back) HTTP traffic. It have many limitations, which appear as "400 Bad Request" error pages without any prints in WebOne log.

Examples of errors: https://github.com/atauenis/webone/issues/82, https://github.com/atauenis/webone/issues/88, https://github.com/atauenis/webone/issues/90.

New low-level & high-level HTTP/HTTPS processing code is written from zero, so currently may work not as expected. Original HTTP/1.1 protocol (RFC 2616) is 176 page long, and not all aspects of it is currently implemented correctly. In addition to HTTP & HTTPS, it also supports "CERN-compatible" FTP proxying. So you can open http://, https:// and ftp:// URLs via Web browser.

**Only in this Beta version, WebOne runs on two ports in parallel.** It's made for test & debug purposes.

- The main port, 8080, is a slightly modified version of original HTTP Proxy. It still use Microsoft HTTPAPI, and works almost as before. But may contain new bugs, due to some modifications of other WebOne parts. **Port 8080 is a regular HTTP-only proxy, and also hosts PAC/WPAD file (just as before). By default, Proxy Automatic Configuration file configures client to this port for HTTP.**
  - Port Number can be changed via `webone.conf/[Server]/Port` or `webone.conf/[Server]/HttpPort`.
- The secondary port, **8081**, is the new HTTP(S) Proxy server. It supports work as **HTTP Proxy, Secure Proxy (only for HTTPS), FTP Proxy**, and also hosts PAC file (same as on 8080) and other WebOne stuff like Retro Online Video Player.
  - Port Number can be changed via `webone.conf/[Server]/HttpsPort`. Cannot be same as HttpPort in this Beta version.

Later WebOne will return to single-port mode.

## Browser configurations

|Protocol|Old |New |Automatic|
|--------|----|----|---------|
|HTTP    |8080|8081|8080     |
|HTTPS   |x   |8081|8081     |
|FTP     |x   |8081|8081     |
|GOPHER  |x   |x   |x        |
|WAIS    |x   |x   |x        |
|NEWS    |x   |x   |x        |
|POP,SMTP|x   |x   |x        |

The automatic configuration settings are most stable and most fast at this moment. But if you want to test WebOne 0.16 Beta fully, try to set Port 8081 for HTTP too.

## Work with HTTPS

To use HTTPS through WebOne 0.16, you need to install Proxy's Root Certificate (aka Certificate Authority). Go to http://proxy:8080/!ca or http://proxy:8081/!ca, download the file to disk, and import the root (CA) certificate to your certificate store.

- Mozilla-based browsers use own storage of Authority (root) certificates. 
  - Firefox: Options -> Network -> Encryption -> View certificates.
  - Mozilla SeaMonkey: Preferences -> Privacy & Security -> Manage certificates.
- Microsoft Internet Explorer use Windows certificates storage. Double click on downloaded `WebOneCA.crt` file, and install to *Trusted Root Certificate Authorities* store. To remove, use `C:\Windows\system32\certmgr.msc` console.
- Apple Safari and Google Chrome are using the system certificate store.
  - On Windows all is identical to MSIE.

Known issue: due to unknown reasons, Windows Certificate Store doesn't accepting WebOne CA certificate unless the OS is Windows XP SP3 (2003/XP64 SP2) with full set of Windows Updates or later. Also Opera 7 doesn't installing it. Firefox 3.6, Mozilla 1.8b - OK. **Help wanted.**

Note that deleting ssl.crt/ssl.key in WebOne installation directory will recreate them, and made imported CA certificates invalid.

You may also specify own PEM Certificate and PEM Private Key instead of automatically created, and they will work as CA for fake SSL certificates used to encrypt HTTPS traffic. Huh, if .NET Runtime accept them.

Work of other protocols over HTTPS proxy (specified by RFC 2616) is not implemented in this Beta version. Probably their support will added later.

## Bug reporting

**Please report any problems experienced with this version.** It's possible via GitHub, VOGONS, Polygon of Ghosts or other possible communication ways.

The most what unknown - memory leaks, perfomance issues, SSL & CA errors. All of this needs a deep test, including comparing with [WebOne 0.15.3](https://github.com/atauenis/webone/releases/tag/v0.15.3).