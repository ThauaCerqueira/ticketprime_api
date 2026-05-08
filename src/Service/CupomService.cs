namespace src.Service;

using src.Infrastructure.IRepository;
using src.DTOs;
using src.Models;

public class CupomService 
{
    private readonly ICupomRepository _repository;

    public CupomService(ICupomRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> CriarAsync(CriarCupomDTO dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo))
            throw new ArgumentException("Código do cupom é obrigatório.");

        if (dto.PorcentagemDesconto <= 0 || dto.PorcentagemDesconto > 100)
            throw new ArgumentException("Desconto deve ser entre 1 e 100.");

        if (dto.ValorMinimoRegra < 0)
            throw new ArgumentException("Valor mínimo não pode ser negativo.");

        var existente = await _repository.ObterPorCodigoAsync(dto.Codigo);
        if (existente != null)
            throw new InvalidOperationException("Cupom já existe.");

        var cupom = new Cupom
        {
            Codigo = dto.Codigo,
            PorcentagemDesconto = dto.PorcentagemDesconto,
            ValorMinimoRegra = dto.ValorMinimoRegra
        };

        var resultado = await _repository.CriarAsync(cupom);
        return resultado > 0;
    }

    public async Task<Cupom?> ObterPorCodigoAsync(string codigo)
    {
        return await _repository.ObterPorCodigoAsync(codigo);
    }

    public async Task<IEnumerable<Cupom>> ListarAsync()
    {
        return await _repository.ListarAsync();
    }

    public async Task<bool> DeletarAsync(string codigo)
    {
        var existente = await _repository.ObterPorCodigoAsync(codigo);
        if (existente == null)
            return false;

        return await _repository.DeletarAsync(codigo);
    }
}