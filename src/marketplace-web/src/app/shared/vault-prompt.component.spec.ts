import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { VaultReadinessService } from '../core/companies/vault-readiness.service';
import { LanguageService } from '../core/i18n/language.service';
import { VaultPromptComponent } from './vault-prompt.component';

describe('VaultPromptComponent', () => {
  let readiness: VaultReadinessService;

  beforeEach(async () => {
    localStorage.setItem('compaspot.language', 'ro');
    localStorage.removeItem('compaspot.vaultPromptDismissed.company-1');
    await TestBed.configureTestingModule({
      imports: [VaultPromptComponent],
      providers: [provideRouter([])]
    }).compileComponents();
    readiness = TestBed.inject(VaultReadinessService);
  });

  afterEach(() => localStorage.removeItem('compaspot.language'));

  function setup(): ReturnType<typeof TestBed.createComponent<VaultPromptComponent>> {
    const fixture = TestBed.createComponent(VaultPromptComponent);
    fixture.componentRef.setInput('companyId', 'company-1');
    fixture.detectChanges();
    return fixture;
  }

  it('shows the recommendation with the three actions and a link to the Document Repository', () => {
    const fixture = setup();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Pentru a asigura că ofertarea are cele mai mari șanse de reușită');
    expect(text).toContain('Link la Depozit de Documente');
    expect(text).toContain('Amintește-mi mai târziu');
    expect(text).toContain('Am înțeles, nu mai arăta acest mesaj. Am făcut toate setările.');

    const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('a');
    expect(link?.getAttribute('href')).toContain('/account/company/company-1');
  });

  it('hides on "Amintește-mi mai târziu" and emits remind without persisting', () => {
    const fixture = setup();
    const reasons: ('remind' | 'forever')[] = [];
    fixture.componentInstance.dismissed.subscribe(reason => reasons.push(reason));

    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(item => item.textContent?.includes('Amintește-mi mai târziu'))!;
    button.click();
    fixture.detectChanges();

    expect(reasons).toEqual(['remind']);
    expect(readiness.isPromptDismissed('company-1')).toBe(false);
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').not.toContain('Pentru a asigura că ofertarea');
  });

  it('persists the permanent dismissal on "Am înțeles..." and emits forever', () => {
    const fixture = setup();
    const reasons: ('remind' | 'forever')[] = [];
    fixture.componentInstance.dismissed.subscribe(reason => reasons.push(reason));

    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(item => item.textContent?.includes('Am înțeles, nu mai arăta acest mesaj'))!;
    button.click();
    fixture.detectChanges();

    expect(reasons).toEqual(['forever']);
    expect(readiness.isPromptDismissed('company-1')).toBe(true);
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').not.toContain('Pentru a asigura că ofertarea');
  });

  it('renders the English text when the language is English', () => {
    TestBed.inject(LanguageService).setLanguage('en');
    const fixture = setup();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('To give your bidding the best chance of success');
    expect(text).toContain('Link to Document Repository');
    expect(text).toContain('Remind me later');
    expect(text).toContain('I understand, don’t show this message again');
  });
});
