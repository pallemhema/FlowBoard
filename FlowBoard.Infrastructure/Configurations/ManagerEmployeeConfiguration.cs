using FlowBoard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowBoard.Infrastructure.Configurations;

public class ManagerEmployeeConfiguration : IEntityTypeConfiguration<ManagerEmployee>
{
    public void Configure(EntityTypeBuilder<ManagerEmployee> builder)
    {
        builder.ToTable("ManagerEmployees");

        builder.HasKey(x => new { x.ManagerId, x.EmployeeId });

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasOne(x => x.Manager)
            .WithMany(x => x.ManagedEmployees)
            .HasForeignKey(x => x.ManagerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Employee)
            .WithMany(x => x.ManagerRelationships)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.EmployeeId);
    }
}
