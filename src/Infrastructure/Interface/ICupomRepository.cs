namespace src.Infrastructure.IRepository;
using src.Models;

public interface ICupomRepository
{
    Task<int> CriarAsync(Cupom cupom);

    Task<Cupom?> ObterPorCodigoAsync(string codigo);

    Task<IEnumerable<Cupom>> ListarAsync();

    Task<bool> DeletarAsync(string codigo);
}