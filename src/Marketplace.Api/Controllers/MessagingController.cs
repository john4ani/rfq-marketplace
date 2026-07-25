using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Messaging;
using Marketplace.Domain.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/offers/{offerId:guid}/conversation")]
[Authorize]
public sealed class MessagingController(IMessagingService messagingService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        Guid offerId, [FromQuery] DateTimeOffset? after, CancellationToken cancellationToken)
    {
        ConversationView? conversation = await messagingService.GetAsync(UserId(), offerId, after, cancellationToken);
        return conversation is null ? NotFound() : Ok(conversation);
    }

    [HttpPost("messages")]
    public async Task<IActionResult> Send(
        Guid offerId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ConversationMessageView? message = await messagingService.SendAsync(
                UserId(), offerId, request.Body, cancellationToken);
            return message is null ? NotFound() : Ok(message);
        }
        catch (MessagingRuleException exception)
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["message"] = [exception.Message] })
            { Title = "Mesajul nu a putut fi trimis." });
        }
    }

    [HttpPost("read")]
    public async Task<IActionResult> MarkRead(Guid offerId, CancellationToken cancellationToken) =>
        await messagingService.MarkReadAsync(UserId(), offerId, cancellationToken) ? NoContent() : NotFound();

    private Guid UserId()
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid id) ? id : throw new UnauthorizedAccessException();
    }
}

public sealed record SendMessageRequest([Required, MaxLength(2_000)] string Body);
