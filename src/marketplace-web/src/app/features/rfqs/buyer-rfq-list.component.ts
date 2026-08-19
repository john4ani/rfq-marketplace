import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { ProcurementOutcome, Rfq, RfqService, RfqStatus } from '../../core/rfqs/rfq.service';

@Component({
  selector: 'app-buyer-rfq-list',
  imports: [RouterLink, DatePipe],
  template: `<section class="rfq-card">
      <header><div><p class="eyebrow">Achizițiile tale</p><h2>Cererile mele</h2>
        <p>Pregătește cererea și public-o atunci când ai toate informațiile importante.</p></div>
        <a class="primary-link" routerLink="/rfqs/new">Cerere nouă</a>
      </header>
      @if (loading()) {
        <div class="loading-board" role="status" aria-label="Se încarcă cererile"><span></span><span></span><span></span></div>
      } @else if (error()) {
        <div class="error-state" role="alert"><div><strong>Cererile nu sunt disponibile momentan.</strong><span>{{ error() }}</span></div><button type="button" (click)="load()">Încearcă din nou</button></div>
      } @else if (rfqs().length === 0) {
        <div class="empty"><strong>Nu ai încă nicio cerere.</strong>
          <span>Începe cu o ciornă; o poți verifica înainte să o publici.</span>
          <a class="primary-link" routerLink="/rfqs/new">Creează prima cerere</a></div>
      } @else {
      <div class="request-board">
        @for (column of columns(); track column.key) {
          <section class="request-column" [id]="column.key === 'live' ? 'offers' : null" [class.live]="column.key === 'live'" [class.drafts]="column.key === 'drafts'" [class.completed]="column.key === 'completed'">
            <header class="column-header"><div><p>{{ column.eyebrow }}</p><h3>{{ column.title }}</h3></div><span>{{ column.requests.length }}</span></header>
            <p class="column-description">{{ column.description }}</p>
            <div class="column-list">
              @for (rfq of column.requests; track rfq.id) {
                <article>
                  <div><span class="status" [class]="rfq.status.toLowerCase()" [class.needs-outcome]="outcomeDue(rfq)">{{ rfq.outcome ? outcomeLabel(rfq.outcome) : outcomeDue(rfq) ? 'Rezultat necesar' : rfq.fulfillmentStatus === 'Delivered' ? 'Livrată' : statusLabel(rfq.status) }}</span>
                    <h4>{{ rfq.title }}</h4>
                    <p>{{ rfq.items.length }} {{ rfq.items.length === 1 ? 'poziție' : 'poziții' }} · {{ rfq.deliveryLocality }}, {{ rfq.county }}</p>
                    <small>Termen oferte: {{ rfq.quotationDeadline | date:'dd.MM.yyyy, HH:mm' }}</small>
                  </div>
                  @if (rfq.status === 'Draft') { <a [routerLink]="['/rfqs', rfq.id, 'edit']">Continuă ciorna</a> }
                  @else { <a [routerLink]="['/rfqs', rfq.id, 'offers']">{{ rfq.outcome ? 'Vezi rezultatul' : outcomeDue(rfq) ? 'Înregistrează rezultatul' : rfq.selectedOfferId ? 'Vezi oferta selectată' : 'Vezi și compară ofertele' }}</a> }
                </article>
              } @empty {
                <div class="column-empty"><strong>{{ column.emptyTitle }}</strong><span>{{ column.emptyText }}</span></div>
              }
            </div>
          </section>
        }
      </div>
      }
    </section>`,
  styleUrl: './buyer-rfq-list.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BuyerRfqListComponent {
  readonly auth = inject(AuthService);
  private readonly service = inject(RfqService);
  private readonly destroyRef = inject(DestroyRef);
  readonly rfqs = signal<Rfq[]>([]);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly columns = computed(() => [
    {
      key:'live', eyebrow:'Acum', title:'Live și în desfășurare',
      description:'Publicate, cu oferte sau cu livrarea în curs.',
      emptyTitle:'Nicio cerere live', emptyText:'Publică o ciornă când este pregătită.',
      requests:this.rfqs().filter(rfq => rfq.status === 'Published' || rfq.status === 'ReceivingOffers' || rfq.status === 'OfferSelected')
    },
    {
      key:'drafts', eyebrow:'De pregătit', title:'Ciorne',
      description:'Cereri salvate, dar încă nepublicate.',
      emptyTitle:'Nicio ciornă', emptyText:'Poți începe o cerere nouă oricând.',
      requests:this.rfqs().filter(rfq => rfq.status === 'Draft')
    },
    {
      key:'completed', eyebrow:'Istoric', title:'Închise și finalizate',
      description:'Rezultate înregistrate, livrate sau anulate.',
      emptyTitle:'Nicio cerere finalizată', emptyText:'Cererile închise vor rămâne aici pentru referință.',
      requests:this.rfqs().filter(rfq => rfq.status === 'Closed' || rfq.status === 'Cancelled')
    }
  ]);
  private readonly currentTime = signal(Date.now());
  private loadedFor = '';
  private deadlineTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    effect(() => {
      const identity = this.auth.identity();
      if (!identity || this.loadedFor === identity.userId) return;
      this.loadedFor = identity.userId; this.load();
    });
    this.destroyRef.onDestroy(() => this.clearDeadlineTimer());
  }
  statusLabel(status: RfqStatus): string {
    return ({ Draft: 'Ciornă', Published: 'Publicată', ReceivingOffers: 'Primește oferte', OfferSelected: 'Ofertă selectată', Closed: 'Închisă', Cancelled: 'Anulată' })[status];
  }
  outcomeDue(rfq: Rfq): boolean {
    return !rfq.outcome && (rfq.status === 'OfferSelected' && rfq.fulfillmentStatus === 'NotApplicable'
      || ((rfq.status === 'Published' || rfq.status === 'ReceivingOffers')
        && new Date(rfq.quotationDeadline).getTime() <= this.currentTime()));
  }
  outcomeLabel(outcome: ProcurementOutcome): string {
    return ({
      PurchasedThroughCompaSpot:'Ofertă acceptată', PurchasedFromParticipant:'Cumpărat de la participant',
      PurchasedElsewhere:'Cumpărat din altă parte', Postponed:'Achiziție amânată', Cancelled:'Anulată',
      NoSuitableOffer:'Fără ofertă potrivită', InsufficientResponses:'Răspunsuri insuficiente', Other:'Închisă'
    })[outcome];
  }
  load(): void {
    this.loading.set(true); this.error.set('');
    this.service.getMine().subscribe({
      next: values => {
        this.rfqs.set(values);
        this.currentTime.set(Date.now());
        this.scheduleNextDeadline(values);
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.error.set('Nu am putut încărca cererile acum. Încearcă din nou.'); }
    });
  }
  private scheduleNextDeadline(rfqs: Rfq[]): void {
    this.clearDeadlineTimer();
    const now = Date.now();
    const deadlines = rfqs
      .filter(rfq => !rfq.outcome && (rfq.status === 'Published' || rfq.status === 'ReceivingOffers'))
      .map(rfq => new Date(rfq.quotationDeadline).getTime())
      .filter(deadline => deadline > now);
    if (deadlines.length === 0) return;
    const delay = Math.min(Math.min(...deadlines) - now + 100, 2_147_483_647);
    this.deadlineTimer = setTimeout(() => {
      this.currentTime.set(Date.now());
      this.scheduleNextDeadline(this.rfqs());
    }, delay);
  }
  private clearDeadlineTimer(): void {
    if (this.deadlineTimer !== null) clearTimeout(this.deadlineTimer);
    this.deadlineTimer = null;
  }
}
