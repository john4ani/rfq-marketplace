import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Rfq, RfqService } from '../../core/rfqs/rfq.service';

@Component({
  selector: 'app-buyer-offer-inbox',
  imports: [DatePipe, RouterLink],
  template: `<section class="offers-inbox">
    <header><div><p class="eyebrow">Cumpărător</p><h1>Oferte primite</h1>
      <p>Compară răspunsurile primite pentru cererile tale și alege când ești pregătit.</p></div>
      <a routerLink="/rfqs/new" class="primary-link">Cerere nouă</a></header>
    @if (loading()) {
      <div class="loading" role="status" aria-label="Se încarcă ofertele"><span></span><span></span><span></span></div>
    } @else if (error()) {
      <div class="state error" role="alert"><div><strong>Ofertele nu sunt disponibile momentan.</strong><small>{{ error() }}</small></div><button type="button" (click)="load()">Încearcă din nou</button></div>
    } @else if (requests().length === 0) {
      <div class="state"><strong>Nu ai primit încă oferte.</strong><span>Ofertele primite pentru cererile publicate vor apărea aici.</span><a routerLink="/requests">Vezi cererile mele</a></div>
    } @else {
      <div class="offer-list">
        @for (request of requests(); track request.id) {
          <article><div><span class="status">{{ statusLabel(request) }}</span><h2>{{ request.title }}</h2>
            <p>{{ request.deliveryLocality }}, {{ request.county }} · termen: {{ request.quotationDeadline | date:'dd.MM.yyyy, HH:mm' }}</p></div>
            <a [routerLink]="['/rfqs', request.id, 'offers']">{{ request.selectedOfferId ? 'Vezi oferta selectată' : 'Compară ofertele' }}</a></article>
        }
      </div>
    }
  </section>`,
  styles: [`.offers-inbox{display:grid;gap:1.25rem;max-width:70rem;margin:auto;padding:clamp(1.25rem,3vw,2.25rem);background:var(--warm-white,#faf7f2)}header{display:flex;align-items:start;justify-content:space-between;gap:1rem}h1,h2,p{margin:0}.eyebrow{color:var(--forest-light,#2f6b4f);font-weight:700;font-size:.82rem;text-transform:uppercase;letter-spacing:.06em}header p:not(.eyebrow){margin-top:.45rem;color:var(--muted,#5f665f);max-width:42rem}.primary-link,article a,.state a{display:inline-flex;align-items:center;justify-content:center;border-radius:.55rem;padding:.68rem .95rem;background:var(--forest,#1e3d2f);color:#fff;text-decoration:none;font-weight:700;white-space:nowrap}.offer-list{display:grid;gap:.75rem}article,.state{border:1px solid #dce5e0;border-radius:1rem;background:#fffdf9;padding:1.1rem 1.2rem;box-shadow:0 6px 22px rgb(25 54 41 / 5%)}article{display:flex;align-items:center;justify-content:space-between;gap:1rem}article h2{font-size:1.05rem;margin:.42rem 0}article p{color:var(--muted,#5f665f);font-size:.9rem}.status{display:inline-block;padding:.22rem .48rem;border-radius:999px;background:#e4f1e7;color:#1e593b;font-size:.75rem;font-weight:800}.state{display:grid;gap:.45rem;text-align:center;padding:2rem}.state span,.state small{color:var(--muted,#5f665f)}.state a{justify-self:center;background:#fff;color:var(--forest,#1e3d2f);border:1px solid var(--forest,#1e3d2f)}.state.error{grid-template-columns:1fr auto;text-align:left;align-items:center}.state button{border:1px solid var(--forest,#1e3d2f);background:#fff;color:var(--forest,#1e3d2f);border-radius:.55rem;padding:.6rem .8rem;font-weight:700}.loading{display:flex;justify-content:center;gap:.45rem;padding:3rem}.loading span{width:.65rem;height:.65rem;border-radius:50%;background:var(--forest-light,#2f6b4f);animation:pulse 1s infinite alternate}.loading span:nth-child(2){animation-delay:.2s}.loading span:nth-child(3){animation-delay:.4s}@keyframes pulse{to{opacity:.25;transform:translateY(-.3rem)}}@media(max-width:600px){header,article{align-items:stretch;flex-direction:column}.primary-link,article a{width:100%}.state.error{grid-template-columns:1fr;text-align:center}}`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BuyerOfferInboxComponent {
  private readonly service = inject(RfqService);
  readonly requests = signal<Rfq[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true); this.error.set('');
    this.service.getMine().subscribe({
      next: requests => {
        this.requests.set(requests
          .filter(request => request.status === 'ReceivingOffers' || request.status === 'OfferSelected' || request.selectedOfferId !== null)
          .sort((left, right) => new Date(right.updatedAt).getTime() - new Date(left.updatedAt).getTime()));
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.error.set('Verifică conexiunea la serviciu și încearcă din nou.'); }
    });
  }

  statusLabel(request: Rfq): string {
    if (request.selectedOfferId) return 'Ofertă selectată';
    return request.status === 'ReceivingOffers' ? 'Oferte primite' : 'Oferte disponibile';
  }
}
