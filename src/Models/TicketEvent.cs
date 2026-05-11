namespace src.Models;

public class TicketEvent
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int CapacidadeTotal { get; set; }
    /// <summary>Vagas restantes do evento (decrementado em compra, incrementado em cancelamento).</summary>
    public int CapacidadeRestante { get; set; }
    public DateTime DataEvento { get; set; }

    /// <summary>
    /// Data/hora de término do evento. Quando nulo, usa-se DataEvento.AddHours(4) como fallback
    /// para compatibilidade com eventos existentes que não possuem este campo configurado.
    /// </summary>
    public DateTime? DataTermino { get; set; }
    public decimal PrecoPadrao { get; set; }
    public int LimiteIngressosPorUsuario { get; set; } = 6;
    public string Local { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public string GeneroMusical { get; set; } = string.Empty;
    public bool EventoGratuito { get; set; }
    public string Status { get; set; } = "Rascunho";
    /// <summary>Taxa de serviço cobrada por ingresso. Máximo 5% do PrecoPadrao.</summary>
    public decimal TaxaServico { get; set; }

    /// <summary>
    /// Indica se o evento oferece meia-entrada (Lei 12.933/2013).
    /// Quando true, o frontend exibe a opção de escolha entre ingresso padrão e meia-entrada.
    /// </summary>
    public bool TemMeiaEntrada { get; set; }

    public TicketEvent(string nome, int capacidadeTotal, DateTime dataEvento, decimal precoPadrao, int limiteIngressosPorUsuario = 6)
    {
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("O nome do evento é obrigatório.");
        if (dataEvento <= DateTime.Now) throw new ArgumentException("A data do evento deve ser no futuro.");
        if (precoPadrao < 0) throw new ArgumentException("O preço padrão deve ser um valor positivo.");
        if (capacidadeTotal <= 0) throw new ArgumentException("A capacidade total deve ser um valor positivo.");
        if (limiteIngressosPorUsuario <= 0) throw new ArgumentException("O limite de ingressos por usuário deve ser um valor positivo.");

        Nome = nome;
        CapacidadeTotal = capacidadeTotal;
        DataEvento = dataEvento;
        PrecoPadrao = precoPadrao;
        LimiteIngressosPorUsuario = limiteIngressosPorUsuario;
    }

    /// <summary>
    /// Thumbnail da primeira foto do evento (Base64, JPEG) para exibição na vitrine.
    /// Populado apenas em consultas que fazem JOIN com EventoFotos.
    /// </summary>
    public string? FotoThumbnailBase64 { get; set; }

    /// <summary>CPF do admin/organizador que criou o evento. Permite exibir o perfil público do organizador.</summary>
    public string? OrganizadorCpf { get; set; }

    /// <summary>
    /// URL pública da imagem de capa do evento (retornada por IStorageService).
    /// Substitui o sistema de fotos criptografadas para exibição pública simples.
    /// </summary>
    public string? ImagemUrl { get; set; }

    /// <summary>
    /// Categoria do evento para filtros e cupons.
    /// Exemplos: "Show", "Teatro", "Esporte", "Stand-up", "Conferência", "Festival".
    /// Mantemos GeneroMusical por compatibilidade — Categoria é o campo primário.
    /// </summary>
    public string Categoria { get; set; } = string.Empty;

    public TicketEvent() { }
}
