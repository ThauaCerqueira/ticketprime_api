using src.DTOs;
using src.Models;
using src.Infrastructure.Repository;
using src.Infrastructure.IRepository;

namespace src.Service;

public class EventoService
{
    private readonly IEventoRepository _Repository;
    public EventoService(IEventoRepository repository)
    {
        _Repository = repository;
    }
    public async Task<Evento?> CriarNovoEvento(CriarEventoDTO dto)
    {

        var novoEvento = new Evento(
            dto.Nome,
            dto.CapacidadeTotal,
            dto.DataEvento,
            dto.PrecoPadrao,
            dto.LimiteIngressosPorUsuario
        );
        await _Repository.AdicionarAsync(novoEvento);

        return novoEvento;
    }
    public async Task<IEnumerable<Evento>> ListarEventos()
    {
        return await _Repository.ObterTodosAsync();
    }

    public async Task<IEnumerable<Evento>> ListarEventosDisponiveis()
    {
    return await _Repository.ObterDisponiveisAsync();
    }
}