using Marketplace.Application.Notifications;
using Marketplace.Domain.Notifications;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Infrastructure.Notifications;

public sealed class NotificationService(MarketplaceDbContext dbContext, TimeProvider timeProvider) : INotificationService
{
    public async Task<IReadOnlyCollection<Notification>> GetAsync(Guid recipientId, CancellationToken cancellationToken) =>
        await dbContext.Notifications.AsNoTracking().Where(item => item.RecipientId == recipientId)
            .OrderByDescending(item => item.CreatedAt).Take(50).ToListAsync(cancellationToken);

    public Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken) =>
        dbContext.Notifications.CountAsync(item => item.RecipientId == recipientId && item.ReadAt == null, cancellationToken);

    public async Task<Notification?> MarkReadAsync(Guid recipientId, Guid notificationId, CancellationToken cancellationToken)
    {
        Notification? notification = await dbContext.Notifications.SingleOrDefaultAsync(
            item => item.Id == notificationId && item.RecipientId == recipientId, cancellationToken);
        if (notification is null) return null;
        notification.MarkRead(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return notification;
    }

    public async Task MarkAllReadAsync(Guid recipientId, CancellationToken cancellationToken)
    {
        Notification[] unread = await dbContext.Notifications.Where(
            item => item.RecipientId == recipientId && item.ReadAt == null).ToArrayAsync(cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        foreach (Notification notification in unread) notification.MarkRead(now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
