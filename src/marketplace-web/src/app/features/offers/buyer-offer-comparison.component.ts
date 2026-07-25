import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { BuyerOffer, OfferService } from '../../core/offers/offer.service';
import { Rfq, RfqService } from '../../core/rfqs/rfq.service';

@Component({
  selector: 'app-buyer-offer-comparison',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  template: `<section class="compare">
    <a class="back" routerLink="/dashboard">← Cererile mele</a>
    @if (rfq(); as request) {
      <header><div><p class="eyebrow">Gestionare cerere</p><h1>{{ request.title }}</h1>
        <p>Urmărește cererea, gestionează termenul și compară ofertele primite.</p></div>
        <div class="header-state"><span class="status">{{ request.fulfillmentStatus === 'Delivered' ? 'Livrată' : statusLabel(request.status) }}</span><span>{{ offers().length }} oferte eligibile</span></div></header>
      <section class="request-overview">
        <div class="overview-heading"><div><p class="eyebrow">Cererea trimisă</p><h2>Stare și detalii</h2></div>
          @if (canManage(request)) {
            <div class="request-actions">
              <button type="button" class="secondary" (click)="startDeadlineExtension(request.quotationDeadline)">Prelungește termenul</button>
              <button type="button" class="danger-link" (click)="showCancel.set(true)">Anulează cererea</button>
            </div>
          }
        </div>
        <dl class="request-facts">
          <div><dt>Termen oferte</dt><dd>{{ request.quotationDeadline | date:'dd.MM.yyyy, HH:mm' }}</dd></div>
          <div><dt>Livrare</dt><dd>{{ request.deliveryLocality }}, {{ request.county }}</dd></div>
          <div><dt>Transport în ofertă</dt><dd>{{ request.deliveryRequired ? 'Solicitat' : 'Opțional' }}</dd></div>
          <div><dt>Buget estimat</dt><dd>{{ request.estimatedBudget ? (request.estimatedBudget | currency:'RON':'symbol-narrow':'1.0-2':'ro') : 'Nespecificat' }}</dd></div>
          <div><dt>Îndeplinire</dt><dd>{{ request.allowSplit ? 'Poate fi împărțită' : 'Un singur furnizor' }}</dd></div>
        </dl>
        <details class="request-details">
          <summary>Vezi cererea completă <span>{{ request.items.length }} {{ request.items.length === 1 ? 'poziție' : 'poziții' }}</span></summary>
          <div class="request-copy">
            @if (request.projectName) { <div><small>Proiect</small><p>{{ request.projectName }}</p></div> }
            @if (request.description) { <div><small>Descriere</small><p>{{ request.description }}</p></div> }
            <div><small>Adresă de livrare</small><p>{{ request.deliveryAddress }}, {{ request.deliveryLocality }}, {{ request.county }}</p></div>
            @if (request.preferredDeliveryDate) { <div><small>Livrare preferată</small><p>{{ request.preferredDeliveryDate | date:'dd.MM.yyyy' }}</p></div> }
            @if (request.buyerNotes) { <div><small>Observații pentru furnizori</small><p>{{ request.buyerNotes }}</p></div> }
          </div>
          @if (request.items.length > 0) {
            <ol class="request-items">
              @for (item of request.items; track item.id; let index = $index) {
                <li><div><strong>{{ item.productName }}</strong>
                  @if (item.specification) { <small>{{ item.specification }}</small> }</div>
                  <span>{{ item.quantity }} {{ unitLabel(item.unit) }}</span></li>
              }
            </ol>
          } @else {
            <p class="general-request">Cerere generală, fără poziții individuale.</p>
          }
        </details>
        @if (showDeadline()) {
          <form class="management-form" (submit)="extendDeadline($event)">
            <div><strong>Prelungește termenul pentru oferte</strong><small>Poți doar să alegi o dată ulterioară termenului curent.</small></div>
            <label>Termen nou<input type="datetime-local" [value]="newDeadline()" (input)="newDeadline.set($any($event.target).value)"></label>
            @if (managementError()) { <p class="error" role="alert">{{ managementError() }}</p> }
            <div class="management-actions"><button type="button" class="secondary" (click)="cancelManagement()">Renunță</button>
              <button type="submit" [disabled]="busy() || !newDeadline()">{{ busy() ? 'Se salvează…' : 'Salvează termenul' }}</button></div>
          </form>
        }
        @if (showCancel()) {
          <div class="cancel-confirmation" role="group" aria-label="Confirmă anularea cererii">
            <div><strong>Anulezi această cerere?</strong><p>Furnizorii nu vor mai putea trimite oferte. Această acțiune nu poate fi anulată.</p></div>
            @if (managementError()) { <p class="error" role="alert">{{ managementError() }}</p> }
            <div class="management-actions"><button type="button" class="secondary" (click)="cancelManagement()">Păstrează cererea</button>
              <button type="button" class="danger" (click)="cancelRequest()" [disabled]="busy()">{{ busy() ? 'Se anulează…' : 'Confirmă anularea' }}</button></div>
          </div>
        }
      </section>
      @if (request.selectedOfferId) {
        <section class="decision">
          <div class="success"><strong>{{ request.fulfillmentStatus === 'Delivered' ? 'Livrarea a fost confirmată.' : 'Oferta câștigătoare a fost selectată.' }}</strong>
            <span>{{ request.fulfillmentStatus === 'Delivered' ? 'Cererea este finalizată cu statusul Livrată.' : 'Cererea este închisă pentru alte oferte.' }}</span></div>
          @if (request.fulfillmentStatus === 'AwaitingDelivery') {
            <p class="reopen-limit" role="status">Transport inclus · furnizorul urmează să declare livrarea.</p>
          } @else if (request.fulfillmentStatus === 'DeliveryReported') {
            <div class="confirm-selection" role="group" aria-label="Confirmă primirea livrării">
              <div><strong>Furnizorul a declarat livrarea</strong>
                <p>Confirmă numai după ce ai primit și verificat produsele.</p></div>
              @if (!confirmDeliveryPending()) {
                <button type="button" (click)="confirmDeliveryPending.set(true)">Am primit produsele</button>
              } @else {
                <div class="confirm-actions"><button class="secondary" type="button" (click)="confirmDeliveryPending.set(false)">Verific mai târziu</button>
                  <button type="button" (click)="confirmDelivery()" [disabled]="busy()">Confirmă primirea și finalizează</button></div>
              }
            </div>
          }
          @if (request.reopenCount >= 1) {
            <p class="reopen-limit" role="status">Această cerere a fost deja redeschisă o dată. Selecția curentă este finală și nu mai poate fi anulată prin redeschidere.</p>
          } @else if (request.fulfillmentStatus === 'DeliveryReported' || request.fulfillmentStatus === 'Delivered') {
          } @else if (!showReopen()) {
            <button class="reopen-trigger" type="button" (click)="startReopen(request.quotationDeadline)">Ai făcut o greșeală? Redeschide cererea</button>
          } @else {
            <form class="reopen-form" (submit)="reopen($event)">
              <div><h2>Redeschide cererea</h2>
                <p>Selecția curentă va fi anulată, toate ofertele vor reveni în așteptare, iar furnizorii vor primi motivul.</p></div>
              <label>Motivul redeschiderii
                <textarea rows="3" maxlength="500" [value]="reopenReason()" (input)="reopenReason.set($any($event.target).value)"
                  placeholder="Explică ce s-a întâmplat și ce trebuie reverificat"></textarea>
                <small>Minimum 10 caractere · {{ reopenReason().trim().length }}/500</small>
              </label>
              @if (deadlineExpired()) {
                <label>Termen nou pentru oferte
                  <input type="datetime-local" [value]="reopenDeadline()" (input)="reopenDeadline.set($any($event.target).value)">
                  <small>Termenul anterior a expirat; este necesară o dată nouă.</small>
                </label>
              }
              @if(reopenError()){<p class="error" role="alert">{{reopenError()}}</p>}
              <div class="reopen-actions"><button type="button" class="secondary" (click)="cancelReopen()">Renunță</button>
                <button type="submit" [disabled]="busy() || reopenReason().trim().length < 10 || (deadlineExpired() && !reopenDeadline())">
                  {{busy()?'Se redeschide…':'Confirmă redeschiderea'}}</button></div>
            </form>
          }
        </section>
      }
      @if (offers().length > 0) {
        <section class="summary" aria-label="Rezumat oferte">
          <div><small>Cel mai mic total</small><strong>{{ lowestTotal() | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</strong></div>
          <div><small>Cea mai rapidă livrare</small><strong>{{ fastestDelivery() }} zile</strong></div>
          <div><small>Oferte primite</small><strong>{{ offers().length }}</strong></div>
        </section>
      }
      @if (loading()) { <div class="loading" role="status"><span></span><span></span></div> }
      @if (!loading() && offers().length === 0) {
        <div class="empty"><span aria-hidden="true">◇</span><div><strong>Nu ai primit încă oferte.</strong>
          @if (canManage(request)) {
            <small>Te vom anunța când un furnizor răspunde. Poți prelungi termenul sau anula cererea din secțiunea de mai sus.</small>
          } @else {
            <small>Cererea nu mai primește oferte.</small>
          }</div></div>
      }
      <div class="cards">
        @for (entry of offers(); track entry.offer.id; let first = $first) {
          <article [class.best]="first" [class.winner]="entry.offer.status === 'Selected'">
            <div class="top"><div>@if (first && !request.selectedOfferId) { <span class="recommend">Cel mai mic total</span> }
              @if (entry.offer.status === 'Selected') { <span class="recommend">Câștigătoare</span> }<h2>{{ entry.supplierName }}</h2></div>
              <div class="price"><small>Total</small><strong>{{ entry.offer.total | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</strong></div></div>
            <dl>
              @if (entry.offer.pricingMode === 'Total' || request.items.length === 0) {
                <div><dt>Tip ofertă</dt><dd>Preț total · întreaga cerere</dd></div>
              } @else {
                <div><dt>Produse / servicii</dt><dd>{{ entry.offer.itemsTotal | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</dd></div>
                <div><dt>Cost livrare</dt><dd>{{ entry.offer.deliveryCost | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</dd></div>
              }
              <div><dt>Termen livrare</dt><dd>{{ entry.offer.deliveryDays }} zile</dd></div>
              <div><dt>Transport</dt><dd>{{ entry.offer.deliveryIncluded ? 'Inclus' : 'Neinclus' }}</dd></div>
              <div><dt>Valabilă până la</dt><dd>{{ entry.offer.validUntil | date:'dd.MM.yyyy' }}</dd></div>
            </dl>
            @if (entry.offer.paymentTerms) { <div class="terms"><small>Condiții de plată</small><p>{{ entry.offer.paymentTerms }}</p></div> }
            @if (entry.offer.notes) { <div class="terms"><small>Mesaj furnizor</small><p>{{ entry.offer.notes }}</p></div> }
            <a class="conversation-link" [routerLink]="['/offers', entry.offer.id, 'conversation']">Mesaje cu furnizorul →</a>
            @if (entry.offer.status === 'Selected') {
              <div class="selected">✓ Ofertă selectată</div>
            } @else if (!request.selectedOfferId && pendingOffer()?.offer?.id === entry.offer.id) {
              <div class="confirm-selection" role="group" aria-label="Confirmă selectarea ofertei">
                <div><strong>Confirmă furnizorul</strong>
                  <p>{{ entry.supplierName }} · {{ entry.offer.total | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</p>
                  <small>După confirmare, cererea nu va mai primi oferte.
                    {{ entry.offer.deliveryIncluded ? 'Furnizorul va declara livrarea, apoi vei confirma primirea.' : 'Transportul se organizează separat.' }}</small>
                </div>
                <div class="confirm-actions">
                  <button class="secondary" type="button" (click)="cancelSelection()">Renunță</button>
                  <button type="button" (click)="confirmSelection()" [disabled]="busy()">
                    {{ busy() ? 'Se confirmă…' : 'Confirmă și închide cererea' }}
                  </button>
                </div>
              </div>
            } @else if (!request.selectedOfferId) {
              <button type="button" (click)="requestSelection(entry)" [disabled]="busy()">Selectează această ofertă</button>
            }
          </article>
        }
      </div>
    }
    @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
  </section>`,
  styleUrl: './buyer-offer-comparison.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class BuyerOfferComparisonComponent {
  private readonly offerService = inject(OfferService);
  private readonly rfqService = inject(RfqService);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('rfqId') ?? '';
  readonly rfq = signal<Rfq | null>(null);
  readonly offers = signal<BuyerOffer[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly showReopen = signal(false);
  readonly reopenReason = signal('');
  readonly reopenDeadline = signal('');
  readonly originalDeadline = signal('');
  readonly reopenError = signal('');
  readonly pendingOffer = signal<BuyerOffer | null>(null);
  readonly confirmDeliveryPending = signal(false);
  readonly showDeadline = signal(false);
  readonly showCancel = signal(false);
  readonly newDeadline = signal('');
  readonly managementError = signal('');
  readonly deadlineExpired = computed(() => !!this.originalDeadline() && new Date(this.originalDeadline()).getTime() <= Date.now());
  readonly lowestTotal = computed(() => Math.min(...this.offers().map(entry => entry.offer.total)));
  readonly fastestDelivery = computed(() => Math.min(...this.offers().map(entry => entry.offer.deliveryDays)));

  constructor() { this.load(); }

  requestSelection(entry: BuyerOffer): void {
    this.error.set('');
    this.pendingOffer.set(entry);
  }

  cancelSelection(): void {
    this.pendingOffer.set(null);
  }

  canManage(request: Rfq): boolean {
    return request.status === 'Published' || request.status === 'ReceivingOffers';
  }

  statusLabel(status: Rfq['status']): string {
    const labels: Record<Rfq['status'], string> = {
      Draft: 'Ciornă',
      Published: 'Publicată',
      ReceivingOffers: 'Primește oferte',
      OfferSelected: 'Ofertă selectată',
      Closed: 'Închisă',
      Cancelled: 'Anulată'
    };
    return labels[status];
  }

  unitLabel(unit: Rfq['items'][number]['unit']): string {
    const labels: Record<Rfq['items'][number]['unit'], string> = {
      Piece:'buc.', Pack:'pachete', Bag:'saci', Kilogram:'kg', Tonne:'tone', Metre:'m',
      SquareMetre:'m²', CubicMetre:'m³', Litre:'l', Pallet:'paleți', Other:'unități'
    };
    return labels[unit];
  }

  startDeadlineExtension(currentDeadline: string): void {
    const current = new Date(currentDeadline);
    const proposed = new Date(Math.max(current.getTime() + 24 * 60 * 60 * 1000, Date.now() + 24 * 60 * 60 * 1000));
    this.newDeadline.set(this.localDateTime(proposed));
    this.managementError.set('');
    this.showCancel.set(false);
    this.showDeadline.set(true);
  }

  cancelManagement(): void {
    this.showDeadline.set(false);
    this.showCancel.set(false);
    this.managementError.set('');
  }

  extendDeadline(event: Event): void {
    event.preventDefault();
    const request = this.rfq();
    const deadline = new Date(this.newDeadline());
    if (!request || !this.newDeadline() || Number.isNaN(deadline.getTime())) {
      this.managementError.set('Alege un termen nou valid.');
      return;
    }
    if (deadline.getTime() <= new Date(request.quotationDeadline).getTime()) {
      this.managementError.set('Noul termen trebuie să fie ulterior termenului curent.');
      return;
    }
    this.busy.set(true);
    this.managementError.set('');
    this.rfqService.extendDeadline(this.id, deadline.toISOString()).subscribe({
      next: updated => {
        this.rfq.set(updated);
        this.busy.set(false);
        this.showDeadline.set(false);
      },
      error: error => {
        this.busy.set(false);
        this.managementError.set(this.apiError(error, 'Termenul nu a putut fi prelungit.'));
      }
    });
  }

  cancelRequest(): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.managementError.set('');
    this.rfqService.cancel(this.id).subscribe({
      next: updated => {
        this.rfq.set(updated);
        this.busy.set(false);
        this.showCancel.set(false);
      },
      error: error => {
        this.busy.set(false);
        this.managementError.set(this.apiError(error, 'Cererea nu a putut fi anulată.'));
      }
    });
  }

  confirmSelection(): void {
    const entry = this.pendingOffer();
    if (!entry || this.busy()) return;
    this.busy.set(true);
    this.offerService.select(this.id, entry.offer.id).subscribe({
      next: () => {
        this.pendingOffer.set(null);
        this.load();
      },
      error: error => {
        this.busy.set(false);
        this.error.set(error.error?.errors?.offer?.[0] ?? 'Oferta nu a putut fi selectată.');
      }
    });
  }

  confirmDelivery(): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.offerService.confirmDelivery(this.id).subscribe({
      next: () => {
        this.confirmDeliveryPending.set(false);
        this.load();
      },
      error: error => {
        this.busy.set(false);
        this.error.set(error.error?.errors?.offer?.[0] ?? 'Primirea nu a putut fi confirmată.');
      }
    });
  }

  startReopen(deadline: string): void {
    this.originalDeadline.set(deadline);
    this.reopenReason.set('');
    this.reopenDeadline.set('');
    this.reopenError.set('');
    this.showReopen.set(true);
  }

  cancelReopen(): void {
    this.showReopen.set(false);
    this.reopenError.set('');
  }

  reopen(event: Event): void {
    event.preventDefault();
    const reason = this.reopenReason().trim();
    if (reason.length < 10 || (this.deadlineExpired() && !this.reopenDeadline())) return;
    const deadline = this.deadlineExpired() ? new Date(this.reopenDeadline()).toISOString() : null;
    this.busy.set(true);
    this.reopenError.set('');
    this.offerService.reopen(this.id, reason, deadline).subscribe({
      next: () => { this.showReopen.set(false); this.load(); },
      error: error => {
        this.busy.set(false);
        this.reopenError.set(error.error?.errors?.offer?.[0] ?? 'Cererea nu a putut fi redeschisă.');
      }
    });
  }

  private load(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({ request: this.rfqService.get(this.id), offers: this.offerService.forBuyer(this.id) }).subscribe({
      next: result => {
        this.rfq.set(result.request);
        this.offers.set(result.offers);
        this.loading.set(false);
        this.busy.set(false);
      },
      error: error => {
        this.loading.set(false);
        this.busy.set(false);
        this.rfq.set(null);
        this.error.set(error.status === 404
          ? 'Cererea nu există sau nu aparține contului de cumpărător activ.'
          : 'Cererea și ofertele nu au putut fi încărcate. Încearcă din nou.');
      }
    });
  }

  private apiError(error: { error?: { errors?: Record<string, string[]> } }, fallback: string): string {
    const messages = error.error?.errors ? Object.values(error.error.errors).flat() : [];
    return messages[0] ?? fallback;
  }

  private localDateTime(value: Date): string {
    const offset = value.getTimezoneOffset() * 60000;
    return new Date(value.getTime() - offset).toISOString().slice(0, 16);
  }
}
