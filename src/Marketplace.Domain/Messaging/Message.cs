namespace Marketplace.Domain.Messaging;

public sealed class Message
{
    private Message() { }
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static Message Create(Guid conversationId, Guid senderId, string body, DateTimeOffset now)
    {
        string cleanBody = body?.Trim() ?? string.Empty;
        if (cleanBody.Length == 0) throw new MessagingRuleException("Mesajul nu poate fi gol.");
        if (cleanBody.Length > 2_000) throw new MessagingRuleException("Mesajul poate avea cel mult 2.000 de caractere.");
        return new Message { Id = Guid.NewGuid(), ConversationId = conversationId, SenderId = senderId, Body = cleanBody, CreatedAt = now };
    }
}
