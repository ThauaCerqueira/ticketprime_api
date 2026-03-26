using src.Models;

namespace src.Infrastructure.IRepository;

public interface IEventoRepository
{
    Task AdicionarAsync(Evento evento);
    Task<IEnumerable<Evento>> ObterTodosAsync();
    Task<IEnumerable<Evento>> ObterDisponiveisAsync();
}