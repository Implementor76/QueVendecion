using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QueVendecion.Application.Interfaces;
using QueVendecion.Domain.Enums;
using QueVendecion.Infrastructure.Data;
using QueVendecion.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

builder.Services.AddDbContext<QueVendecionDbContext>(options =>
{
    if (builder.Environment.IsProduction())
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
    else
    {
        options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});

builder.Services.AddScoped<ITiendaService, TiendaService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IPedidoService, PedidoService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key not configured. Set it in User Secrets or environment variables.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "QueVendecion",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "QueVendecion",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsProduction())
        {
            policy.WithOrigins("http://192.168.1.3", "https://192.168.1.3", "https://pedidos.implementor.site")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins("http://localhost:5173", "http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QueVendecionDbContext>();
    if (args.Contains("--upgrade-inventory"))
    {
        if (!app.Environment.IsDevelopment() || !db.Database.IsSqlite())
            throw new InvalidOperationException("--upgrade-inventory solo está permitido con SQLite en Development. En producción aplica migraciones antes de iniciar la API.");
        await InventarioSchemaUpgrade.AplicarAsync(db);
        app.Logger.LogInformation("Esquema de inventario actualizado sin eliminar datos.");
        return;
    }
    if (args.Contains("--upgrade-operation"))
    {
        if (!app.Environment.IsDevelopment() || !db.Database.IsSqlite())
            throw new InvalidOperationException("--upgrade-operation solo está permitido con SQLite en Development.");
        await OperacionSchemaUpgrade.AplicarAsync(db);
        return;
    }
    if (app.Environment.IsDevelopment()) db.Database.EnsureCreated();
    // Existing SQLite databases must be upgraded before EF queries the new fields.
    if (app.Environment.IsDevelopment() && db.Database.IsSqlite())
    {
        await InventarioSchemaUpgrade.AplicarAsync(db);
        await OperacionSchemaUpgrade.AplicarAsync(db);
    }
    else
    {
        if (db.Database.IsNpgsql() && (await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Hay migraciones pendientes. Aplica las migraciones durante el despliegue antes de iniciar la API. No borres ni recrees la base.");
        await InventarioSchemaUpgrade.VerificarAsync(db);
        await OperacionSchemaUpgrade.VerificarAsync(db);
    }

    if (!db.UsuariosTienda.Any(u => u.Rol == RolUsuario.SuperAdmin))
    {
        var seedEmail = builder.Configuration["SeedSuperAdmin:Email"];
        var seedPassword = builder.Configuration["SeedSuperAdmin:Password"];
        if (string.IsNullOrWhiteSpace(seedEmail) && string.IsNullOrWhiteSpace(seedPassword))
        {
            app.Logger.LogWarning("No existe SuperAdmin y no se configuró SeedSuperAdmin. Configura credenciales privadas antes de crear la cuenta inicial.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPassword) || seedPassword.Length < 12)
                throw new InvalidOperationException("SeedSuperAdmin requiere email y contraseña privada de al menos 12 caracteres.");
            using var seedTransaction = db.Database.BeginTransaction();
            var superAdminTienda = new QueVendecion.Domain.Entities.Tienda
            {
                Nombre = "SuperAdmin", Slug = "superadmin", Direccion = "", Telefono = ""
            };
            db.Tiendas.Add(superAdminTienda);
            db.SaveChanges();
            db.UsuariosTienda.Add(new QueVendecion.Domain.Entities.UsuarioTienda
            {
                TiendaId = superAdminTienda.Id,
                Nombre = "Super Admin",
                Email = seedEmail.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword),
                Rol = RolUsuario.SuperAdmin
            });
            db.SaveChanges();
            seedTransaction.Commit();
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (QueVendecion.Application.Exceptions.ReglaNegocioException ex)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { code = ex.Codigo, message = ex.Message });
    }
    catch (Exception ex) when (EsConflictoInventario(ex))
    {
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { code = "inventario_concurrente", message = "El inventario cambió durante la operación. Actualiza e intenta de nuevo." });
    }
});

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static bool EsConflictoInventario(Exception ex) => ex switch
{
    Npgsql.PostgresException pg => pg.SqlState is "40001" or "40P01",
    Microsoft.Data.Sqlite.SqliteException sqlite => sqlite.SqliteErrorCode is 5 or 6,
    _ => ex.InnerException != null && EsConflictoInventario(ex.InnerException)
};
