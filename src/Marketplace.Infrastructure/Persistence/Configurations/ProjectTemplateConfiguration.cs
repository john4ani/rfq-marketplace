using Marketplace.Domain.Rfqs;
using Marketplace.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public sealed class ProjectTemplateConfiguration : IEntityTypeConfiguration<ProjectTemplate>
{
    public void Configure(EntityTypeBuilder<ProjectTemplate> builder)
    {
        builder.ToTable("ProjectTemplates");
        builder.HasKey(template => template.Id);
        builder.Property(template => template.Name).HasMaxLength(180).IsRequired();
        builder.Property(template => template.Slug).HasMaxLength(180).IsRequired();
        builder.Property(template => template.Description).HasMaxLength(2000);
        builder.HasIndex(template => new { template.VerticalId, template.Slug }).IsUnique();
        builder.HasOne<Vertical>().WithMany().HasForeignKey(template => template.VerticalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(template => template.Categories).WithOne().HasForeignKey(category => category.ProjectTemplateId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(template => template.Categories).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

public sealed class ProjectTemplateCategoryConfiguration : IEntityTypeConfiguration<ProjectTemplateCategory>
{
    public void Configure(EntityTypeBuilder<ProjectTemplateCategory> builder)
    {
        builder.ToTable("ProjectTemplateCategories");
        builder.HasKey(category => new { category.ProjectTemplateId, category.CategoryId });
        builder.HasOne<Category>().WithMany().HasForeignKey(category => category.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
