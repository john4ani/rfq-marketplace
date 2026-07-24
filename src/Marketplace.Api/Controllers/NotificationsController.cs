using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Notifications;
using Marketplace.Domain.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok((await notificationService.GetAsync(UserId(), cancellationToken)).Select(NotificationResponse.From));

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken) =>
        Ok(new UnreadCountResponse(await notificationService.GetUnreadCountAsync(UserId(), cancellationToken)));

    [HttpPost("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid notificationId, CancellationToken cancellationToken)
    {
        Notification? notification = await notificationService.MarkReadAsync(UserId(), notificationId, cancellationToken);
        return notification is null ? NotFound() : Ok(NotificationResponse.From(notification));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        await notificationService.MarkAllReadAsync(UserId(), cancellationToken);
        return NoContent();
    }

    private Guid UserId()
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid id) ? id : throw new UnauthorizedAccessException();
    }
}

public sealed record NotificationResponse(Guid Id, NotificationType Type, string Title, string Message, string Link,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, bool IsRead)
{
    public static NotificationResponse From(Notification value) => new(value.Id, value.Type, value.Title, value.Message,
        value.Link, value.CreatedAt, value.ReadAt, value.IsRead);
}
public sealed record UnreadCountResponse(int Count);
