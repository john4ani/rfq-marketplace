using System.ComponentModel.DataAnnotations;
using Marketplace.Application.Identity;
using Marketplace.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Marketplace.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("authentication")]
public sealed class AuthController(IIdentityService identityService, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("register/buyer")]
    public Task<IActionResult> RegisterBuyer(RegisterRequest request, CancellationToken cancellationToken) =>
        Register(request, MarketplaceRoles.Buyer, cancellationToken);

    [HttpPost("register/supplier")]
    public Task<IActionResult> RegisterSupplier(RegisterRequest request, CancellationToken cancellationToken) =>
        Register(request, MarketplaceRoles.Supplier, cancellationToken);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        AuthenticationResult? result = await identityService.LoginAsync(request.Email, request.Password, request.AccountType, cancellationToken);
        if (result is null) return Unauthorized(Problem(title: "Adresa de e-mail, parola sau tipul contului este incorect."));
        SetRefreshCookie(result.RefreshToken, result.AccessTokenExpiresAt.AddDays(14));
        return Ok(new TokenResponse(result.AccessToken, result.AccessTokenExpiresAt));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue("refreshToken", out string? token)) return Unauthorized();
        AuthenticationResult? result = await identityService.RefreshAsync(token, cancellationToken);
        if (result is null) { DeleteRefreshCookie(); return Unauthorized(); }
        SetRefreshCookie(result.RefreshToken, result.AccessTokenExpiresAt.AddDays(14));
        return Ok(new TokenResponse(result.AccessToken, result.AccessTokenExpiresAt));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue("refreshToken", out string? token))
            await identityService.RevokeAsync(token, cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpPost("request-password-reset")]
    public async Task<IActionResult> RequestPasswordReset(EmailRequest request, CancellationToken cancellationToken)
    {
        string? token = await identityService.CreatePasswordResetTokenAsync(request.Email, request.AccountType, cancellationToken);
        return Accepted(environment.IsDevelopment() ? new DevelopmentTokenResponse(token) : null);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken) =>
        await identityService.ResetPasswordAsync(request.Email, request.AccountType, request.Token, request.NewPassword, cancellationToken) ? NoContent() : BadRequest(Problem(title: "Cererea de resetare este invalidă sau a expirat."));

    [Authorize]
    [DisableRateLimiting]
    [HttpGet("me")]
    public IActionResult Me() => Ok(new { userId = User.FindFirst("sub")?.Value, email = User.FindFirst("email")?.Value, roles = User.FindAll(System.Security.Claims.ClaimTypes.Role).Select(claim => claim.Value) });

    private async Task<IActionResult> Register(RegisterRequest request, string role, CancellationToken cancellationToken)
    {
        RegistrationResult result = await identityService.RegisterAsync(request.Email, request.Password, role, cancellationToken);
        if (!result.Succeeded)
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["registration"] = result.Errors.ToArray() })
            {
                Title = "Account registration failed."
            });
        return StatusCode(StatusCodes.Status201Created);
    }

    private void SetRefreshCookie(string value, DateTimeOffset expires) => Response.Cookies.Append("refreshToken", value, new CookieOptions { HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Strict, Expires = expires, Path = "/api/v1/auth" });
    private void DeleteRefreshCookie() => Response.Cookies.Delete("refreshToken", new CookieOptions { Path = "/api/v1/auth" });
}

public sealed record RegisterRequest([Required, EmailAddress] string Email, [Required, MinLength(8)] string Password);
public sealed record LoginRequest([Required, EmailAddress] string Email, [Required] string Password, [Required] string AccountType);
public sealed record EmailRequest([Required, EmailAddress] string Email, [Required] string AccountType);
public sealed record TokenRequest([Required, EmailAddress] string Email, [Required] string Token);
public sealed record ResetPasswordRequest([Required, EmailAddress] string Email, [Required] string AccountType, [Required] string Token, [Required, MinLength(8)] string NewPassword);
public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
public sealed record DevelopmentTokenResponse(string? Token);
