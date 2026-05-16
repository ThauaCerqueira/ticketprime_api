using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using src.Controllers;
using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Models;
using src.Service;
using TicketPrime.Tests;
using Xunit;

namespace tests;

/// <summary>
/// Testes unitários dos endpoints LGPD em UsuarioController:
///   GET  /api/usuarios/meus-dados   — Art. 18 I (acesso)
///   DELETE /api/usuarios/minha-conta — Art. 18 VI (exclusão/anonimização)
/// </summary>
public class LgpdControllerTests
{
    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria controller com serviço real (IUsuarioRepository mockado) e conexão
    /// de banco que irá falhar — testes que chegam à query SQL são integração.
    /// </summary>
    private static UserController CriarController(
        Mock<IUsuarioRepository>? repoMock = null,
        string? cpfClaim = "12345678901")
    {
        repoMock ??= new Mock<IUsuarioRepository>();
        var emailMock = new Mock<IEmailService>();

        var userService = new UserService(repoMock.Object, emailMock.Object);

        // ══════════════════════════════════════════════════════════════
        // ⚠️ ATENÇÃO: Estes testes usam um banco ISOLADO (LGPD_Test)
        // que é configurado via variável de ambiente TEST_DB_CONNECTION.
        // Se não configurada, o helper lança um erro claro.
        // ══════════════════════════════════════════════════════════════
        var connFactory = TestConnectionHelper.CreateDbConnectionFactory("LGPD_Test");

        var controller = new UserController(userService, connFactory, NullLogger<UserController>.Instance);

        if (cpfClaim != null)
        {
            var claims   = new[] { new Claim(ClaimTypes.NameIdentifier, cpfClaim) };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };
        }
        else
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };
        }

        return controller;
    }

    // ════════════════════════════════════════════════════════════════════════
    // GET /api/usuarios/meus-dados
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task MeusDados_SemClaim_Retorna401()
    {
        var controller = CriarController(cpfClaim: null);

        var result = await controller.MeusDados();

        Assert.IsType<UnauthorizedHttpResult>(result);
    }

    [Fact]
    public async Task MeusDados_UsuarioNaoEncontrado_Retorna404()
    {
        var repoMock = new Mock<IUsuarioRepository>();
        repoMock.Setup(r => r.ObterPorCpf("12345678901"))
                .ReturnsAsync((User?)null);

        var controller = CriarController(repoMock);

        var result = await controller.MeusDados();

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(404, statusResult.StatusCode);
    }

    [Fact]
    public async Task MeusDados_UsuarioValido_ChamaRepositorio()
    {
        var usuario = new User
        {
            Cpf    = "12345678901",
            Nome   = "João Silva",
            Email  = "joao@email.com",
            Perfil = "CLIENTE",
            EmailVerificado = true
        };

        var repoMock = new Mock<IUsuarioRepository>();
        repoMock.Setup(r => r.ObterPorCpf("12345678901"))
                .ReturnsAsync(usuario);

        var controller = CriarController(repoMock);

        // Pode retornar 200 (banco disponível) ou 500 (banco indisponível),
        // mas nunca 401 ou 404. O importante é que o repositório foi consultado.
        var result = await controller.MeusDados();

        repoMock.Verify(r => r.ObterPorCpf("12345678901"), Times.Once);
        Assert.IsNotType<UnauthorizedHttpResult>(result);

        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.NotEqual(404, statusResult.StatusCode);
    }

    // ════════════════════════════════════════════════════════════════════════
    // DELETE /api/usuarios/minha-conta
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ExcluirConta_SemClaim_Retorna401()
    {
        var controller = CriarController(cpfClaim: null);

        var result = await controller.ExcluirConta();

        Assert.IsType<UnauthorizedHttpResult>(result);
    }
}
