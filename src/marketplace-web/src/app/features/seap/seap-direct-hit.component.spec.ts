import { HttpHeaders } from '@angular/common/http';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { vi } from 'vitest';
import { FeaturesService } from '../../core/features/features.service';
import { SeapDirectHitComponent } from './seap-direct-hit.component';
import { SeapIngestionStatus } from '../../core/seap/seap.service';

// Livrarea 3 polling behavior: 200 Ready/Partial navigation, Retry-After polling, single-flight status
// requests, terminal stops, retryable-vs-permanent Failed, destroy cleanup and transient backoff.
describe('SeapDirectHitComponent', () => {
  const STATUS_URL = '/api/v1/seap/tenders/T-1/ingestion-status';
  let http!: HttpTestingController;
  let navigate!: ReturnType<typeof vi.fn>;

  const status = (over: Partial<SeapIngestionStatus>): SeapIngestionStatus => ({
    tenderId: 'T-1',
    noticeNo: 'SCN1179200',
    status: 'Pending',
    importMode: 'Standard',
    runId: 'R-1',
    attemptCount: 1,
    startedAt: new Date().toISOString(),
    completedAt: null,
    lastErrorCode: null,
    lastErrorMessage: null,
    documentSetHash: null,
    analysisVersion: null,
    ...over
  });

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [SeapDirectHitComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])]
    });
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    TestBed.inject(FeaturesService).setSeapDirectHitEnabledForTest(true);
    TestBed.inject(FeaturesService).setSeapIngestionWorkerEnabledForTest(true);
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
    http.verify();
  });

  async function create(): Promise<SeapDirectHitComponent> {
    const fixture = TestBed.createComponent(SeapDirectHitComponent);
    fixture.detectChanges();
    await Promise.resolve();
    return fixture.componentInstance;
  }

  async function resolve202(component: SeapDirectHitComponent, retryAfter = 5): Promise<void> {
    component.notice.set('SCN1179200');
    component.resolve();
    http.expectOne('/api/v1/seap/tenders/resolve').flush(
      { tenderId: 'T-1', noticeNo: 'SCN1179200', status: 'Pending', tender: null },
      { status: 202, statusText: 'Accepted', headers: new HttpHeaders({ 'Retry-After': String(retryAfter) }) });
    await Promise.resolve();
    await Promise.resolve();
  }

  it('navigates immediately to the detail page on a 200 Ready resolve', async () => {
    const component = await create();
    component.notice.set('SCN1179200');
    component.resolve();
    http.expectOne('/api/v1/seap/tenders/resolve').flush({
      tenderId: 'T-1', noticeNo: 'SCN1179200', status: 'Ready',
      tender: { id: 'T-1', seapExternalId: 'SCN1179200', title: 'Titlu', summaryGroundedInDocuments: true, attachments: [] } as never
    });
    await Promise.resolve();
    await Promise.resolve();

    expect(navigate).toHaveBeenCalledWith(['/seap/tenders', 'T-1']);
    vi.advanceTimersByTime(10_000); // no polling was started
  });

  it('navigates to the detail page on a 200 Partial resolve (data kept server-side)', async () => {
    const component = await create();
    component.notice.set('SCN1179200');
    component.resolve();
    http.expectOne('/api/v1/seap/tenders/resolve').flush({
      tenderId: 'T-1', noticeNo: 'SCN1179200', status: 'Partial',
      tender: { id: 'T-1', seapExternalId: 'SCN1179200', title: 'Titlu', attachments: [] } as never
    });
    await Promise.resolve();
    await Promise.resolve();

    expect(navigate).toHaveBeenCalledWith(['/seap/tenders', 'T-1']);
    expect(component.phase()).toBe('form');
    vi.advanceTimersByTime(10_000);
  });

  it('polls the status endpoint honoring Retry-After from the 202 headers', async () => {
    const component = await create();
    await resolve202(component, 2);

    vi.advanceTimersByTime(1999);
    http.expectNone(STATUS_URL);
    vi.advanceTimersByTime(1);
    const first = http.expectOne(STATUS_URL);
    first.flush(status({ status: 'FetchingDocuments' }));
    await Promise.resolve();
    await Promise.resolve();
    expect(component.phase()).toBe('processing');
    expect(component.stageLabel()).toContain('Descărcăm documentele');
  });

  it('never issues two concurrent status requests (single-flight)', async () => {
    const component = await create();
    await resolve202(component, 2);

    vi.advanceTimersByTime(2000);
    const first = http.expectOne(STATUS_URL);
    vi.advanceTimersByTime(10_000); // time passes while the first request is in flight → no second request
    http.expectNone(STATUS_URL);

    first.flush(status({ status: 'Analyzing' }));
    await Promise.resolve();
    await Promise.resolve();
    vi.advanceTimersByTime(2000);
    http.expectOne(STATUS_URL); // only after the first settled does the next tick fire
  });

  it('stops polling and navigates on Ready', async () => {
    const component = await create();
    await resolve202(component, 1);

    vi.advanceTimersByTime(1000);
    http.expectOne(STATUS_URL).flush(status({ status: 'Ready' }));
    await Promise.resolve();
    await Promise.resolve();
    expect(navigate).toHaveBeenCalledWith(['/seap/tenders', 'T-1']);
    vi.advanceTimersByTime(30_000); // no further status requests after the terminal state
  });

  it('offers an explicit Retry only for retryable Failed reasons', async () => {
    const component = await create();
    await resolve202(component, 1);
    vi.advanceTimersByTime(1000);
    http.expectOne(STATUS_URL).flush(status({ status: 'Failed', lastErrorCode: 'AnalysisTimeout', lastErrorMessage: 'timeout' }));
    await Promise.resolve();
    await Promise.resolve();
    expect(component.retryable()).toBe(true);
    expect(component.phase()).toBe('failed');
  });

  it('shows no Retry for a permanent failure (notice unavailable)', async () => {
    const component = await create();
    await resolve202(component, 1);
    vi.advanceTimersByTime(1000);
    http.expectOne(STATUS_URL).flush(status({ status: 'Failed', lastErrorCode: 'NoticeUnavailableOnSeap' }));
    await Promise.resolve();
    await Promise.resolve();
    expect(component.retryable()).toBe(false);
    expect(component.phase()).toBe('failed');
  });

  it('stops the timer and future requests on destroy', async () => {
    const fixture = TestBed.createComponent(SeapDirectHitComponent);
    fixture.detectChanges();
    await Promise.resolve();
    const component = fixture.componentInstance;
    await resolve202(component, 1);

    vi.advanceTimersByTime(1000);
    http.expectOne(STATUS_URL).flush(status({ status: 'Analyzing' }));
    await Promise.resolve();
    await Promise.resolve();
    expect(component.phase()).toBe('processing');

    fixture.destroy();
    vi.advanceTimersByTime(60_000);
    http.expectNone(STATUS_URL); // no request survives the destroy
    expect(navigate).not.toHaveBeenCalled();
  });

  it('applies backoff on a transient status error instead of a tight retry loop', async () => {
    const component = await create();
    await resolve202(component, 5);

    vi.advanceTimersByTime(5000);
    const failing = http.expectOne(STATUS_URL);
    failing.error(new ErrorEvent('error', { message: 'Unknown Error' }));
    await Promise.resolve();
    await Promise.resolve();

    vi.advanceTimersByTime(1000); // well below the 5s backoff → no tight loop
    http.expectNone(STATUS_URL);
    vi.advanceTimersByTime(4000);
    http.expectOne(STATUS_URL); // next attempt only after the full Retry-After backoff
  });

  it('Retry re-resolves once and resumes polling the same active run (no duplicate polling)', async () => {
    const component = await create();
    await resolve202(component, 1);
    vi.advanceTimersByTime(1000);
    http.expectOne(STATUS_URL).flush(status({ status: 'Failed', lastErrorCode: 'AnalysisTimeout' }));
    await Promise.resolve();
    await Promise.resolve();
    expect(component.phase()).toBe('failed');
    expect(component.retryable()).toBe(true);

    component.retry();
    // Exactly one new resolve POST (the backend reuses the already-active run idempotently).
    http.expectOne('/api/v1/seap/tenders/resolve').flush(
      { tenderId: 'T-1', noticeNo: 'SCN1179200', status: 'Pending', tender: null },
      { status: 202, statusText: 'Accepted', headers: new HttpHeaders({ 'Retry-After': '1' }) });
    await Promise.resolve();
    await Promise.resolve();
    expect(component.phase()).toBe('processing');

    vi.advanceTimersByTime(1000);
    http.expectOne(STATUS_URL).flush(status({ status: 'Analyzing' }));
    await Promise.resolve();
    await Promise.resolve();
    vi.advanceTimersByTime(1000);
    // The single poller continues — exactly one request, never two overlapping ones.
    http.expectOne(STATUS_URL);
  });

  it('shows a disabled state and issues no resolve request when the Direct Hit flag is off', async () => {
    const fixture = TestBed.createComponent(SeapDirectHitComponent);
    fixture.detectChanges();
    await Promise.resolve();
    const component = fixture.componentInstance;
    TestBed.inject(FeaturesService).setSeapDirectHitEnabledForTest(false);

    component.notice.set('SCN1179200');
    component.resolve();
    http.expectNone('/api/v1/seap/tenders/resolve'); // flag off → nothing is even attempted
    expect(component.errorMessage()).toContain('dezactivat');
  });

  it('hides the form and issues no request when the Direct Hit flag is on but the worker is off', async () => {
    const fixture = TestBed.createComponent(SeapDirectHitComponent);
    fixture.detectChanges();
    await Promise.resolve();
    const component = fixture.componentInstance;
    TestBed.inject(FeaturesService).setSeapIngestionWorkerEnabledForTest(false); // Direct Hit still true
    fixture.detectChanges();

    expect(TestBed.inject(FeaturesService).seapDirectHitAvailable()).toBe(false);
    // The form is hidden entirely (outer @if on availability)…
    expect(fixture.nativeElement.querySelector('#direct-hit-notice')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('dezactivat momentan');
    // …and resolve issues no request — never a run nobody would process.
    component.resolve();
    http.expectNone('/api/v1/seap/tenders/resolve');
    expect(component.errorMessage()).toContain('dezactivat');
  });
});
