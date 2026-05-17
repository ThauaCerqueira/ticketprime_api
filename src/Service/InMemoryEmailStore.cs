using System.Collections.Concurrent;

namespace src.Service;

/// <summary>
/// Armazena em memória os e-mails enviados pelo ConsoleEmailService (desenvolvimento).
/// Permite que o painel /admin/emails exiba os e-mails simulados sem precisar
/// de SMTP configurado. Útil para testar fluxos de verificação, compra e cancelamento.
/// 
/// ═══════════════════════════════════════════════════════════════════════════════
/// SEGURANÇA: Apenas para desenvolvimento. O store é resetado ao reiniciar a API.
/// Em produção, use SmtpEmailService — este store não deve ser populado.
/// ═══════════════════════════════════════════════════════════════════════════════
/// </summary>
public class InMemoryEmailStore
{
    private readonly ConcurrentQueue<StoredEmail> _emails = new();
    private const int MaxEmails = 200;

    public void Add(string to, string subject, string body)
    {
        var email = new StoredEmail
        {
            Id = Guid.NewGuid().ToString("N"),
            To = to,
            Subject = subject,
            Body = body,
            SentAt = DateTime.UtcNow
        };

        _emails.Enqueue(email);

        // Limita o tamanho para evitar vazamento de memória
        while (_emails.Count > MaxEmails && _emails.TryDequeue(out _)) { }
    }

    public IEnumerable<StoredEmail> GetAll() => _emails.OrderByDescending(e => e.SentAt).ToList();

    public void Clear()
    {
        while (_emails.TryDequeue(out _)) { }
    }
}

public class StoredEmail
{
    public string Id { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}
