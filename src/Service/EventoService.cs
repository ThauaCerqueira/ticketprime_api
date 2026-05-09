using src.DTOs;
using src.Models;
using src.Infrastructure.Repository;
using src.Infrastructure.IRepository;

namespace src.Service;

public class EventoService
{
    private const decimal TaxaServicoMaxPct = 0.05m; // 5% do preço do ingresso

    private readonly IEventoRepository _Repository;
    public EventoService(IEventoRepository repository)
    {
        _Repository = repository;
    }

    public async Task<Evento?> CriarNovoEvento(CriarEventoDTO dto)
    {
        // Valida taxa de serviço: não pode exceder 5% do preço do ingresso
        if (dto.TaxaServico < 0)
            throw new ArgumentException("A taxa de serviço não pode ser negativa.");

        if (dto.PrecoPadrao > 0 && dto.TaxaServico > dto.PrecoPadrao * TaxaServicoMaxPct)
            throw new ArgumentException(
                $"A taxa de serviço não pode exceder 5% do preço do ingresso (máximo: R$ {dto.PrecoPadrao * TaxaServicoMaxPct:F2}).");

        var novoEvento = new Evento(
            dto.Nome,
            dto.CapacidadeTotal,
            dto.DataEvento,
            dto.PrecoPadrao,
            dto.LimiteIngressosPorUsuario
        )
        {
            TaxaServico = dto.TaxaServico
        };
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

    /// <summary>
    /// Exclui um evento. Só permitido se não houver reservas ativas vinculadas.
    /// </summary>
    public async Task DeletarEventoAsync(int id)
    {
        var evento = await _Repository.ObterPorIdAsync(id)
            ?? throw new InvalidOperationException("Evento não encontrado.");

        var deletou = await _Repository.DeletarAsync(id);
        if (!deletou)
            throw new InvalidOperationException(
                "Não é possível excluir o evento pois existem reservas ativas vinculadas a ele.");
    }
}