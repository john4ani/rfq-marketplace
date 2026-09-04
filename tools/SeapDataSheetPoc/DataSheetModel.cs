using System.Text.RegularExpressions;

namespace SeapDataSheetPoc;

/// <summary>Output shape of the deterministic Fișa de date preparation (Stage 1.5 POC v2). No semantic
/// interpretation, no LLM — the extractor only delimits the published text and keeps its source pages.</summary>
public sealed record DataSheetExtraction(
    string DocumentRole,
    bool HasTextLayer,
    bool NeedsOcr,
    int PageCount,
    int ExtractionVersion,
    int HeadingLetters,
    IReadOnlyList<DataSheetSection> Sections,
    IReadOnlyList<HeadingRejection> RejectedHeadings,
    IReadOnlyList<string> MissingExpectedRoles,
    IReadOnlyList<string> Warnings,
    string Sha256);

/// <summary>One delimited section. <see cref="RawCode"/> is the printed code (the citation); the same logical
/// role can be printed under different codes (e.g. I.5 vs I.6) and repeats once per lot.</summary>
public sealed record DataSheetSection(
    string RawCode,
    string NormalizedRole,
    string Title,
    int StartPage,
    int EndPage,
    int? LotIndex,
    string Text);

/// <summary>A heading-like token evaluated and rejected, with the reason (mid-paragraph, wrong margin,
/// unknown code). Reported separately — 0 unmatched is not a success criterion on its own.</summary>
public sealed record HeadingRejection(int Page, string Text, string Reason);

/// <summary>Heading lexicon entry: <see cref="Pattern"/> is matched against the START of a reconstructed
/// visual line; <see cref="RawCode"/> is the printed code kept as citation; <see cref="Role"/> is the stable
/// logical key; <see cref="Title"/> is the canonical Romanian title.</summary>
public sealed record HeadingRule(Regex Pattern, string RawCode, string Role, string Title);

/// <summary>A word token of the flattened document with its original offset.</summary>
public sealed record Token(int Start, int Length);

/// <summary>A visual text line reconstructed from word coordinates.</summary>
public sealed record VisualLine(int Page, int LineStartOffset, int LineEndOffset, double Left, string Text);

public static class DataSheetRoles
{
    // Stable logical keys (not SEAP codes — the printed code is the citation).
    public const string SectionAuthority = "SectionAuthority";
    public const string SectionObject = "SectionObject";
    public const string SectionConditions = "SectionConditions";
    public const string SectionProcedure = "SectionProcedure";
    public const string SectionComplementary = "SectionComplementary";
    public const string AuthorityNameAddresses = "AuthorityNameAddresses";
    public const string ContractingAuthorityMainActivity = "ContractingAuthorityMainActivity";
    public const string AuthorityMainActivity = "ContractingAuthorityMainActivity";
    public const string ProcurementObjectTitle = "ProcurementObjectTitle";
    public const string CpvCode = "CpvCode";
    public const string ContractType = "ContractType";
    public const string ContractDescription = "ContractDescription";
    public const string EstimatedValue = "EstimatedValue";
    public const string Lots = "Lots";
    public const string LotHeader = "LotHeader";
    public const string LotTitle = "LotTitle";
    public const string LotCpv = "LotCpv";
    public const string LotDescription = "LotDescription";
    public const string AwardCriteria = "AwardCriteria";
    public const string LotEstimatedValue = "LotEstimatedValue";
    public const string ContractDuration = "ContractDuration";
    public const string ParticipationConditions = "ParticipationConditions";
    public const string PersonalSituationExclusionDuae = "PersonalSituationExclusionDuae";
    public const string ProfessionalActivityCapacity = "ProfessionalActivityCapacity";
    public const string EconomicFinancialCapacity = "EconomicFinancialCapacity";
    public const string TechnicalProfessionalCapacity = "TechnicalProfessionalCapacity";
    public const string QualityStandards = "QualityStandards";
    public const string DepositsGuarantees = "DepositsGuarantees";
    public const string ParticipationGuarantee = "ParticipationGuarantee";
    public const string PerformanceGuarantee = "PerformanceGuarantee";
    public const string FinancingPaymentConditions = "FinancingPaymentConditions";
    public const string ContractConditions = "ContractConditions";
    public const string SpecialContractExecutionConditions = "SpecialContractExecutionConditions";
    public const string ContractExecutionPersonnel = "ContractExecutionPersonnel";
    public const string ProcedureType = "ProcedureType";
    public const string AwardingMethod = "AwardingMethod";
    public const string ElectronicAuction = "ElectronicAuction";
    public const string OfferReceiptDeadline = "OfferReceiptDeadline";
    public const string OfferLanguages = "OfferLanguages";
    public const string SupplementaryInfo = "SupplementaryInfo";
    public const string ContestationProcedure = "ContestationProcedure";
}
