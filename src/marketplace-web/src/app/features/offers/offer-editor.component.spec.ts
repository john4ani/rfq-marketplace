import { registerLocaleData } from '@angular/common';
import localeRo from '@angular/common/locales/ro';
import { TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { OfferService } from '../../core/offers/offer.service';
import { OpenRfq } from '../../core/rfqs/open-rfq.service';
import { OfferEditorComponent } from './offer-editor.component';

registerLocaleData(localeRo);

describe('OfferEditorComponent', () => {
  const generalRequest: OpenRfq = {
    id: 'rfq-general',
    source: 'Manual',
    inputType: 'StructuredForm',
    needStatus: 'Published',
    projectContext: null,
    title: 'Cerere generală',
    projectName: null,
    description: 'Fără poziții individuale',
    primaryCategoryId: 'category-1',
    categoryName: 'Categorie',
    buyerDisplayName: 'Cumpărător',
    deliveryLocality: 'Iași',
    county: 'Iași',
    preferredDeliveryDate: null,
    quotationDeadline: '2026-08-01T10:00:00.000Z',
    alternativeProductsAccepted: true,
    deliveryRequired: true,
    estimatedBudget: 5_000,
    publishedAt: '2026-07-25T10:00:00.000Z',
    isPartialMatch: false,
    items: []
  };

  const itemizedRequest: OpenRfq = {
    ...generalRequest,
    id: 'rfq-itemized',
    title: 'Cerere cu poziții',
    deliveryRequired: false,
    isPartialMatch: true,
    items: [
      { id: 'item-1', itemNumber: 1, productName: 'Ciment', manufacturerOrBrand: null, specification: null, quantity: 2, unit: 'Bag', alternativesAccepted: true, buyerNotes: null, categoryId: null, kind: 'Product' },
      { id: 'item-2', itemNumber: 2, productName: 'Montaj', manufacturerOrBrand: null, specification: null, quantity: 1, unit: 'Other', alternativesAccepted: true, buyerNotes: null, categoryId: null, kind: 'Service' }
    ]
  };

  it('uses an all-inclusive total for a general request', async () => {
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [{ provide: OfferService, useValue: { getForRequest: () => of(null) } }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfferEditorComponent);
    fixture.componentRef.setInput('request', generalRequest);
    fixture.detectChanges();

    expect(fixture.componentInstance.draft().pricingMode).toBe('Total');
    expect(fixture.componentInstance.draft().deliveryIncluded).toBe(true);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Preț total, inclusiv livrarea');
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Cost livrare (lei)');
    fixture.componentInstance.setDeliveryTerms('BuyerPickup');
    expect(fixture.componentInstance.draft().deliveryIncluded).toBe(true);
  });

  it('normalizes a legacy general offer stored as delivery cost', async () => {
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [{
        provide: OfferService,
        useValue: {
          getForRequest: () => of({
            id: 'offer-1',
            rfqId: generalRequest.id,
            supplierId: 'supplier-1',
            status: 'Submitted',
            pricingMode: 'Itemized',
            currency: 'RON',
            totalPrice: null,
            deliveryIncluded: true,
            deliveryCost: 1_250,
            deliveryDays: 4,
            validUntil: '2026-08-01',
            paymentTerms: null,
            notes: null,
            itemsTotal: 0,
            total: 1_250,
            createdAt: '2026-07-25T10:00:00.000Z',
            updatedAt: '2026-07-25T10:00:00.000Z',
            submittedAt: '2026-07-25T10:00:00.000Z',
            items: []
          })
        }
      }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfferEditorComponent);
    fixture.componentRef.setInput('request', generalRequest);
    fixture.detectChanges();

    expect(fixture.componentInstance.draft().pricingMode).toBe('Total');
    expect(fixture.componentInstance.draft().totalPrice).toBe(1_250);
    expect(fixture.componentInstance.draft().deliveryCost).toBe(0);
  });

  it('excludes unavailable positions from the displayed total', async () => {
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [{ provide: OfferService, useValue: { getForRequest: () => of(null) } }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfferEditorComponent);
    fixture.componentRef.setInput('request', itemizedRequest);
    fixture.detectChanges();

    fixture.componentInstance.draft.update(value => ({
      ...value,
      deliveryCost: 30,
      items: [
        { ...value.items[0], quantity: 2, unitPrice: 100, available: true },
        { ...value.items[1], quantity: 1, unitPrice: 999, available: false }
      ]
    }));

    expect(fixture.componentInstance.total()).toBe(230);
    expect(fixture.componentInstance.coverage()).toBe('Partial');
  });

  it('shows the payable total when VAT is added', async () => {
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [{ provide: OfferService, useValue: { getForRequest: () => of(null) } }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfferEditorComponent);
    fixture.componentRef.setInput('request', generalRequest);
    fixture.detectChanges();

    fixture.componentInstance.draft.update(value => ({ ...value, totalPrice: 1_000, taxTreatment: 'VatExcluded', vatRate: 19 }));

    expect(fixture.componentInstance.netTotal()).toBe(1_000);
    expect(fixture.componentInstance.vatAmount()).toBe(190);
    expect(fixture.componentInstance.total()).toBe(1_190);
  });

  it('does not send the same save operation twice while it is pending', async () => {
    const pending = new Subject<never>();
    const save = vi.fn().mockReturnValue(pending);
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [{ provide: OfferService, useValue: { getForRequest: () => of(null), save } }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfferEditorComponent);
    fixture.componentRef.setInput('request', generalRequest);
    fixture.detectChanges();

    fixture.componentInstance.save();
    fixture.componentInstance.save();

    expect(save).toHaveBeenCalledTimes(1);
  });
});
