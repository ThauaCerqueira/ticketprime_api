using Microsoft.Extensions.Caching.Memory;

namespace src.Service;

/// <summary>
/// Serviço de blacklist de tokens JWT para invalidação imediata após logout.
///
/// Quando o usuário faz logout, o JTI (JWT ID) do access token ativo é armazenado
/// nessa blacklist até a expiração natural do token. Isso garante que tokens copiados
/// antes do logout (ex: via tráfego interceptado) não possam ser reutilizados.
///
/// ESCOPO: IMemoryCache (in-process). Em deployment com múltiplas instâncias,
/// substitua por IDistributedCache (Redis) para que o logout seja propagado para
/// todos os pods.
/// </summary>
public sealed class JwtBlacklistService
{
    private readonly IMemoryCache _cache;

    public JwtBlacklistService(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    /// Revoga um JTI. O entry expira automaticamente quando o token original expiraria.
    /// </summary>
    /// <param name="jti">O valor da claim "jti" do token.</param>
    /// <param name="tokenExpiry">Momento em que o token original expira (UTC).</param>
    public void Revoke(string jti, DateTime tokenExpiry)
    {
        var ttl = tokenExpiry - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero) return; // Token já expirado — nenhuma ação necessária

        _cache.Set(CacheKey(jti), true, ttl);
    }

    /// <summary>
    /// Verifica se o JTI foi revogado (i.e., o token foi invalidado por logout).
    /// </summary>
    public bool IsRevoked(string jti)
        => _cache.TryGetValue(CacheKey(jti), out _);

    private static string CacheKey(string jti) => $"jwt_revoked:{jti}";
}
