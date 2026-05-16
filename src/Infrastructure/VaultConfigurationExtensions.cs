using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.AuthMethods.AppRole;
using VaultSharp.V1.AuthMethods.Kubernetes;

namespace src.Infrastructure;

/// <summary>
/// Configuração para conexão com HashiCorp Vault.
/// 
/// Uso em appsettings.json:
/// {
///   "Vault": {
///     "Address": "https://vault:8200",
///     "AuthMethod": "token",        // token | approle | kubernetes
///     "Token": "hvs...",            // usado quando AuthMethod = token
///     "RoleId": "",                 // usado quando AuthMethod = approle
///     "SecretId": "",               // usado quando AuthMethod = approle
///     "KubernetesRole": "",         // usado quando AuthMethod = kubernetes
///     "MountPoint": "secret",
///     "SecretPath": "ticketprime/crypto"
///   }
/// }
/// 
/// As secrets lidas do Vault são prefixadas com "Vault:" e mescladas
/// na configuração. Ex: Crypto:PrivateKeyBase64 ← Vault secret
/// </summary>
public class VaultOptions
{
    public const string SectionName = "Vault";

    public string Address { get; set; } = string.Empty;
    public string AuthMethod { get; set; } = "token";
    public string Token { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string SecretId { get; set; } = string.Empty;
    public string KubernetesRole { get; set; } = string.Empty;
    public string MountPoint { get; set; } = "secret";
    public string SecretPath { get; set; } = string.Empty;
}

/// <summary>
/// Extensão para carregar secrets do HashiCorp Vault como configuração do .NET.
/// Deve ser chamada no início do Program.cs, ANTES de builder.Build().
/// 
/// As secrets lidas são mescladas na configuração e ficam acessíveis
/// via IConfiguration, exatamente como appsettings.json ou env vars.
/// O CryptoKeyService já lê de IConfiguration["Crypto:PrivateKeyBase64"],
/// então funciona sem modificações.
/// </summary>
public static class VaultConfigurationExtensions
{
    /// <summary>
    /// Adiciona o Vault como fonte de configuração.
    /// </summary>
    public static IConfigurationBuilder AddVaultConfiguration(
        this IConfigurationBuilder builder,
        VaultOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Address))
            return builder; // Vault não configurado — ignora

        return builder.Add(new VaultConfigurationSource(options));
    }
}

/// <summary>
/// Fonte de configuração que lê secrets do Vault.
/// </summary>
internal sealed class VaultConfigurationSource : IConfigurationSource
{
    private readonly VaultOptions _options;

    public VaultConfigurationSource(VaultOptions options)
    {
        _options = options;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new VaultConfigurationProvider(_options);
    }
}

/// <summary>
/// Provider que carrega secrets do Vault e os expõe como configuração.
/// </summary>
internal sealed class VaultConfigurationProvider : ConfigurationProvider
{
    private readonly VaultOptions _options;

    public VaultConfigurationProvider(VaultOptions options)
    {
        _options = options;
    }

    public override void Load()
    {
        try
        {
            var authMethod = CreateAuthMethod();
            var settings = new VaultClientSettings(_options.Address, authMethod);

            var client = new VaultClient(settings);

            // Lê o segredo do caminho configurado
            var mountPoint = _options.MountPoint.TrimEnd('/');
            var secretPath = _options.SecretPath.TrimStart('/');

            var secret = client.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                path: secretPath,
                mountPoint: mountPoint
            ).GetAwaiter().GetResult();

            if (secret?.Data?.Data != null)
            {
                foreach (var kv in secret.Data.Data)
                {
                    if (kv.Value is string strValue)
                    {
                        // Mapeia para a chave que o CryptoKeyService espera:
                        // Se o Vault tem "PrivateKeyBase64", vira "Crypto:PrivateKeyBase64"
                        Data[$"Crypto:{kv.Key}"] = strValue;
                    }
                }
            }

            Console.WriteLine($"[Vault] ✓ Secrets carregadas de {mountPoint}/{secretPath}");
        }
        catch (Exception ex)
        {
            // Se o Vault não estiver acessível, a aplicação continua
            // usando as configs locais (appsettings.json / env vars).
            // Isso permite desenvolvimento sem Vault.
            Console.WriteLine($"[Vault] ⚠ Falha ao carregar secrets (não crítico): {ex.Message}");
        }
    }

    private IAuthMethodInfo CreateAuthMethod()
    {
        return _options.AuthMethod.ToLowerInvariant() switch
        {
            "approle" => new AppRoleAuthMethodInfo(_options.RoleId, _options.SecretId),
            "kubernetes" => new KubernetesAuthMethodInfo(
                _options.KubernetesRole,
                "vault" // service account token path padrão
            ),
            _ => new TokenAuthMethodInfo(_options.Token)
        };
    }
}
