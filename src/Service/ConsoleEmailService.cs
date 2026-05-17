using Microsoft.Extensions.Logging;

namespace src.Service;

/// <summary>
/// Implementação de IEmailService que escreve os emails no console
/// e os armazena em InMemoryEmailStore para consulta via /admin/emails.
/// Usada em desenvolvimento quando não há servidor SMTP configurado.
/// </summary>
public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;
    private readonly InMemoryEmailStore _emailStore;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger, InMemoryEmailStore emailStore)
    {
        _logger = logger;
        _emailStore = emailStore;
    }

    public Task SendAsync(string to, string subject, string body)
    {
        _logger.LogInformation(
            "[EMAIL] Para: {To} | Assunto: {Subject}",
            to, subject);

        Console.WriteLine(new string('=', 60));
        Console.WriteLine($"📧 PARA: {to}");
        Console.WriteLine($"📧 ASSUNTO: {subject}");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine(body);
        Console.WriteLine(new string('=', 60));

        // Armazena em memória para consulta no painel admin
        _emailStore.Add(to, subject, body);

        return Task.CompletedTask;
    }
}
