using Marketplace.Domain.Messaging;

namespace Marketplace.Domain.Tests;

public sealed class MessagingTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void MessageIsTrimmedAndKeepsItsParticipants()
    {
        Guid conversationId = Guid.NewGuid();
        Guid senderId = Guid.NewGuid();
        Message message = Message.Create(conversationId, senderId, "  Bună ziua!  ", Now);
        Assert.Equal(conversationId, message.ConversationId);
        Assert.Equal(senderId, message.SenderId);
        Assert.Equal("Bună ziua!", message.Body);
        Assert.Equal(Now, message.CreatedAt);
    }

    [Fact]
    public void EmptyAndOversizedMessagesAreRejected()
    {
        Assert.Throws<MessagingRuleException>(() => Message.Create(Guid.NewGuid(), Guid.NewGuid(), "   ", Now));
        Assert.Throws<MessagingRuleException>(() => Message.Create(Guid.NewGuid(), Guid.NewGuid(), new string('x', 2_001), Now));
    }

    [Fact]
    public void OnlyParticipantsCanMarkConversationRead()
    {
        Guid buyerId = Guid.NewGuid();
        Guid supplierId = Guid.NewGuid();
        Conversation conversation = Conversation.Create(Guid.NewGuid(), buyerId, supplierId, Now);
        conversation.MarkRead(buyerId, Now.AddMinutes(1));
        conversation.MarkRead(supplierId, Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(1), conversation.BuyerLastReadAt);
        Assert.Equal(Now.AddMinutes(2), conversation.SupplierLastReadAt);
        Assert.Throws<MessagingRuleException>(() => conversation.MarkRead(Guid.NewGuid(), Now.AddMinutes(3)));
    }
}
