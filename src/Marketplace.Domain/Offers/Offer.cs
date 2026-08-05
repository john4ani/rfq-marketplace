namespace Marketplace.Domain.Offers;

public enum OfferStatus { Draft, Submitted, Revised, Withdrawn, Selected, NotSelected }
public enum OfferPricingMode { Itemized, Total }
public enum OfferCoverage { Full, Partial }
public enum OfferTaxTreatment { NotSpecified, VatIncluded, VatExcluded, NotApplicable }
public enum OfferDeliveryTerms { IncludedInPrice, AddedToTotal, BuyerPickup, ArrangedSeparately }

public sealed class Offer
{
    private readonly List<OfferItem> _items = [];
    private Offer() { }
    public Guid Id { get; private set; }
    public Guid RfqId { get; private set; }
    public Guid SupplierId { get; private set; }
    public OfferStatus Status { get; private set; }
    public OfferPricingMode PricingMode { get; private set; }
    public OfferCoverage Coverage { get; private set; }
    public OfferTaxTreatment TaxTreatment { get; private set; }
    public decimal? VatRate { get; private set; }
    public OfferDeliveryTerms DeliveryTerms { get; private set; }
    public string Currency { get; private set; } = "RON";
    public decimal? TotalPrice { get; private set; }
    public bool DeliveryIncluded { get; private set; }
    public decimal DeliveryCost { get; private set; }
    public int DeliveryDays { get; private set; }
    public DateOnly ValidUntil { get; private set; }
    public string? PaymentTerms { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public int RevisionNumber { get; private set; }
    public IReadOnlyCollection<OfferItem> Items => _items;
    public decimal ItemsTotal => _items.Where(item => item.Available).Sum(item => item.Quantity * item.UnitPrice);
    public decimal QuotedSubtotal => PricingMode == OfferPricingMode.Total
        ? TotalPrice ?? 0
        : ItemsTotal + (DeliveryTerms == OfferDeliveryTerms.AddedToTotal ? DeliveryCost : 0);
    public decimal VatAmount => TaxTreatment switch
    {
        OfferTaxTreatment.VatExcluded => decimal.Round(QuotedSubtotal * (VatRate ?? 0) / 100, 2),
        OfferTaxTreatment.VatIncluded => decimal.Round(QuotedSubtotal - QuotedSubtotal / (1 + (VatRate ?? 0) / 100), 2),
        _ => 0
    };
    public decimal NetTotal => TaxTreatment == OfferTaxTreatment.VatIncluded ? QuotedSubtotal - VatAmount : QuotedSubtotal;
    public decimal Total => TaxTreatment == OfferTaxTreatment.VatExcluded ? QuotedSubtotal + VatAmount : QuotedSubtotal;
    public bool HasComparableTotal => TaxTreatment != OfferTaxTreatment.NotSpecified
        && DeliveryTerms != OfferDeliveryTerms.ArrangedSeparately;

    public static Offer Create(Guid id, Guid rfqId, Guid supplierId, OfferDraft draft, DateTimeOffset now)
    {
        var offer = new Offer { Id = id, RfqId = rfqId, SupplierId = supplierId, Status = OfferStatus.Draft, CreatedAt = now };
        offer.Apply(draft, now);
        return offer;
    }
    public void Update(OfferDraft draft, DateTimeOffset now)
    {
        if (Status is OfferStatus.Withdrawn or OfferStatus.Selected or OfferStatus.NotSelected)
            throw new OfferRuleException("Oferta nu mai poate fi modificată.");
        Apply(draft, now);
    }
    public void Submit(DateTimeOffset now)
    {
        if (Status is not (OfferStatus.Draft or OfferStatus.Submitted or OfferStatus.Revised))
            throw new OfferRuleException("Oferta nu poate fi trimisă în starea curentă.");
        if (RevisionNumber > 0 && SubmittedAt.HasValue && UpdatedAt <= SubmittedAt)
            throw new OfferRuleException("Oferta nu are modificări noi de trimis.");
        if (PricingMode == OfferPricingMode.Total && TotalPrice is null or <= 0)
            throw new OfferRuleException("Introduceți valoarea totală a ofertei.");
        if (Coverage == OfferCoverage.Partial && PricingMode == OfferPricingMode.Total)
            throw new OfferRuleException("O ofertă parțială trebuie detaliată pe poziții.");
        if (PricingMode == OfferPricingMode.Itemized && _items.Count == 0)
            throw new OfferRuleException("Adăugați cel puțin o poziție sau folosiți oferta cu preț total.");
        if (TaxTreatment == OfferTaxTreatment.NotSpecified)
            throw new OfferRuleException("Selectați modul de prezentare a TVA-ului.");
        if (ValidUntil < DateOnly.FromDateTime(now.UtcDateTime))
            throw new OfferRuleException("Valabilitatea ofertei nu poate fi în trecut.");
        Status = SubmittedAt.HasValue ? OfferStatus.Revised : OfferStatus.Submitted;
        RevisionNumber++;
        SubmittedAt = now;
        UpdatedAt = now;
    }
    public void Withdraw(DateTimeOffset now)
    {
        if (Status is not (OfferStatus.Submitted or OfferStatus.Revised))
            throw new OfferRuleException("Doar o ofertă trimisă poate fi retrasă.");
        Status = OfferStatus.Withdrawn; UpdatedAt = now;
    }
    public void Select(DateTimeOffset now) { Status = OfferStatus.Selected; UpdatedAt = now; }
    public void Reject(DateTimeOffset now)
    {
        if (Status is OfferStatus.Submitted or OfferStatus.Revised) { Status = OfferStatus.NotSelected; UpdatedAt = now; }
    }
    public void RestoreAfterRequestReopened(DateTimeOffset now)
    {
        if (Status is not (OfferStatus.Selected or OfferStatus.NotSelected)) return;
        Status = RevisionNumber > 1 ? OfferStatus.Revised : OfferStatus.Submitted;
        UpdatedAt = now;
    }
    private void Apply(OfferDraft draft, DateTimeOffset now)
    {
        if (draft.DeliveryCost < 0) throw new OfferRuleException("Costul livrării nu poate fi negativ.");
        if (draft.TotalPrice < 0) throw new OfferRuleException("Valoarea totală a ofertei nu poate fi negativă.");
        if (draft.DeliveryDays < 0 || draft.DeliveryDays > 365) throw new OfferRuleException("Termenul de livrare trebuie să fie între 0 și 365 de zile.");
        if (draft.DeliveryTerms is null && !draft.DeliveryIncluded && draft.DeliveryCost > 0)
            throw new OfferRuleException("Costul livrării poate fi completat doar când transportul este inclus.");
        if (draft.TaxTreatment is OfferTaxTreatment.VatIncluded or OfferTaxTreatment.VatExcluded
            && (draft.VatRate is null or < 0 or > 100))
            throw new OfferRuleException("Cota TVA trebuie să fie între 0 și 100%.");
        if (draft.TaxTreatment == OfferTaxTreatment.NotApplicable && draft.VatRate is not null)
            throw new OfferRuleException("Nu completați cota TVA când TVA-ul nu se aplică.");
        OfferDeliveryTerms deliveryTerms = draft.DeliveryTerms ?? (draft.DeliveryIncluded
            ? draft.PricingMode == OfferPricingMode.Total ? OfferDeliveryTerms.IncludedInPrice : OfferDeliveryTerms.AddedToTotal
            : OfferDeliveryTerms.BuyerPickup);
        if (deliveryTerms != OfferDeliveryTerms.AddedToTotal && draft.DeliveryCost > 0)
            throw new OfferRuleException("Costul transportului se completează doar când este adăugat separat la total.");
        if (draft.Items.Select(item => item.RfqItemId).Distinct().Count() != draft.Items.Count)
            throw new OfferRuleException("Aceeași poziție nu poate apărea de mai multe ori.");
        PricingMode = draft.PricingMode;
        Coverage = draft.Coverage;
        TaxTreatment = draft.TaxTreatment;
        VatRate = draft.TaxTreatment is OfferTaxTreatment.VatIncluded or OfferTaxTreatment.VatExcluded ? draft.VatRate : null;
        DeliveryTerms = deliveryTerms;
        TotalPrice = draft.PricingMode == OfferPricingMode.Total ? draft.TotalPrice : null;
        DeliveryIncluded = deliveryTerms is OfferDeliveryTerms.IncludedInPrice or OfferDeliveryTerms.AddedToTotal;
        DeliveryCost = deliveryTerms == OfferDeliveryTerms.AddedToTotal ? draft.DeliveryCost : 0;
        DeliveryDays = deliveryTerms != OfferDeliveryTerms.ArrangedSeparately ? draft.DeliveryDays : 0;
        ValidUntil = draft.ValidUntil;
        PaymentTerms = Clean(draft.PaymentTerms); Notes = Clean(draft.Notes);
        Dictionary<Guid, OfferItem> existing = _items.ToDictionary(item => item.RfqItemId);
        _items.Clear();
        foreach (OfferItemDraft item in draft.PricingMode == OfferPricingMode.Total ? [] : draft.Items)
        {
            if (existing.TryGetValue(item.RfqItemId, out OfferItem? current))
            {
                current.Update(item);
                _items.Add(current);
            }
            else _items.Add(OfferItem.Create(Guid.NewGuid(), Id, item));
        }
        UpdatedAt = now;
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record OfferDraft(
    OfferPricingMode PricingMode,
    decimal? TotalPrice,
    decimal DeliveryCost,
    int DeliveryDays,
    DateOnly ValidUntil,
    string? PaymentTerms,
    string? Notes,
    IReadOnlyCollection<OfferItemDraft> Items,
    bool DeliveryIncluded = true,
    OfferCoverage Coverage = OfferCoverage.Full,
    OfferTaxTreatment TaxTreatment = OfferTaxTreatment.NotSpecified,
    decimal? VatRate = null,
    OfferDeliveryTerms? DeliveryTerms = null);
public sealed record OfferItemDraft(Guid RfqItemId, decimal Quantity, decimal UnitPrice, string? ProductDescription, bool Available, bool IsAlternative = false);
public sealed class OfferRuleException(string message) : InvalidOperationException(message);
