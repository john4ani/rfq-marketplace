using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Marketplace.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyQualificationEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CompanyCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    CertificateType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SerialNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyCertificates_SupplierProfiles_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyExperiences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContractTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContractNumberDate = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ValueAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ValueCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    CpvCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CompletionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DocumentRefUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyExperiences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyExperiences_SupplierProfiles_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyLegalProfiles",
                columns: table => new
                {
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalRepresentativeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LegalRepresentativeRole = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    LegalForm = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyLegalProfiles", x => x.SupplierId);
                    table.ForeignKey(
                        name: "FK_CompanyLegalProfiles_SupplierProfiles_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyPersonnel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProfessionalCertificates = table.Column<string[]>(type: "text[]", nullable: false),
                    CvUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyPersonnel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyPersonnel_SupplierProfiles_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "SupplierProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CompanyLegalProfiles_BankAccounts",
                columns: table => new
                {
                    CompanyLegalProfileSupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BankName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyLegalProfiles_BankAccounts", x => new { x.CompanyLegalProfileSupplierId, x.Id });
                    table.ForeignKey(
                        name: "FK_CompanyLegalProfiles_BankAccounts_CompanyLegalProfiles_Comp~",
                        column: x => x.CompanyLegalProfileSupplierId,
                        principalTable: "CompanyLegalProfiles",
                        principalColumn: "SupplierId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCertificates_SupplierId_ValidUntil",
                table: "CompanyCertificates",
                columns: new[] { "SupplierId", "ValidUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyExperiences_SupplierId_CompletionDate",
                table: "CompanyExperiences",
                columns: new[] { "SupplierId", "CompletionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyPersonnel_SupplierId",
                table: "CompanyPersonnel",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyCertificates");

            migrationBuilder.DropTable(
                name: "CompanyExperiences");

            migrationBuilder.DropTable(
                name: "CompanyLegalProfiles_BankAccounts");

            migrationBuilder.DropTable(
                name: "CompanyPersonnel");

            migrationBuilder.DropTable(
                name: "CompanyLegalProfiles");
        }
    }
}
