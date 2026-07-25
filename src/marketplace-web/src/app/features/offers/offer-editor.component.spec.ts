import { registerLocaleData } from '@angular/common';
import localeRo from '@angular/common/locales/ro';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { OfferService } from '../../core/offers/offer.service';
import { OpenRfq } from '../../core/rfqs/open-rfq.service';
import { OfferEditorComponent } from './offer-editor.component';

registerLocaleData(localeRo);

describe('OfferEditorComponent', () => {
  const generalRequest: OpenRfq = {
    id: 'rfq-general',
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
    fixture.componentInstance.setDeliveryIncluded(false);
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
});
