using src.Models;

namespace  src.Infrastructure.IRepository;
public interface IUsuarioRepository 
{
    Task<Usuario?> ObterPorCpf(string cpf);
    Task CriarUsuario(Usuario usuario);
}