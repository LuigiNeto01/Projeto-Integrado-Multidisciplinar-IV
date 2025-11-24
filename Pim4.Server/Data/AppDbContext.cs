using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pim4.Server.Data.Entities;

namespace Pim4.Server.Data
{
    public class AppDbContext : DbContext
    {
        private readonly string _chamadosTable;
        private readonly string _chamadosUserColumn;

        public DbSet<User> Users => Set<User>();
        public DbSet<Chamado> Chamados => Set<Chamado>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

        public AppDbContext(DbContextOptions<AppDbContext> options, IConfiguration configuration) : base(options)
        {
            _chamadosTable = configuration["CHAMADOS_TABLE"]
                ?? Environment.GetEnvironmentVariable("CHAMADOS_TABLE")
                ?? "chamado";
            _chamadosUserColumn = configuration["CHAMADOS_USER_COLUMN"]
                ?? Environment.GetEnvironmentVariable("CHAMADOS_USER_COLUMN")
                ?? "id_usuario_criador";
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("user", "public");
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Id).HasColumnName("id");
                entity.Property(u => u.Cpf).HasColumnName("cpf");
                entity.Property(u => u.Nome).HasColumnName("nome");
                entity.Property(u => u.Email).HasColumnName("email");
                entity.Property(u => u.Senha).HasColumnName("senha");
                entity.Property(u => u.Cargo).HasColumnName("cargo");
                entity.Property(u => u.Nivel).HasColumnName("nivel");
            });

            modelBuilder.Entity<Chamado>(entity =>
            {
                entity.ToTable(_chamadosTable, "public");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Id).HasColumnName("id");
                entity.Property(c => c.Titulo).HasColumnName("titulo");
                entity.Property(c => c.Motivo).HasColumnName("motivo");
                entity.Property(c => c.Descricao).HasColumnName("descricao");
                entity.Property(c => c.Prioridade).HasColumnName("prioridade");
                entity.Property(c => c.Resolvido).HasColumnName("resolvido");
                entity.Property(c => c.DataCriacao).HasColumnName("data_criacao");
                entity.Property(c => c.UsuarioCriadorId).HasColumnName(_chamadosUserColumn);

                entity.HasOne(c => c.UsuarioCriador)
                      .WithMany(u => u.ChamadosCriados)
                      .HasForeignKey(c => c.UsuarioCriadorId);
            });

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.ToTable("mensagens_chat", "public");
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Id).HasColumnName("id");
                entity.Property(m => m.IdChamado).HasColumnName("id_chamado");
                entity.Property(m => m.IdUsuario).HasColumnName("id_usuario");
                entity.Property(m => m.Mensagem).HasColumnName("mensagem");
                entity.Property(m => m.DataEnvio).HasColumnName("data_envio");

                entity.HasOne(m => m.Usuario)
                      .WithMany(u => u.Mensagens)
                      .HasForeignKey(m => m.IdUsuario);

                entity.HasOne(m => m.Chamado)
                      .WithMany(c => c.Mensagens)
                      .HasForeignKey(m => m.IdChamado);
            });
        }
    }
}
