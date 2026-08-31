import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { VaultReadinessService } from '../core/companies/vault-readiness.service';
import { LanguageService } from '../core/i18n/language.service';

/**
 * Shared recommendation shown before „Caută în SEAP" / „AI Auto-Bid" when the Document Repository has no
 * usable documents. Offers three choices: open the repository to upload documents, be reminded later, or
 * never show this message again (persisted per company). `dismissed` fires for the last two so the caller
 * can proceed with the pending action (e.g. start the auto-bid generation).
 */
@Component({
  selector: 'app-vault-prompt',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: [`:host{display:block}.vault-prompt{padding:1.1rem 1.25rem;border:1px solid color-mix(in srgb,var(--warm-amber) 55%,var(--border));border-radius:1rem;background:linear-gradient(135deg,#faf0d8,#f7e7c6);box-shadow:var(--shadow-soft)}.vault-prompt p{margin:0 0 .9rem;color:var(--ink);line-height:1.55;font-weight:600}.vault-prompt-actions{display:flex;flex-wrap:wrap;gap:.6rem;align-items:center}.vault-prompt-actions a,.vault-prompt-actions button{display:inline-flex;align-items:center;min-height:2.4rem;padding:.55rem 1rem;border-radius:.6rem;font:inherit;font-size:.88rem;font-weight:800;text-decoration:none;cursor:pointer}.vault-prompt-actions .primary{color:#fff;background:var(--forest)}.vault-prompt-actions button{color:var(--forest);background:transparent;border:1px solid color-mix(in srgb,var(--forest) 40%,var(--border))}`],
  template: `@if (visible()) {
    <section class="vault-prompt" role="status" aria-live="polite">
      <p>{{ i18n.t('Pentru a asigura că ofertarea are cele mai mari șanse de reușită, încarcă în Depozitul de Documente cât mai multe documente relevante pentru licitațiile la care participi.') }}</p>
      <div class="vault-prompt-actions">
        <a class="primary" [routerLink]="['/account/company', companyId()]">{{ i18n.t('Link la Depozit de Documente') }}</a>
        <button type="button" (click)="remindLater()">{{ i18n.t('Amintește-mi mai târziu') }}</button>
        <button type="button" (click)="understood()">{{ i18n.t('Am înțeles, nu mai arăta acest mesaj. Am făcut toate setările.') }}</button>
      </div>
    </section>
  }`
})
export class VaultPromptComponent {
  readonly companyId = input.required<string>();
  /** Fires with the chosen dismissal so the caller can proceed with the pending action. */
  readonly dismissed = output<'remind' | 'forever'>();
  readonly visible = signal(true);
  readonly i18n = inject(LanguageService);
  private readonly readiness = inject(VaultReadinessService);

  /** Hides the prompt until the next time the action is used; the message will show again. */
  remindLater(): void {
    this.visible.set(false);
    this.dismissed.emit('remind');
  }

  /** Persists the permanent dismissal and hides the prompt from now on. */
  understood(): void {
    this.readiness.dismissForever(this.companyId());
    this.visible.set(false);
    this.dismissed.emit('forever');
  }
}
