using System.ComponentModel.DataAnnotations;

namespace Pim4.Server.Data.Entities
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        public string? Cpf { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Senha { get; set; }
        public string Cargo { get; set; } = string.Empty;
        public int? Nivel { get; set; }

        public ICollection<Chamado> ChamadosCriados { get; set; } = new List<Chamado>();
        public ICollection<ChatMessage> Mensagens { get; set; } = new List<ChatMessage>();
    }
}
