using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using System.Security.Cryptography;
using Pim4.Server.Data;

// AuthController
// Eu explico: endpoints de autenticacao da API.
// - POST /auth/register: cria usuario basico (cargo 'usuario') e retorna JWT.
// - POST /auth/login: autentica credenciais e emite JWT com claims (email, role, nameid).
// Notas: chaves JWT e conexao ao banco vem de variaveis de ambiente (.env)
namespace Pim4.Server.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
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
                return BadRequest(new { message = "Nome, e-mail e senha são obrigatórios." });
            }

            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString))
            {
                return Unauthorized(new { message = "Banco não configurado." });
            }

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            // Verifica se email já existe
            await using (var exists = new NpgsqlCommand(Sql.Auth.ExistsEmail, conn))
            {
                exists.Parameters.AddWithValue("@e", request.Email.Trim());
                var has = await exists.ExecuteScalarAsync();
                if (has != null) return Conflict(new { message = "E-mail já cadastrado." });
            }

            var cpf = (request.Cpf ?? string.Empty).Replace(".", "").Replace("-", "");
            var hash = Sha256Hex(request.Password);
            await using (var cmd = new NpgsqlCommand(Sql.Auth.InsertUser, conn))
            {
                cmd.Parameters.AddWithValue("@cpf", cpf);
                cmd.Parameters.AddWithValue("@nome", request.Nome.Trim());
                cmd.Parameters.AddWithValue("@email", request.Email.Trim());
                cmd.Parameters.AddWithValue("@senha", hash);
                cmd.Parameters.AddWithValue("@cargo", "usuario");
                cmd.Parameters.AddWithValue("@nivel", DBNull.Value);
                var newId = await cmd.ExecuteScalarAsync();

                // Auto login
                var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "dev_super_secret_please_change";
                var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "Pim4";
                var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "Pim4Client";
                var jwt = GenerateJwtToken(jwtSecret, issuer, audience, request.Email.Trim(), Convert.ToInt32(newId), "usuario");
                return Ok(new LoginResponse
                {
                    Token = jwt.token,
                    ExpiresAt = jwt.expiresAt,
                    User = new { id = Convert.ToInt32(newId), email = request.Email.Trim(), nome = request.Nome.Trim(), cpf, cargo = "usuario", nivel = (int?)null }
                });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "Credenciais invÃ¡lidas." });
            }

            var debug = (Environment.GetEnvironmentVariable("DEBUG_LOGIN") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase);
            var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "dev_super_secret_please_change";
            var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "Pim4";
            var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "Pim4Client";

            // DEBUG: aceita credenciais padrÃ£o
            if (debug)
            {
                var dbgEmail = Environment.GetEnvironmentVariable("DEBUG_EMAIL") ?? "admin@demo.com";
                var dbgPass = Environment.GetEnvironmentVariable("DEBUG_PASSWORD") ?? "admin";
                if (request.Email.Equals(dbgEmail, StringComparison.OrdinalIgnoreCase) && request.Password == dbgPass)
                {
                    var token = GenerateJwtToken(jwtSecret, issuer, audience, request.Email, 0, "admin");
                    return Ok(new LoginResponse
                    {
                        Token = token.token,
                        ExpiresAt = token.expiresAt,
                        User = new { id = 0, email = request.Email, nome = "Admin", cargo = "admin", nivel = 0 }
                    });
                }
            }

            // ProduÃ§Ã£o: verificar no PostgreSQL (DB: Pim, tabela: user)
            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString))
            {
                // Sem DB: negar
                return Unauthorized(new { message = "Banco não configurado." });
            }

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            await using var cmd = new NpgsqlCommand(Sql.Auth.SelectUserByEmail, conn);
            cmd.Parameters.AddWithValue("@e", request.Email);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return Unauthorized(new { message = "Credenciais inválidas." });
            }

            var id = reader.GetInt32(reader.GetOrdinal("id"));
            var email = reader.GetString(reader.GetOrdinal("email"));
            var nome = reader.IsDBNull(reader.GetOrdinal("nome")) ? "" : reader.GetString(reader.GetOrdinal("nome"));
            var senha = reader.IsDBNull(reader.GetOrdinal("senha")) ? null : reader.GetString(reader.GetOrdinal("senha"));
            var cargo = reader.IsDBNull(reader.GetOrdinal("cargo")) ? "" : reader.GetString(reader.GetOrdinal("cargo"));
            int nivel = 0;
            if (!reader.IsDBNull(reader.GetOrdinal("nivel")))
            {
                // suporta smallint/int
                try { nivel = reader.GetInt32(reader.GetOrdinal("nivel")); }
                catch { nivel = reader.GetInt16(reader.GetOrdinal("nivel")); }
            }
            var cpf = reader.IsDBNull(reader.GetOrdinal("cpf")) ? "" : reader.GetString(reader.GetOrdinal("cpf"));

            // VerificaÃ§Ã£o com hash SHA-256 (hex) conforme projeto anterior
            var ok = false;
            if (!string.IsNullOrEmpty(senha))
            {
                var hashInput = Sha256Hex(request.Password);
                // compara ignorando caixa
                ok = string.Equals(senha, hashInput, StringComparison.OrdinalIgnoreCase);
            }
            if (!ok)
            {
                return Unauthorized(new { message = "Credenciais inválidas." });
            }

            var jwt = GenerateJwtToken(jwtSecret, issuer, audience, email, id, cargo);
            return Ok(new LoginResponse
            {
                Token = jwt.token,
                ExpiresAt = jwt.expiresAt,
                User = new { id, email, nome, cpf, cargo, nivel }
            });
        }

        private static (string token, DateTime expiresAt) GenerateJwtToken(string secret, string issuer, string audience, string email, int userId, string cargo)
        {
            // Usa a mesma derivaÃ§Ã£o de chave do Program.cs: se o segredo tiver menos de 32 bytes,
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




