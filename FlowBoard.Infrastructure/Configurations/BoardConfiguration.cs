using FlowBoard.Domain.Entities;
using FlowBoard.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowBoard.Infrastructure.Configurations;

public class BoardConfiguration
    : BaseEntityConfiguration<Board>
{
    public override void Configure(EntityTypeBuilder<Board> builder)
    {
        base.Configure(builder);

        builder.ToTable("Boards");

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.Purpose)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(BoardPurpose.Project)
            .HasSentinel(BoardPurpose.Project);

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.Boards)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Owner)
            .WithMany(x => x.OwnedBoards)
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Boards)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OwnerId);

        builder.HasIndex(x => x.ProjectId);
    }
}