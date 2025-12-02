using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pim4.Server.Data;
using Pim4.Server.Data.Entities;
using System.Linq;

// ChatController
// mensagens de chat por chamado usando Entity Framework.
// - GET /chat/{chamadoId}/messages: lista mensagens (join com usuario p/ nome).
// - POST /chat/{chamadoId}/messages: envia mensagem como usuario autenticado.
namespace Pim4.Server.Controllers
{
    [ApiController]
    [Route("chat")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ChatController(AppDbContext db)
        {
            _db = db;
        }

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

        private static string? GetEmail(ClaimsPrincipal? user)
        {
            return user?.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                ?? user?.FindFirst(ClaimTypes.Email)?.Value
                ?? user?.Identity?.Name;
        }

        private async Task<int?> GetCurrentUserIdAsync()
        {
            var idStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(idStr, out var idParsed)) return idParsed;
            var email = GetEmail(User);
            if (string.IsNullOrWhiteSpace(email)) return null;
            return await _db.Users
                .Where(u => u.Email == email)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync();
        }

        [HttpGet("{chamadoId:int}/messages")]
        public async Task<IActionResult> ListMessages([FromRoute] int chamadoId)
        {
            var list = await _db.ChatMessages.AsNoTracking()
                .Include(m => m.Usuario)
                .Where(m => m.IdChamado == chamadoId)
                .OrderBy(m => m.DataEnvio)
                .ThenBy(m => m.Id)
                .Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    IdChamado = m.IdChamado,
                    IdUsuario = m.IdUsuario,
                    Nome = m.Usuario != null ? m.Usuario.Nome ?? string.Empty : string.Empty,
                    Mensagem = m.Mensagem ?? string.Empty,
                    DataEnvio = m.DataEnvio
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("{chamadoId:int}/messages")]
        public async Task<IActionResult> SendMessage([FromRoute] int chamadoId, [FromBody] SendMessageRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.Mensagem)) return BadRequest(new { message = "Mensagem vazia." });

            var userId = await GetCurrentUserIdAsync();
            if (userId is null) return Unauthorized(new { message = "Sessão inválida." });

            var message = new ChatMessage
            {
                IdChamado = chamadoId,
                IdUsuario = userId.Value,
                Mensagem = body.Mensagem,
                DataEnvio = DateTime.UtcNow
            };

            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync();
            return Ok(new { id = message.Id });
        }
    }
}
