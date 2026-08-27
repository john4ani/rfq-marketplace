import { registerLocaleData } from '@angular/common';
import localeRo from '@angular/common/locales/ro';
import { computed } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
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

  it('loads the company categories and renders them as checkbox filters', () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const fixture = setup(listTenders);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Construcții');
    expect(text).toContain('Transport și logistică');
    expect(text).toContain('Doar anunțurile în raza de 30 km de Cluj');
    // The tender card shows the derived county and category.
    expect(text).toContain('Cluj');
    expect(text).toContain('Montare de garduri');
    // The first fetch has no category selection and no radius (the county resolves on a later tick,
    // which is irrelevant while the radius filter is inactive).
    expect(listTenders).toHaveBeenCalledWith(expect.objectContaining({ categoryIds: [], radiusKm: null }));
  });

  it('refetches with the selected category ids when a checkbox is toggled', () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const fixture = setup(listTenders);
    fixture.detectChanges();

    const checks = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('input[type="checkbox"]');
    const firstCheck = Array.from(checks).find(check => check.id === undefined) ?? checks[0];
    firstCheck.click();
    fixture.detectChanges();

    expect(listTenders).toHaveBeenLastCalledWith({ categoryIds: [constructii.id], radiusKm: null, county: 'Cluj' });
  });

  it('refetches with the profile radius when the radius filter is enabled', () => {
    const listTenders = vi.fn().mockReturnValue(of([tender]));
    const fixture = setup(listTenders);
    fixture.detectChanges();

    const radiusCheck = (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('input[type="checkbox"]');
    const last = radiusCheck[radiusCheck.length - 1];
    last.click();
    fixture.detectChanges();

    expect(listTenders).toHaveBeenLastCalledWith({ categoryIds: [], radiusKm: 30, county: 'Cluj' });
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
    const downloadGeneratedFile = vi.fn().mockReturnValue(of(new Blob(['content'])));
    // jsdom does not implement object URLs; stub them so the download path is exercised.
    Object.defineProperty(URL, 'createObjectURL', { value: vi.fn(() => 'blob:mock'), configurable: true });
    Object.defineProperty(URL, 'revokeObjectURL', { value: vi.fn(), configurable: true });
    TestBed.configureTestingModule({
      imports: [SeapTendersComponent],
      providers: [
        provideRouter([]),
        { provide: ProfileService, useValue: profiles },
        { provide: SupplierSettingsService, useValue: settings },
        { provide: SeapService, useValue: { listTenders } },
        { provide: AuthService, useValue: { isAdministrator: computed(() => true) } },
        { provide: CompanyVaultService, useValue: { uploadSpecification, generateFromSpecification, downloadGeneratedFile } }
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

    expect(uploadSpecification).toHaveBeenCalledWith(company.id, expect.any(File));
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Ofertă tehnică');
    expect(text).toContain('Descarcă propunerea tehnică (.docx)');
    expect(text).toContain('Descarcă lista de cantități (.xlsx)');

    buttons = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'));
    buttons.find(button => button.textContent?.includes('Descarcă propunerea tehnică'))?.click();
    expect(downloadGeneratedFile).toHaveBeenCalledWith(company.id, specResult.documentId, 'TechnicalProposal');
    buttons.find(button => button.textContent?.includes('Descarcă lista de cantități'))?.click();
    expect(downloadGeneratedFile).toHaveBeenCalledWith(company.id, specResult.documentId, 'BillOfQuantities');
  });
});
