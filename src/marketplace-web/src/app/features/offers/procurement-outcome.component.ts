import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, input, output, signal } from '@angular/core';
import { ProcurementOutcome, Rfq, RfqService } from '../../core/rfqs/rfq.service';

@Component({
  selector: 'app-procurement-outcome',
  imports: [DatePipe],
  template: `@if (request().outcome; as outcome) {
    <section class="complete" role="status">
      <div><p class="eyebrow">Rezultat înregistrat</p><h2>{{ outcomeLabel(outcome) }}</h2>
        @if (request().outcomeReason) { <p>{{ request().outcomeReason }}</p> }</div>
      <span>Închisă la {{ request().outcomeRecordedAt | date:'dd.MM.yyyy, HH:mm' }}</span>
    </section>
  } @else if (canRecord()) {
    @if (!expanded() && !isDue()) {
      <section class="prompt"><div><strong>Achiziția s-a încheiat?</strong>
        <span>Înregistrează rezultatul real când nu mai aștepți alte oferte.</span></div>
        <button type="button" class="secondary" (click)="start()">Închide cu rezultat</button></section>
    } @else {
      <form (submit)="save($event)">
        <div><p class="eyebrow">Închide bucla</p><h2>Cum s-a încheiat achiziția?</h2>
          <p>Alege rezultatul real. Răspunsul ne ajută să îmbunătățim potrivirea cererilor cu furnizorii.</p></div>
        <label>Rezultat
          <select [value]="selected()" (change)="selected.set($any($event.target).value)">
            <option value="">Alege rezultatul</option>
            @if (request().selectedOfferId) {
              <option value="PurchasedThroughCompaSpot">Am cumpărat prin oferta selectată în CompaSpot</option>
            } @else {
              @if (offerCount() > 0) { <option value="PurchasedFromParticipant">Am cumpărat de la un participant, în afara platformei</option> }
              <option value="PurchasedElsewhere">Am cumpărat din altă parte</option>
              <option value="Postponed">Am amânat achiziția</option>
              <option value="NoSuitableOffer">Nu am găsit o ofertă potrivită</option>
              <option value="InsufficientResponses">Nu am primit suficiente răspunsuri</option>
              <option value="Other">Alt rezultat</option>
            }
          </select>
        </label>
        <label>Detalii {{ selected() === 'Other' ? '(obligatoriu)' : '(opțional)' }}
          <textarea rows="3" maxlength="1000" [value]="reason()" (input)="reason.set($any($event.target).value)"
            placeholder="Adaugă context util despre rezultat"></textarea>
        </label>
        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        <div class="actions">
          @if (!isDue()) { <button type="button" class="secondary" (click)="cancel()">Renunță</button> }
          <button type="submit" [disabled]="busy() || !selected() || (selected() === 'Other' && !reason().trim())">
            {{ busy() ? 'Se salvează…' : 'Confirmă și închide cererea' }}</button>
        </div>
        <small>Rezultatul se salvează în istoricul cererii și nu poate fi schimbat.</small>
      </form>
    }
  }`,
  styleUrl: './procurement-outcome.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProcurementOutcomeComponent {
  private readonly service = inject(RfqService);
  private readonly destroyRef = inject(DestroyRef);
  readonly request = input.required<Rfq>();
  readonly offerCount = input.required<number>();
  readonly recorded = output<Rfq>();
  readonly expanded = signal(false);
  readonly selected = signal<ProcurementOutcome | ''>('');
  readonly reason = signal('');
  readonly error = signal('');
  readonly busy = signal(false);
  private readonly currentTime = signal(Date.now());
  private deadlineTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    effect(() => this.scheduleDeadline(this.request()));
    this.destroyRef.onDestroy(() => this.clearDeadlineTimer());
  }

  canRecord(): boolean {
    const request = this.request();
    return !request.outcome && (request.status === 'Published' || request.status === 'ReceivingOffers'
      || (request.status === 'OfferSelected' && request.fulfillmentStatus === 'NotApplicable'));
  }
  isDue(): boolean {
    const request = this.request();
    return (request.status === 'OfferSelected' && request.fulfillmentStatus === 'NotApplicable')
      || ((request.status === 'Published' || request.status === 'ReceivingOffers')
        && new Date(request.quotationDeadline).getTime() <= this.currentTime());
  }
  start(): void {
    this.selected.set(''); this.reason.set(''); this.error.set(''); this.expanded.set(true);
  }
  cancel(): void { this.expanded.set(false); this.error.set(''); }
  save(event: Event): void {
    event.preventDefault();
    const outcome = this.selected();
    if (!outcome || this.busy()) return;
    const reason = this.reason().trim();
    if (outcome === 'Other' && !reason) { this.error.set('Descrie pe scurt rezultatul cererii.'); return; }
    this.busy.set(true); this.error.set('');
    this.service.recordOutcome(this.request().id, outcome, reason || null).subscribe({
      next: request => { this.busy.set(false); this.recorded.emit(request); },
      error: response => { this.busy.set(false); this.error.set(this.apiError(response)); }
    });
  }
  outcomeLabel(outcome: ProcurementOutcome): string {
    return ({
      PurchasedThroughCompaSpot:'Cumpărat prin CompaSpot', PurchasedFromParticipant:'Cumpărat de la un participant',
      PurchasedElsewhere:'Cumpărat din altă parte', Postponed:'Achiziție amânată', Cancelled:'Cerere anulată',
      NoSuitableOffer:'Nicio ofertă potrivită', InsufficientResponses:'Răspunsuri insuficiente', Other:'Alt rezultat'
    })[outcome];
  }
  private apiError(response: { error?: { errors?: Record<string, string[]>; detail?: string } }): string {
    const messages = response.error?.errors ? Object.values(response.error.errors).flat() : [];
    return messages[0] ?? response.error?.detail ?? 'Rezultatul nu a putut fi salvat.';
  }
  private scheduleDeadline(request: Rfq): void {
    this.clearDeadlineTimer();
    if (request.outcome || (request.status !== 'Published' && request.status !== 'ReceivingOffers')) return;
    const delay = new Date(request.quotationDeadline).getTime() - Date.now();
    if (delay <= 0) { this.currentTime.set(Date.now()); return; }
    this.deadlineTimer = setTimeout(() => {
      this.currentTime.set(Date.now());
      this.scheduleDeadline(this.request());
    }, Math.min(delay + 100, 2_147_483_647));
  }
  private clearDeadlineTimer(): void {
    if (this.deadlineTimer !== null) clearTimeout(this.deadlineTimer);
    this.deadlineTimer = null;
  }
}
