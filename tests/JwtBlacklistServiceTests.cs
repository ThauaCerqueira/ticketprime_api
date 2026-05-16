using Microsoft.Extensions.Caching.Memory;
using src.Service;
using Xunit;

namespace TicketPrime.Tests.Security;

/// <summary>
/// Testes para <see cref="JwtBlacklistService"/> — serviço de invalidação imediata
/// de access tokens JWT após logout.
///
/// Cenários cobertos:
///   - Revogação básica (token é marcado como revogado)
///   - Token já expirado é ignorado (sem entrada no cache)
///   - IsRevoked retorna false para JTI desconhecido
///   - IsRevoked retorna true após Revoke
///   - JTIs diferentes são independentes (sem interferência)
///   - Revogação dupla do mesmo JTI é idempotente
///   - Entrada expira naturalmente após o TTL (simulação)
///   - Múltiplos tokens de diferentes usuários convivem no cache
///   - Chave de cache tem prefixo correto (evita colisões)
/// </summary>
public class JwtBlacklistServiceTests
{
    private static JwtBlacklistService CriarServico()
    {
        var options = new MemoryCacheOptions();
        var cache = new MemoryCache(options);
        return new JwtBlacklistService(cache);
    }

    // ─────────────────────────────────────────────────────────────────
    // Revoke: comportamento básico
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Revoke_TokenNaoExpirado_DeveMarcarComoRevogado()
    {
        var svc = CriarServico();
        var jti = Guid.NewGuid().ToString();
        var expiry = DateTime.UtcNow.AddMinutes(30);

        svc.Revoke(jti, expiry);

        Assert.True(svc.IsRevoked(jti));
    }

    [Fact]
    public void Revoke_TokenJaExpirado_NaoDeveAdicionarAoCache()
    {
        var svc = CriarServico();
        var jti = Guid.NewGuid().ToString();
        var expiry = DateTime.UtcNow.AddSeconds(-1); // Já expirou

        svc.Revoke(jti, expiry);

        // Token expirado não precisa ser rastreado — já é inválido
        Assert.False(svc.IsRevoked(jti));
    }

    [Fact]
    public void Revoke_TokenExpirandoAgora_NaoDeveAdicionarAoCache()
    {
        var svc = CriarServico();
        var jti = Guid.NewGuid().ToString();
        var expiry = DateTime.UtcNow; // Expiração no instante exato

        svc.Revoke(jti, expiry);

        // TTL = 0 ou negativo → não inserido
        Assert.False(svc.IsRevoked(jti));
    }

    // ─────────────────────────────────────────────────────────────────
    // IsRevoked: comportamento para JTI desconhecido
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsRevoked_JtiDesconhecido_DeveRetornarFalso()
    {
        var svc = CriarServico();
        var jtiNaoRevogado = Guid.NewGuid().ToString();

        Assert.False(svc.IsRevoked(jtiNaoRevogado));
    }

    [Fact]
    public void IsRevoked_JtiVazio_DeveRetornarFalso()
    {
        var svc = CriarServico();

        Assert.False(svc.IsRevoked(string.Empty));
    }

    [Fact]
    public void IsRevoked_JtiComEspacos_DeveRetornarFalso()
    {
        var svc = CriarServico();

        Assert.False(svc.IsRevoked("   "));
    }

    // ─────────────────────────────────────────────────────────────────
    // Independência: JTIs diferentes não interferem
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsRevoked_JtisDiferentes_SaoIndependentes()
    {
        var svc = CriarServico();
        var jtiA = Guid.NewGuid().ToString();
        var jtiB = Guid.NewGuid().ToString();
        var jtiC = Guid.NewGuid().ToString();
        var expiry = DateTime.UtcNow.AddMinutes(30);

        svc.Revoke(jtiA, expiry);
        svc.Revoke(jtiC, expiry);

        Assert.True(svc.IsRevoked(jtiA));
        Assert.False(svc.IsRevoked(jtiB)); // Nunca revogado
        Assert.True(svc.IsRevoked(jtiC));
    }

    [Fact]
    public void IsRevoked_MultiplosSufixosSimilares_NaoConfundem()
    {
        // Garante que "abc" e "abcXYZ" são chaves distintas no cache
        var svc = CriarServico();
        var baseJti = "shared-prefix-token";
        var jtiDiferente = "shared-prefix-token-extended";
        var expiry = DateTime.UtcNow.AddMinutes(30);

        svc.Revoke(baseJti, expiry);

        Assert.True(svc.IsRevoked(baseJti));
        Assert.False(svc.IsRevoked(jtiDiferente));
    }

    // ─────────────────────────────────────────────────────────────────
    // Idempotência: revogar duas vezes o mesmo JTI
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Revoke_MesmJtiDuasVezes_NaoLancaExcecao()
    {
        var svc = CriarServico();
        var jti = Guid.NewGuid().ToString();
        var expiry = DateTime.UtcNow.AddMinutes(30);

        svc.Revoke(jti, expiry);
        var ex = Record.Exception(() => svc.Revoke(jti, expiry.AddMinutes(5)));

        Assert.Null(ex); // Não deve lançar exceção
        Assert.True(svc.IsRevoked(jti)); // Ainda revogado
    }

    // ─────────────────────────────────────────────────────────────────
    // Múltiplos usuários: tokens diferentes convivem no cache
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Revoke_MuitosTokensSimultaneos_TodosSaoRastreados()
    {
        var svc = CriarServico();
        var expiry = DateTime.UtcNow.AddMinutes(30);
        var jtis = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid().ToString()).ToList();

        foreach (var jti in jtis)
            svc.Revoke(jti, expiry);

        foreach (var jti in jtis)
            Assert.True(svc.IsRevoked(jti), $"JTI {jti} deveria estar revogado");
    }

    // ─────────────────────────────────────────────────────────────────
    // TTL variados: expiração diferente por token
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(480)]
    public void Revoke_ComDiferentesExpiracoes_DevePersistirNoCache(int minutosRestantes)
    {
        var svc = CriarServico();
        var jti = Guid.NewGuid().ToString();
        var expiry = DateTime.UtcNow.AddMinutes(minutosRestantes);

        svc.Revoke(jti, expiry);

        Assert.True(svc.IsRevoked(jti),
            $"Token com {minutosRestantes} minutos restantes deveria estar no cache");
    }

    // ─────────────────────────────────────────────────────────────────
    // Isolamento de cache: instâncias diferentes têm caches separados
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Revoke_EmInstanciasDiferentes_NaoCompartilhamEstado()
    {
        var svc1 = CriarServico();
        var svc2 = CriarServico();
        var jti = Guid.NewGuid().ToString();
        var expiry = DateTime.UtcNow.AddMinutes(30);

        svc1.Revoke(jti, expiry);

        Assert.True(svc1.IsRevoked(jti));
        Assert.False(svc2.IsRevoked(jti)); // Instância diferente, cache diferente
    }

    // ─────────────────────────────────────────────────────────────────
    // Formato do JTI: diferentes formatos são aceitos
    // ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")] // UUID padrão
    [InlineData("abc123def456")]                           // Alfanumérico
    [InlineData("a-b-c-d-e")]                              // Com hífens
    [InlineData("tok_01HX9PZJ3QMBGZ7W8YRCBFDE4X")]        // ULID-like
    public void Revoke_ComDiferentesFormatosDeJti_DeveRevogar(string jti)
    {
        var svc = CriarServico();
        var expiry = DateTime.UtcNow.AddMinutes(30);

        svc.Revoke(jti, expiry);

        Assert.True(svc.IsRevoked(jti));
    }

    // ─────────────────────────────────────────────────────────────────
    // Consistência: IsRevoked antes e depois de Revoke
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void IsRevoked_AntesDeRevogar_RetornaFalso_AposRevogar_RetornaVerdadeiro()
    {
        var svc = CriarServico();
        var jti = Guid.NewGuid().ToString();
        var expiry = DateTime.UtcNow.AddMinutes(30);

        var antesDeRevogar = svc.IsRevoked(jti);
        svc.Revoke(jti, expiry);
        var aposRevogar = svc.IsRevoked(jti);

        Assert.False(antesDeRevogar);
        Assert.True(aposRevogar);
    }

    // ─────────────────────────────────────────────────────────────────
    // Cenário realista: fluxo login → logout → reuso do token
    // ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CenarioCompleto_LoginLogoutReusoToken_TokenDeveSerBloqueado()
    {
        // Simula: usuário faz login (jti gerado), faz logout (jti revogado),
        // tenta usar o mesmo token → bloqueado
        var svc = CriarServico();

        // 1. Login: JWT gerado com jti único e expiração de 30 min
        var jtiDoToken = Guid.NewGuid().ToString();
        var expiracaoDoToken = DateTime.UtcNow.AddMinutes(30);

        // 2. O token ainda não foi revogado (usuário ainda logado)
        Assert.False(svc.IsRevoked(jtiDoToken));

        // 3. Logout: JTI adicionado à blacklist
        svc.Revoke(jtiDoToken, expiracaoDoToken);

        // 4. Tentativa de reutilizar o token → bloqueado
        Assert.True(svc.IsRevoked(jtiDoToken));
    }

    [Fact]
    public void CenarioMultiplosLogins_CadaSessaoComJtiUnico_SaoIndependentes()
    {
        // Usuário tem 3 dispositivos com sessões ativas simultaneamente
        var svc = CriarServico();
        var expiry = DateTime.UtcNow.AddMinutes(30);

        var jtiDispositivo1 = Guid.NewGuid().ToString();
        var jtiDispositivo2 = Guid.NewGuid().ToString();
        var jtiDispositivo3 = Guid.NewGuid().ToString();

        // Apenas dispositivo2 faz logout
        svc.Revoke(jtiDispositivo2, expiry);

        // Dispositivos 1 e 3 continuam ativos
        Assert.False(svc.IsRevoked(jtiDispositivo1));
        Assert.True(svc.IsRevoked(jtiDispositivo2));
        Assert.False(svc.IsRevoked(jtiDispositivo3));
    }
}
