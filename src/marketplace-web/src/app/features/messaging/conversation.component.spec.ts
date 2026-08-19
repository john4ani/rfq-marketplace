import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { MessagingService } from '../../core/messaging/messaging.service';
import { ConversationComponent } from './conversation.component';

describe('ConversationComponent', () => {
  it('keeps the buying conversation context clear without treating it as an account role', async () => {
    await TestBed.configureTestingModule({
      imports: [ConversationComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'offer-1' } } } },
        {
          provide: MessagingService,
          useValue: {
            get: () => of({
              offerId: 'offer-1', rfqId: 'rfq-1', rfqTitle: 'Cherestea pentru acoperiș',
              otherPartyName: 'Depozitul de Lemn', viewerRole: 'Buyer', unreadCount: 0,
              messages: [
                { id: 'message-1', body: 'Am nevoie de livrare.', createdAt: '2026-08-05T10:00:00Z', isMine: true },
                { id: 'message-2', body: 'Putem livra vineri.', createdAt: '2026-08-05T10:05:00Z', isMine: false }
              ]
            }),
            markRead: () => of(undefined),
            send: () => of(undefined)
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ConversationComponent);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Discuți despre o achiziție inițiată de tine.');
    expect(text).toContain('Cerere:');
    expect(text).toContain('Tu');
    expect(text).toContain('Depozitul de Lemn');
  });

  it('explains company representation in the selling conversation context', async () => {
    await TestBed.configureTestingModule({
      imports: [ConversationComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'offer-2' } } } },
        {
          provide: MessagingService,
          useValue: {
            get: () => of({
              offerId: 'offer-2', rfqId: 'rfq-2', rfqTitle: 'Placaj exterior',
              otherPartyName: 'Client Cherestea', viewerRole: 'Supplier', unreadCount: 0,
              messages: [
                { id: 'message-3', body: 'Oferta este valabilă.', createdAt: '2026-08-05T11:00:00Z', isMine: true },
                { id: 'message-4', body: 'Mulțumesc.', createdAt: '2026-08-05T11:05:00Z', isMine: false }
              ]
            }),
            markRead: () => of(undefined),
            send: () => of(undefined)
          }
        }
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(ConversationComponent);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Discuți în numele firmei pe care o reprezinți.');
    expect(text).toContain('Client Cherestea');
  });
});
