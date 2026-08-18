import { registerLocaleData } from '@angular/common';
import localeRo from '@angular/common/locales/ro';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
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
    buyerTrustFacts: null,
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

  it('uses one total price for a simple single-position request', async () => {
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [{ provide: OfferService, useValue: { getForRequest: () => of(null) } }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfferEditorComponent);
    fixture.componentRef.setInput('request', {
      ...itemizedRequest,
      id: 'rfq-pellets',
      title: '6 tone peleți',
      isPartialMatch: false,
      items: [{ ...itemizedRequest.items[0], productName: 'Peleți', quantity: 6, unit: 'Tonne' }]
    });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(fixture.componentInstance.draft().pricingMode).toBe('Total');
    expect(text).toContain('Preț total, inclusiv livrarea');
    expect(text).not.toContain('Preț pe fiecare poziție');
    expect(text).not.toContain('Cum vrei să prezinți prețul?');
  });

  it('uses total pricing when delivery is stored as a service line', async () => {
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [{ provide: OfferService, useValue: { getForRequest: () => of(null) } }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfferEditorComponent);
    fixture.componentRef.setInput('request', {
      ...itemizedRequest,
      id: 'rfq-pellets-delivery',
      isPartialMatch: false,
      items: [
        { ...itemizedRequest.items[0], productName: 'Pellets', quantity: 6, unit: 'Tonne', kind: 'Product' },
        { ...itemizedRequest.items[1], productName: 'Livrare la adresa', kind: 'Service' }
      ]
    });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(fixture.componentInstance.draft().pricingMode).toBe('Total');
    expect(text).not.toContain('PreÈ› pe fiecare poziÈ›ie');
    expect(text).not.toContain('Cum vrei sÄƒ prezinÈ›i preÈ›ul?');
  });

  it('offers a pricing choice for multiple commercial positions', async () => {
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [{ provide: OfferService, useValue: { getForRequest: () => of(null) } }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfferEditorComponent);
    fixture.componentRef.setInput('request', {
      ...itemizedRequest,
      id: 'rfq-two-products',
      isPartialMatch: false,
      items: [
        { ...itemizedRequest.items[0], productName: 'Ciment', kind: 'Product' },
        { ...itemizedRequest.items[1], productName: 'BCA', kind: 'Product' }
      ]
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.hasPricingModeChoice()).toBe(true);
  });

  it('preserves the total when exploring a partial offer and blocks an empty review', async () => {
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [{ provide: OfferService, useValue: { getForRequest: () => of(null) } }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfferEditorComponent);
    fixture.componentRef.setInput('request', {
      ...itemizedRequest,
      isPartialMatch: false,
      items: itemizedRequest.items.map(item => ({ ...item, kind: 'Product' as const }))
    });
    fixture.detectChanges();

    fixture.componentInstance.draft.update(value => ({ ...value, totalPrice: 2_100 }));
    fixture.componentInstance.startPartialOffer();
    fixture.componentInstance.setPricingMode('Total');
    expect(fixture.componentInstance.draft().totalPrice).toBe(2_100);

    fixture.componentInstance.draft.update(value => ({ ...value, totalPrice: null }));
    fixture.componentInstance.review();
    expect(fixture.componentInstance.confirmingSubmission()).toBe(false);
    expect(fixture.componentInstance.error()).toContain('prețul total');
  });

  it('normalizes a legacy general offer stored as delivery cost', async () => {
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [provideRouter([]), {
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

  it('shows localized units and a live subtotal for each quoted position', async () => {
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [{ provide: OfferService, useValue: { getForRequest: () => of(null) } }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfferEditorComponent);
    fixture.componentRef.setInput('request', {
      ...itemizedRequest,
      items: [{ ...itemizedRequest.items[0], productName: 'Cherestea', quantity: 2.4, unit: 'CubicMetre' }]
    });
    fixture.detectChanges();

    fixture.componentInstance.draft.update(value => ({
      ...value,
      items: [{ ...value.items[0], quantity: 2.4, unitPrice: 1_200 }]
    }));
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Cerut: 2.4 m³');
    expect(text).toContain('Valoare poziție: 2.880,00');
    expect(fixture.componentInstance.lineTotal(0)).toBe(2_880);
  });

  it('keeps saved prices associated by request item id when API line order differs', async () => {
    await TestBed.configureTestingModule({
      imports: [OfferEditorComponent],
      providers: [provideRouter([]), {
        provide: OfferService,
        useValue: {
          getForRequest: () => of({
            id: 'offer-reordered', rfqId: itemizedRequest.id, supplierId: 'supplier-1', status: 'Submitted',
            pricingMode: 'Itemized', coverage: 'Full', taxTreatment: 'VatIncluded', vatRate: 19,
            deliveryTerms: 'AddedToTotal', currency: 'RON', totalPrice: null, deliveryIncluded: true,
            deliveryCost: 0, deliveryDays: 2, validUntil: '2026-08-19', paymentTerms: null, notes: null,
            itemsTotal: 210, netTotal: 176.47, vatAmount: 33.53, total: 210, hasComparableTotal: true,
            createdAt: '2026-08-01T10:00:00.000Z', updatedAt: '2026-08-01T10:00:00.000Z',
            submittedAt: '2026-08-01T10:00:00.000Z',
            items: [
              { id: 'offer-item-2', rfqItemId: 'item-2', quantity: 1, unitPrice: 10, productDescription: 'Montaj', available: true, isAlternative: false, lineTotal: 10 },
              { id: 'offer-item-1', rfqItemId: 'item-1', quantity: 2, unitPrice: 100, productDescription: 'Ciment', available: true, isAlternative: false, lineTotal: 200 }
            ]
          })
        }
      }]
    }).compileComponents();
    const fixture = TestBed.createComponent(OfferEditorComponent);
    fixture.componentRef.setInput('request', itemizedRequest);
    fixture.detectChanges();

    expect(fixture.componentInstance.requestedItem('item-2')?.productName).toBe('Montaj');
    expect(fixture.componentInstance.coveredCount()).toBe(2);
    fixture.componentInstance.editSubmittedOffer();
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text.indexOf('Ciment')).toBeLessThan(text.indexOf('Montaj'));
    expect(text).not.toContain('Cantitate parțială');
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
