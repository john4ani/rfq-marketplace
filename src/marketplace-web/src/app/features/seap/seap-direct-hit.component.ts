import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { Subscription, timer } from 'rxjs';
import { FeaturesService } from '../../core/features/features.service';
import { INGESTION_STAGE_LABELS, SeapIngestionStatus, SeapResolveResponse, SeapService } from '../../core/seap/seap.service';

/** SEAP external notice numbers look like SCN1179200 / CN1010518 — the direct-hit lookup input format. */
const NOTICE_NUMBER_PATTERN = /^(SCN|CN)\d+$/i;

/** Failure codes that a Retry cannot fix (the notice vanished from SEAP or is legitimately document-less). */
const PERMANENT_FAILURE_CODES = new Set(['NoticeUnavailableOnSeap', 'NoPublishedDocuments', 'NoReadableDocuments']);

/** Stage labels shown while a Stage 1 DocumentImport run processes (durable original-document download only —
 *  no AI digest in this stage). Keyed by the same run statuses as INGESTION_STAGE_LABELS. */
const DOCUMENT_IMPORT_STAGE_LABELS: Record<SeapIngestionStatus['status'], string> = {
  Pending: 'Anunțul a fost găsit. Pregătim dosarul anunțului…',
  FetchingDocuments: 'Descărcăm și stocăm durabil documentele originale…',
  Analyzing: 'Procesăm documentele dosarului…',
  Ready: 'Documentele originale au fost stocate.',
  Partial: 'Unele documente originale nu au putut fi descărcate.',
  Failed: 'Descărcarea documentelor a eșuat.'
};

type DirectHitPhase = 'form' | 'processing' | 'failed';

/**
 * Livrarea 3 — Direct Hit SEAP: user enters a notice number (SCN…/CN…), the backend resolves it; when the
 * ingestion run is not ready yet (HTTP 202), this page polls the ingestion status URL exposed by the Location
 * header, honoring Retry-After. Polling stops on Ready/Partial (→ tender detail), Failed or 404/403; an
 * explicit Retry is offered only for the retryable Failed state. Polling is cleaned up on destroy and is
 * single-flight (never overlapping status requests).
 */
@Component({
  selector: 'app-seap-direct-hit',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: `:host{display:block}.page{display:grid;gap:1.1rem;max-width:46rem;margin:0 auto}.back{color:var(--forest);font-weight:750;text-decoration:none;font-size:.92rem}.card{padding:1.3rem 1.5rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface);box-shadow:var(--shadow-soft)}label{display:block;font-size:.85rem;font-weight:800;margin-bottom:.4rem}input{width:100%;box-sizing:border-box;padding:.8rem 1rem;border:1px solid var(--border);border-radius:.65rem;font:inherit}button{min-height:2.6rem;padding:.6rem 1.1rem;border:0;border-radius:.6rem;color:#fff;background:var(--forest);font:inherit;font-weight:800;cursor:pointer}button:disabled{opacity:.55;cursor:not-allowed}.hint{margin:.4rem 0 0;color:var(--muted);font-size:.8rem}.error{color:#b3261e}.spinner{width:1.1rem;height:1.1rem;border:2px solid color-mix(in srgb,var(--forest) 25%,transparent);border-top-color:var(--forest);border-radius:50%;animation:spin .8s linear infinite;display:inline-block;vertical-align:-.22rem;margin-right:.5rem}@keyframes spin{to{transform:rotate(360deg)}}.progress{display:grid;gap:.7rem;padding:1.5rem;text-align:center}.steps{display:flex;justify-content:center;gap:.4rem;flex-wrap:wrap}.step{padding:.3rem .7rem;border-radius:999px;border:1px solid var(--border);font-size:.78rem;font-weight:750;color:var(--muted)}.step.done{color:#176b52;background:#e3f2ea;border-color:#bcdccb}.step.now{color:var(--forest);border-color:var(--forest)}.retry{margin-top:.6rem}.retry button{margin-left:.5rem}`,
  template: `<section class="page">
    <a class="back" routerLink="/seap/tenders">← Anunțuri SEAP</a>
    @if (features.seapDirectHitAvailable()) {
    @if (phase() === 'form') {
      <section class="card">
        <label for="direct-hit-notice">Introdu numărul anunțului SEAP</label>
        <input id="direct-hit-notice" [value]="notice()" (keyup.enter)="resolve()" (input)="notice.set($any($event.target).value)" placeholder="Ex: SCN1179200 sau CN1010518" autocomplete="off" spellcheck="false" />
        <p class="hint">Căutăm anunțul direct pe SEAP, importăm metadatele și analizăm documentele în fundal.</p>
        @if (errorMessage()) { <p class="error" role="alert">{{ errorMessage() }}</p> }
        <p><button type="button" [disabled]="busy()" (click)="resolve()">{{ busy() ? 'Se caută anunțul…' : 'Caută anunțul' }}</button></p>
      </section>
    }
    @if (phase() === 'processing') {
      <section class="card progress" role="status" aria-live="polite">
        <div class="spinner"></div>
        <p><strong>{{ stageLabel() }}</strong></p>
        <div class="steps">
          <span class="step" [class.done]="stepsPassed() >= 1" [class.now]="status() === 'Pending'">Import</span>
          <span class="step" [class.done]="stepsPassed() >= 2" [class.now]="status() === 'FetchingDocuments'">{{ purpose() === 'DocumentImport' ? 'Documente originale' : 'Documente' }}</span>
          @if (purpose() !== 'DocumentImport') {
          <span class="step" [class.done]="stepsPassed() >= 3" [class.now]="status() === 'Analyzing'">Analiză AI</span>
          }
        </div>
        @if (statusError()) { <p class="error">{{ statusError() }}</p> }
      </section>
    }
    @if (phase() === 'failed') {
      <section class="card progress" role="alert">
        <p><strong>Anunțul nu a putut fi procesat.</strong></p>
        <p class="error">{{ statusError() || 'A apărut o eroare neașteptată.' }}</p>
        @if (retryable()) {
          <p class="retry">Vrei să încerci din nou? <button type="button" (click)="retry()">Reîncearcă procesarea</button></p>
        } @else {
          <p>Acest anunț nu poate fi reprocesat automat.</p>
        }
      </section>
    }
    } @else {
      <section class="card progress" role="status"><p><strong>Importul direct este dezactivat momentan.</strong></p></section>
    }
  </section>`
})
export class SeapDirectHitComponent {
  private readonly service = inject(SeapService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly features = inject(FeaturesService);

  readonly notice = signal('');
  readonly busy = signal(false);
  readonly phase = signal<DirectHitPhase>('form');
  readonly errorMessage = signal('');
  readonly stageLabel = signal('');
  readonly status = signal<SeapIngestionStatus['status'] | null>(null);
  /** Purpose of the run being polled ('DocumentImport' = Stage 1 dossier download; otherwise the full
   *  digest+analysis pipeline). Drives the stage labels and which pipeline steps are rendered. */
  readonly purpose = signal<'DigestAndAnalyze' | 'DocumentImport' | null>(null);
  readonly statusError = signal('');
  readonly stepsPassed = signal(0);
  /** Failed is retryable only for transient-error codes; permanent failures (e.g. the notice no longer on
   *  SEAP) never offer a Retry button. */
  readonly retryable = signal(false);
  /** Seconds between polls — read from the 202 Retry-After header, then kept as the default. */
  private pollIntervalSeconds = 5;
  private pollTimer: Subscription | null = null;
  private pollTarget = '';

  resolve(): void {
    if (!this.features.seapDirectHitAvailable()) {
      this.errorMessage.set('Importul direct este dezactivat momentan.');
      return;
    }
    const value = this.notice().trim();
    if (!NOTICE_NUMBER_PATTERN.test(value)) {
      this.errorMessage.set('Introdu un număr valid de anunț (SCN… sau CN…).');
      return;
    }
    if (this.busy()) return;
    this.busy.set(true);
    this.errorMessage.set('');
    this.status.set(null);
    this.purpose.set(null);
    this.retryable.set(false);
    this.phase.set('form');

    this.service.resolveNotice(value.toUpperCase()).subscribe({
      next: response => {
        this.busy.set(false);
        const body: SeapResolveResponse | null = response.body;
        if (body?.tender?.id) {
          // HTTP 200 — Ready/Partial with the full tender: straight to the canonical detail page.
          void this.router.navigate(['/seap/tenders', body.tender.id]);
          return;
        }
        if (response.status === 202 && body?.tenderId) {
          // HTTP 202 — metadata imported, ingestion run tracked: poll the status URL from Location, honoring
          // Retry-After instead of a hardcoded interval.
          const retryAfter = Number(response.headers.get('Retry-After'));
          this.pollIntervalSeconds = Number.isFinite(retryAfter) && retryAfter > 0 ? retryAfter : 5;
          this.phase.set('processing');
          this.purpose.set(body.purpose ?? null);
          this.stageLabel.set(this.stageLabelFor('Pending'));
          this.stepsPassed.set(1);
          this.startPolling(body.tenderId);
          return;
        }
        this.phase.set('form');
        this.errorMessage.set(body?.message ?? 'Anunțul nu a fost găsit pe SEAP.');
      },
      error: error => {
        this.busy.set(false);
        this.phase.set('form');
        this.errorMessage.set(this.problemDetail(error) ?? 'Anunțul nu a putut fi rezolvat. Încearcă din nou mai târziu.');
      }
    });
  }

  /** Single-flight polling: one in-flight status request at a time; the next tick is scheduled only after the
   *  previous response settled (running status → new timer; transient error → backoff timer; terminal →
   *  stop). Everything is torn down on destroy / terminal state. The 202 Location header points at the same
   *  ingestion-status endpoint polled here; Retry-After (captured in resolve) governs the interval. */
  private startPolling(tenderId: string): void {
    this.pollTarget = tenderId;
    this.scheduleNext();
  }

  private scheduleNext(): void {
    this.pollTimer?.unsubscribe();
    this.pollTimer = timer(this.pollIntervalSeconds * 1000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.fetchStatusOnce());
  }

  private fetchStatusOnce(): void {
    this.service.getIngestionStatus(this.pollTarget)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: status => this.handleStatus(status),
        error: error => {
          const httpError = error as { status?: number };
          if (httpError.status === 404 || httpError.status === 403) {
            this.stopPolling();
            this.fail('Anunțul nu mai este accesibil.');
            return;
          }
          this.statusError.set('Nu am putut verifica starea procesării. Se reîncearcă…');
          this.scheduleNext(); // transient — retry after the full backoff, never a tight loop
        }
      });
  }

  private handleStatus(status: SeapIngestionStatus): void {
    this.status.set(status.status);
    this.purpose.set(status.purpose ?? null);
    this.statusError.set('');
    if (status.status === 'Pending' || status.status === 'FetchingDocuments' || status.status === 'Analyzing') {
      this.stageLabel.set(this.stageLabelFor(status.status));
      this.stepsPassed.set(status.status === 'Pending' ? 1 : status.status === 'FetchingDocuments' ? 2 : 3);
      this.scheduleNext(); // next tick only after this response settled
      return;
    }
    this.stopPolling();
    if (status.status === 'Ready' || status.status === 'Partial') {
      // Ready shows the four tabs; Partial shows the available data + reason on the same detail page.
      void this.router.navigate(['/seap/tenders', status.tenderId]);
      return;
    }
    // Failed is the only terminal state with an explicit Retry, and only when the cause is retryable.
    this.stageLabel.set(this.stageLabelFor(status.status));
    this.statusError.set(status.lastErrorMessage || 'Procesarea a eșuat după mai multe încercări.');
    this.retryable.set(!PERMANENT_FAILURE_CODES.has(status.lastErrorCode ?? ''));
    this.phase.set('failed');
  }

  /** Stage label honoring the run purpose: DocumentImport runs (Stage 1, durable original-document download)
   *  use the dossier labels; any other purpose uses the generic digest+analysis pipeline labels. */
  private stageLabelFor(step: SeapIngestionStatus['status']): string {
    return this.purpose() === 'DocumentImport'
      ? DOCUMENT_IMPORT_STAGE_LABELS[step]
      : INGESTION_STAGE_LABELS[step];
  }

  retry(): void {
    this.phase.set('form');
    this.statusError.set('');
    this.resolve();
  }

  private stopPolling(): void {
    this.pollTimer?.unsubscribe();
    this.pollTimer = null;
  }

  private fail(message: string): void {
    this.stopPolling();
    this.retryable.set(false);
    this.statusError.set(message);
    this.phase.set('failed');
  }

  private problemDetail(error: unknown): string | null {
    const problem = (error as { error?: { detail?: string } })?.error;
    return problem?.detail ?? null;
  }
}
