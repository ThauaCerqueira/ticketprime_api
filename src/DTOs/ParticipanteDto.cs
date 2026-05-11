namespace src.DTOs;

/// <summary>
/// Dados de um participante para exportação CSV pelo administrador.
/// Campos de PII mascarados conforme LGPD.
/// </summary>
public class ParticipanteDto
{
    public string CodigoIngresso  { get; set; } = string.Empty;
    public string NomeParticipante { get; set; } = string.Empty;

    /// <summary>CPF mascarado (ex.: ***.456.789-**).</summary>
    public string Cpf             { get; set; } = string.Empty;
    public string Email           { get; set; } = string.Empty;
    public string Setor           { get; set; } = string.Empty;
    public decimal ValorPago      { get; set; }
    public string Status          { get; set; } = string.Empty;
    public DateTime DataCompra    { get; set; }
    public DateTime? DataCheckin  { get; set; }
    public bool MeiaEntrada       { get; set; }
    public bool TemSeguro         { get; set; }
}
