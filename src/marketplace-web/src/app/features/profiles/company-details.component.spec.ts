import { TestBed } from '@angular/core/testing';
import { computed } from '@angular/core';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { CompanyRegistryService } from '../../core/companies/company-registry.service';
import { CompanyVaultService } from '../../core/companies/company-vault.service';
import { FeaturesService } from '../../core/features/features.service';
import { ProfileService, SupplierProfile } from '../../core/profiles/profile.service';
import { SeapService } from '../../core/seap/seap.service';
import { SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { CompanyDetailsComponent } from './company-details.component';

describe('CompanyDetailsComponent', () => {
  const company: SupplierProfile = {
    id: 'company-1', tradingName: 'SC Ion Balan', companyRegistrationNumber: 'J33/1/2026', taxIdentifier: null,
    contactPerson: 'Ion', contactEmail: 'ion@example.test', phoneNumber: '0712345678', website: null,
    address: 'Strada 1', city: 'Suceava', county: 'Suceava', country: 'România', latitude: null,
    longitude: null, marketRadiusKm: 30, description: null, approvalStatus: 'Approved', isActive: true
  };

  function mount() {
    TestBed.configureTestingModule({
      imports: [CompanyDetailsComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'company-1' } } } },
        { provide: ProfileService, useValue: { getSupplier: vi.fn().mockReturnValue(of(company)), saveSupplier: vi.fn().mockReturnValue(of({ ...company, marketRadiusKm: 50 })) } },
        { provide: AuthService, useValue: { session: () => null, isAdministrator: computed(() => false) } },
        { provide: CompanyRegistryService, useValue: { list: () => of([]), grantFreeGeneration: vi.fn() } },
        { provide: SupplierSettingsService, useValue: {
          getCategories: () => of([]),
          getSupplierCategories: () => of({ supplierId: 'company-1', categoryIds: [] }),
          getDeliverySettings: () => of({ normalDeliveryRadiusKm: 30, maximumDeliveryRadiusKm: 30, minimumOrderValue: null, nationwideDelivery: false, counties: [] })
        } },
        { provide: CompanyVaultService, useValue: { list: () => of([]) } },
        // SEAP-first MVP: the marketplace is hidden, so Firma shows only the SEAP niche + interest radius.
        { provide: FeaturesService, useValue: { marketplaceEnabled: computed(() => false) } },
        { provide: SeapService, useValue: { saveMatchProfile: vi.fn().mockReturnValue(of({ companyId: 'company-1', cpvPrefixes: [], counties: [] })) } }
      ]
    });
    return TestBed.createComponent(CompanyDetailsComponent);
  }

  it('renders the company details with the vault and settings without crashing', () => {
    const fixture = mount();
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('SC Ion Balan');
  });

  it('shows the editable interest radius and saves it to the supplier profile', async () => {
    const fixture = mount();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('Rază de interes pentru anunțuri');
    expect(text).toContain('Anunțuri SEAP');

    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input[formControlName="marketRadiusKm"]');
    expect(input).toBeTruthy();
    input!.value = '50';
    input!.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const saveButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'))
      .find(button => button.textContent?.includes('Salvează raza de interes'));
    expect(saveButton).toBeTruthy();
    saveButton!.click();
    fixture.detectChanges();

    const profiles = TestBed.inject(ProfileService);
    expect(profiles.saveSupplier).toHaveBeenCalledWith('company-1', expect.objectContaining({ marketRadiusKm: 50 }));
  });

  it('hides marketplace sections and shows the SEAP niche (CPV + counties) when the marketplace is off', async () => {
    const companyWithSeap: SupplierProfile = {
      ...company,
      seapCpvPrefixes: ['45233000'], seapCounties: ['Cluj']
    };
    const saveMatchProfile = vi.fn().mockReturnValue(of({ companyId: 'company-1', cpvPrefixes: ['45233000', '45221111'], counties: ['Cluj', 'Vâlcea'] }));
    TestBed.configureTestingModule({
      imports: [CompanyDetailsComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'company-1' } } } },
        { provide: ProfileService, useValue: { getSupplier: vi.fn().mockReturnValue(of(companyWithSeap)), saveSupplier: vi.fn().mockReturnValue(of(companyWithSeap)) } },
        { provide: AuthService, useValue: { session: () => null, isAdministrator: computed(() => false) } },
        { provide: SupplierSettingsService, useValue: {
          getCategories: () => of([]),
          getSupplierCategories: () => of({ supplierId: 'company-1', categoryIds: [] }),
          getDeliverySettings: () => of({ normalDeliveryRadiusKm: 30, maximumDeliveryRadiusKm: 30, minimumOrderValue: null, nationwideDelivery: false, counties: [] })
        } },
        { provide: CompanyVaultService, useValue: { list: () => of([]) } },
        { provide: FeaturesService, useValue: { marketplaceEnabled: computed(() => false) } },
        { provide: SeapService, useValue: { saveMatchProfile } }
      ]
    });
    const fixture = TestBed.createComponent(CompanyDetailsComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    // Marketplace sections are hidden in the SEAP-first MVP…
    expect(text).not.toContain('Categorii de produse');
    expect(text).not.toContain('Zonă și rază de livrare');
    // …and the SEAP niche (CPV prefixes + counties, saved on the profile) replaces them.
    expect(text).toContain('Nișa ta SEAP');
    expect(text).toContain('45233000');
    expect(text).toContain('Cluj');

    const saveButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'))
      .find(button => button.textContent?.includes('Salvează nișa SEAP'))!;
    expect(saveButton).toBeTruthy();
    saveButton.click();
    fixture.detectChanges();

    expect(saveMatchProfile).toHaveBeenCalledWith('company-1', ['45233000'], ['Cluj']);
  });

  it('shows the free-generation grant action on the company profile for administrators and calls the registry', () => {
    const registeredEntry = {
      id: 'company-1', name: 'SC Ion Balan', kind: 'Registered' as const, claimed: true, locality: 'Suceava', county: 'Suceava',
      email: 'ion@example.test', phone: null, website: null, taxIdentifier: 'RO123', categories: ['Lemn și plăci'],
      relevantOpportunities: 2, activeOpportunities: 1, approvedMatches: 1, lastRelevantOpportunity: null, createdAt: '2026-08-22T00:00:00Z', contactState: null, isActive: true, freeGenerationsRemaining: 0
    };
    const grantFreeGeneration = vi.fn().mockReturnValue(of({ ...registeredEntry, freeGenerationsRemaining: 1 }));
    TestBed.configureTestingModule({
      imports: [CompanyDetailsComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'company-1' } } } },
        { provide: ProfileService, useValue: { getSupplier: vi.fn().mockReturnValue(of(company)), saveSupplier: vi.fn() } },
        { provide: AuthService, useValue: { session: () => null, isAdministrator: computed(() => true) } },
        { provide: CompanyRegistryService, useValue: { list: () => of([registeredEntry]), grantFreeGeneration } },
        { provide: SupplierSettingsService, useValue: {
          getCategories: () => of([]),
          getSupplierCategories: () => of({ supplierId: 'company-1', categoryIds: [] }),
          getDeliverySettings: () => of({ normalDeliveryRadiusKm: 30, maximumDeliveryRadiusKm: 30, minimumOrderValue: null, nationwideDelivery: false, counties: [] })
        } },
        { provide: CompanyVaultService, useValue: { list: () => of([]) } },
        { provide: FeaturesService, useValue: { marketplaceEnabled: computed(() => false) } },
        { provide: SeapService, useValue: { saveMatchProfile: vi.fn() } }
      ]
    });
    const fixture = TestBed.createComponent(CompanyDetailsComponent);
    fixture.detectChanges();

    const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Oferă o generare gratuită');
    expect(text).toContain('0');

    const button = [...fixture.nativeElement.querySelectorAll('button')].find((item: HTMLButtonElement) => item.textContent?.includes('Oferă o generare gratuită')) as HTMLButtonElement;
    button.click();
    fixture.detectChanges();

    expect(grantFreeGeneration).toHaveBeenCalledWith('company-1');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('1');
    confirmSpy.mockRestore();
  });
});
