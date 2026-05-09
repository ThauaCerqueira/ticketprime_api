using src.Models;
using src.DTOs;
 
namespace src.Infrastructure.IRepository;
 
public interface IReservaRepository
{
    Task<Reserva> CriarAsync(Reserva reserva);
    Task<IEnumerable<ReservaDetalhadaDTO>> ListarPorUsuarioAsync(string cpf);
    Task<bool> CancelarAsync(int reservaId, string usuarioCpf);
    Task<int> ContarReservasUsuarioPorEventoAsync(string usuarioCpf, int eventoId);
    Task<int> ContarReservasPorEventoAsync(int eventoId);
    Task<ReservaDetalhadaDTO?> ObterDetalhadaPorIdAsync(int reservaId, string usuarioCpf);
}