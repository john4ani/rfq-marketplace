using System.Text;
using Marketplace.Application.Identity;
using Marketplace.Infrastructure.Identity;
using Marketplace.Infrastructure.Persistence;
using Marketplace.Application.Profiles;
using Marketplace.Infrastructure.Profiles;
using Marketplace.Application.Suppliers;
using Marketplace.Infrastructure.Suppliers;
using Marketplace.Application.LiveMarket;
using Marketplace.Infrastructure.LiveMarket;
using Marketplace.Application.Rfqs;
using Marketplace.Infrastructure.Rfqs;
using Marketplace.Application.Offers;
using Marketplace.Infrastructure.Offers;
using Marketplace.Application.Notifications;
using Marketplace.Infrastructure.Notifications;
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
            options.User.RequireUniqueEmail = true;
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
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ISupplierSettingsService, SupplierSettingsService>();
        services.AddScoped<ILiveMarketService, LiveMarketService>();
        services.AddScoped<IRfqService, RfqService>();
        services.AddScoped<IOfferService, OfferService>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
