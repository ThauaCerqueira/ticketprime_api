using Microsoft.AspNetCore.Mvc;
using src.Infrastructure;
using src.Models;

namespace src.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioRepository _repository;

    public UsuariosController(UsuarioRepository repository)
    {
        _repository = repository;
    }

    [HttpPost]
    public async Task<IActionResult> CriarUsuario([FromBody] Usuario usuario)
    {
        
        Console.WriteLine("Chegou aqui!"); // debug rápido
        await _repository.CriarUsuario(usuario);

        return Ok("Usuário criado com sucesso");
    }
}