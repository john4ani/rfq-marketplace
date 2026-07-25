import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MessagingService } from './messaging.service';

describe('MessagingService', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));

  it('loads new messages using the last received timestamp as a cursor', () => {
    const service = TestBed.inject(MessagingService);
    const http = TestBed.inject(HttpTestingController);
    service.get('offer-1', '2026-07-24T12:00:00Z').subscribe();
    const request = http.expectOne(request =>
      request.url === '/api/v1/offers/offer-1/conversation' &&
      request.params.get('after') === '2026-07-24T12:00:00Z');
    expect(request.request.method).toBe('GET');
    request.flush({ offerId: 'offer-1', rfqId: 'rfq-1', rfqTitle: 'Cerere', otherPartyName: 'Partener',
      viewerRole: 'Buyer', unreadCount: 0, messages: [] });
    http.verify();
  });

  it('sends a message and marks the conversation read', () => {
    const service = TestBed.inject(MessagingService);
    const http = TestBed.inject(HttpTestingController);
    service.send('offer-1', 'Detalii livrare?').subscribe();
    const send = http.expectOne('/api/v1/offers/offer-1/conversation/messages');
    expect(send.request.method).toBe('POST');
    expect(send.request.body).toEqual({ body: 'Detalii livrare?' });
    send.flush({ id: 'message-1', body: 'Detalii livrare?', createdAt: '2026-07-24T12:00:00Z', isMine: true });
    service.markRead('offer-1').subscribe();
    const read = http.expectOne('/api/v1/offers/offer-1/conversation/read');
    expect(read.request.method).toBe('POST');
    read.flush(null);
    http.verify();
  });
});
