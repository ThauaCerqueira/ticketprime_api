using System.Text;
using QRCoder;

namespace src.Service;

/// <summary>
/// Serviço centralizado de templates HTML para emails transacionais.
/// Todos os métodos geram o corpo HTML completo e enviam via IEmailService.
/// </summary>
public class EmailTemplateService
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(IEmailService emailService, ILogger<EmailTemplateService> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    // ── Helpers ─────────────────────────────────────────────────────

    private static string TemplateBase(string titulo, string conteudoHtml)
    {
        return $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="UTF-8"></head>
        <body style="margin:0;padding:0;background:#f4f4f4;font-family:'Segoe UI',Arial,sans-serif;">
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f4;">
                <tr><td style="padding:20px 10px;">
                    <table role="presentation" width="600" align="center" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);">
                        <!-- Header -->
                        <tr>
                            <td style="padding:30px 30px 10px;background:linear-gradient(135deg,#7c3aed,#6d28d9);text-align:center;">
                                <h1 style="color:#ffffff;margin:0;font-size:22px;">🎫 TicketPrime</h1>
                                <p style="color:#c4b5fd;margin:4px 0 0;font-size:13px;">Sua plataforma de eventos</p>
                            </td>
                        </tr>
                        <!-- Title -->
                        <tr>
                            <td style="padding:25px 30px 5px;">
                                <h2 style="color:#1f2937;margin:0;font-size:20px;">{titulo}</h2>
                            </td>
                        </tr>
                        <!-- Content -->
                        <tr>
                            <td style="padding:10px 30px 25px;color:#374151;font-size:15px;line-height:1.6;">
                                {conteudoHtml}
                            </td>
                        </tr>
                        <!-- Footer -->
                        <tr>
                            <td style="padding:20px 30px;background:#f9fafb;border-top:1px solid #e5e7eb;">
                                <p style="color:#9ca3af;font-size:12px;margin:0;text-align:center;">
                                    TicketPrime — Sua plataforma de eventos<br>
                                    Este é um email automático, por favor não responda.
                                </p>
                            </td>
                        </tr>
                    </table>
                </td></tr>
            </table>
        </body>
        </html>
        """;
    }

    private static string StyleButton(string texto, string url)
    {
        return $"""
        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:20px 0;">
            <tr>
                <td style="border-radius:6px;background:#7c3aed;text-align:center;">
                    <a href="{url}" style="display:inline-block;padding:12px 28px;color:#ffffff;text-decoration:none;font-size:15px;font-weight:600;border-radius:6px;">
                        {texto}
                    </a>
                </td>
            </tr>
        </table>
        """;
    }

    private static string CodigoBlock(string codigo)
    {
        return $"""
        <div style="background:#f3f4f6;padding:20px;text-align:center;border-radius:8px;margin:20px 0;border:1px dashed #c4b5fd;">
            <span style="font-size:26px;font-weight:bold;letter-spacing:5px;color:#7c3aed;font-family:'Courier New',monospace;">{codigo}</span>
        </div>
        """;
    }

    private static string InfoLinha(string label, string valor)
    {
        return $"""
        <tr>
            <td style="padding:6px 12px;color:#6b7280;font-size:13px;border-bottom:1px solid #f3f4f6;width:140px;">{label}</td>
            <td style="padding:6px 12px;color:#1f2937;font-size:14px;border-bottom:1px solid #f3f4f6;font-weight:600;">{valor}</td>
        </tr>
        """;
    }

    // ── Public Methods ──────────────────────────────────────────────

    /// <summary>
    /// Gera um QR Code em PNG Base64 a partir de uma string.
    /// </summary>
    public static string GerarQrCodeBase64(string conteudo)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(conteudo, QRCodeGenerator.ECCLevel.Q);
        using var png = new PngByteQRCode(data);
        var bytes = png.GetGraphic(4, new byte[] { 124, 58, 237 }, new byte[] { 255, 255, 255 }); // #7c3aed
        return Convert.ToBase64String(bytes);
    }

    // ── 1. Verificação de Email ─────────────────────────────────────

    public async Task SendEmailVerificationAsync(string to, string nome, string token)
    {
        var conteudo = $"""
        <p>Olá <strong>{nome}</strong>,</p>
        <p>Bem-vindo ao <strong>TicketPrime</strong>! Use o código abaixo para verificar seu email e ativar sua conta:</p>
        {CodigoBlock(token)}
        <p>Este código expira em <strong>24 horas</strong>.</p>
        <p>Se você não criou uma conta no TicketPrime, ignore este email.</p>
        """;

        await _emailService.SendAsync(to, "TicketPrime — Confirme seu email 📧", TemplateBase("Confirme seu email", conteudo));
        _logger.LogInformation("Email de verificação enviado para {Email}", to);
    }

    // ── 2. Redefinição de Senha ─────────────────────────────────────

    public async Task SendPasswordRecoveryAsync(string to, string token)
    {
        var conteudo = $"""
        <p>Recebemos uma solicitação de redefinição de senha para sua conta no <strong>TicketPrime</strong>.</p>
        <p>Use o código abaixo para criar uma nova senha:</p>
        {CodigoBlock(token)}
        <p>Este código expira em <strong>1 hora</strong>.</p>
        <p>Se você não solicitou a redefinição de senha, ignore este email.</p>
        """;

        await _emailService.SendAsync(to, "TicketPrime — Redefinição de senha 🔑", TemplateBase("Redefinição de senha", conteudo));
        _logger.LogInformation("Email de redefinição de senha enviado para {Email}", to);
    }

    // ── 3. Confirmação de Compra com QR Code ────────────────────────

    public async Task SendPurchaseConfirmationAsync(
        string to,
        string nomeCliente,
        string eventoNome,
        DateTime dataEvento,
        string local,
        decimal valorPago,
        string codigoIngresso,
        string qrCodeBase64)
    {
        var dataFormatada = dataEvento.ToString("dd/MM/yyyy 'às' HH:mm");
        var valorFormatado = valorPago.ToString("C2", new System.Globalization.CultureInfo("pt-BR"));

        var conteudo = $"""
        <p>Olá <strong>{nomeCliente}</strong>,</p>
        <p>Sua compra foi realizada com sucesso! 🎉</p>

        <h3 style="color:#7c3aed;margin-bottom:8px;">📋 Detalhes da Compra</h3>
        <table style="width:100%;border-collapse:collapse;">
            {InfoLinha("Evento", eventoNome)}
            {InfoLinha("Data", dataFormatada)}
            {InfoLinha("Local", local)}
            {InfoLinha("Valor Pago", valorFormatado)}
            {InfoLinha("Código do Ingresso", codigoIngresso)}
        </table>

        <h3 style="color:#7c3aed;margin:20px 0 8px;">📱 Seu QR Code de Acesso</h3>
        <p style="color:#6b7280;font-size:13px;">Apresente este QR Code na entrada do evento para validação.</p>
        <div style="text-align:center;margin:15px 0;padding:20px;background:#f9fafb;border-radius:8px;">
            <img src="data:image/png;base64,{qrCodeBase64}" alt="QR Code do Ingresso" style="width:200px;height:200px;image-rendering:pixelated;" />
        </div>
        <p style="text-align:center;color:#6b7280;font-size:12px;">Código: <strong>{codigoIngresso}</strong></p>
        """;

        await _emailService.SendAsync(to, $"TicketPrime — Ingresso confirmado: {eventoNome} 🎫",
            TemplateBase("Compra confirmada ✅", conteudo));
        _logger.LogInformation("Confirmação de compra enviada para {Email} | Evento: {Evento}", to, eventoNome);
    }

    // ── 4. Confirmação de Cancelamento ──────────────────────────────

    public async Task SendCancellationConfirmationAsync(
        string to,
        string nomeCliente,
        string eventoNome,
        DateTime dataEvento,
        decimal valorDevolvido)
    {
        var dataFormatada = dataEvento.ToString("dd/MM/yyyy 'às' HH:mm");
        var valorFormatado = valorDevolvido.ToString("C2", new System.Globalization.CultureInfo("pt-BR"));

        var conteudo = $"""
        <p>Olá <strong>{nomeCliente}</strong>,</p>
        <p>Seu ingresso para o evento abaixo foi cancelado conforme solicitado.</p>

        <h3 style="color:#7c3aed;margin-bottom:8px;">📋 Detalhes do Cancelamento</h3>
        <table style="width:100%;border-collapse:collapse;">
            {InfoLinha("Evento", eventoNome)}
            {InfoLinha("Data", dataFormatada)}
            {InfoLinha("Valor Devolvido", valorFormatado)}
        </table>

        <p style="color:#6b7280;font-size:13px;margin-top:15px;">
            💡 O reembolso será processado em até <strong>5 dias úteis</strong> na forma de pagamento original.
            Em caso de dúvidas, entre em contato com nosso suporte.
        </p>
        """;

        await _emailService.SendAsync(to, $"TicketPrime — Cancelamento: {eventoNome} ❌",
            TemplateBase("Cancelamento confirmado", conteudo));
        _logger.LogInformation("Confirmação de cancelamento enviada para {Email} | Evento: {Evento}", to, eventoNome);
    }

    // ── 5. Notificação de Alteração de Evento ───────────────────────

    public async Task SendEventChangedNotificationAsync(
        string to,
        string nomeCliente,
        string eventoNome,
        string tipoAlteracao,
        string detalhes)
    {
        var icone = tipoAlteracao.ToLowerInvariant() switch
        {
            "data" => "📅",
            "local" => "📍",
            "horario" => "🕐",
            "preco" => "💰",
            _ => "ℹ️"
        };

        var conteudo = $"""
        <p>Olá <strong>{nomeCliente}</strong>,</p>
        <p>O evento <strong>{eventoNome}</strong> sofreu uma alteração.</p>

        <div style="background:#fef3c7;padding:15px 20px;border-radius:8px;margin:15px 0;border-left:4px solid #f59e0b;">
            <h4 style="color:#92400e;margin:0 0 5px;font-size:15px;">{icone} {tipoAlteracao}</h4>
            <p style="color:#78350f;margin:0;font-size:14px;">{detalhes}</p>
        </div>

        <p style="color:#6b7280;font-size:13px;">
            Pedimos desculpas pelo inconveniente. Se você não puder comparecer na nova data,
            pode cancelar seu ingresso acessando sua conta no TicketPrime.
        </p>
        """;

        await _emailService.SendAsync(to, $"TicketPrime — Alteração no evento: {eventoNome} {icone}",
            TemplateBase("Evento alterado ⚠️", conteudo));
        _logger.LogInformation("Notificação de alteração enviada para {Email} | Evento: {Evento}", to, eventoNome);
    }

    // ── 6. Notificação de Cancelamento de Evento ────────────────────

    public async Task SendEventCancelledNotificationAsync(
        string to,
        string nomeCliente,
        string eventoNome,
        DateTime dataEventoOriginal)
    {
        var dataFormatada = dataEventoOriginal.ToString("dd/MM/yyyy");

        var conteudo = $"""
        <p>Olá <strong>{nomeCliente}</strong>,</p>
        <p>Infelizmente, o evento <strong>{eventoNome}</strong>, previsto para <strong>{dataFormatada}</strong>, foi <strong>cancelado</strong>.</p>

        <div style="background:#fee2e2;padding:15px 20px;border-radius:8px;margin:15px 0;border-left:4px solid #ef4444;">
            <p style="color:#991b1b;margin:0;font-size:14px;">
                🚫 O evento foi cancelado pelo organizador. Seu ingresso será reembolsado integralmente.
            </p>
        </div>

        <p style="color:#6b7280;font-size:13px;">
            💡 O reembolso será processado automaticamente em até <strong>10 dias úteis</strong> na forma de pagamento original.
            Não é necessário entrar em contato. Você receberá um comprovante de reembolso por email.
        </p>
        <p style="color:#6b7280;font-size:13px;">
            Lamentamos pelo ocorrido e esperamos vê-lo em outros eventos em breve.
        </p>
        """;

        await _emailService.SendAsync(to, $"TicketPrime — Evento cancelado: {eventoNome} 🚫",
            TemplateBase("Evento cancelado", conteudo));
        _logger.LogInformation("Notificação de cancelamento enviada para {Email} | Evento: {Evento}", to, eventoNome);
    }

    // ── 7. Notificação de Vaga na Fila de Espera ─────────────────────

    public async Task SendWaitingListNotificationAsync(
        string to,
        string nomeCliente,
        string eventoNome,
        int eventoId,
        DateTime dataEvento)
    {
        var dataFormatada = dataEvento.ToString("dd/MM/yyyy 'às' HH:mm");

        var conteudo = $"""
        <p>Olá <strong>{nomeCliente}</strong>,</p>
        <p>🎉 Uma vaga foi liberada para o evento <strong>{eventoNome}</strong>!</p>

        <div style="background:#ecfdf5;padding:15px 20px;border-radius:8px;margin:15px 0;border-left:4px solid #10b981;">
            <h4 style="color:#065f46;margin:0 0 5px;font-size:15px;">✅ Vaga disponível!</h4>
            <p style="color:#064e3b;margin:0;font-size:14px;">
                Alguém cancelou sua reserva e você é o próximo da fila de espera!
            </p>
        </div>

        <h3 style="color:#7c3aed;margin-bottom:8px;">📋 Detalhes do Evento</h3>
        <table style="width:100%;border-collapse:collapse;">
            {InfoLinha("Evento", eventoNome)}
            {InfoLinha("Data", dataFormatada)}
        </table>

        <p style="color:#6b7280;font-size:13px;margin-top:15px;">
            ⏳ Você tem <strong>30 minutos</strong> para garantir seu ingresso antes que a vaga seja oferecida
            ao próximo da fila. Acesse o TicketPrime agora e adquira seu ingresso!
        </p>
        {StyleButton("Garantir meu ingresso", $"https://ticketprime.com/eventos/{eventoId}")}
        <p style="color:#9ca3af;font-size:12px;margin-top:10px;">
            Se você não tiver mais interesse, não é necessário fazer nada — a vaga será repassada automaticamente
            ao próximo da fila.
        </p>
        """;

        await _emailService.SendAsync(to, $"TicketPrime — Vaga liberada: {eventoNome} 🎉",
            TemplateBase("Vaga disponível! 🎫", conteudo));
        _logger.LogInformation("Notificação de fila de espera enviada para {Email} | Evento: {Evento}", to, eventoNome);
    }
}
