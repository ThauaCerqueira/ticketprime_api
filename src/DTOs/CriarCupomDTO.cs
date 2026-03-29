using System.ComponentModel.DataAnnotations;
namespace src.DTOs;
public class CriarCupomDTO
{
    [Required(ErrorMessage = "O código é obrigatório.")]
    [StringLength(20, ErrorMessage = "Código muito longo.")]
    public required string Codigo { get; set; }

    [Required]
    [Range(1, 100, ErrorMessage = "O desconto deve ser entre 1% e 100%.")]
    public decimal PorcentagemDesconto { get; set; }

    [Required]
    [Range(0, 1000000, ErrorMessage = "O valor não pode ser negativo.")]
    public decimal ValorMinimoRegra { get; set; }
}