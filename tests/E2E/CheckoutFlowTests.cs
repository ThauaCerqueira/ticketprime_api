using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace TicketPrime.E2E;

/// <summary>
/// Playwright E2E tests for the checkout / purchase flow.
/// These require a running app with at least one event in the database.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class CheckoutFlowTests : PlaywrightTestBase
{
    private static string BaseUrl =>
        Environment.GetEnvironmentVariable("TICKETPRIME_BASE_URL") ?? "https://localhost";

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true,
        ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
        Locale = "pt-BR"
    };

    [Test]
    public async Task PurchaseButton_RequiresLogin()
    {
        // Navigate to the event listing as an anonymous user
        await Page.GotoAsync($"{BaseUrl}/vitrine");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Find the first "Comprar" or "Ver evento" button
        var comprarBtn = Page.GetByRole(AriaRole.Button, new() { NameString = "Comprar" })
            .Or(Page.GetByRole(AriaRole.Link, new() { NameString = "Comprar" }));

        var count = await comprarBtn.CountAsync();
        if (count == 0)
        {
            Assert.Ignore("No events available in DB — skipping checkout flow test.");
            return;
        }

        await comprarBtn.First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // After clicking Comprar without login, should redirect to login or show login modal
        var url = Page.Url;
        var pageContent = await Page.ContentAsync();

        bool redirectedOrBlockedLogin =
            url.Contains("/login") ||
            pageContent.Contains("CPF") ||
            pageContent.Contains("Entrar");

        Assert.That(redirectedOrBlockedLogin, Is.True,
            "Expected login redirect or prompt when purchasing without authentication.");
    }

    [Test]
    public async Task EventDetailPage_ShowsEventInfo()
    {
        // Load the vitrine, find the first event link and navigate to its detail page
        await Page.GotoAsync($"{BaseUrl}/vitrine");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var eventoLink = Page.GetByRole(AriaRole.Link, new() { NameString = "Ver evento" })
            .Or(Page.GetByRole(AriaRole.Link, new() { NameString = "Detalhes" }));

        var count = await eventoLink.CountAsync();
        if (count == 0)
        {
            Assert.Ignore("No events available in DB — skipping detail page test.");
            return;
        }

        await eventoLink.First.ClickAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should be on /evento/{id} and show event content
        Assert.That(Page.Url, Does.StartWith($"{BaseUrl}/evento/"));

        // Title must not be a generic 404
        var title = await Page.TitleAsync();
        Assert.That(title, Does.Not.Contain("404"));
    }
}
