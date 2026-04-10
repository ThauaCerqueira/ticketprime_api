using Microsoft.AspNetCore.Mvc;
using src.Models;
using src.Service;


namespace src.Controllers;


[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioService _usuarioService;

    public UsuariosController(UsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    [HttpPost]
    public async Task<IActionResult> CriarUsuario([FromBody] Usuario usuario)
    {
        try 
        {
            var novoUsuario = await _usuarioService.CadastrarUsuario(usuario);
            
            return Created($"/api/usuarios/{novoUsuario.Cpf}", new { message = "Usuário criado com sucesso" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Erro interno: {ex.Message}");
        }
    }
}