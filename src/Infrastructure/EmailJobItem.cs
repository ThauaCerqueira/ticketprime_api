namespace src.Infrastructure;

/// <summary>
/// Tipos de email transacional suportados pela fila de background.
/// </summary>
public enum EmailJobType
{
    EmailVerification,
    PasswordRecovery,
    PurchaseConfirmation,
    CancellationConfirmation,
    EventUpdateNotification,
    EventCancellationNotification,
    WaitingQueueNotification
}

/// <summary>
/// Item da fila de emails em background (Channel).
/// Processado por BackgroundEmailService de forma assíncrona.
/// </summary>
public class EmailJobItem
{
    public EmailJobType Type { get; init; }
    public string To { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public Guid JobId { get; } = Guid.NewGuid();
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
}
