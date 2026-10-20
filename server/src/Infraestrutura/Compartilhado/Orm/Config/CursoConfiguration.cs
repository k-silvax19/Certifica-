using GeradorCertificadosOnline.Dominio.Modulos.Cursos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GeradorCertificadosOnline.Infraestrutura.Compartilhado.Orm.Config;

public class CursoConfiguration : IEntityTypeConfiguration<Curso>
{
    public void Configure(EntityTypeBuilder<Curso> builder)
    {
        // Cursos anteriores à vinculação permanecem sem dono e não são expostos nas consultas.
        builder.HasOne<IdentityUser<Guid>>()
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.Nome)
            .HasMaxLength(Curso.TamanhoMaximoNome);
    }
}
