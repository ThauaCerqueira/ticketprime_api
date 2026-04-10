using src.Models;
using Moq;
using src.Infrastructure.IRepository;
using src.Service;

namespace tests;

public class UnitTest1
{
    [Fact]
    public void TestarCriacaoDeObjetoUsuario()
    {
        var usuario = new Usuario 
        { 
            Cpf = "12345678901", 
            Nome = "Teste", 
            Email = "teste@email.com", 
            Senha = "123" 
        };

        var cpfEsperado = "12345678901";

        Assert.Equal(cpfEsperado, usuario.Cpf);
    }


    [Fact]
    public async Task CadastrarUsuario_DeveRetornarErro_QuandoCpfJaCadastrado()
    {
        var repoMock = new Mock<IUsuarioRepository>();
        var service = new UsuarioService(repoMock.Object);
        
        var cpfDuplicado = "12345678901";
        var usuarioNoBanco = new Usuario { Cpf = cpfDuplicado, Nome = "Usuario Antigo" };
        var novaTentativa = new Usuario { Cpf = cpfDuplicado, Nome = "Usuario Novo" };

        repoMock.Setup(r => r.ObterPorCpf(cpfDuplicado))
                .ReturnsAsync(usuarioNoBanco);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            service.CadastrarUsuario(novaTentativa)
        );

        Assert.Equal("Erro: O CPF informado já está cadastrado.", exception.Message);
    }
}