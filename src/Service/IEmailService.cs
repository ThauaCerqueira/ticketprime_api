namespace src.Service;

/// <summary>
/// Serviço de envio de email. Pode ser implementado com SMTP real
/// (SmtpEmailService) ou console (ConsoleEmailService) para desenvolvimento.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Envia um email para um destinatário.
    /// </summary>
    Task SendAsync(string to, string subject, string body);
}
