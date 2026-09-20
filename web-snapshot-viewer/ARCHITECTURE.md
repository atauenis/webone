# Architecture: `--web-snapshot-viewer`

## General flow

```
Browser → Proxy → [has dimensions?]
                    ├─ NO → Return "probe page" (minimal HTML with JS)
                    │        Browser runs JS → redirects with dimensions → Proxy
                    └─ YES → Playwright screenshot → HTML shell page with strip images
```

---

## File structure

```
./web-snapshot-viewer/
├── WebSnapshotViewer.cs      # HTTP router — dispatches all requests to the right handler
├── DimensionProbe.cs         # Generates lightweight HTML to detect viewport w/h
├── ScreenshotEngine.cs       # Playwright wrapper — screenshot, scroll, click
├── SnapshotPage.cs           # Generates the shell page (strip IMGs + scripts)
├── StripManager.cs           # Splits PNG into horizontal strips, hashes, encodes JPEG
├── BlankStrip.cs             # Generates checkerboard placeholder GIF per session
├── ScrollHandler.cs          # Scroll sync JS (inline) + /scroll-pos endpoint
├── ClickHandler.cs           # Click overlay JS + /click endpoint
└── ClientStripManager.cs     # JS helpers: sendCmd(), updateStrip(), reloadPage()
```

---

## Interception point in the proxy

```
HttpTransit → [SnapshotViewerMode?]
                ├─ YES → WebSnapshotViewer.Handle(request)
                └─ NO  → normal proxy flow
```

`Program.cs` flags:
```csharp
public static bool SnapshotViewerMode   = false;  // --web-snapshot-viewer
public static bool SnapshotViewerHeaded = false;  // --snapshot-headed
public static int  JpegQuality          = 85;     // --quality
public static int  StripHeight          = 100;    // --strip-size
public static int  MinThreads           = 1000;   // --set-min-threads
```

Thread pool is raised at startup to avoid starvation (50 tabs × 6 connections = 300 concurrent requests):
```csharp
ThreadPool.SetMinThreads(MinThreads, MinThreads);
```

Browser driver is pre-warmed at startup so the first request doesn't wait:
```csharp
ScreenshotEngine.EnsureContextAsync().GetAwaiter().GetResult();
```

---

## HTTP endpoints (all on `snapshot.webone.internal`)

| Endpoint | Handler | Description |
|---|---|---|
| `GET /snap?url=...&w=...&h=...` | `HandleSnapshot` | Takes screenshot, returns shell page |
| `GET /strip?key=...&i=...&r=...` | `HandleStrip` | Serves a single JPEG strip |
| `GET /blank-strip?key=...` | `HandleBlankStrip` | Serves the placeholder GIF for unloaded strips |
| `GET /click?key=...&x=...&y=...` | `HandleClick` | Forwards click to Playwright, returns strip update JS |
| `GET /scroll-pos?key=...&y=...` | `HandleScrollPos` | Syncs scroll position to Playwright, returns 1×1 GIF |

All other URLs → probe page (JS redirect with viewport dimensions).

---

## Detailed flow

### Step 1 — Probe page

Browser requests any URL → proxy has no dimensions → returns:
```html
<HTML><BODY><SCRIPT>
var t="http://snapshot.webone.internal/snap";
var u=encodeURIComponent(location.href);
location.replace(t+"?url="+u+"&w="+window.innerWidth+"&h="+window.innerHeight);
</SCRIPT></BODY></HTML>
```
Browser runs JS and redirects to `/snap` with its actual viewport dimensions.

### Step 2 — Snapshot

`/snap?url=...&w=...&h=...` → check cache → if miss:
1. Playwright navigates to `url` with viewport `w × h`
2. Takes full-page PNG screenshot
3. `StripManager.CreateStrips()` splits PNG into horizontal strips of `StripHeight` px
4. Each strip is hashed (SHA256 of raw pixels) and encoded as JPEG RGB
5. A checkerboard placeholder GIF is generated for the session (`BlankStrip.Generate`)
6. `StripSet` stored in cache (key = `SHA256(url + width)`, TTL = 5 min)
7. Shell page returned

---

## Strip system

### StripData
```csharp
class StripData {
    byte[]  Hash;      // SHA256 of raw pixels — used to detect changes after clicks
    byte[]  Jpeg;      // JPEG-encoded bytes served directly to browser
    string  Revision;  // Unix timestamp ms — appended to URL to bust browser cache
    int     Height;    // Pixel height of this strip (last strip may be shorter)
}
```

### StripSet
```csharp
class StripSet {
    StripData[] Strips;
    int  ImageWidth, ImageHeight, StripHeight;
    int  ViewportHeight, NumberStripsInViewport;
    int  LastScrollY;
    byte[] BlankStripGif;   // Per-session checkerboard GIF (same width as screenshot)
    DateTime CreatedAt;
}
```

### JPEG encoding
Strips are encoded as **JPEG RGB** (`JpegEncodingColor.Rgb`) to avoid the YCbCr→RGB conversion crash in Safari 1 on Mac OS X 10.3 (`vec_ycc_rgb_convert` AltiVec bug).

---

## Shell page

The shell page is a plain HTML document that:
1. Has CSS `IMG { display:block; width:100% }` so strips fill the full width
2. Contains one `<IMG>` per strip — only the first `NumberStripsInViewport + 2` get a real SRC; the rest get `/blank-strip?key=...` (lazy loading)
3. Has a hidden `<IFRAME NAME="cmd">` used as an AJAX equivalent for Safari 1 — JS navigates it to `/click?...` and the server returns JS that updates strip images
4. Includes the click overlay `<DIV>` that captures all clicks
5. Inlines the scroll script (no separate request needed)

```
<HTML>
  <HEAD>
    <STYLE> IMG { width:100% } </STYLE>
    <SCRIPT> ClientStripManager JS (sendCmd, updateStrip) </SCRIPT>
    <SCRIPT> ScrollHandler JS (_srcs[], _scroll, _sendScroll, _loadStrips) </SCRIPT>
  </HEAD>
  <BODY>
    <IMG id="strip0" src="/strip?key=...&i=0&r=...">   ← real (in viewport)
    <IMG id="strip1" src="/strip?key=...&i=1&r=...">   ← real (in viewport)
    ...
    <IMG id="stripN" src="/blank-strip?key=...">        ← placeholder (lazy)
    <IFRAME NAME="cmd" style="position:absolute;left:-9999px">
    <DIV id="_ov" style="position:absolute;top:0;z-index:9999"> ← click overlay
    <SCRIPT> click overlay JS </SCRIPT>
  </BODY>
</HTML>
```

---

## Scroll system

The inline scroll script runs every 200ms via `setInterval` and also on `window.onscroll` (modern browsers):

```javascript
var _srcs = ['...url for strip 0...', '...url for strip 1...', ...];

function _getScrollY() { return window.pageYOffset || 0; }

function _sendScroll(sy) {
    new Image().src = '/scroll-pos?key=...&y=' + sy + '&t=' + Date.now();
}

function _loadStrips(sy) {
    var first = Math.floor(sy / stripHeight);
    var last  = Math.min(images.length - 1, first + visibleStrips + 2);
    for (var i = first; i <= last; i++)
        document.images[i].src = _srcs[i];
}

function _scroll() {
    var sy = _getScrollY();
    if (sy == _lastY) return;
    _lastY = sy;
    _loadStrips(sy);
    _sendScroll(sy);
}

window.onscroll = _scroll;
setInterval('_scroll()', 200);
```

`/scroll-pos` handler calls `ScreenshotEngine.ScrollTo(key, y)` which runs `window.scrollTo(0, y)` in Playwright via JS evaluation. Returns a 1×1 transparent GIF so the `new Image()` request completes cleanly.

---

## Click system

The click overlay `<DIV>` covers the full page (`position:absolute; z-index:9999`). On click:

```javascript
var pointerX = e.pageX || 0;
var pointerY = e.pageY || 0;
var px = Math.round(pointerX * _sc);   // scale to screenshot pixels
var py = Math.round(pointerY * _sc);
sendCmd('/click?key=...&x=' + px + '&y=' + py);
```

`sendCmd()` navigates the hidden `cmd` iframe to `/click?...`.

Server side (`ClickHandler.Handle`):
1. Calls `ScreenshotEngine.ClickAndScreenshot(key, x, y)`
2. Playwright scrolls to bring `y` into viewport, then `Mouse.ClickAsync(x, viewportY)`
3. Waits for navigation (3s timeout) or settles for JS-only changes
4. Takes new full-page PNG
5. `StripManager.UpdateStrips()` compares hashes — only changed strips get new JPEG + revision
6. Returns a JS page (loaded by the `cmd` iframe) that calls `parent.updateStrip(id, url)` for each changed strip
7. If strip count changed (page height changed) → `parent.reloadPage()` instead

---

## Cache

```csharp
private static readonly Dictionary<string, StripSet> Cache = new();
private static readonly TimeSpan CacheTTL = TimeSpan.FromMinutes(5);
```

Key = `SHA256(url + ":" + width)` as hex string. Height is excluded because full-page screenshots capture all content regardless of viewport height.

---

## Key design decisions

| Decision | Choice | Reason |
|---|---|---|
| Image format | JPEG RGB | Avoids YCbCr→RGB AltiVec crash in Safari 1 on PPC |
| Strip height | 100px default (`--strip-size`) | Balance between number of HTTP requests and RAM per strip |
| Lazy loading | Only first `viewport/stripHeight + 2` strips load | Avoids flooding old browsers with 30+ simultaneous image requests |
| Blank strip | Checkerboard GIF per session | Shows placeholder while strip loads instead of black/white |
| Scroll detection | `setInterval(200)` + `window.onscroll` | Safari 1 has no `onscroll` event; `setInterval` as fallback |
| Scroll sync | `new Image()` fire-and-forget GET | Most compatible async request for Safari 1 (no XHR) |
| Click transport | Hidden `cmd` iframe navigation | Safari 1 compatible AJAX equivalent |
| Thread pool | `SetMinThreads(1000, 1000)` | Prevents starvation with many concurrent strip requests |
| Browser pre-warm | `EnsureContextAsync()` at startup | First request doesn't wait for Playwright to launch |
