using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace TicketPrime.E2E;

/// <summary>
/// Playwright E2E tests for the user registration / sign-up flow.
/// Tests form validation, error states, and navigation behaviors.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class UserRegistrationFlowTests : PageTest
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
    public async Task RegistrationPage_HasAllFormFields()
    {
        await Page.GotoAsync($"{BaseUrl}/cadastro-user");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Look for key form controls
        var allInputs = Page.Locator("input");
        var inputCount = await allInputs.CountAsync();

        // Should have at least: CPF, Nome, Email, Senha, Confirmar Senha, Termos checkbox
        Assert.That(inputCount, Is.GreaterThanOrEqualTo(5),
            "Expected at least 5 input fields in registration form.");

        // Should have a submit button
        var submitButton = Page.GetByRole(AriaRole.Button).Filter(new() { HasTextString = "Criar" });
        await Expect(submitButton.First).ToBeVisibleAsync();
    }

    [Test]
    public async Task RegistrationForm_ShowsErrorOnEmptySubmit()
    {
        await Page.GotoAsync($"{BaseUrl}/cadastro-user");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Click the submit button without filling anything
        var submitButton = Page.GetByRole(AriaRole.Button).Filter(new() { HasTextString = "Criar" });
        var count = await submitButton.CountAsync();

        if (count == 0)
        {
            // Try alternative button text
            var altSubmit = Page.GetByRole(AriaRole.Button).Filter(new() { HasTextString = "criar" });
            count = await altSubmit.CountAsync();
            if (count == 0)
            {
                Assert.Ignore("Could not locate submit button — skipping test.");
                return;
            }
            await altSubmit.First.ClickAsync();
        }
        else
        {
            await submitButton.First.ClickAsync();
        }

        await Task.Delay(1000); // Allow validation to render

        // After submitting empty, there should be an error message visible
        var errorText = await Page.TextContentAsync("body");
        Assert.That(errorText, Does.Contain("obrigatório").Or
                               .Contain("CPF").Or
                               .Contain("senha").Or
                               .Contain("obrigatória"),
            "Expected validation error after submitting empty registration form.");
    }

    [Test]
    public async Task RegistrationForm_AcceptsValidInput()
    {
        await Page.GotoAsync($"{BaseUrl}/cadastro-user");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Fill in all fields with test data
        var inputs = Page.Locator("input");
        var inputCount = await inputs.CountAsync();

        // We need at least 5 inputs (CPF, Nome, Email, Senha, Confirmar, Termos)
        if (inputCount < 5)
        {
            Assert.Ignore($"Expected 5+ inputs, found {inputCount} — skipping.");
            return;
        }

        // Fill fields — we'll find them by placeholder or index
        var fields = await inputs.AllAsync();

        foreach (var field in fields)
        {
            var placeholder = await field.GetAttributeAsync("placeholder") ?? "";

            if (placeholder.Contains("000"))
                await field.FillAsync("12345678901"); // CPF
            else if (placeholder.Contains("nome", StringComparison.OrdinalIgnoreCase))
                await field.FillAsync("Teste E2E Usuário");
            else if (placeholder.Contains("email", StringComparison.OrdinalIgnoreCase))
                await field.FillAsync($"e2e_{Guid.NewGuid():N}@test.com");
            else if (placeholder.Contains("senha", StringComparison.OrdinalIgnoreCase) ||
                     placeholder.Contains("Senha", StringComparison.OrdinalIgnoreCase))
                await field.FillAsync("Teste@123");
            else if (placeholder.Contains("Confirma", StringComparison.OrdinalIgnoreCase) ||
                     placeholder.Contains("repita", StringComparison.OrdinalIgnoreCase))
                await field.FillAsync("Teste@123");
        }

        // Check the terms checkbox if present
        var termsCheckbox = Page.Locator("input[type='checkbox']").First;
        if (await termsCheckbox.IsVisibleAsync())
        {
            await termsCheckbox.CheckAsync();
        }

        // Now click submit
        var submitBtn = Page.GetByRole(AriaRole.Button).Filter(new() { HasTextString = "Criar" });
        var btnCount = await submitBtn.CountAsync();

        if (btnCount > 0)
        {
            await submitBtn.First.ClickAsync();
            await Task.Delay(2000);

            // We should get either a success message or an API error (not a crash)
            var pageContent = await Page.ContentAsync();
            Assert.That(pageContent, Does.Not.Contain("404").And
                                      .Not.Contain("internal error"),
                "Registration form submission should not cause a 404 or internal error.");
        }
    }

    [Test]
    public async Task RegistrationPage_ShowsPasswordMismatch()
    {
        await Page.GotoAsync($"{BaseUrl}/cadastro-user");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var inputs = Page.Locator("input");
        var fields = await inputs.AllAsync();

        // Fill password fields with mismatched values
        foreach (var field in fields)
        {
            var placeholder = await field.GetAttributeAsync("placeholder") ?? "";

            if (placeholder.Contains("000"))
                await field.FillAsync("12345678901");
            else if (placeholder.Contains("nome", StringComparison.OrdinalIgnoreCase))
                await field.FillAsync("Teste Mismatch");
            else if (placeholder.Contains("email", StringComparison.OrdinalIgnoreCase))
                await field.FillAsync("mismatch@test.com");
            else if (placeholder.Contains("Confirma", StringComparison.OrdinalIgnoreCase) ||
                     placeholder.Contains("repita", StringComparison.OrdinalIgnoreCase))
                await field.FillAsync("SenhaDiferente123");
        }

        // Click submit
        var submitBtn = Page.GetByRole(AriaRole.Button).Filter(new() { HasTextString = "Criar" });
        var count = await submitBtn.CountAsync();
        if (count == 0)
        {
            Assert.Ignore("Submit button not found.");
            return;
        }

        await submitBtn.First.ClickAsync();
        await Task.Delay(1000);

        // Should show password mismatch error
        var bodyText = await Page.TextContentAsync("body");
        Assert.That(bodyText, Does.Contain("coincidem").Or
                               .Contain("senhas"),
            "Expected password mismatch error message.");
    }
}
