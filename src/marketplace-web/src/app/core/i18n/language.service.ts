import { Injectable, computed, signal } from '@angular/core';
import { translations } from './translations';

export type AppLanguage = 'ro' | 'en';

const STORAGE_KEY = 'compaspot.language';

/**
 * Detects the initial language: an explicit user choice in localStorage wins; otherwise the browser
 * locale decides (Romanian browser → Romanian, everything else → English). Romanian remains the main
 * product language; English is offered for users from other countries.
 */
function detectInitialLanguage(): AppLanguage {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'ro' || stored === 'en') return stored;
  } catch {
    // Storage unavailable (private mode); fall through to locale detection.
  }
  if (typeof navigator !== 'undefined') {
    const locale = (navigator.language ?? '').toLowerCase();
    if (locale.startsWith('ro')) return 'ro';
  }
  return 'en';
}

@Injectable({ providedIn: 'root' })
export class LanguageService {
  readonly language = signal<AppLanguage>(detectInitialLanguage());

  readonly isEnglish = computed(() => this.language() === 'en');

  constructor() {
    if (typeof document !== 'undefined') document.documentElement.lang = this.language() === 'ro' ? 'ro' : 'en';
  }

  setLanguage(language: AppLanguage): void {
    if (this.language() === language) return;
    this.language.set(language);
    try {
      localStorage.setItem(STORAGE_KEY, language);
    } catch {
      // Storage unavailable; the choice still applies for this session.
    }
    if (typeof document !== 'undefined') document.documentElement.lang = language === 'ro' ? 'ro' : 'en';
  }

  toggle(): void {
    this.setLanguage(this.language() === 'ro' ? 'en' : 'ro');
  }

  /**
   * Returns the English translation for the Romanian source text, or the source text itself when the
   * language is Romanian or the translation is missing. Reading the language signal inside the method
   * makes Angular re-render the calling template binding when the language changes.
   */
  t(key: string): string {
    const language = this.language();
    if (language === 'ro') return key;
    return translations[key] ?? key;
  }
}
