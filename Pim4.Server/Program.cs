using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using DotNetEnv;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Pim4.Server.Data;

// Load .env early if present
// Carrego variaveis do .env logo no inicio (nao quebra se ausente)
try { Env.Load(); } catch { }

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// MVC + autorizacao basica (JWT configurado mais abaixo)
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddHttpClient<Pim4.Server.Services.GeminiService>();

var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION")
    ?? builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(connString))
{
    throw new InvalidOperationException("POSTGRES_CONNECTION nao configurada.");
}
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connString);
});

// CORS for local Vite dev
// CORS amigavel p/ desenvolvimento (Vite/localhost)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => true)
              .AllowCredentials());
});

// JWT Auth
// Parametros basicos para validacao de tokens JWT
var jwtIssuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "Pim4";
var jwtAudience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "Pim4Client";
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "dev_super_secret_please_change";
byte[] keyBytes = Encoding.UTF8.GetBytes(jwtSecret);
if (keyBytes.Length < 32)
{
    // Derivo para 256 bits quando o segredo for curto (evita erro IDX10720)
    keyBytes = SHA256.HashData(keyBytes);
}
var signingKey = new SymmetricSecurityKey(keyBytes);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = signingKey,
        ClockSkew = TimeSpan.FromMinutes(2),
        // Garante que Identity.Name seja o e-mail
        NameClaimType = JwtRegisteredClaimNames.Email,
        RoleClaimType = ClaimTypes.Role
    };
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
// Swagger para ambiente de desenvolvimento
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Servir arquivos estaticos/build do client quando publicado juntos
app.UseDefaultFiles();
app.UseStaticFiles();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var disableHttpsRedirect = (Environment.GetEnvironmentVariable("DISABLE_HTTPS_REDIRECT") ?? "false")
    .Equals("true", StringComparison.OrdinalIgnoreCase);
if (!disableHttpsRedirect)
{
    app.UseHttpsRedirection();
}

app.UseCors("DevCors");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// SPA fallback (quando publicado junto do client)
app.MapFallbackToFile("/index.html");

app.Run();
