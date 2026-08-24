import { LanguageService } from './language.service';
import { translations } from './translations';

describe('LanguageService', () => {
  function createService() {
    return new LanguageService();
  }

  afterEach(() => {
    try {
      localStorage.removeItem('compaspot.language');
    } catch {
      // ignore
    }
    // Restore a neutral navigator for the next test.
    Object.defineProperty(window.navigator, 'language', { configurable: true, value: 'en-US' });
  });

  it('defaults to English for a non-Romanian browser locale', () => {
    Object.defineProperty(window.navigator, 'language', { configurable: true, value: 'fr-FR' });
    expect(createService().language()).toBe('en');
  });

  it('defaults to Romanian for a Romanian browser locale', () => {
    Object.defineProperty(window.navigator, 'language', { configurable: true, value: 'ro-RO' });
    expect(createService().language()).toBe('ro');
  });

  it('prefers the persisted user choice over the browser locale', () => {
    localStorage.setItem('compaspot.language', 'en');
    Object.defineProperty(window.navigator, 'language', { configurable: true, value: 'ro-RO' });
    expect(createService().language()).toBe('en');
  });

  it('persists the chosen language and sets the document lang', () => {
    Object.defineProperty(window.navigator, 'language', { configurable: true, value: 'en-US' });
    const service = createService();
    service.setLanguage('ro');
    expect(localStorage.getItem('compaspot.language')).toBe('ro');
    expect(document.documentElement.lang).toBe('ro');
    service.setLanguage('en');
    expect(localStorage.getItem('compaspot.language')).toBe('en');
    expect(document.documentElement.lang).toBe('en');
  });

  it('returns the Romanian source text while the language is Romanian', () => {
    const service = createService();
    service.setLanguage('ro');
    expect(service.t('Autentificare')).toBe('Autentificare');
  });

  it('returns the English translation once English is selected', () => {
    const service = createService();
    service.setLanguage('en');
    expect(service.t('Autentificare')).toBe('Sign in');
  });

  it('returns the source text when a translation is missing', () => {
    const service = createService();
    service.setLanguage('en');
    expect(service.t('Text care nu are traducere încă')).toBe('Text care nu are traducere încă');
  });

  it('exposes a toggle between the two languages', () => {
    const service = createService();
    service.setLanguage('ro');
    service.toggle();
    expect(service.language()).toBe('en');
    service.toggle();
    expect(service.language()).toBe('ro');
  });
});

describe('translations dictionary', () => {
  it('contains only non-empty English values', () => {
    for (const [key, value] of Object.entries(translations)) {
      expect(key.trim().length).toBeGreaterThan(0);
      expect(value.trim().length).toBeGreaterThan(0);
      expect(value).not.toBe(key);
    }
  });

  it('has unique keys', () => {
    const keys = Object.keys(translations);
    expect(new Set(keys).size).toBe(keys.length);
  });
});
