using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Pim4.Server.Data;

// ChatController
// Eu explico: mensagens de chat por chamado.
// - GET /chat/{chamadoId}/messages: lista mensagens (join com usuario p/ nome).
// - POST /chat/{chamadoId}/messages: envia mensagem como usuario autenticado.

namespace Pim4.Server.Controllers
{
    [ApiController]
    [Route("chat")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        public class ChatMessageDto
        {
            public int Id { get; set; }
            public int IdChamado { get; set; }
            public int IdUsuario { get; set; }
            public string Nome { get; set; } = string.Empty;
            public string Mensagem { get; set; } = string.Empty;
            public DateTime DataEnvio { get; set; }
        }

        public class SendMessageRequest
        {
            public string Mensagem { get; set; } = string.Empty;
        }

        private static string? GetEmail(ClaimsPrincipal user)
        {
            return user?.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                ?? user?.FindFirst(ClaimTypes.Email)?.Value
                ?? user?.Identity?.Name;
        }

        private static async Task<int?> GetCurrentUserIdAsync(ClaimsPrincipal user, NpgsqlConnection conn)
        {
            var idStr = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(idStr, out var idParsed)) return idParsed;
            var email = GetEmail(user);
            if (string.IsNullOrWhiteSpace(email)) return null;
            await using var q = new NpgsqlCommand("SELECT id FROM public.\"user\" WHERE email=@e LIMIT 1", conn);
            q.Parameters.AddWithValue("@e", email);
            var obj = await q.ExecuteScalarAsync();
            return obj == null ? null : Convert.ToInt32(obj);
        }

        [HttpGet("{chamadoId:int}/messages")]
        public async Task<IActionResult> ListMessages([FromRoute] int chamadoId)
        {
            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString)) return Unauthorized(new { message = "Banco não configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            var list = new List<ChatMessageDto>();
            await using var cmd = new NpgsqlCommand(Sql.Chat.ListMessages, conn);
            cmd.Parameters.AddWithValue("@id", chamadoId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var dto = new ChatMessageDto
                {
                    Id = r.GetInt32(r.GetOrdinal("id")),
                    IdChamado = r.GetInt32(r.GetOrdinal("id_chamado")),
                    IdUsuario = r.GetInt32(r.GetOrdinal("id_usuario")),
                    Nome = r.IsDBNull(r.GetOrdinal("nome")) ? string.Empty : r.GetString(r.GetOrdinal("nome")),
                    Mensagem = r.IsDBNull(r.GetOrdinal("mensagem")) ? string.Empty : r.GetString(r.GetOrdinal("mensagem")),
                    DataEnvio = r.IsDBNull(r.GetOrdinal("data_envio")) ? DateTime.MinValue : r.GetDateTime(r.GetOrdinal("data_envio"))
                };
                list.Add(dto);
            }

            return Ok(list);
        }

        [HttpPost("{chamadoId:int}/messages")]
        public async Task<IActionResult> SendMessage([FromRoute] int chamadoId, [FromBody] SendMessageRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.Mensagem)) return BadRequest(new { message = "Mensagem vazia." });
            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString)) return Unauthorized(new { message = "Banco não configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            var userId = await GetCurrentUserIdAsync(User, conn);
            if (userId is null) return Unauthorized(new { message = "Sessão inválida." });

            await using var cmd = new NpgsqlCommand(Sql.Chat.InsertMessage, conn);
            cmd.Parameters.AddWithValue("@ch", chamadoId);
            cmd.Parameters.AddWithValue("@u", userId.Value);
            cmd.Parameters.AddWithValue("@m", body.Mensagem);
            cmd.Parameters.AddWithValue("@d", DateTime.UtcNow);
            var newId = await cmd.ExecuteScalarAsync();
            return Ok(new { id = Convert.ToInt32(newId) });
        }
    }
}
