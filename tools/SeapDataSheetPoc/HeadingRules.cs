using System.Text.RegularExpressions;

namespace SeapDataSheetPoc;

/// <summary>Heading lexicon for the SEAP „Fișa de date" (format nou). Each entry anchors the printed code at
/// the START of a reconstructed visual line — mid-paragraph occurrences never match. The printed code is
/// preserved (<c>RawCode</c>), the stable role is separate (the same role may be printed under I.5 or I.6).</summary>
public static class HeadingRules
{
    public static IReadOnlyList<HeadingRule> Rules { get; } = Build();

    private static List<HeadingRule> Build()
    {
        var rules = new List<HeadingRule>();
        void Add(string rawCode, string role, string title, string codeRx, string? label = null)
        {
            string suffix = string.IsNullOrWhiteSpace(label) ? string.Empty : $@"\s*(?:{label})?";
            rules.Add(new HeadingRule(
                new Regex($@"\A{codeRx}{suffix}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                rawCode, role, title));
        }

        // Section banners.
        Add("I", "SectionAuthority", "Autoritatea contractantă", @"Sectiunea I\b", @".{0,80}");
        Add("II", "SectionObject", "Obiectul contractului", @"Sectiunea II\b", @".{0,80}");
        Add("III", "SectionConditions", "Informații juridice, economice, financiare și tehnice", @"Sectiunea III\b", @".{0,120}");
        Add("IV", "SectionProcedure", "Procedura", @"Sectiunea IV\b", @".{0,80}");
        Add("VI", "SectionComplementary", "Informații complementare", @"Sectiunea VI\b", @".{0,80}");
        // VI.3 is a real section at the document end — it must never be produced by the letter 'I' of a line
        // like "I.3 …" inside Section III content; anchoring at a line start + left margin is what protects it.
        Add("VI.3", "SupplementaryInfo", "Informații suplimentare", @"VI\.3\b", @"Informatii suplimentare");

        // I – authority.
        Add("I.1", "AuthorityNameAddresses", "Denumire și adrese", @"I\.1\)", @"Denumire si adrese");
        Add("I.2", "CommonProcurement", "Achiziție comună", @"I\.2\)", @"Achizitie comuna");
        Add("I.3", "Communication", "Comunicare", @"I\.3\)", @"Comunicare");
        Add("I.4", "AuthorityType", "Tipul autorității contractante", @"I\.4\)", @"Tipul autoritatii");
        // Same logical role, printed either I.5) (SCN) or I.6) (CN alias). The printed code stays the citation.
        Add("I.5", "ContractingAuthorityMainActivity", "Activitate principală", @"I\.5\)", @"Activitate principala");
        Add("I.6", "ContractingAuthorityMainActivity", "Activitate principală (alias CN)", @"I\.6\)", @"Activitate principala");

        // II – object of the contract.
        Add("II.1", "ProcurementObject", "Obiectul achiziției", @"II\.1\b", @"Obiectul achizitiei");
        Add("II.1.1", "ProcurementObjectTitle", "Titlu", @"II\.1\.1\b", @"Titlu");
        Add("II.1.2", "CpvCode", "Cod CPV principal", @"II\.1\.2\b", @"Cod CPV");
        Add("II.1.3", "ContractType", "Tip de contract", @"II\.1\.3\b", @"Tip de contract");
        Add("II.1.4", "ContractDescription", "Descrierea succintă a contractului", @"II\.1\.4\b", @"Descrierea succinta");
        Add("II.1.5", "EstimatedValue", "Valoarea totală estimată", @"II\.1\.5\)?", @"Valoarea totala estimata");
        Add("II.1.6", "Lots", "Împărțire pe loturi", @"II\.1\.6\)?", @"Impartire in loturi");
        Add("II.1.7", "AdditionalInfoTotal", "Informații suplimentare (total)", @"II\.1\.7\)?", @"Valoare|Informatii");
        Add("II.2", "LotHeader", "Descrierea lotului", @"II\.2\b", @"Descrierea lotului");
        Add("II.2.1", "LotTitle", "Titlu lot", @"II\.2\.1\b", @"Titlu");
        Add("II.2.2", "LotCpv", "Cod CPV lot", @"II\.2\.2\b", @"Cod CPV");
        Add("II.2.3", "LotExecutionPlace", "Locul de executare a lucrărilor", @"II\.2\.3\b", @"Locul de");
        Add("II.2.4", "LotDescription", "Descrierea achiziției publice (lot)", @"II\.2\.4\b", @"Descrierea achizitiei");
        Add("II.2.5", "AwardCriteria", "Criterii de atribuire", @"II\.2\.5\b", @"Criterii de atribuire");
        Add("II.2.6", "LotEstimatedValue", "Valoarea estimată (lot)", @"II\.2\.6\)?", @"Valoarea");
        Add("II.2.7", "ContractDuration", "Durata contractului / termenul de execuție", @"II\.2\.7\b", @"Durata contractului");
        Add("II.2.10", "LotVariants", "Informații privind variantele", @"II\.2\.10\b", @"variantele");
        Add("II.2.11", "LotOptions", "Informații privind opțiunile", @"II\.2\.11\b", @"optiunile");
        Add("II.2.12", "LotElectronicCatalogues", "Cataloage electronice", @"II\.2\.12\b", @"cataloage");
        Add("II.2.13", "LotEuFunds", "Fonduri ale Uniunii Europene", @"II\.2\.13\b", @"Uniunii");
        Add("II.2.14", "LotSupplementaryInfo", "Informații suplimentare (lot)", @"II\.2\.14\b", @"suplimentare");
        Add("II.3", "PriceAdjustment", "Ajustarea prețului contractului", @"II\.3\b", @"Ajustarea pretului");
        Add("III.1.5", "ReservedContracts", "Contracte rezervate", @"III\.1\.5\)?", @"contractele");
        Add("III.1.8", "GroupLegalForm", "Forma juridică a grupului", @"III\.1\.8\)?", @"Forma juridica");

        // III – conditions (may be absent in DFs that declare no conditions).
        Add("III.1", "ParticipationConditions", "Condiții de participare", @"III\.1\)?", @"CONDITII DE PARTICIPARE");
        Add("III.1.1", "PersonalSituationExclusionDuae", "Situația personală (motive de excludere / DUAE)", @"III\.1\.1\b", @"Situatia personala|a\)");
        Add("III.1.1.b", "ProfessionalActivityCapacity", "Capacitatea de exercitare a activității profesionale", @"III\.1\.1\s*b\)", @"Capacitatea de exercitare");
        Add("III.1.2", "EconomicFinancialCapacity", "Capacitatea economică și financiară", @"III\.1\.2\)?", @"Capacitatea economica");
        Add("III.1.3", "TechnicalProfessionalCapacity", "Capacitatea tehnică și/sau profesională", @"III\.1\.3\b", @"Capacitatea tehnica");
        Add("III.1.3.b", "QualityStandards", "Standarde de asigurare a calității și a mediului", @"III\.1\.3\s*b\)", @"Standarde");
        Add("III.1.6", "DepositsGuarantees", "Depozite valorice și garanții solicitate", @"III\.1\.6\)?", @"Depozite valorice");
        Add("III.1.6.a", "ParticipationGuarantee", "Garanția de participare", @"III\.1\.6\s*\.?a\)", @"Garantie de participare");
        Add("III.1.6.b", "PerformanceGuarantee", "Garanția de bună execuție", @"III\.1\.6\s*\.?b\)", @"Garantie de buna executie");
        Add("III.1.7", "FinancingPaymentConditions", "Condiții de finanțare și plată", @"III\.1\.7\)?", @"conditii|modalitati|finantare");
        Add("III.1.9", "ApplicableLegislation", "Legislația aplicabilă", @"III\.1\.9\)?", @"Legislatia");
        Add("III.2", "ContractConditions", "Condiții referitoare la contract", @"III\.2\)?", @"CONDITII REFERITOARE");
        Add("III.2.2", "SpecialContractExecutionConditions", "Executarea contractului / condiții speciale", @"III\.2\.2\)?", @"Executarea contractului");
        Add("III.2.3", "ContractExecutionPersonnel", "Personalul responsabil de executare a contractului", @"III\.2\.3\)?", @"personalul responsabil");

        // IV – procedure.
        Add("IV.1", "ProcedureDescription", "Descriere", @"IV\.1\b", @"Descriere");
        Add("IV.1.1", "ProcedureType", "Tipul procedurii", @"IV\.1\.1\b", @"Tipul procedurii");
        Add("IV.1.1.a", "AwardingMethod", "Modalitatea de atribuire", @"IV\.1\.1\s*a\)", @"Modalitatea");
        Add("IV.1.6", "ElectronicAuction", "Licitatie electronică", @"IV\.1\.6\b", @"licitatia electronica");
        Add("IV.2", "AdministrativeInfo", "Informații administrative", @"IV\.2\b", @"Informatii administrative");
        Add("IV.2.4", "OfferLanguages", "Limbile cererilor de participare", @"IV\.2\.4\b", @"Limbile");
        Add("IV.3", "AdministrativeInfo", "Informații administrative", @"IV\.3\b", @"administrative");
        Add("IV.3.2", "OfferSubmissionConditions", "Condiții de depunere a ofertelor", @"IV\.3\.2\b", @"Conditii de depunere");
        Add("IV.3.4", "OfferReceiptDeadline", "Termenul limită de primire a ofertelor", @"IV\.3\.4\b", @"Termenul limita");

        // VI – complementary.
        Add("VI.1", "RecurrentProcurement", "Achiziție periodică", @"VI\.1\b", @"periodica");
        Add("VI.2", "EWorkTools", "Instrumente de lucru electronice", @"VI\.2\b", @"electronice");
        Add("VI.4", "ContestationProcedure", "Proceduri de contestare", @"VI\.4\b", @"contestare");
        Add("VI.4.1", "ContestationBody", "Organismul de soluționare a contestațiilor", @"VI\.4\.1\b", @"Organismul");

        Add("IV.1.1.b", "ProcedureTypeDetail", "Tipul procedurii (detaliat)", @"IV\.1\.1\s*b\)", @"Tipul");
        Add("IV.1.3", "FrameworkAgreementDps", "Acord-cadru / sistem dinamic de achiziții", @"IV\.1\.3\b", @"acord-cadru");
        Add("IV.2.6", "OfferValidityPeriod", "Perioada minimă de valabilitate a ofertelor", @"IV\.2\.6\b", @"Perioada minima");
        Add("IV.4", "OfferPresentation", "Prezentarea ofertei", @"IV\.4\b", @"Prezentarea ofertei");
        Add("IV.4.1", "TechnicalProposalPresentation", "Modul de prezentare al propunerii tehnice", @"IV\.4\.1\b", @"Modul de prezentare al propunerii tehnice");
        Add("IV.4.2", "FinancialProposalPresentation", "Modul de prezentare al propunerii financiare", @"IV\.4\.2\b", @"Modul de prezentare al propunerii financiare");
        Add("IV.4.3", "OfferPresentationDetail", "Modul de prezentare al ofertei", @"IV\.4\.3\b", @"Modul de prezentare al ofertei");
        Add("VI.4.3", "ContestationProcedureDetail", "Procedura de contestare", @"VI\.4\.3\b", @"Procedura de contestare");
        Add("VI.4.4", "ContestationInfoService", "Serviciul de informații privind contestarea", @"VI\.4\.4\b", @"Serviciul");

        return rules;
    }
}


