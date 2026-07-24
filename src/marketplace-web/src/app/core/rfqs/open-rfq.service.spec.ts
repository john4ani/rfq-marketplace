import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { OpenRfqService } from './open-rfq.service';

describe('OpenRfqService', () => {
  it('loads matched requests with optional filters and details', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const service = TestBed.inject(OpenRfqService);
    const http = TestBed.inject(HttpTestingController);

    service.list('ciment', 'category-id').subscribe();
    const list = http.expectOne(request =>
      request.url === '/api/v1/open-rfqs'
      && request.params.get('search') === 'ciment'
      && request.params.get('categoryId') === 'category-id');
    expect(list.request.method).toBe('GET');
    list.flush([]);

    service.get('rfq-id').subscribe();
    const detail = http.expectOne('/api/v1/open-rfqs/rfq-id');
    expect(detail.request.method).toBe('GET');
    detail.flush({ id: 'rfq-id', items: [] });
    http.verify();
  });
});
