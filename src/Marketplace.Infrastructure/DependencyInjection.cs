using System.Text;
using Npgsql;
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
using Marketplace.Application.Seap;
using Marketplace.Infrastructure.Seap;
using Marketplace.Infrastructure.Bidding;
using Marketplace.Infrastructure.Documents;
using Marketplace.Infrastructure.Embeddings;
using Marketplace.Infrastructure.Storage;

using Marketplace.Application.Bidding;
using Marketplace.Application.Billing;
using Marketplace.Infrastructure.Billing;
using Amazon.S3;
using Marketplace.Application.Companies;
using Marketplace.Infrastructure.Companies;
using Marketplace.Application.AutoBidding;
using Marketplace.Infrastructure.AutoBidding;

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
                npgsql.UseVector();
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
        services.AddScoped<ISeapTenderMatchInterpreter, OpenAiSeapTenderMatchInterpreter>();
        services.AddScoped<IAssistedIntakeService, AssistedIntakeService>();
        services.AddScoped<IConstructionIntakeService, ConstructionIntakeService>();
        services.AddScoped<ISeapTenderMatchService, SeapTenderMatchService>();
        services.AddScoped<IProcurementNeedAuditTrail, ProcurementNeedAuditTrail>();
        services.AddScoped<IOfferService, OfferService>();
        services.AddScoped<ProcurementOutcomeReminderService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IMessagingService, MessagingService>();
        services.AddSingleton<ICategoryDiscoveryProfileProvider, CategoryDiscoveryProfileProvider>();
        services.AddSingleton<ISupplierSearchIntentBuilder, SupplierSearchIntentBuilder>();
        services.AddScoped<IGooglePlacesEvidenceCollector, GooglePlacesEvidenceCollector>();
        services.AddScoped<IWebEvidenceCollector, WebEvidenceCollector>();
        services.AddScoped<ISupplierEvidenceMerger, SupplierEvidenceMerger>();
        services.AddScoped<ISupplierEvidencePipeline, SupplierEvidencePipeline>();
        services.AddScoped<ISupplierEvidenceQualifier, SupplierEvidenceQualifier>();
        services.AddScoped<ISupplierDiscoveryService, SupplierDiscoveryService>();
        services.AddScoped<ITrustService, TrustService>();
        services.AddScoped<IProductEventService, ProductEventService>();
        services.AddScoped<IConciergeService, ConciergeService>();
        services.Configure<FeaturesOptions>(configuration.GetSection(FeaturesOptions.SectionName));
        services.Configure<SeapOptions>(configuration.GetSection(SeapOptions.SectionName));
        services.Configure<SeapStorageOptions>(configuration.GetSection("Seap:Storage"));
        services.Configure<AutoBiddingOptions>(configuration.GetSection(AutoBiddingOptions.SectionName));
        services.Configure<S3Options>(configuration.GetSection(S3Options.SectionName));
        services.AddHttpClient("seap");
        services.AddSingleton<IAmazonS3>(provider =>
        {
            S3Options options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<S3Options>>().Value;
            string region = string.IsNullOrWhiteSpace(options.Region) ? "eu-central-1" : options.Region;
            if (!string.IsNullOrWhiteSpace(options.ServiceUrl))
            {
                // Cloudflare R2 (S3-compatible): static credentials + path-style addressing (no virtual-host buckets).
                AmazonS3Config r2Config = new()
                {
                    ServiceURL = options.ServiceUrl,
                    ForcePathStyle = true,
                    AuthenticationRegion = region,
                    // Cloudflare R2 does not implement AWS SigV4 streaming signing (it rejects both
                    // STREAMING-AWS4-HMAC-SHA256-PAYLOAD and the -TRAILER variant). Disable the automatic
                    // flexible checksum that would add the trailer header R2 also rejects; the full-payload
                    // signing itself is set per request in CloudflareR2StorageService (UseChunkEncoding=false).
                    RequestChecksumCalculation = Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED
                };
                return new AmazonS3Client(options.AccessKeyId, options.SecretAccessKey, r2Config);
            }
            // AWS S3 via the default IAM credential chain.
            return new AmazonS3Client(Amazon.RegionEndpoint.GetBySystemName(region));
        });
        services.Configure<LocalStorageOptions>(configuration.GetSection(LocalStorageOptions.SectionName));
        LocalStorageOptions localStorage = configuration.GetSection(LocalStorageOptions.SectionName).Get<LocalStorageOptions>() ?? new();
        if (localStorage.UseLocalStorage)
            services.AddScoped<IStorageService, LocalFileStorageService>();
        else
            services.AddScoped<IStorageService, CloudflareR2StorageService>();
        services.AddScoped<ISeapApiClient, SeapApiClient>();
        services.AddScoped<ISeapAttachmentStorage, SeapAttachmentStorage>();
        services.AddScoped<ISeapCategorizer, SeapCategorizerService>();
        services.AddScoped<ISeapIngestionService, SeapIngestionService>();
        services.AddHostedService<SeapIngestionWorker>();
        services.Configure<DocumentIntelligenceOptions>(configuration.GetSection(DocumentIntelligenceOptions.SectionName));
        services.AddHttpClient("embeddings");
        services.AddHttpClient("claude");
        services.AddScoped<IEmbeddingService, OpenAiEmbeddingService>();
        services.AddScoped<IDocumentExtractionService, AzureDocumentIntelligenceExtractionService>();
        // AI:UseMockProvider swaps the LLM to a deterministic preset-JSON provider for zero-cost local
        // debugging; prod/staging keep the real Anthropic Claude provider (which hard-caps max_tokens).
        if (configuration.GetValue<bool>("AI:UseMockProvider"))
            services.AddScoped<ILlmProvider>(_ => new StaticMockLlmProvider(StaticMockLlmProvider.RomanianProcurementExpertProposal));
        else
            services.AddScoped<ILlmProvider, AnthropicClaudeProvider>();
        services.AddScoped<IDocumentGenerator, DocumentGenerator>();
        services.AddScoped<IBiddingGenerationService, BiddingGenerationService>();
        services.Configure<StripeOptions>(configuration.GetSection(StripeOptions.SectionName));
        services.AddScoped<IStripeGateway, StripeGateway>();
        services.AddScoped<ICompanyVaultService, CompanyVaultService>();
        services.AddScoped<IAutoBiddingService, AutoBiddingService>();
        services.AddSingleton<IPipelineProgressStore, PipelineProgressStore>();
        return services;
    }
}
