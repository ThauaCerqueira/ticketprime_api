namespace src.Models;

public enum DiscountType
{
    Percentual,
    ValorFixo
}

public class Coupon
{
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Tipo de desconto: Percentual (regra anterior) ou ValorFixo.</summary>
    public DiscountType TipoDesconto { get; set; } = DiscountType.Percentual;

    /// <summary>Percentual de desconto (usado quando TipoDesconto = Percentual).</summary>
    public decimal PorcentagemDesconto { get; set; }

    /// <summary>Valor fixo em reais (usado quando TipoDesconto = ValorFixo).</summary>
    public decimal? ValorDescontoFixo { get; set; }

    public decimal ValorMinimoRegra { get; set; }

    /// <summary>Data de expiração do cupom. Nulo = sem validade.</summary>
    public DateTime? DataExpiracao { get; set; }

    /// <summary>Máximo de usos permitidos. 0 = ilimitado.</summary>
    public int LimiteUsos { get; set; } = 0;

    /// <summary>Contador de vezes que o cupom foi utilizado.</summary>
    public int TotalUsado { get; set; } = 0;

    /// <summary>
    /// Se preenchido, o cupom só se aplica a eventos cujo GeneroMusical
    /// seja igual a este valor. Ex: "Samba", "Rock", "Eletrônica".
    /// </summary>
    public string? CategoriaEvento { get; set; }

    /// <summary>
    /// Se true, o cupom só pode ser usado na PRIMEIRA compra do usuário.
    /// </summary>
    public bool PrimeiroAcesso { get; set; }

    /// <summary>Data/hora de criação do registro (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Data/hora da última atualização (UTC).</summary>
    public DateTime? UpdatedAt { get; set; }

    public bool EstaValido()
    {
        if (DataExpiracao.HasValue && DateTime.UtcNow > DataExpiracao.Value)
            return false;
        if (LimiteUsos > 0 && TotalUsado >= LimiteUsos)
            return false;
        return true;
    }
}
