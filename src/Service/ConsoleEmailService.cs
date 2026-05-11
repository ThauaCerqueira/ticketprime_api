using Microsoft.Extensions.Logging;

namespace src.Service;

/// <summary>
/// Implementação de IEmailService que escreve os emails no console.
/// Usada em desenvolvimento quando não há servidor SMTP configurado.
/// </summary>
public class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
    {
        _logger = logger;
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

        return Task.CompletedTask;
    }
}
