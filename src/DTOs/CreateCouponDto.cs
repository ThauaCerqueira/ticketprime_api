using System.ComponentModel.DataAnnotations;
using src.Models;

namespace src.DTOs;
public class CreateCouponDto
{
    [Required(ErrorMessage = "O código é obrigatório.")]
    [StringLength(20, ErrorMessage = "Código muito longo.")]
    public required string Codigo { get; set; }

    /// <summary>Tipo de desconto: 0 = Percentual, 1 = ValorFixo.</summary>
    [Required]
    public DiscountType TipoDesconto { get; set; } = DiscountType.Percentual;

    /// <summary>Percentual de desconto (obrigatório se TipoDesconto = Percentual).</summary>
    [Range(1, 100, ErrorMessage = "O desconto percentual deve ser entre 1% e 100%.")]
    public decimal PorcentagemDesconto { get; set; }

    /// <summary>Valor fixo de desconto em R$ (obrigatório se TipoDesconto = ValorFixo).</summary>
    [Range(0.01, 1000000, ErrorMessage = "O valor fixo de desconto deve ser positivo.")]
    public decimal? ValorDescontoFixo { get; set; }

    [Required]
    [Range(0, 1000000, ErrorMessage = "O valor não pode ser negativo.")]
    public decimal ValorMinimoRegra { get; set; }

    /// <summary>Data de expiração. Nulo = sem validade.</summary>
    public DateTime? DataExpiracao { get; set; }

    /// <summary>Limite de usos. 0 = ilimitado.</summary>
    [Range(0, int.MaxValue)]
    public int LimiteUsos { get; set; } = 0;

    /// <summary>
    /// Se preenchido, o cupom só se aplica a eventos desta categoria (GeneroMusical).
    /// </summary>
    [StringLength(100)]
    public string? CategoriaEvento { get; set; }

    /// <summary>
    /// Se true, o cupom só vale na primeira compra do usuário.
    /// </summary>
    public bool PrimeiroAcesso { get; set; }
}
