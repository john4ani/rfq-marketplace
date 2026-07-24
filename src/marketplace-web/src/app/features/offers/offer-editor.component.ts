import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Offer, OfferDraft, OfferPricingMode, OfferService } from '../../core/offers/offer.service';
import { OpenRfq } from '../../core/rfqs/open-rfq.service';

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
        @if (request().items.length > 0 && !request().isPartialMatch) {
          <fieldset class="pricing-mode">
            <legend>Cum vrei să prezinți prețul?</legend>
            <label [class.selected]="draft().pricingMode === 'Total'">
              <input type="radio" name="pricingMode" value="Total"
                [ngModel]="draft().pricingMode" (ngModelChange)="setPricingMode($event)">
              <span><strong>Preț total pentru întreaga cerere</strong>
                <small>Rapid, fără să completezi fiecare poziție. Confirmă în mesaj eventualele excepții sau substituții.</small></span>
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
                <div><strong>{{ request().items[index].productName }}</strong>
                  <small>Cerut: {{ request().items[index].quantity }} {{ request().items[index].unit }}</small></div>
                <label>Cantitate<input type="number" min=".001" step=".001" [(ngModel)]="item.quantity"></label>
                <label>Preț unitar (lei)<input type="number" min="0" step=".01" [(ngModel)]="item.unitPrice"></label>
                <label class="available"><input type="checkbox" [(ngModel)]="item.available"> Disponibil</label>
              </div>
            } @empty {
              <p class="notice">Cererea este generală. Introdu valoarea totală a ofertei mai jos.</p>
            }
          </div>
        }

        <div class="terms">
          <label>Livrare (zile)<input type="number" min="0" max="365" [(ngModel)]="draft().deliveryDays"></label>
          @if (draft().pricingMode === 'Itemized') {
            <label>{{ request().items.length ? 'Cost livrare (lei)' : 'Valoare totală ofertă (lei)' }}
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
        <div class="total"><span>Total ofertă</span><strong>{{ total() | currency:'RON':'symbol-narrow':'1.2-2':'ro' }}</strong></div>

        @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
        @if (message()) { <p class="success" role="status">{{ message() }}</p> }
        <div class="actions">
          <button class="secondary" (click)="save()" [disabled]="busy()">Salvează ciorna</button>
          <button (click)="submit()" [disabled]="busy()">Salvează și trimite</button>
          @if (offer()?.status === 'Submitted' || offer()?.status === 'Revised') {
            <button class="danger" (click)="withdraw()" [disabled]="busy()">Retrage</button>
          }
        </div>
      }
    </section>`,
  styles: [`
    :host{display:block;min-width:0;margin-top:2rem;container-type:inline-size}.offer-box{min-width:0;background:#fff;border:1px solid #dfe5e1;border-radius:18px;padding:1.25rem}
    header,.actions,.total{display:flex;justify-content:space-between;gap:1rem;align-items:center}.badge{background:#e9f4ec;color:#185c32;padding:.4rem .7rem;border-radius:99px}
    .pricing-mode{display:grid;grid-template-columns:minmax(0,1fr);gap:.75rem;margin:1rem 0;padding:0;border:0}.pricing-mode legend{grid-column:1/-1;font-weight:800;margin-bottom:.25rem}
    .pricing-mode label{display:flex;align-items:flex-start;gap:.65rem;padding:1rem;border:1px solid #d6dfda;border-radius:12px;background:#fff;cursor:pointer}
    .pricing-mode label.selected{border-color:#176b3a;background:#edf7f1}.pricing-mode input{width:auto;margin-top:.2rem}.pricing-mode span{display:grid;gap:.25rem}.pricing-mode small{color:#56645d}
    .quick-quote{display:grid;grid-template-columns:minmax(0,1fr);gap:1rem;align-items:end;margin:1rem 0;padding:1rem;background:#f3f8f5;border-radius:12px}.quick-quote p{margin:0}
    .lines{display:grid;gap:.75rem;margin:1rem 0}.line{display:grid;grid-template-columns:minmax(0,1fr);gap:.75rem;align-items:end;padding:1rem;background:#f7faf8;border-radius:12px}.line small{display:block}
    .terms{display:grid;grid-template-columns:minmax(0,1fr);gap:.75rem}label{min-width:0;display:grid;gap:.35rem;font-size:.88rem}input,textarea{box-sizing:border-box;min-width:0;width:100%;padding:.7rem;border:1px solid #bdc9c1;border-radius:8px}
    .available{display:flex;align-items:center}.available input{width:auto}.total{margin:1rem 0;padding-top:1rem;border-top:1px solid #ddd}.actions{justify-content:flex-end;flex-wrap:wrap}
    button{border:0;border-radius:9px;padding:.75rem 1rem;background:#176b3a;color:white;font-weight:700}.secondary{background:#eaf1ec;color:#16462b}.danger{background:#a93838}.error{color:#a32626}.success{color:#176b3a}
    @container (min-width:34rem){.pricing-mode,.quick-quote{grid-template-columns:repeat(2,minmax(0,1fr))}.line{grid-template-columns:repeat(2,minmax(0,1fr))}.line>div{grid-column:1/-1}.terms{grid-template-columns:repeat(3,minmax(0,1fr))}}
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
  readonly draft = signal<OfferDraft>({
    pricingMode: 'Total',
    totalPrice: null,
    deliveryCost: 0,
    deliveryDays: 2,
    validUntil: this.validity(),
    paymentTerms: null,
    notes: null,
    items: []
  });
  private initialized = false;

  constructor() {
    effect(() => {
      const request = this.request();
      if (this.initialized) return;
      this.initialized = true;
      this.draft.update(value => ({
        ...value,
        pricingMode: request.items.length > 0 && !request.isPartialMatch ? 'Total' : 'Itemized',
        items: this.requestItems()
      }));
      this.service.getForRequest(request.id).subscribe(value => {
        if (!value) return;
        this.offer.set(value);
        this.draft.set({
          pricingMode: value.pricingMode,
          totalPrice: value.totalPrice,
          deliveryCost: value.deliveryCost,
          deliveryDays: value.deliveryDays,
          validUntil: value.validUntil,
          paymentTerms: value.paymentTerms,
          notes: value.notes,
          items: value.items.length > 0 ? value.items : this.requestItems()
        });
      });
    });
  }

  editable(): boolean {
    return !['Withdrawn', 'Selected', 'NotSelected'].includes(this.offer()?.status ?? '');
  }

  setPricingMode(mode: OfferPricingMode): void {
    this.draft.update(value => ({
      ...value,
      pricingMode: mode,
      totalPrice: mode === 'Total' ? value.totalPrice : null,
      items: mode === 'Itemized' && value.items.length === 0 ? this.requestItems() : value.items
    }));
  }

  total(): number {
    if (this.draft().pricingMode === 'Total') return this.draft().totalPrice ?? 0;
    return this.draft().items.reduce((sum, item) => sum + item.quantity * item.unitPrice, 0) + this.draft().deliveryCost;
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
    this.busy.set(true);
    this.error.set('');
    const value = this.draft();
    const payload = {
      ...value,
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
        this.error.set(error.error?.errors?.offer?.[0] ?? 'Oferta nu a putut fi salvată.');
      }
    });
  }

  submit(): void {
    this.save(() => {
      this.busy.set(true);
      this.service.submit(this.request().id).subscribe({
        next: offer => {
          this.offer.set(offer);
          this.busy.set(false);
          this.message.set('Oferta a fost trimisă cumpărătorului.');
        },
        error: error => {
          this.busy.set(false);
          this.error.set(error.error?.errors?.offer?.[0] ?? 'Oferta nu a putut fi trimisă.');
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
      available: true
    }));
  }

  private validity(): string {
    const date = new Date();
    date.setDate(date.getDate() + 14);
    return date.toISOString().slice(0, 10);
  }
}
