using FlowBoard.Domain.Common;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
namespace FlowBoard.Infrastructure.Configurations;

public abstract class BaseEntityConfiguration<T>
    : IEntityTypeConfiguration<T>
    where T : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .IsRequired(false);

        builder.Property(x => x.ModifiedAt)
            .IsRequired(false);

        builder.Property(x => x.ModifiedByUserId)
            .IsRequired(false);
    }
}