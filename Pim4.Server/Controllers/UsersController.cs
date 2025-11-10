using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Pim4.Server.Data;

// UsersController
// Eu explico: CRUD de usuarios e atualizacao do proprio perfil.
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

        private static async Task<bool> IsAdmin(ClaimsPrincipal user, NpgsqlConnection conn, string? email)
        {
            // Checa primeiro via Claims do usuário corrente (tolerante a maiúsculas/minúsculas)
            if (HasAdminRole(user)) return true;
            if (string.IsNullOrEmpty(email)) return false;
            await using var cmd = new NpgsqlCommand(Sql.Users.GetCargoByEmail, conn);
            cmd.Parameters.AddWithValue("@e", email);
            var cargo = await cmd.ExecuteScalarAsync() as string;
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
            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString)) return Unauthorized(new { message = "Banco não configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            var email = GetEmail(User);
            if (!await IsAdmin(User, conn, email)) return Forbid();

            var list = new List<UserDto>();
            await using var cmd = new NpgsqlCommand(Sql.Users.ListAll, conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var dto = new UserDto
                {
                    Id = r.GetInt32(r.GetOrdinal("id")),
                    Cpf = r.IsDBNull(r.GetOrdinal("cpf")) ? "" : r.GetString(r.GetOrdinal("cpf")),
                    Nome = r.IsDBNull(r.GetOrdinal("nome")) ? "" : r.GetString(r.GetOrdinal("nome")),
                    Email = r.IsDBNull(r.GetOrdinal("email")) ? "" : r.GetString(r.GetOrdinal("email")),
                    Cargo = r.IsDBNull(r.GetOrdinal("cargo")) ? "" : r.GetString(r.GetOrdinal("cargo")),
                    Nivel = r.IsDBNull(r.GetOrdinal("nivel")) ? (int?)null : r.GetInt32(r.GetOrdinal("nivel"))
                };
                list.Add(dto);
            }
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Nome) || string.IsNullOrWhiteSpace(body.Senha))
                return BadRequest(new { message = "Nome, email e senha são obrigatórios." });

            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString)) return Unauthorized(new { message = "Banco não configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();
            var emailReq = GetEmail(User);
            if (!await IsAdmin(User, conn, emailReq)) return Forbid();

            var hash = Sha256Hex(body.Senha);
            var cargo = body.Cargo ?? "usuario";
            var nivel = (object?)body.Nivel ?? DBNull.Value;
            var cpf = (body.Cpf ?? string.Empty).Replace(".", "").Replace("-", "");

            await using var cmd = new NpgsqlCommand(Sql.Users.Insert, conn);
            cmd.Parameters.AddWithValue("@cpf", cpf);
            cmd.Parameters.AddWithValue("@nome", body.Nome.Trim());
            cmd.Parameters.AddWithValue("@email", body.Email.Trim());
            cmd.Parameters.AddWithValue("@senha", hash);
            cmd.Parameters.AddWithValue("@cargo", cargo);
            cmd.Parameters.AddWithValue("@nivel", nivel);
            var newId = await cmd.ExecuteScalarAsync();
            return Ok(new { id = Convert.ToInt32(newId) });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update([FromRoute] int id, [FromBody] UpdateUserRequest body)
        {
            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString)) return Unauthorized(new { message = "Banco não configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();
            var emailReq = GetEmail(User);
            if (!await IsAdmin(User, conn, emailReq)) return Forbid();
        
            var sets = new List<string>();
            var cmd = new NpgsqlCommand();
            cmd.Connection = conn;
            cmd.CommandText = string.Format(Sql.Users.UpdateTemplate, string.Join(", ", sets));
            cmd.Parameters.AddWithValue("@id", id);

            if (body.Cpf != null) { sets.Add("cpf=@cpf"); cmd.Parameters.AddWithValue("@cpf", body.Cpf.Replace(".", "").Replace("-", "")); }
            if (body.Nome != null) { sets.Add("nome=@nome"); cmd.Parameters.AddWithValue("@nome", body.Nome.Trim()); }
            if (body.Email != null) { sets.Add("email=@email"); cmd.Parameters.AddWithValue("@email", body.Email.Trim()); }
            if (body.Cargo != null) { sets.Add("cargo=@cargo"); cmd.Parameters.AddWithValue("@cargo", body.Cargo); }
            if (body.Nivel.HasValue) { sets.Add("nivel=@nivel"); cmd.Parameters.AddWithValue("@nivel", body.Nivel.Value); }
            if (!string.IsNullOrEmpty(body.Senha)) { sets.Add("senha=@senha"); cmd.Parameters.AddWithValue("@senha", Sha256Hex(body.Senha)); }

            if (sets.Count == 0) return BadRequest(new { message = "Nada para atualizar." });
            cmd.CommandText = string.Format(Sql.Users.UpdateTemplate, string.Join(", ", sets));
            var rows = await cmd.ExecuteNonQueryAsync();
            return Ok(new { updated = rows });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete([FromRoute] int id)
        {
            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString)) return Unauthorized(new { message = "Banco não configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();
            var emailReq = User?.Identity?.Name;
            if (!await IsAdmin(User, conn, emailReq)) return Forbid();

            await using var cmd = new NpgsqlCommand(Sql.Users.Delete, conn);
            cmd.Parameters.AddWithValue("@id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return Ok(new { deleted = rows });
        }

        // Atualiza o próprio perfil (sem exigir admin). Campos permitidos: nome, email, senha
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateMeRequest body)
        {
            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString)) return Unauthorized(new { message = "Banco não configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            // Descobre o id do usuario autenticado
            var myId = GetUserIdFromClaims(User);
            if (myId == null)
            {
                var emailClaim = GetEmail(User);
                if (string.IsNullOrEmpty(emailClaim)) return Unauthorized(new { message = "Sessão inválida." });
            await using var q = new NpgsqlCommand(Sql.Users.GetIdByEmail, conn);
            q.Parameters.AddWithValue("@e", emailClaim);
            var obj = await q.ExecuteScalarAsync();
                if (obj == null) return Unauthorized(new { message = "Usuário não encontrado." });
                myId = Convert.ToInt32(obj);
            }

            var sets = new List<string>();
            await using var cmd = new NpgsqlCommand();
            cmd.Connection = conn;
            cmd.Parameters.AddWithValue("@id", myId.Value);
            cmd.CommandText = "UPDATE public.\"user\" SET " + string.Join(", ", sets) + " WHERE id=@id";

            if (!string.IsNullOrWhiteSpace(body.Nome)) { sets.Add("nome=@nome"); cmd.Parameters.AddWithValue("@nome", body.Nome.Trim()); }
            if (!string.IsNullOrWhiteSpace(body.Email)) { sets.Add("email=@email"); cmd.Parameters.AddWithValue("@email", body.Email.Trim()); }
            if (!string.IsNullOrEmpty(body.Senha)) { sets.Add("senha=@senha"); cmd.Parameters.AddWithValue("@senha", Sha256Hex(body.Senha)); }

            if (sets.Count == 0) return BadRequest(new { message = "Nada para atualizar." });
            cmd.CommandText = "UPDATE public.\"user\" SET " + string.Join(", ", sets) + " WHERE id=@id";
            var rows = await cmd.ExecuteNonQueryAsync();
            if (rows <= 0) return NotFound(new { message = "Usuário não encontrado." });

            await using var get = new NpgsqlCommand(Sql.Users.SelectByIdBasic, conn);
            get.Parameters.AddWithValue("@id", myId.Value);
            await using var r = await get.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return NotFound(new { message = "Usuário não encontrado." });
            var dto = new UserDto
            {
                Id = r.GetInt32(r.GetOrdinal("id")),
                Cpf = r.IsDBNull(r.GetOrdinal("cpf")) ? "" : r.GetString(r.GetOrdinal("cpf")),
                Nome = r.IsDBNull(r.GetOrdinal("nome")) ? "" : r.GetString(r.GetOrdinal("nome")),
                Email = r.IsDBNull(r.GetOrdinal("email")) ? "" : r.GetString(r.GetOrdinal("email")),
                Cargo = r.IsDBNull(r.GetOrdinal("cargo")) ? "" : r.GetString(r.GetOrdinal("cargo")),
                Nivel = r.IsDBNull(r.GetOrdinal("nivel")) ? (int?)null : r.GetInt32(r.GetOrdinal("nivel"))
            };
            // Emite novo token para refletir email/claims atualizados
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "dev_super_secret_please_change";
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "Pim4";
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "Pim4Client";
            var jwt = GenerateJwtToken(jwtSecret, issuer, audience, dto.Email, dto.Id, dto.Cargo ?? string.Empty);
            return Ok(new { updated = rows, user = dto, token = jwt.token, expiresAt = jwt.expiresAt });
        }
    }
}
