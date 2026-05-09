using Microsoft.AspNetCore.Mvc;
using src.Models;
using src.Service;
using src.DTOs;

namespace src.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReservasController : ControllerBase
{
    private readonly ReservaService _reservaService;

    public ReservasController(ReservaService reservaService)
    {
        _reservaService = reservaService;
    }

    [HttpGet("{cpf}")]
    public async Task<IActionResult> ObterPorCpf(string cpf)
    {
        try
        {
            var reservas = await _reservaService.ListarReservasUsuarioAsync(cpf);
            return Ok(reservas);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensagem = $"Erro ao obter reservas: {ex.Message}" });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Cancelar(int id, [FromQuery] string cpf)
    {
        try
        {
            await _reservaService.CancelarIngressoAsync(id, cpf);
            return Ok(new { mensagem = "Reserva cancelada com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensagem = $"Erro ao cancelar reserva: {ex.Message}" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ComprarIngresso([FromBody] ComprarIngressoDTO dto, [FromQuery] string cpf)
    {
        try
        {
            var reserva = await _reservaService.ComprarIngressoAsync(cpf, dto.EventoId, dto.CupomUtilizado, dto.ContratarSeguro);
            return CreatedAtAction(nameof(ObterPorCpf), new { cpf }, reserva);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensagem = $"Erro ao comprar ingresso: {ex.Message}" });
        }
    }
}
