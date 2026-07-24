import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LiveMarketService } from './live-market.service';

describe('LiveMarketService', () => {
  it('loads the public Piața Live snapshot', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(LiveMarketService);
    const http = TestBed.inject(HttpTestingController);
    service.getSnapshot().subscribe(snapshot => expect(snapshot.participants.length).toBe(1));
    const request = http.expectOne('/api/v1/piata-live');
    expect(request.request.method).toBe('GET');
    request.flush({ generatedAt: new Date().toISOString(), participants: [{ id: 'id', name: 'Furnizor Test', profileType: 'Supplier', isOnline: true }] });
    http.verify();
  });
});
