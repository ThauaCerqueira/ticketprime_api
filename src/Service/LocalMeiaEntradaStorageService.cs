using Microsoft.Extensions.Logging;

namespace src.Service;

/// <summary>
/// Serviço de armazenamento para documentos de meia-entrada.
/// Aceita imagens (JPEG, PNG) e PDF. Salva em wwwroot/uploads/meia-entrada/.
/// </summary>
public interface IMeiaEntradaStorageService
{
    /// <summary>
    /// Salva um arquivo de documento comprobatório e retorna o caminho relativo.
    /// </summary>
    Task<string> SalvarDocumentoAsync(Stream stream, string nomeOriginal, string contentType);

    /// <summary>
    /// Lê um documento do disco e retorna seus bytes.
    /// </summary>
    Task<byte[]> LerDocumentoAsync(string caminhoRelativo);

    /// <summary>
    /// Remove um documento do disco.
    /// </summary>
    Task<bool> RemoverDocumentoAsync(string caminhoRelativo);
}

public sealed class LocalMeiaEntradaStorageService : IMeiaEntradaStorageService
{
    private static readonly HashSet<string> _tiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "application/pdf"
    };

    private static readonly long _tamanhoMaximoBytes = 10 * 1024 * 1024; // 10 MB

    private readonly string _pastaRaiz;
    private readonly ILogger<LocalMeiaEntradaStorageService> _logger;

    public LocalMeiaEntradaStorageService(IWebHostEnvironment env, ILogger<LocalMeiaEntradaStorageService> logger)
    {
        _logger = logger;
        var basePath = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        _pastaRaiz = Path.GetFullPath(Path.Combine(basePath, "uploads", "meia-entrada"));
        Directory.CreateDirectory(_pastaRaiz);
        _logger.LogInformation("LocalMeiaEntradaStorageService initialized at {PastaRaiz}", _pastaRaiz);
    }

    public async Task<string> SalvarDocumentoAsync(Stream stream, string nomeOriginal, string contentType)
    {
        // Valida tipo MIME
        if (!_tiposPermitidos.Contains(contentType))
            throw new InvalidOperationException(
                $"Tipo de arquivo não permitido: {contentType}. Aceitos: JPEG, PNG, WebP, PDF.");

        // Valida tamanho
        if (stream.Length > _tamanhoMaximoBytes)
            throw new InvalidOperationException("Arquivo excede o limite de 10 MB.");

        // Extrai extensão do content type
        var ext = contentType.ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "application/pdf" => ".pdf",
            _ => ".bin"
        };

        // UUID como nome — evita path traversal e colisões
        var nomeUuid = $"{Guid.NewGuid():N}{ext}";
        var caminhoFisico = Path.Combine(_pastaRaiz, nomeUuid);

        // Garante que o caminho final está dentro da pasta raiz (defesa contra path traversal)
        var caminhoCanonico = Path.GetFullPath(caminhoFisico);
        if (!caminhoCanonico.StartsWith(Path.GetFullPath(_pastaRaiz), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho de upload inválido.");

        stream.Position = 0;
        await using var fileStream = File.Create(caminhoFisico);
        await stream.CopyToAsync(fileStream);

        var urlRelativa = $"/uploads/meia-entrada/{nomeUuid}";
        _logger.LogInformation(
            "Documento meia-entrada salvo: {Url} ({Bytes} bytes, {ContentType})",
            urlRelativa, stream.Length, contentType);

        return urlRelativa;
    }

    public async Task<byte[]> LerDocumentoAsync(string caminhoRelativo)
    {
        var caminhoLimpo = caminhoRelativo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var caminhoFisico = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(_pastaRaiz) ?? _pastaRaiz, caminhoLimpo));

        if (!caminhoFisico.StartsWith(
                Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(_pastaRaiz) ?? _pastaRaiz, "uploads")),
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Caminho inválido.");

        if (!File.Exists(caminhoFisico))
            throw new FileNotFoundException("Documento não encontrado.", caminhoRelativo);

        return await File.ReadAllBytesAsync(caminhoFisico);
    }

    public Task<bool> RemoverDocumentoAsync(string caminhoRelativo)
    {
        try
        {
            var caminhoLimpo = caminhoRelativo.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var caminhoFisico = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(_pastaRaiz) ?? _pastaRaiz, caminhoLimpo));

            if (!caminhoFisico.StartsWith(_pastaRaiz, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Tentativa de remoção fora da pasta permitida: {Caminho}", caminhoRelativo);
                return Task.FromResult(false);
            }

            if (File.Exists(caminhoFisico))
            {
                File.Delete(caminhoFisico);
                _logger.LogInformation("Documento meia-entrada removido: {Caminho}", caminhoRelativo);
                return Task.FromResult(true);
            }

            _logger.LogWarning("Documento não encontrado para remoção: {Caminho}", caminhoRelativo);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover documento: {Caminho}", caminhoRelativo);
            return Task.FromResult(false);
        }
    }
}
