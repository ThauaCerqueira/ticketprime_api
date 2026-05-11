namespace src.Infrastructure.IRepository;
using src.Models;

public interface ICupomRepository
{
    Task<int> CriarAsync(Coupon cupom);
    Task<Coupon?> ObterPorCodigoAsync(string codigo);
    Task<IEnumerable<Coupon>> ListarAsync();
    Task IncrementarUsoAsync(string codigo);
    Task<bool> DeletarAsync(string codigo);
}
