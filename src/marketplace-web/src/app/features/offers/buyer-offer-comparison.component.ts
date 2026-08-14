import { CurrencyPipe, DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { BuyerOffer, OfferItem, OfferService, OfferTaxTreatment } from '../../core/offers/offer.service';
import { ProcurementNeedAuditEvent, Rfq, RfqService } from '../../core/rfqs/rfq.service';
import { ProcurementOutcomeComponent } from './procurement-outcome.component';
import { unitLabel } from '../../shared/unit-label';
import { TrustFactsComponent } from '../profiles/trust-facts.component';

@Component({
  selector: 'app-buyer-offer-comparison',
  imports: [RouterLink, CurrencyPipe, DatePipe, ProcurementOutcomeComponent, TrustFactsComponent],
  template: `<section class="compare">
    <a class="back" routerLink="/dashboard">← Cererile mele</a>
    @if (rfq(); as request) {
      <header><div><p class="eyebrow">Gestionare cerere</p><h1>{{ request.title }}</h1>
        <p>Urmărește cererea, gestionează termenul și compară ofertele primite.</p></div>
        <div class="header-state"><span class="status">{{ request.fulfillmentStatus === 'Delivered' ? 'Livrată' : statusLabel(request.status) }}</span><span>{{ offers().length }} oferte eligibile</span></div></header>
      <section class="request-overview">
        <div class="overview-heading"><div><p class="eyebrow">Cererea trimisă</p><h2>Stare și detalii</h2></div></div>
        <dl class="request-facts">
          <div><dt>Termen oferte</dt><dd>{{ request.quotationDeadline | date:'dd.MM.yyyy, HH:mm' }}</dd>@if (canManage(request)) { <button type="button" class="contextual-action" (click)="startDeadlineExtension(request.quotationDeadline)">Prelungește termenul</button> }@if (canManage(request)) {
            @if (showDeadline()) { <form class="management-form deadline-form" (submit)="extendDeadline($event)"><label>Termen nou<input type="datetime-local" [value]="newDeadline()" (input)="newDeadline.set($any($event.target).value)"></label>@if (managementError()) { <p class="error" role="alert">{{ managementError() }}</p> }<div class="management-actions"><button type="button" class="secondary" (click)="cancelManagement()">Renunță</button><button type="submit" [disabled]="busy() || !newDeadline()">{{ busy() ? 'Se salvează…' : 'Salvează termenul' }}</button></div></form> }
          }</div>
          <div><dt>Livrare</dt><dd>{{ request.deliveryLocality }}, {{ request.county }}</dd></div>
          <div><dt>Transport în ofertă</dt><dd>{{ request.deliveryRequired ? 'Solicitat' : 'Opțional' }}</dd></div>
          <div><dt>Buget estimat</dt><dd>{{ request.estimatedBudget ? (request.estimatedBudget | currency:'RON':'symbol-narrow':'1.0-2':'ro') : 'Nespecificat' }}</dd></div>
          <div><dt>Îndeplinire</dt><dd>{{ request.allowSplit ? 'Poate fi împărțită' : 'Un singur furnizor' }}</dd></div>
          <div><dt>Origine</dt><dd>{{ inputLabel(request.inputType) }}</dd></div>
        </dl>
        <details class="request-details" open>
          <summary>Vezi cererea completă <span>{{ request.items.length }} {{ request.items.length === 1 ? 'poziție' : 'poziții' }}</span></summary>
          <div class="request-copy">
            @if (request.projectName) { <div><small>Proiect</small><p>{{ request.projectName }}</p></div> }
            @if (request.description) { <div><small>Descriere</small><p>{{ request.description }}</p></div> }
            <div><small>Adresă de livrare</small><p>{{ request.deliveryAddress }}, {{ request.deliveryLocality }}, {{ request.county }}</p></div>
            @if (request.preferredDeliveryDate) { <div><small>Livrare preferată</small><p>{{ request.preferredDeliveryDate | date:'dd.MM.yyyy' }}</p></div> }
            @if (request.buyerNotes) {
              <aside class="supplier-notes" aria-label="Observații pentru furnizori">
                <div class="supplier-notes-heading"><span aria-hidden="true">i</span><div><small>Observații pentru furnizori</small>
                  <p>Aceste detalii sunt vizibile furnizorilor când pregătesc oferta.</p></div></div>
                <p class="supplier-notes-copy">{{ request.buyerNotes }}</p>
              </aside>
            }
          </div>
          @if (request.items.length > 0) {
            <ol class="request-items">
              @for (item of request.items; track item.id; let index = $index) {
                <li><div><strong>{{ item.productName }}</strong>
                  <small>{{ item.kind === 'Service' ? 'Serviciu' : 'Produs' }}</small>
                  @if (item.specification) { <small>{{ item.specification }}</small> }</div>
                  <span>{{ item.quantity }} {{ unitLabel(item.unit) }}</span></li>
              }
            </ol>
          } @else {
            <p class="general-request">Cerere generală, fără poziții individuale.</p>
          }
        </details>
        <details class="request-history">
          <summary>Istoric cerere <span>{{ history().length }} evenimente</span></summary>
          <ol>
            @for (event of history(); track event.id) {
              <li><span class="history-marker" aria-hidden="true"></span><div><strong>{{ eventLabel(event.type) }}</strong>
                <small>{{ event.occurredAt | date:'dd.MM.yyyy, HH:mm' }}</small>
                @if (event.details) { <p>{{ event.details }}</p> }</div></li>
            } @empty { <li><div><span>Istoricul nu este încă disponibil.</span></div></li> }
          </ol>
        </details>
      </section>
      @if (!request.selectedOfferId) {
        <section class="outcome-area"><app-procurement-outcome [request]="request" [offerCount]="offers().length" (recorded)="outcomeRecorded($event)" />
          @if (canManage(request)) { <div class="cancel-prompt"><div><strong>Te-ai răzgândit?</strong><span>Poți opri această cerere dacă nu mai ai nevoie de oferte.</span></div><button type="button" class="danger-link" (click)="showCancel.set(true)">Anulează cererea</button></div>
            @if (showCancel()) { <div class="cancel-confirmation" role="group" aria-label="Confirmă anularea cererii"><div><strong>Anulezi această cerere?</strong><p>Furnizorii nu vor mai putea trimite oferte. Această acțiune nu poate fi anulată.</p></div>@if (managementError()) { <p class="error" role="alert">{{ managementError() }}</p> }<div class="management-actions"><button type="button" class="secondary" (click)="cancelManagement()">Păstrează cererea</button><button type="button" class="danger" (click)="cancelRequest()" [disabled]="busy()">{{ busy() ? 'Se anulează…' : 'Confirmă anularea' }}</button></div></div> }
          }
        </section>
      }
      @if (request.selectedOfferId) {
        <section class="decision">
          <div class="success"><strong>{{ request.fulfillmentStatus === 'Delivered' ? 'Livrarea a fost confirmată.' : request.outcome === 'PurchasedThroughCompaSpot' ? 'Achiziția prin oferta acceptată a fost înregistrată.' : 'Oferta a fost acceptată.' }}</strong>
            <span>{{ request.fulfillmentStatus === 'Delivered' ? 'Cererea este finalizată cu statusul Livrată.' : request.outcome === 'PurchasedThroughCompaSpot' ? 'Cererea este finalizată.' : 'Cererea este închisă pentru alte oferte.' }}</span></div>
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
          } @else if (request.fulfillmentStatus === 'NotApplicable' && !request.outcome) {
            <div class="confirm-selection" role="group" aria-label="Confirmă rezultatul achiziției">
              <div><strong>Cum s-a încheiat achiziția?</strong>
                <p>Confirmă atunci când achiziția prin oferta acceptată s-a finalizat.</p></div>
              <button type="button" (click)="confirmSelectedOfferOutcome()" [disabled]="busy()">
                {{ busy() ? 'Se înregistrează…' : 'Am cumpărat prin această ofertă' }}
              </button>
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
          <div><small>Cel mai mic total comparabil</small><strong>{{ lowestComparableTotal() === null ? '—' : (lowestComparableTotal() | currency:'RON':'symbol-narrow':'1.2-2':'ro') }}</strong></div>
          <div><small>Cel mai scurt termen declarat</small><strong>{{ fastestDelivery() === null ? '—' : fastestDelivery() + ' zile' }}</strong></div>
          <div><small>Oferte complete</small><strong>{{ completeOffers() }} din {{ offers().length }}</strong></div>
        </section>
        @if (request.items.length > 0) {
          <section class="line-comparison"><div><p class="eyebrow">Comparație poziție cu poziție</p><h2>Ce include fiecare ofertă</h2></div>
            <div class="table-scroll"><table><thead><tr><th>Poziție cerută</th>@for (entry of offers(); track entry.offer.id) { <th>{{ entry.supplierName }}</th> }</tr></thead>
              <tbody>@for (item of request.items; track item.id) { <tr><th><strong>{{ item.productName }}</strong><small>{{ item.quantity }} {{ unitLabel(item.unit) }}</small></th>
                @for (entry of offers(); track entry.offer.id) { @if (offeredLine(entry, item.id!); as line) {
                  <td [class.missing]="!line.available"><strong>{{ line.available ? (line.lineTotal | currency:'RON':'symbol-narrow':'1.2-2':'ro') : 'Lipsește' }}</strong>
                    @if (line.available) { <small>{{ line.quantity }} × {{ line.unitPrice | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</small> }
                    @if (line.isAlternative) { <span class="alternative-badge">Alternativă</span> }
                    @if (line.productDescription) { <small>{{ line.productDescription }}</small> }</td>
                } @else if (entry.offer.pricingMode === 'Total') { <td><strong>Inclus</strong><small>în prețul total</small></td>
                } @else { <td class="missing"><strong>Lipsește</strong></td> } }
              </tr> }</tbody></table></div>
          </section>
        }
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
        @for (entry of offers(); track entry.offer.id) {
          <article [class.winner]="entry.offer.status === 'Selected'">
            <div class="top"><div><span class="recommend" [class.partial]="entry.offer.coverage === 'Partial'">{{ entry.offer.coverage === 'Full' ? 'Acoperire completă' : 'Acoperire parțială' }}</span>
              @if (entry.offer.status === 'Selected') { <span class="recommend">Câștigătoare</span> }<h2>{{ entry.supplierName }}</h2><app-trust-facts [facts]="entry.trustFacts" role="Supplier" /></div>
              <div class="price"><small>{{ entry.offer.hasComparableTotal ? 'Total de plată' : 'Total fără transport' }}</small><strong>{{ entry.offer.total | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</strong></div></div>
            <p class="tradeoff">{{ tradeoff(entry) }}</p>
            @if (entry.offer.status === 'Selected') {
              <div class="selected">✓ Ofertă selectată</div>
            } @else if (!request.selectedOfferId && pendingOffer()?.offer?.id === entry.offer.id) {
              <div class="confirm-selection" role="group" aria-label="Confirmă selectarea ofertei">
                <div><strong>Confirmă furnizorul</strong><p>{{ entry.supplierName }} · {{ entry.offer.total | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</p><small>După confirmare, cererea nu va mai primi oferte. {{ entry.offer.deliveryIncluded ? 'Furnizorul va declara livrarea, apoi vei confirma primirea.' : 'Transportul se organizează separat.' }}</small></div>
                <div class="confirm-actions"><button class="secondary" type="button" (click)="cancelSelection()">Renunță</button><button type="button" (click)="confirmSelection()" [disabled]="busy()">{{ busy() ? 'Se confirmă…' : 'Confirmă și închide cererea' }}</button></div>
              </div>
            } @else if (!request.selectedOfferId && entry.offer.coverage === 'Full') {
              <button class="select-offer" type="button" (click)="requestSelection(entry)" [disabled]="busy()">Selectează această ofertă</button>
            } @else if (!request.selectedOfferId) {
              <p class="partial-warning">Oferta nu poate închide singură cererea deoarece lipsesc poziții. Folosește mesajele pentru clarificare.</p>
            }
            <dl>
              @if (entry.offer.pricingMode === 'Total' || request.items.length === 0) {
                <div><dt>Tip ofertă</dt><dd>Preț total · întreaga cerere</dd></div>
              } @else {
                <div><dt>Produse / servicii</dt><dd>{{ entry.offer.itemsTotal | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</dd></div>
                @if (entry.offer.deliveryTerms === 'AddedToTotal') { <div><dt>Cost transport</dt><dd>{{ entry.offer.deliveryCost | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</dd></div> }
              }
              <div><dt>Acoperire</dt><dd>{{ entry.coverageDetails.coveredPositionCount }}/{{ entry.coverageDetails.requestedPositionCount || request.items.length }} poziții</dd></div>
              <div><dt>TVA</dt><dd>{{ taxLabel(entry.offer.taxTreatment, entry.offer.vatRate) }}</dd></div>
              <div><dt>Net</dt><dd>{{ entry.offer.netTotal | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</dd></div>
              <div><dt>TVA calculat</dt><dd>{{ entry.offer.vatAmount | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</dd></div>
              <div><dt>Termen</dt><dd>{{ entry.offer.deliveryDays }} zile</dd></div>
              <div><dt>Transport</dt><dd>{{ deliveryLabel(entry.offer.deliveryTerms) }}</dd></div>
              <div><dt>Valabilă până la</dt><dd>{{ entry.offer.validUntil | date:'dd.MM.yyyy' }}</dd></div>
            </dl>
            @if (entry.offer.paymentTerms) { <div class="terms"><small>Condiții de plată</small><p>{{ entry.offer.paymentTerms }}</p></div> }
            @if (entry.offer.notes) { <div class="terms"><small>Mesaj furnizor</small><p>{{ entry.offer.notes }}</p></div> }
            <a class="conversation-link" [routerLink]="['/offers', entry.offer.id, 'conversation']">Mesaje cu furnizorul →</a>
          </article>
        }
      </div>
    }
    @if (error()) { <div class="error load-error" role="alert"><p>{{ error() }}</p><button type="button" class="secondary" (click)="load()">Încearcă din nou</button></div> }
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
  readonly history = signal<ProcurementNeedAuditEvent[]>([]);
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
  readonly completeOffers = computed(() => this.offers().filter(entry => entry.offer.coverage === 'Full').length);
  readonly lowestComparableTotal = computed(() => {
    const totals = this.offers().filter(entry => entry.offer.coverage === 'Full' && entry.offer.hasComparableTotal).map(entry => entry.offer.total);
    return totals.length === 0 ? null : Math.min(...totals);
  });
  readonly fastestDelivery = computed(() => {
    const days = this.offers().filter(entry => entry.offer.deliveryTerms !== 'ArrangedSeparately').map(entry => entry.offer.deliveryDays);
    return days.length === 0 ? null : Math.min(...days);
  });

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

  inputLabel(inputType: Rfq['inputType']): string {
    const labels: Record<Rfq['inputType'], string> = {
      StructuredForm: 'Formular structurat', FreeText: 'Text liber', ShoppingList: 'Listă de cumpărături', ProjectDescription: 'Descriere proiect'
    };
    return labels[inputType];
  }

  eventLabel(type: ProcurementNeedAuditEvent['type']): string {
    const labels: Record<ProcurementNeedAuditEvent['type'], string> = {
      Created:'Cerere creată', Updated:'Cerere actualizată', Published:'Cerere publicată', OfferReceived:'Ofertă primită',
      DeadlineExtended:'Termen prelungit', OfferSelected:'Ofertă selectată', Reopened:'Cerere redeschisă',
      DeliveryReported:'Livrare declarată', Completed:'Cerere finalizată', Cancelled:'Cerere anulată',
      Migrated:'Cerere existentă preluată', OutcomeRecorded:'Rezultat achiziție înregistrat'
    };
    return labels[type];
  }


  unitLabel(unit: Rfq['items'][number]['unit']): string {
    return unitLabel(unit);
  }

  offeredLine(entry: BuyerOffer, rfqItemId: string): OfferItem | undefined {
    return entry.offer.items.find(item => item.rfqItemId === rfqItemId);
  }

  taxLabel(treatment: OfferTaxTreatment, rate: number | null): string {
    if (treatment === 'VatIncluded') return `Inclus · ${rate ?? 0}%`;
    if (treatment === 'VatExcluded') return `Adăugat · ${rate ?? 0}%`;
    if (treatment === 'NotApplicable') return 'Nu se aplică';
    return 'Nespecificat';
  }

  deliveryLabel(terms: BuyerOffer['offer']['deliveryTerms']): string {
    return ({ IncludedInPrice:'Inclus în preț', AddedToTotal:'Adăugat la total', BuyerPickup:'Ridicare de cumpărător', ArrangedSeparately:'Se stabilește separat' })[terms];
  }

  tradeoff(entry: BuyerOffer): string {
    const parts: string[] = [];
    if (entry.offer.coverage === 'Full') parts.push('Acoperă întreaga cerere');
    else parts.push(`Acoperă ${entry.coverageDetails.coveredPositionCount} din ${entry.coverageDetails.requestedPositionCount} poziții`);
    parts.push(entry.offer.hasComparableTotal ? 'totalul include toate costurile declarate' : 'transportul nu este inclus în total');
    if (entry.offer.items.some(item => item.isAlternative)) parts.push('conține alternative marcate explicit');
    parts.push(`termen ${entry.offer.deliveryDays} zile`);
    return `${parts.join(' · ')}.`;
  }

  startDeadlineExtension(currentDeadline: string): void {
    const current = new Date(currentDeadline);
    const proposed = new Date(Math.max(current.getTime() + 24 * 60 * 60 * 1000, Date.now() + 24 * 60 * 60 * 1000));
    this.newDeadline.set(this.localDateTime(proposed));
    this.managementError.set('');
    this.showCancel.set(false);
    this.showDeadline.set(true);
  }

  outcomeRecorded(request: Rfq): void {
    this.rfq.set(request);
    this.rfqService.getHistory(this.id).subscribe(history => this.history.set(history));
  }

  confirmSelectedOfferOutcome(): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.rfqService.recordOutcome(this.id, 'PurchasedThroughCompaSpot', null).subscribe({
      next: request => {
        this.outcomeRecorded(request);
        this.busy.set(false);
      },
      error: error => {
        this.busy.set(false);
        this.error.set(this.apiError(error, 'Rezultatul achiziției nu a putut fi înregistrat.'));
      }
    });
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

  load(): void {
    this.loading.set(true);
    this.error.set('');
    forkJoin({ request: this.rfqService.get(this.id), offers: this.offerService.forBuyer(this.id), history: this.rfqService.getHistory(this.id) }).subscribe({
      next: result => {
        this.rfq.set(result.request);
        this.offers.set(result.offers);
        this.history.set(result.history);
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

  private apiError(error: { error?: { errors?: Record<string, string[]>; detail?: string } }, fallback: string): string {
    const messages = error.error?.errors ? Object.values(error.error.errors).flat() : [];
    return messages[0] ?? error.error?.detail ?? fallback;
  }

  private localDateTime(value: Date): string {
    const offset = value.getTimezoneOffset() * 60000;
    return new Date(value.getTime() - offset).toISOString().slice(0, 16);
  }
}
