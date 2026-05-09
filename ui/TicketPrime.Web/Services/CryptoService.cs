using Microsoft.JSInterop;
using System.Text.Json;
using TicketPrime.Web.Models;

namespace TicketPrime.Web.Services;

/// <summary>
/// Encapsula as chamadas JavaScript para o módulo de criptografia (crypto.js).
/// Toda a criptografia real é executada no navegador via Web Crypto API.
/// </summary>
public sealed class CryptoService : IAsyncDisposable
{
    private readonly IJSRuntime _js;

    private string? _organizerPublicKeyJwk;
    private bool    _initialized;

    public CryptoService(IJSRuntime js) => _js = js;

    /// <summary>Chave pública ECDH P-256 do organizador (JWK serializado), disponível após
    /// <see cref="InicializarAsync"/>.</summary>
    public string? OrganizadorChavePublica => _organizerPublicKeyJwk;

    /// <summary>Indica se o módulo foi inicializado com sucesso.</summary>
    public bool Inicializado => _initialized;

    // ─────────────────────────────────────────────────────────────────────────
    // Inicialização
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gera o par de chaves ECDH P-256 do organizador no navegador e configura a chave
    /// pública do servidor (demo).  Deve ser chamado em <c>OnAfterRenderAsync(firstRender)</c>.
    /// </summary>
    public async Task InicializarAsync()
    {
        try
        {
            var result = await _js.InvokeAsync<JsonElement>("ticketPrimeCrypto.init");
            _organizerPublicKeyJwk = result.GetProperty("organizerPublicKey").GetString();
            _initialized = true;
        }
        catch (JSException ex)
        {
            throw new InvalidOperationException(
                "Falha ao inicializar o módulo de criptografia. " +
                "Verifique se o navegador suporta a Web Crypto API.", ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Criptografia de imagem
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cifra uma imagem no navegador usando AES-GCM-256.
    /// A chave AES é empacotada (wrapped) com o segredo ECDH derivado entre o par do
    /// organizador e a chave pública do servidor.
    /// </summary>
    /// <param name="imageBase64">Bytes da imagem original em Base64 (sem prefixo data:...).</param>
    /// <param name="mimeType">Tipo MIME (image/jpeg | image/png | image/webp).</param>
    /// <param name="nomeArquivo">Nome original do arquivo (usado para gerar o hash).</param>
    /// <param name="tamanhoBytes">Tamanho do arquivo original em bytes.</param>
    /// <returns><see cref="FotoCriptografada"/> pronta para ser incluída no <see cref="PacoteImagem"/>.</returns>
    public async Task<FotoCriptografada> CriptografarImagemAsync(
        string imageBase64,
        string mimeType,
        string nomeArquivo,
        long   tamanhoBytes)
    {
        EnsureInitialized();

        var result = await _js.InvokeAsync<JsonElement>(
            "ticketPrimeCrypto.encryptImage",
            imageBase64,
            mimeType,
            nomeArquivo,
            tamanhoBytes);

        return new FotoCriptografada
        {
            CiphertextBase64      = result.GetProperty("ciphertextBase64").GetString()      ?? string.Empty,
            IvBase64              = result.GetProperty("ivBase64").GetString()              ?? string.Empty,
            ChaveAesCifradaBase64 = result.GetProperty("wrappedKeyBase64").GetString()      ?? string.Empty,
            ChavePublicaOrgJwk    = result.GetProperty("organizerPublicKeyJwk").GetString() ?? string.Empty,
            HashNomeOriginal      = result.GetProperty("fileNameHash").GetString()          ?? string.Empty,
            TipoMime              = mimeType,
            TamanhoBytes          = tamanhoBytes,
            Criptografada         = true
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException(
                "CryptoService não foi inicializado. Chame InicializarAsync() primeiro.");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
