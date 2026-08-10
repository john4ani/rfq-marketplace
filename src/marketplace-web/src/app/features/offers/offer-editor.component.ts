import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Offer, OfferDeliveryTerms, OfferDraft, OfferPricingMode, OfferService, OfferTaxTreatment } from '../../core/offers/offer.service';
import { OpenRfq } from '../../core/rfqs/open-rfq.service';
import { RfqItem } from '../../core/rfqs/rfq.service';
import { unitLabel } from '../../shared/unit-label';

@Component({
  selector: 'app-offer-editor',
  imports: [FormsModule, CurrencyPipe],
  template: `
    <section class="offer-box">
      <header>
        <div><p class="eyebrow">Oferta ta</p><h2>{{ offer() ? 'Actualizează oferta' : 'Trimite o ofertă' }}</h2></div>
        @if (offer(); as currentOffer) { <span class="badge">{{ label(currentOffer.status) }}</span> }
      </header>

      @if (offer()?.status === 'Selected') { <p class="success">Oferta ta a fost selectată.</p> }
      @if (offer()?.status === 'NotSelected') { <p class="notice">Cumpărătorul a selectat o altă ofertă.</p> }

      @if (editable()) {
        @if (hasPricingModeChoice()) {
          <fieldset class="pricing-mode">
            <legend>Cum vrei să prezinți prețul?</legend>
            <label [class.selected]="draft().pricingMode === 'Total'">
              <input type="radio" name="pricingMode" value="Total"
                [ngModel]="draft().pricingMode" (ngModelChange)="setPricingMode($event)">
              <span><strong>Preț total pentru întreaga cerere</strong>
                <small>Rapid, fără să completezi fiecare poziție. Pentru substituții sau lipsuri folosește varianta detaliată.</small></span>
            </label>
            <label [class.selected]="draft().pricingMode === 'Itemized'">
              <input type="radio" name="pricingMode" value="Itemized"
                [ngModel]="draft().pricingMode" (ngModelChange)="setPricingMode($event)">
              <span><strong>Preț pe fiecare poziție</strong>
                <small>Folosește varianta detaliată când vrei să arăți cantități, disponibilitate și prețuri unitare.</small></span>
            </label>
          </fieldset>
        }

        @if (draft().pricingMode === 'Total') {
          <div class="quick-quote">
            <p><strong>Oferta acoperă întreaga cerere.</strong> Nu este necesar un catalog de produse.</p>
            <label>Preț total, inclusiv livrarea (lei) *
              <input type="number" min=".01" step=".01" [(ngModel)]="draft().totalPrice">
            </label>
          </div>
        } @else {
          <div class="lines">
            @for (item of draft().items; track item.rfqItemId; let index = $index) {
              <div class="line">
                <div><strong>{{ requestedItem(item.rfqItemId)?.productName }}</strong>
                  @if (requestedItem(item.rfqItemId); as requested) {
                    <small>Cerut: {{ requested.quantity }} {{ unitLabel(requested.unit) }}</small>
                  }
                  <small class="line-total">Valoare poziție: {{ lineTotal(index) | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</small></div>
                <label>Cantitate<input type="number" min=".001" step=".001" [disabled]="!item.available" [(ngModel)]="item.quantity"></label>
                <label>Preț unitar (lei)<input type="number" min="0" step=".01" [disabled]="!item.available" [(ngModel)]="item.unitPrice"></label>
                <label class="available"><input type="checkbox" [(ngModel)]="item.available"> Disponibil</label>
                @if (item.available) {
                  <label class="description">Produs / serviciu oferit
                    <input [(ngModel)]="item.productDescription" placeholder="Denumire, marcă sau specificație"></label>
                  @if (requestedItem(item.rfqItemId)?.alternativesAccepted && request().alternativeProductsAccepted) {
                    <label class="available alternative"><input type="checkbox" [(ngModel)]="item.isAlternative"> Este o alternativă</label>
                  }
                  @if (requestedItem(item.rfqItemId); as requested) {
                    @if (item.quantity < requested.quantity) { <small class="excluded">Cantitate parțială: lipsesc {{ requested.quantity - item.quantity }} {{ unitLabel(requested.unit) }}.</small> }
                  }
                } @else { <small class="excluded">Poziția lipsește din ofertă și nu intră în total.</small> }
              </div>
            } @empty {
              <p class="notice">Cererea este generală. Introdu valoarea totală a ofertei mai jos.</p>
            }
          </div>
          <div class="coverage" [class.partial]="coverage() === 'Partial'">
            <strong>{{ coverage() === 'Full' ? 'Ofertă completă' : 'Ofertă parțială' }}</strong>
            <span>{{ request().isPartialMatch ? 'Acoperă pozițiile potrivite profilului tău; cumpărătorul va vedea ce rămâne neacoperit.' : coveredCount() + ' din ' + request().items.length + ' poziții sunt acoperite integral.' }}</span>
          </div>
        }

        <div class="commercial-grid">
          <label>Transport *<select [ngModel]="draft().deliveryTerms" (ngModelChange)="setDeliveryTerms($event)">
            <option value="IncludedInPrice">Inclus în preț</option>
            @if (draft().pricingMode === 'Itemized') { <option value="AddedToTotal">Adăugat separat la total</option> }
            @if (!request().deliveryRequired && draft().pricingMode === 'Itemized') {
              <option value="BuyerPickup">Ridicare de către cumpărător</option>
              <option value="ArrangedSeparately">Se stabilește separat</option>
            }
          </select><small>{{ request().deliveryRequired ? 'Cumpărătorul a solicitat transport.' : 'Spune clar cum se organizează transportul.' }}</small></label>
          <label>TVA *<select [ngModel]="draft().taxTreatment" (ngModelChange)="setTaxTreatment($event)">
            <option value="VatIncluded">TVA inclus în prețuri</option>
            <option value="VatExcluded">TVA se adaugă la prețuri</option>
            <option value="NotApplicable">Neplătitor de TVA</option>
          </select></label>
          @if (draft().taxTreatment !== 'NotApplicable') {
            <label>Cotă TVA (%) *<input type="number" min="0" max="100" step=".01" [(ngModel)]="draft().vatRate"></label>
          }
        </div>

        <div class="terms">
          @if (draft().deliveryTerms !== 'ArrangedSeparately') {
            <label>{{ draft().deliveryTerms === 'BuyerPickup' ? 'Disponibil pentru ridicare în (zile)' : 'Livrare în (zile)' }}<input type="number" min="0" max="365" [(ngModel)]="draft().deliveryDays"></label>
          }
          @if (draft().pricingMode === 'Itemized' && draft().deliveryTerms === 'AddedToTotal') {
            <label>Cost transport (lei)
              <input type="number" min="0" step=".01" [(ngModel)]="draft().deliveryCost">
            </label>
          }
          <label>Valabilă până la<input type="date" [(ngModel)]="draft().validUntil"></label>
          <label>Condiții de plată<input [(ngModel)]="draft().paymentTerms" placeholder="Ex: 30% avans"></label>
        </div>

        <label>Mesaj pentru cumpărător
          <textarea rows="3" [(ngModel)]="draft().notes"
            placeholder="Confirmă acoperirea integrală și menționează orice excepție, substituție sau condiție de stoc."></textarea>
        </label>
        <div class="total"><span><small>Net {{ netTotal() | currency:'RON':'symbol-narrow':'1.2-2':'ro' }} · TVA {{ vatAmount() | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</small>Total de plată</span><strong>{{ total() | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</strong></div>
        @if (draft().deliveryTerms === 'ArrangedSeparately') { <p class="notice">Totalul nu include transportul și nu va fi comparat direct cu ofertele care îl includ.</p> }

        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        @if (message()) { <p class="success" role="status">{{ message() }}</p> }
        <div class="actions">
          <button class="secondary" (click)="save()" [disabled]="busy()">Salvează ciorna</button>
          <button (click)="confirmingSubmission.set(true)" [disabled]="busy()">Verifică înainte de trimitere</button>
          @if (offer()?.status === 'Submitted' || offer()?.status === 'Revised') {
            <button class="danger" (click)="withdraw()" [disabled]="busy()">Retrage</button>
          }
        </div>
        @if (confirmingSubmission()) { <section class="submit-confirmation" role="group" aria-label="Confirmă trimiterea ofertei"><strong>Trimiți această ofertă cumpărătorului?</strong><span>Oferta poate fi actualizată cât timp cererea este deschisă.</span><div><button type="button" class="secondary" (click)="confirmingSubmission.set(false)">Mai verific</button><button type="button" (click)="submit()" [disabled]="busy()">{{ busy() ? 'Se trimite…' : 'Trimite oferta' }}</button></div></section> }
      }
    </section>`,
  styles: [`
    :host{display:block;min-width:0;margin-top:2rem;container-type:inline-size}.offer-box{min-width:0;background:#fff;border:1px solid #dfe5e1;border-radius:18px;padding:1.25rem}
    header,.actions,.total{display:flex;justify-content:space-between;gap:1rem;align-items:center}.badge{background:#e9f4ec;color:#185c32;padding:.4rem .7rem;border-radius:99px}
    .pricing-mode{display:grid;grid-template-columns:minmax(0,1fr);gap:.75rem;margin:1rem 0;padding:0;border:0}.pricing-mode legend{grid-column:1/-1;font-weight:800;margin-bottom:.25rem}
    .pricing-mode label{display:flex;align-items:flex-start;gap:.65rem;padding:1rem;border:1px solid #d6dfda;border-radius:12px;background:#fff;cursor:pointer}
    .pricing-mode label.selected{border-color:#176b3a;background:#edf7f1}.pricing-mode label.unavailable{cursor:not-allowed;opacity:.62}.pricing-mode input{width:auto;margin-top:.2rem}.pricing-mode span{display:grid;gap:.25rem}.pricing-mode small{color:#56645d}
    .quick-quote{display:grid;grid-template-columns:minmax(0,1fr);gap:1rem;align-items:end;margin:1rem 0;padding:1rem;background:#f3f8f5;border-radius:12px}.quick-quote p{margin:0}
    .lines{display:grid;gap:.75rem;margin:1rem 0}.line{display:grid;grid-template-columns:minmax(0,1fr);gap:.75rem;align-items:end;padding:1rem;background:#f7faf8;border-radius:12px}.line small{display:block}.line-total{margin-top:.35rem;color:#185c32;font-weight:800}
    .terms,.commercial-grid{display:grid;grid-template-columns:minmax(0,1fr);gap:.75rem;margin:1rem 0}label{min-width:0;display:grid;gap:.35rem;font-size:.88rem}input,select,textarea{box-sizing:border-box;min-width:0;width:100%;padding:.7rem;border:1px solid #bdc9c1;border-radius:8px;background:#fff}
    .available{display:flex;align-items:center}.available input{width:auto}.description{grid-column:1/-1}.excluded{color:#7a4b16}.coverage{display:flex;justify-content:space-between;gap:.75rem;padding:.8rem 1rem;border-radius:.7rem;color:#185c32;background:#e9f4ec}.coverage.partial{color:#704d0d;background:#fff6dc}.total{margin:1rem 0;padding-top:1rem;border-top:1px solid #ddd}.total span{display:grid}.total small{color:#56645d;font-weight:500}.actions{justify-content:flex-end;flex-wrap:wrap}
    button{border:0;border-radius:9px;padding:.75rem 1rem;background:#176b3a;color:white;font-weight:700}.secondary{background:#eaf1ec;color:#16462b}.danger{background:#a93838}.error{color:#a32626}.success{color:#176b3a}.submit-confirmation{display:grid;gap:.5rem;margin-top:1rem;padding:1rem;border:1px solid #d6a84c;border-radius:.75rem;background:#fff9e9}.submit-confirmation span{color:#655b42;font-size:.88rem}.submit-confirmation div{display:flex;justify-content:flex-end;gap:.5rem}
    @container (min-width:34rem){.pricing-mode,.quick-quote{grid-template-columns:repeat(2,minmax(0,1fr))}.line{grid-template-columns:repeat(2,minmax(0,1fr))}.line>div{grid-column:1/-1}.terms,.commercial-grid{grid-template-columns:repeat(3,minmax(0,1fr))}}
    @container (min-width:50rem){.line{grid-template-columns:minmax(180px,1fr) 140px 150px 110px}.line>div{grid-column:auto}}
    @media(max-width:460px){.actions{align-items:stretch;flex-direction:column}.actions button{width:100%}}
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class OfferEditorComponent {
  readonly request = input.required<OpenRfq>();
  private readonly service = inject(OfferService);
  readonly offer = signal<Offer | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly confirmingSubmission = signal(false);
  readonly unitLabel = unitLabel;
  readonly draft = signal<OfferDraft>({
    pricingMode: 'Total',
    totalPrice: null,
    deliveryIncluded: true,
    deliveryCost: 0,
    deliveryDays: 2,
    validUntil: this.validity(),
    paymentTerms: null,
    notes: null,
    items: [],
    coverage: 'Full',
    taxTreatment: 'VatIncluded',
    vatRate: 19,
    deliveryTerms: 'IncludedInPrice'
  });
  private initialized = false;

  constructor() {
    effect(() => {
      const request = this.request();
      if (this.initialized) return;
      this.initialized = true;
      this.draft.update(value => ({
        ...value,
        pricingMode: request.isPartialMatch ? 'Itemized' : 'Total',
        deliveryIncluded: request.deliveryRequired || value.deliveryIncluded,
        coverage: request.isPartialMatch ? 'Partial' : 'Full',
        deliveryTerms: request.isPartialMatch ? 'AddedToTotal' : 'IncludedInPrice',
        items: this.requestItems()
      }));
      this.service.getForRequest(request.id).subscribe(value => {
        if (!value) return;
        const normalizeSimpleOffer = this.mustUseTotalPricing(request)
          && value.pricingMode === 'Itemized';
        this.offer.set(value);
        this.draft.set({
          pricingMode: normalizeSimpleOffer ? 'Total' : value.pricingMode,
          totalPrice: normalizeSimpleOffer ? value.total : value.totalPrice,
          deliveryIncluded: value.deliveryIncluded,
          deliveryCost: normalizeSimpleOffer ? 0 : value.deliveryCost,
          deliveryDays: value.deliveryDays,
          validUntil: value.validUntil,
          paymentTerms: value.paymentTerms,
          notes: value.notes,
          coverage: value.coverage ?? this.coverage(),
          taxTreatment: value.taxTreatment === 'NotSpecified' ? 'VatIncluded' : value.taxTreatment,
          vatRate: value.taxTreatment === 'NotApplicable' ? null : (value.vatRate ?? 19),
          deliveryTerms: value.deliveryTerms ?? (value.deliveryIncluded ? (value.pricingMode === 'Total' ? 'IncludedInPrice' : 'AddedToTotal') : 'BuyerPickup'),
          items: value.items.length > 0 ? this.orderItems(value.items) : this.requestItems()
        });
      });
    });
  }

  editable(): boolean {
    return !['Withdrawn', 'Selected', 'NotSelected'].includes(this.offer()?.status ?? '');
  }

  setPricingMode(mode: OfferPricingMode): void {
    if (mode === 'Itemized' && this.mustUseTotalPricing()) return;
    this.draft.update(value => ({
      ...value,
      pricingMode: mode,
      totalPrice: mode === 'Total' ? value.totalPrice : null,
      coverage: mode === 'Total' ? 'Full' : value.coverage,
      deliveryTerms: mode === 'Total' ? 'IncludedInPrice' : value.deliveryTerms,
      deliveryIncluded: mode === 'Total' || value.deliveryTerms === 'AddedToTotal',
      deliveryCost: mode === 'Total' ? 0 : value.deliveryCost,
      items: mode === 'Itemized' && value.items.length === 0 ? this.requestItems() : value.items
    }));
  }

  setDeliveryTerms(terms: OfferDeliveryTerms): void {
    if ((this.request().deliveryRequired || this.draft().pricingMode === 'Total')
      && (terms === 'BuyerPickup' || terms === 'ArrangedSeparately')) return;
    this.draft.update(value => ({
      ...value,
      deliveryTerms: terms,
      deliveryIncluded: terms === 'IncludedInPrice' || terms === 'AddedToTotal',
      deliveryCost: terms === 'AddedToTotal' ? value.deliveryCost : 0,
      deliveryDays: terms === 'ArrangedSeparately' ? 0 : Math.max(value.deliveryDays, 1)
    }));
  }

  setTaxTreatment(treatment: OfferTaxTreatment): void {
    this.draft.update(value => ({ ...value, taxTreatment: treatment, vatRate: treatment === 'NotApplicable' ? null : (value.vatRate ?? 19) }));
  }

  coveredCount(): number {
    if (this.draft().pricingMode === 'Total') return this.request().items.length;
    return this.draft().items.filter(item => {
      const requested = this.requestedItem(item.rfqItemId);
      return requested && item.available && item.quantity >= requested.quantity;
    }).length;
  }

  coverage(): 'Full' | 'Partial' {
    if (this.request().isPartialMatch) return 'Partial';
    return this.draft().pricingMode === 'Total' || this.coveredCount() === this.request().items.length ? 'Full' : 'Partial';
  }

  total(): number {
    const subtotal = this.quotedSubtotal();
    return this.draft().taxTreatment === 'VatExcluded' ? subtotal + this.vatAmount() : subtotal;
  }

  netTotal(): number { return this.draft().taxTreatment === 'VatIncluded' ? this.quotedSubtotal() - this.vatAmount() : this.quotedSubtotal(); }

  vatAmount(): number {
    const subtotal = this.quotedSubtotal(), rate = this.draft().vatRate ?? 0;
    if (this.draft().taxTreatment === 'VatExcluded') return this.round(subtotal * rate / 100);
    if (this.draft().taxTreatment === 'VatIncluded') return this.round(subtotal - subtotal / (1 + rate / 100));
    return 0;
  }

  lineTotal(index: number): number {
    const item = this.draft().items[index];
    return item?.available ? this.round(item.quantity * item.unitPrice) : 0;
  }

  requestedItem(rfqItemId: string): RfqItem | undefined {
    return this.request().items.find(item => item.id === rfqItemId);
  }

  label(status: string): string {
    return ({
      Draft: 'Ciornă',
      Submitted: 'Trimisă',
      Revised: 'Revizuită',
      Withdrawn: 'Retrasă',
      Selected: 'Selectată',
      NotSelected: 'Neselectată'
    } as Record<string, string>)[status];
  }

  save(after?: () => void): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.error.set('');
    this.message.set('');
    const value = this.draft();
    const payload = {
      ...value,
      coverage: this.coverage(),
      items: value.pricingMode === 'Total' ? [] : value.items
    };
    this.service.save(this.request().id, payload).subscribe({
      next: offer => {
        this.offer.set(offer);
        this.busy.set(false);
        this.message.set('Oferta a fost salvată.');
        after?.();
      },
      error: error => {
        this.busy.set(false);
        this.error.set(error.error?.errors?.offer?.[0] ?? error.error?.detail ?? 'Oferta nu a putut fi salvată.');
      }
    });
  }

  submit(): void {
    if (this.busy()) return;
    this.save(() => {
      this.busy.set(true);
      this.service.submit(this.request().id).subscribe({
        next: offer => {
          this.offer.set(offer);
          this.confirmingSubmission.set(false);
          this.busy.set(false);
          this.message.set('Oferta a fost trimisă cumpărătorului.');
        },
        error: error => {
          this.busy.set(false);
          this.error.set(error.error?.errors?.offer?.[0] ?? error.error?.detail ?? 'Oferta nu a putut fi trimisă.');
        }
      });
    });
  }

  withdraw(): void {
    const offer = this.offer();
    if (!offer || !confirm('Retragi această ofertă?')) return;
    this.busy.set(true);
    this.service.withdraw(offer.id).subscribe({
      next: value => {
        this.offer.set(value);
        this.busy.set(false);
      },
      error: () => {
        this.busy.set(false);
        this.error.set('Oferta nu a putut fi retrasă.');
      }
    });
  }

  private requestItems() {
    return this.request().items.map(item => ({
      rfqItemId: item.id!,
      quantity: item.quantity,
      unitPrice: 0,
      productDescription: null,
      available: true,
      isAlternative: false
    }));
  }

  hasPricingModeChoice(): boolean {
    return !this.mustUseTotalPricing();
  }

  private mustUseTotalPricing(request = this.request()): boolean {
    return !request.isPartialMatch && this.commercialPositionCount(request) <= 1;
  }

  private commercialPositionCount(request: OpenRfq): number {
    return request.items.filter(item => !this.isDeliveryService(item)).length;
  }

  private isDeliveryService(item: RfqItem): boolean {
    return item.kind === 'Service'
      && /(^|\s)(livrare|transport)(\s|$)/i.test(item.productName);
  }

  private orderItems(items: Offer['items']): Offer['items'] {
    const byRequestItem = new Map(items.map(item => [item.rfqItemId, item]));
    return this.request().items.flatMap(item => {
      const offerItem = item.id ? byRequestItem.get(item.id) : undefined;
      return offerItem ? [offerItem] : [];
    });
  }

  private quotedSubtotal(): number {
    if (this.draft().pricingMode === 'Total') return this.draft().totalPrice ?? 0;
    const lines = this.draft().items.reduce((sum, item) => sum + (item.available ? item.quantity * item.unitPrice : 0), 0);
    return lines + (this.draft().deliveryTerms === 'AddedToTotal' ? this.draft().deliveryCost : 0);
  }

  private round(value: number): number { return Math.round((value + Number.EPSILON) * 100) / 100; }

  private validity(): string {
    const date = new Date();
    date.setDate(date.getDate() + 14);
    return date.toISOString().slice(0, 10);
  }
}
