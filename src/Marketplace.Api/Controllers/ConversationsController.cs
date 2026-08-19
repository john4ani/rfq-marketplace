using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

// Unified Mesaje inbox: the User's own procurement conversations, whether buying or representing a company.
// It complements the contextual entry points; it never exposes other people's private conversations.
[ApiController]
[Route("api/v1/conversations")]
[Authorize]
public sealed class ConversationsController(IMessagingService messagingService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await messagingService.ListConversationsAsync(UserId(), cancellationToken));

    private Guid UserId()
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out Guid id) ? id : throw new UnauthorizedAccessException();
    }
}
