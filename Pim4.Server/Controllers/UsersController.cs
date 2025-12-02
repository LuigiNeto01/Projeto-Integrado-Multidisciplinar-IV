using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pim4.Server.Data;
using Pim4.Server.Data.Entities;
using System.Linq;

// UsersController
// CRUD de usuarios e atualizacao do proprio perfil usando Entity Framework.
// - Todas as rotas exigem autenticacao; a maioria exige papel admin.
// - GET/POST/PUT/DELETE /users: apenas admin.
// - PUT /users/me: usuario atualiza nome/email/senha; retorna novo JWT.
namespace Pim4.Server.Controllers
{
    [ApiController]
    [Route("users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;

        public UsersController(AppDbContext db)
        {
            _db = db;
        }

        public class UserDto
        {
            public int Id { get; set; }
            public string Cpf { get; set; } = string.Empty;
            public string Nome { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Cargo { get; set; } = string.Empty;
            public int? Nivel { get; set; }
        }

        public class CreateUserRequest
        {
            public string Cpf { get; set; } = string.Empty;
            public string Nome { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Senha { get; set; } = string.Empty;
            public string Cargo { get; set; } = string.Empty;
            public int? Nivel { get; set; }
        }

        public class UpdateUserRequest
        {
            public string? Cpf { get; set; }
            public string? Nome { get; set; }
            public string? Email { get; set; }
            public string? Senha { get; set; }
            public string? Cargo { get; set; }
            public int? Nivel { get; set; }
        }

        public class UpdateMeRequest
        {
            public string? Nome { get; set; }
            public string? Email { get; set; }
            public string? Senha { get; set; }
        }

        private static string Sha256Hex(string input)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static (string token, DateTime expiresAt) GenerateJwtToken(string secret, string issuer, string audience, string email, int userId, string cargo)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
            if (keyBytes.Length < 32)
            {
                keyBytes = SHA256.HashData(keyBytes);
            }
            var signingKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(keyBytes);
            var creds = new Microsoft.IdentityModel.Tokens.SigningCredentials(signingKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.HmacSha256);
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

        private static string? GetEmail(ClaimsPrincipal user)
        {
            return user?.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                ?? user?.FindFirst(ClaimTypes.Email)?.Value
                ?? user?.Identity?.Name;
        }

        private static bool HasAdminRole(ClaimsPrincipal user)
        {
            if (user == null) return false;
            if (user.IsInRole("admin")) return true;
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value);
            return roles.Any(r => string.Equals(r, "admin", StringComparison.OrdinalIgnoreCase));
        }

        private async Task<bool> IsAdminAsync(ClaimsPrincipal user)
        {
            if (HasAdminRole(user)) return true;
            var email = GetEmail(user);
            if (string.IsNullOrEmpty(email)) return false;
            var cargo = await _db.Users.AsNoTracking()
                .Where(u => u.Email == email)
                .Select(u => u.Cargo)
                .FirstOrDefaultAsync();
            return string.Equals(cargo, "admin", StringComparison.OrdinalIgnoreCase);
        }

        private static int? GetUserIdFromClaims(ClaimsPrincipal user)
        {
            var idStr = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(idStr, out var id)) return id;
            return null;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            if (!await IsAdminAsync(User)) return Forbid();

            var list = await _db.Users.AsNoTracking()
                .OrderByDescending(u => u.Id)
                .Select(u => new UserDto
                {
                    Id = u.Id,
                    Cpf = u.Cpf ?? string.Empty,
                    Nome = u.Nome ?? string.Empty,
                    Email = u.Email ?? string.Empty,
                    Cargo = u.Cargo ?? string.Empty,
                    Nivel = u.Nivel
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Nome) || string.IsNullOrWhiteSpace(body.Senha))
                return BadRequest(new { message = "Nome, email e senha são obrigatórios." });

            if (!await IsAdminAsync(User)) return Forbid();

            var hash = Sha256Hex(body.Senha);
            var cargo = body.Cargo ?? "usuario";
            var cpf = (body.Cpf ?? string.Empty).Replace(".", "").Replace("-", "");

            var entity = new User
            {
                Cpf = cpf,
                Nome = body.Nome.Trim(),
                Email = body.Email.Trim(),
                Senha = hash,
                Cargo = cargo,
                Nivel = body.Nivel
            };

            _db.Users.Add(entity);
            await _db.SaveChangesAsync();
            return Ok(new { id = entity.Id });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UpdateUserRequest body)
        {
            if (!await IsAdminAsync(User)) return Forbid();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return Ok(new { updated = 0 });

            if (body.Cpf != null) user.Cpf = body.Cpf.Replace(".", "").Replace("-", "");
            if (body.Nome != null) user.Nome = body.Nome.Trim();
            if (body.Email != null) user.Email = body.Email.Trim();
            if (body.Cargo != null) user.Cargo = body.Cargo;
            if (body.Nivel.HasValue) user.Nivel = body.Nivel;
            if (!string.IsNullOrEmpty(body.Senha)) user.Senha = Sha256Hex(body.Senha);

            var rows = await _db.SaveChangesAsync();
            return Ok(new { updated = rows });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            if (!await IsAdminAsync(User)) return Forbid();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return Ok(new { deleted = 0 });

            _db.Users.Remove(user);
            var rows = await _db.SaveChangesAsync();
            return Ok(new { deleted = rows });
        }

        // Atualiza o proprio perfil (sem exigir admin). Campos permitidos: nome, email, senha
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest body)
        {
            var myId = GetUserIdFromClaims(User);
            if (myId == null)
            {
                var emailClaim = GetEmail(User);
                if (string.IsNullOrEmpty(emailClaim)) return Unauthorized(new { message = "Sessao invalida." });
                myId = await _db.Users
                    .Where(u => u.Email == emailClaim)
                    .Select(u => (int?)u.Id)
                    .FirstOrDefaultAsync();
                if (myId == null) return Unauthorized(new { message = "Usuario nao encontrado." });
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == myId.Value);
            if (user == null) return NotFound(new { message = "Usuario nao encontrado." });
            if (!string.IsNullOrWhiteSpace(body.Nome)) user.Nome = body.Nome.Trim();
            if (!string.IsNullOrWhiteSpace(body.Email)) user.Email = body.Email.Trim();
            if (!string.IsNullOrEmpty(body.Senha)) user.Senha = Sha256Hex(body.Senha);

            var rows = await _db.SaveChangesAsync();

            var dto = new UserDto
            {
                Id = user.Id,
                Cpf = user.Cpf ?? string.Empty,
                Nome = user.Nome ?? string.Empty,
                Email = user.Email ?? string.Empty,
                Cargo = user.Cargo ?? string.Empty,
                Nivel = user.Nivel
            };

            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "dev_super_secret_please_change";
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "Pim4";
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "Pim4Client";
            var jwt = GenerateJwtToken(jwtSecret, issuer, audience, dto.Email, dto.Id, dto.Cargo ?? string.Empty);
            return Ok(new { updated = rows, user = dto, token = jwt.token, expiresAt = jwt.expiresAt });
        }
    }
}
