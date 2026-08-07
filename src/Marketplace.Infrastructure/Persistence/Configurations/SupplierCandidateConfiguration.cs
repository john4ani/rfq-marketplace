using Marketplace.Domain.SupplierDiscovery;
using Marketplace.Domain.Suppliers;
using Marketplace.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class SupplierCandidateConfiguration : IEntityTypeConfiguration<SupplierCandidate>
{
    public void Configure(EntityTypeBuilder<SupplierCandidate> builder)
    {
        builder.ToTable("SupplierCandidates");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.CompanyName).HasMaxLength(180).IsRequired();
        builder.Property(candidate => candidate.NormalizedCompanyName).HasMaxLength(180).IsRequired();
        builder.Property(candidate => candidate.ContactEmail).HasMaxLength(254);
        builder.Property(candidate => candidate.PhoneNumber).HasMaxLength(40);
        builder.Property(candidate => candidate.Website).HasMaxLength(500);
        builder.Property(candidate => candidate.City).HasMaxLength(120).IsRequired();
        builder.Property(candidate => candidate.County).HasMaxLength(120).IsRequired();
        builder.Property(candidate => candidate.Source).HasMaxLength(500).IsRequired();
        builder.Property(candidate => candidate.Notes).HasMaxLength(1000);
        builder.Property(candidate => candidate.ReviewNotes).HasMaxLength(1000);
        builder.Property(candidate => candidate.Version).IsRowVersion();
        builder.HasIndex(candidate => new { candidate.NormalizedCompanyName, candidate.County }).IsUnique();
        builder.HasIndex(candidate => new { candidate.Status, candidate.County });
        builder.Ignore(candidate => candidate.CategoryIds);
        builder.HasMany(candidate => candidate.Categories).WithOne().HasForeignKey(category => category.CandidateId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(candidate => candidate.Categories).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(candidate => candidate.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(candidate => candidate.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(candidate => candidate.ClaimedBySupplierId).OnDelete(DeleteBehavior.Restrict);
    }
}
