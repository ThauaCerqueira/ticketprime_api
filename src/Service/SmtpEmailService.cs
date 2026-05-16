using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;

namespace src.Service;

/// <summary>
/// Implementação de IEmailService com SMTP real.
/// Configurado via appsettings.json ou variáveis de ambiente.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly bool _useSsl;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _logger = logger;
        var section = configuration.GetSection("EmailSettings");

        // ⚠️ Config path: EmailSettings:SmtpHost (appsettings.json) → EmailSettings__SmtpHost (env vars).
        //    ⛔ Deploy com EmailSmtpHost (sem "Settings") NÃO funciona com este código.
        //    ✅ Correção: usar sempre EmailSettings__SmtpHost no ambiente de deploy.
        //    O .NET configuration hierarchy (env vars > config) resolve automaticamente.
        _host = section["SmtpHost"] ?? "";
        _port = int.Parse(section["SmtpPort"] ?? "587");
        _username = section["SmtpUsername"] ?? "";
        _password = section["SmtpPassword"] ?? "";

        // ═══════════════════════════════════════════════════════════════════
        // TODO SEGURANÇA: Substituir por SecureString para evitar exposição
        //   da senha SMTP em dumps de memória. Strings são imutáveis e
        //   ficam na memória do processo até o GC coletar.
        //   SecureString é limpo da memória ao ser descartado.
        // ═══════════════════════════════════════════════════════════════════
        if (!string.IsNullOrEmpty(_password))
        {
            logger.LogWarning(
                "⚠️ SMTP password carregada como string simples (plaintext). " +
                "Considere usar SecureString ou Azure Key Vault para produção.");
        }

        _fromEmail = section["FromEmail"] ?? "";
        _fromName = section["FromName"] ?? "TicketPrime";
        _useSsl = bool.Parse(section["UseSsl"] ?? "true");

        // Fail-fast: valida que as credenciais SMTP foram configuradas (via env vars, user-secrets ou config)
        var missing = new System.Collections.Generic.List<string>();
        if (string.IsNullOrEmpty(_host)) missing.Add("EmailSettings:SmtpHost");
        if (string.IsNullOrEmpty(_username)) missing.Add("EmailSettings:SmtpUsername");
        if (string.IsNullOrEmpty(_password)) missing.Add("EmailSettings:SmtpPassword");
        if (string.IsNullOrEmpty(_fromEmail)) missing.Add("EmailSettings:FromEmail");

        if (missing.Count > 0)
        {
            var msg = $"Credenciais SMTP não configuradas. Configure as seguintes variáveis de ambiente " +
                      $"ou User Secrets: {string.Join(", ", missing)}. " +
                      $"Exemplo: EmailSettings__SmtpHost, EmailSettings__SmtpUsername, " +
                      $"EmailSettings__SmtpPassword, EmailSettings__FromEmail. " +
                      $"NUNCA coloque credenciais reais no appsettings.json versionado.";
            _logger.LogError("SmtpEmailService: {Missing}", msg);
            throw new InvalidOperationException(msg);
        }
    }

    public async Task SendAsync(string to, string subject, string body)
    {
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_fromEmail, _fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(to);

            using var client = new SmtpClient(_host, _port)
            {
                EnableSsl = _useSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrEmpty(_username))
            {
                client.Credentials = new NetworkCredential(_username, _password);
            }

            await client.SendMailAsync(message);

            _logger.LogInformation(
                "Email enviado com sucesso para {To} | Assunto: {Subject}",
                to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Falha ao enviar email para {To} | Assunto: {Subject}",
                to, subject);
            throw;
        }
    }
}
