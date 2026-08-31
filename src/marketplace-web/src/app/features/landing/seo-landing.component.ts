import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { FeaturesService } from '../../core/features/features.service';
import { LanguageService } from '../../core/i18n/language.service';
import { LiveMarketComponent } from './live-market.component';

/**
 * Public landing. With the marketplace enabled it delegates to the live marketplace (Piața Live);
 * with MarketPlace:Enabled=false (MVP) it becomes a SEAP-focused SEO landing page targeting the
 * purchase-intent keywords (redactare ofertă tehnică SEAP, model propunere tehnică construcții, ...).
 * Authenticated users are sent straight to Anunțuri SEAP.
 */
@Component({
  selector: 'app-seo-landing',
  imports: [RouterLink, LiveMarketComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`:host{display:block}.seo-landing{display:grid;gap:2.2rem;max-width:72rem;margin:0 auto;padding:2rem 1.2rem 3rem}.hero{display:grid;gap:1.2rem;padding:2.4rem 2rem;border:1px solid var(--border);border-radius:1.2rem;background:linear-gradient(135deg,var(--forest),var(--forest-light));color:var(--warm-white)}.hero p.eyebrow{margin:0;color:var(--sand);font-size:.78rem;font-weight:800;letter-spacing:.12em;text-transform:uppercase}.hero h1{margin:.2rem 0;font-size:clamp(1.9rem,4.6vw,3rem);letter-spacing:-.04em;color:#fff}.hero p.lead{margin:0;font-size:1.05rem;line-height:1.6;color:color-mix(in srgb,var(--warm-white) 92%,transparent)}.hero-actions{display:flex;flex-wrap:wrap;gap:.7rem;align-items:center}.primary-action{display:inline-flex;padding:.7rem 1.15rem;border-radius:.65rem;background:var(--warm-amber);color:var(--ink);font-weight:800;text-decoration:none}.secondary-action{display:inline-flex;padding:.7rem 1.15rem;border-radius:.65rem;border:1px solid color-mix(in srgb,var(--warm-white) 45%,transparent);color:var(--warm-white);font-weight:800;text-decoration:none}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(17rem,1fr));gap:1rem}.card{padding:1.25rem;border:1px solid var(--border);border-radius:1rem;background:var(--surface);box-shadow:var(--shadow-soft)}.card h2{margin:0;font-size:1.08rem}.card p{margin:.5rem 0 0;color:var(--muted);line-height:1.55;font-size:.92rem}.terms-note{margin-top:1.4rem;font-size:.8rem;color:var(--muted);line-height:1.5}.terms-note a{color:var(--forest);font-weight:700}`],
  template: `@if (features.marketplaceEnabled()) {
    <app-live-market />
  } @else {
    <section class="seo-landing">
      <div class="hero">
        <p class="eyebrow">{{ i18n.t('Achiziții publice · Ofertare asistată de AI') }}</p>
        <h1>{{ i18n.t('Redactare ofertă tehnică SEAP, rapid și profesional') }}</h1>
        <p class="lead">{{ i18n.t('Generăm propuneri tehnice și liste de cantități pentru licitațiile publice din SEAP — pe baza caietului de sarcini și a datelor firmei tale. Model propunere tehnică construcții, deviz de cantități, consultanță achiziții publice asistată de inteligență artificială.') }}</p>
        <div class="hero-actions">
          @if (auth.isAuthenticated()) {
            <a class="primary-action" routerLink="/seap/tenders">{{ i18n.t('Deschide Anunțuri SEAP') }}</a>
          } @else {
            <a class="primary-action" routerLink="/register">{{ i18n.t('Începe acum') }}</a>
            <a class="secondary-action" routerLink="/sign-in">{{ i18n.t('Autentificare') }}</a>
          }
        </div>
      </div>
      <div class="grid">
        <section class="card"><h2>{{ i18n.t('Redactare ofertă tehnică SEAP') }}</h2>
          <p>{{ i18n.t('Propunerea tehnică se structurează pe cerințele caietului de sarcini, cu răspuns punctual la fiecare specificație — pregătită pentru semnare electronică și depunere în SEAP.') }}</p></section>
        <section class="card"><h2>{{ i18n.t('Model propunere tehnică construcții') }}</h2>
          <p>{{ i18n.t('Șabloane adaptate lucrărilor de construcții: organizare de șantier, dotări, garanție, personal cheie și program de execuție — toate generate din documentele firmei tale.') }}</p></section>
        <section class="card"><h2>{{ i18n.t('Soft generare deviz caiet de sarcini') }}</h2>
          <p>{{ i18n.t('Lista de cantități (.xlsx) se generează direct din caietul de sarcini, cu recapitulație financiară (T1–T4, TVA, TOTAL GENERAL) — gata de completat cu oferta de preț.') }}</p></section>
        <section class="card"><h2>{{ i18n.t('Consultanță achiziții publice AI') }}</h2>
          <p>{{ i18n.t('Asistentul AI analizează anunțurile din e-licitatie.ro, extrage cerințele relevante și îți pregătește documentația, reducând timpul de ofertare de la zile la minute.') }}</p></section>
      </div>
      <p class="terms-note">{{ i18n.t('CompaSpot este un instrument software de redactare asistată, nu un furnizor de servicii de consultanță juridică sau tehnică. Verifică și asumă-ți conținutul documentelor înainte de semnarea electronică și depunerea în SEAP.') }} {{ i18n.t('Vezi') }} <a routerLink="/terms">{{ i18n.t('Termenii și Condițiile') }}</a>.</p>
    </section>
  }`
})
export class SeoLandingComponent {
  readonly auth = inject(AuthService);
  readonly features = inject(FeaturesService);
  readonly i18n = inject(LanguageService);
  private readonly router = inject(Router);
  /** Language-aware browser tab title for the public landing (index.html stays the Romanian SEO default). */
  readonly documentTitle = computed(() => this.i18n.t('Redactare ofertă tehnică SEAP — CompaSpot AI'));
  private readonly titleEffect = effect(() => {
    if (typeof document !== 'undefined') document.title = this.documentTitle();
  });
  private readonly redirectEffect = effect(() => {
    if (this.features.marketplaceEnabled() && this.auth.isAuthenticated()) {
      void this.router.navigate(['/seap/tenders']);
    }
  });
}
