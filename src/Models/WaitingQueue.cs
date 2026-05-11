namespace src.Models;

/// <summary>
/// Representa a entrada de um usuário na fila de espera de um evento lotado.
/// Quando uma vaga é liberada (cancelamento), o próximo da fila é notificado.
/// </summary>
public class WaitingQueue
{
    public int Id { get; set; }

    /// <summary>CPF do usuário na fila de espera.</summary>
    public string UsuarioCpf { get; set; } = string.Empty;

    /// <summary>ID do evento em que o usuário quer entrar.</summary>
    public int EventoId { get; set; }

    /// <summary>Data/hora em que entrou na fila de espera.</summary>
    public DateTime DataEntrada { get; set; }

    /// <summary>
    /// Status da entrada na fila: "Aguardando" (padrão), "Notificado" (já foi avisado),
    /// "Expirado" (desistiu/removeu), "Confirmado" (conseguiu comprar).
    /// </summary>
    public string Status { get; set; } = "Aguardando";

    /// <summary>Data/hora em que o usuário foi notificado sobre vaga disponível.</summary>
    public DateTime? DataNotificacao { get; set; }
}
