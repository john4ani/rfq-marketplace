namespace Marketplace.Domain.Rfqs;

public enum RfqStatus { Draft, Published, ReceivingOffers, OfferSelected, Closed, Cancelled }
public enum FulfillmentStatus { NotApplicable, AwaitingDelivery, DeliveryReported, Delivered }

public sealed class Rfq
{
    private readonly List<RfqItem> _items = [];
    private readonly List<NeedCategory> _needCategories = [];
    private Rfq() { }
    public Guid Id { get; private set; }
    public Guid BuyerId { get; private set; }
    public ProcurementNeedSource Source { get; private set; }
    public ProcurementInputType InputType { get; private set; }
    public ProcurementNeedType NeedType { get; private set; }
    public Guid? ProjectTemplateId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? ProjectName { get; private set; }
    public string? Description { get; private set; }
    public Guid PrimaryCategoryId => _needCategories.OrderBy(category => category.SortOrder).First().CategoryId;
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
    public bool DeliveryRequired { get; private set; }
    public decimal? EstimatedBudget { get; private set; }
    public string? BuyerNotes { get; private set; }
    public RfqStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public Guid? SelectedOfferId { get; private set; }
    public int ReopenCount { get; private set; }
    public FulfillmentStatus FulfillmentStatus { get; private set; }
    public DateTimeOffset? DeliveryReportedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public ProcurementOutcome? Outcome { get; private set; }
    public string? OutcomeReason { get; private set; }
    public DateTimeOffset? OutcomeRecordedAt { get; private set; }
    public IReadOnlyCollection<RfqItem> Items => _items;
    public IReadOnlyCollection<NeedCategory> NeedCategories => _needCategories;
    // A procurement need has one commercial category in MVP. The collection is
    // retained only for persistence compatibility with pre-v2 need records.
    public IReadOnlyCollection<Guid> RequiredCategoryIds => [PrimaryCategoryId];
    public ProcurementNeedStatus NeedStatus => Status switch
    {
        RfqStatus.Draft => ProcurementNeedStatus.Draft,
        RfqStatus.Published => ProcurementNeedStatus.Published,
        RfqStatus.ReceivingOffers => ProcurementNeedStatus.ReceivingResponses,
        RfqStatus.OfferSelected => ProcurementNeedStatus.Awarded,
        RfqStatus.Closed => ProcurementNeedStatus.Completed,
        RfqStatus.Cancelled => ProcurementNeedStatus.Cancelled,
        _ => throw new InvalidOperationException($"Unsupported request status: {Status}.")
    };
    public ProjectContext? ProjectContext
    {
        get
        {
            var context = new ProjectContext(ProjectName, Description);
            return context.IsEmpty ? null : context;
        }
    }
    public DeliveryConstraints DeliveryConstraints => new(
        DeliveryAddress, DeliveryLocality, County, SupplierSearchRadiusKm,
        Latitude, Longitude, PreferredDeliveryDate, DeliveryRequired);

    public static Rfq Create(Guid id, Guid buyerId, RfqDraft draft, DateTimeOffset now)
    {
        ValidateDraft(draft);
        var rfq = new Rfq
        {
            Id = id,
            BuyerId = buyerId,
            Source = draft.Source,
            InputType = draft.InputType,
            Status = RfqStatus.Draft,
            CreatedAt = now
        };
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
        if (Status is not (RfqStatus.Draft or RfqStatus.Published or RfqStatus.ReceivingOffers))
            throw new RfqRuleException("Cererea nu mai poate fi anulată după selectarea unei oferte.");
        Status = RfqStatus.Cancelled;
        ClosedAt = now;
        Outcome = ProcurementOutcome.Cancelled;
        OutcomeReason = null;
        OutcomeRecordedAt = now;
        UpdatedAt = now;
    }

    public void RecordOutcome(ProcurementOutcome outcome, string? reason, DateTimeOffset now)
    {
        if (!Enum.IsDefined(outcome))
            throw new RfqRuleException("Rezultatul selectat nu este valid.");
        if (Outcome.HasValue)
            throw new RfqRuleException("Rezultatul acestei cereri a fost deja înregistrat.");
        if (Status == RfqStatus.Draft)
            throw new RfqRuleException("Publică cererea înainte de a înregistra rezultatul achiziției.");
        if (Status == RfqStatus.Cancelled || outcome == ProcurementOutcome.Cancelled)
            throw new RfqRuleException("Pentru acest rezultat, folosește acțiunea de anulare a cererii.");
        if (outcome == ProcurementOutcome.PurchasedThroughCompaSpot && !SelectedOfferId.HasValue)
            throw new RfqRuleException("Selectează mai întâi oferta cumpărată prin CompaSpot.");
        if (Status == RfqStatus.OfferSelected && outcome != ProcurementOutcome.PurchasedThroughCompaSpot)
            throw new RfqRuleException("O ofertă este deja selectată. Redeschide cererea dacă achiziția nu s-a realizat prin CompaSpot.");
        if (Status == RfqStatus.OfferSelected && FulfillmentStatus is FulfillmentStatus.AwaitingDelivery or FulfillmentStatus.DeliveryReported)
            throw new RfqRuleException("Finalizează mai întâi confirmarea livrării pentru oferta selectată.");
        if (Status == RfqStatus.Closed)
            throw new RfqRuleException("Cererea este deja finalizată.");

        string? cleanReason = Clean(reason);
        if (outcome == ProcurementOutcome.Other && string.IsNullOrWhiteSpace(cleanReason))
            throw new RfqRuleException("Descrie pe scurt celălalt rezultat al cererii.");
        if (cleanReason?.Length > 1000)
            throw new RfqRuleException("Explicația rezultatului poate avea cel mult 1.000 de caractere.");

        Outcome = outcome;
        OutcomeReason = cleanReason;
        OutcomeRecordedAt = now;
        Status = RfqStatus.Closed;
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

    public void SelectOffer(Guid offerId, bool deliveryIncluded, DateTimeOffset now)
    {
        if (Status is not (RfqStatus.Published or RfqStatus.ReceivingOffers))
            throw new RfqRuleException("Nu se mai poate selecta o ofertă pentru această cerere.");
        if (DeliveryRequired && !deliveryIncluded)
            throw new RfqRuleException("Cererea solicită transport inclus. Selectați o ofertă care include livrarea.");
        SelectedOfferId = offerId;
        Status = RfqStatus.OfferSelected;
        FulfillmentStatus = deliveryIncluded ? FulfillmentStatus.AwaitingDelivery : FulfillmentStatus.NotApplicable;
        ClosedAt = now;
        UpdatedAt = now;
    }

    public void ReportDelivered(DateTimeOffset now)
    {
        if (Status != RfqStatus.OfferSelected || FulfillmentStatus != FulfillmentStatus.AwaitingDelivery)
            throw new RfqRuleException("Livrarea nu poate fi declarată în starea curentă.");
        FulfillmentStatus = FulfillmentStatus.DeliveryReported;
        DeliveryReportedAt = now;
        UpdatedAt = now;
    }

    public void ConfirmDelivery(DateTimeOffset now)
    {
        if (Status != RfqStatus.OfferSelected || FulfillmentStatus != FulfillmentStatus.DeliveryReported)
            throw new RfqRuleException("Livrarea poate fi confirmată doar după ce furnizorul o declară efectuată.");
        FulfillmentStatus = FulfillmentStatus.Delivered;
        Status = RfqStatus.Closed;
        DeliveredAt = now;
        ClosedAt = now;
        Outcome = ProcurementOutcome.PurchasedThroughCompaSpot;
        OutcomeReason = null;
        OutcomeRecordedAt = now;
        UpdatedAt = now;
    }

    public void Reopen(string reason, DateTimeOffset? newQuotationDeadline, DateTimeOffset now)
    {
        if (Status != RfqStatus.OfferSelected)
            throw new RfqRuleException("Doar o cerere cu ofertă selectată poate fi redeschisă.");
        if (ReopenCount >= 1)
            throw new RfqRuleException("Cererea a fost deja redeschisă o dată și nu mai poate fi redeschisă.");
        if (FulfillmentStatus is FulfillmentStatus.DeliveryReported or FulfillmentStatus.Delivered)
            throw new RfqRuleException("Cererea nu mai poate fi redeschisă după declararea livrării.");
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
        FulfillmentStatus = FulfillmentStatus.NotApplicable;
        DeliveryReportedAt = null;
        DeliveredAt = null;
        ClosedAt = null;
        Outcome = null;
        OutcomeReason = null;
        OutcomeRecordedAt = null;
        ReopenCount++;
        UpdatedAt = now;
    }

    private void Apply(RfqDraft draft, DateTimeOffset now)
    {
        Title = draft.Title.Trim();
        ProjectName = Clean(draft.ProjectName);
        Description = Clean(draft.Description);
        NeedType = draft.NeedType ?? draft.InputType switch
        {
            ProcurementInputType.ShoppingList => ProcurementNeedType.ShoppingList,
            ProcurementInputType.ProjectDescription => ProcurementNeedType.Project,
            _ => ProcurementNeedType.DirectPurchase
        };
        // A template is explicit Construction compatibility state. NeedType.Project
        // remains a generic procurement shape and must not decide Construction mode.
        ProjectTemplateId = draft.ProjectTemplateId;
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
        DeliveryRequired = draft.DeliveryRequired;
        EstimatedBudget = draft.EstimatedBudget;
        BuyerNotes = Clean(draft.BuyerNotes);
        Guid primaryCategoryId = ResolvePrimaryCategoryId(draft);
        NeedCategory? existingCategory = _needCategories.OrderBy(category => category.SortOrder).FirstOrDefault();
        _needCategories.Clear();
        NeedCategory needCategory = existingCategory is not null && existingCategory.CategoryId == primaryCategoryId
            ? existingCategory
            : NeedCategory.Create(Id, primaryCategoryId, 0);
        needCategory.Reorder(0);
        _needCategories.Add(needCategory);

        RfqItem[] existingItems = _items.OrderBy(item => item.ItemNumber).ToArray();
        _items.Clear();
        int itemNumber = 1;
        foreach (RfqItemDraft item in draft.Items)
        {
            if (itemNumber <= existingItems.Length)
            {
                RfqItem existingItem = existingItems[itemNumber - 1];
                existingItem.Update(itemNumber++, item, needCategory.Id);
                _items.Add(existingItem);
            }
            else
            {
                _items.Add(RfqItem.Create(Guid.NewGuid(), Id, itemNumber++, item, needCategory.Id));
            }
        }
        UpdatedAt = now;
    }

    private static void ValidateDraft(RfqDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Title)) throw new RfqRuleException("Titlul cererii este obligatoriu.");
        if (draft.Title.Trim().Length > 180) throw new RfqRuleException("Titlul poate avea cel mult 180 de caractere.");
        if (ResolvePrimaryCategoryId(draft) == Guid.Empty) throw new RfqRuleException("Selectați categoria principală a necesarului.");
        if (string.IsNullOrWhiteSpace(draft.DeliveryAddress) || string.IsNullOrWhiteSpace(draft.DeliveryLocality) || string.IsNullOrWhiteSpace(draft.County))
            throw new RfqRuleException("Adresa, localitatea și județul de livrare sunt obligatorii.");
        if (draft.SupplierSearchRadiusKm is < 1 or > 1000) throw new RfqRuleException("Raza de căutare a furnizorilor trebuie să fie între 1 și 1.000 km.");
        if (draft.EstimatedBudget < 0) throw new RfqRuleException("Bugetul estimat nu poate fi negativ.");
        if (!Enum.IsDefined(draft.Source)) throw new RfqRuleException("Sursa cererii nu este validă.");
        if (!Enum.IsDefined(draft.InputType)) throw new RfqRuleException("Tipul de intrare al cererii nu este valid.");
        if (draft.NeedType.HasValue && !Enum.IsDefined(draft.NeedType.Value))
            throw new RfqRuleException("Tipul necesarului nu este valid.");
    }

    private static Guid ResolvePrimaryCategoryId(RfqDraft draft) =>
        draft.PrimaryCategoryId != Guid.Empty
            ? draft.PrimaryCategoryId
            : (draft.CategoryIds ?? []).FirstOrDefault(categoryId => categoryId != Guid.Empty);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record RfqDraft(
    string Title, string? ProjectName, string? Description, Guid PrimaryCategoryId,
    string DeliveryAddress, string DeliveryLocality, string County, int SupplierSearchRadiusKm, decimal? Latitude, decimal? Longitude,
    DateOnly? PreferredDeliveryDate, DateTimeOffset QuotationDeadline, bool AlternativeProductsAccepted,
    decimal? EstimatedBudget, string? BuyerNotes, IReadOnlyCollection<RfqItemDraft> Items, bool AllowSplit = false,
    bool DeliveryRequired = false, ProcurementNeedSource Source = ProcurementNeedSource.Manual,
    ProcurementInputType InputType = ProcurementInputType.StructuredForm,
    IReadOnlyCollection<Guid>? CategoryIds = null,
    ProcurementNeedType? NeedType = null,
    Guid? ProjectTemplateId = null);

public sealed class RfqRuleException(string message) : InvalidOperationException(message);
public sealed class RfqConcurrencyException(string message, Exception innerException) : Exception(message, innerException);
