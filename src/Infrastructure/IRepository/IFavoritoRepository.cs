using src.Models;

namespace src.Infrastructure.IRepository;

public interface IFavoritoRepository
{
    Task<bool> IsFavoritoAsync(string usuarioCpf, int eventoId);
    Task AdicionarAsync(string usuarioCpf, int eventoId);
    Task RemoverAsync(string usuarioCpf, int eventoId);
    Task<IEnumerable<Favorito>> ListarPorUsuarioAsync(string usuarioCpf);
    Task<int> ContarFavoritosAsync(int eventoId);
}
