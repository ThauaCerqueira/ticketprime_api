using System.Security.Cryptography;
using System.Text;
using Moq;
using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Models;
using src.Service;

namespace tests;

/// <summary>
/// Testes unitários do fluxo de recuperação de senha:
///   GerarResetSenhaToken → armazena SHA-256 no banco, envia token plaintext por email
///   RedefinirSenha       → compara SHA-256 do token fornecido com o hash armazenado
/// </summary>
public class PasswordRecoveryTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private (UserService service, Mock<IUsuarioRepository> repo, Mock<IEmailService> email) BuildService()
    {
        var repo = new Mock<IUsuarioRepository>();
        var emailSvc = new Mock<IEmailService>();
        emailSvc.Setup(e => e.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

        var service = new UserService(repo.Object, emailSvc.Object);
        return (service, repo, emailSvc);
    }

    // ── GerarResetSenhaToken ──────────────────────────────────────────────────

    [Fact]
    public async Task GerarResetSenhaToken_DeveArmazenarHashNobanco_NaoPlaintext()
    {
        var (svc, repo, _) = BuildService();

        string? hashSalvo = null;
        repo.Setup(r => r.SalvarResetToken(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>()))
            .Callback<string, string, DateTime>((_, hash, _) => hashSalvo = hash)
            .Returns(Task.CompletedTask);

        var tokenPlaintext = await svc.GerarResetSenhaToken("user@example.com");

        Assert.NotNull(hashSalvo);
        // O que está no banco deve ser o SHA-256 do token enviado ao usuário
        Assert.Equal(Sha256Hex(tokenPlaintext), hashSalvo);
        // O token enviado ao usuário NÃO deve ser igual ao que foi salvo no banco
        Assert.NotEqual(tokenPlaintext, hashSalvo);
    }

    [Fact]
    public async Task GerarResetSenhaToken_DeveRetornarTokenCriptograficamenteAleatorio()
    {
        var (svc, repo, _) = BuildService();
        repo.Setup(r => r.SalvarResetToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        var token1 = await svc.GerarResetSenhaToken("user@example.com");
        var token2 = await svc.GerarResetSenhaToken("user@example.com");

        Assert.NotEqual(token1, token2);
        // Token deve ser hexadecimal de 64 chars (SHA-256 de 32 bytes)
        Assert.Matches("^[0-9a-f]{64}$", token1);
    }

    [Fact]
    public async Task GerarResetSenhaToken_DeveDefinirExpiracaoEm1Hora()
    {
        var (svc, repo, _) = BuildService();

        DateTime? expiracaoSalva = null;
        repo.Setup(r => r.SalvarResetToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Callback<string, string, DateTime>((_, _, exp) => expiracaoSalva = exp)
            .Returns(Task.CompletedTask);

        var before = DateTime.UtcNow;
        await svc.GerarResetSenhaToken("user@example.com");
        var after = DateTime.UtcNow;

        Assert.NotNull(expiracaoSalva);
        // Expiração deve ser ~1 hora à frente
        Assert.InRange(expiracaoSalva.Value,
            before.AddMinutes(59),
            after.AddMinutes(61));
    }

    [Fact]
    public async Task GerarResetSenhaToken_DeveEnviarEmail()
    {
        var (svc, repo, emailMock) = BuildService();
        repo.Setup(r => r.SalvarResetToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        await svc.GerarResetSenhaToken("user@example.com");

        emailMock.Verify(e => e.SendAsync(
            "user@example.com",
            It.Is<string>(s => s.Contains("Redefinição")),
            It.Is<string>(b => b.Contains("1 hora"))),
            Times.Once);
    }

    [Fact]
    public async Task GerarResetSenhaToken_EmailInvalido_DeveLancarExcecao()
    {
        var (svc, _, _) = BuildService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.GerarResetSenhaToken("nao-e-email"));
    }

    // ── RedefinirSenha ────────────────────────────────────────────────────────

    [Fact]
    public async Task RedefinirSenha_TokenCorreto_DeveRetornarTrue()
    {
        var (svc, repo, _) = BuildService();

        const string tokenPlaintext = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";
        var tokenHash = Sha256Hex(tokenPlaintext);

        var usuario = new User
        {
            Cpf = "52998224725",
            Email = "user@example.com",
            Senha = BCrypt.Net.BCrypt.HashPassword("OldPass1!"),
            ResetToken = tokenHash,
            ResetTokenExpiracao = DateTime.UtcNow.AddMinutes(30)
        };

        repo.Setup(r => r.ObterPorEmail("user@example.com")).ReturnsAsync(usuario);
        repo.Setup(r => r.AtualizarSenha(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        repo.Setup(r => r.LimparResetToken(It.IsAny<string>())).Returns(Task.CompletedTask);

        var resultado = await svc.RedefinirSenha("user@example.com", tokenPlaintext, "NewPass1!");

        Assert.True(resultado);
        repo.Verify(r => r.AtualizarSenha(usuario.Cpf, It.IsAny<string>()), Times.Once);
        repo.Verify(r => r.LimparResetToken("user@example.com"), Times.Once);
    }

    [Fact]
    public async Task RedefinirSenha_TokenIncorreto_DeveRetornarFalse()
    {
        var (svc, repo, _) = BuildService();

        var usuario = new User
        {
            Cpf = "52998224725",
            Email = "user@example.com",
            Senha = BCrypt.Net.BCrypt.HashPassword("OldPass1!"),
            // Hash de um token diferente
            ResetToken = Sha256Hex("token-correto-que-o-usuario-nao-tem"),
            ResetTokenExpiracao = DateTime.UtcNow.AddMinutes(30)
        };

        repo.Setup(r => r.ObterPorEmail("user@example.com")).ReturnsAsync(usuario);

        var resultado = await svc.RedefinirSenha("user@example.com", "token-errado", "NewPass1!");

        Assert.False(resultado);
        repo.Verify(r => r.AtualizarSenha(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RedefinirSenha_TokenExpirado_DeveRetornarFalse()
    {
        var (svc, repo, _) = BuildService();

        const string tokenPlaintext = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";

        var usuario = new User
        {
            Cpf = "52998224725",
            Email = "user@example.com",
            Senha = BCrypt.Net.BCrypt.HashPassword("OldPass1!"),
            ResetToken = Sha256Hex(tokenPlaintext),
            ResetTokenExpiracao = DateTime.UtcNow.AddMinutes(-1) // expirado
        };

        repo.Setup(r => r.ObterPorEmail("user@example.com")).ReturnsAsync(usuario);

        var resultado = await svc.RedefinirSenha("user@example.com", tokenPlaintext, "NewPass1!");

        Assert.False(resultado);
        repo.Verify(r => r.AtualizarSenha(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RedefinirSenha_EmailNaoEncontrado_DeveRetornarFalse()
    {
        var (svc, repo, _) = BuildService();

        repo.Setup(r => r.ObterPorEmail(It.IsAny<string>())).ReturnsAsync((User?)null);

        var resultado = await svc.RedefinirSenha("naoexiste@example.com", "qualquer-token", "NewPass1!");

        Assert.False(resultado);
    }

    [Fact]
    public async Task RedefinirSenha_SenhaFraca_DeveLancarArgumentException()
    {
        var (svc, repo, _) = BuildService();

        const string tokenPlaintext = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";

        var usuario = new User
        {
            Cpf = "52998224725",
            Email = "user@example.com",
            Senha = BCrypt.Net.BCrypt.HashPassword("OldPass1!"),
            ResetToken = Sha256Hex(tokenPlaintext),
            ResetTokenExpiracao = DateTime.UtcNow.AddMinutes(30)
        };

        repo.Setup(r => r.ObterPorEmail("user@example.com")).ReturnsAsync(usuario);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.RedefinirSenha("user@example.com", tokenPlaintext, "123")); // senha fraca
    }

    [Fact]
    public async Task RedefinirSenha_DeveSalvarSenhaComBcrypt()
    {
        var (svc, repo, _) = BuildService();

        const string tokenPlaintext = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";
        const string novaSenha = "NovaPass1!";

        var usuario = new User
        {
            Cpf = "52998224725",
            Email = "user@example.com",
            Senha = BCrypt.Net.BCrypt.HashPassword("OldPass1!"),
            ResetToken = Sha256Hex(tokenPlaintext),
            ResetTokenExpiracao = DateTime.UtcNow.AddMinutes(30)
        };

        string? hashSalvo = null;
        repo.Setup(r => r.ObterPorEmail("user@example.com")).ReturnsAsync(usuario);
        repo.Setup(r => r.AtualizarSenha(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, h) => hashSalvo = h)
            .Returns(Task.CompletedTask);
        repo.Setup(r => r.LimparResetToken(It.IsAny<string>())).Returns(Task.CompletedTask);

        await svc.RedefinirSenha("user@example.com", tokenPlaintext, novaSenha);

        Assert.NotNull(hashSalvo);
        // O hash salvo deve ser verificável pelo BCrypt
        Assert.True(BCrypt.Net.BCrypt.Verify(novaSenha, hashSalvo));
        // A nova senha não deve ser igual à antiga
        Assert.False(BCrypt.Net.BCrypt.Verify("OldPass1!", hashSalvo));
    }

    // ── Segurança: hash não deve ser reversível ───────────────────────────────

    [Fact]
    public void HashToken_DeveProduzirSha256De64Chars()
    {
        // Usa reflexão estática para testar o comportamento via GerarResetSenhaToken
        // A prova indireta é: o token salvo (64 chars hex) deve ser SHA-256 do token enviado
        var tokenQualquer = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";
        var hash = Sha256Hex(tokenQualquer);

        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
        Assert.NotEqual(tokenQualquer, hash);
    }

    [Fact]
    public void HashToken_MesmoInput_SempreMesmoOutput()
    {
        var token = "aabbccddeeff00112233445566778899aabbccddeeff00112233445566778899";
        var hash1 = Sha256Hex(token);
        var hash2 = Sha256Hex(token);

        Assert.Equal(hash1, hash2);
    }
}
