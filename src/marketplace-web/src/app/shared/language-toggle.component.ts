import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { LanguageService } from '../core/i18n/language.service';

/**
 * RO/EN language selector shown in the app masthead (and the public/auth headers). The current
 * choice is highlighted; clicking switches immediately and persists for the next visit.
 */
@Component({
  selector: 'app-language-toggle',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="language-toggle" role="group" [attr.aria-label]="i18n.t('Alege limba')">
      <button type="button" [class.active]="i18n.language() === 'ro'" (click)="i18n.setLanguage('ro')"
        [attr.aria-pressed]="i18n.language() === 'ro'">RO</button>
      <button type="button" [class.active]="i18n.language() === 'en'" (click)="i18n.setLanguage('en')"
        [attr.aria-pressed]="i18n.language() === 'en'">EN</button>
    </div>
  `,
  styles: `
    .language-toggle { display: inline-flex; align-items: center; gap: .2rem; padding: .2rem; border: 1px solid var(--border); border-radius: .6rem; background: rgb(250 247 242 / 60%); }
    .language-toggle button { min-width: 2.3rem; padding: .4rem .55rem; border: 0; border-radius: .45rem; color: var(--ink); background: transparent; font: inherit; font-size: .78rem; font-weight: 750; letter-spacing: .03em; cursor: pointer; }
    .language-toggle button:hover, .language-toggle button:focus-visible { background: color-mix(in srgb, var(--sand) 60%, white); }
    .language-toggle button.active { color: #fff; background: var(--forest); }
  `
})
export class LanguageToggleComponent {
  readonly i18n = inject(LanguageService);
}
