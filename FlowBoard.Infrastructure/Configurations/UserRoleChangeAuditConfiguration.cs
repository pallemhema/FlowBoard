using FlowBoard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowBoard.Infrastructure.Configurations;

public class UserRoleChangeAuditConfiguration
    : IEntityTypeConfiguration<UserRoleChangeAudit>
{
    public void Configure(EntityTypeBuilder<UserRoleChangeAudit> builder)
    {
        builder.ToTable("UserRoleChangeAudits");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.PreviousRole)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(audit => audit.NewRole)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(audit => audit.ChangedAt)
            .IsRequired();

        builder.HasIndex(audit => new { audit.TargetUserId, audit.ChangedAt });

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(audit => audit.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(audit => audit.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
