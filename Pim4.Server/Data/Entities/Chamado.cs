using System.ComponentModel.DataAnnotations;

namespace Pim4.Server.Data.Entities
{
    public class Chamado
    {
        [Key]
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Motivo { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public int Prioridade { get; set; }
        public bool Resolvido { get; set; }
        public DateTime? DataCriacao { get; set; }
        public int? UsuarioCriadorId { get; set; }

        public User? UsuarioCriador { get; set; }
        public ICollection<ChatMessage> Mensagens { get; set; } = new List<ChatMessage>();
    }
}
