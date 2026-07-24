import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RfqService } from './rfq.service';

describe('RfqService', () => {
  let service: RfqService; let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({providers:[provideHttpClient(),provideHttpClientTesting()]});
    service=TestBed.inject(RfqService);http=TestBed.inject(HttpTestingController);
  });
  afterEach(()=>http.verify());
  it('loads only the current buyer requests',()=>{
    service.getMine().subscribe(values=>expect(values).toEqual([]));
    const request=http.expectOne('/api/v1/rfqs/mine');expect(request.request.method).toBe('GET');request.flush([]);
  });
  it('publishes a saved request',()=>{
    service.publish('rfq-1').subscribe();
    const request=http.expectOne('/api/v1/rfqs/rfq-1/publish');expect(request.request.method).toBe('POST');request.flush({});
  });
  it('extends the quotation deadline',()=>{
    service.extendDeadline('rfq-1','2026-08-01T10:00:00.000Z').subscribe();
    const request=http.expectOne('/api/v1/rfqs/rfq-1/extend-deadline');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({quotationDeadline:'2026-08-01T10:00:00.000Z'});
    request.flush({});
  });
});
