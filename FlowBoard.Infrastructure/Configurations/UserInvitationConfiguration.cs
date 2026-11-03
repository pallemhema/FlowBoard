using FlowBoard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FlowBoard.Infrastructure.Configurations;

public class UserInvitationConfiguration
    : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("UserInvitations");

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.TokenHash)
            .IsRequired()
            .HasMaxLength(64);

        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique();

        builder.HasIndex(invitation => invitation.UserId);

        builder.Property(invitation => invitation.ExpiresAt)
            .IsRequired();

        builder.HasOne(invitation => invitation.User)
            .WithMany(user => user.Invitations)
            .HasForeignKey(invitation => invitation.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
