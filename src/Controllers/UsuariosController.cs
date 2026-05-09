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
            
            return Created($"/api/usuarios/{novoUsuario.Cpf}", new { mensagem = "Usuário criado com sucesso.", dados = novoUsuario });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensagem = ex.Message });
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
}