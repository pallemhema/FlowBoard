using FlowBoard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowBoard.Infrastructure.Configurations;

public class CardConfiguration
    : BaseEntityConfiguration<Card>
{
    public override void Configure(EntityTypeBuilder<Card> builder)
    {
        base.Configure(builder);
        
        builder.ToTable("Cards");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Position)
            .IsRequired();

        // Card -> BoardColumn
        builder.HasOne(x => x.BoardColumn)
            .WithMany(x => x.Cards)
            .HasForeignKey(x => x.BoardColumnId)
            .OnDelete(DeleteBehavior.Cascade);

        // Card -> Assigned User
        builder.HasOne(x => x.AssignedUser)
            .WithMany(x => x.AssignedCards)
            .HasForeignKey(x => x.AssignedUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Card -> Comments
        builder.HasMany(x => x.Comments)
            .WithOne(x => x.Card)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Frequently used when loading cards in a column
        builder.HasIndex(x => new
        {
            x.BoardColumnId,
            x.Position
        });

        // Useful when finding cards assigned to a user
        builder.HasIndex(x => x.AssignedUserId);

        // Useful for filtering by due date
        builder.HasIndex(x => x.DueDate);
    }
}