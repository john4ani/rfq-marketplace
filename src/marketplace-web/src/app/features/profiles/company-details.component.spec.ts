import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ActivatedRoute } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { CompanyVaultService } from '../../core/companies/company-vault.service';
import { ProfileService, SupplierProfile } from '../../core/profiles/profile.service';
import { SupplierSettingsService } from '../../core/suppliers/supplier-settings.service';
import { CompanyDetailsComponent } from './company-details.component';

describe('CompanyDetailsComponent', () => {
  const company: SupplierProfile = {
    id: 'company-1', tradingName: 'SC Ion Balan', companyRegistrationNumber: 'J33/1/2026', taxIdentifier: null,
    contactPerson: 'Ion', contactEmail: 'ion@example.test', phoneNumber: '0712345678', website: null,
    address: 'Strada 1', city: 'Suceava', county: 'Suceava', country: 'RomÃ¢nia', latitude: null,
    longitude: null, marketRadiusKm: 30, description: null, approvalStatus: 'Approved', isActive: true
  };

  function mount() {
    TestBed.configureTestingModule({
      imports: [CompanyDetailsComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => 'company-1' } } } },
        { provide: ProfileService, useValue: { getSupplier: vi.fn().mockReturnValue(of(company)), saveSupplier: vi.fn().mockReturnValue(of({ ...company, marketRadiusKm: 50 })) } },
        { provide: AuthService, useValue: { session: () => null } },
        { provide: SupplierSettingsService, useValue: {
          getCategories: () => of([]),
          getSupplierCategories: () => of({ supplierId: 'company-1', categoryIds: [] }),
          getDeliverySettings: () => of({ normalDeliveryRadiusKm: 30, maximumDeliveryRadiusKm: 30, minimumOrderValue: null, nationwideDelivery: false, counties: [] })
        } },
        { provide: CompanyVaultService, useValue: { list: () => of([]) } }
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

    expect(text).toContain('RazÄƒ de interes pentru anunÈ›uri');
    expect(text).toContain('AnunÈ›uri SEAP');

    const input = (fixture.nativeElement as HTMLElement).querySelector<HTMLInputElement>('input[formControlName="marketRadiusKm"]');
    expect(input).toBeTruthy();
    input!.value = '50';
    input!.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const saveButton = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>('button'))
      .find(button => button.textContent?.includes('SalveazÄƒ raza de interes'));
    expect(saveButton).toBeTruthy();
    saveButton!.click();
    fixture.detectChanges();

    const profiles = TestBed.inject(ProfileService);
    expect(profiles.saveSupplier).toHaveBeenCalledWith('company-1', expect.objectContaining({ marketRadiusKm: 50 }));
  });
});
