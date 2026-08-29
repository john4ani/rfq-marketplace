import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { interval, switchMap, timer } from 'rxjs';
import { PipelineProgress } from '../../core/companies/company-vault.service';
import { AutoBidResponse, BidStatus, SeapService, SeapTender, TenderPricing } from '../../core/seap/seap.service';

/** Friendly Romanian labels for the real per-stage auto-bid progress reported by the API. */
const AUTO_BID_STAGE_LABELS: Record<PipelineProgress['stage'], string> = {
  Extracting: 'Analizăm anunțul…',
  Generating: 'Claude redactează propunerea tehnică…',
  Finalizing: 'Generăm documentele .docx și .xlsx…',
  Completed: 'Propunerea este gata.',
  Failed: 'Generarea a eșuat.'
};

@Component({
  selector: 'app-seap-tender-detail',
  imports: [RouterLink, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`:host{display:block}.page{display:grid;gap:1.2rem;max-width:64rem}.back-link{display:inline-flex;align-items:center;gap:.4rem;width:fit-content;color:var(--forest);font-weight:750;text-decoration:none;font-size:.92rem}.eyebrow{margin:0;color:var(--forest-light);font-size:.76rem;font-weight:800;letter-spacing:.1em;text-transform:uppercase}h1{margin:.15rem 0;font-size:clamp(1.9rem,4vw,2.6rem);letter-spacing:-.04em}.card,.state{padding:1.15rem 1.25rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface);box-shadow:var(--shadow-soft)}.card h2{margin:0;font-size:1.05rem}.facts{display:grid;grid-template-columns:repeat(auto-fit,minmax(11rem,1fr));gap:.75rem}.facts>div{display:grid;gap:.2rem;padding:.85rem;border:1px solid var(--border);border-radius:.8rem;background:var(--warm-white)}.facts span{color:var(--muted);font-size:.74rem;font-weight:750;text-transform:uppercase;letter-spacing:.05em}.facts strong{font-size:.98rem}.actions{display:flex;flex-wrap:wrap;gap:.7rem;align-items:center}button{min-height:2.6rem;padding:.6rem 1.05rem;border:0;border-radius:.6rem;color:#fff;background:var(--forest);font:inherit;font-weight:800;cursor:pointer}button:disabled{opacity:.55;cursor:not-allowed}.note{border-radius:.7rem;padding:.8rem .95rem;line-height:1.5}.note.info{color:#315947;background:#edf6f1}.note.warn{color:#7a5206;background:#faf0d8}.note.error{color:#8b2222;background:#fff0f0}.copilot{margin-top:.7rem;font-size:.82rem}.downloads{display:flex;flex-wrap:wrap;gap:.6rem}.downloads a,.downloads button{display:inline-flex;align-items:center;padding:.55rem .9rem;border-radius:.6rem;color:var(--forest);background:color-mix(in srgb,var(--sand) 45%,white);font-weight:800;text-decoration:none;font-size:.95rem;min-height:auto}.acceptance{display:flex;gap:.6rem;align-items:flex-start;margin-top:.8rem;padding:.8rem .9rem;border:1px solid var(--border);border-radius:.7rem;background:var(--warm-white)}.acceptance input{width:1.05rem;height:1.05rem;margin-top:.15rem;accent-color:var(--forest)}.acceptance label{font-size:.85rem;line-height:1.45;color:var(--ink)}.state{justify-items:center;padding:2rem;text-align:center;color:var(--muted)}.progress{height:.5rem;border-radius:999px;background:color-mix(in srgb,var(--sand) 70%,white);overflow:hidden;margin:.55rem 0}.progress>span{display:block;height:100%;width:42%;border-radius:inherit;background:var(--forest-light);animation:progress-slide 1.4s ease-in-out infinite}@keyframes progress-slide{0%{transform:translateX(-120%)}100%{transform:translateX(300%)}}.progress-stage{margin:.35rem 0 0;color:#315947;font-weight:650}.progress-hint{color:var(--muted)}`],
  template: `<section class="page">
    <a class="back-link" routerLink="/seap/tenders">← Anunțuri SEAP</a>
    @if (loading()) { <section class="state" role="status">Se încarcă anunțul…</section> }
    @else if (tender(); as tender) {
      <header><p class="eyebrow">Anunț SEAP · {{ tender.seapExternalId }}</p><h1>{{ tender.title }}</h1>
        <p>{{ tender.buyerName }}{{ tender.county ? ' · ' + tender.county : '' }}{{ tender.category?.name ? ' · ' + tender.category?.name : '' }} · termen {{ tender.deadline | date:'dd.MM.yyyy, HH:mm' }}</p></header>
      <section class="card"><div class="facts">
        <div><span>Valoare estimată</span><strong>{{ tender.estimatedValue !== null ? (tender.estimatedValue | number:'1.0-0':'ro') + ' ' + (tender.currency ?? 'RON') : 'Nespecificată' }}</strong></div>
        <div><span>Coduri CPV</span><strong>{{ tender.cpvCodes.join(', ') || '—' }}</strong></div>
        <div><span>Categorie</span><strong>{{ tender.category?.name || tender.cpvNames[0] || '—' }}</strong></div>
        <div><span>Publicat</span><strong>{{ tender.publicationDate | date:'dd.MM.yyyy' }}</strong></div>
      </div></section>
      @if (tender.description) { <section class="card"><h2>Descriere</h2><p>{{ tender.description }}</p></section> }
      <section class="card">
        @if (bidStatus(); as status) {
          @if (status.status === 'BidGenerated') {
            <div class="note info"><strong>Oferta automată este gata.</strong>
              <div class="downloads">
                <button type="button" [disabled]="downloading()" (click)="downloadBidFile('TechnicalProposal')">Descarcă propunerea tehnică (.docx)</button>
                <button type="button" [disabled]="downloading()" (click)="downloadBidFile('BillOfQuantities')">Descarcă lista de cantități (.xlsx)</button>
              </div>
              <label class="acceptance"><input type="checkbox" [checked]="acceptsResponsibility()" (change)="acceptsResponsibility.set($any($event.target).checked)" />
                <span>Îmi asum responsabilitatea verificării și asumării conținutului acestor documente înainte de semnarea electronică și depunerea în SEAP.</span></label></div>
          } @else if (busy()) {
            <div class="note info" role="status">
              <strong>Redactăm oferta…</strong>
              <div class="progress" aria-hidden="true"><span></span></div>
              <p class="progress-stage">{{ autoBidStage() || 'Analizăm anunțul și pregătim propunerea tehnică…' }}</p>
              <small class="progress-hint">De obicei durează sub un minut. Poți reveni oricând la anunț.</small>
            </div>
          }
          @else {
            <div class="actions">
              <button type="button" [disabled]="busy()" (click)="startAutoBid(tender.id)">{{ pricing().shadowMode ? 'AI Auto-Bid · gratuit' : pricing().freeGenerationsRemaining > 0 ? 'AI Auto-Bid · gratuit' : 'AI Auto-Bid · ' + pricing().priceEur + ' €' }}</button>
              @if (pricing().shadowMode) { <span class="note info">Mod de test — generarea este gratuită în această etapă. După lansare, prețul estimat pentru acest anunț: {{ pricing().priceEur }} €.</span> }
              @else if (pricing().freeGenerationsRemaining > 0) { <span class="note info">Prima generare este gratuită. Următoarele: {{ pricing().priceEur }} € (valoare estimată).</span> }
              @else { <span class="note info">Generare {{ pricing().priceEur }} €, pe baza valorii estimate a achiziției.</span> }
              <span class="note warn">Generăm propunerea tehnică și lista de cantități pe baza datelor din Company Vault.
                <span class="copilot">CompaSpot este un instrument software de redactare asistată, nu un furnizor de consultanță juridică sau tehnică. Verifică și asumă-ți conținutul înainte de semnarea electronică și depunerea în SEAP.</span></span>
            </div>
          }
          @if (message()) { <p class="note" [class.error]="messageError()" role="status">{{ message() }}</p> }
        }
      </section>
    }
  </section>`
})

export class SeapTenderDetailComponent {
  private readonly service = inject(SeapService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly tenderId = this.route.snapshot.paramMap.get('id') ?? '';
  readonly tender = signal<SeapTender | null>(null);
  readonly bidStatus = signal<BidStatus | null>(null);
  readonly pricing = signal<TenderPricing>({ estimatedValueEur: 0, priceEur: 0, freeGenerationsRemaining: 0, shadowMode: true });
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal('');
  readonly messageError = signal(false);
  readonly acceptsResponsibility = signal(false);
  readonly downloading = signal(false);
  readonly autoBidStage = signal('');

  constructor() {
    this.service.getTender(this.tenderId).subscribe({
      next: value => { this.tender.set(value); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
    this.service.getBidStatus(this.tenderId).subscribe({ next: value => this.bidStatus.set(value) });
    this.service.getTenderPricing(this.tenderId).subscribe({ next: value => this.pricing.set(value) });
  }

  startAutoBid(tenderId: string): void {
    this.busy.set(true);
    this.message.set('');
    this.messageError.set(false);
    this.autoBidStage.set('');
    // The API reports real per-stage progress under this id while the request is in flight; poll it
    // so the user sees what is actually happening instead of an endless "generating…" line.
    const progressRequestId = crypto.randomUUID();
    this.startProgressPolling(progressRequestId);
    this.service.startAutoBid(tenderId, progressRequestId).subscribe({
      next: response => this.handleAutoBid(response),
      error: (error: unknown) => {
        this.busy.set(false);
        this.messageError.set(true);
        this.message.set(this.autoBidErrorMessage(error));
        // A failed generation never consumes the free entitlement; keep the price preview accurate.
        this.refreshPricing();
        this.stopProgressPolling();
      }
    });
  }

  /** Turns an HTTP/API failure into a friendly, specific message (backend ProblemDetails.detail wins,
   *  mirroring the Caiet-de-sarcini pipeline error handling). */
  private autoBidErrorMessage(error: unknown): string {
    const detail = (error as { error?: { detail?: string } })?.error?.detail;
    if (detail) return detail;
    return 'Ofertarea automată nu a putut fi pornită. Încearcă din nou.';
  }

  private handleAutoBid(response: AutoBidResponse): void {
    this.stopProgressPolling();
    if (response.ready) {
      this.busy.set(false);
      this.message.set(response.freeGeneration ? 'Prima generare a fost gratuită. Oferta este gata.' : 'Oferta automată este gata.');
      this.refreshStatus();
      // The free entitlement (if any) was consumed server-side; refresh the price preview so the
      // "Prima generare este gratuită" note/button reflect the remaining count immediately.
      this.refreshPricing();
      return;
    }
    if (response.checkoutUrl) {
      window.location.href = response.checkoutUrl;
      this.busy.set(false);
      this.pollUntilReady();
      return;
    }
    this.busy.set(false);
    this.messageError.set(true);
    this.message.set('Plata nu este configurată. Contactează suportul.');
  }

  /** Downloads one auto-generated proposal file through the API (the stored local://s3:// reference is
   *  resolved server-side; browsers cannot navigate those schemes directly). */
  downloadBidFile(kind: 'TechnicalProposal' | 'BillOfQuantities'): void {
    if (this.downloading()) return;
    this.downloading.set(true);
    this.message.set('');
    this.service.downloadBidFile(this.tenderId, kind).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = kind === 'TechnicalProposal' ? 'PropunereTehnica.docx' : 'ListaCantitati.xlsx';
        anchor.click();
        URL.revokeObjectURL(url);
        this.downloading.set(false);
      },
      error: () => {
        this.downloading.set(false);
        this.messageError.set(true);
        this.message.set('Fișierul nu a putut fi descărcat. Regenerează propunerea și încearcă din nou.');
      }
    });
  }

  private pollUntilReady(): void {
    interval(5000).pipe(
      takeUntilDestroyed(this.destroyRef),
      switchMap(() => this.service.getBidStatus(this.tenderId))
    ).subscribe({
      next: status => {
        this.bidStatus.set(status);
        if (status.status === 'BidGenerated') {
          this.busy.set(false);
          this.message.set('Oferta automată este gata.');
          this.refreshPricing();
        }
      },
      error: () => { /* keep polling; transient */ }
    });
  }

  private refreshStatus(): void {
    this.service.getBidStatus(this.tenderId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: status => { this.bidStatus.set(status); if (status.status !== 'BidGenerated') this.busy.set(true); },
      error: () => this.busy.set(false)
    });
  }

  /** Reloads the fair-pricing preview (free generations remaining / price) after a generation consumed it. */
  private refreshPricing(): void {
    this.service.getTenderPricing(this.tenderId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => this.pricing.set(value),
      error: () => undefined
    });
  }

  private autoBidProgressRequestId: string | null = null;

  /** Polls the real per-stage progress (Generating → Finalizing → Completed/Failed) reported by the API
   *  while the POST /auto-bid request is in flight, mirroring the Caiet-de-sarcini pipeline UX. */
  private startProgressPolling(requestId: string): void {
    this.autoBidProgressRequestId = requestId;
    timer(0, 2000)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        if (this.autoBidProgressRequestId !== requestId) return;
        this.service.getAutoBidProgress(this.tenderId, requestId).subscribe({
          next: progress => {
            if (this.autoBidProgressRequestId !== requestId) return;
            this.autoBidStage.set(progress.message ?? AUTO_BID_STAGE_LABELS[progress.stage]);
          },
          // A not-yet-registered key at t=0 (or an older API without the endpoint) is expected; keep polling.
          error: () => undefined
        });
      });
  }

  private stopProgressPolling(): void {
    this.autoBidProgressRequestId = null;
  }
}

