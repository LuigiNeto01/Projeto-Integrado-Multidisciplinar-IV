using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pim4.Server.Data;
using Pim4.Server.Data.Entities;
using System.Linq;

// ChamadosController
// operacoes de chamados (listar/criar) agora via Entity Framework.
// - GET /chamados: lista conforme cargo (usuario ve os seus; suporte/admin veem mais).
// - POST /chamados: cria um chamado; prioridade pode ser mapeada por motivo.
// - PUT /chamados/{id}/close|reopen: alterna status resolvido.
namespace Pim4.Server.Controllers
{
    [ApiController]
    [Route("chamados")]
    [Authorize]
    public class ChamadosController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ChamadosController(AppDbContext db)
        {
            _db = db;
        }

        public class ChamadoDto
        {
            public int Id { get; set; }
            public string Titulo { get; set; } = string.Empty;
            public string Motivo { get; set; } = string.Empty;
            public string? Descricao { get; set; }
            public int Prioridade { get; set; }
            public bool Resolvido { get; set; }
            public DateTime? DataCriacao { get; set; }
            public int? UsuarioCriadorId { get; set; }
            public string? NomeCriador { get; set; }
        }

        public class CriarChamadoRequest
        {
            public string Titulo { get; set; } = string.Empty;
            public string Motivo { get; set; } = string.Empty;
            public string? Descricao { get; set; }
            public int? Prioridade { get; set; }
        }

        public class ChamadoQuery
        {
            public int? UserId { get; set; }
        }

        private static string? GetEmail(ClaimsPrincipal user)
        {
            return user?.FindFirst("email")?.Value
                ?? user?.FindFirst(ClaimTypes.Email)?.Value
                ?? user?.FindFirst(JwtRegisteredClaimNames.Email)?.Value
                ?? user?.Identity?.Name;
        }

        private async Task<(int? id, string cargo, int nivel)> GetCurrentUserAsync()
        {
            var email = GetEmail(User);
            if (string.IsNullOrEmpty(email)) return (null, string.Empty, 0);

            var data = await _db.Users.AsNoTracking()
                .Where(u => u.Email == email)
                .Select(u => new { u.Id, Cargo = u.Cargo ?? string.Empty, Nivel = u.Nivel ?? 0 })
                .FirstOrDefaultAsync();

            if (data == null) return (null, string.Empty, 0);
            return (data.Id, data.Cargo, data.Nivel);
        }

        private static int ResolvePrioridade(CriarChamadoRequest req)
        {
            if (req.Prioridade.HasValue) return req.Prioridade.Value;
            var m = (req.Motivo ?? string.Empty).Trim().ToLowerInvariant();
            return m switch
            {
                "problemas com o mouse" => 3,
                "problemas com som" => 4,
                "problema com video" => 2,
                "problemas com a internet" => 1,
                _ => 4,
            };
        }

        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var (userId, cargo, nivel) = await GetCurrentUserAsync();
            if (userId is null) return Unauthorized(new { message = "Usuário nao encontrado." });

            var query = _db.Chamados
                .AsNoTracking()
                .Include(c => c.UsuarioCriador)
                .AsQueryable();

            var isUsuario = string.Equals(cargo, "usuario", StringComparison.OrdinalIgnoreCase);
            var isSuporte = string.Equals(cargo, "suporte", StringComparison.OrdinalIgnoreCase);

            if (isUsuario)
                query = query.Where(c => c.UsuarioCriadorId == userId);
            if (isSuporte && nivel > 0)
                query = query.Where(c => c.Prioridade >= nivel);

            var lista = await query
                .OrderByDescending(c => c.Id)
                .Take(200)
                .Select(c => new ChamadoDto
                {
                    Id = c.Id,
                    Titulo = c.Titulo ?? string.Empty,
                    Motivo = c.Motivo ?? string.Empty,
                    Descricao = c.Descricao,
                    Prioridade = c.Prioridade,
                    Resolvido = c.Resolvido,
                    DataCriacao = c.DataCriacao,
                    UsuarioCriadorId = c.UsuarioCriadorId,
                    NomeCriador = c.UsuarioCriador != null ? c.UsuarioCriador.Nome : null
                })
                .ToListAsync();

            return Ok(lista);
        }

        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] CriarChamadoRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Titulo) || string.IsNullOrWhiteSpace(req.Motivo))
                return BadRequest(new { message = "Título e Motivo são obrigatórios." });

            var (userId, cargo, nivel) = await GetCurrentUserAsync();
            if (userId is null) return Unauthorized(new { message = "Usuário não encontrado." });

            var prioridade = ResolvePrioridade(req);

            var entity = new Chamado
            {
                Titulo = req.Titulo.Trim(),
                Motivo = req.Motivo.Trim(),
                Descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim(),
                Prioridade = prioridade,
                DataCriacao = DateTime.UtcNow,
                Resolvido = false,
                UsuarioCriadorId = userId
            };

            _db.Chamados.Add(entity);
            await _db.SaveChangesAsync();
            return Ok(new { id = entity.Id, message = "Chamado criado" });
        }

        [HttpPost("by-user")]
        public async Task<IActionResult> ListarPorUsuario([FromBody] ChamadoQuery queryBody)
        {
            var (userId, cargo, nivel) = await GetCurrentUserAsync();
            if (userId is null) return Unauthorized(new { message = "Usuário nao encontrado." });

            var requestedUserId = queryBody?.UserId ?? userId;
            var isUsuario = string.Equals(cargo, "usuario", StringComparison.OrdinalIgnoreCase);
            var isSuporte = string.Equals(cargo, "suporte", StringComparison.OrdinalIgnoreCase);

            if (isUsuario && requestedUserId != userId)
                return Forbid();

            var query = _db.Chamados
                .AsNoTracking()
                .Include(c => c.UsuarioCriador)
                .AsQueryable();

            if (requestedUserId.HasValue)
                query = query.Where(c => c.UsuarioCriadorId == requestedUserId.Value);
            if (isSuporte && nivel > 0)
                query = query.Where(c => c.Prioridade >= nivel);

            var lista = await query
                .OrderByDescending(c => c.Id)
                .Take(200)
                .Select(c => new ChamadoDto
                {
                    Id = c.Id,
                    Titulo = c.Titulo ?? string.Empty,
                    Motivo = c.Motivo ?? string.Empty,
                    Descricao = c.Descricao,
                    Prioridade = c.Prioridade,
                    Resolvido = c.Resolvido,
                    DataCriacao = c.DataCriacao,
                    UsuarioCriadorId = c.UsuarioCriadorId,
                    NomeCriador = c.UsuarioCriador != null ? c.UsuarioCriador.Nome : null
                })
                .ToListAsync();

            return Ok(lista);
        }

        [HttpPut("{id:int}/close")]
        public async Task<IActionResult> Close([FromRoute] int id)
        {
            var chamado = await _db.Chamados.FirstOrDefaultAsync(c => c.Id == id);
            if (chamado == null) return Ok(new { updated = 0 });

            chamado.Resolvido = true;
            var rows = await _db.SaveChangesAsync();
            return Ok(new { updated = rows });
        }

        [HttpPut("{id:int}/reopen")]
        public async Task<IActionResult> Reopen([FromRoute] int id)
        {
            var chamado = await _db.Chamados.FirstOrDefaultAsync(c => c.Id == id);
            if (chamado == null) return Ok(new { updated = 0 });

            chamado.Resolvido = false;
            var rows = await _db.SaveChangesAsync();
            return Ok(new { updated = rows });
        }
    }
}
