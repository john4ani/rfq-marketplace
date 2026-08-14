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
      @if (!summary()!.phoneVerified || (summary()!.roles.includes('Supplier') && !summary()!.companyVerified)) {
        <section class="verification-actions" aria-label="Acțiuni de verificare">
          @if (!summary()!.phoneVerified) { <div><strong>Telefon neconfirmat</strong><span>Verificarea este făcută manual pentru MVP; nu îți poți confirma singur numărul.</span><a [href]="verificationRequestLink('telefon')">Solicită verificarea telefonului</a></div> }
          @if (summary()!.roles.includes('Supplier') && !summary()!.companyVerified) { <div><strong>Companie neconfirmată</strong><span>Verificăm datele firmei înainte de a afișa semnalul „Companie verificată”.</span><a [href]="verificationRequestLink('companie')">Solicită verificarea companiei</a></div> }
        </section>
      }
      @if (emailMessage()) { <p [class.error]="emailError()" role="status">{{ emailMessage() }}</p> }
    } @else { <div class="trust-error" role="alert"><p>Starea contului nu a putut fi încărcată.</p><button type="button" class="secondary" (click)="load()">Încearcă din nou</button></div> }
  </section>`,
  styles: [`.trust-card{margin:1.25rem 0;padding:1.25rem;border:1px solid var(--border,#d8dce5);border-radius:.75rem;background:var(--surface,#fff)}.trust-facts{display:grid;grid-template-columns:repeat(auto-fit,minmax(9rem,1fr));gap:.75rem;margin:1rem 0}.trust-facts div{padding:.7rem;background:#f6f8fb;border-radius:.5rem}.trust-facts dt{font-size:.85rem;color:#536070}.trust-facts dd{margin:.2rem 0 0;font-weight:700}.trust-explainer{color:#536070}.verification-actions{display:grid;gap:.65rem;margin-top:1rem}.verification-actions div{display:grid;gap:.3rem;padding:.85rem;border:1px solid var(--border,#d8dce5);border-radius:.65rem;background:var(--warm-white,#faf7f2)}.verification-actions span{color:var(--muted,#536070);line-height:1.4}.verification-actions a{justify-self:start;color:var(--forest,#1e3d2f);font-weight:750}`],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class TrustSummaryComponent {
  private readonly profiles = inject(ProfileService); readonly refreshVersion = input(0); readonly summary = signal<AccountTrustSummary | null>(null); readonly loading = signal(true); readonly sendingEmail = signal(false); readonly emailMessage = signal(''); readonly emailError = signal(false);
  constructor() { effect(() => { this.refreshVersion(); this.load(); }); }
  load(): void { this.loading.set(true); this.summary.set(null); this.profiles.getTrustSummary().subscribe({ next: value => { this.summary.set(value); this.loading.set(false); }, error: () => this.loading.set(false) }); }
  requestEmailConfirmation(): void { this.sendingEmail.set(true); this.emailMessage.set(''); this.profiles.requestEmailConfirmation().subscribe({ next: result => { this.sendingEmail.set(false); this.emailError.set(!result.emailSent && !result.alreadyConfirmed); this.emailMessage.set(result.alreadyConfirmed ? 'Adresa de e-mail este deja confirmată.' : result.emailSent ? 'Ți-am trimis e-mailul de confirmare. Deschide linkul pentru a actualiza starea contului.' : 'E-mailul de confirmare nu a fost trimis. Încearcă mai târziu.'); }, error: () => { this.sendingEmail.set(false); this.emailError.set(true); this.emailMessage.set('E-mailul nu a putut fi trimis acum. Încearcă mai târziu.'); } }); }
  verificationRequestLink(subject: 'telefon' | 'companie'): string { const email = this.summary()?.email ?? ''; const label = subject === 'telefon' ? 'telefonului' : 'companiei'; return `mailto:support@compaspot.com?subject=${encodeURIComponent(`Solicitare verificare ${label}`)}&body=${encodeURIComponent(`Solicit verificarea ${label} pentru contul ${email}.`)}`; }
}
