using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using src.DTOs;
using src.Infrastructure.IRepository;
using src.Service;

namespace src.Controllers;

[ApiController]
[Route("api/reservas")]
[EnableRateLimiting("geral")]
public class ReservationController : ControllerBase
{
    private readonly ReservationService _reservationService;

    public ReservationController(ReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    /// <summary>
    /// Compra um ingresso (requer autenticação).
    /// </summary>
    [HttpPost]
    [Authorize]
    [EnableRateLimiting("compra-ingresso")]
    public async Task<IResult> Comprar([FromBody] PurchaseTicketDto dto)
    {
        try
        {
            var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(cpf))
                return Results.Unauthorized();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers.UserAgent.FirstOrDefault();

            var reserva = await _reservationService.ComprarIngressoAsync(
                cpf, dto.EventoId, dto.TicketTypeId, dto.CupomUtilizado, dto.ContratarSeguro, dto.EhMeiaEntrada,
                ipAddress, userAgent,
                dto.MetodoPagamento, dto.CardToken, dto.Ultimos4Cartao, dto.NomeTitular, dto.ValidadeCartao,
                dto.DocumentoBase64, dto.DocumentoNome, dto.DocumentoContentType);
            return Results.Created($"/api/reservas/{reserva.Id}", new
            {
                mensagem = "Ingresso comprado com sucesso!",
                reservaId = reserva.Id,
                codigoIngresso = reserva.CodigoIngresso,
                eventoId = reserva.EventoId,
                valorPago = reserva.ValorFinalPago,
                cupomUtilizado = reserva.CupomUtilizado,
                metodoPagamento = dto.MetodoPagamento,
                chavePix = reserva.ChavePix
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>
    /// Lista reservas do usuário autenticado.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IResult> Listar()
    {
        var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        var reservas = await _reservationService.ListarReservasUsuarioAsync(cpf);
        return Results.Ok(reservas);
    }

    /// <summary>
    /// Retorna os detalhes de um ingresso específico do usuário autenticado.
    /// </summary>
    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<IResult> ObterPorId(int id)
    {
        var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        var reserva = await _reservationService.ObterDetalhadaPorIdAsync(id, cpf);
        if (reserva == null)
            return Results.NotFound(new { mensagem = "Ingresso não encontrado." });

        return Results.Ok(reserva);
    }

    /// <summary>
    /// Lista reservas de um CPF específico (usuário só vê as suas; admin vê qualquer).
    /// </summary>
    [HttpGet("{cpf}")]
    [Authorize]
    public async Task<IResult> ListarPorCpf(string cpf)
    {
        var tokenCpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (tokenCpf != cpf && !HttpContext.User.IsInRole("ADMIN"))
            return Results.Forbid();

        var reservas = await _reservationService.ListarReservasUsuarioAsync(cpf);
        return Results.Ok(reservas);
    }

    /// <summary>
    /// Termo de devolução: calcula o reembolso antes do cancelamento.
    /// </summary>
    [HttpGet("{id:int}/termo-devolucao")]
    [Authorize]
    public async Task<IResult> TermoDevolucao(int id)
    {
        var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        try
        {
            var reserva = await _reservationService.ObterDetalheCancelamentoAsync(id, cpf);

            var regraReembolso = reserva.TemSeguro
                ? "Com seguro de devolução: reembolso integral do ingresso e da taxa de serviço. O custo do seguro não é reembolsado."
                : "Sem seguro: reembolso apenas do valor do ingresso. A taxa de serviço não é devolvida.";

            return Results.Ok(new
            {
                reservaId       = reserva.Id,
                eventoNome      = reserva.Nome,
                dataEvento      = reserva.DataEvento,
                valorPago       = reserva.ValorFinalPago,
                taxaServico     = reserva.TaxaServicoPago,
                temSeguro       = reserva.TemSeguro,
                valorSeguro     = reserva.ValorSeguroPago,
                valorDevolvivel = reserva.ValorDevolvivel,
                regraReembolso  = regraReembolso
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.NotFound(new { mensagem = ex.Message });
        }
    }

    /// <summary>
    /// Cancela um ingresso (devolução).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> Cancelar(int id)
    {
        try
        {
            var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(cpf))
                return Results.Unauthorized();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = HttpContext.Request.Headers.UserAgent.FirstOrDefault();

            await _reservationService.CancelarIngressoAsync(id, cpf, ipAddress, userAgent);
            return Results.Ok(new { mensagem = "Ingresso cancelado com sucesso! Vaga devolvida ao evento." });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>
    /// Transfere a propriedade de um ingresso ativo para outro usuário cadastrado.
    /// </summary>
    [HttpPost("{id}/transferir")]
    [Authorize]
    [EnableRateLimiting("escrita")]
    public async Task<IResult> Transferir(int id, [FromBody] TransferirIngressoDto dto)
    {
        try
        {
            var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(cpf))
                return Results.Unauthorized();

            // Sanitize: only digits allowed in CPF
            var cpfDest = new string(dto.CpfDestinatario.Where(char.IsDigit).ToArray());
            if (cpfDest.Length != 11)
                return Results.BadRequest(new { mensagem = "CPF do destinatário inválido." });

            await _reservationService.TransferirIngressoAsync(id, cpf, cpfDest);
            return Results.Ok(new { mensagem = "Ingresso transferido com sucesso!" });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { mensagem = ex.Message });
        }
    }

    /// <summary>
    /// Consulta o status atual do pagamento de uma reserva junto ao gateway.
    /// Permite reconciliar pagamentos PIX (que podem ficar pendentes),
    /// cartões recusados, ou qualquer transação que precise de verificação posterior.
    /// </summary>
    [HttpGet("{id:int}/status-pagamento")]
    [Authorize]
    public async Task<IResult> StatusPagamento(int id)
    {
        var cpf = HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(cpf))
            return Results.Unauthorized();

        var status = await _reservationService.ConsultarStatusPagamentoAsync(id, cpf);

        if (status == null)
            return Results.NotFound(new { mensagem = "Ingresso não encontrado." });

        if (!status.Sucesso)
            return Results.BadRequest(new { mensagem = status.MensagemErro });

        return Results.Ok(new
        {
            reservaId = id,
            statusPagamento = status.Status.ToString(),
            statusGateway = status.StatusGateway,
            mensagem = status.Status switch
            {
                PaymentGatewayStatus.Approved => "Pagamento aprovado.",
                PaymentGatewayStatus.Pending  => "Pagamento pendente. Para PIX, aguardando pagamento.",
                PaymentGatewayStatus.Rejected => "Pagamento recusado pela operadora.",
                PaymentGatewayStatus.Cancelled => "Pagamento cancelado (PIX expirou ou transação cancelada).",
                PaymentGatewayStatus.Refunded  => "Pagamento estornado/reembolsado.",
                _ => "Status de pagamento desconhecido."
            }
        });
    }
}
