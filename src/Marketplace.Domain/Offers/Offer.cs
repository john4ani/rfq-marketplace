namespace Marketplace.Domain.Offers;

public enum OfferStatus { Draft, Submitted, Revised, Withdrawn, Selected, NotSelected }
public enum OfferPricingMode { Itemized, Total }

public sealed class Offer
{
    private readonly List<OfferItem> _items = [];
    private Offer() { }
    public Guid Id { get; private set; }
    public Guid RfqId { get; private set; }
    public Guid SupplierId { get; private set; }
    public OfferStatus Status { get; private set; }
    public OfferPricingMode PricingMode { get; private set; }
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
    public decimal Total => PricingMode == OfferPricingMode.Total
        ? TotalPrice ?? 0
        : ItemsTotal + DeliveryCost;

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
        if (PricingMode == OfferPricingMode.Itemized && _items.Count == 0)
            throw new OfferRuleException("Adăugați cel puțin o poziție sau folosiți oferta cu preț total.");
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
        if (!draft.DeliveryIncluded && draft.DeliveryCost > 0)
            throw new OfferRuleException("Costul livrării poate fi completat doar când transportul este inclus.");
        if (draft.Items.Select(item => item.RfqItemId).Distinct().Count() != draft.Items.Count)
            throw new OfferRuleException("Aceeași poziție nu poate apărea de mai multe ori.");
        PricingMode = draft.PricingMode;
        TotalPrice = draft.PricingMode == OfferPricingMode.Total ? draft.TotalPrice : null;
        DeliveryIncluded = draft.DeliveryIncluded;
        DeliveryCost = draft.PricingMode == OfferPricingMode.Total || !draft.DeliveryIncluded ? 0 : draft.DeliveryCost;
        DeliveryDays = draft.DeliveryIncluded ? draft.DeliveryDays : 0;
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
    bool DeliveryIncluded = true);
public sealed record OfferItemDraft(Guid RfqItemId, decimal Quantity, decimal UnitPrice, string? ProductDescription, bool Available);
public sealed class OfferRuleException(string message) : InvalidOperationException(message);
