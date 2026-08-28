# Licensing notice for this fork

This is a fork of [WebOne](https://github.com/atauenis/webone), which is licensed under a
permissive BSD-3-Clause-style license (see `LICENSE.txt`).

This fork ports WebOne's TLS layer from OpenSSL to [wolfSSL](https://www.wolfssl.com/), and in
doing so incorporates wolfSSL's official C# wrapper (`wolfSSL_CSharp`, under `WolfSSL/` --
see `WolfSSL/README.md` for exactly what was copied and modified) directly into the source tree.
That wrapper is **GPLv3-licensed** by wolfSSL Inc. (dual-licensed: GPLv3, or a commercial license
available from wolfSSL Inc. -- there is no permissive option). The full GPLv3 text is included at
`WolfSSL/COPYING`.

**Practical effect:** because this fork statically incorporates GPLv3-licensed source rather than
merely linking against a separately-distributed wolfSSL library at runtime, GPLv3's copyleft
terms mean **the combined work -- this fork, as distributed -- is effectively governed by GPLv3**,
not by WebOne's original BSD-style license alone. If you redistribute this fork (source or
binary), treat it as GPLv3-licensed: provide corresponding source, preserve copyright/license
notices, and license any derivative works you distribute under GPLv3-compatible terms.

Upstream WebOne itself (without this fork's wolfSSL changes) remains under its original BSD-style
license -- this notice applies only to this fork, because of the wolfSSL wrapper it adds.

If GPLv3 isn't workable for your use case, wolfSSL Inc. offers a commercial license for
`wolfSSL_CSharp` (contact support@wolfssl.com) that would let a fork remain under WebOne's
original permissive terms instead.
