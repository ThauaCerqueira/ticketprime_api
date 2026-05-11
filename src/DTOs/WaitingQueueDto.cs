namespace src.DTOs;

/// <summary>
/// DTO para exibir informações da fila de espera nas respostas da API.
/// </summary>
public class WaitingQueueDto
{
    public int Id { get; set; }
    public string UsuarioCpf { get; set; } = string.Empty;
    public string? UsuarioNome { get; set; }
    public string? UsuarioEmail { get; set; }
    public int EventoId { get; set; }
    public string? EventoNome { get; set; }
    public DateTime DataEntrada { get; set; }
    public string Status { get; set; } = "Aguardando";
    public DateTime? DataNotificacao { get; set; }
    public int Posicao { get; set; }
}

/// <summary>
/// DTO de resposta ao entrar na fila de espera.
/// </summary>
public class WaitingQueueResponseDto
{
    public string Mensagem { get; set; } = string.Empty;
    public int Posicao { get; set; }
    public int TotalNaFila { get; set; }
}
