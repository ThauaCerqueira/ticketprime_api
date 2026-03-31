using src.Infrastructure;
using src.Infrastructure.IRepository;
using src.Infrastructure.Repository;
using src.Service;
using src.Models;
using src.Repositories;

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

builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<UsuarioService>();
builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
builder.Services.AddScoped<UsuarioRepository>();
builder.Services.AddScoped<ICupomRepository, CupomRepository>();
builder.Services.AddScoped<CupomService>();
builder.Services.AddScoped<IEventoRepository, EventoRepository>();
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

app.MapPost("/api/usuarios", async (Usuario usuario, UsuarioService service) =>
{
    try 
    {
        var resultado = await service.CadastrarUsuario(usuario);
        return Results.Created($"/api/usuarios/{resultado.Cpf}", resultado);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

app.Run();