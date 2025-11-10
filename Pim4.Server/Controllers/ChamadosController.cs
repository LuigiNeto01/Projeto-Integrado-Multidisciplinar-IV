using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;

// ChamadosController
// Eu explico: operacoes de chamados (listar/criar) respeitando papel/nivel.
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

        // Auxiliares para endpoints de update de status
        private static async Task<bool> __TableExists(NpgsqlConnection conn, string schema, string table)
        {
            const string sql = "SELECT 1 FROM information_schema.tables WHERE table_schema = @s AND table_name = @t LIMIT 1";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@s", schema);
            cmd.Parameters.AddWithValue("@t", table);
            var res = await cmd.ExecuteScalarAsync();
            return res != null;
        }

        private static async Task<string?> __ResolveChamadosTable(NpgsqlConnection conn)
        {
            var envTable = Environment.GetEnvironmentVariable("CHAMADOS_TABLE");
            if (!string.IsNullOrWhiteSpace(envTable) && await __TableExists(conn, "public", envTable)) return envTable;
            var candidates = new[] { "chamado", "tarefa", "chamados" };
            foreach (var t in candidates)
                if (await __TableExists(conn, "public", t)) return t;
            return null;
        }

        private static async Task<bool> __ColumnExists(NpgsqlConnection conn, string table, string column)
        {
            const string csql = "SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @t AND column_name = @c LIMIT 1";
            await using var cmd = new NpgsqlCommand(csql, conn);
            cmd.Parameters.AddWithValue("@t", table);
            cmd.Parameters.AddWithValue("@c", column);
            var r = await cmd.ExecuteScalarAsync();
            return r != null;
        }

        [HttpGet]
        public async Task<IActionResult> Listar()
        {
            var email =
                User?.FindFirst("email")?.Value ??
                User?.FindFirst(ClaimTypes.Email)?.Value ??
                User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value ??
                User?.Identity?.Name;
            if (string.IsNullOrEmpty(email)) return Unauthorized();

            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString))
                return Unauthorized(new { message = "Banco não configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            // Carrega dados do usuário logado (id, cargo, nivel)
            int? userId = null;
            string cargo = "";
            int nivel = 0;
            await using (var cmdUser = new NpgsqlCommand("SELECT id, cargo, nivel FROM public.\"user\" WHERE email = @e LIMIT 1", conn))
            {
                cmdUser.Parameters.AddWithValue("@e", email);
                await using var r = await cmdUser.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    userId = r.IsDBNull(r.GetOrdinal("id")) ? null : r.GetInt32(r.GetOrdinal("id"));
                    cargo = r.IsDBNull(r.GetOrdinal("cargo")) ? "" : r.GetString(r.GetOrdinal("cargo"));
                    nivel = r.IsDBNull(r.GetOrdinal("nivel")) ? 0 : r.GetInt32(r.GetOrdinal("nivel"));
                }
            }

            if (userId is null)
                return Unauthorized(new { message = "Usuário não encontrado." });

            var lista = new List<ChamadoDto>();

            static async Task<bool> TableExists(NpgsqlConnection conn, string schema, string table)
            {
                const string sql = "SELECT 1 FROM information_schema.tables WHERE table_schema = @s AND table_name = @t LIMIT 1";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@s", schema);
                cmd.Parameters.AddWithValue("@t", table);
                var res = await cmd.ExecuteScalarAsync();
                return res != null;
            }

            static async Task<string?> ResolveTable(NpgsqlConnection conn)
            {
                var envTable = Environment.GetEnvironmentVariable("CHAMADOS_TABLE");
                if (!string.IsNullOrWhiteSpace(envTable))
                {
                    if (await TableExists(conn, "public", envTable)) return envTable;
                }
                var candidates = new[] { "chamado", "tarefa", "chamados" };
                foreach (var t in candidates)
                {
                    if (await TableExists(conn, "public", t)) return t;
                }
                return null;
            }

            static async Task<string?> ResolveUserIdColumn(NpgsqlConnection conn, string table)
            {
                async Task<bool> Col(string c)
                {
                    const string csql = "SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @t AND column_name = @c LIMIT 1";
                    await using var cmd = new NpgsqlCommand(csql, conn);
                    cmd.Parameters.AddWithValue("@t", table);
                    cmd.Parameters.AddWithValue("@c", c);
                    var r = await cmd.ExecuteScalarAsync();
                    return r != null;
                }
                var cols = new[] { "id_usuario_criador", "id_usuario", "usuario_id", "id_criador" };
                foreach (var c in cols)
                    if (await Col(c)) return c;
                return null;
            }

            async Task FetchFromTable(string tableName)
            {
                var isUsuario = string.Equals(cargo, "usuario", StringComparison.OrdinalIgnoreCase);
                var isSuporte = string.Equals(cargo, "suporte", StringComparison.OrdinalIgnoreCase);

                var userIdCol = await ResolveUserIdColumn(conn, tableName);
                if (string.IsNullOrEmpty(userIdCol)) return;

                var sb = new System.Text.StringBuilder();
                sb.Append($"SELECT c.id, c.titulo, c.motivo, c.descricao, c.prioridade, c.resolvido, c.data_criacao, c.{userIdCol} AS usuario_criador, u.nome AS nome_criador FROM public.\"{tableName}\" c LEFT JOIN public.\"user\" u ON u.id = c.{userIdCol} ");
                var where = new List<string>();
                if (isUsuario) where.Add($"c.{userIdCol} = @id_usuario");
                if (isSuporte && nivel > 0)
                {
                    where.Add("c.prioridade >= @nivel");
                }
                if (where.Count > 0)
                {
                    sb.Append(" WHERE ");
                    sb.Append(string.Join(" AND ", where));
                }
                sb.Append(" ORDER BY c.id DESC LIMIT 200");

                await using var cmd = new NpgsqlCommand(sb.ToString(), conn);
                if (isUsuario)
                    cmd.Parameters.AddWithValue("@id_usuario", userId!.Value);
                if (isSuporte && nivel > 0)
                    cmd.Parameters.AddWithValue("@nivel", nivel);

                try
                {
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var dto = new ChamadoDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("id")),
                            Titulo = reader.IsDBNull(reader.GetOrdinal("titulo")) ? string.Empty : reader.GetString(reader.GetOrdinal("titulo")),
                            Motivo = reader.IsDBNull(reader.GetOrdinal("motivo")) ? string.Empty : reader.GetString(reader.GetOrdinal("motivo")),
                            Descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader.GetString(reader.GetOrdinal("descricao")),
                            Prioridade = reader.IsDBNull(reader.GetOrdinal("prioridade")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("prioridade"))),
                            Resolvido = !reader.IsDBNull(reader.GetOrdinal("resolvido")) && Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("resolvido"))),
                            DataCriacao = reader.IsDBNull(reader.GetOrdinal("data_criacao")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("data_criacao")),
                            UsuarioCriadorId = reader.IsDBNull(reader.GetOrdinal("usuario_criador")) ? (int?)null : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("usuario_criador"))),
                            NomeCriador = reader.IsDBNull(reader.GetOrdinal("nome_criador")) ? null : reader.GetString(reader.GetOrdinal("nome_criador")),
                        };
                        lista.Add(dto);
                    }
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01")
                {
                    // tabela inexistente -> ignora
                }
            }

            var table = await ResolveTable(conn);
            if (table != null) await FetchFromTable(table);
            // nenhum existe -> lista permanece vazia

            return Ok(lista);
        }

        [HttpPost]
        public async Task<IActionResult> Criar([FromBody] CriarChamadoRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Titulo) || string.IsNullOrWhiteSpace(req.Motivo))
                return BadRequest(new { message = "Título e Motivo são obrigatórios." });

            var email =
                User?.FindFirst("email")?.Value ??
                User?.FindFirst(ClaimTypes.Email)?.Value ??
                User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value ??
                User?.Identity?.Name;

            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString))
                return Unauthorized(new { message = "Banco não configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            // Descobre usuário
            int? userId = null;
            string cargo = "";
            int nivel = 0;
            await using (var cmdUser = new NpgsqlCommand("SELECT id, cargo, nivel FROM public.\"user\" WHERE email = @e LIMIT 1", conn))
            {
                cmdUser.Parameters.AddWithValue("@e", (object?)email ?? DBNull.Value);
                await using var r = await cmdUser.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    userId = r.GetInt32(r.GetOrdinal("id"));
                    cargo = r.IsDBNull(r.GetOrdinal("cargo")) ? "" : r.GetString(r.GetOrdinal("cargo"));
                    try { nivel = r.IsDBNull(r.GetOrdinal("nivel")) ? 0 : r.GetInt32(r.GetOrdinal("nivel")); }
                    catch { nivel = r.IsDBNull(r.GetOrdinal("nivel")) ? 0 : r.GetInt16(r.GetOrdinal("nivel")); }
                }
            }

            if (userId is null)
                return Unauthorized(new { message = "Usuário não encontrado." });

            // Resolve tabela e coluna de usuário
            static async Task<bool> TableExists(NpgsqlConnection c, string s, string t)
            {
                const string sql = "SELECT 1 FROM information_schema.tables WHERE table_schema = @s AND table_name = @t LIMIT 1";
                await using var cmd = new NpgsqlCommand(sql, c);
                cmd.Parameters.AddWithValue("@s", s);
                cmd.Parameters.AddWithValue("@t", t);
                var res = await cmd.ExecuteScalarAsync();
                return res != null;
            }

            static async Task<string?> ResolveTable(NpgsqlConnection c)
            {
                var envTable = Environment.GetEnvironmentVariable("CHAMADOS_TABLE");
                if (!string.IsNullOrWhiteSpace(envTable) && await TableExists(c, "public", envTable)) return envTable;
                var candidates = new[] { "chamado", "tarefa", "chamados" };
                foreach (var t in candidates) if (await TableExists(c, "public", t)) return t;
                return null;
            }

            static async Task<string?> ResolveUserIdColumn(NpgsqlConnection c, string table)
            {
                async Task<bool> Col(string col)
                {
                    const string csql = "SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @t AND column_name = @c LIMIT 1";
                    await using var cmd = new NpgsqlCommand(csql, c);
                    cmd.Parameters.AddWithValue("@t", table);
                    cmd.Parameters.AddWithValue("@c", col);
                    var r = await cmd.ExecuteScalarAsync();
                    return r != null;
                }
                var cols = new[] { "id_usuario_criador", "id_usuario", "usuario_id", "id_criador" };
                foreach (var col in cols) if (await Col(col)) return col;
                return null;
            }

            var tableName = await ResolveTable(conn);
            if (tableName is null) return BadRequest(new { message = "Tabela de chamados não encontrada." });
            var userIdCol = await ResolveUserIdColumn(conn, tableName);
            if (string.IsNullOrEmpty(userIdCol)) return BadRequest(new { message = "Coluna de usuário criador não encontrada." });

            // Prioridade: se não enviada e motivo não for 'Outros', aplicar mapeamento básico
            int prioridade;
            if (req.Prioridade.HasValue)
            {
                prioridade = req.Prioridade.Value;
            }
            else
            {
                var m = (req.Motivo ?? string.Empty).Trim().ToLowerInvariant();
                prioridade = m switch
                {
                    "problemas com o mouse" => 3,
                    "problemas com som" => 4,
                    "problema com vídeo" => 2,
                    "problemas com a internet" => 1,
                    _ => 4,
                };
            }

            var sqlIns = $@"INSERT INTO public.""{tableName}"" (titulo, motivo, descricao, prioridade, data_criacao, resolvido, {userIdCol})
                           VALUES (@titulo, @motivo, @descricao, @prioridade, @data_criacao, @resolvido, @user)
                           RETURNING id";

            await using var cmdIns = new NpgsqlCommand(sqlIns, conn);
            cmdIns.Parameters.AddWithValue("@titulo", req.Titulo.Trim());
            cmdIns.Parameters.AddWithValue("@motivo", req.Motivo.Trim());
            if (string.IsNullOrWhiteSpace(req.Descricao))
                cmdIns.Parameters.AddWithValue("@descricao", DBNull.Value);
            else
                cmdIns.Parameters.AddWithValue("@descricao", req.Descricao!.Trim());
            cmdIns.Parameters.AddWithValue("@prioridade", prioridade);
            cmdIns.Parameters.AddWithValue("@data_criacao", DateTime.UtcNow);
            cmdIns.Parameters.AddWithValue("@resolvido", false);
            cmdIns.Parameters.AddWithValue("@user", userId!.Value);

            var newId = await cmdIns.ExecuteScalarAsync();
            return Ok(new { id = Convert.ToInt32(newId), message = "Chamado criado" });
        }

        public class ChamadoQuery
        {
            public int? UserId { get; set; }
        }

        [HttpPost("by-user")]
        public async Task<IActionResult> ListarPorUsuario([FromBody] ChamadoQuery query)
        {
            var email =
                User?.FindFirst("email")?.Value ??
                User?.FindFirst(ClaimTypes.Email)?.Value ??
                User?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value ??
                User?.Identity?.Name;

            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString))
                return Unauthorized(new { message = "Banco não configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            int? userId = null;
            string cargo = "";
            int nivel = 0;
            await using (var cmdUser = new NpgsqlCommand("SELECT id, cargo, nivel FROM public.\"user\" WHERE email = @e LIMIT 1", conn))
            {
                cmdUser.Parameters.AddWithValue("@e", email ?? (object)DBNull.Value);
                await using var r = await cmdUser.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    userId = r.IsDBNull(r.GetOrdinal("id")) ? null : r.GetInt32(r.GetOrdinal("id"));
                    cargo = r.IsDBNull(r.GetOrdinal("cargo")) ? "" : r.GetString(r.GetOrdinal("cargo"));
                    try { nivel = r.IsDBNull(r.GetOrdinal("nivel")) ? 0 : r.GetInt32(r.GetOrdinal("nivel")); }
                    catch { nivel = r.IsDBNull(r.GetOrdinal("nivel")) ? 0 : r.GetInt16(r.GetOrdinal("nivel")); }
                }
            }

            if (userId is null)
                return Unauthorized(new { message = "Usuário não encontrado." });

            var requestedUserId = query?.UserId ?? userId;
            var isUsuario = string.Equals(cargo, "usuario", StringComparison.OrdinalIgnoreCase);
            var isSuporte = string.Equals(cargo, "suporte", StringComparison.OrdinalIgnoreCase);

            if (isUsuario && requestedUserId != userId)
                return Forbid();

            var lista = new List<ChamadoDto>();

            static async Task<bool> TableExists(NpgsqlConnection conn, string schema, string table)
            {
                const string sql = "SELECT 1 FROM information_schema.tables WHERE table_schema = @s AND table_name = @t LIMIT 1";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@s", schema);
                cmd.Parameters.AddWithValue("@t", table);
                var res = await cmd.ExecuteScalarAsync();
                return res != null;
            }

            static async Task<string?> ResolveTable(NpgsqlConnection conn)
            {
                var envTable = Environment.GetEnvironmentVariable("CHAMADOS_TABLE");
                if (!string.IsNullOrWhiteSpace(envTable))
                {
                    if (await TableExists(conn, "public", envTable)) return envTable;
                }
                var candidates = new[] { "chamado", "tarefa", "chamados" };
                foreach (var t in candidates)
                {
                    if (await TableExists(conn, "public", t)) return t;
                }
                return null;
            }

            static async Task<string?> ResolveUserIdColumn(NpgsqlConnection conn, string table)
            {
                async Task<bool> Col(string c)
                {
                    const string csql = "SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @t AND column_name = @c LIMIT 1";
                    await using var cmd = new NpgsqlCommand(csql, conn);
                    cmd.Parameters.AddWithValue("@t", table);
                    cmd.Parameters.AddWithValue("@c", c);
                    var r = await cmd.ExecuteScalarAsync();
                    return r != null;
                }
                var cols = new[] { "id_usuario_criador", "id_usuario", "usuario_id", "id_criador" };
                foreach (var c in cols)
                    if (await Col(c)) return c;
                return null;
            }

            async Task FetchFromTable(string tableName)
            {
                var userIdCol = await ResolveUserIdColumn(conn, tableName);
                if (string.IsNullOrEmpty(userIdCol)) return;

                var sb = new System.Text.StringBuilder();
                sb.Append($"SELECT c.id, c.titulo, c.motivo, c.descricao, c.prioridade, c.resolvido, c.data_criacao, c.{userIdCol} AS usuario_criador, u.nome AS nome_criador FROM public.\"{tableName}\" c LEFT JOIN public.\"user\" u ON u.id = c.{userIdCol} ");
                var where = new List<string>();
                if (requestedUserId.HasValue)
                    where.Add($"c.{userIdCol} = @id_usuario");
                if (isSuporte && nivel > 0)
                    where.Add("c.prioridade >= @nivel");
                if (where.Count > 0)
                {
                    sb.Append(" WHERE ");
                    sb.Append(string.Join(" AND ", where));
                }
                sb.Append(" ORDER BY c.id DESC LIMIT 200");

                await using var cmd = new NpgsqlCommand(sb.ToString(), conn);
                if (requestedUserId.HasValue)
                    cmd.Parameters.AddWithValue("@id_usuario", requestedUserId.Value);
                if (isSuporte && nivel > 0)
                    cmd.Parameters.AddWithValue("@nivel", nivel);

                try
                {
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var dto = new ChamadoDto
                        {
                            Id = reader.GetInt32(reader.GetOrdinal("id")),
                            Titulo = reader.IsDBNull(reader.GetOrdinal("titulo")) ? string.Empty : reader.GetString(reader.GetOrdinal("titulo")),
                            Motivo = reader.IsDBNull(reader.GetOrdinal("motivo")) ? string.Empty : reader.GetString(reader.GetOrdinal("motivo")),
                            Descricao = reader.IsDBNull(reader.GetOrdinal("descricao")) ? null : reader.GetString(reader.GetOrdinal("descricao")),
                            Prioridade = reader.IsDBNull(reader.GetOrdinal("prioridade")) ? 0 : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("prioridade"))),
                            Resolvido = !reader.IsDBNull(reader.GetOrdinal("resolvido")) && Convert.ToBoolean(reader.GetValue(reader.GetOrdinal("resolvido"))),
                            DataCriacao = reader.IsDBNull(reader.GetOrdinal("data_criacao")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("data_criacao")),
                            UsuarioCriadorId = reader.IsDBNull(reader.GetOrdinal("usuario_criador")) ? (int?)null : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("usuario_criador"))),
                            NomeCriador = reader.IsDBNull(reader.GetOrdinal("nome_criador")) ? null : reader.GetString(reader.GetOrdinal("nome_criador")),
                        };
                        lista.Add(dto);
                    }
                }
                catch (PostgresException ex) when (ex.SqlState == "42P01")
                {
                    // ignore table not exists
                }
            }

            var table = await ResolveTable(conn);
            if (table != null) await FetchFromTable(table);

            return Ok(lista);
        }

        [HttpPut("{id:int}/close")]
        public async Task<IActionResult> Close([FromRoute] int id)
        {
            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString))
                return Unauthorized(new { message = "Banco nao configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();
            var table = await __ResolveChamadosTable(conn);
            if (string.IsNullOrEmpty(table)) return BadRequest(new { message = "Tabela de chamados nao encontrada." });
            if (!await __ColumnExists(conn, table, "resolvido")) return BadRequest(new { message = "Coluna 'resolvido' nao encontrada." });

            var sql = $"UPDATE public.\"{table}\" SET resolvido=true WHERE id=@id";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return Ok(new { updated = rows });
        }

        [HttpPut("{id:int}/reopen")]
        public async Task<IActionResult> Reopen([FromRoute] int id)
        {
            var connString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(connString))
                return Unauthorized(new { message = "Banco nao configurado." });

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();
            var table = await __ResolveChamadosTable(conn);
            if (string.IsNullOrEmpty(table)) return BadRequest(new { message = "Tabela de chamados nao encontrada." });
            if (!await __ColumnExists(conn, table, "resolvido")) return BadRequest(new { message = "Coluna 'resolvido' nao encontrada." });

            var sql = $"UPDATE public.\"{table}\" SET resolvido=false WHERE id=@id";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return Ok(new { updated = rows });
        }
    }
}
