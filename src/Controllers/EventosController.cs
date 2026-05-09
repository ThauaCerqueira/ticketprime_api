using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using src.DTOs;
using src.Service;

namespace src.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventosController : ControllerBase
{
    private readonly EventoService _eventoService;

    public EventosController(EventoService eventoService)
    {
        _eventoService = eventoService;
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Use o endpoint em Program.cs - POST /api/eventos", true)]
    public async Task<IActionResult> CriarEvento([FromBody] CriarEventoDTO eventoDTO)
    {
        try
        {
            var novoEvento = await _eventoService.CriarNovoEvento(eventoDTO);
            if (novoEvento == null)
                return BadRequest(new { mensagem = "Não foi possível criar o evento." });

            return CreatedAtAction(nameof(CriarEvento),
                new { id = novoEvento.Id },
                new { mensagem = "Evento criado com sucesso!", dados = novoEvento });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { mensagem = "Erro interno do servidor." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var eventos = await _eventoService.ListarEventos();
        return Ok(eventos);
    }

    [HttpGet("disponiveis")]
    public async Task<IActionResult> GetDisponiveis()
    {
        var eventos = await _eventoService.ListarEventosDisponiveis();
        return Ok(eventos);
    }

    [HttpGet("meus")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> GetMeusEventos()
    {
        var eventos = await _eventoService.ListarEventos();
        return Ok(eventos);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> DeletarEvento(int id)
    {
        try
        {
            await _eventoService.DeletarEventoAsync(id);
            return Ok(new { mensagem = "Evento excluído com sucesso." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { mensagem = "Erro interno ao excluir evento." });
        }
    }
}