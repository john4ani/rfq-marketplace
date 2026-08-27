import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { interval, switchMap } from 'rxjs';
import { AutoBidResponse, BidStatus, SeapService, SeapTender } from '../../core/seap/seap.service';

@Component({
  selector: 'app-seap-tender-detail',
  imports: [RouterLink, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`:host{display:block}.page{display:grid;gap:1.2rem;max-width:64rem}.back-link{display:inline-flex;align-items:center;gap:.4rem;width:fit-content;color:var(--forest);font-weight:750;text-decoration:none;font-size:.92rem}.eyebrow{margin:0;color:var(--forest-light);font-size:.76rem;font-weight:800;letter-spacing:.1em;text-transform:uppercase}h1{margin:.15rem 0;font-size:clamp(1.9rem,4vw,2.6rem);letter-spacing:-.04em}.card,.state{padding:1.15rem 1.25rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface);box-shadow:var(--shadow-soft)}.card h2{margin:0;font-size:1.05rem}.facts{display:grid;grid-template-columns:repeat(auto-fit,minmax(11rem,1fr));gap:.75rem}.facts>div{display:grid;gap:.2rem;padding:.85rem;border:1px solid var(--border);border-radius:.8rem;background:var(--warm-white)}.facts span{color:var(--muted);font-size:.74rem;font-weight:750;text-transform:uppercase;letter-spacing:.05em}.facts strong{font-size:.98rem}.actions{display:flex;flex-wrap:wrap;gap:.7rem;align-items:center}button{min-height:2.6rem;padding:.6rem 1.05rem;border:0;border-radius:.6rem;color:#fff;background:var(--forest);font:inherit;font-weight:800;cursor:pointer}button:disabled{opacity:.55;cursor:not-allowed}.note{border-radius:.7rem;padding:.8rem .95rem;line-height:1.5}.note.info{color:#315947;background:#edf6f1}.note.warn{color:#7a5206;background:#faf0d8}.note.error{color:#8b2222;background:#fff0f0}.downloads{display:flex;flex-wrap:wrap;gap:.6rem}.downloads a{display:inline-flex;padding:.55rem .9rem;border-radius:.6rem;color:var(--forest);background:color-mix(in srgb,var(--sand) 45%,white);font-weight:800;text-decoration:none}.state{justify-items:center;padding:2rem;text-align:center;color:var(--muted)}`],
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
          @if (status.status === 'BidGenerated' && status.technicalProposalUrl) {
            <div class="note info"><strong>Oferta automată este gata.</strong>
              <div class="downloads"><a [href]="status.technicalProposalUrl" target="_blank" rel="noreferrer">Descarcă propunerea tehnică (.docx)</a>
                <a [href]="status.billOfQuantitiesUrl ?? '#'" target="_blank" rel="noreferrer">Descarcă lista de cantități (.xlsx)</a></div></div>
          } @else if (busy()) { <div class="note info" role="status">Se generează oferta automată… poate dura câteva minute.</div> }
          @else { <div class="actions"><button type="button" [disabled]="busy()" (click)="startAutoBid(tender.id)">AI Auto-Bid</button>
            <span class="note info">Generăm propunerea tehnică și lista de cantități pe baza datelor din Company Vault.</span></div> }
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
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal('');
  readonly messageError = signal(false);

  constructor() {
    this.service.getTender(this.tenderId).subscribe({
      next: value => { this.tender.set(value); this.loading.set(false); },
      error: () => this.loading.set(false)
    });
    this.service.getBidStatus(this.tenderId).subscribe({ next: value => this.bidStatus.set(value) });
  }

  startAutoBid(tenderId: string): void {
    this.busy.set(true);
    this.message.set('');
    this.service.startAutoBid(tenderId).subscribe({
      next: response => this.handleAutoBid(response),
      error: () => { this.busy.set(false); this.messageError.set(true); this.message.set('Ofertarea automată nu a putut fi pornită. Încearcă din nou.'); }
    });
  }

  private handleAutoBid(response: AutoBidResponse): void {
    if (response.ready) {
      this.busy.set(false);
      this.refreshStatus();
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

  private pollUntilReady(): void {
    interval(5000).pipe(
      takeUntilDestroyed(this.destroyRef),
      switchMap(() => this.service.getBidStatus(this.tenderId))
    ).subscribe({
      next: status => {
        this.bidStatus.set(status);
        if (status.status === 'BidGenerated') { this.busy.set(false); this.message.set('Oferta automată este gata.'); }
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
}

