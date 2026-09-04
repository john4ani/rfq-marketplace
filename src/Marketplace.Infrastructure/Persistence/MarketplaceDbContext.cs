using Marketplace.Infrastructure.Identity;
using Marketplace.Domain.Profiles;
using Marketplace.Domain.Suppliers;
using Marketplace.Domain.LiveMarket;
using Marketplace.Domain.Rfqs;
using Marketplace.Domain.AiIntake;
using Marketplace.Domain.Offers;
using Marketplace.Domain.Notifications;
using Marketplace.Domain.Messaging;
using Marketplace.Domain.SupplierDiscovery;
using Marketplace.Domain.Trust;
using Marketplace.Domain.Operations;
using Marketplace.Domain.Seap;
using Marketplace.Domain.Vault;
using Marketplace.Domain.Qualification;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Marketplace.Infrastructure.Persistence;

public sealed class MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<BuyerProfile> BuyerProfiles => Set<BuyerProfile>();
    public DbSet<SupplierProfile> SupplierProfiles => Set<SupplierProfile>();
    public DbSet<CompanyMembership> CompanyMemberships => Set<CompanyMembership>();
    public DbSet<CompanyLegalProfile> CompanyLegalProfiles => Set<CompanyLegalProfile>();
    public DbSet<CompanyExperience> CompanyExperiences => Set<CompanyExperience>();
    public DbSet<CompanyCertificate> CompanyCertificates => Set<CompanyCertificate>();
    public DbSet<CompanyPersonnel> CompanyPersonnel => Set<CompanyPersonnel>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Vertical> Verticals => Set<Vertical>();
    public DbSet<SupplierCategory> SupplierCategories => Set<SupplierCategory>();
    public DbSet<SupplierDeliverySettings> SupplierDeliverySettings => Set<SupplierDeliverySettings>();
    public DbSet<SupplierDeliveryArea> SupplierDeliveryAreas => Set<SupplierDeliveryArea>();
    public DbSet<UserPresence> UserPresences => Set<UserPresence>();
    public DbSet<Rfq> Rfqs => Set<Rfq>();
    public DbSet<RfqItem> RfqItems => Set<RfqItem>();
    public DbSet<NeedCategory> ProcurementNeedCategories => Set<NeedCategory>();
    public DbSet<ProjectTemplate> ProjectTemplates => Set<ProjectTemplate>();
    public DbSet<ProjectTemplateCategory> ProjectTemplateCategories => Set<ProjectTemplateCategory>();
    public DbSet<ProcurementNeedAuditEvent> ProcurementNeedAuditEvents => Set<ProcurementNeedAuditEvent>();
    public DbSet<AiIntakeSession> AiIntakeSessions => Set<AiIntakeSession>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<OfferItem> OfferItems => Set<OfferItem>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<SupplierCandidate> SupplierCandidates => Set<SupplierCandidate>();
    public DbSet<SupplierCandidateCategory> SupplierCandidateCategories => Set<SupplierCandidateCategory>();
    public DbSet<SupplierInvitation> SupplierInvitations => Set<SupplierInvitation>();
    public DbSet<SupplierOutreachContact> SupplierOutreachContacts => Set<SupplierOutreachContact>();
    public DbSet<SupplierContactSuppression> SupplierContactSuppressions => Set<SupplierContactSuppression>();
    public DbSet<OpportunityMatch> OpportunityMatches => Set<OpportunityMatch>();
    public DbSet<SupplierDiscoveryAuditEvent> SupplierDiscoveryAuditEvents => Set<SupplierDiscoveryAuditEvent>();
    public DbSet<SupplierWebDiscoveryRun> SupplierWebDiscoveryRuns => Set<SupplierWebDiscoveryRun>();
    public DbSet<AccountTrustRecord> AccountTrustRecords => Set<AccountTrustRecord>();
    public DbSet<AccountAuditEvent> AccountAuditEvents => Set<AccountAuditEvent>();
    public DbSet<ProductEvent> ProductEvents => Set<ProductEvent>();
    public DbSet<ConciergeMatchOverride> ConciergeMatchOverrides => Set<ConciergeMatchOverride>();
    public DbSet<CompanyVault> CompanyVaults => Set<CompanyVault>();
    public DbSet<VaultDocument> VaultDocuments => Set<VaultDocument>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<SeapTender> SeapTenders => Set<SeapTender>();
    public DbSet<SeapTenderAttachment> SeapTenderAttachments => Set<SeapTenderAttachment>();
    public DbSet<SeapRawIngestion> SeapRawIngestions => Set<SeapRawIngestion>();
    public DbSet<SeapIngestionRun> SeapIngestionRuns => Set<SeapIngestionRun>();
    public DbSet<SeapDocumentPreparation> SeapDocumentPreparations => Set<SeapDocumentPreparation>();
    public DbSet<SeapPreparedDocumentPage> SeapPreparedDocumentPages => Set<SeapPreparedDocumentPage>();
    public DbSet<SeapPreparedDocumentSection> SeapPreparedDocumentSections => Set<SeapPreparedDocumentSection>();
    public DbSet<CpvCategoryMapping> CpvCategoryMappings => Set<CpvCategoryMapping>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.HasPostgresExtension("vector");
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(MarketplaceDbContext).Assembly);
    }
}
