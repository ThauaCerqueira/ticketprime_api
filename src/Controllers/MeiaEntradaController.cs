using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.DTOs;
using src.Infrastructure.IRepository;
using src.Service;

namespace src.Controllers;

/// <summary>
/// Controller para gerenciamento de documentos comprobatórios de meia-entrada
/// (Lei 12.933/2013). Permite ao ADMIN listar documentos pendentes, visualizar
/// arquivos e aprovar/rejeitar documentos.
/// </summary>
[ApiController]
[Route("api/meia-entrada")]
[Authorize(Roles = "ADMIN")]
[EnableRateLimiting("geral")]
public class MeiaEntradaController : ControllerBase
{
    private readonly IMeiaEntradaRepository _repository;
    private readonly IMeiaEntradaStorageService _storageService;
    private readonly ILogger<MeiaEntradaController> _logger;

    public MeiaEntradaController(
        IMeiaEntradaRepository repository,
        IMeiaEntradaStorageService storageService,
        ILogger<MeiaEntradaController> logger)
    {
        _repository = repository;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>
    /// Lista todos os documentos pendentes de verificação.
    /// </summary>
    [HttpGet("pendentes")]
    [ProducesResponseType(typeof(MeiaEntradaPendenteDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarPendentes()
    {
        var documentos = await _repository.ListarPendentesAsync();
        var pendentes = new MeiaEntradaPendenteDto
        {
            QuantidadePendentes = documentos.Count,
            Documentos = documentos
        };
        return Ok(pendentes);
    }

    /// <summary>
    /// Lista todos os documentos (histórico completo), opcionalmente filtrados por status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<MeiaEntradaDocumentoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarTodos([FromQuery] string? status = null)
    {
        var documentos = await _repository.ListarTodosAsync(status);
        return Ok(documentos);
    }

    /// <summary>
    /// Obtém a contagem de documentos pendentes.
    /// </summary>
    [HttpGet("contagem")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    public async Task<IActionResult> ContarPendentes()
    {
        var count = await _repository.ContarPendentesAsync();
        return Ok(count);
    }

    /// <summary>
    /// Visualiza/download do documento comprobatório.
    /// Retorna o arquivo original com o MIME type correto.
    /// </summary>
    [HttpGet("{id:int}/arquivo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VisualizarArquivo(int id)
    {
        var documento = await _repository.ObterPorIdAsync(id);
        if (documento == null)
            return NotFound(new { mensagem = "Documento não encontrado." });

        try
        {
            var bytes = await _storageService.LerDocumentoAsync(documento.CaminhoArquivo);
            return File(bytes, documento.TipoMime, documento.NomeOriginal);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { mensagem = "Arquivo físico do documento não encontrado no servidor." });
        }
    }

    /// <summary>
    /// Aprova ou rejeita um documento de meia-entrada.
    /// </summary>
    [HttpPost("{id:int}/verificar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerificarDocumento(int id, [FromBody] MeiaEntradaVerificacaoDto verificacao)
    {
        if (verificacao.Status != "Aprovado" && verificacao.Status != "Rejeitado")
            return BadRequest(new { mensagem = "Status inválido. Use 'Aprovado' ou 'Rejeitado'." });

        if (verificacao.Status == "Rejeitado" && string.IsNullOrWhiteSpace(verificacao.MotivoRejeicao))
            return BadRequest(new { mensagem = "É obrigatório informar o motivo da rejeição." });

        var documento = await _repository.ObterPorIdAsync(id);
        if (documento == null)
            return NotFound(new { mensagem = "Documento não encontrado." });

        if (documento.Status != "Pendente")
            return BadRequest(new { mensagem = $"Este documento já foi {documento.Status.ToLower()}." });

        // Obtém o CPF do admin logado
        var adminCpf = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown";

        await _repository.AtualizarStatusAsync(id, verificacao.Status, adminCpf, verificacao.MotivoRejeicao);

        _logger.LogInformation(
            "Documento meia-entrada {DocumentoId} {Status} por admin {AdminCpf}" +
            (verificacao.Status == "Rejeitado" ? ". Motivo: {Motivo}" : ""),
            id, verificacao.Status, adminCpf, verificacao.MotivoRejeicao);

        return Ok(new { mensagem = $"Documento {verificacao.Status.ToLower()} com sucesso." });
    }

    /// <summary>
    /// Obtém os detalhes de um documento específico, incluindo o conteúdo em Base64.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MeiaEntradaDocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterDetalhes(int id)
    {
        var documento = await _repository.ObterPorIdAsync(id);
        if (documento == null)
            return NotFound(new { mensagem = "Documento não encontrado." });

        // Carrega os dados de exibição da listagem
        var pendentes = await _repository.ListarTodosAsync();
        var dto = pendentes.FirstOrDefault(d => d.Id == id);

        if (dto == null)
        {
            // Se não estiver na listagem geral, monta manualmente
            dto = new MeiaEntradaDocumentoDto
            {
                Id = documento.Id,
                ReservaId = documento.ReservaId,
                UsuarioCpf = documento.UsuarioCpf,
                EventoId = documento.EventoId,
                NomeOriginal = documento.NomeOriginal,
                TipoMime = documento.TipoMime,
                TamanhoBytes = documento.TamanhoBytes,
                Status = documento.Status,
                DataUpload = documento.DataUpload,
                DataVerificacao = documento.DataVerificacao,
                VerificadoPorCpf = documento.VerificadoPorCpf,
                MotivoRejeicao = documento.MotivoRejeicao
            };
        }

        // Tenta carregar o conteúdo do arquivo em Base64
        try
        {
            var bytes = await _storageService.LerDocumentoAsync(documento.CaminhoArquivo);
            dto.ConteudoBase64 = Convert.ToBase64String(bytes);
        }
        catch (FileNotFoundException)
        {
            // Documento sem arquivo físico — não inclui conteúdo
            _logger.LogWarning("Arquivo físico não encontrado para documento {DocumentoId}: {Caminho}",
                id, documento.CaminhoArquivo);
        }

        return Ok(dto);
    }
}
