using Marketplace.Domain.Notifications;
using Marketplace.Domain.Rfqs;
using Marketplace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Marketplace.Infrastructure.Notifications;

public sealed class ProcurementOutcomeReminderService(
    MarketplaceDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task EnsureDueAsync(Guid buyerId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        var dueRequests = await dbContext.Rfqs.AsNoTracking()
            .Where(rfq => rfq.BuyerId == buyerId
                && rfq.Outcome == null
                && (rfq.Status == RfqStatus.Published || rfq.Status == RfqStatus.ReceivingOffers)
                && rfq.QuotationDeadline <= now)
            .Select(rfq => new { rfq.Id, rfq.Title })
            .ToArrayAsync(cancellationToken);
        if (dueRequests.Length == 0) return;

        string[] keys = dueRequests.Select(rfq => $"rfq:{rfq.Id}:outcome-requested").ToArray();
        HashSet<string> existing = (await dbContext.Notifications.AsNoTracking()
            .Where(notification => keys.Contains(notification.DeduplicationKey))
            .Select(notification => notification.DeduplicationKey)
            .ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        foreach (var rfq in dueRequests)
        {
            string key = $"rfq:{rfq.Id}:outcome-requested";
            if (existing.Contains(key)) continue;
            dbContext.Notifications.Add(Notification.Create(
                buyerId,
                NotificationType.ProcurementOutcomeRequested,
                "Cum s-a încheiat achiziția?",
                $"Termenul cererii „{rfq.Title}” a trecut. Înregistrează rezultatul pentru a închide cererea.",
                $"/rfqs/{rfq.Id}/offers",
                key,
                now));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // List and unread-count requests may overlap. The unique key makes the
            // competing insert harmless; the other request has materialized the reminder.
            dbContext.ChangeTracker.Clear();
        }
    }
}
