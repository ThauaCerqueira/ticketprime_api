using src.Models;
using src.DTOs;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using src.Infrastructure;
using src.Infrastructure.Repository;
using src.Infrastructure.IRepository;
using src.Service;
 
namespace TicketPrime.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
 
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                ?? "Server=localhost;Database=TicketPrime;Integrated Security=True;TrustServerCertificate=True;";
            
            // Initialize database
            InitializeDatabase(connectionString);
 
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
 
            var jwtKey = builder.Configuration["Jwt:Key"] ?? "TicketPrimeChaveSecreta2024SuperSegura!";
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = "TicketPrime",
                        ValidAudience = "TicketPrime",
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                    };
                });
 
            builder.Services.AddAuthorization();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
 
            builder.Services.AddSingleton(new DbConnectionFactory(connectionString));
            builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
            builder.Services.AddScoped<ICupomRepository, CupomRepository>();
            builder.Services.AddScoped<IEventoRepository, EventoRepository>();
            builder.Services.AddScoped<IReservaRepository, ReservaRepository>();
            builder.Services.AddScoped<UsuarioService>();
            builder.Services.AddScoped<CupomService>();
            builder.Services.AddScoped<EventoService>();
            builder.Services.AddScoped<AuthService>();
            builder.Services.AddScoped<ReservaService>();
 
            var app = builder.Build();
 
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
 
            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseAuthorization();
 
            app.MapPost("/api/eventos", async (CriarEventoDTO dto, EventoService service) =>
            {
                try
                {
                    var resultado = await service.CriarNovoEvento(dto);
                    if (resultado == null)
                        return Results.BadRequest(new { mensagem = "Erro ao criar evento." });
 
                    return Results.Created($"/api/eventos/{resultado.Id}", resultado);
                }
                catch (Exception ex)
                {
                    return Results.BadRequest(new { mensagem = ex.Message });
                }
            }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));
 
            app.MapGet("/api/eventos", async (EventoService service) =>
            {
                var eventos = await service.ListarEventos();
                return Results.Ok(eventos);
            });

            app.MapGet("/api/eventos/meus", async (EventoService service, HttpContext context) =>
            {
                var cpf = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(cpf))
                    return Results.Unauthorized();

                var eventos = await service.ListarEventos();
                return Results.Ok(eventos);
            }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));
 
            app.MapPost("/api/cupons", async (CriarCupomDTO dto, CupomService service) =>
            {
                try
                {
                    var sucesso = await service.CriarAsync(dto);
                    if (sucesso)
                        return Results.Created($"/api/cupons/{dto.Codigo}", dto);
 
                    return Results.BadRequest(new { mensagem = "Não foi possível criar o cupom." });
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { mensagem = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Conflict(new { mensagem = ex.Message });
                }
                catch (Exception ex)
                {
                    return Results.Json(new { mensagem = "Erro interno do servidor." }, statusCode: 500);
                }
            }).RequireAuthorization(policy => policy.RequireRole("ADMIN"));

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
 
            app.MapPost("/api/auth/login", async (LoginDTO dto, AuthService service) =>
            {
                var resultado = await service.LoginAsync(dto);
                if (resultado == null)
                    return Results.Json(new { mensagem = "CPF ou senha inválidos." }, statusCode: 401);

                return Results.Ok(resultado);
            });

            app.MapPost("/api/reservas", async (ComprarIngressoDTO dto, ReservaService service, HttpContext context) =>
            {
                try
                {
                    var cpf = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
 
                    if (string.IsNullOrEmpty(cpf))
                        return Results.Unauthorized();
 
                    var reserva = await service.ComprarIngressoAsync(cpf, dto.EventoId);
                    return Results.Created($"/api/reservas/{reserva.Id}", new
                    {
                        mensagem = "Ingresso comprado com sucesso!",
                        reservaId = reserva.Id,
                        eventoId = reserva.EventoId
                    });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { mensagem = ex.Message });
                }
            }).RequireAuthorization();
 
            app.MapGet("/api/reservas/minhas", async (ReservaService service, HttpContext context) =>
            {
                var cpf = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
 
                if (string.IsNullOrEmpty(cpf))
                    return Results.Unauthorized();
 
                var reservas = await service.ListarReservasUsuarioAsync(cpf);
                return Results.Ok(reservas);
            }).RequireAuthorization();
 
            app.MapDelete("/api/reservas/{id}", async (int id, ReservaService service, HttpContext context) =>
            {
                try
                {
                    var cpf = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
 
                    if (string.IsNullOrEmpty(cpf))
                        return Results.Unauthorized();
 
                    await service.CancelarIngressoAsync(id, cpf);
                    return Results.Ok(new { mensagem = "Ingresso cancelado com sucesso! Vaga devolvida ao evento." });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { mensagem = ex.Message });
                }
            }).RequireAuthorization();
 
            app.MapGet("/api/perfil", async (UsuarioService service, HttpContext context) =>
            {
                var cpf = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
 
                if (string.IsNullOrEmpty(cpf))
                    return Results.Unauthorized();
 
                var usuario = await service.BuscarPorCpf(cpf);
 
                if (usuario == null)
                    return Results.NotFound(new { mensagem = "Usuário não encontrado." });
 
                return Results.Ok(new
                {
                    usuario.Cpf,
                    usuario.Nome,
                    usuario.Email,
                    usuario.Perfil
                });
            }).RequireAuthorization();
 
            app.Run();
        }

        private static void InitializeDatabase(string connectionString)
        {
            try
            {
                // Use Directory.GetCurrentDirectory() which returns the content root path (src folder)
                // Then go one level up to get to the root project folder
                var scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "db", "script.sql");
                scriptPath = Path.GetFullPath(scriptPath);
                
                Console.WriteLine($"Looking for database script at: {scriptPath}");
                
                if (!File.Exists(scriptPath))
                {
                    Console.WriteLine($"Warning: Database script not found at {scriptPath}");
                    return;
                }

                var sqlScript = File.ReadAllText(scriptPath);
                var statements = sqlScript.Split(new[] { "\nGO\n", "\nGO\r\n", "\r\nGO\r\n", "\nGO\r", "\nGO" }, StringSplitOptions.RemoveEmptyEntries);

                // Change connection string to use master database for initial setup
                var masterConnectionString = connectionString.Replace("Database=TicketPrime", "Database=master");

                using (var connection = new Microsoft.Data.SqlClient.SqlConnection(masterConnectionString))
                {
                    connection.Open();
                    Console.WriteLine("✓ Connected to SQL Server for database initialization");

                    foreach (var statement in statements)
                    {
                        var trimmedStatement = statement.Trim();
                        if (trimmedStatement.Length == 0)
                            continue;

                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = trimmedStatement;
                            command.CommandTimeout = 60;

                            try
                            {
                                command.ExecuteNonQuery();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"⚠ Database setup notice: {ex.Message}");
                            }
                        }
                    }

                    connection.Close();
                }

                Console.WriteLine("✓ Database initialization completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Warning during database setup: {ex.Message}");
                // Don't exit, let the application try to continue
            }
        }
    }
}