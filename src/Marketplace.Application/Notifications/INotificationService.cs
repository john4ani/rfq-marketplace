using Marketplace.Domain.Notifications;

namespace Marketplace.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyCollection<Notification>> GetAsync(Guid recipientId, CancellationToken cancellationToken);
    Task<int> GetUnreadCountAsync(Guid recipientId, CancellationToken cancellationToken);
    Task<Notification?> MarkReadAsync(Guid recipientId, Guid notificationId, CancellationToken cancellationToken);
    Task MarkAllReadAsync(Guid recipientId, CancellationToken cancellationToken);
}
