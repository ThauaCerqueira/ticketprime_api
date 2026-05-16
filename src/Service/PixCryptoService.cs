using System.Security.Cryptography;

namespace src.Service;

/// <summary>
/// Criptografa/descriptografa ChavePix em repouso usando AES-256-GCM.
/// 
/// A chave mestra (PixMasterKey) vem do Vault, mapeada pelo
/// VaultConfigurationProvider como "Crypto:PixMasterKey".
/// 
/// ═══════════════════════════════════════════════════════════════════
/// SEGURANÇA:
///   - AES-256-GCM: criptografia autenticada (confidencialidade +
///     integridade). Nonce de 12 bytes + ciphertext + tag de 16 bytes.
///   - Nonce aleatório a cada chamada → mesmo texto puro gera
///     ciphertext diferente a cada vez.
///   - Chave mestra NUNCA no código fonte — só no Vault.
/// ═══════════════════════════════════════════════════════════════════
/// </summary>
public class PixCryptoService
{
    private readonly byte[] _masterKey;
    private const string ConfigKey = "Crypto:PixMasterKey";

    public PixCryptoService(IConfiguration? configuration = null)
    {
        var keyBase64 = configuration?[ConfigKey];

        if (!string.IsNullOrEmpty(keyBase64))
        {
            // Chave mestra fornecida pelo Vault ou user-secrets
            _masterKey = Convert.FromBase64String(keyBase64);
        }
        else
        {
            // Gera chave na primeira execução (deve ser salva no Vault)
            _masterKey = RandomNumberGenerator.GetBytes(32);
            Console.WriteLine($"[PixCryptoService] ╔══════════════════════════════════════════════════════╗");
            Console.WriteLine($"[PixCryptoService] ║  NOVA CHAVE PIX GERADA — Salve no Vault           ║");
            Console.WriteLine($"[PixCryptoService] ║  vault kv patch secret/ticketprime/crypto         ║");
            Console.WriteLine($"[PixCryptoService] ║    PixMasterKey=\"{Convert.ToBase64String(_masterKey)}\" ║");
            Console.WriteLine($"[PixCryptoService] ╚══════════════════════════════════════════════════════╝");
        }
    }

    /// <summary>
    /// Criptografa a ChavePix com AES-256-GCM.
    /// Retorna Base64 no formato: nonce(12) + ciphertext(N) + tag(16)
    /// </summary>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(_masterKey, 16);
        aes.Encrypt(nonce, plainBytes, ciphertext, tag);

        // Formato: nonce + ciphertext + tag
        var result = new byte[nonce.Length + ciphertext.Length + tag.Length];
        nonce.CopyTo(result, 0);
        ciphertext.CopyTo(result, nonce.Length);
        tag.CopyTo(result, nonce.Length + ciphertext.Length);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Descriptografa a ChavePix previamente criptografada com Encrypt().
    /// </summary>
    public string Decrypt(string encryptedBase64)
    {
        if (string.IsNullOrEmpty(encryptedBase64))
            return string.Empty;

        var data = Convert.FromBase64String(encryptedBase64);
        var nonce = data[..12];
        var ciphertext = data[12..^16];
        var tag = data[^16..];
        var plainBytes = new byte[ciphertext.Length];

        using var aes = new AesGcm(_masterKey, 16);
        aes.Decrypt(nonce, ciphertext, tag, plainBytes);

        return System.Text.Encoding.UTF8.GetString(plainBytes);
    }
}
