using System.Net.Http.Json;

// GeminiService
// Eu explico: cliente HTTP minimalista para a API do Google Generative Language (Gemini).
// - Le credenciais e modelo de variaveis de ambiente/Configuration.
// - Envia prompt como 'contents.parts[].text' e retorna primeiro trecho de texto.
namespace Pim4.Server.Services
{
    public class GeminiService
    {
        private readonly HttpClient _http;
        private readonly string? _apiKey;
        private readonly string _model;
        private readonly string _baseUrl;

        public GeminiService(IConfiguration config, HttpClient http)
        {
            _http = http;
            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                ?? config["GEMINI_API_KEY"];
            _model = Environment.GetEnvironmentVariable("GEMINI_MODEL")
                ?? config["GEMINI_MODEL"]
                ?? "gemini-1.5-flash";
            _baseUrl = Environment.GetEnvironmentVariable("GEMINI_API_BASE_URL")
                ?? config["GEMINI_API_BASE_URL"]
                ?? "https://generativelanguage.googleapis.com";
        }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        // Gera texto a partir de um prompt.
        public async Task<string> GenerateAsync(string prompt, CancellationToken ct = default)
        {
            if (!IsConfigured) throw new InvalidOperationException("GEMINI_API_KEY não configurada.");
            var url = $"{_baseUrl.TrimEnd('/')}/v1beta/models/{_model}:generateContent?key={_apiKey}";

            var payload = new
            {
                contents = new[]
                {
                    new {
                        parts = new[] { new { text = prompt } }
                    }
                }
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(payload)
            };
            using var res = await _http.SendAsync(req, ct);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: ct);
            var text = json?.candidates?.FirstOrDefault()?.content?.parts?.FirstOrDefault()?.text;
            return text ?? string.Empty;
        }

        private sealed class GeminiResponse
        {
            public List<Candidate>? candidates { get; set; }
        }
        private sealed class Candidate
        {
            public Content? content { get; set; }
        }
        private sealed class Content
        {
            public List<Part>? parts { get; set; }
        }
        private sealed class Part
        {
            public string? text { get; set; }
        }
    }
}
