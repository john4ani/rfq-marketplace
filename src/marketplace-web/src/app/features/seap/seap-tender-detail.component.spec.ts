import { registerLocaleData } from '@angular/common';
import localeRo from '@angular/common/locales/ro';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CompanyQualificationService } from '../../core/companies/company-qualification.service';
import { CompanyVaultService } from '../../core/companies/company-vault.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { AutoBidResponse, BidStatus, SeapService, SeapTender } from '../../core/seap/seap.service';
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
      {
        id: 'att-1',
        fileName: 'Fisa de date.pdf',
        downloadUrl: 'api/v1/seap/tenders/t1/attachments/att-1',
      },
      {
        id: 'att-2',
        fileName: 'Caiet de sarcini.zip',
        downloadUrl: 'api/v1/seap/tenders/t1/attachments/att-2',
      },
    ],
    procedureType: 'Procedură simplificată',
    procedureState: 'În desfășurare',
    summaryPoints: [
      'Garanție de participare: 120.000 RON',
      'Termen de execuție: 12 luni',
      'Criteriu: 60% preț, 40% componentă tehnică',
    ],
  };
  const bidStatus = {
    status: 'Imported' as const,
    proposalTitle: null,
    technicalProposalUrl: null,
    billOfQuantitiesUrl: null,
  };
  const pricing = {
    estimatedValueEur: 989_871,
    priceEur: 500,
    freeGenerationsRemaining: 1,
    shadowMode: true,
  };
  const usableDoc = {
    id: 'd1',
    fileName: 'certificari.pdf',
    documentType: 'PDF',
    extracted: true,
    pageCount: 5,
    chunkCount: 18,
    uploadedAt: '2026-08-01T10:00:00Z',
  };
  const completeQualification = {
    legal: {
      legalRepresentativeName: 'Ion Pop',
      legalRepresentativeRole: 'Administrator',
      legalForm: 'SRL',
      bankAccounts: [],
    },
    experience: [
      {
        id: 'e1',
        clientName: 'Primăria Cluj',
        contractTitle: 'Drumuri județene',
        contractNumberDate: null,
        valueAmount: 100000,
        valueCurrency: 'RON',
        cpvCode: '45233000',
        completionDate: '2025-01-01',
        documentRefUrl: null,
      },
    ],
    certificates: [],
    personnel: [],
  };
  const emptyQualification = { legal: null, experience: [], certificates: [], personnel: [] };
  const profiles = {
    getMyCompanies: vi
      .fn()
      .mockReturnValue(of([{ id: 'company-1', name: 'SC Test', role: 'Proprietar' as const }])),
  };

  beforeEach(() => {
    localStorage.setItem('compaspot.language', 'ro');
    // jsdom does not implement crypto.randomUUID; stub it so the progress request id is deterministic.
    Object.defineProperty(crypto, 'randomUUID', {
      value: vi.fn(() => 'req-progress'),
      configurable: true,
    });
  });

  afterEach(() => {
    localStorage.removeItem('compaspot.language');
    vi.restoreAllMocks();
  });

  function setup(options: {
    startAutoBid: ReturnType<typeof vi.fn>;
    generateSummary?: ReturnType<typeof vi.fn>;
    tender?: SeapTender;
    bidStatus?: BidStatus;
    getBidStatus?: ReturnType<typeof vi.fn>;
    refreshDocuments?: ReturnType<typeof vi.fn>;
    downloadBidFile?: ReturnType<typeof vi.fn>;
    downloadAttachment?: ReturnType<typeof vi.fn>;
    downloadDossierOriginal?: ReturnType<typeof vi.fn>;
  }) {
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 't1' }),
              queryParamMap: convertToParamMap({}),
            },
          },
        },
        { provide: ProfileService, useValue: profiles },
        {
          provide: CompanyVaultService,
          useValue: { list: vi.fn().mockReturnValue(of([usableDoc])) },
        },
        {
          provide: CompanyQualificationService,
          useValue: { get: vi.fn().mockReturnValue(of(completeQualification)) },
        },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(options.tender ?? tender)),
            getBidStatus:
              options.getBidStatus ?? vi.fn().mockReturnValue(of(options.bidStatus ?? bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            getAutoBidProgress: vi.fn().mockReturnValue(
              of({
                stage: 'Generating' as const,
                message: 'Claude redactează propunerea tehnică…',
              }),
            ),
            startAutoBid: options.startAutoBid,
            generateSummary: options.generateSummary ?? vi.fn(),
            refreshDocuments: options.refreshDocuments ?? vi.fn(),
            downloadBidFile: options.downloadBidFile ?? vi.fn(),
            downloadAttachment:
              options.downloadAttachment ?? vi.fn().mockReturnValue(of(new Blob(['pdf']))),
            downloadDossierOriginal:
              options.downloadDossierOriginal ?? vi.fn().mockReturnValue(of(new Blob(['pdf']))),
          },
        },
      ],
    });
    return TestBed.createComponent(SeapTenderDetailComponent);
  }

  function clickAutoBid(fixture: ReturnType<typeof setup>): void {
    const button = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find((item) => item.textContent?.includes('AI Auto-Bid'))!;
    expect(button).toBeTruthy();
    button.click();
    fixture.detectChanges();
  }

  it('shows the backend ProblemDetails detail when auto-bid fails (instead of the generic message)', () => {
    const fixture = setup({
      startAutoBid: vi.fn().mockReturnValue(
        throwError(() => ({
          error: {
            detail:
              'Serviciul Claude este temporar indisponibil (circuit deschis). Reîncearcă peste aproximativ o oră.',
          },
        })),
      ),
    });
    fixture.detectChanges();
    clickAutoBid(fixture);

    const status = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(
      '[role="status"]',
    );
    expect(status?.textContent).toContain('Serviciul Claude este temporar indisponibil');
    expect(status?.textContent).not.toContain('Ofertarea automată nu a putut fi pornită');
  });

  it('falls back to the generic message when the failure carries no actionable detail', () => {
    const fixture = setup({
      startAutoBid: vi.fn().mockReturnValue(throwError(() => ({ error: {} }))),
    });
    fixture.detectChanges();
    clickAutoBid(fixture);

    const status = (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(
      '[role="status"]',
    );
    expect(status?.textContent).toContain(
      'Ofertarea automată nu a putut fi pornită. Încearcă din nou.',
    );
  });

  it('confirms a free generation and refreshes the pricing preview so the free note disappears', () => {
    const fixture = setup({
      startAutoBid: vi
        .fn()
        .mockReturnValue(of({ ready: true, shadowMode: true, freeGeneration: true })),
    });
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
    const button = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      'button',
    );
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
      expect(startAutoBid).toHaveBeenCalledWith('t1', 'company-1', 'req-progress');

      start$.next({ ready: true, shadowMode: true, freeGeneration: false });
      start$.complete();
      fixture.detectChanges();
      expect((fixture.nativeElement as HTMLElement).textContent).toContain(
        'Oferta automată este gata.',
      );
    } finally {
      vi.useRealTimers();
    }
  });

  it('shows the Istoric tab with the generated offer documents for a tender with a generated bid', () => {
    const generatedBid: BidStatus = {
      status: 'BidGenerated',
      proposalTitle: 'Ofertă tehnică — Reabilitare drum',
      technicalProposalUrl: 'local://proposal.docx',
      billOfQuantitiesUrl: 'local://boq.xlsx',
    };
    const fixture = setup({ startAutoBid: vi.fn(), bidStatus: generatedBid });
    fixture.detectChanges();

    // Tab 1 no longer clutters the summary with the downloads — it points to the History tab.
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Deschide Istoricul');
    expect(text).not.toContain('Descarcă propunerea tehnică (.docx)');

    const tab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Istoric Ofertă'))!;
    expect(tab).toBeTruthy();
    tab.click();
    fixture.detectChanges();

    const historyText = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(historyText).toContain('Istoric Ofertă');
    expect(historyText).toContain('Ofertă tehnică — Reabilitare drum');
    expect(historyText).toContain('Descarcă propunerea tehnică (.docx)');
    expect(historyText).toContain('Descarcă lista de cantități (.xlsx)');
    expect(historyText).toContain('Îmi asum responsabilitatea');
  });

  it('switches to the Istoric tab automatically when the generation completes', () => {
    const getBidStatus = vi
      .fn()
      .mockReturnValueOnce(
        of({
          status: 'Imported' as const,
          proposalTitle: null,
          technicalProposalUrl: null,
          billOfQuantitiesUrl: null,
        }),
      )
      .mockReturnValue(
        of({
          status: 'BidGenerated' as const,
          proposalTitle: 'Ofertă tehnică — Reabilitare drum',
          technicalProposalUrl: 'local://proposal.docx',
          billOfQuantitiesUrl: 'local://boq.xlsx',
        }),
      );
    const fixture = setup({
      startAutoBid: vi
        .fn()
        .mockReturnValue(of({ ready: true, shadowMode: true, freeGeneration: false })),
      getBidStatus,
    });
    fixture.detectChanges();
    clickAutoBid(fixture);
    fixture.detectChanges();

    // The refreshed bid status reports the generated bid → the page moves to the Istoric tab.
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Istoric Ofertă');
    expect(text).toContain('Descarcă propunerea tehnică (.docx)');
    expect(text).toContain('Ofertă tehnică — Reabilitare drum');
  });

  it('keeps the Istoric download buttons disabled until the user accepts the SEAP responsibility', () => {
    Object.defineProperty(URL, 'createObjectURL', {
      value: vi.fn(() => 'blob:mock'),
      configurable: true,
    });
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true });
    const generatedBid: BidStatus = {
      status: 'BidGenerated',
      proposalTitle: 'Ofertă tehnică — Reabilitare drum',
      technicalProposalUrl: 'local://proposal.docx',
      billOfQuantitiesUrl: 'local://boq.xlsx',
    };
    const downloadBidFile = vi.fn().mockReturnValue(of(new Blob(['docx'])));
    const fixture = setup({ startAutoBid: vi.fn(), bidStatus: generatedBid, downloadBidFile });
    fixture.detectChanges();

    const tab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Istoric Ofertă'))!;
    tab.click();
    fixture.detectChanges();

    // ADR-008: before every document download the user must accept the responsibility checkbox.
    let buttons = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'),
    );
    expect(
      buttons.find((button) => button.textContent?.includes('Descarcă propunerea tehnică'))
        ?.disabled,
    ).toBe(true);
    expect(
      buttons.find((button) => button.textContent?.includes('Descarcă lista de cantități'))
        ?.disabled,
    ).toBe(true);

    // Accepting the responsibility enables both downloads.
    const acceptance = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      '.acceptance input',
    )!;
    acceptance.checked = true;
    acceptance.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    buttons = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'),
    );
    const proposalButton = buttons.find((button) =>
      button.textContent?.includes('Descarcă propunerea tehnică'),
    )!;
    const boqButton = buttons.find((button) =>
      button.textContent?.includes('Descarcă lista de cantități'),
    )!;
    expect(proposalButton.disabled).toBe(false);
    expect(boqButton.disabled).toBe(false);

    proposalButton.click();
    boqButton.click();
    expect(downloadBidFile).toHaveBeenCalledWith('t1', 'TechnicalProposal');
    expect(downloadBidFile).toHaveBeenCalledWith('t1', 'BillOfQuantities');
  });

  it('names the downloaded proposal files with the SEAP notice id (e.g. PropunereTehnica_SCN1178750.docx)', () => {
    Object.defineProperty(URL, 'createObjectURL', {
      value: vi.fn(() => 'blob:mock'),
      configurable: true,
    });
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true });
    const downloadedNames: string[] = [];
    const originalElementClick = HTMLElement.prototype.click;
    // The component downloads via a temporary <a download="…"> — intercept the click so we can read the
    // file name it was about to save, without touching the button event dispatch.
    vi.spyOn(HTMLElement.prototype, 'click').mockImplementation(function (this: HTMLElement) {
      if (this instanceof HTMLAnchorElement)
        downloadedNames.push((this as HTMLAnchorElement).download);
      return originalElementClick.call(this);
    });

    const generatedBid: BidStatus = {
      status: 'BidGenerated',
      proposalTitle: 'Ofertă tehnică — Reabilitare drum',
      technicalProposalUrl: 'local://proposal.docx',
      billOfQuantitiesUrl: 'local://boq.xlsx',
    };
    const downloadBidFile = vi.fn().mockReturnValue(of(new Blob(['docx'])));
    const fixture = setup({ startAutoBid: vi.fn(), bidStatus: generatedBid, downloadBidFile });
    fixture.detectChanges();

    const tab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Istoric Ofertă'))!;
    tab.click();
    fixture.detectChanges();

    const acceptance = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>(
      '.acceptance input',
    )!;
    acceptance.checked = true;
    acceptance.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const buttons = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'),
    );
    buttons.find((button) => button.textContent?.includes('Descarcă propunerea tehnică'))!.click();
    buttons.find((button) => button.textContent?.includes('Descarcă lista de cantități'))!.click();
    fixture.detectChanges();

    expect(downloadBidFile).toHaveBeenCalledWith('t1', 'TechnicalProposal');
    expect(downloadBidFile).toHaveBeenCalledWith('t1', 'BillOfQuantities');
    expect(downloadedNames).toContain('PropunereTehnica_SCN1178750.docx');
    expect(downloadedNames).toContain('ListaCantitati_SCN1178750.xlsx');
  });

  it('opens the documents tab and resolves the published documents on demand', () => {
    const documents$ = new Subject<SeapTender>();
    const refreshDocuments = vi.fn().mockReturnValue(documents$);
    const fixture = setup({
      startAutoBid: vi.fn(),
      refreshDocuments,
      tender: { ...tender, attachments: [] },
    });
    fixture.detectChanges();

    const tab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Documente Originale'))!;
    tab.click();
    fixture.detectChanges();

    // The tab shows a real skeleton while the on-demand request is in flight.
    expect(refreshDocuments).toHaveBeenCalledWith('t1');
    expect(fixture.nativeElement.querySelector('.skeleton')).not.toBeNull();

    documents$.next({
      ...tender,
      attachments: [
        { id: 'att-new-1', fileName: '4. Caiet de sarcini sala sport.pdf.p7s' },
        { id: 'att-new-2', fileName: 'FisaDate.pdf' },
      ],
    });
    documents$.complete();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('4. Caiet de sarcini sala sport.pdf.p7s');
    expect(text).toContain('FisaDate.pdf');
  });

  it('shows a recoverable error when the document resolution fails', () => {
    const refreshDocuments = vi
      .fn()
      .mockReturnValueOnce(throwError(() => ({ error: { detail: 'SEAP indisponibil.' } })))
      .mockReturnValueOnce(
        of({ ...tender, attachments: [{ id: 'att-1', fileName: 'FisaDate.pdf' }] }),
      );
    const fixture = setup({
      startAutoBid: vi.fn(),
      refreshDocuments,
      tender: { ...tender, attachments: [] },
    });
    fixture.detectChanges();

    const tab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Documente Originale'))!;
    tab.click();
    fixture.detectChanges();

    let text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Documentele anunțului nu au putut fi încărcate');
    expect(text).toContain('Reîncearcă');

    // A deliberate retry succeeds and replaces the error with the document list.
    const retry = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Reîncearcă'))!;
    retry.click();
    fixture.detectChanges();
    text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('FisaDate.pdf');
  });

  function setupWithEmptyVault(startAutoBid: ReturnType<typeof vi.fn>) {
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 't1' }),
              queryParamMap: convertToParamMap({}),
            },
          },
        },
        { provide: ProfileService, useValue: profiles },
        { provide: CompanyVaultService, useValue: { list: vi.fn().mockReturnValue(of([])) } },
        {
          provide: CompanyQualificationService,
          useValue: { get: vi.fn().mockReturnValue(of(emptyQualification)) },
        },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(tender)),
            getBidStatus: vi.fn().mockReturnValue(of(bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            getAutoBidProgress: vi.fn(),
            startAutoBid,
            downloadBidFile: vi.fn(),
          },
        },
      ],
    });
    return TestBed.createComponent(SeapTenderDetailComponent);
  }

  it('shows a contextual readiness warning above the generate button when the vault is empty', () => {
    const startAutoBid = vi.fn().mockReturnValue(of({ ready: true, shadowMode: true }));
    const fixture = setupWithEmptyVault(startAutoBid);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain(
      'Profilul firmei / Vault-ul este incomplet. Oferta va folosi placeholdere.',
    );
    expect(text).toContain('Completează aici');
    expect(text).not.toContain('Pentru a asigura că ofertarea are cele mai mari șanse de reușită');
  });

  it('warns when the qualification profile is empty even though the vault has usable documents', () => {
    const startAutoBid = vi.fn();
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 't1' }),
              queryParamMap: convertToParamMap({}),
            },
          },
        },
        { provide: ProfileService, useValue: profiles },
        {
          provide: CompanyVaultService,
          useValue: { list: vi.fn().mockReturnValue(of([usableDoc])) },
        },
        {
          provide: CompanyQualificationService,
          useValue: { get: vi.fn().mockReturnValue(of(emptyQualification)) },
        },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(tender)),
            getBidStatus: vi.fn().mockReturnValue(of(bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            getAutoBidProgress: vi.fn(),
            startAutoBid,
            downloadBidFile: vi.fn(),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(SeapTenderDetailComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain(
      'Profilul firmei / Vault-ul este incomplet. Oferta va folosi placeholdere.',
    );
    expect(text).toContain('Completează aici');
  });

  it('hides the readiness warning when the vault and the qualification profile are complete', () => {
    const startAutoBid = vi.fn().mockReturnValue(of({ ready: true, shadowMode: true }));
    const fixture = setup({ startAutoBid });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain(
      'Profilul firmei / Vault-ul este incomplet. Oferta va folosi placeholdere.',
    );
    expect(text).not.toContain('Pentru a asigura că ofertarea are cele mai mari șanse de reușită');
  });

  it('keeps the generate button visible as disabled + spinner and shows the toast while the request is in flight', () => {
    vi.useFakeTimers();
    try {
      const start$ = new Subject<AutoBidResponse>();
      const fixture = setup({ startAutoBid: vi.fn().mockReturnValue(start$) });
      fixture.detectChanges();
      clickAutoBid(fixture);

      // The CTA stays visible — disabled with the „Se redactează propunerea…" label, not replaced by a note.
      const generateButton = Array.from(
        fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
      ).find((button) => button.textContent?.includes('Se redactează propunerea'))!;
      expect(generateButton).toBeTruthy();
      expect(generateButton.disabled).toBe(true);
      expect(fixture.nativeElement.querySelector('.spinner')).not.toBeNull();
      // The non-blocking toast confirms the AI is drafting in the background.
      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
      expect(text).toContain('AI-ul redactează propunerea tehnică în background');

      // The simulated determinate progress climbs toward 90% (never beyond) while the request is in flight.
      vi.advanceTimersByTime(6000);
      fixture.detectChanges();
      const percent = fixture.componentInstance.autoBidPercent();
      expect(percent).toBeGreaterThan(0);
      expect(percent).toBeLessThanOrEqual(90);

      start$.next({ ready: true, shadowMode: true, freeGeneration: false });
      start$.complete();
      fixture.detectChanges();

      expect(fixture.componentInstance.autoBidPercent()).toBe(100);
      expect((fixture.nativeElement as HTMLElement).textContent ?? '').toContain(
        'Oferta automată este gata.',
      );
    } finally {
      vi.useRealTimers();
    }
  });

  it('hides the toast and resets the progress when the generation fails', () => {
    vi.useFakeTimers();
    try {
      const start$ = new Subject<AutoBidResponse>();
      const fixture = setup({ startAutoBid: vi.fn().mockReturnValue(start$) });
      fixture.detectChanges();
      clickAutoBid(fixture);

      expect((fixture.nativeElement as HTMLElement).textContent ?? '').toContain(
        'AI-ul redactează propunerea tehnică în background',
      );
      vi.advanceTimersByTime(6000);
      fixture.detectChanges();

      start$.error({ error: { detail: 'Claude indisponibil.' } });
      fixture.detectChanges();

      const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
      expect(text).toContain('Claude indisponibil.');
      expect(text).not.toContain('AI-ul redactează propunerea tehnică în background');
      expect(fixture.componentInstance.autoBidPercent()).toBe(0);
    } finally {
      vi.useRealTimers();
    }
  });

  it('never blocks Auto-Bid when the vault API is unavailable', () => {
    const startAutoBid = vi.fn().mockReturnValue(of({ ready: true, shadowMode: true }));
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 't1' }),
              queryParamMap: convertToParamMap({}),
            },
          },
        },
        { provide: ProfileService, useValue: profiles },
        {
          provide: CompanyVaultService,
          useValue: { list: vi.fn().mockReturnValue(throwError(() => new Error('offline'))) },
        },
        {
          provide: CompanyQualificationService,
          useValue: { get: vi.fn().mockReturnValue(throwError(() => new Error('offline'))) },
        },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(tender)),
            getBidStatus: vi.fn().mockReturnValue(of(bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            getAutoBidProgress: vi.fn(),
            startAutoBid,
            downloadBidFile: vi.fn(),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(SeapTenderDetailComponent);
    fixture.detectChanges();
    clickAutoBid(fixture);

    expect(startAutoBid).toHaveBeenCalledWith('t1', 'company-1', 'req-progress');
    // A failed readiness API never blocks the generation — and never invents a warning.
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').not.toContain(
      'Profilul firmei / Vault-ul este incomplet',
    );
  });

  it('opens on the „Sinteză & Ofertare AI" tab by default with the real AI summary bullets and the status card', () => {
    const fixture = setup({ startAutoBid: vi.fn() });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Sinteză AI');
    expect(text).toContain('Garanție de participare: 120.000 RON');
    expect(text).toContain('Termen de execuție: 12 luni');
    // The mock „Orientativ" pill is gone — the backend now supplies the real summary bullets.
    expect(text).not.toContain('Orientativ');
    expect(text).toContain('AI Auto-Bid · gratuit');
    // The header keeps the existing cards and adds the derived procedure state.
    expect(text).toContain('Stare procedură');
    expect(text).toContain('În desfășurare');

    const activeTabs = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
      'button[role="tab"][aria-selected="true"]',
    );
    expect(activeTabs.length).toBe(1);
    expect(activeTabs[0].textContent).toContain('Sinteză & Ofertare AI');
    // The visual highlight must be present: the buttons carry the "tab" class and the selected one adds
    // .active / aria-selected=true, so the forest-pill CSS (.tab.active, .tab[aria-selected="true"]) matches.
    expect(activeTabs[0].classList.contains('tab')).toBe(true);
    expect(activeTabs[0].classList.contains('active')).toBe(true);
    const selectedByStyle = (
      fixture.nativeElement as HTMLElement
    ).querySelectorAll<HTMLButtonElement>('button.tab.active, button.tab[aria-selected="true"]');
    expect(selectedByStyle.length).toBe(1);
  });

  it('shows an actionable empty state when the AI summary is not generated yet — no endless skeleton', () => {
    const generateSummary = vi.fn();
    const fixture = setup({
      startAutoBid: vi.fn(),
      generateSummary,
      tender: { ...tender, summaryPoints: [] },
    });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Sinteza AI nu este generată pentru acest anunț');
    expect(text).toContain('Generează sinteza AI');
    // No fake progress: without a user action there is no request and no skeleton.
    expect(fixture.nativeElement.querySelector('.skeleton')).toBeNull();
    expect(generateSummary).not.toHaveBeenCalled();
  });

  it('generates the AI summary on demand and renders the real bullets', () => {
    const summary$ = new Subject<SeapTender>();
    const generateSummary = vi.fn().mockReturnValue(summary$);
    const fixture = setup({
      startAutoBid: vi.fn(),
      generateSummary,
      tender: { ...tender, summaryPoints: [] },
    });
    fixture.detectChanges();

    const button = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find((item) => item.textContent?.includes('Generează sinteza AI'))!;
    button.click();
    fixture.detectChanges();

    // The skeleton is genuine: it only appears while the on-demand request is actually running.
    expect(generateSummary).toHaveBeenCalledWith('t1');
    expect(fixture.nativeElement.querySelector('.skeleton')).not.toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').not.toContain(
      'Garanție de participare',
    );

    summary$.next({
      ...tender,
      summaryPoints: ['Garanție de participare: 120.000 RON', 'Termen de execuție: 12 luni'],
    });
    summary$.complete();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(fixture.nativeElement.querySelector('.skeleton')).toBeNull();
    expect(text).toContain('Garanție de participare: 120.000 RON');
    expect(text).toContain('Termen de execuție: 12 luni');
  });

  it('shows a recoverable error and retries the summary generation on a deliberate action', () => {
    const generateSummary = vi
      .fn()
      .mockReturnValueOnce(
        throwError(() => ({ error: { detail: 'Serviciul AI este indisponibil.' } })),
      )
      .mockReturnValueOnce(
        of({ ...tender, summaryPoints: ['Garanție de participare: 120.000 RON'] }),
      );
    const fixture = setup({
      startAutoBid: vi.fn(),
      generateSummary,
      tender: { ...tender, summaryPoints: [] },
    });
    fixture.detectChanges();

    const clickSummaryAction = () => {
      const button = Array.from(
        fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
      ).find(
        (item) =>
          item.textContent?.includes('Generează sinteza AI') ||
          item.textContent?.includes('Încearcă din nou'),
      )!;
      button.click();
      fixture.detectChanges();
    };

    clickSummaryAction();
    let text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Sinteza AI nu a putut fi generată în acest moment');
    expect(text).toContain('Încearcă din nou');
    expect(fixture.nativeElement.querySelector('.skeleton')).toBeNull();

    // A deliberate retry succeeds and replaces the error state with the real bullets.
    clickSummaryAction();
    text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Garanție de participare: 120.000 RON');
    expect(text).not.toContain('Încearcă din nou');
  });

  it('switches to „Detalii Achiziție" and shows the detail rows with elegant placeholders', () => {
    const fixture = setup({ startAutoBid: vi.fn() });
    fixture.detectChanges();

    const tab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Detalii Achiziție'))!;
    tab.click();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Criteriul de atribuire');
    // The award criterion is not in the SEAP structured data — the UI must never invent „Prețul cel mai scăzut".
    // The originals are stored but not analysed yet, so the placeholder honestly points at the documents.
    expect(text).toContain('Necesită analiza documentației');
    expect(text).not.toContain('Prețul cel mai scăzut');
    expect(text).toContain('Tipul procedurii');
    expect(text).toContain('Procedură simplificată');
    expect(text).toContain('Sursa de finanțare');
    expect(text).toContain('Durata contractului');
    // The summary tab content is no longer rendered.
    expect(text).not.toContain('Sinteză AI');
  });

  it('shows „Necesită generarea sintezei AI" (not „Date indisponibile") when the extraction has not run yet', () => {
    const fixture = setup({
      startAutoBid: vi.fn(),
      tender: { ...tender, summaryPoints: [], awardCriterion: null, fundingSource: null },
    });
    fixture.detectChanges();

    const tab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Detalii Achiziție'))!;
    tab.click();
    fixture.detectChanges();

    // Before the AI extraction exists the value is simply not extracted yet — never invent data and never
    // claim the notice lacks it. An actionable „Generează sinteza AI" hint leads to the extraction.
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Neimportat încă din detaliile SEAP');
    expect(text).not.toContain('Date indisponibile în anunț');
    expect(text).not.toContain('Prețul cel mai scăzut');
    expect(text).toContain('Generează sinteza AI');
  });

  it('opens directly on a tab from the shareable ?tab= query parameter', () => {
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 't1' }),
              queryParamMap: convertToParamMap({ tab: 'details' }),
            },
          },
        },
        { provide: ProfileService, useValue: profiles },
        {
          provide: CompanyVaultService,
          useValue: { list: vi.fn().mockReturnValue(of([usableDoc])) },
        },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(tender)),
            getBidStatus: vi.fn().mockReturnValue(of(bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            getAutoBidProgress: vi.fn(),
            startAutoBid: vi.fn(),
            downloadBidFile: vi.fn(),
            downloadAttachment: vi.fn(),
          },
        },
      ],
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

    const tab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Condiții de Calificare'))!;
    tab.click();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Cuantumul garanției de participare');
    expect(text).toContain('Capacitate tehnică / profesională');
    expect(text).toContain('Personal solicitat');
    expect(text).toContain('Certificate și standarde (ISO)');
  });

  it('lists the original documents and downloads one through the authenticated API', () => {
    Object.defineProperty(URL, 'createObjectURL', {
      value: vi.fn(() => 'blob:mock'),
      configurable: true,
    });
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true });
    const fixture = setup({ startAutoBid: vi.fn() });
    fixture.detectChanges();

    const tab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Documente Originale'))!;
    tab.click();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Fisa de date.pdf');
    expect(text).toContain('Caiet de sarcini.zip');

    const downloadButton = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Descarcă'))!;
    downloadButton.click();
    fixture.detectChanges();

    expect(TestBed.inject(SeapService).downloadAttachment).toHaveBeenCalledWith('t1', 'att-1');
  });

  it('groups a DUAE + P7S dossier, shows status/size/failure codes and downloads the original via the controlled route', () => {
    Object.defineProperty(URL, 'createObjectURL', {
      value: vi.fn(() => 'blob:mock'),
      configurable: true,
    });
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true });
    const downloadDossierOriginal = vi.fn().mockReturnValue(of(new Blob(['signed'])));
    const dossierTender: SeapTender = {
      ...tender,
      attachments: [
        {
          id: 'duae-1',
          fileName: 'DUAE_CERERE_1.xml',
          downloadStatus: 'Downloaded',
          documentGroup: 'Duae',
          sizeBytes: 4096,
          downloadUrl: 'api/v1/seap/tenders/t1/documents/duae-1/download',
        },
        {
          id: 'fisa-1',
          fileName: 'Fisa de date.pdf.p7s',
          downloadStatus: 'Downloaded',
          documentGroup: 'TenderDocumentation',
          sizeBytes: 123_456,
          downloadUrl: 'api/v1/seap/tenders/t1/documents/fisa-1/download',
        },
        {
          id: 'caiet-1',
          fileName: 'Caiet de sarcini.rar.p7s',
          downloadStatus: 'Failed',
          documentGroup: 'TenderDocumentation',
          downloadErrorCode: 'OriginalDocumentTooLarge',
          downloadUrl: 'api/v1/seap/tenders/t1/attachments/caiet-1',
        },
      ],
    };
    const fixture = setup({
      startAutoBid: vi.fn(),
      tender: dossierTender,
      downloadDossierOriginal,
    });
    fixture.detectChanges();

    const tab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Documente Originale'))!;
    tab.click();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    // Grouped by the functional dossier group, original file names preserved (.xml and signed .p7s untouched).
    expect(text).toContain('Documente DUAE');
    expect(text).toContain('Documentație de atribuire');
    expect(text).toContain('DUAE_CERERE_1.xml');
    expect(text).toContain('Fisa de date.pdf.p7s');
    expect(text).toContain('Caiet de sarcini.rar.p7s');
    // Durable-download state, sizes and the partial-dossier warning are surfaced.
    expect(text).toContain('Stocat');
    expect(text).toContain('4 KB');
    expect(text).toContain('121 KB');
    expect(text).toContain('Fișierul depășește limita de dimensiune admisă');
    expect(text).toContain('Dosar parțial.');
    // Downloaded originals go through the controlled dossier download route.
    const downloadButtons = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).filter((button) => button.textContent?.includes('Descarcă originalul'));
    expect(downloadButtons.length).toBe(2);
    const duaeButton = downloadButtons.find((button) =>
      button.closest('tr')?.textContent?.includes('DUAE_CERERE_1.xml'),
    )!;
    duaeButton.click();
    fixture.detectChanges();
    expect(downloadDossierOriginal).toHaveBeenCalledWith('t1', 'duae-1');
  });

  it('shows an empty state on the documents tab when the authority published no files', () => {
    const tenderWithoutAttachments: SeapTender = { ...tender, attachments: [] };
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 't1' }),
              queryParamMap: convertToParamMap({}),
            },
          },
        },
        { provide: ProfileService, useValue: profiles },
        {
          provide: CompanyVaultService,
          useValue: { list: vi.fn().mockReturnValue(of([usableDoc])) },
        },
        {
          provide: CompanyQualificationService,
          useValue: { get: vi.fn().mockReturnValue(of(completeQualification)) },
        },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn().mockReturnValue(of(tenderWithoutAttachments)),
            getBidStatus: vi.fn().mockReturnValue(of(bidStatus)),
            getTenderPricing: vi.fn().mockReturnValue(of(pricing)),
            getAutoBidProgress: vi.fn(),
            startAutoBid: vi.fn(),
            refreshDocuments: vi.fn().mockReturnValue(of(tenderWithoutAttachments)),
            downloadBidFile: vi.fn(),
            downloadAttachment: vi.fn(),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(SeapTenderDetailComponent);
    fixture.detectChanges();

    const tab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Documente Originale'))!;
    tab.click();
    fixture.detectChanges();

    // Honest unavailable state: no published document was found for this notice — never a broken/mock row.
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').toContain(
      'Nu au fost găsite documente publicate',
    );
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').not.toContain(
      'Niciun document atașat',
    );
  });

  it('keeps the Sinteză AI tab focused on the summary bullets without duplicating the decision rows', () => {
    const fixture = setup({
      startAutoBid: vi.fn(),
      tender: {
        ...tender,
        summaryPoints: ['Garanție de participare: 120.000 RON'],
        awardCriterion: 'Prețul cel mai scăzut',
        participationGuarantee: '120.000 RON',
        certificates: ['ISO 9001, ISO 45001'],
      },
    });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    // The summary card only carries the high-level bullets; the structured rows live in their own tabs.
    expect(text).toContain('Garanție de participare: 120.000 RON');
    expect(text).not.toContain('Date de decizie');
    expect(text).not.toContain('Certificări obligatorii (ISO, ANRE)');
  });

  it('hides the document-analysis CTA until the LightOpportunityAnalysis stage is enabled', () => {
    const generateSummary = vi.fn();
    const fixture = setup({
      startAutoBid: vi.fn(),
      generateSummary,
      tender: {
        ...tender,
        summaryPoints: ['Termen scurt de depunere a ofertelor: 17.09.2026'],
        summaryGroundedInDocuments: false,
      },
    });
    fixture.detectChanges();

    // The legacy POST /summary enrichment must not be offered as the future light-analysis flow.
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('va fi disponibilă într-o etapă ulterioară');
    expect(text).not.toContain('Analizează documentele publicate');
    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    );
    expect(
      buttons.some((item) => item.textContent?.includes('Analizează documentele publicate')),
    ).toBe(false);
    expect(generateSummary).not.toHaveBeenCalled();
  });

  it('routes the extracted decision data to the dedicated detail and qualification tabs', () => {
    const fixture = setup({
      startAutoBid: vi.fn(),
      tender: {
        ...tender,
        summaryPoints: ['Garanție de participare: 120.000 RON'],
        awardCriterion: 'Prețul cel mai scăzut',
        participationGuarantee: '120.000 RON',
        contractDuration: '12 luni',
        fundingSource: 'Buget local',
        qualificationRequirements: ['Minim 2 contracte similare'],
        keyPersonnel: ['Manager de proiect'],
        certificates: ['ISO 9001, ISO 45001'],
      },
    });
    fixture.detectChanges();

    // Detalii Achiziție: award criterion, funding source, duration.
    const detailsTab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Detalii Achiziție'))!;
    detailsTab.click();
    fixture.detectChanges();
    const detailsText = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(detailsText).toContain('Criteriul de atribuire');
    expect(detailsText).toContain('Prețul cel mai scăzut');
    expect(detailsText).toContain('Buget local');
    expect(detailsText).toContain('12 luni');
    expect(detailsText).not.toContain('Certificări obligatorii (ISO, ANRE)');

    // Condiții de Calificare: guarantee, experience, key personnel, certificates.
    const qualificationTab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((button) => button.textContent?.includes('Condiții de Calificare'))!;
    qualificationTab.click();
    fixture.detectChanges();
    const qualificationText = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(qualificationText).toContain('Cuantumul garanției de participare');
    expect(qualificationText).toContain('120.000 RON');
    expect(qualificationText).toContain('Minim 2 contracte similare');
    expect(qualificationText).toContain('Manager de proiect');
    expect(qualificationText).toContain('Certificate și standarde (ISO)');
    expect(qualificationText).toContain('ISO 9001, ISO 45001');
  });

  it('surfaces the document fallback note on the Documente Originale tab instead of the summary', () => {
    const documentNote =
      'Documentele publicate (Fișa de date, Caietul de sarcini) nu au putut fi descărcate automat de pe SEAP. Sinteza s-a generat doar din metadatele anunțului.';
    const generateSummary = vi.fn().mockReturnValue(
      of({
        ...tender,
        summaryPoints: ['Termen de execuție: 12 luni'],
        summaryDocumentNote: documentNote,
      }),
    );
    const fixture = setup({
      startAutoBid: vi.fn(),
      generateSummary,
      tender: { ...tender, summaryPoints: [] },
    });
    fixture.detectChanges();

    const button = Array.from(
      fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>,
    ).find((item) => item.textContent?.includes('Generează sinteza AI'))!;
    button.click();
    fixture.detectChanges();

    // The note is shown right where the generation happened (summary card)...
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').toContain(
      'Sinteza s-a generat doar din metadatele anunțului',
    );

    // ...and it is also bound to the Documente Originale tab where the document state belongs.
    const documentsTab = Array.from(
      fixture.nativeElement.querySelectorAll('button[role="tab"]') as NodeListOf<HTMLButtonElement>,
    ).find((item) => item.textContent?.includes('Documente Originale'))!;
    documentsTab.click();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').toContain(
      'Sinteza s-a generat doar din metadatele anunțului',
    );
  });

  it('deep-links by SEAP notice number through the direct-hit lookup and renders with the internal id', () => {
    const getBidStatus = vi.fn().mockReturnValue(of(bidStatus));
    const getTenderPricing = vi.fn().mockReturnValue(of(pricing));
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 'SCN1178750' }),
              queryParamMap: convertToParamMap({}),
            },
          },
        },
        { provide: ProfileService, useValue: profiles },
        {
          provide: CompanyVaultService,
          useValue: { list: vi.fn().mockReturnValue(of([usableDoc])) },
        },
        {
          provide: CompanyQualificationService,
          useValue: { get: vi.fn().mockReturnValue(of(completeQualification)) },
        },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn(),
            getTenderByExternalId: vi.fn().mockReturnValue(of(tender)),
            getBidStatus,
            getTenderPricing,
            getAutoBidProgress: vi.fn(),
            startAutoBid: vi.fn(),
            refreshDocuments: vi.fn(),
            downloadBidFile: vi.fn(),
            downloadAttachment: vi.fn().mockReturnValue(of(new Blob(['pdf']))),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(SeapTenderDetailComponent);
    fixture.detectChanges();

    // The direct-hit response carries the internal Guid; every follow-up call uses it, not the SCN number.
    expect(getBidStatus).toHaveBeenCalledWith('t1');
    expect(getTenderPricing).toHaveBeenCalledWith('t1');
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').toContain('SCN1178750');
    expect((fixture.nativeElement as HTMLElement).textContent ?? '').toContain(
      'Reabilitare infrastructură rutieră în comuna Udești',
    );
  });

  it('shows an error state instead of a blank page when the tender cannot be loaded', () => {
    TestBed.configureTestingModule({
      imports: [SeapTenderDetailComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ id: 'SCN9999999' }),
              queryParamMap: convertToParamMap({}),
            },
          },
        },
        { provide: ProfileService, useValue: profiles },
        {
          provide: CompanyVaultService,
          useValue: { list: vi.fn().mockReturnValue(of([usableDoc])) },
        },
        {
          provide: CompanyQualificationService,
          useValue: { get: vi.fn().mockReturnValue(of(emptyQualification)) },
        },
        {
          provide: SeapService,
          useValue: {
            getTender: vi.fn(),
            getTenderByExternalId: vi.fn().mockReturnValue(
              throwError(() => ({
                error: {
                  detail:
                    'Anunțul SCN9999999 nu a fost găsit pe SEAP. Verifică numărul și încearcă din nou.',
                },
              })),
            ),
            getBidStatus: vi.fn(),
            getTenderPricing: vi.fn(),
            getAutoBidProgress: vi.fn(),
            startAutoBid: vi.fn(),
            refreshDocuments: vi.fn(),
            downloadBidFile: vi.fn(),
            downloadAttachment: vi.fn(),
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(SeapTenderDetailComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Anunțul nu a putut fi afișat');
    expect(text).toContain('nu a fost găsit pe SEAP');
    expect(text).toContain('Încearcă din nou');
  });
});
