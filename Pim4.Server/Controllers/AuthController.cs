using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pim4.Server.Data;
using Pim4.Server.Data.Entities;

// AuthController
// Eu explico: endpoints de autenticacao da API.
// - POST /auth/register: cria usuario basico (cargo 'usuario') e retorna JWT.
// - POST /auth/login: autentica credenciais e emite JWT com claims (email, role, nameid).
// Notas: agora usamos Entity Framework (PostgreSQL) em vez de SQL manual.
namespace Pim4.Server.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AuthController(AppDbContext db)
        {
            _db = db;
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class LoginResponse
        {
            public string Token { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public object User { get; set; } = new { };
        }

        public class RegisterRequest
        {
            public string Cpf { get; set; } = string.Empty;
            public string Nome { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.Nome))
            {
                return BadRequest(new { message = "Nome, e-mail e senha sǜo obrigat��rios." });
            }

            var email = request.Email.Trim();
            if (await _db.Users.AnyAsync(u => u.Email == email))
                return Conflict(new { message = "E-mail jǭ cadastrado." });

            var cpf = (request.Cpf ?? string.Empty).Replace(".", "").Replace("-", "");
            var hash = Sha256Hex(request.Password);
            var user = new User
            {
                Cpf = cpf,
                Nome = request.Nome.Trim(),
                Email = email,
                Senha = hash,
                Cargo = "usuario",
                Nivel = null
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "dev_super_secret_please_change";
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "Pim4";
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "Pim4Client";
            var jwt = GenerateJwtToken(jwtSecret, issuer, audience, email, user.Id, "usuario");
            return Ok(new LoginResponse
            {
                Token = jwt.token,
                ExpiresAt = jwt.expiresAt,
                User = new { id = user.Id, email, nome = request.Nome.Trim(), cpf, cargo = "usuario", nivel = (int?)null }
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Credenciais invǟ��lidas." });
            }

            var debug = (Environment.GetEnvironmentVariable("DEBUG_LOGIN") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "dev_super_secret_please_change";
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "Pim4";
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "Pim4Client";

            // DEBUG: aceita credenciais padrǟ��o
            if (debug)
            {
                var dbgEmail = Environment.GetEnvironmentVariable("DEBUG_EMAIL") ?? "admin@demo.com";
                var dbgPass = Environment.GetEnvironmentVariable("DEBUG_PASSWORD") ?? "admin";
                if (request.Email.Equals(dbgEmail, StringComparison.OrdinalIgnoreCase) && request.Password == dbgPass)
                {
                    var tokenDbg = GenerateJwtToken(jwtSecret, issuer, audience, request.Email, 0, "admin");
                    return Ok(new LoginResponse
                    {
                        Token = tokenDbg.token,
                        ExpiresAt = tokenDbg.expiresAt,
                        User = new { id = 0, email = request.Email, nome = "Admin", cargo = "admin", nivel = 0 }
                    });
                }
            }

            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                return Unauthorized(new { message = "Credenciais invǭlidas." });
            }

            var ok = false;
            if (!string.IsNullOrEmpty(user.Senha))
            {
                var hashInput = Sha256Hex(request.Password);
                ok = string.Equals(user.Senha, hashInput, StringComparison.OrdinalIgnoreCase);
            }
            if (!ok)
            {
                return Unauthorized(new { message = "Credenciais invǭlidas." });
            }

            var token = GenerateJwtToken(jwtSecret, issuer, audience, user.Email, user.Id, user.Cargo);
            return Ok(new LoginResponse
            {
                Token = token.token,
                ExpiresAt = token.expiresAt,
                User = new
                {
                    id = user.Id,
                    email = user.Email,
                    nome = user.Nome ?? string.Empty,
                    cpf = user.Cpf ?? string.Empty,
                    cargo = user.Cargo ?? string.Empty,
                    nivel = user.Nivel
                }
            });
        }

        private static (string token, DateTime expiresAt) GenerateJwtToken(string secret, string issuer, string audience, string email, int userId, string cargo)
        {
            // Usa a mesma derivaǟ��ǟ��o de chave do Program.cs: se o segredo tiver menos de 32 bytes,
            // aplica SHA-256 para garantir 256 bits para HS256.
            byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
            if (keyBytes.Length < 32)
            {
                keyBytes = SHA256.HashData(keyBytes);
            }
            var signingKey = new SymmetricSecurityKey(keyBytes);
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            var expires = DateTime.UtcNow.AddHours(2);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, email),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, cargo ?? string.Empty)
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: expires,
                signingCredentials: creds
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expires);
        }

        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
