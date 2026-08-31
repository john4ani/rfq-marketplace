import { registerLocaleData } from '@angular/common';
import localeRo from '@angular/common/locales/ro';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CompanyVaultService } from '../../core/companies/company-vault.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { AutoBidResponse, SeapService, SeapTender } from '../../core/seap/seap.service';
import { SeapTenderDetailComponent } from './seap-tender-detail.component';

registerLocaleData(localeRo);

describe('SeapTenderDetailComponent', () => {
  const tender: SeapTender = {
    id: 't1',
    seapExternalId: 'SCN1178750',
    title: 'Reabilitare infrastructură rutieră în comuna Udești',
    description: null,
    cpvCodes: ['45233120-6'],
    cpvNames: ['Lucrari de constructii de drumuri'],
    county: 'Suceava',
    categoryId: null,
    category: null,
    estimatedValue: 4_949_355,
    currency: 'RON',
    buyerName: 'Comuna Udesti',
    publicationDate: '2026-08-14T00:00:00Z',
    deadline: '2026-09-04T15:00:00Z',
    status: 'Imported',
    proposalTitle: null,
    technicalProposalUrl: null,
    billOfQuantitiesUrl: null,
    attachments: [
      { id: 'att-1', fileName: 'Fisa de date.pdf', downloadUrl: 'api/v1/seap/tenders/t1/attachments/att-1' },
      { id: 'att-2', fileName: 'Caiet de sarcini.zip', downloadUrl: 'api/v1/seap/tenders/t1/attachments/att-2' }
    ],
    procedureType: 'Procedură simplificată',
    procedureState: 'În desfășurare'
  };
  const bidStatus = { status: 'Imported' as const, proposalTitle: null, technicalProposalUrl: null, billOfQuantitiesUrl: null };
  const pricing = { estimatedValueEur: 989_871, priceEur: 500, freeGenerationsRemaining: 1, shadowMode: true };
  const usableDoc = {
    id: 'd1', fileName: 'certificari.pdf', documentType: 'PDF',
    extracted: true, pageCount: 5, chunkCount: 18, uploadedAt: '2026-08-01T10:00:00Z'
  };
  const profiles = { getMyCompanies: vi.fn().mockReturnValue(of([{ id: 'company-1', name: 'SC Test', role: 'Proprietar' as const }])) };

  beforeEach(() => {
    localStorage.setItem('compaspot.language', 'ro');
    // jsdom does not implement crypto.randomUUID; stub it so the progress request id is deterministic.
    Object.defineProperty(crypto, 'randomUUID', { value: vi.fn(() => 'req-progress'), configurable: true });
  });

  afterEach(() => localStorage.removeItem('compaspot.language'));

  function setup(options: { startAutoBid: ReturnType<typeof vi.fn> }) {
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 't1' }), queryParamMap: convertToParamMap({}) } } },
        { provide: ProfileService, useValue: profiles },
        { provide: CompanyVaultService, useValue: { list: vi.fn().mockReturnValue(of([usableDoc])) } },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(tender)),
            getBidStatus: vi.fn().mockReturnValue(of(bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            getAutoBidProgress: vi.fn().mockReturnValue(of({ stage: 'Generating' as const, message: 'Claude redactează propunerea tehnică…' })),
            startAutoBid: options.startAutoBid,
            downloadBidFile: vi.fn(),
            downloadAttachment: vi.fn().mockReturnValue(of(new Blob(['pdf'])))
          }
        }
      ]
    });
    return TestBed.createComponent(SeapTenderDetailComponent);
  }

  function clickAutoBid(fixture: ReturnType<typeof setup>): void {
    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(item => item.textContent?.includes('AI Auto-Bid'))!;
    expect(button).toBeTruthy();
    button.click();
    fixture.detectChanges();
  }

  it('shows the backend ProblemDetails detail when auto-bid fails (instead of the generic message)', () => {
    const fixture = setup({
      startAutoBid: vi.fn().mockReturnValue(throwError(() => ({
        error: { detail: 'Serviciul Claude este temporar indisponibil (circuit deschis). Reîncearcă peste aproximativ o oră.' }
      })))
    });
    fixture.detectChanges();
    clickAutoBid(fixture);

    const status = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('[role="status"]');
    expect(status?.textContent).toContain('Serviciul Claude este temporar indisponibil');
    expect(status?.textContent).not.toContain('Ofertarea automată nu a putut fi pornită');
  });

  it('falls back to the generic message when the failure carries no actionable detail', () => {
    const fixture = setup({ startAutoBid: vi.fn().mockReturnValue(throwError(() => ({ error: {} }))) });
    fixture.detectChanges();
    clickAutoBid(fixture);

    const status = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>('[role="status"]');
    expect(status?.textContent).toContain('Ofertarea automată nu a putut fi pornită. Încearcă din nou.');
  });

  it('confirms a free generation and refreshes the pricing preview so the free note disappears', () => {
    const fixture = setup({ startAutoBid: vi.fn().mockReturnValue(of({ ready: true, shadowMode: true, freeGeneration: true })) });
    fixture.detectChanges();
    const service = TestBed.inject(SeapService);
    const getTenderPricing = service.getTenderPricing as unknown as ReturnType<typeof vi.fn>;
    const pricingCallsBefore = getTenderPricing.mock.calls.length;

    clickAutoBid(fixture);

    // The mocked bid-status is not BidGenerated, so the busy note also carries role="status"; assert the
    // success message is rendered somewhere on the page.
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Prima generare a fost gratuită. Oferta este gata.');
    // The entitlement was consumed server-side; the page re-fetches pricing immediately.
    expect(getTenderPricing.mock.calls.length).toBeGreaterThan(pricingCallsBefore);
  });

  it('labels shadow-mode generations as free (test mode) instead of showing a chargeable price', () => {
    const fixture = setup({ startAutoBid: vi.fn() });
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('AI Auto-Bid · gratuit');
    expect(text).toContain('Mod de test');
    expect(text).toContain('500 €'); // the go-live price is mentioned, but not charged now
    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button');
    expect(button?.textContent).not.toContain('€'); // the button must not pretend a charge happens now
  });

  it('shows real per-stage progress while the generation request is in flight', () => {
    vi.useFakeTimers();
    try {
      const start$ = new Subject<AutoBidResponse>();
      const startAutoBid = vi.fn().mockReturnValue(start$);
      const fixture = setup({ startAutoBid });
      fixture.detectChanges();
      clickAutoBid(fixture);

      // The request is still in flight: the busy panel shows the real stage label reported by the API
      // (Generating → "Claude redactează propunerea tehnică…") instead of a bare "se generează…".
      vi.advanceTimersByTime(2100);
      fixture.detectChanges();
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
      expect(text).toContain('Claude redactează propunerea tehnică');
      expect(startAutoBid).toHaveBeenCalledWith('t1', 'req-progress');

      start$.next({ ready: true, shadowMode: true, freeGeneration: false });
      start$.complete();
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain('Oferta automată este gata.');
    } finally {
      vi.useRealTimers();
    }
  });

  function setupWithEmptyVault(startAutoBid: ReturnType<typeof vi.fn>) {
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 't1' }), queryParamMap: convertToParamMap({}) } } },
        { provide: ProfileService, useValue: profiles },
        { provide: CompanyVaultService, useValue: { list: vi.fn().mockReturnValue(of([])) } },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(tender)),
            getBidStatus: vi.fn().mockReturnValue(of(bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            getAutoBidProgress: vi.fn(),
            startAutoBid,
            downloadBidFile: vi.fn()
          }
        }
      ]
    });
    return TestBed.createComponent(SeapTenderDetailComponent);
  }

  it('blocks AI Auto-Bid and shows the repository prompt when no usable documents exist, then proceeds on "remind me later"', () => {
    const startAutoBid = vi.fn().mockReturnValue(of({ ready: true, shadowMode: true }));
    const fixture = setupWithEmptyVault(startAutoBid);
    fixture.detectChanges();
    clickAutoBid(fixture);

    // The generation is paused until the user picks one of the prompt options.
    expect(startAutoBid).not.toHaveBeenCalled();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Pentru a asigura că ofertarea are cele mai mari șanse de reușită');
    expect(text).toContain('Amintește-mi mai târziu');

    const remindButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Amintește-mi mai târziu'))!;
    remindButton.click();
    fixture.detectChanges();

    expect(startAutoBid).toHaveBeenCalledWith('t1', 'req-progress');
  });

  it('persists the permanent dismissal when "Am înțeles..." is chosen and then generates', () => {
    localStorage.removeItem('compaspot.vaultPromptDismissed.company-1');
    const startAutoBid = vi.fn().mockReturnValue(of({ ready: true, shadowMode: true }));
    const fixture = setupWithEmptyVault(startAutoBid);
    fixture.detectChanges();
    clickAutoBid(fixture);

    const understoodButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Am înțeles, nu mai arăta acest mesaj'))!;
    understoodButton.click();
    fixture.detectChanges();

    expect(localStorage.getItem('compaspot.vaultPromptDismissed.company-1')).toBe('true');
    expect(startAutoBid).toHaveBeenCalledWith('t1', 'req-progress');
  });

  it('skips the repository prompt when the user permanently dismissed it', () => {
    localStorage.setItem('compaspot.vaultPromptDismissed.company-1', 'true');
    const startAutoBid = vi.fn().mockReturnValue(of({ ready: true, shadowMode: true }));
    const fixture = setupWithEmptyVault(startAutoBid);
    fixture.detectChanges();
    clickAutoBid(fixture);

    expect(startAutoBid).toHaveBeenCalledWith('t1', 'req-progress');
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').not.toContain('Pentru a asigura că ofertarea');
  });

  it('never blocks Auto-Bid when the vault API is unavailable', () => {
    const startAutoBid = vi.fn().mockReturnValue(of({ ready: true, shadowMode: true }));
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 't1' }), queryParamMap: convertToParamMap({}) } } },
        { provide: ProfileService, useValue: profiles },
        { provide: CompanyVaultService, useValue: { list: vi.fn().mockReturnValue(throwError(() => new Error('offline'))) } },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(tender)),
            getBidStatus: vi.fn().mockReturnValue(of(bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            getAutoBidProgress: vi.fn(),
            startAutoBid,
            downloadBidFile: vi.fn()
          }
        }
      ]
    });
    const fixture = TestBed.createComponent(SeapTenderDetailComponent);
    fixture.detectChanges();
    clickAutoBid(fixture);

    expect(startAutoBid).toHaveBeenCalledWith('t1', 'req-progress');
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').not.toContain('Pentru a asigura că ofertarea');
  });

  it('opens on the „Sinteză & Ofertare AI" tab by default with the orientative AI summary and the status card', () => {
    const fixture = setup({ startAutoBid: vi.fn() });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Sinteză AI');
    expect(text).toContain('Garanția de participare');
    expect(text).toContain('Orientativ');
    expect(text).toContain('AI Auto-Bid · gratuit');
    // The header keeps the existing cards and adds the derived procedure state.
    expect(text).toContain('Stare procedură');
    expect(text).toContain('În desfășurare');

    const activeTabs = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button[role="tab"][aria-selected="true"]');
    expect(activeTabs.length).toBe(1);
    expect(activeTabs[0].textContent).toContain('Sinteză & Ofertare AI');
    // The visual highlight must be present: the buttons carry the "tab" class and the selected one adds
    // .active / aria-selected=true, so the forest-pill CSS (.tab.active, .tab[aria-selected="true"]) matches.
    expect(activeTabs[0].classList.contains('tab')).toBe(true);
    expect(activeTabs[0].classList.contains('active')).toBe(true);
    const selectedByStyle = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button.tab.active, button.tab[aria-selected="true"]');
    expect(selectedByStyle.length).toBe(1);
  });

  it('switches to „Detalii Achiziție" and shows the detail rows with elegant placeholders', () => {
    const fixture = setup({ startAutoBid: vi.fn() });
    fixture.detectChanges();

    const tab = Array.from(fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Detalii Achiziție'))!;
    tab.click();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Criteriul de atribuire');
    expect(text).toContain('Prețul cel mai scăzut');
    expect(text).toContain('Tipul procedurii');
    expect(text).toContain('Procedură simplificată');
    expect(text).toContain('Sursa de finanțare');
    expect(text).toContain('Durata contractului');
    // The summary tab content is no longer rendered.
    expect(text).not.toContain('Sinteză AI');
  });

  it('opens directly on a tab from the shareable ?tab= query parameter', () => {
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 't1' }), queryParamMap: convertToParamMap({ tab: 'details' }) } } },
        { provide: ProfileService, useValue: profiles },
        { provide: CompanyVaultService, useValue: { list: vi.fn().mockReturnValue(of([usableDoc])) } },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(tender)),
            getBidStatus: vi.fn().mockReturnValue(of(bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            getAutoBidProgress: vi.fn(),
            startAutoBid: vi.fn(),
            downloadBidFile: vi.fn(),
            downloadAttachment: vi.fn()
          }
        }
      ]
    });
    const fixture = TestBed.createComponent(SeapTenderDetailComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Criteriul de atribuire');
    expect(text).not.toContain('Sinteză AI');
  });

  it('shows the qualification tab with placeholders until the parser extracts them', () => {
    const fixture = setup({ startAutoBid: vi.fn() });
    fixture.detectChanges();

    const tab = Array.from(fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Condiții de Calificare'))!;
    tab.click();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Cuantumul garanției de participare');
    expect(text).toContain('Capacitate tehnică / profesională');
    expect(text).toContain('Personal solicitat');
    expect(text).toContain('Certificate și standarde (ISO)');
  });

  it('lists the original documents and downloads one through the authenticated API', () => {
    Object.defineProperty(URL, 'createObjectURL', { value: vi.fn(() => 'blob:mock'), configurable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true });
    const fixture = setup({ startAutoBid: vi.fn() });
    fixture.detectChanges();

    const tab = Array.from(fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Documente Originale'))!;
    tab.click();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Fisa de date.pdf');
    expect(text).toContain('Caiet de sarcini.zip');

    const downloadButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Descarcă'))!;
    downloadButton.click();
    fixture.detectChanges();

    expect(TestBed.inject(SeapService).downloadAttachment).toHaveBeenCalledWith('t1', 'att-1');
  });

  it('shows an empty state on the documents tab when the authority published no files', () => {
    const tenderWithoutAttachments: SeapTender = { ...tender, attachments: [] };
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 't1' }), queryParamMap: convertToParamMap({}) } } },
        { provide: ProfileService, useValue: profiles },
        { provide: CompanyVaultService, useValue: { list: vi.fn().mockReturnValue(of([usableDoc])) } },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(tenderWithoutAttachments)),
            getBidStatus: vi.fn().mockReturnValue(of(bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            getAutoBidProgress: vi.fn(),
            startAutoBid: vi.fn(),
            downloadBidFile: vi.fn(),
            downloadAttachment: vi.fn()
          }
        }
      ]
    });
    const fixture = TestBed.createComponent(SeapTenderDetailComponent);
    fixture.detectChanges();

    const tab = Array.from(fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Documente Originale'))!;
    tab.click();
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent ?? '').toContain('Niciun document atașat încă');
  });
});
