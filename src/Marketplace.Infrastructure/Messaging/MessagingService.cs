using Marketplace.Application.Messaging;
using Marketplace.Domain.Messaging;
using Marketplace.Domain.Notifications;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Messaging;

public sealed class MessagingService(MarketplaceDbContext dbContext, TimeProvider timeProvider) : IMessagingService
{
    public async Task<ConversationView?> GetAsync(
        Guid userId, Guid offerId, DateTimeOffset? after, CancellationToken cancellationToken)
    {
        ParticipantContext? context = await Participant(offerId, cancellationToken);
        if (context is null || !context.IsParticipant(userId)) return null;

        Conversation? conversation = await dbContext.Conversations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.OfferId == offerId, cancellationToken);
        IQueryable<Message> query = dbContext.Messages.AsNoTracking().Where(item => item.ConversationId == offerId);
        if (after.HasValue) query = query.Where(item => item.CreatedAt > after.Value);
        Message[] messages = await query.OrderBy(item => item.CreatedAt).Take(100).ToArrayAsync(cancellationToken);
        DateTimeOffset? readAt = userId == context.BuyerId ? conversation?.BuyerLastReadAt : conversation?.SupplierLastReadAt;
        int unreadCount = await dbContext.Messages.CountAsync(
            item => item.ConversationId == offerId && item.SenderId != userId
                && (!readAt.HasValue || item.CreatedAt > readAt.Value), cancellationToken);
        string otherPartyName = await OtherPartyName(context, userId, cancellationToken);
        return new ConversationView(
            offerId,
            context.RfqId,
            context.RfqTitle,
            otherPartyName,
            userId == context.BuyerId ? "Buyer" : "Supplier",
            unreadCount,
            messages.Select(item => new ConversationMessageView(
                item.Id, item.Body, item.CreatedAt, item.SenderId == userId)).ToArray());
    }

    public async Task<ConversationMessageView?> SendAsync(
        Guid userId, Guid offerId, string body, CancellationToken cancellationToken)
    {
        ParticipantContext? context = await Participant(offerId, cancellationToken);
        if (context is null || !context.IsParticipant(userId)) return null;
        DateTimeOffset now = timeProvider.GetUtcNow();
        Conversation? conversation = await dbContext.Conversations.SingleOrDefaultAsync(
            item => item.OfferId == offerId, cancellationToken);
        if (conversation is null)
        {
            conversation = Conversation.Create(offerId, context.BuyerId, context.SupplierId, now);
            dbContext.Conversations.Add(conversation);
        }

        Message message = Message.Create(conversation.Id, userId, body, now);
        conversation.MessageSent(now);
        conversation.MarkRead(userId, now);
        dbContext.Messages.Add(message);
        Guid recipientId = userId == context.BuyerId ? context.SupplierId : context.BuyerId;
        dbContext.Notifications.Add(Notification.Create(
            recipientId,
            NotificationType.MessageReceived,
            $"Mesaj nou: {context.RfqTitle}",
            $"Ai primit un mesaj de la {(userId == context.BuyerId ? "cumpărător" : "furnizor")}.",
            $"/offers/{offerId}/conversation",
            $"message:{message.Id}",
            now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ConversationMessageView(message.Id, message.Body, message.CreatedAt, true);
    }

    public async Task<bool> MarkReadAsync(Guid userId, Guid offerId, CancellationToken cancellationToken)
    {
        ParticipantContext? context = await Participant(offerId, cancellationToken);
        if (context is null || !context.IsParticipant(userId)) return false;
        Conversation? conversation = await dbContext.Conversations.SingleOrDefaultAsync(
            item => item.OfferId == offerId, cancellationToken);
        if (conversation is null) return true;
        conversation.MarkRead(userId, timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<ParticipantContext?> Participant(Guid offerId, CancellationToken cancellationToken) =>
        await (from offer in dbContext.Offers.AsNoTracking()
               join rfq in dbContext.Rfqs.AsNoTracking() on offer.RfqId equals rfq.Id
               where offer.Id == offerId && offer.SubmittedAt != null
               select new ParticipantContext(offer.Id, rfq.Id, rfq.Title, rfq.BuyerId, offer.SupplierId))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<string> OtherPartyName(
        ParticipantContext context, Guid viewerId, CancellationToken cancellationToken)
    {
        if (viewerId == context.BuyerId)
            return await dbContext.SupplierProfiles.AsNoTracking().Where(item => item.Id == context.SupplierId)
                .Select(item => item.TradingName).SingleOrDefaultAsync(cancellationToken) ?? "Furnizor";
        return await dbContext.BuyerProfiles.AsNoTracking().Where(item => item.Id == context.BuyerId)
            .Select(item => item.CompanyName ?? item.DisplayName).SingleOrDefaultAsync(cancellationToken) ?? "Cumpărător";
    }

    private sealed record ParticipantContext(Guid OfferId, Guid RfqId, string RfqTitle, Guid BuyerId, Guid SupplierId)
    {
        public bool IsParticipant(Guid userId) => userId == BuyerId || userId == SupplierId;
    }
}
