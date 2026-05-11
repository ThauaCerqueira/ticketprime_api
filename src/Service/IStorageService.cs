namespace src.Service;

/// <summary>
/// Abstração para armazenamento de arquivos (imagens, documentos).
/// Implemente para S3, Azure Blob, GCS ou armazenamento local.
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Salva um arquivo e retorna a URL pública de acesso.
    /// </summary>
    /// <param name="stream">Conteúdo do arquivo.</param>
    /// <param name="nomeArquivo">Nome desejado (sem path). A implementação pode renomear para UUID.</param>
    /// <param name="contentType">MIME type (ex: "image/jpeg").</param>
    /// <param name="subpasta">Subpasta lógica (ex: "eventos", "perfis").</param>
    Task<string> SalvarAsync(Stream stream, string nomeArquivo, string contentType, string subpasta = "geral");

    /// <summary>
    /// Remove um arquivo pelo caminho relativo retornado por SalvarAsync.
    /// </summary>
    Task<bool> RemoverAsync(string caminhoRelativo);
}
