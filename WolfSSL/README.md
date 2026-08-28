# WolfSSL

This directory contains wolfSSL's official C# wrapper (`wolfSSL_CSharp`), copied from
[wolfSSL's own repository](https://github.com/wolfSSL/wolfssl) at `wrapper/CSharp/wolfSSL_CSharp/`,
with a small set of local modifications:

- `wolfSSL.cs`:
  - The native library name (`wolfssl_dll`) changed from the Windows-only `"wolfssl.dll"` to the
    bare name `"wolfssl"`, so .NET's cross-platform native library resolution finds
    `libwolfssl.so` on Linux (and `wolfssl.dylib` on macOS) instead of only working on Windows.
  - `set_fd` widened from `set_fd(IntPtr ssl, Socket fd)` to `set_fd(IntPtr ssl, object fd)` --
    the method body only ever `GCHandle`-pins whatever's passed as an opaque IO context, so this
    lets a `Stream` be used instead of requiring a raw `Socket`.
  - Added `CTX_use_certificate_buffer` / `CTX_use_PrivateKey_buffer` (the upstream wrapper only
    exposes file-based cert/key loading; WebOne generates certificates in memory and never writes
    them to disk).
  - Added `CTX_SetMinVersion` (needed to actually accept SSLv3 ClientHellos through wolfSSL's
    "flexible" `usev23_server()` method -- see `WolfSslServerStream.cs`).
- `X509.cs`, `wolfCrypt.cs`: unmodified, kept for completeness / potential future use.

`WolfSslServerStream.cs` (this directory's one non-wolfSSL-sourced file) is WebOne's own adapter:
a `System.IO.Stream` subclass that performs the TLS server handshake via this wrapper's IO
callbacks, standing in for `System.Net.Security.SslStream` on the client-facing accept path. See
that file's header comment for why: .NET's own OpenSSL-backed `SslStream` can't be pointed at
wolfSSL by simply swapping the backing `.so` -- wolfSSL's OpenSSL compatibility layer is a
*source*-level compatibility shim (recompile against wolfSSL's own headers), not an *ABI*-level
one, so it cannot satisfy .NET's native crypto shim's expectations at the shared-library level.
This wrapper instead calls wolfSSL's native API directly.

## Building wolfSSL

This wrapper needs wolfSSL's native library built and available on the library search path
(`libwolfssl.so` found via `LD_LIBRARY_PATH` or a proper `ldconfig`-registered install path on
Linux; `wolfssl.dylib` on macOS; `wolfssl.dll` on Windows -- see the main `README.md`'s "Server
prerequisites" section for the current state of platform support). **Only Linux has actually been
built and tested against this fork**, using [wolfSSL](https://github.com/wolfSSL/wolfssl)
(tested against tag `v5.9.2`) built from source with:

```sh
./autogen.sh
./configure --enable-opensslextra --enable-opensslall --enable-sslv3 --enable-tlsv10 \
  --enable-arc4 --enable-debug --prefix=/opt/wolfssl \
  CFLAGS="-DWOLFSSL_ALLOW_TLS_SHA1 -DWOLFSSL_STATIC_RSA"
make -j
sudo make install    # or skip and point LD_LIBRARY_PATH at src/.libs/ directly for local testing
```

Rationale for the non-default flags:
- `--enable-sslv3` / `--enable-tlsv10`: off by default in modern wolfSSL; needed for legacy
  client handshakes (SSLv3, TLS1.0). These are separate flags, not implied by each other.
- `--enable-arc4`: RC4 is off by default (unlike other legacy ciphers).
- `WOLFSSL_ALLOW_TLS_SHA1`: needed if your CA/leaf certs are signed with SHA1WithRSA (common for
  older self-issued CAs built for legacy-client compatibility) -- wolfSSL 5.x treats SHA1-with-RSA
  as a last-resort signature type, and chain verification needs this macro or SHA1 support can get
  compiled out depending on old-TLS build state.
- `WOLFSSL_STATIC_RSA`: wolfSSL disables static-key (non-forward-secret) cipher suites by default
  since 3.6.6. Legacy clients often only offer plain RSA key exchange, not DHE/ECDHE.
- `--enable-opensslextra` / `--enable-opensslall`: not strictly required by the native API path
  this wrapper uses, but were part of the tested/verified build and left in; a smaller build using
  wolfSSL's own `wrapper/CSharp/user_settings.h` recipe (`cp wrapper/CSharp/user_settings.h .;
  ./configure --enable-usersettings; make`, per wolfSSL's own `wrapper/CSharp/README.md`) is likely
  sufficient too but was **not** the recipe actually tested here.

Additionally, `webone/WolfSslServerStream.cs` calls `wolfSSL_CTX_SetMinVersion(ctx, WOLFSSL_SSLV3)`
at runtime (not a build flag) to lower wolfSSL's default minimum-version floor so the "flexible"
server method actually accepts SSLv3 ClientHellos rather than rejecting them with a record-layer
version error.

Verified end-to-end against real Windows Vista + IE9 (SChannel): TLS1.0, TLS1.2, and SSLv3 with
`SSL_RSA_WITH_RC4_128_SHA` all handshake successfully with this build. SSLv2 is not and cannot be
supported -- wolfSSL has no SSLv2 implementation at all (not even a configure flag for it).

## License

**This directory is GPLv3-licensed**, inherited from wolfSSL's own C# wrapper (dual-licensed
GPLv3 / commercial by wolfSSL Inc. -- see `wolfssl/wrapper/CSharp/wolfSSL_CSharp/../../../COPYING`
in the wolfSSL source tree, or https://www.wolfssl.com/license/). This is **different** from the
rest of this repository, which is under WebOne's own BSD-3-Clause-style license (see the
repository root `LICENSE.txt`). See the repository root `NOTICE.md` for what this means for the
project as a whole.
