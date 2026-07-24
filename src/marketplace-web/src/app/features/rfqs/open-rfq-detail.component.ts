import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OpenRfq, OpenRfqService } from '../../core/rfqs/open-rfq.service';
import { OfferEditorComponent } from '../offers/offer-editor.component';

@Component({
  selector: 'app-open-rfq-detail',
  imports: [RouterLink, DatePipe, OfferEditorComponent],
  template: `<section class="detail-shell">
    <a class="back" routerLink="/open-requests">← Cereri deschise</a>
    @if (loading()) { <div class="loading" role="status"><span></span><span></span><span></span></div> }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    @if (request(); as rfq) {
      <header><div><p class="eyebrow">{{ rfq.isPartialMatch ? 'Partea relevantă pentru specializarea ta' : rfq.categoryName }}</p><h1>{{ rfq.title }}</h1>
        <p>{{ rfq.projectName || 'Cerere publicată în Piața Live' }}</p></div>
        <div class="deadline"><small>Termen pentru ofertă</small><strong>{{ rfq.quotationDeadline | date:'dd.MM.yyyy, HH:mm' }}</strong></div></header>
      <section class="summary">
        <div><small>Cumpărător</small><strong>{{ rfq.buyerDisplayName }}</strong></div>
        <div><small>Livrare</small><strong>{{ rfq.deliveryLocality }}, {{ rfq.county }}</strong></div>
        @if (rfq.preferredDeliveryDate) { <div><small>Data preferată</small><strong>{{ rfq.preferredDeliveryDate | date:'dd.MM.yyyy' }}</strong></div> }
        @if (rfq.estimatedBudget !== null) { <div><small>Buget estimat</small><strong>{{ rfq.estimatedBudget }} lei</strong></div> }
      </section>

      <div class="detail-layout">
        <div class="request-column">
          @if (rfq.description) { <section class="panel"><p class="panel-label">Context</p><h2>Descrierea cererii</h2><p>{{ rfq.description }}</p></section> }
          <section class="panel"><p class="panel-label">Necesar</p><h2>{{rfq.isPartialMatch?'Pozițiile potrivite pentru tine':'Poziții solicitate'}}</h2>
            @if(rfq.isPartialMatch){<p class="match-note">Proiectul a fost împărțit deoarece nu există momentan un furnizor eligibil care să acopere toate categoriile.</p>}
            @if (rfq.items.length === 0) { <div class="general-request"><strong>Cerere generală</strong><p>Cumpărătorul nu a introdus poziții individuale. Poți răspunde cu un preț total și condițiile comerciale.</p></div> }
            @for (item of rfq.items; track item.id) {
              <article class="material"><span class="item-number">{{ $index + 1 }}</span><div><strong>{{ item.productName }}</strong>
                <small>{{ item.specification || item.manufacturerOrBrand || 'Fără specificație suplimentară' }}</small></div>
                <span>{{ item.quantity }} · {{ item.unit }}</span></article>
            }
          </section>
        </div>
        <aside class="offer-column"><div class="offer-heading"><p class="eyebrow">Răspunsul tău</p><h2>Pregătește oferta</h2>
          <p>Completează prețul și condițiile. Poți reveni să actualizezi oferta înainte de termen.</p></div>
          <app-offer-editor [request]="rfq" /></aside>
      </div>
    }
  </section>`,
  styleUrl: './open-rfq-detail.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OpenRfqDetailComponent {
  private readonly service = inject(OpenRfqService);
  readonly request = signal<OpenRfq | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');

  constructor() {
    const id = inject(ActivatedRoute).snapshot.paramMap.get('rfqId') ?? '';
    this.service.get(id).subscribe({
      next: value => { this.request.set(value); this.loading.set(false); },
      error: error => { this.loading.set(false); this.error.set(error.status === 404
        ? 'Cererea nu este disponibilă pentru profilul și zona ta.'
        : 'Cererea nu a putut fi încărcată. Încearcă din nou.'); }
    });
  }
}
