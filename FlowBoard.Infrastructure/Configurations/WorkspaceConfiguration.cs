using FlowBoard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowBoard.Infrastructure.Data.Configurations;

public class WorkspaceConfiguration
    : IEntityTypeConfiguration<Workspace>
{
    public void Configure(
        EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        // Workspace -> Owner
        builder.HasOne(x => x.Owner)
            .WithMany(x => x.OwnedWorkspaces)
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Workspace -> Boards
        builder.HasMany(x => x.Boards)
            .WithOne(x => x.Workspace)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}