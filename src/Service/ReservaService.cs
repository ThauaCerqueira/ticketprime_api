using Microsoft.Extensions.Logging;
using src.Models;
using src.Infrastructure.IRepository;
using src.Infrastructure;
using src.DTOs;
using Dapper;

namespace src.Service;

public class ReservationService
{
    private readonly IReservaRepository _reservaRepository;
    private readonly IEventoRepository _eventoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ICupomRepository _cupomRepository;
    private readonly ITransacaoCompraExecutor _transacaoExecutor;
    private readonly DbConnectionFactory _connectionFactory;
    private readonly EmailTemplateService _emailTemplate;
    private readonly ILogger<ReservationService> _logger;
    private readonly IWaitingQueueService _waitingQueueService;
    private readonly AuditLogService _auditLog;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IMeiaEntradaRepository _meiaEntradaRepository;
    private readonly IMeiaEntradaStorageService _meiaEntradaStorageService;
    private readonly PixCryptoService _pixCryptoService;

    public ReservationService(
        IReservaRepository reservaRepository,
        IEventoRepository eventoRepository,
        IUsuarioRepository usuarioRepository,
        ICupomRepository cupomRepository,
        ITransacaoCompraExecutor transacaoExecutor,
        DbConnectionFactory connectionFactory,
        EmailTemplateService emailTemplate,
        ILogger<ReservationService> logger,
        IWaitingQueueService waitingQueueService,
        AuditLogService auditLog,
        IPaymentGateway paymentGateway,
        IMeiaEntradaRepository meiaEntradaRepository,
        IMeiaEntradaStorageService meiaEntradaStorageService,
        PixCryptoService pixCryptoService)
    {
        _reservaRepository = reservaRepository;
        _eventoRepository = eventoRepository;
        _usuarioRepository = usuarioRepository;
        _cupomRepository = cupomRepository;
        _transacaoExecutor = transacaoExecutor;
        _connectionFactory = connectionFactory;
        _emailTemplate = emailTemplate;
        _logger = logger;
        _waitingQueueService = waitingQueueService;
        _auditLog = auditLog;
        _paymentGateway = paymentGateway;
        _meiaEntradaRepository = meiaEntradaRepository;
        _meiaEntradaStorageService = meiaEntradaStorageService;
        _pixCryptoService = pixCryptoService;
    }

    public async Task<Reservation> ComprarIngressoAsync(
        string usuarioCpf,
        int eventoId,
        int ticketTypeId,
        string? cupomUtilizado = null,
        bool contratarSeguro = false,
        bool ehMeiaEntrada = false,
        string? ipAddress = null,
        string? userAgent = null,
        string metodoPagamento = "pix",
        string? cardToken = null,
        string? ultimos4Cartao = null,
        string? nomeTitular = null,
        string? validadeCartao = null,
        string? documentoBase64 = null,
        string? documentoNome = null,
        string? documentoContentType = null)
    {
        // R1 - Validação de Integridade
        var usuario = await _usuarioRepository.ObterPorCpf(usuarioCpf);
        if (usuario == null)
            throw new InvalidOperationException("Usuário não encontrado.");

        if (!usuario.EmailVerificado)
            throw new InvalidOperationException("É necessário verificar seu email antes de comprar ingressos. Verifique sua caixa de entrada.");

        var evento = await _eventoRepository.ObterPorIdAsync(eventoId);
        if (evento == null)
            throw new InvalidOperationException("Evento não encontrado.");

        if (evento.DataEvento <= DateTime.Now)
            throw new InvalidOperationException("Este evento já aconteceu.");

        // R1a - Carrega o tipo de ingresso (setor) escolhido
        var ticketType = await _eventoRepository.ObterTipoIngressoPorIdAsync(ticketTypeId);
        if (ticketType == null)
            throw new InvalidOperationException("Tipo de ingresso não encontrado.");

        if (ticketType.EventoId != eventoId)
            throw new InvalidOperationException("Este tipo de ingresso não pertence ao evento selecionado.");

        if (ticketType.CapacidadeRestante <= 0)
            throw new InvalidOperationException("Não há mais vagas disponíveis para este setor.");

        // R1b - Validação de meia-entrada
        if (ehMeiaEntrada && !evento.TemMeiaEntrada)
            throw new InvalidOperationException("Este evento não oferece meia-entrada.");

        // R1b1 - Validação de documento comprobatório (Lei 12.933/2013)
        if (ehMeiaEntrada && string.IsNullOrEmpty(documentoBase64))
            throw new InvalidOperationException(
                "Para comprar meia-entrada é obrigatório enviar um documento comprobatório " +
                "(carteirinha estudantil, identidade de idoso, laudo médico, etc.). " +
                "(Lei 12.933/2013)");

        if (!ehMeiaEntrada && !string.IsNullOrEmpty(documentoBase64))
            throw new InvalidOperationException(
                "Documento comprobatório enviado para compra de inteira. " +
                "Remova o arquivo ou selecione meia-entrada.");

        // R1c - Preço base: preço do tipo de ingresso (setor) ou 50% para meia-entrada
        var precoBase = ehMeiaEntrada ? ticketType.Preco / 2 : ticketType.Preco;

        // R2 - Pré-verificação do limite (falha rápida; confirmada com UPDLOCK dentro da transação)
        var reservasCpfPreCheck = await _reservaRepository.ContarReservasUsuarioPorEventoAsync(usuarioCpf, eventoId);
        if (reservasCpfPreCheck >= evento.LimiteIngressosPorUsuario)
            throw new InvalidOperationException($"Você já atingiu o limite de {evento.LimiteIngressosPorUsuario} reservas para este evento.");

        // R4 - Validação do cupom antes de abrir transação
        decimal valorIngresso = precoBase;
        Coupon? cupom = null;
        bool aplicarDesconto = false;

        if (!string.IsNullOrEmpty(cupomUtilizado))
        {
            cupom = await _cupomRepository.ObterPorCodigoAsync(cupomUtilizado);
            if (cupom == null)
                throw new InvalidOperationException("Cupom inválido ou inexistente.");

            if (!cupom.EstaValido())
                throw new InvalidOperationException("Cupom expirado ou com limite de usos atingido.");

            // R4a - Validação de categoria: se o cupom tem CategoriaEvento, o evento deve corresponder
            if (!string.IsNullOrEmpty(cupom.CategoriaEvento))
            {
                var catCupom = cupom.CategoriaEvento.Trim().ToLowerInvariant();
                var catEvento = (evento.GeneroMusical ?? "").Trim().ToLowerInvariant();
                if (catCupom != catEvento)
                    throw new InvalidOperationException(
                        $"Este cupom é válido apenas para eventos da categoria \"{cupom.CategoriaEvento}\".");
            }

            // R4b - Validação de primeiro acesso: usuário não pode ter compras anteriores
            if (cupom.PrimeiroAcesso)
            {
                var totalReservas = await _reservaRepository.ContarReservasUsuarioAsync(usuarioCpf);
                if (totalReservas > 0)
                    throw new InvalidOperationException(
                        "Este cupom é válido apenas para a primeira compra.");
            }

            if (precoBase >= cupom.ValorMinimoRegra)
            {
                decimal desconto;
                if (cupom.TipoDesconto == Models.DiscountType.ValorFixo && cupom.ValorDescontoFixo.HasValue)
                {
                    // Desconto de valor fixo (ex: R$ 20 de desconto)
                    desconto = cupom.ValorDescontoFixo.Value;
                }
                else
                {
                    // Desconto percentual (ex: 10% de desconto)
                    desconto = precoBase * (cupom.PorcentagemDesconto / 100);
                }
                valorIngresso = Math.Max(0, precoBase - desconto);
                aplicarDesconto = true;
            }
        }

        // R5 - Taxa de serviço
        decimal taxaServico = evento.TaxaServico;

        // R6 - Seguro de devolução (calculado sobre o preço base do tipo de ingresso)
        decimal valorSeguro = contratarSeguro ? precoBase * 0.15m : 0m;

        var reserva = new Reservation
        {
            UsuarioCpf       = usuarioCpf,
            EventoId         = eventoId,
            TicketTypeId     = ticketTypeId,
            CupomUtilizado   = cupomUtilizado,
            TaxaServicoPago  = taxaServico,
            TemSeguro        = contratarSeguro,
            ValorSeguroPago  = valorSeguro,
            EhMeiaEntrada    = ehMeiaEntrada,
            ValorFinalPago   = valorIngresso + taxaServico + valorSeguro
        };

        // ── Processa pagamento antes de criar a reserva ────────────────
        if (reserva.ValorFinalPago > 0)
        {
            // Gera chave de idempotência UMA VEZ por tentativa de compra.
            // Em caso de timeout + retry, a mesma chave é reenviada ao gateway,
            // garantindo que o MercadoPago não cobre duas vezes.
            var idempotencyKey = Guid.NewGuid().ToString();
            reserva.IdempotencyKey = idempotencyKey;

            var payReq = new PaymentRequest
            {
                MetodoPagamento = metodoPagamento,
                Valor           = reserva.ValorFinalPago,
                Descricao       = $"Ingresso: {evento.Nome}",
                CardToken       = cardToken,
                Ultimos4Cartao  = ultimos4Cartao,
                NomeTitular     = nomeTitular,
                ValidadeCartao  = validadeCartao,
                PagadorEmail    = usuario.Email,
                IdempotencyKey  = idempotencyKey
            };
            var payResult = await _paymentGateway.ProcessarAsync(payReq);
            if (!payResult.Sucesso)
                throw new InvalidOperationException($"Pagamento recusado: {payResult.MensagemErro}");

            // Persiste o código da transação para possibilitar estornos futuros
            reserva.CodigoTransacaoGateway = payResult.CodigoTransacao;

            // Para PIX, captura o QR Code (texto "copia e cola") e
            // CRIPTOGRAFA antes de persistir no banco (AES-256-GCM).
            // A descriptografia ocorre antes de retornar ao frontend.
            reserva.ChavePix = !string.IsNullOrEmpty(payResult.ChavePix)
                ? _pixCryptoService.Encrypt(payResult.ChavePix)
                : null;
        }

        // R2 + R3 + Incremento cupom + Criação de reserva — delega para executor transacional
        var reservaCriada = await _transacaoExecutor.ExecutarTransacaoAsync(
            reserva, evento, cupomUtilizado, aplicarDesconto, ticketTypeId, loteId: null);

        // ── Envia confirmação de compra por email com QR Code ───────────
        try
        {
            var qrCode = EmailTemplateService.GerarQrCodeBase64(reservaCriada.CodigoIngresso);
            await _emailTemplate.SendPurchaseConfirmationAsync(
                to: usuario.Email,
                nomeCliente: usuario.Nome,
                eventoNome: evento.Nome,
                dataEvento: evento.DataEvento,
                local: evento.Local ?? "A definir",
                valorPago: reservaCriada.ValorFinalPago,
                codigoIngresso: reservaCriada.CodigoIngresso,
                qrCodeBase64: qrCode);
        }
        catch (Exception ex)
        {
            // Falha no envio de email não deve impedir a compra
            _logger.LogWarning(ex, "Falha ao enviar email de confirmação de compra para {Email}", usuario.Email);
        }

        // ── Registra auditoria financeira da compra ────────────────────
        decimal valorDesconto = 0;
        if (aplicarDesconto && cupom != null)
        {
            valorDesconto = precoBase - valorIngresso;
        }
        await _auditLog.LogCompraIngressoAsync(
            usuarioCpf: usuarioCpf,
            eventoId: eventoId,
            reservaId: reservaCriada.Id,
            valorFinalPago: reservaCriada.ValorFinalPago,
            taxaServico: taxaServico,
            valorSeguro: valorSeguro,
            cupomUtilizado: cupomUtilizado,
            valorDesconto: aplicarDesconto ? valorDesconto : null,
            ipAddress: ipAddress ?? "unknown",
            userAgent: userAgent);

        // ── Salva documento comprobatório de meia-entrada ────────────
        if (ehMeiaEntrada && !string.IsNullOrEmpty(documentoBase64))
        {
            try
            {
                var bytes = Convert.FromBase64String(documentoBase64);
                using var ms = new MemoryStream(bytes);
                var caminho = await _meiaEntradaStorageService.SalvarDocumentoAsync(
                    ms,
                    documentoNome ?? "documento.pdf",
                    documentoContentType ?? "application/pdf");

                var doc = new MeiaEntradaDocumento
                {
                    ReservaId = reservaCriada.Id,
                    UsuarioCpf = usuarioCpf,
                    EventoId = eventoId,
                    CaminhoArquivo = caminho,
                    NomeOriginal = documentoNome ?? "documento",
                    TipoMime = documentoContentType ?? "application/pdf",
                    TamanhoBytes = bytes.Length,
                    Status = "Pendente",
                    DataUpload = DateTime.UtcNow
                };

                await _meiaEntradaRepository.InserirAsync(doc);
                _logger.LogInformation(
                    "Documento meia-entrada salvo para reserva {ReservaId}: {Caminho} ({Bytes} bytes)",
                    reservaCriada.Id, caminho, bytes.Length);
            }
            catch (Exception ex)
            {
                // Falha no salvamento do documento não impede a compra,
                // mas o documento precisará ser enviado posteriormente.
                _logger.LogWarning(ex,
                    "Falha ao salvar documento de meia-entrada para reserva {ReservaId}. " +
                    "O usuário precisará enviar o documento posteriormente.",
                    reservaCriada.Id);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // SEGURANÇA: Descriptografa a ChavePix antes de retornar ao frontend.
        //   No banco, a ChavePix está criptografada com AES-256-GCM.
        //   O usuário vê o QR Code Pix apenas uma vez neste response.
        // ═══════════════════════════════════════════════════════════════════
        if (!string.IsNullOrEmpty(reservaCriada.ChavePix))
        {
            reservaCriada.ChavePix = _pixCryptoService.Decrypt(reservaCriada.ChavePix);
        }

        return reservaCriada;
    }

    public async Task<IEnumerable<ReservationDetailDto>> ListarReservasUsuarioAsync(string cpf)
    {
        return await _reservaRepository.ListarPorUsuarioAsync(cpf);
    }

    public async Task<ReservationDetailDto?> ObterDetalhadaPorIdAsync(int reservaId, string usuarioCpf)
    {
        return await _reservaRepository.ObterDetalhadaPorIdAsync(reservaId, usuarioCpf);
    }

    public async Task<ReservationDetailDto> ObterDetalheCancelamentoAsync(int reservaId, string usuarioCpf)
    {
        var detalhe = await _reservaRepository.ObterDetalhadaPorIdAsync(reservaId, usuarioCpf)
            ?? throw new InvalidOperationException("Reserva não encontrada.");

        return detalhe;
    }

    public async Task CancelarIngressoAsync(
        int reservaId,
        string usuarioCpf,
        string? ipAddress = null,
        string? userAgent = null)
    {
        var reserva = await _reservaRepository.ObterDetalhadaPorIdAsync(reservaId, usuarioCpf)
            ?? throw new InvalidOperationException("Reserva não encontrada.");

        if (reserva.DataEvento <= DateTime.Now.AddHours(48))
            throw new InvalidOperationException("Cancelamento não permitido: o prazo de 48 horas antes do evento foi ultrapassado.");

        // Cancelamento atômico com UPDLOCK: soft delete (UPDATE Status) + UPDATE vagas +
        // restauração do cupom na mesma transação
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            // UPDLOCK previne race conditions com outras transações concorrentes.
            // Soft delete: marca como Cancelada em vez de deletar fisicamente.
            var rows = await connection.ExecuteAsync(
                @"UPDATE Reservas WITH (UPDLOCK, ROWLOCK)
                  SET Status = 'Cancelada',
                      DataCancelamento = GETUTCDATE(),
                      MotivoCancelamento = 'Cancelado pelo usuário'
                  WHERE Id = @Id AND UsuarioCpf = @Cpf AND Status = 'Ativa'",
                new { Id = reservaId, Cpf = usuarioCpf }, transaction);

            if (rows == 0)
                throw new InvalidOperationException("Não foi possível cancelar a reserva.");

            await connection.ExecuteAsync(
                "UPDATE Eventos SET CapacidadeRestante = CapacidadeRestante + 1 WHERE Id = @Id",
                new { Id = reserva.EventoId }, transaction);

            // Restaura o uso do cupom se foi utilizado na compra
            if (!string.IsNullOrEmpty(reserva.CupomUtilizado))
            {
                await connection.ExecuteAsync(
                    @"UPDATE Cupons SET TotalUsado = CASE
                        WHEN TotalUsado > 0 THEN TotalUsado - 1
                        ELSE 0 END
                      WHERE Codigo = @Codigo",
                    new { Codigo = reserva.CupomUtilizado }, transaction);
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        // ── Estorno automático via gateway ─────────────────────────────
        var valorDevolvivel = reserva.TemSeguro
            ? reserva.ValorFinalPago - reserva.ValorSeguroPago
            : reserva.ValorFinalPago - reserva.TaxaServicoPago;

        if (valorDevolvivel > 0 && !string.IsNullOrWhiteSpace(reserva.CodigoTransacaoGateway))
        {
            try
            {
                var estornoResult = await _paymentGateway.EstornarAsync(
                    reserva.CodigoTransacaoGateway,
                    valorDevolvivel,
                    "Cancelamento pelo usuário");

                if (estornoResult.Sucesso)
                {
                    using var conn2 = _connectionFactory.CreateConnection();
                    await conn2.ExecuteAsync(
                        @"UPDATE Reservas
                          SET IdEstornoGateway = @IdEstorno, DataEstorno = GETUTCDATE()
                          WHERE Id = @Id",
                        new { IdEstorno = estornoResult.IdEstorno, Id = reservaId });

                    _logger.LogInformation(
                        "Estorno processado. ReservaId={Id}, EstornoId={Estorno}, Valor={Valor}",
                        reservaId, estornoResult.IdEstorno, valorDevolvivel);
                }
                else
                {
                    _logger.LogWarning(
                        "Estorno não processado pelo gateway. ReservaId={Id}, Erro={Erro}",
                        reservaId, estornoResult.MensagemErro);
                }
            }
            catch (Exception ex)
            {
                // Falha no estorno não desfaz o cancelamento — deve ser resolvido manualmente
                _logger.LogError(ex,
                    "Erro ao processar estorno para ReservaId={Id}. Requer intervenção manual.",
                    reservaId);
            }
        }

        // ── Notifica o próximo da fila de espera ───────────────────────
        try
        {
            await _waitingQueueService.NotificarProximoDaFilaAsync(reserva.EventoId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao notificar fila de espera após cancelamento da reserva {ReservaId}", reservaId);
        }

        // ── Envia email de confirmação de cancelamento ─────────────────
        try
        {
            var usuario = await _usuarioRepository.ObterPorCpf(usuarioCpf);
            if (usuario != null)
            {
                await _emailTemplate.SendCancellationConfirmationAsync(
                    to: usuario.Email,
                    nomeCliente: usuario.Nome,
                    eventoNome: reserva.Nome,
                    dataEvento: reserva.DataEvento,
                    valorDevolvido: reserva.ValorDevolvivel);
            }
        }
        catch (Exception ex)
        {
            // Falha no envio de email não deve impedir o cancelamento
            _logger.LogWarning(ex, "Falha ao enviar email de cancelamento para {Cpf}", usuarioCpf);
        }

        // ── Registra auditoria financeira do cancelamento ──────────────
        await _auditLog.LogCancelamentoIngressoAsync(
            usuarioCpf: usuarioCpf,
            eventoId: reserva.EventoId,
            reservaId: reservaId,
            valorDevolvido: reserva.ValorDevolvivel,
            ipAddress: ipAddress ?? "unknown",
            userAgent: userAgent);
    }

    /// <summary>
    /// Transfere a propriedade de um ingresso ativo para outro usuário cadastrado.
    /// O destinatário deve ser um usuário existente e diferente do remetente.
    /// </summary>
    public async Task TransferirIngressoAsync(int reservaId, string cpfRemetente, string cpfDestinatario)
    {
        if (cpfRemetente == cpfDestinatario)
            throw new InvalidOperationException("O destinatário deve ser um usuário diferente.");

        var destinatario = await _usuarioRepository.ObterPorCpf(cpfDestinatario)
            ?? throw new InvalidOperationException("Usuário destinatário não encontrado.");

        // Garante que o ingresso pertence ao remetente e está ativo
        var reserva = await _reservaRepository.ObterDetalhadaPorIdAsync(reservaId, cpfRemetente)
            ?? throw new InvalidOperationException("Ingresso não encontrado ou não pertence a você.");

        if (reserva.Status != "Ativa")
            throw new InvalidOperationException("Somente ingressos ativos podem ser transferidos.");

        var ok = await _reservaRepository.TransferirAsync(reservaId, cpfRemetente, cpfDestinatario);
        if (!ok)
            throw new InvalidOperationException("Não foi possível realizar a transferência.");

        _logger.LogInformation("Ingresso {ReservaId} transferido de {Remetente} para {Destinatario}",
            reservaId, cpfRemetente, cpfDestinatario);
    }

    /// <summary>
    /// Consulta o status atual do pagamento de uma reserva junto ao gateway.
    /// Utilizado para reconciliar pagamentos PIX (que podem ficar pendentes),
    /// cartões recusados, ou qualquer transação que precise de verificação posterior.
    /// </summary>
    /// <param name="reservaId">ID da reserva.</param>
    /// <param name="usuarioCpf">CPF do usuário solicitante (para verificação de propriedade).</param>
    /// <returns>Status do pagamento no gateway, ou null se a reserva não existir ou não tiver transação registrada.</returns>
    public async Task<PaymentStatusResult?> ConsultarStatusPagamentoAsync(int reservaId, string usuarioCpf)
    {
        var reserva = await _reservaRepository.ObterDetalhadaPorIdAsync(reservaId, usuarioCpf);
        if (reserva == null)
            return null;

        if (string.IsNullOrWhiteSpace(reserva.CodigoTransacaoGateway))
            return PaymentStatusResult.Falha("Esta reserva não possui código de transação no gateway de pagamento.");

        return await _paymentGateway.ConsultarStatusAsync(reserva.CodigoTransacaoGateway);
    }
}
