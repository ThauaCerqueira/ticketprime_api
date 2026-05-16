namespace src.DTOs;

/// <summary>
/// DTO para o payload do webhook do MercadoPago.
/// </summary>
public sealed class MercadoPagoWebhookDto
{
    public string? Action { get; set; }
    public string? ApiVersion { get; set; }
    public MercadoPagoWebhookDataDto? Data { get; set; }
    public DateTime DateCreated { get; set; }
    public long Id { get; set; }
    public bool LiveMode { get; set; }
    public string? Type { get; set; }
    public string? UserId { get; set; }
}

public sealed class MercadoPagoWebhookDataDto
{
    public string? Id { get; set; }
}
