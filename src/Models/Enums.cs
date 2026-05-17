using System.ComponentModel;

namespace src.Models;

/// <summary>
/// Status de um evento.
/// ═══════════════════════════════════════════════════════════════════════
/// Use as extension methods em StatusExtensions para comparar/enumerar
/// de forma type-safe, evitando typos com strings literais.
/// ═══════════════════════════════════════════════════════════════════════
/// </summary>
public enum EventStatus
{
    [Description("Rascunho")]
    Rascunho = 0,

    [Description("Publicado")]
    Publicado = 1,

    [Description("Cancelado")]
    Cancelado = 2
}

/// <summary>
/// Status de uma reserva/ingresso.
/// </summary>
public enum ReservationStatus
{
    [Description("Ativa")]
    Ativa = 0,

    [Description("Usada")]
    Usada = 1,

    [Description("Cancelada")]
    Cancelada = 2,

    [Description("Aguardando Pagamento")]
    AguardandoPagamento = 3
}

/// <summary>
/// Status de uma entrada na fila de espera.
/// </summary>
public enum QueueStatus
{
    [Description("Ativo")]
    Ativo = 0,

    [Description("Notificado")]
    Notificado = 1,

    [Description("Expirado")]
    Expirado = 2
}

/// <summary>
/// Status de um documento de meia-entrada.
/// </summary>
public enum DocumentStatus
{
    [Description("Pendente")]
    Pendente = 0,

    [Description("Verificado")]
    Verificado = 1,

    [Description("Rejeitado")]
    Rejeitado = 2
}

/// <summary>
/// Extension methods para trabalhar com enums de status de forma type-safe.
/// ═══════════════════════════════════════════════════════════════════════
/// Exemplo de uso:
///   if (evento.StatusEnum == EventStatus.Publicado) { ... }
///   if (reserva.Status.Is(ReservationStatus.Ativa)) { ... }
/// ═══════════════════════════════════════════════════════════════════════
/// </summary>
public static class StatusExtensions
{
    // ── EventStatus ──────────────────────────────────────────────────

    public static EventStatus? ToEventStatus(this string? value) =>
        value switch
        {
            "Rascunho" => EventStatus.Rascunho,
            "Publicado" => EventStatus.Publicado,
            "Cancelado" => EventStatus.Cancelado,
            _ => null
        };

    public static string ToEventStatusString(this EventStatus status) => status switch
    {
        EventStatus.Rascunho => "Rascunho",
        EventStatus.Publicado => "Publicado",
        EventStatus.Cancelado => "Cancelado",
        _ => "Rascunho"
    };

    public static bool Is(this string? value, EventStatus status) =>
        value.ToEventStatus() == status;

    // ── ReservationStatus ────────────────────────────────────────────

    public static ReservationStatus? ToReservationStatus(this string? value) =>
        value switch
        {
            "Ativa" => ReservationStatus.Ativa,
            "Usada" => ReservationStatus.Usada,
            "Cancelada" => ReservationStatus.Cancelada,
            "Aguardando Pagamento" or "AguardandoPagamento" => ReservationStatus.AguardandoPagamento,
            _ => null
        };

    public static string ToReservationStatusString(this ReservationStatus status) => status switch
    {
        ReservationStatus.Ativa => "Ativa",
        ReservationStatus.Usada => "Usada",
        ReservationStatus.Cancelada => "Cancelada",
        ReservationStatus.AguardandoPagamento => "Aguardando Pagamento",
        _ => "Ativa"
    };

    public static bool Is(this string? value, ReservationStatus status) =>
        value.ToReservationStatus() == status;
}
