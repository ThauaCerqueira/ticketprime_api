namespace src.Models;

/// <summary>
/// Representa um lote progressivo de preços para um evento.
/// Ex: 1º Lote (R$ 50,00), 2º Lote (R$ 70,00), 3º Lote (R$ 90,00).
/// Cada lote pode ser global (aplicado a todos os tipos de ingresso)
/// ou específico de um tipo de ingresso.
/// </summary>
public class Lote
{
    public int Id { get; set; }

    /// <summary>ID do evento ao qual este lote pertence.</summary>
    public int EventoId { get; set; }

    /// <summary>
    /// ID do tipo de ingresso ao qual este lote se aplica.
    /// Null significa que o lote se aplica a todos os tipos de ingresso do evento.
    /// </summary>
    public int? TicketTypeId { get; set; }

    /// <summary>Nome do lote (ex: "1º Lote", "2º Lote", "3º Lote", "Early Bird").</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Preço do ingresso neste lote.</summary>
    public decimal Preco { get; set; }

    /// <summary>Quantidade máxima de ingressos a vender neste lote.</summary>
    public int QuantidadeMaxima { get; set; }

    /// <summary>Quantidade de ingressos já vendidos neste lote.</summary>
    public int QuantidadeVendida { get; set; }

    /// <summary>Data de início do lote (null = imediato).</summary>
    public DateTime? DataInicio { get; set; }

    /// <summary>Data de fim do lote (null = até acabar ou evento).</summary>
    public DateTime? DataFim { get; set; }

    /// <summary>Indica se o lote está ativo.</summary>
    public bool Ativo { get; set; } = true;

    public Lote() { }

    public Lote(string nome, decimal preco, int quantidadeMaxima, int? ticketTypeId = null)
    {
        if (string.IsNullOrWhiteSpace(nome))
            throw new ArgumentException("O nome do lote é obrigatório.");
        if (preco < 0)
            throw new ArgumentException("O preço do lote deve ser um valor positivo.");
        if (quantidadeMaxima <= 0)
            throw new ArgumentException("A quantidade máxima deve ser um valor positivo.");

        Nome = nome;
        Preco = preco;
        QuantidadeMaxima = quantidadeMaxima;
        TicketTypeId = ticketTypeId;
    }

    /// <summary>
    /// Verifica se o lote está disponível para venda agora.
    /// Considera data de início/fim, quantidade vendida e status ativo.
    /// </summary>
    public bool EstaDisponivel()
    {
        if (!Ativo) return false;
        if (QuantidadeVendida >= QuantidadeMaxima) return false;

        var now = DateTime.UtcNow;
        if (DataInicio.HasValue && now < DataInicio.Value) return false;
        if (DataFim.HasValue && now > DataFim.Value) return false;

        return true;
    }
}
