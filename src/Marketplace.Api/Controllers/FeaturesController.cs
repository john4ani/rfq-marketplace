using Marketplace.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Marketplace.Api.Controllers;

/// <summary>Public surface exposing the live feature flags so the SPA can hide marketplace navigation
/// when MarketPlace:Enabled is false (only Anunțuri SEAP + Admin remain).</summary>
[ApiController]
[Route("api/v1/features")]
public sealed class FeaturesController(IOptionsMonitor<FeaturesOptions> features) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Get() => Ok(new
    {
        marketplaceEnabled = features.CurrentValue.MarketPlaceEnabled,
        autoBiddingEnabled = features.CurrentValue.AutoBiddingEnabled,
        seapDirectHitEnabled = features.CurrentValue.SeapDirectHitEnabled,
        seapIngestionWorkerEnabled = features.CurrentValue.SeapIngestionWorkerEnabled
    });
}