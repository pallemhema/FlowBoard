using FlowBoard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowBoard.Infrastructure.Configurations;

public class TeamConfiguration : BaseEntityConfiguration<Team>
{
    public override void Configure(EntityTypeBuilder<Team> builder)
    {
        base.Configure(builder);

        builder.ToTable("Teams");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasIndex(x => x.Name);
    }
}
