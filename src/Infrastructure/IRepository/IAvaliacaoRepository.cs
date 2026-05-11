using src.Models;

namespace src.Infrastructure.IRepository;

public interface IAvaliacaoRepository
{
    Task<bool> UsuarioJaAvaliouAsync(string usuarioCpf, int eventoId);
    Task CriarAsync(Avaliacao avaliacao);
    Task<IEnumerable<Avaliacao>> ListarPorEventoAsync(int eventoId);
    Task<double?> ObterMediaAsync(int eventoId);
}
