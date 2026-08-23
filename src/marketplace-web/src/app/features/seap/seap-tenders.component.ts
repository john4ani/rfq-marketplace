import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SeapService, SeapTender } from '../../core/seap/seap.service';

@Component({
  selector: 'app-seap-tenders',
  imports: [RouterLink, DatePipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`:host{display:block}.page{display:grid;gap:1.2rem;max-width:76rem}.back-link{display:inline-flex;align-items:center;gap:.4rem;width:fit-content;color:var(--forest);font-weight:750;text-decoration:none;font-size:.92rem}.eyebrow{margin:0;color:var(--forest-light);font-size:.76rem;font-weight:800;letter-spacing:.1em;text-transform:uppercase}h1{margin:.15rem 0;font-size:clamp(1.9rem,4vw,2.8rem);letter-spacing:-.04em}.page>p{margin:0;color:var(--muted);line-height:1.5}.card,.state{padding:1.15rem 1.25rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface);box-shadow:var(--shadow-soft)}.card{display:flex;justify-content:space-between;gap:1.25rem;align-items:center;text-decoration:none;color:inherit;transition:border-color .15s}.card:hover{border-color:color-mix(in srgb,var(--forest) 45%,var(--border))}.card h2{margin:0;font-size:1.1rem}.card p{margin:.2rem 0 0;color:var(--muted);font-size:.9rem}.pill{display:inline-flex;align-items:center;padding:.3rem .72rem;border-radius:999px;font-size:.78rem;font-weight:800}.pill.ready{color:#176b52;background:#e3f2ea}.pill.done{color:var(--forest);background:color-mix(in srgb,var(--sand) 60%,white)}.arrow{flex:0 0 auto;color:var(--forest);font-weight:800}.state{justify-items:center;padding:2rem;text-align:center;color:var(--muted)}.state strong{color:var(--ink)}.state button{min-height:2.6rem;padding:.6rem 1.05rem;border:0;border-radius:.6rem;color:#fff;background:var(--forest);font:inherit;font-weight:800;cursor:pointer}`],
  template: `<section class="page">
    <a class="back-link" routerLink="/dashboard">← Înapoi acasă</a>
    <header><p class="eyebrow">Achiziții publice</p><h1>Anunțuri SEAP</h1>
      <p>Oportunități publice din e-licitatie.ro, cu ofertare automată asistată de AI.</p></header>
    @if (loading()) { <section class="state" role="status">Se încarcă anunțurile…</section> }
    @else if (error()) { <section class="state" role="alert"><strong>Anunțurile nu au putut fi încărcate.</strong><button type="button" (click)="load()">Încearcă din nou</button></section> }
    @else if (tenders().length === 0) { <section class="state"><strong>Nu există încă anunțuri importate.</strong><p>Anunțurile SEAP apar aici după import.</p></section> }
    @else {
      @for (tender of tenders(); track tender.id) {
        <a class="card" [routerLink]="['/seap/tenders', tender.id]">
          <div><h2>{{ tender.title }}</h2>
            <p>{{ tender.buyerName }} · {{ tender.cpvCodes.join(', ') || 'fără CPV' }}{{ tender.estimatedValue !== null ? ' · ' + (tender.estimatedValue | number:'1.0-0':'ro') + ' ' + (tender.currency ?? 'RON') : '' }}</p>
            <p>Termen: {{ tender.deadline | date:'dd.MM.yyyy, HH:mm' }}</p></div>
          <span class="pill" [class.ready]="tender.status === 'ReadyForBidding'" [class.done]="tender.status === 'BidGenerated'">{{ statusLabel(tender.status) }}</span>
        </a>
      }
    }
  </section>`
})
export class SeapTendersComponent {
  private readonly service = inject(SeapService);
  readonly tenders = signal<SeapTender[]>([]);
  readonly loading = signal(true);
  readonly error = signal(false);

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.service.listTenders().subscribe({
      next: value => { this.tenders.set(value); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set(true); }
    });
  }

  statusLabel(status: SeapTender['status']): string {
    return ({ Imported: 'Importat', ReadyForBidding: 'Gata de ofertare', BidGenerated: 'Ofertă generată', Failed: 'Eroare' })[status];
  }
}
