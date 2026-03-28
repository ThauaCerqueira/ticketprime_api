using src.Models;

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
}
