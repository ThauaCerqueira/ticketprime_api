using Microsoft.AspNetCore.Mvc;
using src.Models;
using src.Service;
using src.DTOs;

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

    [HttpGet("perfil/{cpf}")]
    public async Task<IActionResult> ObterPerfil(string cpf)
    {
        try
        {
            var usuario = await _usuarioService.BuscarPorCpf(cpf);
            
            if (usuario == null)
                return NotFound(new { mensagem = "Usuário não encontrado." });

            var perfil = new PerfilResponse
            {
                Cpf = usuario.Cpf,
                Nome = usuario.Nome,
                Email = usuario.Email,
                Perfil = usuario.Perfil
            };

            return Ok(perfil);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensagem = $"Erro ao obter perfil: {ex.Message}" });
        }
    }
}