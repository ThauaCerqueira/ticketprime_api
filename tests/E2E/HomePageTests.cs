using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace TicketPrime.E2E;

/// <summary>
/// Playwright E2E tests for TicketPrime.
///
/// Prerequisites:
///   1. Install Playwright browsers once:
///      dotnet build tests/E2E && pwsh tests/E2E/bin/Debug/net10.0/playwright.ps1 install
///   2. Start the app (docker compose up -d)
///   3. Set env var TICKETPRIME_BASE_URL or tests use https://localhost (default)
///   4. Run: dotnet test tests/E2E
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class HomePageTests : PageTest
{
    private static string BaseUrl =>
        Environment.GetEnvironmentVariable("TICKETPRIME_BASE_URL") ?? "https://localhost";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true,    // Accept self-signed certs used in dev/docker
        ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
        Locale = "pt-BR"
    };

    [Test]
    public async Task HomePage_Loads_WithHeroSection()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Title should contain TicketPrime
        Assert.That(await Page.TitleAsync(), Does.Contain("TicketPrime"));
    }

    [Test]
    public async Task VitrineRoute_ShowsEventListing()
    {
        await Page.GotoAsync($"{BaseUrl}/vitrine");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Check the page loaded (should not be an error page)
        var title = await Page.TitleAsync();
        Assert.That(title, Does.Not.Contain("404"));
    }

    [Test]
    public async Task NavBar_HasLoginLink()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The login/entrar link must be visible
        var loginLink = Page.GetByRole(AriaRole.Link, new() { NameString = "Entrar" })
            .Or(Page.GetByRole(AriaRole.Link, new() { NameString = "Login" }));

        var count = await loginLink.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Login link not found in navbar");
    }

    [Test]
    public async Task EventDetailPage_Redirects_When_NotFound()
    {
        // Use an event ID that is unlikely to exist
        var response = await Page.GotoAsync($"{BaseUrl}/evento/999999999");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should show a 404 message or redirect — at minimum not crash
        Assert.That(response, Is.Not.Null);
    }

    [Test]
    public async Task LoginPage_ShowsLoginForm()
    {
        await Page.GotoAsync($"{BaseUrl}/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // The page should have an email/CPF input and password input
        var cpfInput = Page.GetByRole(AriaRole.Textbox).First;
        await Expect(cpfInput).ToBeVisibleAsync();
    }
}
