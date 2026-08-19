using Marketplace.Application.Companies;
using Marketplace.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/admin/companies")]
[Authorize(Roles = MarketplaceRoles.Administrator)]
public sealed class CompanyRegistryController(ICompanyRegistryService registry) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await registry.GetAsync(cancellationToken));
}
