import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OfferService, OfferStatus, OfferSummary } from '../../core/offers/offer.service';

type OfferFilter = 'All' | 'Active' | 'Selected' | 'NotSelected' | 'Draft' | 'Withdrawn';

@Component({
  selector: 'app-supplier-offer-list',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  template: `<section class="offers-card">
    <header>
      <div><p class="eyebrow">Furnizor</p><h2>Ofertele mele</h2>
        <p>Urmărește răspunsurile trimise și decizia cumpărătorului.</p></div>
      <span class="count">{{ offers().length }} în total</span>
    </header>

    @if (loading()) {
      <div class="loading" role="status" aria-label="Se încarcă ofertele"><span></span><span></span></div>
    } @else if (error()) {
      <p class="error" role="alert">{{ error() }}</p>
    } @else if (offers().length === 0) {
      <div class="empty"><span aria-hidden="true">↗</span><div><strong>Nu ai trimis încă nicio ofertă.</strong>
        <small>Explorează cererile potrivite și răspunde oportunităților relevante.</small></div>
        <a routerLink="/open-requests">Vezi cererile deschise</a></div>
    } @else {
      <nav class="filters" aria-label="Filtre oferte">
        @for (option of filterOptions; track option.value) {
          <button type="button" [class.active]="filter() === option.value" (click)="filter.set(option.value)">
            {{ option.label }} <span>{{ countFor(option.value) }}</span>
          </button>
        }
      </nav>

      <div class="offer-list">
        @for (offer of visibleOffers(); track offer.id) {
          <article [class.selected]="offer.status === 'Selected'" [class.not-selected]="offer.status === 'NotSelected'">
            <div class="offer-main"><span class="status" [class]="offer.status.toLowerCase()">{{ statusLabel(offer.status) }}</span>
              <h3>{{ offer.rfqTitle }}</h3>
              <p>Actualizată {{ offer.updatedAt | date:'dd.MM.yyyy, HH:mm' }}</p></div>
            <div class="offer-value"><small>Valoare ofertă</small><strong>{{ offer.total | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</strong></div>
            <div class="offer-deadline"><small>Termen cerere</small><strong>{{ offer.quotationDeadline | date:'dd.MM.yyyy, HH:mm' }}</strong></div>
            <div class="offer-actions">
              <a [routerLink]="['/open-requests', offer.rfqId]">{{ actionLabel(offer.status) }} →</a>
              @if (offer.status !== 'Draft') {
                <a class="message-link" [routerLink]="['/offers', offer.id, 'conversation']">Mesaje</a>
              }
            </div>
          </article>
        } @empty {
          <p class="filtered-empty">Nu există oferte cu acest status.</p>
        }
      </div>
    }
  </section>`,
  styleUrl: './supplier-offer-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SupplierOfferListComponent {
  private readonly service = inject(OfferService);
  readonly offers = signal<OfferSummary[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly filter = signal<OfferFilter>('All');
  readonly filterOptions: ReadonlyArray<{ value: OfferFilter; label: string }> = [
    { value: 'All', label: 'Toate' },
    { value: 'Active', label: 'În așteptare' },
    { value: 'Selected', label: 'Acceptate' },
    { value: 'NotSelected', label: 'Neselectate' },
    { value: 'Draft', label: 'Ciorne' },
    { value: 'Withdrawn', label: 'Retrase' }
  ];
  readonly visibleOffers = computed(() => {
    const filter = this.filter();
    return this.offers().filter(offer => filter === 'All' || (filter === 'Active'
      ? offer.status === 'Submitted' || offer.status === 'Revised'
      : offer.status === filter));
  });

  constructor() {
    this.service.getMine().subscribe({
      next: offers => { this.offers.set(offers); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Ofertele nu au putut fi încărcate. Încearcă din nou.'); }
    });
  }

  countFor(filter: OfferFilter): number {
    if (filter === 'All') return this.offers().length;
    return this.offers().filter(offer => filter === 'Active'
      ? offer.status === 'Submitted' || offer.status === 'Revised'
      : offer.status === filter).length;
  }

  statusLabel(status: OfferStatus): string {
    return ({
      Draft: 'Ciornă',
      Submitted: 'Trimisă',
      Revised: 'Revizuită',
      Withdrawn: 'Retrasă',
      Selected: 'Acceptată',
      NotSelected: 'Neselectată'
    })[status];
  }

  actionLabel(status: OfferStatus): string {
    return status === 'Draft' ? 'Continuă oferta' : status === 'Selected' ? 'Vezi oferta acceptată' : 'Vezi cererea';
  }
}
