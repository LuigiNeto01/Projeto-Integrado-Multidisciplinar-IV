using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pim4.Server.Services;

// AiController
// Eu explico: gera texto curto de confirmacao/FAQ para abertura de chamado.
// - POST /ai/chamado/opiniao: carrega prompt do arquivo, substitui placeholders
//   e opcionalmente envia ao Gemini. Se falhar ou sem chave, usa fallback.

namespace Pim4.Server.Controllers
{
    [ApiController]
    [Route("ai")]
    public class AiController : ControllerBase
    {
        private readonly GeminiService _gemini;

        public AiController(GeminiService gemini)
        {
            _gemini = gemini;
        }
        public class ChamadoInfo
        {
            public string? Titulo { get; set; }
            public string? Motivo { get; set; }
            public string? Descricao { get; set; }
            public int? Prioridade { get; set; }
            public string? Nome { get; set; }
            public string? Email { get; set; }
        }

        private static string LoadPrompt()
        {
            var baseDir = AppContext.BaseDirectory;
            var path = Path.Combine(baseDir, "AI", "Prompts", "ChamadoConfirmacao.txt");
            if (!System.IO.File.Exists(path)) return "Confirmação de abertura de chamado.";
            return System.IO.File.ReadAllText(path);
        }

        [HttpPost("chamado/opiniao")]
        [AllowAnonymous]
        public async Task<IActionResult> ChamadoOpiniao([FromBody] ChamadoInfo body)
        {
            var tpl = LoadPrompt();
            string result = tpl
                .Replace("{{titulo}}", body?.Titulo ?? string.Empty)
                .Replace("{{motivo}}", body?.Motivo ?? string.Empty)
                .Replace("{{descricao}}", body?.Descricao ?? string.Empty)
                .Replace("{{prioridade}}", body?.Prioridade?.ToString() ?? string.Empty)
                .Replace("{{nome}}", body?.Nome ?? string.Empty)
                .Replace("{{email}}", body?.Email ?? string.Empty);
            if (_gemini.IsConfigured)
            {
                try
                {
                    var aiText = await _gemini.GenerateAsync(result);
                    return Ok(new { text = Truncate(aiText, 500), model = "gemini" });
                }
                catch
                {
                    // Fallback para o texto pronto
                }
            }
            // Fallback: gera confirmação curta + dica imediata
            var fb = BuildFallback(body);
            return Ok(new { text = fb, model = "fallback" });
        }

        private static string Truncate(string? s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static string BuildFallback(ChamadoInfo? b)
        {
            string nome = b?.Nome ?? "";
            string titulo = b?.Titulo ?? "";
            string motivo = (b?.Motivo ?? "").ToLowerInvariant();
            string email = b?.Email ?? "";
            string sug = motivo.Contains("internet") ? "Reinicie o modem e verifique cabos."
                     : motivo.Contains("som") || motivo.Contains("áudio") || motivo.Contains("audio") ? "Verifique volume e driver de áudio."
                     : motivo.Contains("vídeo") || motivo.Contains("video") ? "Reinicie o app e atualize driver de vídeo."
                     : motivo.Contains("mouse") ? "Troque a porta USB e limpe o sensor."
                     : motivo.Contains("senha") ? "Tente redefinir a senha no portal."
                     : "Reinicie o equipamento e verifique conexões.";
            var prioridade = b?.Prioridade != null ? $", prioridade {b.Prioridade}" : string.Empty;
            var text = $"Olá {nome}, seu chamado foi registrado: {titulo} (motivo: {b?.Motivo}{prioridade}). Sugestão: {sug} Entraremos em contato em {email}.";
            return Truncate(text, 500);
        }
    }
}
