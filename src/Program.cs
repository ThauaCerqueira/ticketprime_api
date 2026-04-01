using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Infrastructure.Repository;
using src.Service;
using src.Models;
using src.Repositories;
using src.DTOs;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=TicketPrime;Integrated Security=True;TrustServerCertificate=True;";

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers(); 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); 

builder.Services.AddSingleton(new DbConnectionFactory(connectionString));

builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<ICupomRepository, CupomRepository>();
builder.Services.AddScoped<IEventoRepository, EventoRepository>();

builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<CupomService>();
builder.Services.AddScoped<EventoService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll"); 
app.UseAuthorization();

app.MapControllers();

app.MapPost("/api/eventos", async (CriarEventoDTO dto, EventoService service) =>
{
    var resultado = await service.CriarNovoEvento(dto);
    if (resultado == null) return Results.BadRequest("Erro ao criar evento.");
    
    return Results.Created($"/api/eventos/{resultado.Id}", resultado);
});

app.MapGet("/minimal/eventos", async (EventoService service) =>
{
    var eventos = await service.ListarEventos();
    return Results.Ok(eventos);
});

app.MapPost("/api/cupons", async (CriarCupomDTO dto, CupomService service) =>
{
    try 
    {
        var sucesso = await service.CriarAsync(dto);
        if (sucesso)
        {
            return Results.Created($"/api/cupons/{dto.Codigo}", dto);
        }
        return Results.BadRequest("Não foi possível criar o cupom.");
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { mensagem = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { mensagem = ex.Message });
    }
});

app.MapPost("/api/usuarios", async (Usuario usuario, UsuarioService service) =>
{
    try 
    {
        var resultado = await service.CadastrarUsuario(usuario);
        return Results.Created($"/api/usuarios/{resultado.Cpf}", resultado);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { mensagem = ex.Message });
    }
});

app.Run();