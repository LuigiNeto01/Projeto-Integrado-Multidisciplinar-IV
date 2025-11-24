using System.ComponentModel.DataAnnotations;

namespace Pim4.Server.Data.Entities
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }
        public int IdChamado { get; set; }
        public int IdUsuario { get; set; }
        public string Mensagem { get; set; } = string.Empty;
        public DateTime DataEnvio { get; set; }

        public Chamado? Chamado { get; set; }
        public User? Usuario { get; set; }
    }
}
