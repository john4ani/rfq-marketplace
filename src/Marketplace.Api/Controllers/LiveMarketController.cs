using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Marketplace.Application.LiveMarket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/piata-live")]
public sealed class LiveMarketController(ILiveMarketService liveMarketService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? profileType, CancellationToken cancellationToken)
    {
        Guid? viewerId = null;
        if (CurrentUserId(out Guid userId))
        {
            viewerId = userId;
            await liveMarketService.MarkOnlineAsync(userId, cancellationToken);
        }
        return Ok(await liveMarketService.GetSnapshotAsync(viewerId, profileType, cancellationToken));
    }

    [Authorize]
    [HttpPost("presence")]
    public async Task<IActionResult> Online(CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid userId)) return Unauthorized();
        await liveMarketService.MarkOnlineAsync(userId, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpDelete("presence")]
    public async Task<IActionResult> Offline(CancellationToken cancellationToken)
    {
        if (!CurrentUserId(out Guid userId)) return Unauthorized();
        await liveMarketService.MarkOfflineAsync(userId, cancellationToken);
        return NoContent();
    }

    private bool CurrentUserId(out Guid userId)
    {
        string? subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(subject, out userId);
    }
}
