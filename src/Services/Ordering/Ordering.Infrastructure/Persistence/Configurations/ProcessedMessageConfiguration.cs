using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ordering.Domain.Entities;

namespace Ordering.Infrastructure.Persistence.Configurations;

internal sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages");

        // The publisher's message id as the primary key. This one line is what turns a
        // redelivery into a database conflict instead of a second order, and it works
        // across however many instances of Ordering are running — which an in-memory
        // "seen ids" cache would not.
        builder.HasKey(processed => processed.MessageId);
        builder.Property(processed => processed.MessageId).ValueGeneratedNever();

        builder.Property(processed => processed.MessageType).IsRequired().HasMaxLength(200);

        // These rows only need to outlive the window in which a redelivery is plausible.
        // Service Bus gives up after its own retry and TTL limits, so a periodic sweep on
        // this index is the cleanup — not yet written, and noted as a known gap.
        builder.HasIndex(processed => processed.HandledAt);
    }
}
