using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace SnapshotViewerTests
{
    public class ScreenshotEngineTests
    {
        private readonly ITestOutputHelper _output;

        public ScreenshotEngineTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Firefox_LaunchesHeadless()
        {
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            Assert.True(browser.IsConnected);
            _output.WriteLine("Firefox launched OK.");
            await browser.CloseAsync();
        }

        [Fact]
        public async Task Firefox_NavigatesAndReturnsTitle()
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var page = await browser.NewPageAsync();
            await page.GotoAsync("https://example.com", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30000
            });

            string title = await page.TitleAsync();
            _output.WriteLine($"Page title: {title}");

            Assert.False(string.IsNullOrEmpty(title));
            await page.CloseAsync();
        }

        [Fact]
        public async Task Firefox_TakesFullPageScreenshot()
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var page = await browser.NewPageAsync(new BrowserNewPageOptions
            {
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
            });

            await page.GotoAsync("https://example.com", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.Load,
                Timeout = 30000
            });

            byte[] jpg = await page.ScreenshotAsync(new PageScreenshotOptions
            {
                FullPage = true,
                Type = ScreenshotType.Jpeg,
                Quality = 85
            });

            _output.WriteLine($"Screenshot size: {jpg.Length} bytes");

            Assert.True(jpg.Length > 1000, "Screenshot should be larger than 1KB");
            await page.CloseAsync();
        }
    }
}
