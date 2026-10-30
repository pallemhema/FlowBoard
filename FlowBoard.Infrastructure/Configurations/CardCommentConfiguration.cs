using FlowBoard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowBoard.Infrastructure.Configurations;

public class CardCommentConfiguration
    : BaseEntityConfiguration<CardComment>
{
    public override void Configure(EntityTypeBuilder<CardComment> builder)
    {
        base.Configure(builder);
        
        builder.ToTable("CardComments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content)
            .IsRequired()
            .HasMaxLength(2000);

        // Comment -> Card
        builder.HasOne(x => x.Card)
            .WithMany(x => x.Comments)
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);

        // Comment -> User
        builder.HasOne(x => x.User)
            .WithMany(x => x.Comments)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Useful when loading comments for a card
        builder.HasIndex(x => x.CardId);

        // Useful when querying comments by user
        builder.HasIndex(x => x.UserId);
    }
}