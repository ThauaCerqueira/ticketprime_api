using Microsoft.Playwright.NUnit;

namespace TicketPrime.E2E;

/// <summary>
/// Classe base para todos os testes E2E.
/// Verifica automaticamente se os browsers do Playwright estão instalados
/// e pula (Assert.Ignore) o teste quando não estão, evitando falhas de
/// infraestrutura no pipeline.
/// </summary>
public abstract class PlaywrightTestBase : PageTest
{
    /// <summary>
    /// Executado antes de cada teste. Se o executável do Chromium não existir,
    /// o teste é marcado como Ignorado em vez de falhar com PlaywrightException.
    /// </summary>
    [SetUp]
    public void VerificarBrowsersInstalados()
    {
        var playwrightDir = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ms-playwright");

        if (!Directory.Exists(playwrightDir))
        {
            Assert.Ignore(
                "Browsers do Playwright não encontrados. " +
                "Execute: pwsh tests/E2E/bin/Debug/net10.0/playwright.ps1 install");
            return;
        }

        // Verifica se há pelo menos um executável de chromium
        var chromiumExe = Directory.GetFiles(playwrightDir, "chrome-headless-shell.exe", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(playwrightDir, "chrome", SearchOption.AllDirectories))
            .FirstOrDefault();

        if (chromiumExe == null)
        {
            Assert.Ignore(
                "Browsers do Playwright não encontrados. " +
                "Execute: pwsh tests/E2E/bin/Debug/net10.0/playwright.ps1 install");
        }
    }
}
