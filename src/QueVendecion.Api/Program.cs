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
    db.Database.EnsureCreated();

    if (!db.UsuariosTienda.Any(u => u.Rol == RolUsuario.SuperAdmin))
    {
        var superAdminTienda = new QueVendecion.Domain.Entities.Tienda
        {
            Nombre = "SuperAdmin",
            Slug = "superadmin",
            Direccion = "",
            Telefono = ""
        };
        db.Tiendas.Add(superAdminTienda);
        db.SaveChanges();

        db.UsuariosTienda.Add(new QueVendecion.Domain.Entities.UsuarioTienda
        {
            TiendaId = superAdminTienda.Id,
            Nombre = "Super Admin",
            Email = "marco.cisneros@gmail.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("C@r0l1na10"),
            Rol = RolUsuario.SuperAdmin
        });
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowFrontend");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
