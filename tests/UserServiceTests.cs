using src.Models;
using Moq;
using src.Infrastructure.IRepository;
using src.Service;

namespace tests;

public class UserServiceTests
{
    [Fact]
    public void TestarCriacaoDeObjetoUsuario()
    {
        var usuario = new User
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
        var emailMock = new Mock<IEmailService>();
        var service = new UserService(repoMock.Object, emailMock.Object);
        
        // CPF válido com dígitos verificadores corretos
        var cpfDuplicado = "52998224725";
        var usuarioNoBanco = new User { Cpf = cpfDuplicado, Nome = "Usuario Antigo" };
        var novaTentativa = new User { Cpf = cpfDuplicado, Nome = "Usuario Novo", Email = "novo@test.com", Senha = "Str0ng!Pass" };

        repoMock.Setup(r => r.ObterPorCpf(cpfDuplicado))
                .ReturnsAsync(usuarioNoBanco);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => 
            service.CadastrarUsuario(novaTentativa)
        );

        Assert.Equal("Erro: O CPF informado já está cadastrado.", exception.Message);
    }
}