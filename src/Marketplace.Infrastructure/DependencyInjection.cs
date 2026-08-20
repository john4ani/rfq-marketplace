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
using Marketplace.Application.Messaging;
using Marketplace.Infrastructure.Messaging;
using Marketplace.Application.AiIntake;
using Marketplace.Infrastructure.AiIntake;
using Marketplace.Application.SupplierDiscovery;
using Marketplace.Infrastructure.SupplierDiscovery;
using Marketplace.Application.Trust;
using Marketplace.Infrastructure.Trust;
using Marketplace.Application.Operations;
using Marketplace.Infrastructure.Operations;
using Marketplace.Application.Companies;
using Marketplace.Infrastructure.Companies;
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
        services.AddDbContext<MarketplaceDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(MarketplaceDbContext).Assembly.FullName);
                npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null);
            }));
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
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromMinutes(30));
        services.Configure<JwtOptions>(configuration.GetRequiredSection(JwtOptions.SectionName));
        services.Configure<PasswordResetEmailOptions>(configuration.GetSection(PasswordResetEmailOptions.SectionName));
        services.Configure<EmailVerificationOptions>(configuration.GetSection(EmailVerificationOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<SupplierOutreachOptions>(configuration.GetSection(SupplierOutreachOptions.SectionName));
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
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IPasswordResetDelivery, SmtpPasswordResetDelivery>();
        services.AddScoped<IEmailVerificationDelivery, SmtpEmailVerificationDelivery>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ICompanyAuthorizationService, CompanyAuthorizationService>();
        services.AddScoped<ICompanyRegistryService, CompanyRegistryService>();
        services.AddScoped<ISupplierSettingsService, SupplierSettingsService>();
        services.AddScoped<ILiveMarketService, LiveMarketService>();
        services.AddScoped<IRfqService, RfqService>();
        services.AddScoped<IProjectTemplateService, ProjectTemplateService>();
        services.Configure<AiIntakeOptions>(configuration.GetSection(AiIntakeOptions.SectionName));
        services.AddHttpClient<OpenAiResponsesTransport>((provider, client) =>
        {
            AiIntakeOptions options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiIntakeOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.RequestTimeoutSeconds, 5, 120));
        });
        services.Configure<GooglePlacesOptions>(configuration.GetSection(GooglePlacesOptions.SectionName));
        services.AddHttpClient<GooglePlacesClient>((provider, client) =>
        {
            GooglePlacesOptions options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GooglePlacesOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.RequestTimeoutSeconds, 5, 60));
        });
        services.AddScoped<IGooglePlacesClient>(provider => provider.GetRequiredService<GooglePlacesClient>());
        services.AddHttpClient<GoogleLocationGeocoder>((provider, client) =>
        {
            GooglePlacesOptions options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<GooglePlacesOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.RequestTimeoutSeconds, 5, 60));
        });
        services.AddScoped<ILocationGeocoder>(provider => provider.GetRequiredService<GoogleLocationGeocoder>());
        services.AddScoped<IGeneralNeedInterpreter, OpenAiNeedExtractionProvider>();
        services.AddScoped<IConstructionProjectInterpreter, OpenAiConstructionProjectInterpreter>();
        services.AddScoped<IAssistedIntakeService, AssistedIntakeService>();
        services.AddScoped<IConstructionIntakeService, ConstructionIntakeService>();
        services.AddScoped<IProcurementNeedAuditTrail, ProcurementNeedAuditTrail>();
        services.AddScoped<IOfferService, OfferService>();
        services.AddScoped<ProcurementOutcomeReminderService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IMessagingService, MessagingService>();
        services.AddSingleton<ICategoryDiscoveryProfileProvider, CategoryDiscoveryProfileProvider>();
        services.AddSingleton<ISupplierSearchIntentBuilder, SupplierSearchIntentBuilder>();
        services.AddScoped<IGooglePlacesEvidenceCollector, GooglePlacesEvidenceCollector>();
        services.AddScoped<IWebEvidenceCollector, WebEvidenceCollector>();
        services.AddSingleton<ISupplierEvidenceMerger, SupplierEvidenceMerger>();
        services.AddScoped<ISupplierEvidencePipeline, SupplierEvidencePipeline>();
        services.AddScoped<ISupplierEvidenceQualifier, SupplierEvidenceQualifier>();
        services.AddScoped<ISupplierDiscoveryService, SupplierDiscoveryService>();
        services.AddScoped<ITrustService, TrustService>();
        services.AddScoped<IProductEventService, ProductEventService>();
        services.AddScoped<IConciergeService, ConciergeService>();
        return services;
    }
}
