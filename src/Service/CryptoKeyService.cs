using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace src.Service;

/// <summary>
/// Gerencia pares de chaves ECDH P-256 para E2E encryption de fotos de eventos.
///
/// Anteriormente era um singleton com uma única chave global. Agora suporta:
/// - Chave global herdada (para compatibilidade com fluxos existentes)
/// - Chaves por evento (cada evento/organizador tem seu próprio par de chaves)
///
/// A chave privada fica apenas no servidor e nunca é transmitida.
/// </summary>
public sealed class CryptoKeyService : IDisposable
{
    private readonly ECDiffieHellman _ecdhGlobal;
    private readonly string _chavePublicaGlobalJwk;

    /// <summary>
    /// Chave de configuração para a chave privada ECDH persistida em Base64 (PKCS#8).
    /// Configure via appsettings.json ou variável de ambiente:
    ///   appsettings: "Crypto:PrivateKeyBase64": "base64..."
    ///   env var:     Crypto__PrivateKeyBase64=base64...
    /// ═══════════════════════════════════════════════════════════════════
    /// Se não configurada, uma NOVA chave é gerada na inicialização e
    /// logada no console para ser salva.
    /// ═══════════════════════════════════════════════════════════════════
    /// </summary>
    private const string ChavePrivadaConfigKey = "Crypto:PrivateKeyBase64";

    // Cache de chaves por evento: <eventoId, (ECDiffieHellman, jwk)>
    private readonly ConcurrentDictionary<int, (ECDiffieHellman Key, string Jwk)> _chavesPorEvento = new();

    public CryptoKeyService(IConfiguration configuration)
    {
        var chaveBase64 = configuration[ChavePrivadaConfigKey];

        if (!string.IsNullOrEmpty(chaveBase64))
        {
            // Restaura chave existente de configuração persistente
            var keyBytes = Convert.FromBase64String(chaveBase64);
            _ecdhGlobal = ECDiffieHellman.Create();
            _ecdhGlobal.ImportPkcs8PrivateKey(keyBytes, out _);
        }
        else
        {
            // Gera nova chave (primeira execução ou config não definida)
            _ecdhGlobal = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var privateKey = _ecdhGlobal.ExportPkcs8PrivateKey();
            var base64 = Convert.ToBase64String(privateKey);
            Console.WriteLine($"[CryptoKeyService] ╔══════════════════════════════════════════════════════╗");
            Console.WriteLine($"[CryptoKeyService] ║  NOVA CHAVE ECDH GERADA — Salve em appsettings    ║");
            Console.WriteLine($"[CryptoKeyService] ║  \"{ChavePrivadaConfigKey}\":                  ║");
            Console.WriteLine($"[CryptoKeyService] ║  \"{base64}\"  ║");
            Console.WriteLine($"[CryptoKeyService] ╚══════════════════════════════════════════════════════╝");
        }

        _chavePublicaGlobalJwk = ExportarChavePublicaJwk(_ecdhGlobal);
    }

    /// <summary>
    /// Retorna a chave pública ECDH P-256 global do servidor em formato JWK.
    /// Usada por fluxos que não especificam um evento (compatibilidade).
    /// </summary>
    public string ObterChavePublicaJwk() => _chavePublicaGlobalJwk;

    /// <summary>
    /// Retorna a chave pública de um evento específico em formato JWK.
    /// Se ainda não existir, uma nova chave é gerada para este evento.
    /// </summary>
    public string ObterChavePublicaEventoJwk(int eventoId)
    {
        var entry = _chavesPorEvento.GetOrAdd(eventoId, _ =>
        {
            var ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var jwk = ExportarChavePublicaJwk(ecdh);
            return (ecdh, jwk);
        });

        return entry.Jwk;
    }

    /// <summary>
    /// Deriva um segredo compartilhado usando a chave privada GLOBAL do servidor
    /// e a chave pública do organizador.
    /// </summary>
    public byte[] DerivarSegredo(byte[] chavePublicaOrgX, byte[] chavePublicaOrgY)
    {
        using var otherParty = ECDiffieHellman.Create();
        otherParty.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = { X = chavePublicaOrgX, Y = chavePublicaOrgY }
        });

        return _ecdhGlobal.DeriveKeyFromHash(otherParty.PublicKey, HashAlgorithmName.SHA256);
    }

    /// <summary>
    /// Deriva um segredo compartilhado para um evento específico.
    /// </summary>
    public byte[] DerivarSegredoDoEvento(int eventoId, byte[] chavePublicaOrgX, byte[] chavePublicaOrgY)
    {
        if (!_chavesPorEvento.TryGetValue(eventoId, out var entry))
        {
            throw new InvalidOperationException($"Nenhuma chave encontrada para o evento {eventoId}. " +
                "Chame ObterChavePublicaEventoJwk primeiro para gerar a chave.");
        }

        using var otherParty = ECDiffieHellman.Create();
        otherParty.ImportParameters(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = { X = chavePublicaOrgX, Y = chavePublicaOrgY }
        });

        return entry.Key.DeriveKeyFromHash(otherParty.PublicKey, HashAlgorithmName.SHA256);
    }

    public void Dispose()
    {
        _ecdhGlobal.Dispose();
        foreach (var entry in _chavesPorEvento.Values)
        {
            entry.Key.Dispose();
        }
        _chavesPorEvento.Clear();
    }

    private static string ExportarChavePublicaJwk(ECDiffieHellman ecdh)
    {
        var parameters = ecdh.ExportParameters(false);
        var xBase64Url = Base64UrlEncode(parameters.Q.X!);
        var yBase64Url = Base64UrlEncode(parameters.Q.Y!);

        var jwk = new
        {
            kty = "EC",
            crv = "P-256",
            x = xBase64Url,
            y = yBase64Url
        };

        return JsonSerializer.Serialize(jwk);
    }

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
