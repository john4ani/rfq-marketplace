import { registerLocaleData } from '@angular/common';
import localeRo from '@angular/common/locales/ro';
import { computed } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { CompanyVaultService, SpecificationBidResult } from '../../core/companies/company-vault.service';
import { ProfileService } from '../../core/profiles/profile.service';
import { SeapService, SeapTender } from '../../core/seap/seap.service';
import { SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { SeapTendersComponent } from './seap-tenders.component';

registerLocaleData(localeRo);

describe('SeapTendersComponent', () => {
  const company = { id: 'company-1', name: 'SC Test', role: 'Proprietar' as const };
  const constructii = { id: 'cat-constructii', verticalId: 'v1', parentId: null, name: 'Construcții', slug: 'constructii' };
  const transport = { id: 'cat-transport', verticalId: 'v1', parentId: null, name: 'Transport și logistică', slug: 'transport-logistica' };

  const tender: SeapTender = {
    id: 'tender-1', seapExternalId: 'CN-1', title: 'Lucrări de construcții', description: null,
    cpvCodes: ['45200000-1'], cpvNames: ['Montare de garduri'], county: 'Cluj',
    categoryId: 'cat-constructii', category: { id: 'cat-constructii', name: 'Construcții' },
    estimatedValue: 1000, currency: 'RON', buyerName: 'Primăria Cluj',
    publicationDate: '2026-08-01T10:00:00Z', deadline: '2026-09-01T10:00:00Z',
    status: 'ReadyForBidding', proposalTitle: null, technicalProposalUrl: null, billOfQuantitiesUrl: null, attachments: []
  };

  const profiles = {
    getMyCompanies: vi.fn().mockReturnValue(of([company])),
    getSupplier: vi.fn().mockReturnValue(of({ county: 'Cluj', marketRadiusKm: 30 }))
  };
  const settings = {
    getSupplierCategories: vi.fn().mockReturnValue(of({ categoryIds: [constructii.id, transport.id] })),
    getCategories: vi.fn().mockReturnValue(of([constructii, transport]))
  };
  const specResult: SpecificationBidResult = {
    documentId: 'spec-1', fileName: 'caiet.pdf', documentUrl: '/storage/specs/caiet.pdf', markdown: '# Cerințe',
    proposalTitle: 'Ofertă tehnică — Lucrări de construcții', technicalProposalUrl: '/storage/bids/propunere.docx',
    billOfQuantitiesUrl: '/storage/bids/deviz.xlsx', shadowMode: true, generatedAt: '2026-08-26T16:00:00Z'
  };

  function setup(listTenders: ReturnType<typeof vi.fn>, isAdmin = true) {
    TestBed.configureTestingModule({
      imports: [SeapTendersComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profiles },
        { provide: SupplierSettingsService, useValue: settings },
        { provide: SeapService, useValue: { listTenders } },
        { provide: AuthService, useValue: { isAdministrator: computed(() => isAdmin) } },
        { provide: CompanyVaultService, useValue: { uploadSpecification: vi.fn(), generateFromSpecification: vi.fn(), downloadGeneratedFile: vi.fn() } }
      ]
    });
    return TestBed.createComponent(SeapTendersComponent);
  }

  it('shows SEAP filters only — the marketplace Category filter is not rendered', () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const fixture = setup(listTenders);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    // The SEAP page is isolated from the B2B Category entity: no product-category checkboxes.
    expect(text).not.toContain('Categoriile firmei');
    expect(text).not.toContain('Filtrează după categoriile firmei');
    expect(text).toContain('Filtre SEAP');
    expect(text).toContain('Doar anunțurile în raza de 30 km de Cluj');
    // The tender card shows the derived county and category.
    expect(text).toContain('Cluj');
    expect(text).toContain('Montare de garduri');
    // The first fetch carries only SEAP filter params (radius inactive, profile county resolved).
    expect(listTenders).toHaveBeenCalledWith({ radiusKm: null, county: 'Cluj', cpvPrefixes: [], counties: [] });
  });

  it('refetches with the new CPV prefix as soon as a prefix chip is added', () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const fixture = setup(listTenders);
    fixture.detectChanges();

    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input[aria-label="Adaugă prefix CPV"]')!;
    input.value = '45233000';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    const addButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Adaugă') && !button.disabled)!;
    addButton.click();
    fixture.detectChanges();

    // The feed is re-fetched immediately with the new query param.
    expect(listTenders).toHaveBeenLastCalledWith(expect.objectContaining({ cpvPrefixes: ['45233000'], counties: [] }));
  });

  it('refetches with the profile radius when the radius filter is enabled', () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const fixture = setup(listTenders);
    fixture.detectChanges();

    const radiusCheck = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('input[type="checkbox"]');
    const last = radiusCheck[radiusCheck.length - 1];
    last.click();
    fixture.detectChanges();

    expect(listTenders).toHaveBeenLastCalledWith({ radiusKm: 30, county: 'Cluj', cpvPrefixes: [], counties: [] });
  });

  it('still lists tenders when the user has no company (no filters shown)', () => {
    const profilesWithoutCompany = { getMyCompanies: vi.fn().mockReturnValue(of([])) };
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    TestBed.configureTestingModule({
      imports: [SeapTendersComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profilesWithoutCompany },
        { provide: SupplierSettingsService, useValue: settings },
        { provide: SeapService, useValue: { listTenders } },
        { provide: AuthService, useValue: { isAdministrator: computed(() => true) } },
        { provide: CompanyVaultService, useValue: { uploadSpecification: vi.fn(), generateFromSpecification: vi.fn(), downloadGeneratedFile: vi.fn() } }
      ]
    });
    const fixture = TestBed.createComponent(SeapTendersComponent);
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent ?? '').toContain('Lucrări de construcții');
    expect(listTenders).toHaveBeenCalled();
  });

  it('shows the Caiet de sarcini upload section to administrators', () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const admin = setup(listTenders, true);
    admin.detectChanges();
    expect((admin.nativeElement as HTMLElement).textContent ?? '').toContain('Caiet de sarcini');
  });

  it('hides the Caiet de sarcini upload section from non-administrators', () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const regular = setup(listTenders, false);
    regular.detectChanges();
    expect((regular.nativeElement as HTMLElement).textContent ?? '').not.toContain('Caiet de sarcini');
  });

  it('uploads a Caiet de sarcini and downloads the generated proposal files', () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const uploadSpecification = vi.fn().mockReturnValue(of(specResult));
    const generateFromSpecification = vi.fn();
    const getSpecificationProgress = vi.fn().mockReturnValue(of({ stage: 'Generating', message: 'Etapa 2/3 — Claude redactează…' }));
    const downloadGeneratedFile = vi.fn().mockReturnValue(of(new Blob(['content'])));
    // jsdom does not implement object URLs or crypto.randomUUID; stub them so the download path is exercised.
    Object.defineProperty(URL, 'createObjectURL', { value: vi.fn(() => 'blob:mock'), configurable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true });
    Object.defineProperty(crypto, 'randomUUID', { value: vi.fn(() => 'req-1234'), configurable: true });
    TestBed.configureTestingModule({
      imports: [SeapTendersComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profiles },
        { provide: SupplierSettingsService, useValue: settings },
        { provide: SeapService, useValue: { listTenders } },
        { provide: AuthService, useValue: { isAdministrator: computed(() => true) } },
        { provide: CompanyVaultService, useValue: { uploadSpecification, generateFromSpecification, getSpecificationProgress, downloadGeneratedFile } }
      ]
    });
    const fixture = TestBed.createComponent(SeapTendersComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.onSpecFileSelected(new File(['pdf'], 'caiet.pdf', { type: 'application/pdf' }));
    fixture.detectChanges();

    let buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'));
    buttons.find(button => button.textContent?.includes('Încarcă'))?.click();
    fixture.detectChanges();

    expect(uploadSpecification).toHaveBeenCalledWith(company.id, expect.any(File), 'req-1234');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Ofertă tehnică');
    expect(text).toContain('Descarcă propunerea tehnică (.docx)');
    expect(text).toContain('Descarcă lista de cantități (.xlsx)');

    buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'));
    expect(buttons.find(button => button.textContent?.includes('Descarcă propunerea tehnică'))?.disabled).toBe(true);

    const acceptance = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('.acceptance input');
    acceptance!.click();
    fixture.detectChanges();

    buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'));
    buttons.find(button => button.textContent?.includes('Descarcă propunerea tehnică'))?.click();
    expect(downloadGeneratedFile).toHaveBeenCalledWith(company.id, specResult.documentId, 'TechnicalProposal');
    buttons.find(button => button.textContent?.includes('Descarcă lista de cantități'))?.click();
    expect(downloadGeneratedFile).toHaveBeenCalledWith(company.id, specResult.documentId, 'BillOfQuantities');
  });

  it('shows a friendly error when the pipeline fails with a backend ProblemDetails detail', () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const errorResponse = { error: { detail: 'PDF-ul pare scanat ilizibil sau protejat (textul extras este insuficient).' } };
    const uploadSpecification = vi.fn().mockReturnValue(throwError(() => errorResponse));
    const getSpecificationProgress = vi.fn().mockReturnValue(of({ stage: 'Failed', message: 'PDF-ul pare scanat ilizibil sau protejat.' }));
    Object.defineProperty(crypto, 'randomUUID', { value: vi.fn(() => 'req-fail'), configurable: true });
    TestBed.configureTestingModule({
      imports: [SeapTendersComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profiles },
        { provide: SupplierSettingsService, useValue: settings },
        { provide: SeapService, useValue: { listTenders } },
        { provide: AuthService, useValue: { isAdministrator: computed(() => true) } },
        { provide: CompanyVaultService, useValue: { uploadSpecification, generateFromSpecification: vi.fn(), getSpecificationProgress, downloadGeneratedFile: vi.fn() } }
      ]
    });
    const fixture = TestBed.createComponent(SeapTendersComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.onSpecFileSelected(new File(['pdf'], 'scan.pdf', { type: 'application/pdf' }));
    fixture.detectChanges();

    const buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'));
    buttons.find(button => button.textContent?.includes('Încarcă'))?.click();
    fixture.detectChanges();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('scanat ilizibil sau protejat');
    expect(uploadSpecification).toHaveBeenCalledWith(company.id, expect.any(File), 'req-fail');
  });


  it('loads saved SEAP profile filters and passes them to the first fetch', async () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const profilesWithFilters = {
      getMyCompanies: vi.fn().mockReturnValue(of([company])),
      getSupplier: vi.fn().mockReturnValue(of({ county: 'Cluj', marketRadiusKm: 30, seapCpvPrefixes: ['45233000'], seapCounties: ['Cluj'] }))
    };
    TestBed.configureTestingModule({
      imports: [SeapTendersComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profilesWithFilters },
        { provide: SupplierSettingsService, useValue: settings },
        { provide: SeapService, useValue: { listTenders } },
        { provide: AuthService, useValue: { isAdministrator: computed(() => false) } },
        { provide: CompanyVaultService, useValue: { uploadSpecification: vi.fn(), generateFromSpecification: vi.fn(), downloadGeneratedFile: vi.fn() } }
      ]
    });
    const fixture = TestBed.createComponent(SeapTendersComponent);
    fixture.detectChanges();

    // Saved profile filters are active from the very first fetch (no re-entry into the chat).
    expect(listTenders).toHaveBeenLastCalledWith(expect.objectContaining({ cpvPrefixes: ['45233000'], counties: ['Cluj'] }));
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Filtre avansate');
    expect(text).toContain('45233000');
    expect(text).toContain('Configurează-ți potrivirea cu AI');
  });

  it('persists advanced filter chip edits to the company profile', async () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const saveMatchProfile = vi.fn().mockReturnValue(of({ companyId: company.id, cpvPrefixes: ['45233000'], counties: ['Cluj'] }));
    TestBed.configureTestingModule({
      imports: [SeapTendersComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profiles },
        { provide: SupplierSettingsService, useValue: settings },
        { provide: SeapService, useValue: { listTenders, saveMatchProfile } },
        { provide: AuthService, useValue: { isAdministrator: computed(() => false) } },
        { provide: CompanyVaultService, useValue: { uploadSpecification: vi.fn(), generateFromSpecification: vi.fn(), downloadGeneratedFile: vi.fn() } }
      ]
    });
    const fixture = TestBed.createComponent(SeapTendersComponent);
    fixture.detectChanges();

    const saveButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Salvează filtrele'))!;
    saveButton.click();
    fixture.detectChanges();

    expect(saveMatchProfile).toHaveBeenCalledWith(company.id, [], []);
    // Saving re-applies the persisted filters and re-fetches the feed immediately.
    expect(listTenders).toHaveBeenLastCalledWith(expect.objectContaining({ cpvPrefixes: ['45233000'], counties: ['Cluj'] }));
  });


  it('explains the active filters in the empty state', async () => {
    const listTenders = vi.fn().mockReturnValue(of([]));
    const profilesWithFilters = {
      getMyCompanies: vi.fn().mockReturnValue(of([company])),
      getSupplier: vi.fn().mockReturnValue(of({ county: 'Cluj', marketRadiusKm: 30, seapCpvPrefixes: [], seapCounties: ['Cluj'] }))
    };
    TestBed.configureTestingModule({
      imports: [SeapTendersComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profilesWithFilters },
        { provide: SupplierSettingsService, useValue: settings },
        { provide: SeapService, useValue: { listTenders } },
        { provide: AuthService, useValue: { isAdministrator: computed(() => false) } },
        { provide: CompanyVaultService, useValue: { uploadSpecification: vi.fn(), generateFromSpecification: vi.fn(), downloadGeneratedFile: vi.fn() } }
      ]
    });
    const fixture = TestBed.createComponent(SeapTendersComponent);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Nu există anunțuri potrivite.');
    expect(text).toContain('Filtre active: județe: Cluj');
  });

  it('adds a county to the active filters and does not offer already-selected ones in the dropdown', async () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const profilesWithFilters = {
      getMyCompanies: vi.fn().mockReturnValue(of([company])),
      getSupplier: vi.fn().mockReturnValue(of({ county: 'Cluj', marketRadiusKm: 30, seapCpvPrefixes: [], seapCounties: ['Cluj'] }))
    };
    TestBed.configureTestingModule({
      imports: [SeapTendersComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profilesWithFilters },
        { provide: SupplierSettingsService, useValue: settings },
        { provide: SeapService, useValue: { listTenders } },
        { provide: AuthService, useValue: { isAdministrator: computed(() => false) } },
        { provide: CompanyVaultService, useValue: { uploadSpecification: vi.fn(), generateFromSpecification: vi.fn(), downloadGeneratedFile: vi.fn() } }
      ]
    });
    const fixture = TestBed.createComponent(SeapTendersComponent);
    fixture.detectChanges();

    const select = (fixture.nativeElement as HTMLElement).querySelector<HTMLSelectElement>('select[aria-label="Alege județul de adăugat"]')!;
    const optionsBefore = Array.from(select.options).map(option => option.textContent?.trim());
    expect(optionsBefore).not.toContain('Cluj');
    expect(optionsBefore).toContain('Vâlcea');

    select.value = 'Iași';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    const addButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Adaugă') && !button.disabled)!;
    addButton.click();
    fixture.detectChanges();

    expect(listTenders).toHaveBeenLastCalledWith(expect.objectContaining({ counties: ['Cluj', 'Iași'] }));
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Județul „Iași” a fost adăugat la filtrele active.');
    const optionsAfter = Array.from(select.options).map(option => option.textContent?.trim());
    expect(optionsAfter).not.toContain('Iași');
  });

  it('refetches immediately when a saved county chip is removed', async () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const profilesWithFilters = {
      getMyCompanies: vi.fn().mockReturnValue(of([company])),
      getSupplier: vi.fn().mockReturnValue(of({ county: 'Cluj', marketRadiusKm: 30, seapCpvPrefixes: [], seapCounties: ['Cluj'] }))
    };
    TestBed.configureTestingModule({
      imports: [SeapTendersComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profilesWithFilters },
        { provide: SupplierSettingsService, useValue: settings },
        { provide: SeapService, useValue: { listTenders } },
        { provide: AuthService, useValue: { isAdministrator: computed(() => false) } },
        { provide: CompanyVaultService, useValue: { uploadSpecification: vi.fn(), generateFromSpecification: vi.fn(), downloadGeneratedFile: vi.fn() } }
      ]
    });
    const fixture = TestBed.createComponent(SeapTendersComponent);
    fixture.detectChanges();

    const removeButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.getAttribute('aria-label') === 'Șterge Cluj')!;
    removeButton.click();
    fixture.detectChanges();

    expect(listTenders).toHaveBeenLastCalledWith(expect.objectContaining({ counties: [], cpvPrefixes: [] }));
  });

  it('searches SEAP with the active parameters and the current-year window', async () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const fixture = setup(listTenders);
    fixture.detectChanges();

    const searchButton = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(button => button.textContent?.includes('Caută în SEAP'))!;
    expect(searchButton).toBeTruthy();
    searchButton.click();
    fixture.detectChanges();

    const year = new Date().getFullYear();
    expect(listTenders).toHaveBeenLastCalledWith(expect.objectContaining({
      search: true,
      from: `${year}-01-01`,
      to: `${year}-12-31`,
      cpvPrefixes: [],
      counties: []
    }));
  });
});
