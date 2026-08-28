import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ProfileService } from '../../core/profiles/profile.service';
import { SeapService } from '../../core/seap/seap.service';
import { SeapOnboardingChatComponent } from './seap-onboarding-chat.component';

@Component({ template: '' })
class FeedStub {}

describe('SeapOnboardingChatComponent', () => {
  const company = { id: 'company-1', name: 'SC Test', role: 'Proprietar' as const };

  const clarifying = {
    clarificationNeeded: true, clarificationQuestion: 'În ce județe cauți licitații?',
    extractedCpvPrefixes: [], extractedCountyCodes: [], extractedCounties: [], summaryText: null,
    provider: 'OpenAI', model: 'test', promptVersion: 'test', inputTokens: 1, outputTokens: 1
  };
  const ready = {
    clarificationNeeded: false, clarificationQuestion: null,
    extractedCpvPrefixes: ['45233000', '45221111'], extractedCountyCodes: ['IS', 'SV', 'BT'],
    extractedCounties: ['Iași', 'Suceava', 'Botoșani'],
    summaryText: 'Am identificat interes pentru Lucrări de drumuri și poduri în zona Moldovei.',
    provider: 'OpenAI', model: 'test', promptVersion: 'test', inputTokens: 1, outputTokens: 1
  };

  function setup(options: {
    availability: ReturnType<typeof vi.fn>;
    matchTenders: ReturnType<typeof vi.fn>;
    saveMatchProfile?: ReturnType<typeof vi.fn>;
    companies?: ReturnType<typeof vi.fn>;
    description?: string;
  }) {
    return TestBed.configureTestingModule({
      imports: [SeapOnboardingChatComponent],
      providers: [
        provideRouter([{ path: 'seap/tenders', component: FeedStub }]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({ description: options.description ?? '' }) } }
        },
        {
          provide: SeapService,
          useValue: {
            matchTenderAvailability: options.availability,
            matchTenders: options.matchTenders,
            saveMatchProfile: options.saveMatchProfile ?? vi.fn()
          }
        },
        { provide: ProfileService, useValue: { getMyCompanies: options.companies ?? vi.fn().mockReturnValue(of([company])) } }
      ]
    }).compileComponents();
  }

  it('keeps the manual route available when the AI match assistant is unavailable', async () => {
    setup({ availability: vi.fn().mockReturnValue(of({ enabled: true, modelAvailable: false })), matchTenders: vi.fn() });
    const fixture = TestBed.createComponent(SeapOnboardingChatComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Asistentul de potrivire este indisponibil momentan.');
    expect(fixture.nativeElement.textContent).toContain('Deschide Anunțuri SEAP');
  });

  it('clarifies once, shows the synthesis card and saves the filters on confirm', async () => {
    const matchTenders = vi.fn()
      .mockReturnValueOnce(of(clarifying))
      .mockReturnValueOnce(of(ready));
    const saveMatchProfile = vi.fn().mockReturnValue(of({ companyId: company.id, cpvPrefixes: ready.extractedCpvPrefixes, counties: ready.extractedCounties }));
    setup({
      availability: vi.fn().mockReturnValue(of({ enabled: true, modelAvailable: true })),
      matchTenders,
      saveMatchProfile,
      description: 'Căutăm licitații pentru lucrări de drumuri și poduri în zona Moldovei.'
    });
    const fixture = TestBed.createComponent(SeapOnboardingChatComponent);
    fixture.detectChanges();

    // Auto-started from the transferred description → first turn asks exactly one question.
    expect(matchTenders).toHaveBeenCalledWith(expect.stringContaining('drumuri'), []);
    expect(fixture.nativeElement.textContent).toContain('În ce județe cauți licitații?');

    // Answering appends the turn and yields the summary card.
    expect(fixture.componentInstance.busy()).toBe(false);
    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input[placeholder="Scrie răspunsul"]')!;
    input.value = 'În Moldova.';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    const answerButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Răspunde'))!;
    expect(answerButton.disabled).toBe(false);
    answerButton.click();
    fixture.detectChanges();

    expect(matchTenders).toHaveBeenLastCalledWith(expect.stringContaining('drumuri'),
      [{ question: 'În ce județe cauți licitații?', answer: 'În Moldova.' }]);
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('45233000');
    expect(text).toContain('Iași');
    expect(text).toContain('Moldovei');

    const saveButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Salvează filtrele'))!;
    saveButton.click();
    await fixture.whenStable();

    expect(saveMatchProfile).toHaveBeenCalledWith(company.id, ready.extractedCpvPrefixes, ready.extractedCounties);
    expect(TestBed.inject(Router).url).toBe('/seap/tenders');
  });
});
