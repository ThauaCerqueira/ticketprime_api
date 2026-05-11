using src.DTOs;

namespace src.Infrastructure.IRepository;

public interface IWaitingQueueService
{
    Task<WaitingQueueResponseDto> EntrarNaFilaAsync(string usuarioCpf, int eventoId);
    Task SairDaFilaAsync(string usuarioCpf, int eventoId);
    Task<IEnumerable<WaitingQueueDto>> ListarFilaPorEventoAsync(int eventoId);
    Task<IEnumerable<WaitingQueueDto>> ListarMinhasFilasAsync(string usuarioCpf);
    Task NotificarProximoDaFilaAsync(int eventoId);
}
