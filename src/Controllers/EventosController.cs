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
    public async Task<IActionResult> CriarEvento([FromBody] CriarEventoDTO eventoDTO)
    {
        try{
            var novoEvento = await _eventoService.CriarNovoEvento(eventoDTO);
            Console.WriteLine($"Recebido: {eventoDTO.Nome}, Capacidade: {eventoDTO.CapacidadeTotal}");
            if (novoEvento == null)
            {
                return BadRequest("Não foi possível criar o evento.");
            }
            return CreatedAtAction(nameof(CriarEvento), new { id = novoEvento.Id}, novoEvento);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}