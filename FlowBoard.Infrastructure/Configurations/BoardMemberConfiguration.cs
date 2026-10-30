using FlowBoard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowBoard.Infrastructure.Configurations;

public class BoardMemberConfiguration : IEntityTypeConfiguration<BoardMember>
{
    public void Configure(EntityTypeBuilder<BoardMember> builder)
    {
        builder.ToTable("BoardMembers");

        // Composite primary key
        builder.HasKey(x => new
        {
            x.BoardId,
            x.UserId
        });

        builder.Property(x => x.JoinedAt)
            .IsRequired();

        // BoardMember -> Board
        builder.HasOne(x => x.Board)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        // BoardMember -> User
        builder.HasOne(x => x.User)
            .WithMany(x => x.BoardMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Useful for finding all boards a user belongs to
        builder.HasIndex(x => x.UserId);
    }
}