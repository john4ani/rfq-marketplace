using System.Text;
using Marketplace.Application.Identity;
using Marketplace.Infrastructure.Identity;
using Marketplace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Marketplace.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("MarketplaceDatabase")
            ?? throw new InvalidOperationException("Connection string 'MarketplaceDatabase' is required.");
        services.AddDbContext<MarketplaceDbContext>(options => options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(MarketplaceDbContext).Assembly.FullName)));
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = false;
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.SignIn.RequireConfirmedEmail = false;
        }).AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<MarketplaceDbContext>().AddDefaultTokenProviders();
        services.Configure<JwtOptions>(configuration.GetRequiredSection(JwtOptions.SectionName));
        JwtOptions jwt = configuration.GetRequiredSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is required.");
        if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32) throw new InvalidOperationException("JWT signing key must contain at least 32 bytes.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience, ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)), ClockSkew = TimeSpan.FromSeconds(30), NameClaimType = "email", RoleClaimType = System.Security.Claims.ClaimTypes.Role };
        });
        services.AddAuthorization();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IIdentityService, IdentityService>();
        return services;
    }
}
