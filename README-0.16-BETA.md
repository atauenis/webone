# v0.16 Beta Release Notes

While the latest stable version of WebOne Proxy Server is 0.15.3, the development is still going forward. There are also newer developers builds of next version, 0.16. They are available for testing and debugging. Compiled builds can be found in [Release archive](https://github.com/atauenis/webone/releases). But sometimes a newer build can be obtained from sources from Git repository.

This is a pre-release of WebOne Proxy Server, version 0.16 Beta 2. It is half-completed work, containing preview of new features and may be not enough stable.

WebOne 0.16's major new feature is support for HTTPS requests through proxy. Also it supports "CERN-compatible" FTP support, meaning ability to open ftp:// URLs via Web browser as well as https:// and http://.

 Implementing these features support required also to rewrite from scratch most of code. New low-level & high-level HTTP/HTTPS processing code is written from zero, so currently may work not as expected. Original HTTP/1.1 protocol (RFC 2616) is 176 page long, and not all aspects of it are currently implemented correctly.

**If you have problems, first try to set `UseMsHttpApi=enable` in webone.conf file.** This will temporarily disable new HTTP server code and enable old. And please report to author.

## Browser configurations

|Protocol|Old |New |
|--------|----|----|
|HTTP    |8080|8080|
|HTTPS   |x   |8080|
|FTP     |x   |8080|
|GOPHER  |x   |x   |
|WAIS    |x   |x   |
|NEWS    |x   |x   |
|POP,SMTP|x   |x   |
|SOCKS   |x   |x   |


## Work with HTTPS

To use HTTPS through WebOne 0.16, you need to install Proxy's Root Certificate (aka Certificate Authority). Go to http://proxy:8080/!ca, download the file to disk, and import the root (CA) certificate to your certificate store.

- Mozilla-based browsers use own storage of Authority (root) certificates. 
  - Firefox: Options -> Network -> Encryption -> View certificates.
  - Mozilla SeaMonkey: Preferences -> Privacy & Security -> Manage certificates.
  - Sometimes just a click to `WebOne CA root certificate` link at status page starts certificate import.
- Netscape Navigator 3, 4 is a bit similar to Mozilla.
- Microsoft Internet Explorer use Windows certificates storage. Double click on downloaded `WebOneCA.crt` file, and install to *Trusted Root Certificate Authorities* store. To remove, use `C:\Windows\system32\certmgr.msc` console.
- Apple Safari and Google Chrome are using the system certificate store.
  - On Windows all is identical to MSIE.

**Known issue**: "export" versions of pre-2000 browsers are not supported. It is need to install "U.S. only" versions or a "128-bit update" for browser.

Note that deleting ssl.crt/ssl.key in WebOne installation directory will recreate them, and made imported CA certificates invalid.

You may also specify own PEM Certificate and PEM Private Key instead of automatically created, and they will work as CA for fake SSL certificates used to encrypt HTTPS traffic. Huh, if .NET Runtime accept them.

Work of other protocols over HTTPS proxy (specified by RFC 2616) is not implemented in this Beta version. Probably their support will added later.

## Bug reporting

**Please report any problems experienced with this version.** It's possible via GitHub, VOGONS, Polygon of Ghosts or other possible communication ways.

The most what unknown - memory leaks, perfomance issues, SSL & CA errors. All of this needs a deep test, including comparing with [WebOne 0.15.3](https://github.com/atauenis/webone/releases/tag/v0.15.3).