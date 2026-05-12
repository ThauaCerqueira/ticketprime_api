using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace TicketPrime.E2E;

/// <summary>
/// Playwright E2E tests for navigation, routing, and page structure.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class NavigationFlowTests : PlaywrightTestBase
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
    public async Task HomePage_HasCorrectTitleAndHero()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("TicketPrime"));

        // Hero section should have a heading
        var heading = Page.GetByRole(AriaRole.Heading).First;
        await Expect(heading).ToBeVisibleAsync();
    }

    [Test]
    public async Task NavMenu_HasAllExpectedLinks()
    {
        await Page.GotoAsync(BaseUrl);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Check for key navigation links
        var eventosLink = Page.GetByRole(AriaRole.Link, new() { NameString = "Eventos" })
            .Or(Page.GetByRole(AriaRole.Link, new() { NameString = "eventos" }));
        var loginLink = Page.GetByRole(AriaRole.Link, new() { NameString = "Entrar" })
            .Or(Page.GetByRole(AriaRole.Link, new() { NameString = "Login" }));

        await Expect(eventosLink.First).ToBeVisibleAsync();
        await Expect(loginLink.First).ToBeVisibleAsync();
    }

    [Test]
    public async Task CadastroUserPage_ShowsRegistrationForm()
    {
        await Page.GotoAsync($"{BaseUrl}/cadastro-user");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should have the registration form with CPF, Nome, Email, Senha fields
        var inputs = Page.GetByRole(AriaRole.Textbox);
        var count = await inputs.CountAsync();

        // We expect at least CPF, Name, Email, Password fields
        Assert.That(count, Is.GreaterThanOrEqualTo(3),
            "Expected at least 3 input fields in the registration form.");

        // Should have a submit button
        var submitBtn = Page.GetByRole(AriaRole.Button, new() { NameString = "Criar" })
            .Or(Page.GetByRole(AriaRole.Button, new() { NameString = "criar" }));
        await Expect(submitBtn.First).ToBeVisibleAsync();
    }

    [Test]
    public async Task NotFoundPage_DoesNotCrash()
    {
        await Page.GotoAsync($"{BaseUrl}/pagina-inexistente-xyz");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should not throw — page should handle gracefully
        var title = await Page.TitleAsync();
        Assert.That(title, Is.Not.Null);

        // The page should have some content (not a blank page)
        var bodyText = await Page.TextContentAsync("body");
        Assert.That(bodyText, Is.Not.Null.Or.Empty);
    }

    [Test]
    public async Task EventosDisponiveis_PageLoads()
    {
        await Page.GotoAsync($"{BaseUrl}/vitrine");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Eventos"));
        Assert.That(title, Does.Contain("TicketPrime"));
    }

    [Test]
    public async Task RecuperarSenha_PageLoads()
    {
        await Page.GotoAsync($"{BaseUrl}/recuperar-senha");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Should have at least one input field (email)
        var inputs = Page.GetByRole(AriaRole.Textbox);
        var count = await inputs.CountAsync();
        Assert.That(count, Is.GreaterThan(0),
            "Expected at least one input on password recovery page.");
    }
}
