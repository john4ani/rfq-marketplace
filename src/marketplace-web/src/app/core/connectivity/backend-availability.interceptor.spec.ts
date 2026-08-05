import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { backendAvailabilityInterceptor } from './backend-availability.interceptor';
import { BackendAvailabilityService } from './backend-availability.service';

describe('backendAvailabilityInterceptor', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([backendAvailabilityInterceptor])),
        provideHttpClientTesting()
      ]
    });
  });

  it('reports a network failure but lets the next API call recover automatically', () => {
    const backend = TestBed.inject(BackendAvailabilityService);
    const http = TestBed.inject(HttpTestingController);
    const client = TestBed.inject(HttpClient);

    client.get('/api/v1/piata-live').subscribe({ error: () => undefined });
    http.expectOne('/api/v1/piata-live').error(new ProgressEvent('network error'));

    expect(backend.status()).toBe('unavailable');

    client.get('/api/v1/notifications/unread-count').subscribe();
    http.expectOne('/api/v1/notifications/unread-count').flush({ count: 0 });

    expect(backend.status()).toBe('available');
    http.verify();
  });

  it('recognizes Vite empty proxy failures as unavailable', () => {
    const backend = TestBed.inject(BackendAvailabilityService);
    const http = TestBed.inject(HttpTestingController);
    const client = TestBed.inject(HttpClient);

    client.post('/api/v1/auth/refresh', {}).subscribe({ error: () => undefined });
    http.expectOne('/api/v1/auth/refresh').flush('', { status: 500, statusText: 'Internal Server Error' });

    expect(backend.status()).toBe('unavailable');
    http.verify();
  });

  it('keeps the backend available for a reachable application-level 503', () => {
    const backend = TestBed.inject(BackendAvailabilityService);
    const http = TestBed.inject(HttpTestingController);
    const client = TestBed.inject(HttpClient);

    client.post('/api/v1/assisted-intakes', { input: 'test' }).subscribe({ error: () => undefined });
    http.expectOne('/api/v1/assisted-intakes').flush(
      { title: 'Asistent indisponibil', status: 503 },
      { status: 503, statusText: 'Service Unavailable' }
    );

    expect(backend.status()).toBe('available');
    http.verify();
  });

  it('closes the circuit after a reachable response', () => {
    const backend = TestBed.inject(BackendAvailabilityService);
    const http = TestBed.inject(HttpTestingController);
    const client = TestBed.inject(HttpClient);

    backend.markUnavailable();
    backend.markAvailable();

    client.get('/api/v1/piata-live').subscribe();
    http.expectOne('/api/v1/piata-live').flush({ participants: [] });

    expect(backend.status()).toBe('available');
    http.verify();
  });
});
