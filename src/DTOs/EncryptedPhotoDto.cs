namespace src.DTOs;

/// <summary>
/// Pacote de uma foto criptografada ponta a ponta (E2E).
/// Mapeado diretamente da tabela EventoFotos e do frontend FotoCriptografada.
/// </summary>
public class EncryptedPhotoDto
{
    /// <summary>Conteúdo cifrado (AES-GCM ciphertext + auth-tag) em Base64.</summary>
    public string CiphertextBase64 { get; set; } = string.Empty;

    /// <summary>IV de 12 bytes usado no AES-GCM, em Base64.</summary>
    public string IvBase64 { get; set; } = string.Empty;

    /// <summary>Chave AES-GCM empacotada via AES-KW (segredo ECDH), em Base64.</summary>
    public string ChaveAesCifradaBase64 { get; set; } = string.Empty;

    /// <summary>Chave pública ECDH P-256 do organizador (JSON JWK serializado).</summary>
    public string ChavePublicaOrgJwk { get; set; } = string.Empty;

    /// <summary>SHA-256 do nome original do arquivo, em Base64.</summary>
    public string HashNomeOriginal { get; set; } = string.Empty;

    /// <summary>Tipo MIME original (image/jpeg, image/png, image/webp).</summary>
    public string TipoMime { get; set; } = string.Empty;

    /// <summary>Tamanho em bytes do arquivo original (antes da criptografia).</summary>
    public long TamanhoBytes { get; set; }

    /// <summary>Indica que a criptografia foi concluída com sucesso.</summary>
    public bool Criptografada { get; set; }

    /// <summary>
    /// Thumbnail redimensionado (JPEG, max 400px largura) em Base64 (sem prefixo).
    /// Armazenado sem criptografia para exibição na vitrine pública.
    /// </summary>
    public string? ThumbnailBase64 { get; set; }
}
