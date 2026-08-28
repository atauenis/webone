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

## License

**This directory is GPLv3-licensed**, inherited from wolfSSL's own C# wrapper (dual-licensed
GPLv3 / commercial by wolfSSL Inc. -- see `wolfssl/wrapper/CSharp/wolfSSL_CSharp/../../../COPYING`
in the wolfSSL source tree, or https://www.wolfssl.com/license/). This is **different** from the
rest of this repository, which is under WebOne's own BSD-3-Clause-style license (see the
repository root `LICENSE.txt`). See the repository root `NOTICE.md` for what this means for the
project as a whole.
