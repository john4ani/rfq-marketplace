import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { ProfileService, AccountTrustSummary } from '../../core/profiles/profile.service';

@Component({
  selector: 'app-trust-summary',
  template: `<section class="trust-card" aria-labelledby="trust-title">
    <div><p class="eyebrow">Încredere și transparență</p><h2 id="trust-title">Starea contului</h2></div>
    @if (loading()) { <p role="status">Se încarcă starea contului…</p> }
    @else if (summary()) { <p class="trust-explainer">Arătăm doar fapte verificabile, nu scoruri sau evaluări publice.</p>
      <dl class="trust-facts"><div><dt>Profil complet</dt><dd>{{ summary()!.profileCompletenessPercent }}%</dd></div><div><dt>E-mail</dt><dd>{{ summary()!.emailVerified ? 'Confirmat' : 'Neconfirmat' }}</dd></div><div><dt>Telefon</dt><dd>{{ summary()!.phoneVerified ? 'Confirmat' : 'Neconfirmat' }}</dd></div>@if(summary()!.roles.includes('Supplier')) { <div><dt>Companie</dt><dd>{{ summary()!.companyVerified ? 'Verificată' : 'Neconfirmată' }}</dd></div> }</dl>
      <p class="trust-explainer">Activitate: {{ summary()!.publishedRequests }} cereri publicate · {{ summary()!.submittedOffers }} oferte trimise@if(summary()!.selectedOffers) { · {{ summary()!.selectedOffers }} oferte selectate }</p>
      @if (!summary()!.emailVerified) { <button type="button" class="secondary" (click)="requestEmailConfirmation()" [disabled]="sendingEmail()">{{ sendingEmail() ? 'Se trimite…' : 'Trimite e-mailul de confirmare' }}</button> }
      @if (emailMessage()) { <p [class.error]="emailError()" role="status">{{ emailMessage() }}</p> }
    } @else { <div class="trust-error" role="alert"><p>Starea contului nu a putut fi încărcată.</p><button type="button" class="secondary" (click)="load()">Încearcă din nou</button></div> }
  </section>`,
  styles: [`.trust-card{margin:1.25rem 0;padding:1.25rem;border:1px solid var(--border,#d8dce5);border-radius:.75rem;background:var(--surface,#fff)}.trust-facts{display:grid;grid-template-columns:repeat(auto-fit,minmax(9rem,1fr));gap:.75rem;margin:1rem 0}.trust-facts div{padding:.7rem;background:#f6f8fb;border-radius:.5rem}.trust-facts dt{font-size:.85rem;color:#536070}.trust-facts dd{margin:.2rem 0 0;font-weight:700}.trust-explainer{color:#536070}`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TrustSummaryComponent {
  private readonly profiles = inject(ProfileService); readonly refreshVersion = input(0); readonly summary = signal<AccountTrustSummary | null>(null); readonly loading = signal(true); readonly sendingEmail = signal(false); readonly emailMessage = signal(''); readonly emailError = signal(false);
  constructor() { effect(() => { this.refreshVersion(); this.load(); }); }
  load(): void { this.loading.set(true); this.summary.set(null); this.profiles.getTrustSummary().subscribe({ next: value => { this.summary.set(value); this.loading.set(false); }, error: () => this.loading.set(false) }); }
  requestEmailConfirmation(): void { this.sendingEmail.set(true); this.emailMessage.set(''); this.profiles.requestEmailConfirmation().subscribe({ next: () => { this.sendingEmail.set(false); this.emailError.set(false); this.emailMessage.set('Ți-am trimis un e-mail de confirmare. Deschide linkul pentru a actualiza starea contului.'); }, error: () => { this.sendingEmail.set(false); this.emailError.set(true); this.emailMessage.set('E-mailul nu a putut fi trimis acum. Încearcă mai târziu.'); } }); }
}
