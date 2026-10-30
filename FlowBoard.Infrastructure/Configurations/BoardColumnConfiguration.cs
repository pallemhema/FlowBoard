using FlowBoard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowBoard.Infrastructure.Configurations;

public class BoardColumnConfiguration
    : BaseEntityConfiguration<BoardColumn>
{
    public override void Configure(EntityTypeBuilder<BoardColumn> builder)
    {
        base.Configure(builder);

        builder.ToTable("BoardColumns");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Position)
            .IsRequired();

        builder.HasOne(x => x.Board)
            .WithMany(x => x.Columns)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new
        {
            x.BoardId,
            x.Position
        });
    }
}