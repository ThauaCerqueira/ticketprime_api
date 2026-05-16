namespace src.Service;

using System.Text.RegularExpressions;
using src.Infrastructure.IRepository;
using src.DTOs;
using src.Models;

public class CupomService
{
    private readonly ICupomRepository _repository;
    private readonly IReservaRepository _reservaRepository;
    private static readonly Regex _codigoRegex = new(@"^[a-zA-Z0-9]+$", RegexOptions.Compiled);

    public CupomService(ICupomRepository repository, IReservaRepository? reservaRepository = null)
    {
        _repository = repository;
        _reservaRepository = reservaRepository!;
    }

    public async Task<bool> CriarAsync(CreateCouponDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo))
            throw new ArgumentException("Código do cupom é obrigatório.");

        if (dto.Codigo.Length < 3 || dto.Codigo.Length > 20)
            throw new ArgumentException("O código do cupom deve ter entre 3 e 20 caracteres.");

        if (!_codigoRegex.IsMatch(dto.Codigo))
            throw new ArgumentException("O código do cupom deve conter apenas letras e números.");

        // Validação conforme o tipo de desconto
        if (dto.TipoDesconto == DiscountType.Percentual)
        {
            if (dto.PorcentagemDesconto < 1 || dto.PorcentagemDesconto > 100)
                throw new ArgumentException("Desconto percentual deve ser entre 1 e 100.");
        }
        else if (dto.TipoDesconto == DiscountType.ValorFixo)
        {
            if (dto.ValorDescontoFixo is null or <= 0)
                throw new ArgumentException("Desconto de valor fixo deve ser informado e positivo.");
        }

        if (dto.ValorMinimoRegra < 0)
            throw new ArgumentException("Valor mínimo não pode ser negativo.");

        var existente = await _repository.ObterPorCodigoAsync(dto.Codigo);
        if (existente != null)
            throw new InvalidOperationException("Cupom já existe.");

        var cupom = new Coupon
        {
            Codigo = dto.Codigo,
            TipoDesconto = dto.TipoDesconto,
            PorcentagemDesconto = dto.TipoDesconto == DiscountType.Percentual ? dto.PorcentagemDesconto : 0,
            ValorDescontoFixo = dto.TipoDesconto == DiscountType.ValorFixo ? dto.ValorDescontoFixo : null,
            ValorMinimoRegra = dto.ValorMinimoRegra,
            DataExpiracao = dto.DataExpiracao,
            LimiteUsos = dto.LimiteUsos,
            CategoriaEvento = dto.CategoriaEvento,
            PrimeiroAcesso = dto.PrimeiroAcesso
        };

        var resultado = await _repository.CriarAsync(cupom);
        return resultado > 0;
    }

    public async Task<Coupon?> ObterPorCodigoAsync(string codigo)
    {
        return await _repository.ObterPorCodigoAsync(codigo);
    }

    public async Task<IEnumerable<Coupon>> ListarAsync()
    {
        return await _repository.ListarAsync();
    }

    public async Task<bool> DeletarAsync(string codigo)
    {
        var existente = await _repository.ObterPorCodigoAsync(codigo);
        if (existente == null)
            return false;

        // ═══════════════════════════════════════════════════════════════
        // SEGURANÇA: Verifica se existem reservas ATIVAS usando este cupom
        // ANTES de deletar. Se houver, retorna false e o controller retorna
        // um 400 amigável em vez de um 500 genérico (FK violation).
        // ═══════════════════════════════════════════════════════════════
        if (_reservaRepository != null)
        {
            var reservasAtivas = await _reservaRepository.ContarReservasPorCupomAsync(codigo);
            if (reservasAtivas > 0)
                throw new InvalidOperationException(
                    $"Não é possível excluir o cupom '{codigo}': " +
                    $"existem {reservasAtivas} reserva(s) ativa(s) utilizando este cupom. " +
                    $"Cancele as reservas primeiro ou aguarde os eventos ocorrerem.");
        }

        return await _repository.DeletarAsync(codigo);
    }
}
