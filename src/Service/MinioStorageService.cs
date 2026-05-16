using Minio;
using Minio.DataModel.Args;

namespace src.Service;

/// <summary>
/// Implementação de IStorageService usando MinIO (S3-compatible).
/// 
/// ═══════════════════════════════════════════════════════════════════
/// VANTAGENS sobre LocalFileStorageService:
///   - Escalável horizontalmente (todos os servidores veem os mesmos arquivos)
///   - Backup já configurado via MinIO lifecycle policies
///   - URLs públicas diretas (sem passar pelo servidor .NET)
///   - CDN-ready (basta colocar Cloudflare/CloudFront na frente)
/// ═══════════════════════════════════════════════════════════════════
/// 
/// Configuração necessária (appsettings.json ou Vault):
/// {
///   "Minio": {
///     "Endpoint": "localhost:9000",
///     "AccessKey": "minioadmin",
///     "SecretKey": "minioadmin",
///     "Bucket": "ticketprime",
///     "PublicUrl": "http://localhost:9000"  // ou CDN em produção
///   }
/// }
/// </summary>
public class MinioStorageService : IStorageService
{
    private static readonly HashSet<string> _tiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
    };

    private static readonly long _tamanhoMaximoBytes = 5 * 1024 * 1024; // 5 MB

    private readonly IMinioClient _minioClient;
    private readonly string _bucket;
    private readonly string _publicUrl;
    private readonly ILogger<MinioStorageService> _logger;

    public MinioStorageService(IConfiguration configuration, ILogger<MinioStorageService> logger)
    {
        _logger = logger;
        var section = configuration.GetSection("Minio");

        var endpoint = section["Endpoint"] ?? "localhost:9000";
        var accessKey = section["AccessKey"] ?? "minioadmin";
        var secretKey = section["SecretKey"] ?? "minioadmin";
        _bucket = section["Bucket"] ?? "ticketprime";
        var useSsl = bool.Parse(section["UseSsl"] ?? "false");
        _publicUrl = (section["PublicUrl"] ?? $"{(useSsl ? "https" : "http")}://{endpoint}").TrimEnd('/');

        _minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSsl)
            .Build();

        _logger.LogInformation(
            "MinioStorageService initialized: Endpoint={Endpoint}, Bucket={Bucket}, PublicUrl={PublicUrl}",
            endpoint, _bucket, _publicUrl);
    }

    public async Task<string> SalvarAsync(Stream stream, string nomeArquivo, string contentType, string subpasta = "geral")
    {
        // Valida tipo MIME
        if (!_tiposPermitidos.Contains(contentType))
            throw new InvalidOperationException(
                $"Tipo de arquivo não permitido: {contentType}. Aceitos: JPEG, PNG, WebP, GIF.");

        // Valida tamanho
        if (stream.Length > _tamanhoMaximoBytes)
            throw new InvalidOperationException("Arquivo excede o limite de 5 MB.");

        // Extrai extensão do content type
        var ext = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/gif" => ".gif",
            _ => ".bin"
        };

        // UUID como nome — previne colisões e path traversal
        var objectName = $"{subpasta}/{Guid.NewGuid():N}{ext}";

        stream.Position = 0;

        var putArgs = new PutObjectArgs()
            .WithBucket(_bucket)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(stream.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putArgs);

        // Retorna URL pública do objeto
        var url = $"{_publicUrl}/{_bucket}/{objectName}";
        _logger.LogInformation("Arquivo salvo no MinIO: {Url} ({Bytes} bytes)", url, stream.Length);

        return url;
    }

    public async Task<bool> RemoverAsync(string caminhoRelativo)
    {
        try
        {
            // Extrai o objectName da URL: "http://minio:9000/ticketprime/eventos/uuid.jpg"
            // → objectName = "eventos/uuid.jpg"
            var objectName = ExtrairObjectName(caminhoRelativo);
            if (string.IsNullOrWhiteSpace(objectName))
                return false;

            var rmArgs = new RemoveObjectArgs()
                .WithBucket(_bucket)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(rmArgs);
            _logger.LogInformation("Arquivo removido do MinIO: {Object}", objectName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao remover arquivo do MinIO: {Caminho}", caminhoRelativo);
            return false;
        }
    }

    /// <summary>
    /// Extrai o objectName (subpasta/uuid.ext) de uma URL completa do MinIO.
    /// Ex: "http://localhost:9000/ticketprime/eventos/uuid.jpg" → "eventos/uuid.jpg"
    /// </summary>
    private string ExtrairObjectName(string caminhoRelativo)
    {
        // Se for URL completa, remove o prefixo
        var prefix = $"{_publicUrl}/{_bucket}/";
        if (caminhoRelativo.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return caminhoRelativo[prefix.Length..];

        // Se for caminho relativo (subpasta/arquivo), usa direto
        if (!caminhoRelativo.Contains('/'))
            return caminhoRelativo;

        return caminhoRelativo;
    }
}
