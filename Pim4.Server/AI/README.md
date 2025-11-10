Este diretório armazena prompts de IA usados pela aplicação.

Arquivos:
- Prompts/ChamadoConfirmacao.txt — texto base usado para confirmar a abertura de um chamado. Suporta placeholders: {{titulo}}, {{motivo}}, {{descricao}}, {{prioridade}}, {{nome}}, {{email}}.

Integração:
- Endpoint: POST /ai/chamado/opiniao
  - Body: { "titulo", "motivo", "descricao", "prioridade", "nome", "email" }
  - Resposta: { "text": "..." }

Futuro:
- Para integrar ao Gemini, use o conteúdo deste arquivo como prompt e faça as substituições dos placeholders antes do envio.

