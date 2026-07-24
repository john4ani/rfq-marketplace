import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { OpenRfq, OpenRfqService } from '../../core/rfqs/open-rfq.service';

@Component({
  selector: 'app-open-rfq-detail',
  imports: [RouterLink, DatePipe],
  template: `<section class="detail-shell">
    <a class="back" routerLink="/open-requests">← Cereri deschise</a>
    @if (loading()) { <p role="status">Se încarcă cererea…</p> }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
    @if (request(); as rfq) {
      <header><div><p class="eyebrow">{{ rfq.categoryName }}</p><h1>{{ rfq.title }}</h1>
        <p>{{ rfq.projectName }}</p></div><span>Termen: {{ rfq.quotationDeadline | date:'dd.MM.yyyy, HH:mm' }}</span></header>
      <section class="summary">
        <div><small>Cumpărător</small><strong>{{ rfq.buyerDisplayName }}</strong></div>
        <div><small>Livrare</small><strong>{{ rfq.deliveryLocality }}, {{ rfq.county }}</strong></div>
        @if (rfq.preferredDeliveryDate) { <div><small>Data preferată</small><strong>{{ rfq.preferredDeliveryDate | date:'dd.MM.yyyy' }}</strong></div> }
        @if (rfq.estimatedBudget !== null) { <div><small>Buget estimat</small><strong>{{ rfq.estimatedBudget }} lei</strong></div> }
      </section>
      @if (rfq.description) { <section class="panel"><h2>Descriere</h2><p>{{ rfq.description }}</p></section> }
      <section class="panel"><h2>Materiale solicitate</h2>
        @if (rfq.items.length === 0) { <p>Solicitarea este generală; cumpărătorul nu a introdus poziții individuale.</p> }
        @for (item of rfq.items; track item.id) {
          <article class="material"><div><strong>{{ item.productName }}</strong>
            <small>{{ item.specification || item.manufacturerOrBrand || 'Fără specificație suplimentară' }}</small></div>
            <span>{{ item.quantity }} · {{ item.unit }}</span></article>
        }
      </section>
      <section class="next-step"><strong>Trimiterea ofertelor urmează în următorul slice Milestone 5.</strong>
        <span>Acest ecran este momentan read-only.</span></section>
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
        : 'Cererea nu a putut fi încărcată.'); }
    });
  }
}
