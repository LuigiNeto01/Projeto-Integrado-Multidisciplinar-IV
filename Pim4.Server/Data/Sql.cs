// Sql.cs
// Eu explico: centralizo aqui as strings SQL usadas pela API.
// - Evita SQL inline espalhado por controladores.
// - Facilita manutencao e auditoria.
namespace Pim4.Server.Data
{
    public static class Sql
    {
        public static class Auth
        {
            public const string SelectUserByEmail = "SELECT id, cpf, nome, email, senha, cargo, nivel FROM public.\"user\" WHERE email = @e LIMIT 1";
            public const string ExistsEmail = "SELECT 1 FROM public.\"user\" WHERE email=@e LIMIT 1";
            public const string InsertUser = "INSERT INTO public.\"user\" (cpf, nome, email, senha, cargo, nivel) VALUES (@cpf, @nome, @email, @senha, @cargo, @nivel) RETURNING id";
        }

        public static class Users
        {
            public const string GetCargoByEmail = "SELECT cargo FROM public.\"user\" WHERE email = @e LIMIT 1";
            public const string ListAll = "SELECT id, cpf, nome, email, cargo, nivel FROM public.\"user\" ORDER BY id DESC";
            public const string Insert = "INSERT INTO public.\"user\" (cpf, nome, email, senha, cargo, nivel) VALUES (@cpf, @nome, @email, @senha, @cargo, @nivel) RETURNING id";
            public const string UpdateTemplate = "UPDATE public.\"user\" SET {0} WHERE id=@id";
            public const string Delete = "DELETE FROM public.\"user\" WHERE id=@id";
            public const string GetIdByEmail = "SELECT id FROM public.\"user\" WHERE email=@e LIMIT 1";
            public const string SelectByIdBasic = "SELECT id, cpf, nome, email, cargo, nivel FROM public.\"user\" WHERE id=@id";
        }

        public static class Chat
        {
            public const string ListMessages = @"SELECT mc.id, mc.id_chamado, mc.id_usuario, u.nome, mc.mensagem, mc.data_envio
                                 FROM public.mensagens_chat mc
                                 LEFT JOIN public.""user"" u ON u.id = mc.id_usuario
                                 WHERE mc.id_chamado = @id
                                 ORDER BY mc.data_envio ASC, mc.id ASC";

            public const string InsertMessage = @"INSERT INTO public.mensagens_chat (id_chamado, id_usuario, mensagem, data_envio)
                                 VALUES (@ch, @u, @m, @d) RETURNING id";
        }

        public static class Chamados
        {
            // Templates para montagem dinâmica por tabela/colunas
            public const string InsertTemplate = "INSERT INTO public.\"{0}\" (titulo, motivo, descricao, prioridade, data_criacao, resolvido, {1}) VALUES (@titulo, @motivo, @descricao, @prioridade, @data_criacao, @resolvido, @user) RETURNING id";
            public const string SelectBaseTemplate = "SELECT c.id, c.titulo, c.motivo, c.descricao, c.prioridade, c.resolvido, c.data_criacao, c.{0} AS usuario_criador, u.nome AS nome_criador FROM public.\"{1}\" c LEFT JOIN public.\"user\" u ON u.id = c.{0}";
            public const string CloseTemplate = "UPDATE public.\"{0}\" SET resolvido=true WHERE id=@id";
            public const string ReopenTemplate = "UPDATE public.\"{0}\" SET resolvido=false WHERE id=@id";
        }
    }
}
