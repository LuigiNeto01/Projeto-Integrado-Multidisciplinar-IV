Pim4 — API .NET 8 + React (Vite)

Visão Geral
- Backend: ASP.NET Core (.NET 8) com autenticação JWT, CORS p/ Vite, Swagger (dev), centralização de SQLs e endpoints para usuários, chamados, chat e IA.
- Frontend: React + Vite + Tailwind (básico), com telas de login/registro, dashboard, gestão de chamados (com chat), e gestão de usuários (somente admin).
- Banco: PostgreSQL (docker-compose opcional). Strings em `.env`.

Arquitetura
- `Pim4.Server/` (API)
  - `Controllers/`
    - `AuthController`: login (`/auth/login`) e registro (`/auth/register`, cargo padrão `usuario`).
    - `UsersController`: CRUD protegido por papel `admin` (com verificação por claim/DB) e `PUT /users/me` (perfil próprio).
    - `ChamadosController`: listar/criar chamados; filtros por usuário/nível; `PUT /chamados/{id}/close|reopen`.
    - `ChatController`: mensagens do chamado (`GET/POST /chat/{chamadoId}/messages`).
    - `AiController`: gera confirmação/FAQ curta para abertura de chamado (`POST /ai/chamado/opiniao`). Usa Gemini se configurado.
  - `Data/Sql.cs`: todas as strings SQL centralizadas (Auth/Users/Chat/Chamados templates).
  - `AI/Prompts/ChamadoConfirmacao.txt`: prompt com placeholders ({{titulo}}, {{motivo}}, {{descricao}}, {{prioridade}}, {{nome}}, {{email}}), máx. 500 chars, 1–2 frases.
  - `Services/GeminiService.cs`: cliente HTTP do Gemini (Generative Language API).
  - `Program.cs`: registra serviços, CORS, autenticação JWT.
- `pim4.client/` (SPA)
  - `src/components/`
    - `LoginForm` (+ `RegisterModal`) — login e registro.
    - `Sidebar` — navegação.
    - `ChamadosGrid` — cards com indicadores, filtros, divisão abertos/fechados e botão para chat.
    - `ChatModal` — interface de chat, fechar/reabrir chamado.
    - `AbrirChamadoModal` — abertura de chamado; integra IA para confirmação antes do envio.
    - `ProfileModal` — edição de perfil (nome/email/senha).
  - `src/pages/`
    - `Home` — integra Sidebar e páginas.
    - `Dashboard` — KPIs, gráfico de prioridades (CSS), alertas, export CSV, bloco de usuários (admin).
  - `src/services/` — `api/http`, `auth/api`, `users/api`, `chamados/api`, `chat/api`, `ai/api`.

Pré‑requisitos
- .NET 8 SDK
- Node 18+ (ou 20+)
- Docker (opcional para Postgres)

Configuração
1) Backend: copie `Pim4.Server/.env.example` para `Pim4.Server/.env` e ajuste:
   - `POSTGRES_CONNECTION` — ex.: `Host=localhost;Port=5432;Username=pim4;Password=pim4;Database=pim4;`
   - `DEBUG_LOGIN` — true para usar `DEBUG_EMAIL/DEBUG_PASSWORD` no dev (opcional)
   - `JWT_*` — segredo/issuer/audience (opcional no dev)
   - `GEMINI_API_KEY` — para ativar integração com Gemini no endpoint de IA (opcional)

2) Banco (opcional via Docker):
   - `docker compose up -d postgres`
   - Ajuste a string de conexão do passo 1 para o usuário/senha/db do compose (`pim4`/`pim4`/`pim4`).

Rodando o projeto
- API: na raiz do repo
  - `dotnet restore`
  - `dotnet run --project Pim4.Server`
  - Swagger (dev): `http://localhost:5132/swagger`

- Client: em `pim4.client`
  - `npm install`
  - `npm run dev` (Vite em `http://localhost:5173`)
  - O backend já possui CORS liberado para o Vite no dev.

Fluxos principais
- Autenticação
  - Registro: botão “Registrar” na tela de login → `POST /auth/register` → auto‑login (cargo `usuario`).
  - Login: `POST /auth/login` (JWT com claims: email, role, nameid).

- Usuários (admin)
  - `GET/POST/PUT/DELETE /users` (somente `admin`).
  - UI “Gerir usuários” visível apenas para `admin`.

- Chamados
  - `GET /chamados` (admin/suporte veem mais; usuário vê os seus).
  - `POST /chamados` cria chamado (prioridade mapeada por motivo ou definida quando “Outros”).
  - `PUT /chamados/{id}/close` / `reopen` — fecha/reabre chamado.
  - UI mostra indicadores, filtros e separação entre abertos/fechados.

- Chat
  - `GET/POST /chat/{chamadoId}/messages` — mensagens do chamado.
  - Modal permite enviar mensagens (bloqueado se fechado) e alternar status (exceto cargo `usuario`).

- IA (confirmação de abertura)
  - `POST /ai/chamado/opiniao` — gera mensagem curta (até 500 chars) com confirmação + FAQ imediata a partir do prompt.
  - Frontend (AbrirChamadoModal) só permite “Confirmar envio” se a mensagem for retornada com sucesso.

Variáveis de Ambiente (Server)
- Veja `Pim4.Server/.env.example`. Principais:
  - `POSTGRES_CONNECTION` — conexão ao PostgreSQL
  - `DEBUG_LOGIN`, `DEBUG_EMAIL`, `DEBUG_PASSWORD`
  - `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`
  - `DISABLE_HTTPS_REDIRECT`
  - `GEMINI_API_KEY`, `GEMINI_MODEL`, `GEMINI_API_BASE_URL`

Boas práticas / Notas
- .gitignore na raiz ignora artefatos (`bin/`, `obj/`, `node_modules/`, `dist/`) e `.env`, mantendo `.env.example` sob versionamento.
- SQLs centralizados em `Data/Sql.cs` facilitam manutenção e padronização.
- Após alterar email do usuário (perfil), a sessão é renovada com novo token.

Licença
- Sem licença explícita. Ajuste conforme a necessidade do seu projeto.

