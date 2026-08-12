import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { RfqService } from '../../core/rfqs/rfq.service';
import { BuyerOfferInboxComponent } from './buyer-offer-inbox.component';

describe('BuyerOfferInboxComponent', () => {
  it('shows only requests that have received or selected offers', () => {
    TestBed.configureTestingModule({
      imports: [BuyerOfferInboxComponent], providers: [provideRouter([]), {
        provide: RfqService, useValue: { getMine: () => of([
          { id:'draft', title:'Ciornă', status:'Draft', selectedOfferId:null, updatedAt:'2026-08-12T09:00:00Z' },
          { id:'published', title:'Așteaptă oferte', status:'Published', selectedOfferId:null, updatedAt:'2026-08-12T10:00:00Z' },
          { id:'received', title:'Are oferte', status:'ReceivingOffers', selectedOfferId:null, updatedAt:'2026-08-12T11:00:00Z', deliveryLocality:'Iași', county:'Iași', quotationDeadline:'2026-08-15T10:00:00Z' },
          { id:'selected', title:'Ofertă aleasă', status:'Closed', selectedOfferId:'offer-1', updatedAt:'2026-08-12T12:00:00Z', deliveryLocality:'Iași', county:'Iași', quotationDeadline:'2026-08-15T10:00:00Z' }
        ]) }
      }]
    });
    const fixture = TestBed.createComponent(BuyerOfferInboxComponent);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Are oferte');
    expect(text).toContain('Ofertă aleasă');
    expect(text).not.toContain('Ciornă');
    expect(text).not.toContain('Așteaptă oferte');
  });
});
