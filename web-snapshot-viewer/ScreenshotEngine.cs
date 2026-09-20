using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace WebOne.SnapshotViewer
{
	/// <summary>
	/// Takes full-page PNG screenshots using Playwright/Firefox.
	/// Keeps a single browser context (with animations disabled) and open pages alive.
	/// </summary>
	static class ScreenshotEngine
	{
		private static IPlaywright _playwright;
		private static IBrowser _browser;
		private static IBrowserContext _context;
		private static readonly SemaphoreSlim _browserLock = new SemaphoreSlim(1, 1);

		// CSS injected into every page to disable transitions and animations instantly.
		private const string DisableAnimationsScript =
			"const _s = document.createElement('style');" +
			"_s.textContent = '*, *::before, *::after {" +
				"animation: none !important;" +
				"animation-duration: 0s !important;" +
				"animation-delay: 0s !important;" +
				"transition: none !important;" +
				"transition-duration: 0s !important;" +
				"transition-delay: 0s !important;" +
				"scroll-behavior: auto !important;" +
			"}';" +
			"(document.head || document.documentElement).appendChild(_s);";

		// Active pages kept alive per session key so clicks can be forwarded.
		private static readonly Dictionary<string, IPage> _sessions = new();
		private static readonly SemaphoreSlim _sessionsLock = new SemaphoreSlim(1, 1);

		/// <summary>
		/// Navigates to <paramref name="url"/> at the given viewport size, takes a full-page
		/// PNG screenshot, and keeps the page open for future click interactions.
		/// </summary>
		public static async Task<byte[]> TakeScreenshot(string sessionKey, string url, int width, int height)
		{
			await EnsureContextAsync();
			await CloseSessionAsync(sessionKey);

			var page = await _context.NewPageAsync();
			await page.SetViewportSizeAsync(width, height);

			await page.GotoAsync(url, new PageGotoOptions
			{
				WaitUntil = WaitUntilState.Load,
				Timeout = 30000
			});

			byte[] png = await page.ScreenshotAsync(new PageScreenshotOptions
			{
				FullPage = true,
				Type = ScreenshotType.Png
			});

			await _sessionsLock.WaitAsync();
			try { _sessions[sessionKey] = page; }
			finally { _sessionsLock.Release(); }

			return png;
		}

		/// <summary>
		/// Scrolls the page to the given Y position without taking a screenshot.
		/// </summary>
		public static async Task ScrollTo(string sessionKey, int scrollY)
		{
			IPage page;
			await _sessionsLock.WaitAsync();
			try
			{
				if (!_sessions.TryGetValue(sessionKey, out page)) return;
			}
			finally { _sessionsLock.Release(); }

			await page.EvaluateAsync("(y) => window.scrollTo(0, y)", scrollY);
		}

		/// <summary>
		/// Takes a full-page PNG screenshot of the current page state without navigating.
		/// Returns null if the session is not found.
		/// </summary>
		public static async Task<byte[]> Screenshot(string sessionKey)
		{
			IPage page;
			await _sessionsLock.WaitAsync();
			try
			{
				if (!_sessions.TryGetValue(sessionKey, out page)) return null;
			}
			finally { _sessionsLock.Release(); }

			return await page.ScreenshotAsync(new PageScreenshotOptions
			{
				FullPage = true,
				Type = ScreenshotType.Png
			});
		}

		/// <summary>
		/// Clicks at the given page coordinates, waits for any resulting navigation or DOM
		/// changes to settle, then returns a new full-page PNG screenshot.
		/// Returns null if the session is not found.
		/// </summary>
		public static async Task<byte[]> ClickAndScreenshot(string sessionKey, int x, int y)
		{
			IPage page;
			await _sessionsLock.WaitAsync();
			try
			{
				if (!_sessions.TryGetValue(sessionKey, out page)) return null;
			}
			finally { _sessionsLock.Release(); }

			// Register navigation listener BEFORE clicking to avoid race condition.
			var navigationTask = page.WaitForNavigationAsync(new PageWaitForNavigationOptions
			{
				WaitUntil = WaitUntilState.Load,
				Timeout = 3000
			});

			await page.EvaluateAsync("(y) => window.scrollTo(0, y - window.innerHeight/2)", y);
			int scrollY = await page.EvaluateAsync<int>("window.scrollY");
			await page.Mouse.ClickAsync(x, y - scrollY);

			try
			{
				await navigationTask;
			}
			catch (TimeoutException)
			{
				// No navigation — JS-only interaction (dropdown, modal, etc.)
			}

			return await Screenshot(sessionKey);
		}

		private static async Task CloseSessionAsync(string sessionKey)
		{
			await _sessionsLock.WaitAsync();
			try
			{
				if (_sessions.TryGetValue(sessionKey, out var old))
				{
					_sessions.Remove(sessionKey);
					try { await old.CloseAsync(); } catch { /* already closed */ }
				}
			}
			finally { _sessionsLock.Release(); }
		}

		public static async Task EnsureContextAsync()
		{
			if (_context != null) return;

			await _browserLock.WaitAsync();
			try
			{
				if (_context != null) return;

				_playwright = await Playwright.CreateAsync();
				_browser = await _playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
				{
					Headless = !Program.SnapshotViewerHeaded
				});

				// Create a shared context with reduced motion and no animations.
				_context = await _browser.NewContextAsync(new BrowserNewContextOptions
				{
					ReducedMotion = ReducedMotion.Reduce
				});

				// Inject the animation-disabling CSS into every page before any scripts run.
				await _context.AddInitScriptAsync(DisableAnimationsScript);
			}
			finally
			{
				_browserLock.Release();
			}
		}

		/// <summary>
		/// Shuts down all open pages, the browser context, and the browser. Call on proxy shutdown.
		/// </summary>
		public static async Task ShutdownAsync()
		{
			foreach (var page in _sessions.Values)
				try { await page.CloseAsync(); } catch { }
			_sessions.Clear();

			if (_context != null) try { await _context.CloseAsync(); } catch { }
			if (_browser != null) try { await _browser.CloseAsync(); } catch { }
			_playwright?.Dispose();
		}
	}
}
