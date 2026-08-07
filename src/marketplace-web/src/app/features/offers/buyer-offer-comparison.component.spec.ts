import { registerLocaleData } from '@angular/common';
import localeRo from '@angular/common/locales/ro';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { OfferService } from '../../core/offers/offer.service';
import { Rfq } from '../../core/rfqs/rfq.service';
import { RfqService } from '../../core/rfqs/rfq.service';
import { BuyerOfferComparisonComponent } from './buyer-offer-comparison.component';

registerLocaleData(localeRo);

describe('BuyerOfferComparisonComponent', () => {
  const request: Rfq = {
    categoryIds:['category-1'],needType:'DirectPurchase',projectTemplateId:null,
    id:'rfq-1',buyerId:'buyer-1',title:'Materiale renovare',projectName:'Apartament',
    source:'Manual',inputType:'StructuredForm',needStatus:'Published',
    projectContext:{name:'Apartament',description:'Necesar complet'},
    deliveryConstraints:{address:'Strada Test 1',locality:'Iași',county:'Iași',supplierSearchRadiusKm:30,latitude:null,longitude:null,preferredDeliveryDate:null,transportRequired:true},
    outcome:null,outcomeReason:null,outcomeRecordedAt:null,
    description:'Necesar complet',primaryCategoryId:'category-1',deliveryAddress:'Strada Test 1',
    deliveryLocality:'Iași',county:'Iași',supplierSearchRadiusKm:30,latitude:null,longitude:null,
    preferredDeliveryDate:null,quotationDeadline:'2026-08-01T10:00:00.000Z',
    alternativeProductsAccepted:true,estimatedBudget:5000,buyerNotes:null,allowSplit:false,
    status:'Published',createdAt:'2026-07-24T10:00:00.000Z',updatedAt:'2026-07-24T10:00:00.000Z',
    publishedAt:'2026-07-24T10:00:00.000Z',closedAt:null,selectedOfferId:null,reopenCount:0,
    deliveryRequired:true,fulfillmentStatus:'NotApplicable',deliveryReportedAt:null,deliveredAt:null,
    items:[{id:'item-1',itemNumber:1,productName:'Ciment',manufacturerOrBrand:null,
      specification:'40 kg',quantity:20,unit:'Bag',alternativesAccepted:true,buyerNotes:null,categoryId:null,kind:'Product'}]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports:[BuyerOfferComparisonComponent],
      providers:[
        {provide:ActivatedRoute,useValue:{snapshot:{paramMap:{get:()=>request.id}}}},
        {provide:RfqService,useValue:{
          get:()=>of(request),
          getHistory:()=>of([]),
          extendDeadline:()=>of({...request,quotationDeadline:'2026-08-02T10:00:00.000Z'}),
          cancel:()=>of({...request,status:'Cancelled'}),
          recordOutcome:()=>of({...request,status:'Closed',outcome:'PurchasedElsewhere'})
        }},
        {provide:OfferService,useValue:{forBuyer:()=>of([])}}
      ]
    }).compileComponents();
  });

  it('shows request context and management actions even without offers', () => {
    const fixture=TestBed.createComponent(BuyerOfferComparisonComponent);
    fixture.detectChanges();
    const page=fixture.nativeElement as HTMLElement;

    expect(page.querySelector('h1')?.textContent).toContain('Materiale renovare');
    expect(page.querySelector('.request-facts')?.textContent).toContain('Iași');
    expect(page.querySelector('.request-details summary')?.textContent).toContain('1 poziție');
    expect(page.querySelector('.empty')?.textContent).toContain('Nu ai primit încă oferte');
    expect(Array.from(page.querySelectorAll('button')).map(button=>button.textContent)).toEqual(
      expect.arrayContaining([expect.stringContaining('Prelungește termenul'),expect.stringContaining('Anulează cererea')]));
  });

  it('opens the deadline management form', () => {
    const fixture=TestBed.createComponent(BuyerOfferComparisonComponent);
    fixture.detectChanges();

    (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('.request-actions .secondary')?.click();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('.management-form')?.textContent)
      .toContain('Prelungește termenul pentru oferte');
  });

  it('asks for the real outcome after the quotation deadline', () => {
    const fixture=TestBed.createComponent(BuyerOfferComparisonComponent);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('app-procurement-outcome form')?.textContent)
      .toContain('Cum s-a încheiat achiziția?');
  });

  it('integrates purchase confirmation into the selected-offer decision', async () => {
    TestBed.overrideProvider(RfqService, { useValue: {
      get: () => of({
        ...request,
        status: 'OfferSelected',
        selectedOfferId: 'offer-1',
        fulfillmentStatus: 'NotApplicable'
      }),
      getHistory: () => of([]),
      recordOutcome: () => of({
        ...request,
        status: 'Closed',
        selectedOfferId: 'offer-1',
        fulfillmentStatus: 'NotApplicable',
        outcome: 'PurchasedThroughCompaSpot'
      })
    }});
    const fixture=TestBed.createComponent(BuyerOfferComparisonComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const page=fixture.nativeElement as HTMLElement;
    expect(page.querySelector('.decision')?.textContent).toContain('Oferta a fost acceptată.');
    expect(page.querySelector('.decision')?.textContent).toContain('Am cumpărat prin această ofertă');
    expect(page.querySelector('app-procurement-outcome')).toBeNull();
    expect(page.textContent).not.toContain('Închide bucla');

    page.querySelector<HTMLButtonElement>('.decision .confirm-selection button')?.click();
    fixture.detectChanges();
    expect(page.querySelector('.decision')?.textContent)
      .toContain('Achiziția prin oferta acceptată a fost înregistrată.');
  });

  it('hides reopening after the provider reports delivery', async () => {
    TestBed.overrideProvider(RfqService, { useValue: {
      get: () => of({
        ...request,
        status: 'OfferSelected',
        selectedOfferId: 'offer-1',
        fulfillmentStatus: 'DeliveryReported'
      }),
      getHistory: () => of([])
    }});
    const fixture=TestBed.createComponent(BuyerOfferComparisonComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const page=(fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(page).toContain('Furnizorul a declarat livrarea');
    expect(page).not.toContain('Redeschide cererea');
  });

  it('does not present a legacy general-offer total as delivery cost', async () => {
    const generalRequest = { ...request, items: [] };
    TestBed.overrideProvider(RfqService, { useValue: { get: () => of(generalRequest), getHistory: () => of([]) } });
    TestBed.overrideProvider(OfferService, { useValue: {
      forBuyer: () => of([{
        supplierName: 'Furnizor Test',
        offer: {
          id:'offer-1',rfqId:request.id,supplierId:'supplier-1',status:'Submitted',
          pricingMode:'Itemized',coverage:'Full',taxTreatment:'NotSpecified',vatRate:null,deliveryTerms:'AddedToTotal',
          currency:'RON',totalPrice:null,deliveryIncluded:true,deliveryCost:1250,
          deliveryDays:4,validUntil:'2026-08-01',paymentTerms:null,notes:null,
          itemsTotal:0,netTotal:1250,vatAmount:0,total:1250,hasComparableTotal:false,createdAt:'2026-07-25T10:00:00.000Z',
          updatedAt:'2026-07-25T10:00:00.000Z',submittedAt:'2026-07-25T10:00:00.000Z',items:[]
        }, coverageDetails:{requestedPositionCount:0,coveredPositionCount:0,missingRfqItemIds:[]}
      }])
    }});
    const fixture=TestBed.createComponent(BuyerOfferComparisonComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const page=(fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(page).toContain('Preț total · întreaga cerere');
    expect(page).not.toContain('Cost livrare');
  });

  it('explains coverage and compares each requested position without declaring a cheapest winner', async () => {
    const baseOffer = {
      id:'offer-1',rfqId:request.id,supplierId:'supplier-1',status:'Submitted',pricingMode:'Itemized',
      coverage:'Full',taxTreatment:'VatIncluded',vatRate:19,deliveryTerms:'AddedToTotal',currency:'RON',
      totalPrice:null,deliveryIncluded:true,deliveryCost:50,deliveryDays:3,validUntil:'2026-08-01',
      paymentTerms:null,notes:null,itemsTotal:1800,netTotal:1554.62,vatAmount:295.38,total:1850,
      hasComparableTotal:true,createdAt:'2026-07-25T10:00:00.000Z',updatedAt:'2026-07-25T10:00:00.000Z',
      submittedAt:'2026-07-25T10:00:00.000Z',items:[{id:'line-1',rfqItemId:'item-1',quantity:20,unitPrice:90,
        productDescription:'Ciment echivalent',available:true,isAlternative:true,lineTotal:1800}]
    };
    TestBed.overrideProvider(OfferService, { useValue: { forBuyer: () => of([
      {supplierName:'Furnizor complet',offer:baseOffer,coverageDetails:{requestedPositionCount:1,coveredPositionCount:1,missingRfqItemIds:[]}},
      {supplierName:'Furnizor parțial',offer:{...baseOffer,id:'offer-2',supplierId:'supplier-2',coverage:'Partial',total:1700,
        items:[{...baseOffer.items[0],available:false,lineTotal:0}]},coverageDetails:{requestedPositionCount:1,coveredPositionCount:0,missingRfqItemIds:['item-1']}}
    ]) } });
    const fixture=TestBed.createComponent(BuyerOfferComparisonComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const page=fixture.nativeElement as HTMLElement;

    expect(page.querySelector('.line-comparison')?.textContent).toContain('Ciment');
    expect(page.querySelector('.line-comparison')?.textContent).toContain('Alternativă');
    expect(page.querySelector('.line-comparison')?.textContent).toContain('Lipsește');
    expect(page.textContent).toContain('Acoperă 0 din 1 poziții');
    expect(page.textContent).not.toContain('Cea mai bună ofertă');
    expect(page.querySelectorAll('.cards button').length).toBe(1);
  });
});
