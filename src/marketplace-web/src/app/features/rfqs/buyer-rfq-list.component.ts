import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { Rfq, RfqService, RfqStatus } from '../../core/rfqs/rfq.service';

@Component({
  selector: 'app-buyer-rfq-list',
  imports: [RouterLink, DatePipe],
  template: `@if (auth.session()?.accountType === 'Buyer') {
    <section class="rfq-card">
      <header><div><p class="eyebrow">Cumpărător</p><h2>Cererile mele</h2>
        <p>Pregătește o listă de materiale și public-o atunci când este completă.</p></div>
        <a class="primary-link" routerLink="/rfqs/new">Cerere nouă</a>
      </header>
      @if (loading()) { <p role="status">Se încarcă cererile…</p> }
      @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
      @if (!loading() && rfqs().length === 0) {
        <div class="empty"><strong>Nu ai încă nicio cerere.</strong>
          <span>Începe cu o ciornă; o poți verifica înainte să o publici.</span></div>
      }
      <div class="rfq-list">
        @for (rfq of rfqs(); track rfq.id) {
          <article>
            <div><span class="status" [class]="rfq.status.toLowerCase()">{{ statusLabel(rfq.status) }}</span>
              <h3>{{ rfq.title }}</h3>
              <p>{{ rfq.items.length }} {{ rfq.items.length === 1 ? 'material' : 'materiale' }} · {{ rfq.deliveryLocality }}, {{ rfq.county }}</p>
              <small>Termen oferte: {{ rfq.quotationDeadline | date:'dd.MM.yyyy, HH:mm' }}</small>
            </div>
            @if (rfq.status === 'Draft') { <a [routerLink]="['/rfqs', rfq.id, 'edit']">Continuă ciorna</a> }
            @else { <a [routerLink]="['/rfqs', rfq.id, 'offers']">{{ rfq.selectedOfferId ? 'Vezi oferta selectată' : 'Vezi și compară ofertele' }}</a> }
          </article>
        }
      </div>
    </section>
  }`,
  styleUrl: './buyer-rfq-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BuyerRfqListComponent {
  readonly auth = inject(AuthService);
  private readonly service = inject(RfqService);
  readonly rfqs = signal<Rfq[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  private loadedFor = '';

  constructor() {
    effect(() => {
      const session = this.auth.session();
      if (session?.accountType !== 'Buyer' || this.loadedFor === session.userId) return;
      this.loadedFor = session.userId; this.load();
    });
  }
  statusLabel(status: RfqStatus): string {
    return ({ Draft: 'Ciornă', Published: 'Publicată', ReceivingOffers: 'Primește oferte', OfferSelected: 'Ofertă selectată', Closed: 'Închisă', Cancelled: 'Anulată' })[status];
  }
  private load(): void {
    this.loading.set(true); this.error.set('');
    this.service.getMine().subscribe({
      next: values => { this.rfqs.set(values); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Cererile nu au putut fi încărcate. Verifică dacă API-ul rulează și încearcă din nou.'); }
    });
  }
}
