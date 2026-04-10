using System.ComponentModel.DataAnnotations;
namespace src.DTOs;

public record CriarEventoDTO
{
    [Required(ErrorMessage = "O nome do evento é obrigatório.")]
    [StringLength(100, ErrorMessage = "O nome é muito longo.")]
    public string Nome { get; set; } = string.Empty;

    [Range(1, 1000000, ErrorMessage = "A capacidade deve ser entre 1 e 1.000.000.")]
    public int CapacidadeTotal { get; set; }

    [Required(ErrorMessage = "A data é obrigatória.")]
    public DateTime DataEvento { get; set; }

    [Range(0, 100000, ErrorMessage = "O preço não pode ser negativo.")]
    public decimal PrecoPadrao { get; set; }
}