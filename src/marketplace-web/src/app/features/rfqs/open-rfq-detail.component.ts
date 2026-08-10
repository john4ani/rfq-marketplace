import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OpenRfq, OpenRfqService } from '../../core/rfqs/open-rfq.service';
import { unitLabel } from '../../shared/unit-label';
import { OfferEditorComponent } from '../offers/offer-editor.component';
import { TrustFactsComponent } from '../profiles/trust-facts.component';

@Component({
  selector: 'app-open-rfq-detail',
  imports: [RouterLink, DatePipe, OfferEditorComponent, TrustFactsComponent],
  template: `<section class="detail-shell">
    <a class="back" routerLink="/open-requests">← Cereri deschise</a>
    @if (loading()) { <div class="loading" role="status"><span></span><span></span><span></span></div> }
    @if (error()) { <section class="error" role="alert"><div><strong>Cererea nu este disponibilă momentan.</strong><span>{{ error() }}</span></div><button type="button" (click)="load()">Încearcă din nou</button></section> }
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
      <section class="match-band"><span aria-hidden="true">✓</span><div><strong>{{ rfq.isPartialMatch ? 'Potrivire parțială cu profilul firmei tale' : 'Cerere potrivită profilului firmei tale' }}</strong>
        <p>{{ rfq.isPartialMatch ? 'Vezi numai pozițiile din categoriile pe care le acoperi.' : 'Categoria și zona de livrare corespund preferințelor tale.' }}</p></div></section>
      <div class="detail-layout">
        <div class="request-column">
          <section class="panel trust-panel"><div><p class="panel-label">Încredere</p><h2>Despre cumpărător</h2></div><app-trust-facts [facts]="rfq.buyerTrustFacts" role="Buyer" /></section>
          @if (rfq.description) { <section class="panel"><p class="panel-label">Context</p><h2>Descrierea cererii</h2><p>{{ rfq.description }}</p></section> }
          <section class="panel"><p class="panel-label">Necesar</p><h2>{{rfq.isPartialMatch?'Pozițiile potrivite pentru tine':'Poziții solicitate'}}</h2>
            @if(rfq.isPartialMatch){<p class="match-note">Proiectul a fost împărțit deoarece nu există momentan un furnizor eligibil care să acopere toate categoriile.</p>}
            @if (rfq.items.length === 0) { <div class="general-request"><strong>Cerere generală</strong><p>Cumpărătorul nu a introdus poziții individuale. Poți răspunde cu un preț total și condițiile comerciale.</p></div> }
            @for (item of rfq.items; track item.id) {
              <article class="material"><span class="item-number">{{ $index + 1 }}</span><div><strong>{{ item.productName }}</strong>
                <small>{{ item.kind === 'Service' ? 'Serviciu' : 'Produs' }} · {{ item.specification || item.manufacturerOrBrand || 'Fără specificație suplimentară' }}</small></div>
                <span>{{ item.quantity }} {{ unitLabel(item.unit) }}</span></article>
            }
          </section>
        </div>
        <aside class="offer-column"><div class="offer-guidance"><span aria-hidden="true">i</span><p><strong>Răspunde simplu și clar.</strong> Poți salva o ciornă și reveni înainte de termen.</p></div><app-offer-editor [request]="rfq" /></aside>
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
  readonly unitLabel = unitLabel;
  private readonly requestId = inject(ActivatedRoute).snapshot.paramMap.get('rfqId') ?? '';

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.service.get(this.requestId).subscribe({
      next: value => { this.request.set(value); this.loading.set(false); },
      error: error => { this.loading.set(false); this.error.set(error.status === 404
        ? 'Cererea nu este disponibilă pentru profilul și zona ta.'
        : 'Cererea nu a putut fi încărcată. Încearcă din nou.'); }
    });
  }
}
