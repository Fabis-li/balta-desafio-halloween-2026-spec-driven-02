using LinkShortener.Api.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LinkShortener.Api.Data;

public class LinkConfiguration : IEntityTypeConfiguration<Link>
{
    public void Configure(EntityTypeBuilder<Link> builder)
    {
        builder.ToTable("Links");

        builder.HasKey(link => link.Id);

        builder.Property(link => link.Code)
            .IsRequired()
            .HasMaxLength(Link.CodeMaxLength);

        // D-04: unicidade garantida pelo banco, não só pela checagem no serviço
        builder.HasIndex(link => link.Code)
            .IsUnique();

        builder.Property(link => link.TargetUrl)
            .IsRequired()
            .HasMaxLength(Link.TargetUrlMaxLength);

        builder.Property(link => link.CreatedAtUtc)
            .IsRequired();

        builder.Property(link => link.ExpiresAtUtc);

        builder.Property(link => link.AccessCount)
            .IsRequired()
            .HasDefaultValue(0L);
    }
}
