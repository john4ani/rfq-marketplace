namespace Marketplace.Domain.Rfqs;

public enum RfqStatus { Draft, Published, ReceivingOffers, OfferSelected, Closed, Cancelled }

public sealed class Rfq
{
    private readonly List<RfqItem> _items = [];
    private Rfq() { }
    public Guid Id { get; private set; }
    public Guid BuyerId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? ProjectName { get; private set; }
    public string? Description { get; private set; }
    public Guid PrimaryCategoryId { get; private set; }
    public string DeliveryAddress { get; private set; } = string.Empty;
    public string DeliveryLocality { get; private set; } = string.Empty;
    public string County { get; private set; } = string.Empty;
    public int SupplierSearchRadiusKm { get; private set; }
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public DateOnly? PreferredDeliveryDate { get; private set; }
    public DateTimeOffset QuotationDeadline { get; private set; }
    public bool AlternativeProductsAccepted { get; private set; }
    public bool AllowSplit { get; private set; }
    public decimal? EstimatedBudget { get; private set; }
    public string? BuyerNotes { get; private set; }
    public RfqStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? SelectedOfferId { get; private set; }
    public int ReopenCount { get; private set; }
    public IReadOnlyCollection<RfqItem> Items => _items;

    public static Rfq Create(Guid id, Guid buyerId, RfqDraft draft, DateTimeOffset now)
    {
        ValidateDraft(draft);
        var rfq = new Rfq { Id = id, BuyerId = buyerId, Status = RfqStatus.Draft, CreatedAt = now };
        rfq.Apply(draft, now);
        return rfq;
    }

    public void Update(RfqDraft draft, DateTimeOffset now)
    {
        if (Status != RfqStatus.Draft) throw new RfqRuleException("Doar o cerere în starea Ciornă poate fi modificată.");
        ValidateDraft(draft);
        Apply(draft, now);
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status != RfqStatus.Draft) throw new RfqRuleException("Doar o cerere în starea Ciornă poate fi publicată.");
        if (QuotationDeadline <= now) throw new RfqRuleException("Termenul pentru oferte trebuie să fie în viitor.");
        Status = RfqStatus.Published;
        PublishedAt = now;
        UpdatedAt = now;
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == RfqStatus.Cancelled) throw new RfqRuleException("Cererea este deja anulată.");
        Status = RfqStatus.Cancelled;
        ClosedAt = now;
        UpdatedAt = now;
    }

    public void ExtendQuotationDeadline(DateTimeOffset newDeadline, DateTimeOffset now)
    {
        if (Status is not (RfqStatus.Published or RfqStatus.ReceivingOffers))
            throw new RfqRuleException("Termenul poate fi prelungit doar pentru o cerere care primește oferte.");
        if (newDeadline <= now)
            throw new RfqRuleException("Noul termen pentru oferte trebuie să fie în viitor.");
        if (newDeadline <= QuotationDeadline)
            throw new RfqRuleException("Noul termen trebuie să fie ulterior termenului curent.");

        QuotationDeadline = newDeadline;
        UpdatedAt = now;
    }

    public void MarkOfferReceived(DateTimeOffset now)
    {
        if (Status is not (RfqStatus.Published or RfqStatus.ReceivingOffers))
            throw new RfqRuleException("Cererea nu mai primește oferte.");
        if (QuotationDeadline <= now) throw new RfqRuleException("Termenul pentru oferte a expirat.");
        Status = RfqStatus.ReceivingOffers;
        UpdatedAt = now;
    }

    public void SelectOffer(Guid offerId, DateTimeOffset now)
    {
        if (Status is not (RfqStatus.Published or RfqStatus.ReceivingOffers))
            throw new RfqRuleException("Nu se mai poate selecta o ofertă pentru această cerere.");
        SelectedOfferId = offerId;
        Status = RfqStatus.OfferSelected;
        ClosedAt = now;
        UpdatedAt = now;
    }

    public void Reopen(string reason, DateTimeOffset? newQuotationDeadline, DateTimeOffset now)
    {
        if (Status != RfqStatus.OfferSelected)
            throw new RfqRuleException("Doar o cerere cu ofertă selectată poate fi redeschisă.");
        if (ReopenCount >= 1)
            throw new RfqRuleException("Cererea a fost deja redeschisă o dată și nu mai poate fi redeschisă.");
        string cleanReason = reason?.Trim() ?? string.Empty;
        if (cleanReason.Length < 10)
            throw new RfqRuleException("Motivul redeschiderii trebuie să aibă cel puțin 10 caractere.");
        if (cleanReason.Length > 500)
            throw new RfqRuleException("Motivul redeschiderii poate avea cel mult 500 de caractere.");
        DateTimeOffset deadline = newQuotationDeadline ?? QuotationDeadline;
        if (deadline <= now)
            throw new RfqRuleException("Pentru a redeschide cererea, stabilește un termen nou în viitor.");

        QuotationDeadline = deadline;
        SelectedOfferId = null;
        Status = RfqStatus.ReceivingOffers;
        ClosedAt = null;
        ReopenCount++;
        UpdatedAt = now;
    }

    private void Apply(RfqDraft draft, DateTimeOffset now)
    {
        Title = draft.Title.Trim();
        ProjectName = Clean(draft.ProjectName);
        Description = Clean(draft.Description);
        PrimaryCategoryId = draft.PrimaryCategoryId;
        DeliveryAddress = draft.DeliveryAddress.Trim();
        DeliveryLocality = draft.DeliveryLocality.Trim();
        County = draft.County.Trim();
        SupplierSearchRadiusKm = draft.SupplierSearchRadiusKm;
        Latitude = draft.Latitude;
        Longitude = draft.Longitude;
        PreferredDeliveryDate = draft.PreferredDeliveryDate;
        QuotationDeadline = draft.QuotationDeadline;
        AlternativeProductsAccepted = draft.AlternativeProductsAccepted;
        AllowSplit = draft.AllowSplit;
        EstimatedBudget = draft.EstimatedBudget;
        BuyerNotes = Clean(draft.BuyerNotes);
        RfqItem[] existingItems = _items.OrderBy(item => item.ItemNumber).ToArray();
        _items.Clear();
        int itemNumber = 1;
        foreach (RfqItemDraft item in draft.Items)
        {
            if (itemNumber <= existingItems.Length)
            {
                RfqItem existingItem = existingItems[itemNumber - 1];
                existingItem.Update(itemNumber++, item);
                _items.Add(existingItem);
            }
            else
            {
                _items.Add(RfqItem.Create(Guid.NewGuid(), Id, itemNumber++, item));
            }
        }
        UpdatedAt = now;
    }

    private static void ValidateDraft(RfqDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Title)) throw new RfqRuleException("Titlul cererii este obligatoriu.");
        if (draft.Title.Trim().Length > 180) throw new RfqRuleException("Titlul poate avea cel mult 180 de caractere.");
        if (draft.PrimaryCategoryId == Guid.Empty) throw new RfqRuleException("Selectați categoria principală.");
        if (string.IsNullOrWhiteSpace(draft.DeliveryAddress) || string.IsNullOrWhiteSpace(draft.DeliveryLocality) || string.IsNullOrWhiteSpace(draft.County))
            throw new RfqRuleException("Adresa, localitatea și județul de livrare sunt obligatorii.");
        if (draft.SupplierSearchRadiusKm is < 1 or > 1000) throw new RfqRuleException("Raza de căutare a furnizorilor trebuie să fie între 1 și 1.000 km.");
        if (draft.EstimatedBudget < 0) throw new RfqRuleException("Bugetul estimat nu poate fi negativ.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record RfqDraft(
    string Title, string? ProjectName, string? Description, Guid PrimaryCategoryId,
    string DeliveryAddress, string DeliveryLocality, string County, int SupplierSearchRadiusKm, decimal? Latitude, decimal? Longitude,
    DateOnly? PreferredDeliveryDate, DateTimeOffset QuotationDeadline, bool AlternativeProductsAccepted,
    decimal? EstimatedBudget, string? BuyerNotes, IReadOnlyCollection<RfqItemDraft> Items, bool AllowSplit = false);

public sealed class RfqRuleException(string message) : InvalidOperationException(message);
