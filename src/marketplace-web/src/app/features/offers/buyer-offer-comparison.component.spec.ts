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
    id:'rfq-1',buyerId:'buyer-1',title:'Materiale renovare',projectName:'Apartament',
    description:'Necesar complet',primaryCategoryId:'category-1',deliveryAddress:'Strada Test 1',
    deliveryLocality:'Iași',county:'Iași',supplierSearchRadiusKm:30,latitude:null,longitude:null,
    preferredDeliveryDate:null,quotationDeadline:'2026-08-01T10:00:00.000Z',
    alternativeProductsAccepted:true,estimatedBudget:5000,buyerNotes:null,allowSplit:false,
    status:'Published',createdAt:'2026-07-24T10:00:00.000Z',updatedAt:'2026-07-24T10:00:00.000Z',
    publishedAt:'2026-07-24T10:00:00.000Z',closedAt:null,selectedOfferId:null,reopenCount:0,
    items:[{id:'item-1',itemNumber:1,productName:'Ciment',manufacturerOrBrand:null,
      specification:'40 kg',quantity:20,unit:'Bag',alternativesAccepted:true,buyerNotes:null,categoryId:null}]
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports:[BuyerOfferComparisonComponent],
      providers:[
        {provide:ActivatedRoute,useValue:{snapshot:{paramMap:{get:()=>request.id}}}},
        {provide:RfqService,useValue:{
          get:()=>of(request),
          extendDeadline:()=>of({...request,quotationDeadline:'2026-08-02T10:00:00.000Z'}),
          cancel:()=>of({...request,status:'Cancelled'})
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
});
