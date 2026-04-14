using src.Models;

namespace  src.Infrastructure.IRepository;
public interface IUsuarioRepository 
{
    Task<Usuario?> ObterPorCpf(string cpf);
    Task CriarUsuario(Usuario usuario);

    Task<Usuario?> ObterPorLogin(string email, string senha);
}