import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { Rfq, RfqService } from '../../core/rfqs/rfq.service';
import { BuyerRfqListComponent } from './buyer-rfq-list.component';

describe('BuyerRfqListComponent', () => {
  afterEach(() => vi.useRealTimers());

  it('changes an open request to outcome-required when its deadline passes without reloading', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-08-05T10:00:00.000Z'));
    const rfq = {
      id:'rfq-1',title:'Cerere aproape expirată',status:'Published',outcome:null,
      fulfillmentStatus:'NotApplicable',items:[],deliveryLocality:'Suceava',county:'Suceava',
      quotationDeadline:'2026-08-05T10:00:01.000Z',selectedOfferId:null
    } as unknown as Rfq;
    TestBed.configureTestingModule({
      imports:[BuyerRfqListComponent],
      providers:[
        provideRouter([]),
        {provide:AuthService,useValue:{session:signal({userId:'buyer-1',email:'buyer@example.test',accountType:'Buyer'})}},
        {provide:RfqService,useValue:{getMine:()=>of([rfq])}}
      ]
    });
    const fixture = TestBed.createComponent(BuyerRfqListComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Publicată');

    vi.advanceTimersByTime(1_101);
    fixture.detectChanges();

    const page = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(page).toContain('Rezultat necesar');
    expect(page).toContain('Înregistrează rezultatul');
  });

  it('separates live, draft and completed requests into task-focused columns', () => {
    const request = (id: string, title: string, status: Rfq['status']) => ({
      id,title,status,outcome:null,fulfillmentStatus:'NotApplicable',items:[],deliveryLocality:'Suceava',county:'Suceava',
      quotationDeadline:'2026-08-10T10:00:00.000Z',selectedOfferId:null
    }) as unknown as Rfq;
    TestBed.configureTestingModule({
      imports:[BuyerRfqListComponent],
      providers:[
        provideRouter([]),
        {provide:AuthService,useValue:{session:signal({userId:'buyer-1',email:'buyer@example.test',accountType:'Buyer'})}},
        {provide:RfqService,useValue:{getMine:()=>of([
          request('live-1','Cerere live','ReceivingOffers'),
          request('draft-1','Cerere ciornă','Draft'),
          request('closed-1','Cerere finalizată','Closed')
        ])}}
      ]
    });
    const fixture = TestBed.createComponent(BuyerRfqListComponent);
    fixture.detectChanges();
    const page = fixture.nativeElement as HTMLElement;

    expect(page.querySelector('.request-column.live')?.textContent).toContain('Cerere live');
    expect(page.querySelector('.request-column.drafts')?.textContent).toContain('Cerere ciornă');
    expect(page.querySelector('.request-column.completed')?.textContent).toContain('Cerere finalizată');
  });
});
